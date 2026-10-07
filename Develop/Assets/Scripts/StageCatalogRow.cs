using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BeastBeat
{
    public sealed class StageCatalogRow : MonoBehaviour
    {
        public Button button;
        public TMP_Text titleLabel;
        public GameObject selectedMark;
        public Color normalColor = new Color(.075f, .12f, .2f), selectedColor = new Color(.9f, .88f, .32f);
        public Color normalText = Color.white, selectedText = new Color(.05f, .09f, .15f);
        [SerializeField] StageListActions owner;
        [SerializeField] int stageId;
        public int StageId => stageId;
        public void Bind(StageListActions controller, StageData stage, bool selected)
        {
            owner = controller; stageId = stage.id;
            titleLabel.text = "배틀! " + stage.name;
            SetSelected(selected);
        }
        public void SetSelected(bool value)
        {
            if (button.targetGraphic) button.targetGraphic.color = value ? selectedColor : normalColor;
            titleLabel.color = value ? selectedText : normalText;
            if (selectedMark) selectedMark.SetActive(value);
        }
        public void SelectStage() { if (owner) owner.SelectStage(stageId); }
    }
}
