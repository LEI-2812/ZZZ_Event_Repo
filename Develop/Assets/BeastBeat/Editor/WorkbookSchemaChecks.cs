using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace BeastBeat.Editor
{
    public static class WorkbookSchemaChecks
    {
        static int count;
        static void Check(bool ok, string label) { if(!ok) throw new Exception("Workbook check failed: " + label); count++; }
        static T Component<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(true)).First(c => !(c is Behaviour b) || b.enabled);
        static void Preview(string name, Action<Scene> check) {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/" + name + ".unity");
            try { check(scene); } finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        [MenuItem("Beast Beat/검증/엑셀 시트 연결 테스트")]
        public static void Run()
        {
            if(EditorApplication.isPlaying) throw new Exception("Stop Play Mode before preview checks.");
            count = 0;
            var source = GameWorkbook.Load(); var data = GameData.Load(source); data.Validate();
            Check(EventCatalog.Parse(source.ReadSheet("event_list")).Count == 10, "all current events load without enabled");
            Check(EventCatalog.Parse("id,category,updated_at,title,description,enabled\n1,permanent,2026-09-01,test,test,0").Count == 1, "legacy enabled ignored");
            Check(data.stage_list.Length == 14 && data.stage_list[0].name == "Random Play!", "NPC rows and team title");
            Check(data.stage_list.Sum(s => s.level_gain) == 19, "configured growth totals");
            Check(data.stage_list.Count(s => s.level_gain == 0) == 4, "zero growth stages accepted");
            Check(data.items.Length == 20 && data.stage_rewards.Length == 42, "items and stage rewards loaded from sheets");
            var book = ScriptableObject.CreateInstance<GameWorkbook>();
            book.sheets = source.sheets.Select(s => new GameWorkbook.Sheet { name = s.name, records = s.records }).ToArray();
            book.revision = Guid.NewGuid().ToString("N");
            var itemSheet = book.sheets.First(s => s.name.Equals("items", StringComparison.OrdinalIgnoreCase));
            itemSheet.records = "id,name,grade\n" + string.Join("\n", data.items.Select(i => i.id + ",XLSX Item " + i.id + "," + (i.id == 1 ? 3 : 4)));
            var partySheet = book.sheets.First(s => s.name.Equals("party", StringComparison.OrdinalIgnoreCase));
            partySheet.records = "id,uid,bid,is_on_field,hp,state,max_hp\n1,1,1,1,100,1,100";
            var modified = GameData.Load(book);
            Check(modified.items[0].name == "XLSX Item 1" && modified.items[0].grade == 3, "item edits override original JSON");
            Check(ItemCatalog.GradeColor(4) != ItemCatalog.GradeColor(3), "fourth grade has separate color");
            string validItems = itemSheet.records;
            itemSheet.records = "id,name,grade\n1,a,5";
            bool rejected = false; try { GameData.Load(book); } catch(FormatException) { rejected = true; }
            Check(rejected, "invalid grade rejected");
            itemSheet.records = "id,name,grade\n1,a,1\n1,b,2";
            rejected = false; try { GameData.Load(book); } catch(FormatException) { rejected = true; }
            Check(rejected, "duplicate item rejected");
            itemSheet.records = "id,name,grade\n999,a,1";
            rejected = false; try { GameData.Load(book); } catch(FormatException) { rejected = true; }
            Check(rejected, "missing reward item reference rejected");
            itemSheet.records = validItems;
            string path = Path.Combine(Application.temporaryCachePath, "BeastBeatSchema-" + Guid.NewGuid().ToString("N"), "save.json");
            var p = new ProgressService(modified, path);
            Check(p.Owns(1), "users flags initialize ownership without individual level columns");
            p.Save.level = 7; p.SyncPartyLevels();
            var party = BattleCatalog.Load(book).ForUser(p);
            Check(party.Length == 1 && party[0].bid == 1, "party loads without lv or experience columns");
            var battle = new BattleEngine(p, modified.stage_list[0].id, 3, party);
            Check(battle.Player.level == 7, "party level derives from player");
            var previousProgress = BeastBeatSession.Progress; var previousBattle = BeastBeatSession.Battle;
            bool previousPreview = BeastBeatSession.PreviewBattle;
            try {
                BeastBeatSession.Progress = p; BeastBeatSession.Battle = battle; BeastBeatSession.PreviewBattle = true;
                Preview("Event List Scene", s => {
                    var ui = Component<EventEntryActions>(s); ui.workbook = book; ui.ReloadWorkbook(); ui.ShowPermanent(); ui.SelectEvent(1);
                    Check(ui.VisibleCount == 5 && ui.rewardLabels[0].text == "XLSX Item 1", "event items update from workbook");
                    Check(ui.rewardImages[0].color == ItemCatalog.GradeColor(3), "event grade color");
                });
                Preview("Stage List Scene", s => {
                    var ui = Component<StageListActions>(s); ui.workbook = book; ui.ReloadWorkbook(); ui.ShowWarmingup(); ui.SelectStage(1);
                    Check(ui.stageTitle.text == "Random Play!" && ui.rewardLabels[0].text.StartsWith("XLSX Item 2"), "stage title and reward item source");
                    Check(ui.rewardImages[0].color == ItemCatalog.GradeColor(4), "stage grade color");
                });
                foreach(string name in new[]{"Reward List Scene", "Level Complete List Scene"}) Preview(name, s => {
                    var ui = Component<RewardScreenActions>(s); ui.workbook = book; ui.ReloadWorkbook();
                    var row = ui.infoScroll.content.GetComponentsInChildren<RewardListRow>().First();
                    Check(row.itemLabels[0].text.StartsWith("XLSX Item "), name + " item labels update");
                    Check(ui.specialItems.text.Contains("XLSX Item "), name + " special item labels update");
                });
                Preview("Battle Scene", s => {
                    var ui = Component<BattleSceneActions>(s); ui.workbook = book; ui.SendMessage("Awake");
                    Check(ui.Battle != null && ui.Battle.Player.level == 7, "battle initializes without XP sliders");
                    Check(!s.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Any(t => t.name == "Slider_Lv"), "removed sliders stay removed");
                });
                battle.Finished = battle.Won = true; BattleOutcome.Complete(battle, true);
                Preview("Clear Scene", s => {
                    var ui = Component<ClearSceneActions>(s); ui.workbook = book; ui.SendMessage("Awake");
                    Check(ui.DisplayedParty[0].lv == 7 && ui.levelLabels[0].text == "Lv. 7", "result level independent of party.lv");
                });
                p.Save.level = 1; p.Save.cleared.Clear(); p.SyncPartyLevels();
                foreach(var stage in modified.stage_list.OrderBy(s => s.id)) p.Victory(stage.id, p.Save.party);
                Check(p.Save.level == 20 && p.Save.cleared.Count == 14, "all configured clears reach level 20");
                int amount = p.Save.inventory.Sum(i => i.amount);
                foreach(var stage in modified.stage_list) p.Victory(stage.id, p.Save.party);
                Check(p.Save.level == 20 && p.Save.inventory.Sum(i => i.amount) == amount, "no repeated growth or rewards");
            } finally {
                BeastBeatSession.Progress = previousProgress; BeastBeatSession.Battle = previousBattle; BeastBeatSession.PreviewBattle = previousPreview;
                UnityEngine.Object.DestroyImmediate(book);
            }
            Debug.Log("BEAST_BEAT_WORKBOOK_SCHEMA_PASS " + count + " checks");
        }
    }
}
