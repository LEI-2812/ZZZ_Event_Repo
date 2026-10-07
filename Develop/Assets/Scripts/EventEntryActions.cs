using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BeastBeat
{
    public sealed class EventEntryActions : MonoBehaviour
    {
        [Header("XLSX and editable list prefab")]
        public GameWorkbook workbook;
        public ScrollRect eventScroll;
        public EventCatalogRow rowPrefab;
        [Header("Existing scene objects")]
        public Button permanentButton, limitedButton, goEventButton;
        public TMP_Text titleText, descriptionText, statusText;
        public TMP_Text[] achievementTitles, achievementCounts, rewardLabels;
        public Image background;
        public Image[] rewardImages;


        [Header("Tab colors (no sprites)")]
        public Color activeTab = new Color(1f, .87f, .08f);
        private readonly Color inactiveTab = new Color32(0x16, 0x16, 0x16, 0xFF);
        int category;
        [SerializeField] int selectedId;
        string lastRevision;
        float nextRefresh;
        Sprite initialBackground;
        GameData data;
        List<EventCatalogEntry> entries = new List<EventCatalogEntry>();
        static int rememberedCategory;
        static int rememberedId;
        public int Category => category;
        public int SelectedId => selectedId;
        public int VisibleCount => entries.Count(e => e.category == category);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSelection() { rememberedCategory = 0; rememberedId = 0; }
        void Awake()
        {
            if (!enabled) return;
            initialBackground = background ? background.sprite : null;
            EnsureData(); category = rememberedCategory; selectedId = rememberedId;
            ReloadWorkbook();
        }
        void EnsureData()
        {
            if (data == null) data = GameData.Load();
            if (Application.isPlaying && BeastBeatSession.Progress == null)
            {
                var p = new ProgressService(data); BeastBeatSession.Progress = p;
                var stage = data.ResolveStage(p.Save.lastStage);
                BeastBeatSession.SelectedStage = stage == null ? 0 : stage.id;
                BeastBeatSession.StageGroup = stage == null ? 1 : stage.type;
            }
        }
        void Update()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + .5f;
            var source = workbook ? workbook : GameWorkbook.Load();
            if ((source ? source.revision : "missing") != lastRevision) ReloadWorkbook();
        }
        [ContextMenu("Reload Workbook / Update Scene Preview")]
        public void ReloadWorkbook()
        {
            var source = workbook ? workbook : GameWorkbook.Load();
            lastRevision = source ? source.revision : "missing";
            try
            {
                if(!source)throw new FormatException("game_data.xlsx를 찾을 수 없습니다.");
                data = GameData.Load(source); EnsureData(); var parsed = EventCatalog.Parse(source.ReadSheet("event_list"));
                foreach (var entry in parsed)
                {
                    if (entry.rewardIds.Any(id => !data.items.Any(item => item.id == id))) throw new FormatException("없는 보상 아이템 ID: 이벤트 " + entry.id);
                    if (entry.achievementIds.Any(id => !data.achievement.Any(a => a.id == id))) throw new FormatException("없는 업적 ID: 이벤트 " + entry.id);
                }
                entries = parsed; RefreshList(false);
            }
            catch (Exception ex) { goEventButton.interactable=false; Debug.LogError("이벤트 XLSX: " + ex.Message, this); if (descriptionText) descriptionText.text = "이벤트 데이터를 확인해 주세요.\n" + ex.Message; }
        }
        public void ShowPermanent() { SetCategory(0); }
        public void ShowLimited() { SetCategory(1); }
        void SetCategory(int value)
        {
            if (category != value) selectedId = 0;
            category = value; RefreshList(true);
        }
        void RefreshList(bool resetScroll)
        {
            if (!eventScroll || !rowPrefab) return;
            var visible = entries.Where(e => e.category == category).ToList();
            if (!visible.Any(e => e.id == selectedId)) selectedId = visible.Count > 0 ? visible[0].id : 0;
            // Only pool our row components. Other user-authored children are untouched.
            var rows = eventScroll.content.GetComponentsInChildren<EventCatalogRow>(true).ToList();
            while (rows.Count < visible.Count)
            {
                EventCatalogRow row;
                #if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    var go = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(rowPrefab.gameObject, eventScroll.content);
                    UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Add XLSX event row");
                    row = go.GetComponent<EventCatalogRow>();
                }
                else
                #endif
                row = Instantiate(rowPrefab, eventScroll.content);
                rows.Add(row);
            }
            for (int i = 0; i < rows.Count; i++)
            {
                rows[i].gameObject.SetActive(i < visible.Count);
                if (i < visible.Count) { rows[i].name = "Btn_Event_" + visible[i].id; rows[i].Bind(this, visible[i], visible[i].id == selectedId); }
            }
            if (permanentButton.targetGraphic) permanentButton.targetGraphic.color = category == 0 ? activeTab : inactiveTab;
            if (limitedButton.targetGraphic) limitedButton.targetGraphic.color = category == 1 ? activeTab : inactiveTab;

            permanentButton.GetComponentInChildren<TMP_Text>(true).color = category == 0 ? Color.black : Color.white;
            limitedButton.GetComponentInChildren<TMP_Text>(true).color = category == 1 ? Color.black : Color.white;

            goEventButton.interactable = visible.Count > 0;
            SelectEvent(selectedId);
            LayoutRebuilder.ForceRebuildLayoutImmediate(eventScroll.content);
            if (resetScroll) { eventScroll.StopMovement(); eventScroll.verticalNormalizedPosition = 1; }
            #if UNITY_EDITOR
            if (!Application.isPlaying) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            #endif
        }
        public void SelectEvent(int id)
        {
            var entry = entries.FirstOrDefault(e => e.id == id && e.category == category);
            selectedId = entry == null ? 0 : id;
            if (Application.isPlaying) { rememberedCategory = category; rememberedId = selectedId; }
            foreach (var row in eventScroll.content.GetComponentsInChildren<EventCatalogRow>(true)) row.SetSelected(row.EventId == selectedId);
            titleText.text = entry == null ? "등록된 이벤트가 없습니다" : entry.title;
            descriptionText.text = entry == null ? "XLSX에 이벤트를 추가해 주세요." : entry.description;
            statusText.text = category == 0 ? "리두기록" : "기간 한정";
            if (background)
            {
                if (!initialBackground) initialBackground = background.sprite;
                var sprite = entry != null && !string.IsNullOrEmpty(entry.background) ? Resources.Load<Sprite>(entry.background) : null;
                background.sprite = sprite ? sprite : initialBackground;
            }
            for (int i = 0; i < rewardImages.Length; i++)
            {
                var item = entry != null && i < entry.rewardIds.Length ? data.items.First(x => x.id == entry.rewardIds[i]) : null;
                // Keep the original image and sprite; placeholders use item names and grade colors.
                rewardImages[i].color = item == null ? new Color(.08f, .09f, .1f, .7f) : ItemCatalog.GradeColor(item.grade);
                if (i < rewardLabels.Length) rewardLabels[i].text = item == null ? "—" : ItemCatalog.DisplayName(item);
            }
            for (int i = 0; i < achievementTitles.Length; i++)
            {
                var achievement = entry != null && i < entry.achievementIds.Length ? data.achievement.First(a => a.id == entry.achievementIds[i]) : null;
                achievementTitles[i].text = achievement == null ? "—" : achievement.name;
                achievementCounts[i].text = achievement == null ? "—" : "조건 준비 중";
            }
        }
    }
}
