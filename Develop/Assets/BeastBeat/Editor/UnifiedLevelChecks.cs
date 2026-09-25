using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BeastBeat.Editor
{
    public static class UnifiedLevelChecks
    {
        [MenuItem("Beast Beat/검증/공통 레벨 성장 테스트")]
        public static void Run()
        {
            int checks = 0;
            Action<bool, string> check = (ok, label) => {
                if (!ok) throw new Exception("Unified level check failed: " + label);
                checks++;
            };
            string dir = Path.Combine(Application.temporaryCachePath, "BeastBeatUnifiedLevel-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var data = GameData.Load();
            var stages = data.stage_list.OrderBy(s => s.id).ToArray();
            var ids = data.bangboo.Take(4).Select(b => b.id).ToArray();
            data.initialOwnedBangbooIds = data.initialOwnedBangbooIds.Union(ids).ToArray();
            var p = new ProgressService(data, Path.Combine(dir, "save.json"));
            p.Save.owned = ids.Select(id => new OwnedBoo { id = id, level = 1, xp = 90 }).ToList();
            p.Save.party = ids.Take(3).ToList();
            p.Save.level = 5;
            p.Save.xp = 90;
            // Simulate an older save containing different levels and accumulated XP.
            p.Save.owned[0].level = 2;
            File.WriteAllText(Path.Combine(dir, "save.json"), JsonUtility.ToJson(p.Save));
            p = new ProgressService(data, Path.Combine(dir, "save.json"));
            check(p.Save.level == 5 && p.Save.owned.All(b => b.level == 5 && b.xp == 0) && p.Save.xp == 0, "legacy save keeps player level and synchronizes all owned Bangboo");

            var book = GameWorkbook.Load();
            var catalog = BattleCatalog.Load(book);
            check(catalog.party.Length > 0, "party loads without experience columns");
            var collection = BangbooCatalog.Load(book, data.rewardCatalog.user.id);
            check(ids.All(id => collection.Level(p, id) == 5), "collection ignores individual workbook levels");
            p.Save.configuredParty = ids.Take(3).ToArray();
            check(catalog.ForUser(p).Length == 3, "configured party loads without lv");

            // Exercise per-stage values through the actual workbook parser, not just a field assignment.
            string csv = string.Join(",", StageCatalog.Headers) + ",level_gain\n" +
                stages[0].id + ",1,1,NPC,hello,1,1\n" + stages[1].id + ",1,2,NPC,hello,1,3\n";
            StageCatalog.Apply(data, StageCatalog.Parse(csv));
            check(data.Stage(stages[1].id).level_gain == 3, "stage-specific workbook gain applied");
            foreach (string invalid in new[] { "-1", "1.5", "" }) {
                bool rejected = false;
                try { StageCatalog.Parse(string.Join(",", StageCatalog.Headers) + ",level_gain\n1,1,1,NPC,hello,1," + invalid + "\n"); }
                catch (FormatException) { rejected = true; }
                check(rejected, "reject invalid gain: " + invalid);
            }
            var party = catalog.ForUser(p);
            var battle = new BattleEngine(p, stages[0].id, 1, party);
            check(battle.player.All(f => f.level == 5), "battle ignores per-Bangboo level overrides");
            check(battle.enemy.All(f => f.level == data.Stage(stages[0].id).level), "opponent keeps stage level");
            battle.Finished = battle.Won = true;
            var outcome = BattleOutcome.Complete(battle, false);
            check(p.Save.level == 6 && p.Save.owned.All(b => b.level == 6), "first clear +1 including reserve Bangboo");
            check(outcome.Party.All(b => b.lv == 6 && b.leveledUp), "result labels reflect shared level");
            int inventory = p.Save.inventory.Sum(i => i.amount);
            check(ReferenceEquals(outcome, BattleOutcome.Complete(battle, false)) && p.Save.level == 6 && p.Save.inventory.Sum(i => i.amount) == inventory, "same outcome cannot grant twice");
            p.Victory(stages[0].id, p.Save.party);
            check(p.Save.level == 6 && p.Save.inventory.Sum(i => i.amount) == inventory, "reclear grants no levels or duplicate rewards");

            var lost = new BattleEngine(p, stages[1].id, 2) { Finished = true, Won = false };
            BattleOutcome.Complete(lost, false);
            check(p.Save.level == 6 && !p.Save.cleared.Contains(stages[1].id), "defeat grants nothing");
            var preview = new BattleEngine(p, stages[1].id, 2) { Finished = true, Won = true };
            BattleOutcome.Complete(preview, true);
            check(p.Save.level == 6 && !p.Save.cleared.Contains(stages[1].id), "practice grants nothing");
            p.Victory(stages[1].id, p.Save.party);
            check(p.Save.level == 9 && p.Save.owned.All(b => b.level == 9), "different stage grants +3");
            var loaded = new ProgressService(data, Path.Combine(dir, "save.json"));
            check(loaded.Save.level == 9 && loaded.Save.owned.All(b => b.level == 9) && loaded.Save.cleared.Count == 2, "save/reload preserves growth and first-clear records");
            loaded.Victory(stages[1].id, loaded.Save.party);
            check(loaded.Save.level == 9, "reclear after reload grants nothing");
            loaded.Save.level = data.balance.maxLevel - 1;
            loaded.Save.cleared.Remove(stages[1].id);
            loaded.Victory(stages[1].id, loaded.Save.party);
            check(loaded.Save.level == data.balance.maxLevel && loaded.Save.owned.All(b => b.level == data.balance.maxLevel), "multi-level gain clamps at cap");
            check(loaded.CanClaim("l" + data.balance.maxLevel), "level reward eligibility uses shared player level");
            Debug.Log("BEAST_BEAT_UNIFIED_LEVEL_PASS " + checks + " checks");
        }
    }
}
