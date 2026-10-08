using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace GameScreen.Editor
{
    [InitializeOnLoad]
    public static class AuthoringRuntimeUIProbe
    {
        private const string Output = "Logs/GameAuthoringStage01/UIProbe";
        private static int phase;
        private static int frame;
        private static double deadline;
        private static SceneSetup[] originalScenes;
        private static SceneAsset originalStartScene;
        private static bool originalOptionsEnabled;
        private static EnterPlayModeOptions originalOptions;
        private static GameObject host;
        private static PanelSettings settings;
        private static ThemeStyleSheet theme;
        private static RenderTexture texture;
        private static UIDocument document;
        private static VisualElement[] cells;
        private static ScrollView catalog;
        private static VisualElement lastEntry;
        private static Label status;
        private static int[] durability;
        private static int selected = -1;
        private static int pointerEvents;
        private static int propertyEvents;
        private static float lastBefore;
        private static bool failed;

        static AuthoringRuntimeUIProbe()
        {
            EditorApplication.update += Observe;
        }

        public static void Run()
        {
            if (phase != 0 || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Play Mode가 종료된 상태에서 검사하세요.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("미저장 씬을 보존하려면 먼저 저장 후 검사하세요.");
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/results.txt", "");
            originalScenes = EditorSceneManager.GetSceneManagerSetup();
            originalStartScene = EditorSceneManager.playModeStartScene;
            originalOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            originalOptions = EditorSettings.enterPlayModeOptions;
            EditorSceneManager.playModeStartScene = null;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            phase = 1;
            failed = false;
            deadline = EditorApplication.timeSinceStartup + 90;
            EditorApplication.EnterPlaymode();
        }

        private static void Observe()
        {
            if (phase == 0) return;
            try
            {
                if (phase == 99)
                {
                    if (!EditorApplication.isPlayingOrWillChangePlaymode) Finish();
                    return;
                }
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("UI 시제품 검사 시간 초과");
                if (!EditorApplication.isPlaying) return;
                if (phase == 1)
                {
                    CreatePanel();
                    frame = Time.frameCount;
                    phase = 2;
                    return;
                }
                if (Time.frameCount < frame + 8) return;
                frame = Time.frameCount;
                if (phase == 2)
                {
                    VisualElement root = document.rootVisualElement;
                    Check(root.panel != null && root.panel.contextType == ContextType.Player, "RuntimePanelAttached");
                    Check(cells[80].worldBound.width > 20 && cells[80].worldBound.height > 20, "Board9x9LaidOut");
                    VisualElement picked = root.panel.Pick(cells[40].worldBound.center);
                    Check(picked == cells[40], "RuntimePanelHitTestCell40");
                    using (PointerDownEvent click = PointerDownEvent.GetPooled(new Event
                    {
                        type = EventType.MouseDown, button = 0, mousePosition = cells[40].worldBound.center
                    }))
                    {
                        click.target = picked;
                        picked.SendEvent(click);
                    }
                    Check(selected == 40 && pointerEvents == 1, "PointerSelectionChangesModel");
                    Button increase = root.Q<Button>("increase-durability");
                    using (NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled())
                    {
                        submit.target = increase;
                        increase.SendEvent(submit);
                    }
                    Check(durability[40] == 2 && durability[39] == 1 && propertyEvents == 1, "PropertyButtonChangesOnlySelectedCell");
                    Check(status.text.Contains("2"), "PropertyViewUpdated");
                    Check(catalog.contentContainer.childCount == 200, "LongCatalog200Rows");
                    File.AppendAllText(Output + "/results.txt", "LAYOUT content=" + catalog.contentContainer.layout.height + " viewport=" + catalog.contentViewport.layout.height + "\n");
                    Check(catalog.contentContainer.layout.height > catalog.contentViewport.layout.height * 5, "CatalogHasScrollableLayout");
                    lastBefore = lastEntry.worldBound.y;
                    catalog.ScrollTo(lastEntry);
                    phase = 3;
                    return;
                }
                if (phase == 3)
                {
                    Check(catalog.scrollOffset.y > 1000 && lastEntry.worldBound.y < lastBefore - 1000, "ScrollMovesRenderedContent");
                    Check(catalog.contentViewport.worldBound.Overlaps(lastEntry.worldBound), "LastCatalogEntryVisible");
                    Capture();
                    File.AppendAllText(Output + "/results.txt", "PASS RuntimeUIProbe\n");
                    phase = 99;
                    EditorApplication.ExitPlaymode();
                }
            }
            catch (Exception error)
            {
                failed = true;
                File.AppendAllText(Output + "/results.txt", "FAIL " + error + "\n");
                Debug.LogException(error);
                phase = 99;
                if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.ExitPlaymode();
            }
        }

        private static void CreatePanel()
        {
            selected = -1;
            pointerEvents = propertyEvents = 0;
            texture = new RenderTexture(1024, 768, 0);
            texture.Create();
            settings = ScriptableObject.CreateInstance<PanelSettings>();
            theme = ScriptableObject.CreateInstance<ThemeStyleSheet>();
            settings.themeStyleSheet = theme;
            settings.targetTexture = texture;
            settings.scaleMode = PanelScaleMode.ConstantPixelSize;
            settings.clearColor = true;
            settings.colorClearValue = new Color(0.08f, 0.1f, 0.14f);
            host = new GameObject("Authoring Runtime UI Probe");
            document = host.AddComponent<UIDocument>();
            document.panelSettings = settings;
            VisualElement root = document.rootVisualElement;
            // 검사 전용 임시 UI이므로 제품 스타일과 프리팹을 변경하지 않는다.
            root.style.unityFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            root.style.width = 1024;
            root.style.height = 768;
            root.style.flexDirection = FlexDirection.Row;
            root.style.paddingTop = 24;
            root.style.paddingLeft = 24;
            VisualElement board = new VisualElement();
            board.style.width = 486;
            board.style.height = 486;
            board.style.flexDirection = FlexDirection.Row;
            board.style.flexWrap = Wrap.Wrap;
            root.Add(board);
            cells = new VisualElement[81];
            durability = new int[81];
            for (int i = 0; i < cells.Length; i++)
            {
                int index = i;
                durability[i] = 1;
                VisualElement cell = new VisualElement { name = "cell-" + i };
                cell.style.width = 50;
                cell.style.height = 50;
                cell.style.marginRight = 4;
                cell.style.marginBottom = 4;
                cell.style.backgroundColor = i % 2 == 0 ? new Color(0.2f, 0.55f, 0.65f) : new Color(0.3f, 0.7f, 0.6f);
                cell.RegisterCallback<PointerDownEvent>(_ =>
                {
                    selected = index;
                    pointerEvents++;
                    status.text = "Cell " + index + " / durability " + durability[index];
                    cell.style.backgroundColor = Color.cyan;
                });
                cells[i] = cell;
                board.Add(cell);
            }
            VisualElement inspector = new VisualElement();
            inspector.style.width = 450;
            inspector.style.marginLeft = 24;
            root.Add(inspector);
            status = new Label("Select a cell");
            status.style.height = 36;
            status.style.color = Color.white;
            inspector.Add(status);
            Button increase = new Button(() =>
            {
                if (selected < 0) return;
                durability[selected]++;
                propertyEvents++;
                status.text = "Cell " + selected + " / durability " + durability[selected];
                cells[selected].style.backgroundColor = new Color(1f, 0.7f, 0.3f);
            }) { name = "increase-durability", text = "Durability +1" };
            increase.style.height = 40;
            increase.style.backgroundColor = new Color(0.35f, 0.45f, 0.65f);
            inspector.Add(increase);
            catalog = new ScrollView(ScrollViewMode.Vertical);
            catalog.style.height = 540;
            // 빈 시험 테마에서는 ScrollView 내부 뷰포트 제약도 명시해야 한다.
            catalog.contentViewport.style.height = 540;
            catalog.contentViewport.style.flexShrink = 0;
            catalog.contentViewport.style.overflow = Overflow.Hidden;
            catalog.contentContainer.style.flexShrink = 0;
            catalog.verticalScroller.style.display = DisplayStyle.None;
            catalog.horizontalScroller.style.display = DisplayStyle.None;
            catalog.style.marginTop = 20;
            inspector.Add(catalog);
            for (int i = 0; i < 200; i++)
            {
                Label row = new Label("Element " + (i + 1));
                row.style.height = 32;
                row.style.flexShrink = 0;
                row.style.color = Color.white;
                row.style.backgroundColor = i % 2 == 0 ? new Color(0.16f, 0.21f, 0.28f) : new Color(0.21f, 0.27f, 0.35f);
                catalog.Add(row);
                lastEntry = row;
            }
        }

        private static void Capture()
        {
            RenderTexture previous = RenderTexture.active;
            Texture2D image = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = texture;
                image.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                image.Apply();
                Color32[] pixels = image.GetPixels32();
                int distinct = 0;
                for (int i = 0; i < pixels.Length; i += 137)
                    if (!pixels[i].Equals(pixels[0])) distinct++;
                Check(distinct > 100, "RuntimePanelRenderedNonUniformPixels");
                File.WriteAllBytes(Output + "/runtime-panel.png", image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(image);
            }
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            File.AppendAllText(Output + "/results.txt", "PASS " + message + "\n");
        }

        private static void Finish()
        {
            phase = 0;
            if (host != null) UnityEngine.Object.DestroyImmediate(host);
            if (settings != null) UnityEngine.Object.DestroyImmediate(settings);
            if (theme != null) UnityEngine.Object.DestroyImmediate(theme);
            if (texture != null) { texture.Release(); UnityEngine.Object.DestroyImmediate(texture); }
            EditorSceneManager.playModeStartScene = originalStartScene;
            EditorSettings.enterPlayModeOptionsEnabled = originalOptionsEnabled;
            EditorSettings.enterPlayModeOptions = originalOptions;
            if (originalScenes != null && originalScenes.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(originalScenes);
            if (Application.isBatchMode) EditorApplication.Exit(failed ? 1 : 0);
        }
    }
}

