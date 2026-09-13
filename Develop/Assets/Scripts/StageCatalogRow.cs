using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BeastBeat
{
    public sealed class StageCatalogRow : MonoBehaviour
    {
        public Button button;
        public TMP_Text titleLabel, stateLabel;
        public GameObject selectedMark;
        public Color normalColor = new Color(.075f, .12f, .2f), selectedColor = new Color(.9f, .88f, .32f);
        public Color normalText = Color.white, selectedText = new Color(.05f, .09f, .15f);
        [SerializeField] StageListActions owner;
        [SerializeField] int stageId;
        public int StageId => stageId;
        public void Bind(StageListActions controller, StageData stage, bool selected, bool open, bool cleared)
        {
            owner = controller; stageId = stage.id;
            string npc = stage.npc.Length > 10 ? stage.npc.Substring(0, 9) + "…" : stage.npc;
            titleLabel.text = "배틀! ‘" + npc + "’";
            stateLabel.text = cleared ? "클리어" : open ? "도전 가능" : "잠김";
            SetSelected(selected);
        }
        public void SetSelected(bool value)
        {
            if (button.targetGraphic) button.targetGraphic.color = value ? selectedColor : normalColor;
            titleLabel.color = value ? selectedText : normalText;
            stateLabel.color = value ? selectedText : new Color(.68f, .76f, .87f);
            if (selectedMark) selectedMark.SetActive(value);
        }
        public void SelectStage() { if (owner) owner.SelectStage(stageId); }
    }
}
