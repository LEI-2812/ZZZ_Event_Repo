using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BeastBeat
{
    public sealed class RewardListRow:MonoBehaviour
    {
        public TMP_Text titleLabel,progressLabel,buttonLabel;
        public Button actionButton;
        public Image background;
        public GameObject selectedMark;
        public Image[] itemImages;
        public TMP_Text[] itemLabels;
        [SerializeField] RewardScreenActions owner;
        [SerializeField] int groupId;
        [SerializeField] string claimKey;
        public string ClaimKey=>claimKey;
        public int GroupId=>groupId;
        public void Group(RewardScreenActions screen,int id,string title,string count,bool selected)
        {
            owner = screen; groupId = id; claimKey = ""; titleLabel.text = title;
            EnsureGroupProgress();
            if (progressLabel) progressLabel.text = count;

            ColorUtility.TryParseHtmlString("#FFD700", out Color selectedColor);
            ColorUtility.TryParseHtmlString("#424242", out Color normalColor);
            background.color = selected ? selectedColor : normalColor;

            titleLabel.color = selected ? new Color(.05f, .09f, .15f) : Color.white;
            if (progressLabel) progressLabel.color = titleLabel.color;
            if (selectedMark)selectedMark.SetActive(selected);actionButton.interactable=true;
        }
        void EnsureGroupProgress()
        {
            if (progressLabel) return;
            progressLabel = Instantiate(titleLabel, titleLabel.transform.parent);
            progressLabel.name = "Txt_RewardCount";
            progressLabel.raycastTarget = false;
            progressLabel.fontStyle = FontStyles.Bold;
            progressLabel.alignment = TextAlignmentOptions.Center;
            progressLabel.enableAutoSizing = true;
            progressLabel.fontSizeMin = 12;
            progressLabel.fontSizeMax = titleLabel.fontSize;
            progressLabel.gameObject.SetActive(true);

            var titleRect = titleLabel.rectTransform;
            titleRect.anchorMin = new Vector2(0, .48f);
            titleRect.anchorMax = Vector2.one;
            titleRect.offsetMin = new Vector2(12, 0);
            titleRect.offsetMax = new Vector2(-12, -4);
            titleLabel.alignment = TextAlignmentOptions.Center;

            var countRect = progressLabel.rectTransform;
            countRect.anchorMin = Vector2.zero;
            countRect.anchorMax = new Vector2(1, .48f);
            countRect.offsetMin = new Vector2(12, 5);
            countRect.offsetMax = new Vector2(-12, 0);
        }
        public void Reward(RewardScreenActions screen,string key,string title,RewardData[] items,string progress,bool enabled,bool claimed)
        {
            owner = screen; claimKey = key; titleLabel.text = title;
            if (progressLabel) progressLabel.text = progress;
            actionButton.interactable = enabled;
            buttonLabel.text=claimed?"수령 완료":enabled?"수령하기":"진행 중";
            ColorUtility.TryParseHtmlString("#424242", out Color normalColor);
            if (background) background.color = normalColor;

            for (int i = 0; i < itemImages.Length; i++)
            {
                var image = itemImages[i];
                var label = i < itemLabels.Length ? itemLabels[i] : null;
                bool visible = i < 3 && i < items.Length;
                if (image) image.gameObject.SetActive(visible);
                if (label) label.gameObject.SetActive(visible);
                if (!visible) continue;
                var definition = System.Array.Find(screen.Data.items, d => d.id == items[i].items_id);
                if (definition == null) continue;
                var sprite = Resources.Load<Sprite>("Image/Items/" + definition.id);
                if (image)
                {
                    image.sprite = sprite;
                    image.preserveAspect = true;
                    image.color = sprite ? Color.white : ItemCatalog.GradeColor(definition.grade);
                }
                if (label) label.text = sprite ? "×" + items[i].amount
                    : ItemCatalog.DisplayName(definition) + "\n×" + items[i].amount;
            }
        }
        public void InvokeAction(){if(!owner)return;if(string.IsNullOrEmpty(claimKey))owner.SelectGroup(groupId);else owner.ClaimRow(claimKey);}
    }
}
