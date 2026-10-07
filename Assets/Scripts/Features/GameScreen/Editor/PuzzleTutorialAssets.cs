using System;
using Tutorial;
using UnityEditor;
using UnityEngine;

namespace GameScreen.Editor
{
    public static class PuzzleTutorialAssets
    {
        public const string OverlayPath = "Assets/Prefabs/UI/Puzzle/TutorialOverlay.prefab";
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Play 종료 후 연결하세요.");
            GenerateOverlay(OverlayPath);
            ApplyToPrefab(PuzzleUIAssets.Folder + "/PuzzleScreen.prefab", OverlayPath);
        }

        public static void ApplyToPrefab(string path, string overlayPath)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Transform safe = root.transform.Find("SafeArea");
                if (safe == null) throw new InvalidOperationException("기존 SafeArea가 없습니다.");
                if (safe.Find("TutorialOverlay") == null)
                {
                    GameObject overlay = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(overlayPath), safe);
                    overlay.name = "TutorialOverlay";
                    // 팝업 Host는 기존 순서를 보존하며 안내보다 위에 둔다.
                    Transform host = safe.Find("PopupHost");
                    if (host != null) overlay.transform.SetSiblingIndex(host.GetSiblingIndex());
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        public static GameObject GenerateOverlay(string path)
        {
            Font font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/MoonRabbitUI-Regular.ttf");
            Sprite rounded = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/Puzzle/rounded-panel.png");
            RectTransform root = Rect("TutorialOverlay", null);
            try
            {
                root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.sizeDelta = Vector2.zero;
                RectTransform content = Rect("Content", root); content.anchorMin = Vector2.zero; content.anchorMax = Vector2.one; content.sizeDelta = Vector2.zero;
                UnityEngine.UI.Image[] shades = new UnityEngine.UI.Image[81];
                for (int i = 0; i < shades.Length; i++)
                {
                    shades[i] = Rect("Cell" + i, content).gameObject.AddComponent<UnityEngine.UI.Image>();
                    shades[i].raycastTarget = false;
                    UnityEngine.UI.Outline outline = shades[i].gameObject.AddComponent<UnityEngine.UI.Outline>();
                    outline.effectColor = new Color32(245, 200, 90, 255); outline.effectDistance = new Vector2(2, -2); outline.enabled = false;
                }
                RectTransform bubble = Rect("Bubble", content); bubble.sizeDelta = new Vector2(380, 108);
                UnityEngine.UI.Image panel = bubble.gameObject.AddComponent<UnityEngine.UI.Image>(); panel.sprite = rounded;
                panel.type = UnityEngine.UI.Image.Type.Sliced; panel.color = new Color32(255, 244, 217, 255); panel.raycastTarget = false;
                UnityEngine.UI.Text text = Rect("Instructions", bubble).gameObject.AddComponent<UnityEngine.UI.Text>();
                text.font = font; text.fontSize = 18; text.color = new Color32(40, 54, 79, 255); text.alignment = TextAnchor.UpperLeft; text.raycastTarget = false;
                text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
                text.rectTransform.offsetMin = new Vector2(16, 40); text.rectTransform.offsetMax = new Vector2(-16, -12);
                RectTransform nextRect = Rect("Next", bubble); nextRect.anchorMin = nextRect.anchorMax = new Vector2(1, 0); nextRect.pivot = new Vector2(1, 0);
                nextRect.anchoredPosition = new Vector2(-12, 8); nextRect.sizeDelta = new Vector2(84, 30);
                UnityEngine.UI.Image buttonImage = nextRect.gameObject.AddComponent<UnityEngine.UI.Image>(); buttonImage.sprite = rounded; buttonImage.type = UnityEngine.UI.Image.Type.Sliced;
                buttonImage.color = new Color32(245, 200, 90, 255);
                UnityEngine.UI.Button next = nextRect.gameObject.AddComponent<UnityEngine.UI.Button>(); next.targetGraphic = buttonImage;
                UnityEngine.UI.Text label = Rect("Label", nextRect).gameObject.AddComponent<UnityEngine.UI.Text>();
                label.font = font; label.fontSize = 18; label.text = "다음"; label.color = text.color; label.alignment = TextAnchor.MiddleCenter; label.raycastTarget = false;
                label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one; label.rectTransform.sizeDelta = Vector2.zero;
                RectTransform hand = Rect("Finger", content); hand.sizeDelta = new Vector2(38, 48);
                TutorialFingerGraphic graphic = hand.gameObject.AddComponent<TutorialFingerGraphic>(); graphic.raycastTarget = false; graphic.color = new Color32(255, 244, 217, 255);
                UnityEngine.UI.Outline handOutline = hand.gameObject.AddComponent<UnityEngine.UI.Outline>(); handOutline.effectColor = text.color; handOutline.effectDistance = new Vector2(2, -2);
                RectTransform badge = Rect("FreeItem", content); badge.sizeDelta = new Vector2(100, 24);
                UnityEngine.UI.Text free = badge.gameObject.AddComponent<UnityEngine.UI.Text>(); free.font = font; free.fontSize = 17;
                free.text = "무료 체험"; free.color = new Color32(245, 200, 90, 255); free.alignment = TextAnchor.MiddleCenter; free.raycastTarget = false;
                TutorialOverlayView view = root.gameObject.AddComponent<TutorialOverlayView>(); view.Configure(content, bubble, hand, badge, text, next, shades);
                root.gameObject.AddComponent<PuzzleTutorialBinding>().Configure(view); content.gameObject.SetActive(false);
                return PrefabUtility.SaveAsPrefabAsset(root.gameObject, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root.gameObject); }
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = 5;
            rect.SetParent(parent, false); return rect;
        }
    }
}
