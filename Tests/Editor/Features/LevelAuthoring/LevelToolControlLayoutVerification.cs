using System;
using System.IO;
using System.Linq;
using System.Text;
using LevelTool;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelAuthoring.Editor
{
    // 레벨툴 Play Mode에서 현재 형식의 시험 레벨과 레벨 설정 패널을 연 뒤 CLI로 실행한다.
    public static class LevelToolControlLayoutVerification
    {
        private static UIDocument document;
        private static PanelSettings originalPanel, panel;
        private static RenderTexture texture;
        private static GenericDropdownMenu menu;
        private static VisualElement root;
        private static Rect baseline;
        private static int phase, sizeIndex;
        private static double next;
        private static string originalState;
        private static readonly StringBuilder Evidence = new StringBuilder();
        private static readonly Vector2Int[] Sizes = { new Vector2Int(1280, 800), new Vector2Int(1920, 1200) };
        private static bool batch, previousEnabled;
        private static EnterPlayModeOptions previousOptions;
        private static int batchExit;
        private static double batchDeadline;
        private static string recoveryFolder;

        public static void Run()
        {
            batch = true; batchExit = 0;
            previousEnabled = EditorSettings.enterPlayModeOptionsEnabled; previousOptions = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorSceneManager.OpenScene(LevelTool.Editor.LevelToolAssets.ScenePath);
            recoveryFolder = Path.GetFullPath("Logs/LevelToolRefactorStage03/controls-" + Guid.NewGuid().ToString("N"));
            LevelToolRecoveryIsolation.Configure(recoveryFolder);
            batchDeadline = EditorApplication.timeSinceStartup + 120;
            EditorApplication.update += StartBatch;
            EditorApplication.playModeStateChanged += BatchChanged;
            EditorApplication.EnterPlaymode();
        }

        private static void StartBatch()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > batchDeadline) throw new Exception("Control layout setup timeout");
                if (!EditorApplication.isPlaying) return;
                LevelToolScreen screen = UnityEngine.Object.FindFirstObjectByType<LevelToolScreen>();
                const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                if (screen == null || (bool)typeof(LevelToolScreen).GetField("busy", flags).GetValue(screen)) return;
                LevelToolRecoveryIsolation.Verify(screen, recoveryFolder);
                screen.Workspace.Create(Path.Combine(recoveryFolder, "Project"), LevelToolTestProject.Create());
                typeof(LevelToolScreen).GetField("levelSettingsVisible", flags).SetValue(screen, true);
                typeof(LevelToolScreen).GetMethod("Refresh", flags).Invoke(screen, null);
                EditorApplication.update -= StartBatch;
                Main();
            }
            catch (Exception error) { Debug.LogException(error); batchExit = 1; EditorApplication.update -= StartBatch; EditorApplication.ExitPlaymode(); }
        }

        private static void BatchChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.playModeStateChanged -= BatchChanged;
            EditorSettings.enterPlayModeOptionsEnabled = previousEnabled; EditorSettings.enterPlayModeOptions = previousOptions;
            batch = false; EditorApplication.Exit(batchExit);
        }

        public static object Main()
        {
            LevelToolScreen screen = UnityEngine.Object.FindFirstObjectByType<LevelToolScreen>();
            if (!EditorApplication.isPlaying || screen?.Workspace.Session == null)
                throw new InvalidOperationException("Play Mode에서 시험 레벨을 먼저 열어 주세요.");
            originalState = screen.Workspace.Session.ExportState();
            document = screen.GetComponent<UIDocument>(); originalPanel = document.panelSettings;
            panel = UnityEngine.Object.Instantiate(originalPanel); document.panelSettings = panel;
            root = document.rootVisualElement;
            Evidence.Clear(); phase = sizeIndex = 0; next = 0;
            EditorApplication.update += Tick;
            return "레이아웃 검사 시작";
        }

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup < next) return;
            try
            {
                if (phase == 0)
                {
                    Vector2Int size = Sizes[sizeIndex];
                    texture = new RenderTexture(size.x, size.y, 24); texture.Create(); panel.targetTexture = texture;
                }
                else if (phase == 1)
                {
                    baseline = root.Q("workspace-body").worldBound;
                    Check(baseline.height > 300, "편집 영역 기본 높이");
                    OpenMenu(root.Q<DropdownField>("trial-source"));
                }
                else if (phase == 2)
                {
                    Stable("상단 드롭다운");
                    VisualElement overlay = root.panel.visualTree.Q(className: "unity-base-dropdown");
                    Check(overlay != null && overlay.resolvedStyle.position == Position.Absolute, "팝업 절대 배치");
                    VisualElement popup = overlay.Q(className: "unity-base-dropdown__container-outer");
                    Check(popup.worldBound.yMin >= root.Q("trial-source").worldBound.yMin && popup.worldBound.yMax <= root.worldBound.yMax, "팝업 화면 내 위치");
                    Capture("dropdown"); CloseMenu();
                    root.Q<TextField>("level-name").Focus();
                }
                else if (phase == 3)
                {
                    Stable("이름 필드 포커스");
                    foreach (string id in new[] { "level-name", "level-number", "level-moves" })
                    {
                        VisualElement field = root.Q(id), input = field.Q(className: "unity-base-field__input");
                        VisualElement text = input.Q(className: "unity-text-element--inner-input-field-component");
                        Check(input.worldBound.width >= 60 && text.worldBound.height >= 16, id + " 입력 크기");
                        Check(text.worldBound.yMin >= input.worldBound.yMin && text.worldBound.yMax <= input.worldBound.yMax, id + " 텍스트 잘림 없음");
                    }
                    root.Q<IntegerField>("level-moves").Focus();
                }
                else if (phase == 4)
                {
                    Stable("숫자 필드 포커스"); root.Q<Button>("inspector-level").Focus();
                    OpenMenu(root.Q("properties").Query<DropdownField>().ToList().First(e => e.worldBound.height > 0));
                }
                else
                {
                    Stable("우측 드롭다운"); Capture("properties"); CloseMenu();
                    Check(UnityEngine.Object.FindFirstObjectByType<LevelToolScreen>().Workspace.Session.ExportState() == originalState, "레벨 데이터 보존");
                    panel.targetTexture = null; texture.Release(); UnityEngine.Object.DestroyImmediate(texture); texture = null;
                    if (++sizeIndex == Sizes.Length) { Evidence.AppendLine("PASS"); Finish(); return; }
                    phase = -1;
                }
                phase++; next = EditorApplication.timeSinceStartup + 1.5;
            }
            catch (Exception error) { Evidence.AppendLine("FAIL " + error); Finish(); }
        }

        private static void OpenMenu(VisualElement field)
        {
            menu = new GenericDropdownMenu(); menu.AddItem("현재 편집본 JSON", true, () => { }); menu.AddItem("생성된 MemoryPack", false, () => { });
            menu.DropDown(field.worldBound, field, DropdownMenuSizeMode.Fixed);
        }
        private static void CloseMenu()
        {
            if (menu == null) return;
            typeof(GenericDropdownMenu).GetMethod("Hide", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(menu, new object[] { false });
            menu = null;
        }
        private static void Stable(string action)
        {
            Rect after = root.Q("workspace-body").worldBound;
            Evidence.AppendLine(Sizes[sizeIndex] + " " + action + " before=" + baseline + " after=" + after);
            Check(Mathf.Abs(after.height - baseline.height) < 1 && Mathf.Abs(after.y - baseline.y) < 1, action + " 편집 영역 유지");
        }
        private static void Check(bool condition, string label) { if (!condition) throw new Exception(label); }
        private static void Capture(string name)
        {
            RenderTexture previous = RenderTexture.active; RenderTexture.active = texture;
            Texture2D image = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); image.Apply();
            File.WriteAllBytes("Logs/LevelToolControls-" + sizeIndex + "-" + name + ".png", image.EncodeToPNG());
            RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(image);
        }
        private static void Finish()
        {
            EditorApplication.update -= Tick; CloseMenu(); document.panelSettings = originalPanel;
            if (texture != null) { texture.Release(); UnityEngine.Object.DestroyImmediate(texture); }
            UnityEngine.Object.DestroyImmediate(panel);
            File.WriteAllText("Logs/LevelToolControlsVerification.txt", Evidence.ToString());
            if (batch)
            {
                batchExit = Evidence.ToString().Contains("FAIL ") ? 1 : 0;
                Debug.Log(Evidence.ToString());
                EditorApplication.ExitPlaymode();
            }
        }
    }
}
