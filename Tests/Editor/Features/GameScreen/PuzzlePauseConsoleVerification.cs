using System;
using System.IO;
using System.Reflection;
using System.Threading;
using Board;
using Cysharp.Threading.Tasks;
using GameScreen;
using GameScreen.Editor;
using Levels;
using Tutorial;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

[InitializeOnLoad]
public static class PuzzlePauseConsoleVerification
{
    private const string Key = "Verification.PauseConsole.";
    private const string Output = "Logs/Tutorial/PauseConsole/";
    static PuzzlePauseConsoleVerification()
    {
        Application.logMessageReceived += (message, stack, type) =>
        {
            if (SessionState.GetBool(Key + "active", false) && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
                File.AppendAllText(Output + "errors.txt", SessionState.GetString(Key + "phase", "") + ": " + message + "\n" + stack + "\n");
        };
        EditorApplication.playModeStateChanged += change =>
        {
            if (!SessionState.GetBool(Key + "active", false)) return;
            File.AppendAllText(Output + "results.txt", "STATE " + change + "\n");
            if (change == PlayModeStateChange.EnteredPlayMode) Exercise().Forget(Fail);
            if (change == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += Next;
        };
    }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("별도 검사 에디터에서만 실행");
        Directory.CreateDirectory(Output);
        File.WriteAllText(Output + "results.txt", "");
        File.WriteAllText(Output + "errors.txt", "");
        SessionState.SetBool(Key + "failed", false);
        SessionState.SetInt(Key + "case", -1);
        SessionState.SetBool(Key + "oldRecord", SessionState.GetBool("Puzzle.EditorLaunch.tutorialCompleted.1", false));
        SessionState.SetBool(Key + "playerHas", PlayerPrefs.HasKey("MoonRabbit.Tutorial.Completed.1"));
        SessionState.SetInt(Key + "playerValue", PlayerPrefs.GetInt("MoonRabbit.Tutorial.Completed.1"));
        SessionState.SetBool(Key + "active", true);
        EditorSceneManager.OpenScene(PuzzleGameAssets.ScenePath);
        Next();
    }
    private static void Check(bool passed, string description)
    {
        if (!passed) throw new InvalidOperationException(description);
        File.AppendAllText(Output + "results.txt", "PASS " + description + "\n");
    }
    private static void Next()
    {
        int index = SessionState.GetInt(Key + "case", -1) + 1;
        if (index == 2 || SessionState.GetBool(Key + "failed", false)) { Finish(); return; }
        SessionState.SetInt(Key + "case", index);
        SessionState.SetString(Key + "phase", index == 0 ? "Asset 진입" : "MemoryPack 진입");
        try
        {
            LevelDefinition level = AssetDatabase.LoadAssetAtPath<LevelDefinition>("Assets/Data/Levels/Level_01.asset");
            PuzzleEditorLaunchRequest request = PuzzleEditorLaunchRequest.Capture(level, (PuzzleEditorLevelSource)index, 12345, TutorialRunMode.Always);
            Check(request.CreateVisualCatalog() != null, "이미지 설정 왕복 " + index);
            PuzzleEditorLauncher.Launch(request, 0);
        }
        catch (Exception error) { Fail(error); }
    }
    private static async UniTask Until(Func<bool> condition)
    {
        using CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await UniTask.WaitUntil(condition, cancellationToken: timeout.Token);
    }
    private static void Click(UnityEngine.UI.Button button)
    {
        Check(button != null && button.IsInteractable(), "버튼 조작 가능 " + button?.name);
        ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
    }
    private static async UniTask Exercise()
    {
        PuzzleGameSession session = UnityEngine.Object.FindFirstObjectByType<PuzzleGameSession>();
        await Until(() => session != null && session.CanShowTutorial);
        TutorialOverlayView overlay = UnityEngine.Object.FindFirstObjectByType<TutorialOverlayView>(FindObjectsInactive.Include);
        PuzzleScreenView screen = overlay.GetComponentInParent<PuzzleScreenView>();
        Click(overlay.Next);
        await Until(() => session.TutorialState.State == TutorialProgressState.AwaitAction && session.CanAcceptInput);
        BoardCoordinate first = session.TutorialState.First.Value, second = session.TutorialState.Second.Value;
        PuzzleBoardInput input = UnityEngine.Object.FindFirstObjectByType<PuzzleBoardInput>();
        Vector2 start = session.BoardCamera.WorldToScreenPoint(session.TutorialCellWorldPosition(first));
        Vector2 end = session.BoardCamera.WorldToScreenPoint(session.TutorialCellWorldPosition(second));
        foreach (string name in new[] { "BeginPointer", "UpdatePointer", "EndPointer" })
            typeof(PuzzleBoardInput).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(input, new object[] { -1, name == "BeginPointer" ? start : end });
        await Until(() => session.TutorialState.StepIndex == 2 && overlay.Next.gameObject.activeInHierarchy && overlay.Next.IsInteractable());
        Click(overlay.Next);
        await Until(() => session.TutorialState.State == TutorialProgressState.Completed && session.CanAcceptInput);
        Check(!overlay.Content.gameObject.activeInHierarchy, "레벨1 튜토리얼 완료");
        int moves = session.State.MovesRemaining;
        SessionState.SetString(Key + "phase", "완료 후 일시정지");
        UnityEngine.UI.Button pauseButton = (UnityEngine.UI.Button)typeof(PuzzleScreenView).GetField("pauseButton", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(screen);
        Click(pauseButton);
        await UniTask.DelayFrame(10);
        Check(session.IsPaused && !session.CanAcceptInput, "일시정지 팝업 동안 입력 차단");
        for (int i = 0; i < session.State.Missions.Count; i++) Check(session.MissionSprite(i) != null, "일시정지 중 미션 이미지 " + i);
        PuzzlePauseView pause = UnityEngine.Object.FindFirstObjectByType<PuzzlePauseView>();
        UnityEngine.UI.Button resume = (UnityEngine.UI.Button)typeof(PuzzlePauseView).GetField("resume", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(pause);
        Click(resume);
        await Until(() => !session.IsPaused && session.CanAcceptInput);
        Check(session.State.MovesRemaining == moves, "계속하기 후 이동 수 유지");
        SessionState.SetString(Key + "phase", "팝업 열린 상태에서 Play 종료");
        Click(pauseButton);
        await UniTask.DelayFrame(5);
        // 화면을 미리 비활성화하지 않고 사용자의 Play 종료와 같은 수명주기를 검사한다.
        // Addressables 종료 콜백 이후 남은 HUD 프레임의 조회를 재현한다.
        void LastHudFrame(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.ExitingPlayMode) return;
            EditorApplication.playModeStateChanged -= LastHudFrame;
            try
            {
                typeof(PuzzleScreenView).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(screen, null);
                Check(!session.IsReady, "리소스 종료 경계에서는 준비 상태 해제");
            }
            catch (Exception error)
            {
                File.AppendAllText(Output + "results.txt", "FAIL 종료 경계 HUD: " + error + "\n");
                SessionState.SetBool(Key + "failed", true);
            }
        }
        EditorApplication.playModeStateChanged += LastHudFrame;
        EditorApplication.delayCall += EditorApplication.ExitPlaymode;
    }
    private static void Fail(Exception error)
    {
        File.AppendAllText(Output + "results.txt", "FAIL " + error + "\n");
        SessionState.SetBool(Key + "failed", true);
        if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode(); else Finish();
    }
    private static void Finish()
    {
        bool playerUnchanged = PlayerPrefs.HasKey("MoonRabbit.Tutorial.Completed.1") == SessionState.GetBool(Key + "playerHas", false) &&
            PlayerPrefs.GetInt("MoonRabbit.Tutorial.Completed.1") == SessionState.GetInt(Key + "playerValue", 0);
        SessionState.SetBool("Puzzle.EditorLaunch.tutorialCompleted.1", SessionState.GetBool(Key + "oldRecord", false));
        SessionState.SetBool(Key + "active", false);
        using FileStream log = new FileStream(Application.consoleLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using StreamReader reader = new StreamReader(log);
        bool nativeError = reader.ReadToEnd().Contains("Serialization depth limit");
        if (nativeError) File.AppendAllText(Output + "results.txt", "FAIL Unity 원본 로그의 직렬화 깊이 오류\n");
        bool success = !nativeError && !SessionState.GetBool(Key + "failed", false) && playerUnchanged && new FileInfo(Output + "errors.txt").Length == 0;
        File.AppendAllText(Output + "results.txt", (success ? "PASS" : "FAIL") + " 콘솔 Error/Exception/Assert 없음 및 플레이어 기록 보존\n");
        EditorApplication.Exit(success ? 0 : 1);
    }
}
