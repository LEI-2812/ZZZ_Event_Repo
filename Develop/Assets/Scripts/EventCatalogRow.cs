using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BeastBeat
{
    // Button / Image / TMP objects are authored in the prefab, not drawn here.
    public sealed class EventCatalogRow : MonoBehaviour
    {
        public Button button;
        public TMP_Text titleLabel;
        public GameObject selectionMark;
        public Color normalColor = new Color(.09f, .1f, .12f, .92f);
        public Color selectedColor = new Color(1f, .87f, .08f, 1f);
        public Color normalText = Color.white, selectedText = Color.black;
        [SerializeField] EventEntryActions owner;
        [SerializeField] int eventId;
        public int EventId => eventId;
        public void Bind(EventEntryActions controller, EventCatalogEntry entry, bool selected)
        {
            owner = controller; eventId = entry.id;
            titleLabel.text = entry.title;
            SetSelected(selected);
        }
        public void SetSelected(bool selected)
        {
            if (button.targetGraphic) button.targetGraphic.color = selected ? selectedColor : normalColor;
            titleLabel.color = selected ? selectedText : normalText;
            if (selectionMark) selectionMark.SetActive(selected);
        }
        public void SelectEvent() { if (owner) owner.SelectEvent(eventId); }
    }
}
