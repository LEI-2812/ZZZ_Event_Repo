using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BeastBeat
{
    public sealed class StageListActions : MonoBehaviour
    {
        [Header("XLSX and list prefab")]
        public GameWorkbook workbook;
        public int eventId = 1;
        public ScrollRect stageScroll;
        public StageCatalogRow rowPrefab;
        [Header("Existing scene objects")]
        public Button[] groupButtons;
        public Button battleButton;
        public TMP_Text stageTitle, npcDialogue, npcName, statusText, clearRewardTitle;
        public Image npcPortrait, npcElement;
        public Image[] goodElements, badElements, rewardImages;
        public TMP_Text npcElementLabel;
        public TMP_Text[] rewardLabels;


        [Header("Selection colors")]
        public Color activeTab = new Color(.2f, .72f, 1f), inactiveTab = Color.white;
        public Color inactiveElement = new Color(.35f, .37f, .4f);
        [SerializeField] int group = 1, selectedId = 1;
        GameData data;
        string lastRevision;
        float nextRefresh;
        bool valid;
        public int Group => group;
        public int SelectedId => selectedId;
        public int VisibleCount => data == null ? 0 : data.stage_list.Count(s => s.event_list_id == eventId && s.type == group);
        void Awake()
        {
            if (!enabled) return;
            if (BeastBeatSession.Progress == null)
            {
                BeastBeatSession.Progress = new ProgressService(GameData.Load());
                var saved = BeastBeatSession.Progress.Data.ResolveStage(BeastBeatSession.Progress.Save.lastStage);
                BeastBeatSession.SelectedStage = saved == null ? 0 : saved.id;
                BeastBeatSession.StageGroup = saved == null ? 1 : saved.type;
            }
            if (BeastBeatSession.SelectedEvent > 0) eventId = BeastBeatSession.SelectedEvent;
            selectedId = BeastBeatSession.SelectedStage;
            if (selectedId <= 0) selectedId = BeastBeatSession.Progress.Save.lastStage;
            group = BeastBeatSession.StageGroup;

            int requestedId = selectedId;
            ReloadWorkbook();
            var last = data == null ? null : data.stage_list.FirstOrDefault(s => s.id == requestedId && s.event_list_id == eventId);
            if (last != null) { group = last.type; selectedId = last.id; RefreshList(true); }
        }
        void Update()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + .5f;
            var source = workbook ? workbook : GameWorkbook.Load();
            if ((source ? source.revision : "missing") != lastRevision) ReloadWorkbook();
        }
        System.Collections.IEnumerator Start()
        {
            // ScrollRect caches its initial bounds in its first layout pass.
            // Restore an off-screen selection after that pass, including scene returns.
            yield return null;
            if (valid) { Canvas.ForceUpdateCanvases(); RefreshList(true); }
        }
        [ContextMenu("Reload Workbook / Update Scene Preview")]
        public void ReloadWorkbook()
        {
            var source = workbook ? workbook : GameWorkbook.Load();
            lastRevision = source ? source.revision : "missing";
            try
            {
                var fresh = GameData.Load(source);
                fresh.Validate();
                data = fresh; valid = true;
                if (Application.isPlaying)
                {
                    BeastBeatSession.Progress.ReloadData(fresh);
                }
                RefreshList(false);
            }
            catch (Exception ex) { valid = false; battleButton.interactable = false; statusText.text = "XLSX 확인: " + ex.Message; Debug.LogError("Stage XLSX: " + ex.Message, this); }
        }
        public void ShowWarmingup() { ChangeGroup(1); }
        public void ShowPre() { ChangeGroup(2); }
        public void ShowTournament() { ChangeGroup(3); }
        public void ShowFinal() { ChangeGroup(4); }
        void ChangeGroup(int value)
        {
            if (!valid) return;
            if (group != value) selectedId = 0;
            group = value; RefreshList(true);
        }
        void RefreshList(bool resetScroll)
        {
            if (!stageScroll || !rowPrefab) return;
            var visible = data.stage_list.Where(s => s.event_list_id == eventId && s.type == group).OrderBy(s => s.id).ToArray();
            if (!visible.Any(s => s.id == selectedId)) selectedId = visible.Length == 0 ? 0 : visible[0].id;
            var rows = stageScroll.content.GetComponentsInChildren<StageCatalogRow>(true).ToList();
            while (rows.Count < visible.Length)
            {
                StageCatalogRow row;
                #if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    var go = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(rowPrefab.gameObject, stageScroll.content);
                    UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Add XLSX stage row"); row = go.GetComponent<StageCatalogRow>();
                }
                else
                #endif
                row = Instantiate(rowPrefab, stageScroll.content);
                rows.Add(row);
            }
            for (int i = 0; i < rows.Count; i++)
            {
                rows[i].gameObject.SetActive(i < visible.Length);
                if (i < visible.Length)
                {
                    var stage = visible[i]; rows[i].name = "Btn_Stage_" + stage.id;
                    bool open = !Application.isPlaying || BeastBeatSession.Progress.StageOpen(stage.id);
                    bool cleared = Application.isPlaying && BeastBeatSession.Progress.Save.cleared.Contains(stage.id);
                    rows[i].Bind(this, stage, stage.id == selectedId, open, cleared);
                }
            }
            for (int i = 0; i < groupButtons.Length; i++) groupButtons[i].targetGraphic.color = i + 1 == group ? activeTab : inactiveTab;
            SelectStage(selectedId);
            LayoutRebuilder.ForceRebuildLayoutImmediate(stageScroll.content);
            if (resetScroll)
            {
                stageScroll.StopMovement(); stageScroll.verticalNormalizedPosition = 1;
                // Keep the restored/selected row inside the viewport.
                int index = Array.FindIndex(visible, s => s.id == selectedId);
                if (index >= 0 && stageScroll.content.rect.height > stageScroll.viewport.rect.height)
                {
                    var rt = (RectTransform)rows[index].transform;
                    float bottom = -rt.anchoredPosition.y + rt.rect.height * rt.pivot.y;
                    float offset = Mathf.Max(0, bottom - stageScroll.viewport.rect.height);
                    stageScroll.verticalNormalizedPosition = 1 - Mathf.Clamp01(offset / (stageScroll.content.rect.height - stageScroll.viewport.rect.height));
                }
            }
            #if UNITY_EDITOR
            if (!Application.isPlaying) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            #endif
        }
        public void SelectStage(int id)
        {
            if (!valid) return;
            var stage = data.stage_list.FirstOrDefault(s => s.id == id && s.event_list_id == eventId && s.type == group);
            selectedId = stage == null ? 0 : stage.id;
            foreach (var row in stageScroll.content.GetComponentsInChildren<StageCatalogRow>(true)) row.SetSelected(row.StageId == selectedId);
            if (Application.isPlaying && stage != null) { BeastBeatSession.SelectedStage = stage.id; BeastBeatSession.StageGroup = group; }
            stageTitle.text = stage == null ? "등록된 스테이지가 없습니다" : stage.name;
            npcName.text = stage == null ? "—" : stage.npc;
            npcName.gameObject.SetActive(!npcPortrait.sprite);
            string dialogue = stage == null ? "" : stage.npc_dialogue.Replace("\r", " ").Replace("\n", " ");
            npcDialogue.text = dialogue.Length > 30 ? "“" + dialogue.Substring(0, 29) + "…”" : "“" + dialogue + "”";
            npcElement.color = stage == null ? inactiveElement : ElementColor(stage.npc_type);
            npcElementLabel.text = stage == null ? "—" : Elements.Name(stage.npc_type);
            for (int i = 0; i < 5; i++)
            {
                goodElements[i].color = stage != null && Elements.Strong(i + 1, stage.npc_type) ? ElementColor(i + 1) : inactiveElement;
                badElements[i].color = stage != null && Elements.Strong(stage.npc_type, i + 1) ? ElementColor(i + 1) : inactiveElement;
            }
            var rewards = stage == null ? Array.Empty<RewardData>() : data.stage_rewards.Where(r => r.owner_id == stage.id).Take(4).ToArray();
            for (int i = 0; i < rewardImages.Length; i++)
            {
                var reward = i < rewards.Length ? rewards[i] : null;
                var item = reward == null ? null : data.items.First(x => x.id == reward.items_id);
                rewardLabels[i].text = item == null ? "—" : ItemCatalog.DisplayName(item) + "\n×" + reward.amount;
                rewardImages[i].color = item == null ? new Color(.2f, .22f, .25f) : ItemCatalog.GradeColor(item.grade);
            }
            bool ready = stage != null;
            statusText.text = stage == null ? "이 분류에 스테이지를 추가해 주세요." : "";
            clearRewardTitle.text = "클리어 보상";
            if (stage != null && Application.isPlaying)
            {
                var p = BeastBeatSession.Progress;
                if (!p.Eligible) { ready = false; statusText.text = "이벤트 참여 조건을 확인해 주세요."; }

                else if (!p.StageOpen(stage.id)) { ready = false; statusText.text = "앞선 스테이지를 클리어하면 도전할 수 있습니다."; }
            }
            battleButton.interactable = ready;
        }
        static Color ElementColor(int type) { ColorUtility.TryParseHtmlString("#" + Elements.Hex(type), out var color); return color; }
        public void GoBack() => Move_Scene.For(this).GoBack();
        public void StartBattle() => Move_Scene.For(this).StartBattle();
        // 화면은 선택 상태와 배틀 준비만 담당하며 실제 씬 로드는 Move_Scene에서 처리합니다.
        public bool TryPrepareBattle(out string message)
        {
            message = "";
            if (!valid || selectedId == 0) { message = "유효한 스테이지를 선택하세요."; return false; }
            var p = BeastBeatSession.Progress;
            try
            {
                if (!p.Eligible || !p.StageOpen(selectedId)) throw new InvalidOperationException("스테이지 참여 조건을 확인하세요.");
                var battle = BattleCatalog.CreateBattle(p, selectedId);
                int oldStage = p.Save.lastStage;
                p.Save.lastStage = selectedId;
                if (!p.TryPersist()) { p.Save.lastStage = oldStage; throw new InvalidOperationException(p.StorageWarning); }
                BeastBeatSession.Battle = battle;
                BeastBeatSession.SelectedStage = selectedId; BeastBeatSession.StageGroup = group;
                BeastBeatSession.PreviewBattle = false;
                return true;
            }
            catch (Exception ex) { message = ex.Message; statusText.text = message; return false; }
        }
    }
}
