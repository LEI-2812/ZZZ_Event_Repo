using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BeastBeat
{
    // Keeps the authored 1600x900 UI and game cameras inside the same viewport.
    // Created at runtime so scene assets and persistent button references stay intact.
    [DefaultExecutionOrder(10000)]
    public sealed class FixedAspectDisplay : MonoBehaviour
    {
        const float ReferenceWidth = 1600f, ReferenceHeight = 900f;
        readonly List<Canvas> canvases = new List<Canvas>();
        readonly List<Camera> cameras = new List<Camera>();
        readonly List<Rect> cameraRects = new List<Rect>();
        readonly List<RectTransform> frames = new List<RectTransform>();
        RectTransform[] bars;
        int lastWidth, lastHeight;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            var host = new GameObject("Fixed 16x9 Display");
            DontDestroyOnLoad(host);
            host.AddComponent<FixedAspectDisplay>();
        }

        void Awake()
        {
            CreateBars();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDestroy() => SceneManager.sceneLoaded -= OnSceneLoaded;

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ConfigureScene(scene, Screen.width, Screen.height);
        }

        public void ConfigureScene(Scene scene, int width, int height)
        {
            if (bars == null) CreateBars();
            BindScene(scene);
            ApplySize(width, height);
        }

        void BindScene(Scene scene)
        {
            // Remove references to unloaded scenes, retaining additive scenes.
            for (int i = canvases.Count - 1; i >= 0; i--)
                if (!canvases[i]) { canvases.RemoveAt(i); frames.RemoveAt(i); }
            for (int i = cameras.Count - 1; i >= 0; i--)
                if (!cameras[i]) { cameras.RemoveAt(i); cameraRects.RemoveAt(i); }

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
                {
                    if (canvas.transform.IsChildOf(transform) || !canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay ||
                        canvas.targetDisplay != 0 || canvases.Contains(canvas)) continue;
                    var children = new List<Transform>();
                    foreach (Transform child in canvas.transform) children.Add(child);
                    var frame = new GameObject("16x9 Content", typeof(RectTransform)).GetComponent<RectTransform>();
                    frame.SetParent(canvas.transform, false);
                    frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(.5f, .5f);
                    frame.sizeDelta = new Vector2(ReferenceWidth, ReferenceHeight);
                    foreach (var child in children)
                    {
                        var rect = child as RectTransform;
                        Vector3 position = rect ? rect.anchoredPosition3D : child.localPosition;
                        Vector2 size = rect ? rect.sizeDelta : Vector2.zero;
                        child.SetParent(frame, false);
                        if (rect) { rect.anchoredPosition3D = position; rect.sizeDelta = size; }
                        else child.localPosition = position;
                    }
                    // Disable the scaler so it cannot overwrite our exact fit scale.
                    var scaler = canvas.GetComponent<CanvasScaler>();
                    if (scaler) scaler.enabled = false;
                    canvases.Add(canvas);
                    frames.Add(frame);
                }
                foreach (var camera in root.GetComponentsInChildren<Camera>(true))
                {
                    if (camera.targetTexture || camera.targetDisplay != 0 || cameras.Contains(camera)) continue;
                    cameras.Add(camera);
                    cameraRects.Add(camera.rect);
                }
            }
        }

        public static Rect CalculateViewport(int width, int height)
        {
            if (width <= 0 || height <= 0) return new Rect(0, 0, 1, 1);
            float scale = Mathf.Min(width / ReferenceWidth, height / ReferenceHeight);
            float w = ReferenceWidth * scale / width, h = ReferenceHeight * scale / height;
            return new Rect((1 - w) * .5f, (1 - h) * .5f, w, h);
        }

        void LateUpdate()
        {
            if (Screen.width != lastWidth || Screen.height != lastHeight)
                ApplySize(Screen.width, Screen.height);
        }

        void ApplySize(int width, int height)
        {
            if (width <= 0 || height <= 0) return;
            lastWidth = width; lastHeight = height;
            float scale = Mathf.Min(width / ReferenceWidth, height / ReferenceHeight);
            Rect viewport = CalculateViewport(width, height);
            for (int i = 0; i < canvases.Count; i++)
            {
                if (!canvases[i]) continue;
                canvases[i].scaleFactor = scale;
                frames[i].anchoredPosition = Vector2.zero;
            }
            for (int i = 0; i < cameras.Count; i++)
            {
                if (!cameras[i] || cameras[i].targetTexture) continue;
                Rect original = cameraRects[i];
                cameras[i].rect = new Rect(viewport.x + original.x * viewport.width,
                    viewport.y + original.y * viewport.height,
                    original.width * viewport.width, original.height * viewport.height);
            }
            SetBar(bars[0], Vector2.zero, new Vector2(viewport.xMin, 1));
            SetBar(bars[1], new Vector2(viewport.xMax, 0), Vector2.one);
            SetBar(bars[2], new Vector2(viewport.xMin, 0), new Vector2(viewport.xMax, viewport.yMin));
            SetBar(bars[3], new Vector2(viewport.xMin, viewport.yMax), new Vector2(viewport.xMax, 1));
        }

        void CreateBars()
        {
            var go = new GameObject("Letterbox", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            bars = new RectTransform[4];
            for (int i = 0; i < bars.Length; i++)
            {
                var image = new GameObject("Black Bar " + i, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                image.transform.SetParent(go.transform, false);
                image.color = Color.black;
                image.raycastTarget = true;
                bars[i] = image.rectTransform;
                image.gameObject.SetActive(false);
            }
        }

        static void SetBar(RectTransform bar, Vector2 min, Vector2 max)
        {
            bar.anchorMin = min; bar.anchorMax = max;
            bar.offsetMin = bar.offsetMax = Vector2.zero;
            bar.gameObject.SetActive(max.x - min.x > .00001f && max.y - min.y > .00001f);
        }
    }
}
