using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using LevelAuthoring.Editing;
using LevelTool;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelAuthoring.Editor
{
    public static class LevelToolPlayVerification
    {
        private static bool priorEnabled;
        private static EnterPlayModeOptions priorOptions;
        private static double deadline, next;
        private static int phase, exit;
        private static LevelToolScreen screen;
        private static AuthoringEditSession session;
        private static string snapshot;
        private static RenderTexture texture;
        private static PanelSettings panel;
        private static RenderTexture oldTarget;
        private static bool casesDone;
        private static string recoveryFolder;
        public static void Run()
        {
            priorEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            priorOptions = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorSceneManager.OpenScene(LevelTool.Editor.LevelToolAssets.ScenePath);
            recoveryFolder = Path.GetFullPath("Logs/GameAuthoringStage05/recovery-" + Guid.NewGuid().ToString("N"));
            LevelToolRecoveryIsolation.Configure(recoveryFolder);
            phase = 0; exit = 0; casesDone = false; deadline = EditorApplication.timeSinceStartup + 300;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += State;
            EditorApplication.EnterPlaymode();
        }
        private static void State(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            if (panel != null) panel.targetTexture = oldTarget;
            if (texture != null) { texture.Release(); UnityEngine.Object.DestroyImmediate(texture); }
            EditorSettings.enterPlayModeOptionsEnabled = priorEnabled;
            EditorSettings.enterPlayModeOptions = priorOptions;
            EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= State;
            EditorApplication.Exit(exit);
        }
        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("레벨툴 Play 검사 시간 초과, phase " + phase);
                if (!EditorApplication.isPlaying || phase == 99) return;
                if (phase == 0)
                {
                    screen = UnityEngine.Object.FindFirstObjectByType<LevelToolScreen>();
                    if (screen == null) return;
                    if ((bool)typeof(LevelToolScreen).GetField("busy", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(screen)) return;
                    LevelToolRecoveryExercise.Require();
                    LevelToolRecoveryIsolation.Verify(screen, recoveryFolder);
                    panel = screen.GetComponent<UIDocument>().panelSettings;
                    oldTarget = panel.targetTexture;
                    texture = new RenderTexture(1280, 800, 0); texture.Create(); panel.targetTexture = texture;
                    screen.Workspace.Open(File.ReadAllText("Logs/GameAuthoringStage02/latest-fixtures.txt"), "Cancel");
                    session = screen.Workspace.Session;
                    string id = session.Documents.First(doc => doc.Kind == "level" && (int)doc.Data["levelNumber"] == 2).Id;
                    session.SelectLevel(id);
                    LevelToolUIExercise.Run(screen);
                    LevelToolSupplyExercise.Run(screen);
                    session.SelectCells(new[] { 40 });
                    session.Apply("시험용 이동 수", docs => docs[id].Data["moveCount"] = 37);
                    typeof(LevelToolScreen).GetMethod("Refresh", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(screen, null);
                    snapshot = session.ExportState();
                    phase = 1; next = EditorApplication.timeSinceStartup + 5;
                }
                else if (phase == 1 && EditorApplication.timeSinceStartup >= next)
                {
                    VisualElement root = screen.GetComponent<UIDocument>().rootVisualElement;
                    if (root.Q("board").childCount < 81) throw new Exception("9×9 보드 누락");
                    if (root.Q("board").Query<Image>().ToList().Count == 0) throw new Exception("보드 이미지 누락: " + root.Q<Label>("status").text);
                    if (root.Q("board").worldBound.width < 300) throw new Exception("보드 레이아웃 붕괴");
                    Capture();
                    Debug.Log("PASS runtime level tool board images and layout");
                    screen.BeginPlay().Forget(); phase = 2;
                }
                else if (phase == 2)
                {
                    if (screen.TestSession == null) throw new Exception("시험 시작 실패: " + screen.GetComponent<UIDocument>().rootVisualElement.Q<Label>("status").text);
                    if (!screen.TestSession.IsReady) return;
                    if (!screen.TestSession.PlayContext.IsTest || screen.TestSession.PlayContext.AllowLevelAdvance) throw new Exception("정식 게임 문맥 사용");
                    if (screen.TestSession.State == null) throw new Exception("실제 게임 보드 없음");
                    screen.ReturnToEditor();
                    if (!ReferenceEquals(session, screen.Workspace.Session) || session.ExportState() != snapshot) throw new Exception("복귀 후 편집 상태 변경");
                    session.Undo(); if (session.IsDirty) throw new Exception("복귀 후 Undo 실패");
                    session.Redo(); if (!session.IsDirty) throw new Exception("복귀 후 Redo 실패");
                    Debug.Log("PASS runtime tool real game test and return keeps session/history");
                    phase = 3;
                    RunCases().Forget(error => { exit = 1; Debug.LogException(error); casesDone = true; });
                }
                else if (phase == 3 && casesDone)
                {
                    if (exit != 0) { phase = 99; EditorApplication.ExitPlaymode(); return; }
                    panel.targetTexture = null; texture.Release(); texture.width = 800; texture.height = 600; texture.Create(); panel.targetTexture = texture;
                    screen.GetComponent<UIDocument>().rootVisualElement.Query<Slider>().ToList().Single(value => value.label == "보드 확대").value = 92;
                    phase = 4; next = EditorApplication.timeSinceStartup + 1;
                }
                else if (phase == 4 && EditorApplication.timeSinceStartup >= next)
                {
                    VisualElement root = screen.GetComponent<UIDocument>().rootVisualElement;
                    ScrollView scroll = root.Q<ScrollView>("board-scroll");
                    if (scroll.worldBound.width < 50 || root.Q("properties").worldBound.xMax > root.worldBound.xMax + 1) throw new Exception("작은 화면 패널 배치 오류");
                    scroll.scrollOffset = new Vector2(300, 200);
                    phase = 5; next = EditorApplication.timeSinceStartup + 1;
                }
                else if (phase == 5 && EditorApplication.timeSinceStartup >= next)
                {
                    if (screen.GetComponent<UIDocument>().rootVisualElement.Q<ScrollView>("board-scroll").scrollOffset.x <= 0) throw new Exception("확대한 보드를 가로 스크롤할 수 없습니다.");
                    Capture("tool-screen-small.png"); Debug.Log("PASS small window zoom and board scrolling");
                    phase = 99; EditorApplication.ExitPlaymode();
                }
            }
            catch (Exception error)
            {
                exit = 1; phase = 99; Debug.LogException(error);
                if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
                else State(PlayModeStateChange.EnteredEditMode);
            }
        }
        private static async UniTask RunCases()
        {
            await LevelToolPlayCases.Run(screen);
            await LevelToolStorageUIExercise.Run(screen);
            await LevelToolRecoveryExercise.Run(screen);
            casesDone = true;
        }
        private static void Capture(string name = "tool-screen.png")
        {
            RenderTexture previous = RenderTexture.active;
            Texture2D image = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = texture; image.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); image.Apply();
                File.WriteAllBytes("Logs/GameAuthoringStage04/" + name, image.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(image); }
        }
    }
}
