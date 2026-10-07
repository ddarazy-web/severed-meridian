using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GameScreen.Editor
{
    public static class PuzzleUIAssets
    {
        public const string Folder = "Assets/Prefabs/UI/Puzzle";
        private static Font font;
        private static Sprite rounded;
        private static readonly Color Navy = new Color32(40, 54, 79, 255);
        private static readonly Color Cream = new Color32(255, 244, 217, 255);
        private static readonly Color Yellow = new Color32(245, 200, 90, 255);
        private static string saveFolder = Folder;
        public static GameObject GeneratePrefabs(string outputFolder)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Play 종료 후 생성하세요.");
            if (!Path.GetFullPath(outputFolder).StartsWith(Path.GetFullPath("Assets/Prefabs/UI") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("UI 프리팹 폴더 아래에서만 생성할 수 있습니다.");
            string previousFolder = saveFolder; saveFolder = outputFolder;
            try
            {
                Directory.CreateDirectory(outputFolder); AssetDatabase.Refresh();
                font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/MoonRabbitUI-Regular.ttf"); rounded = Sprite("rounded-panel");
                GameObject mission = CreateMission(); GameObject hud = CreateHud(mission.GetComponent<PuzzleMissionView>());
                GameObject items = CreateItems(); GameObject pause = CreatePopup(true), result = CreatePopup(false);
                CreateScreen(hud, items, pause, result);
                PuzzleLevelTransitionAssets.ApplyToPrefab(outputFolder + "/PuzzleResultPopup.prefab");
                PuzzlePopupAssets.ApplyToPrefabs(outputFolder, outputFolder == Folder ? PuzzlePopupAssets.CatalogPath : outputFolder + "/PopupCatalog.asset");
                PuzzleTutorialAssets.GenerateOverlay(outputFolder + "/TutorialOverlay.prefab");
                PuzzleTutorialAssets.ApplyToPrefab(outputFolder + "/PuzzleScreen.prefab", outputFolder + "/TutorialOverlay.prefab");
                return AssetDatabase.LoadAssetAtPath<GameObject>(outputFolder + "/PuzzleScreen.prefab");
            }
            finally { saveFolder = previousFolder; }
        }

        [MenuItem("Tools/Match/목업 UI 프리팹 연결")]
        public static void Generate()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Play 종료 후 생성하세요.");
            Scene existing = SceneManager.GetSceneByPath(PuzzleGameAssets.ScenePath);
            if (existing.IsValid() && existing.isDirty) throw new InvalidOperationException("미저장 게임 씬을 먼저 확인하세요.");
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            foreach (string path in Directory.GetFiles("Assets/Textures/UI/Puzzle", "*.png"))
            {
                TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path.Replace('\\', '/'));
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false; importer.alphaIsTransparency = true;
                if (path.Contains("rounded-panel")) importer.spriteBorder = new Vector4(20, 20, 20, 20);
                importer.SaveAndReimport();
            }
            GameObject screen = GeneratePrefabs(Folder);
            PuzzleGameAssets.GenerateGameplay();
            existing = SceneManager.GetSceneByPath(PuzzleGameAssets.ScenePath);
            bool opened = !existing.IsValid() || !existing.isLoaded;
            Scene scene = opened ? EditorSceneManager.OpenScene(PuzzleGameAssets.ScenePath, OpenSceneMode.Additive) : existing;
            try
            {
                var roots = scene.GetRootGameObjects();
                PuzzleGameSession session = roots.SelectMany(r => r.GetComponentsInChildren<PuzzleGameSession>(true)).Single();
                session.BoardCamera.cullingMask &= ~(1 << 5);
                session.BoardCamera.backgroundColor = new Color32(97, 121, 134, 255);
                EditorUtility.SetDirty(session.BoardCamera);
                PuzzleScreenView view = roots.SelectMany(r => r.GetComponentsInChildren<PuzzleScreenView>(true)).SingleOrDefault();
                if (view == null) view = ((GameObject)PrefabUtility.InstantiatePrefab(screen, scene)).GetComponent<PuzzleScreenView>();
                view.Configure(session, session.GetComponent<PuzzleBoardInput>());
                EditorUtility.SetDirty(view); PrefabUtility.RecordPrefabInstancePropertyModifications(view);
                foreach (var debug in roots.SelectMany(r => r.GetComponentsInChildren<PuzzlePlayDebugView>(true))) UnityEngine.Object.DestroyImmediate(debug);
                if (!roots.SelectMany(r => r.GetComponentsInChildren<EventSystem>(true)).Any())
                {
                    GameObject events = new GameObject("PuzzleEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                    SceneManager.MoveGameObjectToScene(events, scene);
                }
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
            // 세션 프리팹에서도 임시 IMGUI를 제거한다.
            string sessionPath = PuzzleGameAssets.Folder + "/PuzzleGameSession.prefab";
            GameObject contents = PrefabUtility.LoadPrefabContents(sessionPath);
            try
            {
                var debug = contents.GetComponent<PuzzlePlayDebugView>();
                if (debug != null) UnityEngine.Object.DestroyImmediate(debug);
                PrefabUtility.SaveAsPrefabAsset(contents, sessionPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        private static GameObject CreateMission()
        {
            RectTransform root = Rect("PuzzleMission", null);
            UnityEngine.UI.Button button = Button(root, "Target", "", Cream);
            Stretch(button.transform as RectTransform);
            UnityEngine.UI.Image icon = Image("Icon", button.transform, Color.white, null); Box(icon, 15, 2, 50, 48); icon.preserveAspect = true;
            UnityEngine.UI.Text count = Text("Count", button.transform, "0/0", 17, Navy); Box(count, 0, 49, 80, 24);
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(.5f, 1); icon.rectTransform.anchoredPosition = new Vector2(-25, -2);
            count.rectTransform.anchorMin = new Vector2(0, 0); count.rectTransform.anchorMax = new Vector2(1, 0); count.rectTransform.pivot = new Vector2(.5f, 0);
            count.rectTransform.anchoredPosition = Vector2.zero; count.rectTransform.sizeDelta = new Vector2(0, 24); count.resizeTextForBestFit = true; count.resizeTextMinSize = 11; count.resizeTextMaxSize = 17;
            root.gameObject.AddComponent<PuzzleMissionView>().Configure(icon, count, button);
            return Save(root.gameObject);
        }

        private static GameObject CreateHud(PuzzleMissionView mission)
        {
            RectTransform root = Rect("PuzzleHUD", null), moves = Rect("Moves", root);
            var label = Text("Label", moves, "남은 이동", 14, Cream); Box(label, 0, 0, 220, 24);
            Stretch(label.rectTransform); label.alignment = TextAnchor.UpperCenter;
            var number = Text("Number", moves, "20", 72, Cream); Stretch(number.rectTransform);
            number.rectTransform.offsetMax = new Vector2(0, -24); number.resizeTextForBestFit = true; number.resizeTextMinSize = 35; number.resizeTextMaxSize = 80;
            UnityEngine.UI.Image tray = Image("Missions", root, Cream, rounded);
            var grid = tray.gameObject.AddComponent<GridLayoutGroup>(); grid.padding = new RectOffset(10, 10, 10, 10);
            root.gameObject.AddComponent<PuzzleHudView>().Configure(number, mission, tray.transform);
            return Save(root.gameObject);
        }

        private static GameObject CreateItems()
        {
            RectTransform root = Rect("PuzzleItemBar", null);
            string[] names = { "hammer", "swap", "shuffle" };
            var buttons = new UnityEngine.UI.Button[3];
            for (int i = 0; i < 3; i++)
            {
                buttons[i] = Button(root, names[i], "", Cream);
                UnityEngine.UI.Image icon = Image("Icon", buttons[i].transform, Color.white, Sprite(names[i]));
                Stretch(icon.rectTransform); icon.rectTransform.offsetMin = new Vector2(16, 15); icon.rectTransform.offsetMax = new Vector2(-16, -15); icon.preserveAspect = true;
            }
            root.gameObject.AddComponent<PuzzleItemBarView>().Configure(buttons);
            return Save(root.gameObject);
        }

        private static GameObject CreatePopup(bool isPause)
        {
            RectTransform root = Rect(isPause ? "PuzzlePausePopup" : "PuzzleResultPopup", null); Stretch(root);
            UnityEngine.UI.Image shade = root.gameObject.AddComponent<UnityEngine.UI.Image>(); shade.color = new Color32(23, 42, 65, 184); shade.raycastTarget = true;
            UnityEngine.UI.Image panel = Image("Panel", root, Cream, rounded);
            var rect = panel.rectTransform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); rect.sizeDelta = new Vector2(380, 360);
            UnityEngine.UI.Text title = Text("Title", rect, isPause ? "잠깐 쉬어가요" : "정리 완료!", 32, Navy); Box(title, 20, 35, 340, 65);
            UnityEngine.UI.Text detail = Text("Detail", rect, isPause ? "준비되면 이어서 정리해요" : "", 16, Navy); Box(detail, 20, 105, 340, 65);
            var primary = Button(rect, "Primary", isPause ? "계속하기" : "다시하기", Yellow); Box(primary, 30, 200, 320, 52);
            if (isPause)
            {
                var retry = Button(rect, "Retry", "처음부터 다시하기", new Color32(245, 230, 197, 255)); Box(retry, 30, 270, 320, 48);
                root.gameObject.AddComponent<PuzzlePauseView>().Configure(primary, retry);
            }
            else root.gameObject.AddComponent<PuzzleResultView>().Configure(title, detail, primary);
            root.gameObject.SetActive(false);
            return Save(root.gameObject);
        }

        private static GameObject CreateScreen(GameObject hudPrefab, GameObject itemPrefab, GameObject pausePrefab, GameObject resultPrefab)
        {
            RectTransform root = Rect("PuzzleScreen", null);
            Canvas canvas = root.gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = root.gameObject.AddComponent<CanvasScaler>(); scaler.referenceResolution = new Vector2(1280, 720);
            root.gameObject.AddComponent<GraphicRaycaster>();
            GameObject backdrop = new GameObject("BackdropCamera", typeof(Camera)); backdrop.transform.SetParent(root, false);
            Camera backdropCamera = backdrop.GetComponent<Camera>(); backdropCamera.depth = -100; backdropCamera.cullingMask = 1 << 5;
            backdropCamera.clearFlags = CameraClearFlags.SolidColor; backdropCamera.backgroundColor = new Color32(97, 121, 134, 255);
            backdropCamera.orthographic = true; backdropCamera.orthographicSize = 5;
            GameObject scenery = new GameObject("Scenery", typeof(SpriteRenderer)); scenery.layer = 5; scenery.transform.SetParent(backdrop.transform, false);
            scenery.transform.localPosition = new Vector3(0, 0, 10);
            backdrop.AddComponent<PuzzleBackdropView>().Configure(backdropCamera, scenery.GetComponent<SpriteRenderer>(), Sprite("scenery-landscape"), Sprite("scenery-portrait"));
            // 보드 카메라 영역은 투명하게 남기고 주변 무대만 UI로 칠한다.
            RectTransform safe = Rect("SafeArea", root);
            var level = Text("Level", safe, "LEVEL 1", 15, Cream);
            level.alignment = TextAnchor.MiddleLeft;
            var pauseButton = Button(safe, "Pause", "", Cream);
            var pauseIcon = Image("Icon", pauseButton.transform, Color.white, Sprite("pause")); Stretch(pauseIcon.rectTransform); pauseIcon.rectTransform.offsetMin = new Vector2(10, 10); pauseIcon.rectTransform.offsetMax = new Vector2(-10, -10);
            var hud = Instance(hudPrefab, safe).GetComponent<PuzzleHudView>();
            var items = Instance(itemPrefab, safe).GetComponent<PuzzleItemBarView>();
            RectTransform board = Rect("BoardArea", safe), prompt = Rect("ItemPrompt", safe);
            var status = Text("Status", prompt, "시험용 아이템 · 수량 무제한", 12, Cream); Stretch(status.rectTransform);
            var cancel = Button(prompt, "Cancel", "취소", Cream); Box(cancel, 0, 35, 80, 30); cancel.gameObject.SetActive(false);
            var cancelRect = cancel.transform as RectTransform; cancelRect.anchorMin = cancelRect.anchorMax = new Vector2(1, 1); cancelRect.anchoredPosition = new Vector2(-65, 0); cancelRect.sizeDelta = new Vector2(65, 30);
            var explanation = Button(safe, "MissionDescription", "", Color.clear); Stretch(explanation.transform as RectTransform);
            UnityEngine.Object.DestroyImmediate(explanation.GetComponent<UnityEngine.UI.Outline>());
            var bubble = Image("Bubble", explanation.transform, Cream, rounded);
            bubble.rectTransform.anchorMin = bubble.rectTransform.anchorMax = bubble.rectTransform.pivot = new Vector2(.5f, .5f);
            bubble.rectTransform.sizeDelta = new Vector2(340, 180);
            var explanationText = Text("Description", bubble.transform, "", 20, Navy); Stretch(explanationText.rectTransform);
            explanation.gameObject.SetActive(false);
            var selected = Image("SelectedCell", safe, new Color(1, .82f, .3f, .25f), rounded);
            selected.rectTransform.anchorMin = selected.rectTransform.anchorMax = new Vector2(0, 1); selected.rectTransform.pivot = new Vector2(.5f, .5f);
            var selectionOutline = selected.gameObject.AddComponent<UnityEngine.UI.Outline>(); selectionOutline.effectColor = Yellow; selectionOutline.effectDistance = new Vector2(3, -3);
            selected.gameObject.SetActive(false);
            var pause = Instance(pausePrefab, safe).GetComponent<PuzzlePauseView>(); Stretch(pause.transform as RectTransform);
            var result = Instance(resultPrefab, safe).GetComponent<PuzzleResultView>(); Stretch(result.transform as RectTransform);
            var layout = root.gameObject.AddComponent<PuzzleScreenLayout>();
            layout.Configure(scaler, safe, hud.transform as RectTransform, hud.transform.Find("Moves") as RectTransform,
                hud.transform.Find("Missions") as RectTransform, board, items.transform as RectTransform, prompt, pauseButton.transform as RectTransform, level.rectTransform);
            var view = root.gameObject.AddComponent<PuzzleScreenView>();
            view.Setup(hud, items, pause, result, layout, pauseButton, cancel, explanation, level, status, explanationText);
            view.SetSelectionView(selected.rectTransform);
            return Save(root.gameObject);
        }

        private static GameObject Instance(GameObject prefab, Transform parent)
            => (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        private static GameObject Save(GameObject root)
        { GameObject asset = PrefabUtility.SaveAsPrefabAsset(root, saveFolder + "/" + root.name + ".prefab"); UnityEngine.Object.DestroyImmediate(root); return asset; }
        private static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/Puzzle/" + name + ".png");
        private static RectTransform Rect(string name, Transform parent)
        { RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false); return rect; }
        private static UnityEngine.UI.Image Image(string name, Transform parent, Color color, Sprite sprite)
        {
            var image = Rect(name, parent).gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = color; image.sprite = sprite;
            image.type = sprite == rounded ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple; image.raycastTarget = false; return image;
        }
        private static UnityEngine.UI.Text Text(string name, Transform parent, string value, int size, Color color)
        {
            var label = Rect(name, parent).gameObject.AddComponent<UnityEngine.UI.Text>(); label.font = font; label.text = value; label.fontSize = size;
            label.fontStyle = FontStyle.Normal;
            label.color = color; label.alignment = TextAnchor.MiddleCenter; label.raycastTarget = false; return label;
        }
        private static UnityEngine.UI.Button Button(Transform parent, string name, string value, Color color)
        {
            var image = Image(name, parent, color, rounded); image.raycastTarget = true;
            var outline = image.gameObject.AddComponent<UnityEngine.UI.Outline>(); outline.effectColor = Navy; outline.effectDistance = new Vector2(2, -2);
            var button = image.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image;
            var label = Text("Label", image.transform, value, 18, Navy); Stretch(label.rectTransform); return button;
        }
        private static void Box(Component component, float x, float y, float w, float h) => PuzzleScreenLayout.Box(component.transform as RectTransform, x, y, w, h);
        private static void Stretch(RectTransform rect)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    }
}
