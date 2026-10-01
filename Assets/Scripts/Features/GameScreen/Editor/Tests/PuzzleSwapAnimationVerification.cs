using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Board;
using Cysharp.Threading.Tasks;
using Levels;
using Levels.Editor;
using Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace GameScreen.Editor
{
    [InitializeOnLoad]
    public static partial class PuzzleSwapAnimationVerification
    {
        private const string Key = "StageSix.Verify";
        private const string Output = "Logs/PuzzleSwipeSwapVerification/";
        private static readonly List<string> results = new List<string>();

        static PuzzleSwapAnimationVerification()
        {
            EditorApplication.playModeStateChanged += change =>
            {
                if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
                { SessionState.SetBool(Key, false); VerifyAsync().Forget(Debug.LogException); }
                if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key + ".Scene", false))
                { SessionState.SetBool(Key + ".Scene", false); VerifySceneAsync().Forget(Debug.LogException); }
            };
        }

        [MenuItem("Tools/Match/스와이프 교환 연출 검증")]
        public static void Run()
        {
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("미저장 씬 보존: 검사를 시작하지 않습니다.");
            Directory.CreateDirectory(Output);
            SessionState.SetBool(Key, true);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        private static async UniTask VerifyAsync()
        {
            results.Clear();
            GameObject root = new GameObject("Swap Verification");
            LevelDefinition level = null;
            int exit = 0;
            try
            {
                PuzzleWorldBoard board = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PuzzleGameAssets.Folder + "/PuzzleWorldBoard.prefab"), root.transform).GetComponent<PuzzleWorldBoard>();
                Camera camera = new GameObject("Camera", typeof(Camera)).GetComponent<Camera>();
                camera.transform.SetParent(root.transform); camera.transform.position = new Vector3(0, 0, -10);
                camera.orthographic = true; camera.orthographicSize = (PuzzleWorldBoard.HalfHeight + 0.7f);
                level = (LevelDefinition)typeof(BoardActionVerification).GetMethod("RocketBoard", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                LevelRuntimeState state = LevelStateBuilder.Build(level, 12345).State;
                GameObject owner = new GameObject("Session"); owner.transform.SetParent(root.transform);
                PuzzleGameSession session = owner.AddComponent<PuzzleGameSession>(); session.Configure(board, camera);
                Set(session, "started", true);
                PuzzleArtwork art = new PuzzleArtwork(); Set(session, "artwork", art);
                await art.PrepareAsync(state, CancellationToken.None);
                Set(session, "executor", new BoardActionExecutor(state)); Set(session, "ready", true);
                board.Draw(state, art);
                BoardCoordinate a = new BoardCoordinate(2, 3), b = new BoardCoordinate(3, 3);
                SpriteRenderer moving = board.OccupantAt(a), other = board.OccupantAt(b);
                Vector3 origin = moving.transform.position, otherOrigin = other.transform.position;
                SpriteRenderer[] renderers = board.GetComponentsInChildren<SpriteRenderer>();
                Sprite[] before = renderers.Select(r => r.sprite).ToArray();
                Dictionary<SpriteRenderer, Vector3> stationary = renderers.Where(r => r != moving && r != other).ToDictionary(r => r, r => r.transform.position);
                BoardActionExecutor baseline = new BoardActionExecutor(state); baseline.Swap(a, b);
                Capture(camera, "success-start", 1280, 720);
                Check(session.TrySwap(a, b), "유효 교환 규칙 적용");
                Check(renderers.Select(r => r.sprite).SequenceEqual(before), "교환 동안 계산 전 스프라이트 보존");
                BoardActionPhase phase = session.Phase;
                typeof(PuzzleGameSession).GetMethod("Advance", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(session, null);
                Check(session.Phase == phase, "교환 완료 전 다음 연쇄 차단");
                Check(!session.CanAcceptInput && !session.TryUseItem(BoardItem.Shuffle), "재생 중 입력/아이템 차단");
                Tick(session, 0.075f);
                Check(Vector3.Distance(moving.transform.position, (origin + otherOrigin) * 0.5f) < 0.001f, "유효 교환 중간 위치");
                Check(stationary.All(pair => pair.Key.transform.position == pair.Value), "바닥과 비대상 레이어 고정");
                Capture(camera, "success-mid", 1280, 720);
                Vector3 pausedAt = moving.transform.position;
                Check(session.SetPaused(true), "재생 중 일시정지"); Tick(session, 1);
                Check(moving.transform.position == pausedAt && session.IsPresenting, "정지 중 재생 시간과 위치 보존");
                session.SetPaused(false); Tick(session, 0.075f);
                Check(!session.IsPresenting && Snapshot(session.State) == Snapshot(baseline.State), "표시 완료 후 실행기 결과 동일");
                Capture(camera, "success-end", 1280, 720);
                for (int step = 0; step < 1000 && baseline.HasPendingCascade; step++)
                { Call(session, "Advance"); baseline.AdvanceCascade(); }
                Check(!baseline.HasPendingCascade && Snapshot(session.State) == Snapshot(baseline.State), "교환 뒤 전체 연쇄 동일");

                Reset();
                BoardCoordinate invalidA = new BoardCoordinate(3, 3), invalidB = new BoardCoordinate(3, 4);
                Check(ActionQuery.Swap(state, invalidA, invalidB).Reason == ActionReason.NoNewMatch, "무효 매칭 fixture");
                string unchanged = Snapshot(session.State);
                SpriteRenderer invalid = board.OccupantAt(invalidA);
                Vector3 invalidOrigin = invalid.transform.position;
                Capture(camera, "return-start", 450, 800);
                Check(!session.TrySwap(invalidA, invalidB) && session.IsPresenting, "무효 교환 왕복 시작");
                Tick(session, 0.15f);
                Check(Vector3.Distance(invalid.transform.position, board.transform.TransformPoint(PuzzleWorldBoard.CellPosition(invalidB))) < 0.001f, "무효 교환 도착점");
                Capture(camera, "return-far", 450, 800);
                Check(!session.TrySwap(a, b), "복귀 중 추가 교환 차단");
                Tick(session, 0.075f); Capture(camera, "return-mid", 450, 800);
                Tick(session, 0.075f);
                Check(invalid.transform.position == invalidOrigin && session.CanAcceptInput && Snapshot(session.State) == unchanged, "복귀 완료와 모든 규칙 상태 불변");
                Capture(camera, "return-end", 450, 800);

                Reset();
                Check(!session.TrySwap(a, new BoardCoordinate(-1, 3)), "보드 밖 거절"); Tick(session, 0.075f);
                Check(Vector3.Distance(moving.transform.position, origin) < 0.1f && Snapshot(session.State) == unchanged, "보드 밖으로 이동하지 않음");
                Tick(session, 1);
                PuzzleBoardInput input = owner.AddComponent<PuzzleBoardInput>(); input.Configure(session, board, camera);
                VerifyGestures(session, input, board, camera, state, art);
                UnityEngine.Object.DestroyImmediate(input);
                Reset();
                await PuzzleBoardInputVerification.VerifyAsync(board, camera);
                owner.SetActive(false);
                await VerifyContentsAsync(board, camera, root.transform, state);
                await VerifyFixedLayersAsync(board, camera, root.transform);
                await VerifyRestartAsync(board, camera, root.transform);

                void Reset()
                { Call(session, "ResetPresentation"); Set(session, "executor", new BoardActionExecutor(state)); board.Draw(session.State, art); }
            }
            catch (Exception error) { results.Add("FAIL " + error); exit = 1; }
            finally
            {
                UnityEngine.Object.Destroy(root);
                if (level != null) UnityEngine.Object.Destroy(level);
                File.WriteAllLines(Output + "results.txt", results);
                if (Application.isBatchMode) EditorApplication.Exit(exit);
                else EditorApplication.ExitPlaymode();
            }
        }

        private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
        private static void Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args);
        private static void Tick(PuzzleGameSession session, float seconds) => Call(session, "AdvancePresentation", seconds);
        private static string Snapshot(object state) => (string)typeof(LevelInitialStateVerification).GetMethod("Snapshot", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new[] { state });

        private static void VerifyGestures(PuzzleGameSession session, PuzzleBoardInput input, PuzzleWorldBoard board, Camera camera, LevelRuntimeState state, PuzzleArtwork art)
        {
            BoardCoordinate a = new BoardCoordinate(2, 3), b = new BoardCoordinate(3, 3);
            Vector2 Screen(BoardCoordinate at, Vector3 offset = default) => camera.WorldToScreenPoint(board.transform.TransformPoint(PuzzleWorldBoard.CellPosition(at) + offset));
            void Reset()
            { input.CancelGesture(); Call(session, "ResetPresentation"); Set(session, "executor", new BoardActionExecutor(state)); board.Draw(session.State, art); }
            foreach (Vector3 axis in new[] { Vector3.down, Vector3.up, Vector3.left, Vector3.right })
            {
                Reset();
                Call(input, "BeginPointer", 11, Screen(a));
                Call(input, "UpdatePointer", 11, Screen(a, axis * .24f));
                Check(session.State.MovesRemaining == state.MovesRemaining && !session.IsPresenting, "0.24칸 미확정 " + axis);
                Check(Vector3.Distance(board.OccupantAt(a).transform.position, board.transform.TransformPoint(PuzzleWorldBoard.CellPosition(a))) > .2f, "드래그 미리보기 " + axis);
                Call(input, "UpdatePointer", 11, Screen(a, axis * .25f));
                Vector3 exactStart = (Vector3)typeof(PuzzleBoardInput).GetField("start", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(input);
                object[] pointArguments = { Screen(a, axis * .25f), Vector3.zero };
                typeof(PuzzleBoardInput).GetMethod("TryLocalPoint", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(input, pointArguments);
                results.Add("INFO threshold delta " + (((Vector3)pointArguments[1]) - exactStart).ToString("F9"));
                Check(session.IsPresenting, "놓기 전 0.25칸 확정 " + axis);
                string after = Snapshot(session.State);
                Call(input, "UpdatePointer", 11, Screen(b));
                Call(input, "EndPointer", 11, Screen(b));
                Check(Snapshot(session.State) == after, "이동/종료 추가 행동 없음 " + axis);
                Tick(session, 1);
            }
            Reset(); Call(input, "BeginPointer", 11, Screen(b)); Call(input, "UpdatePointer", 11, Screen(b, new Vector3(.5f, .5f)));
            Check(!session.CanAcceptInput, "대각선 동률 가로 확정");
            Check(session.State.MovesRemaining == state.MovesRemaining, "가로 무효 교환으로 판정");
            Reset(); Call(input, "BeginPointer", 11, Screen(a)); Call(input, "UpdatePointer", 11, Screen(a, Vector3.down * .2f));
            input.CancelGesture();
            Check(board.OccupantAt(a).transform.position == board.transform.TransformPoint(PuzzleWorldBoard.CellPosition(a)), "미확정 취소 위치 복원");
            Reset(); Call(input, "BeginPointer", 11, Screen(a)); Call(input, "EndPointer", 11, Screen(b));
            Check(session.State.MovesRemaining == state.MovesRemaining - 1, "Ended-only 제스처 교환");
            Tick(session, .075f); string committed = Snapshot(session.State);
            board.transform.rotation = Quaternion.Euler(0, 0, 15); input.CancelGesture();
            Tick(session, .075f);
            Check(Snapshot(session.State) == committed && !session.IsPresenting, "확정 후 회전/취소는 규칙 롤백 없음");
            board.transform.rotation = Quaternion.identity;

            InputSettings previous = InputSystem.settings;
            InputSettings settings = UnityEngine.Object.Instantiate(previous);
            Mouse mouse = null; Touchscreen touch = null;
            try
            {
                settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                InputSystem.settings = settings;
                mouse = InputSystem.AddDevice<Mouse>(); touch = InputSystem.AddDevice<Touchscreen>();
                Reset();
                InputSystem.QueueStateEvent(mouse, new MouseState { position = Screen(a), buttons = 1 }); InputSystem.Update(); Call(input, "Update");
                InputSystem.QueueStateEvent(mouse, new MouseState { position = Screen(b), buttons = 1 }); InputSystem.Update(); Call(input, "Update");
                Check(session.State.MovesRemaining == state.MovesRemaining - 1, "실제 마우스 held 이동 확정");
                Tick(session, 1);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = Screen(b) }); InputSystem.Update(); Call(input, "Update");
                Check(session.State.MovesRemaining == state.MovesRemaining - 1, "확정 완료 뒤 mouse release 중복 없음");
                Reset();
                InputSystem.QueueStateEvent(touch, new TouchState { touchId = 31, phase = UnityEngine.InputSystem.TouchPhase.Began, position = Screen(a) }); InputSystem.Update(); Call(input, "Update");
                InputSystem.QueueStateEvent(touch, new TouchState { touchId = 31, phase = UnityEngine.InputSystem.TouchPhase.Moved, position = Screen(b) }); InputSystem.Update(); Call(input, "Update");
                Check(session.State.MovesRemaining == state.MovesRemaining - 1, "실제 터치 Moved 확정");
                InputSystem.QueueStateEvent(touch, new TouchState { touchId = 32, phase = UnityEngine.InputSystem.TouchPhase.Began, position = Screen(a) }); InputSystem.Update(); Call(input, "Update");
                InputSystem.QueueStateEvent(touch, new TouchState { touchId = 31, phase = UnityEngine.InputSystem.TouchPhase.Ended, position = Screen(b) }); InputSystem.Update(); Call(input, "Update");
                Check(session.State.MovesRemaining == state.MovesRemaining - 1, "두 번째 터치/Ended 중복 없음");
            }
            finally
            {
                if (mouse != null) InputSystem.RemoveDevice(mouse);
                if (touch != null) InputSystem.RemoveDevice(touch);
                InputSystem.settings = previous; UnityEngine.Object.Destroy(settings); Reset();
            }
        }

        private static void Capture(Camera camera, string name, int width, int height)
        {
            RenderTexture previous = RenderTexture.active, target = camera.targetTexture;
            float aspect = camera.aspect; Rect rect = camera.rect;
            RenderTexture texture = new RenderTexture(width, height, 24);
            Texture2D image = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                camera.targetTexture = texture; camera.rect = new Rect(0, 0, 1, 1); camera.aspect = (float)width / height;
                camera.Render(); RenderTexture.active = texture;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                File.WriteAllBytes(Output + name + ".png", image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous; camera.targetTexture = target; camera.rect = rect; camera.aspect = aspect;
                UnityEngine.Object.Destroy(texture); UnityEngine.Object.Destroy(image);
            }
        }
        private static void Check(bool condition, string name)
        { if (!condition) throw new InvalidOperationException(name); results.Add("PASS " + name); }
    }
}
