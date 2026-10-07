using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BeastBeat
{
    public sealed class ExitConfirmation : MonoBehaviour
    {
        GameObject overlay, previousSelection;
        Button cancel;
        TMP_FontAsset font;
        bool open;
        float previousTimeScale;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            var host = new GameObject("Exit Confirmation");
            DontDestroyOnLoad(host);
            host.AddComponent<ExitConfirmation>();
        }
        void Awake()
        {
            font = Resources.Load<TMP_FontAsset>("Fonts/NanumSquareNeoOTF-Hv SDF");
            Build();
            SceneManager.activeSceneChanged += SceneChanged;
        }
        void Update()
        {
            if (SceneManager.GetActiveScene().name == "Battle Scene") return;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (open) Close(); else Show();
            }
        }
        void SceneChanged(Scene previous, Scene next) => Close();
        void OnDestroy()
        {
            SceneManager.activeSceneChanged -= SceneChanged;
            if (open) Time.timeScale = previousTimeScale;
        }
        public void Show()
        {
            if (open || SceneManager.GetActiveScene().name == "Battle Scene") return;
            previousTimeScale = Time.timeScale;
            previousSelection = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            open = true;
            Time.timeScale = 0;
            overlay.SetActive(true);
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(cancel.gameObject);
        }
        public void Close()
        {
            if (!open) return;
            open = false;
            Time.timeScale = previousTimeScale;
            overlay.SetActive(false);
            if (EventSystem.current)
                EventSystem.current.SetSelectedGameObject(previousSelection && previousSelection.activeInHierarchy ? previousSelection : null);
        }
        void Quit()
        {
#if UNITY_EDITOR
            Close();
            UnityEditor.EditorApplication.isPlaying = false;
#elif !UNITY_WEBGL
            Application.Quit();
#endif
        }
        RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }
        void Label(Transform parent, string text, Vector2 size, Vector2 position, float fontSize)
        {
            var label = Rect("Label", parent, size, position).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
        }
        Button MakeButton(Transform parent, string text, float x, Color color)
        {
            var rect = Rect(text, parent, new Vector2(200, 65), new Vector2(x, -75));
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            Label(rect, text, new Vector2(190, 60), Vector2.zero, 27);
            return button;
        }
        void Build()
        {
            overlay = new GameObject("Exit Overlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            overlay.transform.SetParent(transform, false);
            var canvas = overlay.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32766;
            var scaler = overlay.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var shade = Rect("Dim Background", overlay.transform, Vector2.zero, Vector2.zero);
            shade.anchorMin = Vector2.zero;
            shade.anchorMax = Vector2.one;
            shade.offsetMin = shade.offsetMax = Vector2.zero;
            shade.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, .7f);
            var panel = Rect("Panel", overlay.transform, new Vector2(580, 290), Vector2.zero);
            panel.gameObject.AddComponent<Image>().color = new Color32(35, 35, 35, 255);
            Label(panel, "게임 종료", new Vector2(530, 60), new Vector2(0, 85), 36);
#if UNITY_WEBGL && !UNITY_EDITOR
            Label(panel, "게임을 종료하려면 브라우저 탭을 닫아 주세요.", new Vector2(530, 80), Vector2.zero, 24);
            cancel = MakeButton(panel, "돌아가기", 0, new Color32(66, 66, 66, 255));
#else
            Label(panel, "게임을 종료하시겠습니까?", new Vector2(530, 60), Vector2.zero, 27);
            cancel = MakeButton(panel, "취소", -115, new Color32(66, 66, 66, 255));
            MakeButton(panel, "종료", 115, new Color32(246, 70, 116, 255)).onClick.AddListener(Quit);
#endif
            cancel.onClick.AddListener(Close);
            overlay.SetActive(false);
        }
    }
}