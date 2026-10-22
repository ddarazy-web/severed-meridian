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
        private static PanelScaleMode oldScaleMode;
        private static float oldScale;
        private static bool casesDone;
        private static string recoveryFolder;
        private static bool gameOnly;
        private static bool nativeStart;
        public static void RunNativeStartUx() { nativeStart = true; Run(false); }
        public static void Run() { nativeStart = false; Run(false); }
        public static void RunGameEntryRegression() { nativeStart = false; Run(true); }
        private static void Run(bool onlyGameEntry)
        {
            gameOnly = onlyGameEntry;
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
            if (panel != null) { panel.targetTexture = oldTarget; panel.scaleMode = oldScaleMode; panel.scale = oldScale; }
            if (texture != null) { texture.Release(); UnityEngine.Object.DestroyImmediate(texture); }
            EditorSettings.enterPlayModeOptionsEnabled = priorEnabled;
            EditorSettings.enterPlayModeOptions = priorOptions;
            EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= State;
            if (nativeStart && Directory.Exists(recoveryFolder))
            {
                // 이 실행에서 만든 격리 저장소만 제거한다. 캡처와 결과 로그는 별도 경로에 남는다.
                try { Directory.Delete(recoveryFolder, true); }
                catch (Exception error) { exit = 1; Debug.LogException(error); }
            }
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
                    oldScaleMode = panel.scaleMode; oldScale = panel.scale;
                    if (nativeStart) { panel.scaleMode = PanelScaleMode.ConstantPixelSize; panel.scale = 1; }
                    // UI Toolkit의 마스크 렌더링에는 스텐실 버퍼가 필요하다.
                    texture = new RenderTexture(1280, 800, 24); texture.Create(); panel.targetTexture = texture;
                    if (nativeStart)
                    {
                        phase = 98;
                        RunNativeStartCases().Forget(error => { exit = 1; Debug.LogException(error); casesDone = true; });
                        return;
                    }
                    screen.Workspace.Open(File.ReadAllText("Logs/GameAuthoringStage02/latest-fixtures.txt"), "Cancel");
                    session = screen.Workspace.Session;
                    string id = session.Documents.First(doc => doc.Kind == "level" && (int)doc.Data["levelNumber"] == 2).Id;
                    session.SelectLevel(id);
                    if (!gameOnly)
                    {
                        LevelToolUIExercise.Run(screen);
                        LevelToolSupplyExercise.Run(screen);
                    }
                    session.SelectCells(new[] { 40 });
                    session.Apply("시험용 이동 수", docs => docs[id].Data["moveCount"] = 37);
                    typeof(LevelToolScreen).GetMethod("Refresh", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(screen, null);
                    snapshot = session.ExportState();
                    phase = 1; next = EditorApplication.timeSinceStartup + 5;
                }
                else if (phase == 98 && casesDone)
                {
                    phase = 99; EditorApplication.ExitPlaymode();
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
                    if (gameOnly) { phase = 99; EditorApplication.ExitPlaymode(); return; }
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
            await RunPackedTrial();
            if (!gameOnly)
            {
                await LevelToolStorageUIExercise.Run(screen);
                await LevelToolRecoveryExercise.Run(screen);
            }
            casesDone = true;
        }
        private static async UniTask RunNativeStartCases()
        {
            await LevelToolNativeStartExercise.Run(screen, texture, panel, recoveryFolder);
            casesDone = true;
        }
        private static async UniTask RunPackedTrial()
        {
            var editing = screen.Workspace.Session;
            string id = editing.Documents.First(doc => doc.Kind == "level" && (int)doc.Data["levelNumber"] == 2).Id;
            editing.SelectLevel(id);
            editing.Apply("팩 시험용 미저장 이동 수", docs => docs[id].Data["moveCount"] = 73);
            typeof(LevelToolScreen).GetMethod("Refresh", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(screen, null);
            var source = screen.GetComponent<UIDocument>().rootVisualElement.Q<DropdownField>("trial-source");
            if (source == null) throw new Exception("시험 입력 선택 누락");
            source.index = 1;
            string before = editing.ExportState();
            var expected = await Levels.LevelPackLoader.LoadAsync(2);
            try
            {
                await screen.BeginPlay();
                var game = screen.TestSession;
                if (game == null || !game.IsReady || !game.PlayContext.IsTest || game.PlayContext.Tutorial.UsesPlayerProgress)
                    throw new Exception("MemoryPack 시험 진입/문맥 오류");
                if (game.State.MovesRemaining != expected.MoveCount)
                    throw new Exception("MemoryPack 대신 미저장 JSON 사용");
                screen.ReturnToEditor();
                await UniTask.Yield(); await UniTask.Yield();
                if (editing.ExportState() != before || UnityEngine.Object.FindObjectsByType<GameScreen.PuzzleGameSession>(FindObjectsSortMode.None).Length != 0)
                    throw new Exception("MemoryPack 복귀 후 편집본/객체 보존 오류");
                Debug.Log("PASS runtime tool MemoryPack selector uses published moves and preserves unsaved JSON on return");
            }
            finally
            {
                UnityEngine.Object.Destroy(expected);
                screen.ReturnToEditor();
                source.index = 0;
                editing.Undo();
            }
        }
        private static void Capture(string name = "tool-screen.png")
        {
            RenderTexture previous = RenderTexture.active;
            Texture2D image = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = texture; image.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); image.Apply();
                Directory.CreateDirectory("Logs/GameAuthoringStage04");
                File.WriteAllBytes("Logs/GameAuthoringStage04/" + name, image.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(image); }
        }
    }
}
