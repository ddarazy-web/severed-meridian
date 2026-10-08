using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Board;
using Cysharp.Threading.Tasks;
using GameScreen;
using GameScreen.Editor;
using Levels;
using Levels.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tutorial.Editor
{
    [InitializeOnLoad]
    public static partial class TutorialComposerPlayVerification
    {
        private const string Key = "Tutorial.Composer01.Play.";
        private static bool Samples => SessionState.GetBool(Key + "samples", false);
        private static bool Shared => SessionState.GetBool(Key + "shared", false);
        private static string SampleRecordKey => Shared ? "Puzzle.EditorLaunch.tutorialIdentity." + SessionState.GetString(Key + "identity", "") : "Puzzle.EditorLaunch.tutorialCompleted." + Number;
        private static string Output => Shared ? "Logs/Tutorial/Composer03/Play/" : Samples ? "Logs/Tutorial/Composer02/Play/" : "Logs/Tutorial/Composer01/";
        private const int Number = 100001;
        static TutorialComposerPlayVerification()
        {
            EditorApplication.playModeStateChanged += change =>
            {
                if (!SessionState.GetBool(Key + "active", false)) return;
                if (change == PlayModeStateChange.EnteredPlayMode) VerifyPlay().Forget(Finish);
                if (change == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += Next;
            };
            Application.logMessageReceived += (message, stack, type) =>
            {
                if (SessionState.GetBool(Key + "active", false) && type == LogType.Exception)
                    File.AppendAllText(Output + "play-exceptions.txt", message + "\n" + stack + "\n");
            };
        }
        private static void Check(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); File.AppendAllText(Output + "play-results.txt", "PASS " + message + "\n"); }
        public static void Run() => RunCases(false);
        public static void RunSamples() { SessionState.SetBool(Key + "shared", false); RunCases(true); }
        public static void RunSharedSamples()
        {
            SessionState.SetBool(Key + "shared", true);
            SessionState.SetString(Key + "identity", "verification." + Guid.NewGuid().ToString("N"));
            RunCases(true);
        }
        private static void RunCases(bool samples)
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 검사 에디터에서만 실행합니다.");
            string packPath = LevelPackBuild.FilePath(Number);
            if (File.Exists(packPath)) throw new InvalidOperationException("검사 팩 번호가 사용 중입니다. 원본을 변경하지 않습니다.");
            SessionState.SetBool(Key + "samples", samples); Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "play-results.txt", ""); File.WriteAllText(Output + "play-exceptions.txt", "");
            string folder = "Assets/__ComposerPlay_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder)); SessionState.SetString(Key + "folder", folder);
            LevelDefinition level = samples ? TutorialSampleBoards.All.First(value => value.Id == SampleIds[0]).CreateBoard() :
                LevelPackCodec.ReadLevel(File.ReadAllBytes(Output + "workflow.bytes"), 1);
            level.hideFlags = HideFlags.None;
            using (SerializedObject data = new SerializedObject(level)) { data.FindProperty("levelNumber").intValue = Number; data.ApplyModifiedPropertiesWithoutUndo(); }
            AssetDatabase.CreateAsset(level, folder + "/Sample.asset");
            File.WriteAllBytes(packPath, LevelPackCodec.Snapshot(level)); AssetDatabase.ImportAsset(packPath);
            SessionState.SetBool(Key + "oldRecord", SessionState.GetBool("Puzzle.EditorLaunch.tutorialCompleted." + Number, false));
            SessionState.SetBool(Key + "playerHas", PlayerPrefs.HasKey("MoonRabbit.Tutorial.Completed." + Number));
            SessionState.SetInt(Key + "playerValue", PlayerPrefs.GetInt("MoonRabbit.Tutorial.Completed." + Number));
            SessionState.SetInt(Key + "case", -1); SessionState.SetBool(Key + "active", true);
            PuzzleUIRenderVerification.RememberSize(); Next();
        }
        private static void Next()
        {
            if (!SessionState.GetBool(Key + "active", false)) return;
            try
            {
                EditorWindow previous = EditorUtility.InstanceIDToObject(SessionState.GetInt(Key + "owner", 0)) as EditorWindow;
                if (previous != null) previous.Close();
                int index = SessionState.GetInt(Key + "case", -1) + 1; SessionState.SetInt(Key + "case", index);
                if (index == (Samples ? SampleIds.Length * 2 : 2)) { Finish(null); return; }
                LevelEditorWindow owner = ScriptableObject.CreateInstance<LevelEditorWindow>();
                if (Samples)
                {
                    using SerializedObject settings = new SerializedObject(owner);
                    settings.FindProperty("tutorialComposerMode").boolValue = true; settings.ApplyModifiedPropertiesWithoutUndo();
                }
                SessionState.SetInt(Key + "owner", owner.GetInstanceID()); owner.ShowUtility(); owner.CreateGUI();
                if (Samples) PrepareSample(owner, index);
                owner.SetLevel(AssetDatabase.LoadAssetAtPath<LevelDefinition>(SessionState.GetString(Key + "folder", "") + "/Sample.asset"));
                owner.CreateGUI();
                owner.rootVisualElement.Q<PopupField<string>>("game-level-source").value = index % 2 == 0 ? "에셋" : "MemoryPack";
                owner.rootVisualElement.Q<PopupField<string>>("game-tutorial-mode").value = "항상 실행";
                Button button = owner.rootVisualElement.Q<Button>("game-play-level");
                using NavigationSubmitEvent click = NavigationSubmitEvent.GetPooled(); click.target = button; button.SendEvent(click);
                Check(PuzzleEditorLauncher.IsBusy, "실제 게임 플레이 버튼 실행 " + index + " · " + owner.rootVisualElement.Q<Label>("game-launch-info")?.text);
                Check(SessionState.GetInt("Puzzle.EditorLaunch.source", -1) == (int)(index % 2 == 0 ? PuzzleEditorLevelSource.Asset : PuzzleEditorLevelSource.MemoryPack), "선택한 데이터 입력 경로로 실행 요청 전달 " + (index % 2 == 0 ? "Asset" : "MemoryPack"));
            }
            catch (Exception error) { Finish(error); }
        }
        private static async UniTask Wait(Func<bool> predicate)
        {
            using CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            await UniTask.WaitUntil(predicate, cancellationToken: timeout.Token);
        }
        private static async UniTask VerifyPlay()
        {
            PuzzleGameSession session = UnityEngine.Object.FindFirstObjectByType<PuzzleGameSession>();
            await Wait(() => session != null && session.State != null && session.CanShowTutorial);
            TutorialOverlayView view = UnityEngine.Object.FindFirstObjectByType<TutorialOverlayView>(FindObjectsInactive.Include);
            Check(session.State.LevelNumber == Number && session.TutorialState.State == TutorialProgressState.AwaitAction, "조립 조건 데이터로 실제 게임 진입");
            if (Samples) { await VerifySamplePlay(session, view); return; }
            BoardCoordinate first = session.TutorialState.First.Value, second = session.TutorialState.Second.Value;
            int index = SessionState.GetInt(Key + "case", 0);
            foreach (Vector2Int size in new[] { new Vector2Int(1280, 720), new Vector2Int(720, 1280) })
            {
                PuzzleUIRenderVerification.SetSize(size.x, size.y); await Wait(() => Screen.width == size.x && Screen.height == size.y);
                await UniTask.DelayFrame(3, PlayerLoopTiming.LastPostLateUpdate); Canvas.ForceUpdateCanvases();
                Check(view.Content.gameObject.activeInHierarchy && view.Finger.gameObject.activeInHierarchy, "실제 안내/손가락 표시 " + index + "/" + size);
                foreach (BoardCoordinate cell in new[] { first, second })
                {
                    UnityEngine.UI.Graphic graphic = view.Content.Find("Cell" + (cell.Row * 9 + cell.Column)).GetComponent<UnityEngine.UI.Graphic>();
                    Check(graphic.color.a == 0 && !graphic.raycastTarget, "대상은 투명하고 입력을 가로채지 않음 " + cell);
                }
                ScreenCapture.CaptureScreenshot(Output + "composer-play-" + index + "-" + size.x + "x" + size.y + ".png");
                await UniTask.Delay(150, ignoreTimeScale: true);
            }
            Check(!session.CanPreviewSwap(new BoardCoordinate(0, 0), new BoardCoordinate(0, 1)), "지정하지 않은 교환 차단");
            int moves = session.State.MovesRemaining;
            PuzzleBoardInput input = UnityEngine.Object.FindFirstObjectByType<PuzzleBoardInput>();
            Vector2 start = session.BoardCamera.WorldToScreenPoint(session.TutorialCellWorldPosition(first));
            Vector2 end = session.BoardCamera.WorldToScreenPoint(session.TutorialCellWorldPosition(second));
            foreach (string method in new[] { "BeginPointer", "UpdatePointer", "EndPointer" })
                typeof(PuzzleBoardInput).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(input, new object[] { -1, method == "BeginPointer" ? start : end });
            await Wait(() => session.TutorialState.State == TutorialProgressState.Completed);
            Check(session.State.MovesRemaining == moves - 1 && !session.IsPresenting && !session.HasProgressFeedback, "실제 교환·매칭·낙하 연출 종료 후 조건 완료");
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
            Check(!view.Content.gameObject.activeInHierarchy && session.CanAcceptInput, "안내 종료 후 일반 게임 조작 복귀");
            view.GetComponentInParent<PuzzleScreenView>().enabled = false; EditorApplication.ExitPlaymode();
        }
        private static void Finish(Exception error)
        {
            if (error == null && new FileInfo(Output + "play-exceptions.txt").Length > 0) error = new InvalidOperationException("게임 실행 중 예외 발생");
            string playerKey = "MoonRabbit.Tutorial.Completed." + Number;
            if (PlayerPrefs.HasKey(playerKey) != SessionState.GetBool(Key + "playerHas", false) || PlayerPrefs.GetInt(playerKey) != SessionState.GetInt(Key + "playerValue", 0))
                error ??= new InvalidOperationException("실제 플레이어 완료 기록 변경");
            if (Shared)
            {
                if (PlayerPrefs.HasKey("MoonRabbit.Tutorial.Identity." + SessionState.GetString(Key + "identity", "")))
                    error ??= new InvalidOperationException("실제 플레이어 ID 기록 변경");
                SessionState.EraseBool(SampleRecordKey);
            }
            SessionState.SetBool(Key + "active", false);
            SessionState.SetBool("Tutorial.Composer01.FullRun", false);
            SessionState.SetBool("Puzzle.EditorLaunch.tutorialCompleted." + Number, SessionState.GetBool(Key + "oldRecord", false));
            EditorWindow owner = EditorUtility.InstanceIDToObject(SessionState.GetInt(Key + "owner", 0)) as EditorWindow;
            if (owner != null) owner.Close();
            PuzzleUIRenderVerification.RestoreSize();
            AssetDatabase.DeleteAsset(LevelPackBuild.FilePath(Number));
            AssetDatabase.DeleteAsset(SessionState.GetString(Key + "folder", ""));
            if (error != null) { File.AppendAllText(Output + "play-results.txt", "FAIL " + error); Debug.LogException(error); }
            EditorApplication.Exit(error == null ? 0 : 1);
        }
    }
}
