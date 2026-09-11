using System;
using UnityEngine;
using UnityEngine.UI;

namespace BeastBeat
{
    [DisallowMultipleComponent]
    public sealed class EditableUINode : MonoBehaviour
    {
        [HideInInspector] public string bindingKey;
        [Tooltip("이 오브젝트의 문구를 게임 데이터 대신 Inspector에 입력한 값으로 고정합니다.")]
        public bool overrideText;
        [Tooltip("이미지/초상화를 Inspector에 지정한 텍스처로 고정합니다.")]
        public bool overrideTexture;
        [HideInInspector] public bool authored;
        [HideInInspector] public string sourceText;
        [HideInInspector] public Color sourceColor;
        [HideInInspector] public Vector2 sourceSize;
        [NonSerialized] public bool touched;
        [NonSerialized] bool captured;
        [NonSerialized] bool customText,customColor;
        [NonSerialized] Vector2 sizeDelta;
        [NonSerialized] UnityEngine.Events.UnityAction runtimeClick;

        void CaptureOverrides()
        {
            if(captured)return;captured=true;
            var text=GetComponent<Text>();var graphic=GetComponent<Graphic>();
            customText=authored&&text&&text.text!=sourceText;
            customColor=authored&&graphic&&graphic.color!=sourceColor;
            sizeDelta=authored?((RectTransform)transform).sizeDelta-sourceSize:Vector2.zero;
        }
        public void TextValue(string value)
        {
            CaptureOverrides();var text=GetComponent<Text>();
            if(!overrideText&&!customText)text.text=value;
        }
        public void ColorValue(Color value)
        {
            CaptureOverrides();if(!customColor)GetComponent<Graphic>().color=value;
        }
        public void DynamicSize(Vector2 value)
        {
            CaptureOverrides();((RectTransform)transform).sizeDelta=value+sizeDelta;
        }
        public void Bind(Button button,Action action)
        {
            if(runtimeClick!=null)button.onClick.RemoveListener(runtimeClick);
            runtimeClick=()=>action();button.onClick.AddListener(runtimeClick);
        }
        public void SaveDefaults()
        {
            var text=GetComponent<Text>();var graphic=GetComponent<Graphic>();
            sourceText=text?text.text:null;sourceColor=graphic?graphic.color:Color.white;
            sourceSize=((RectTransform)transform).sizeDelta;authored=true;captured=false;
        }
    }
}
