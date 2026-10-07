using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Board;
using Cysharp.Threading.Tasks;
using Levels;
using Levels.Editor;
using Simulation;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameScreen.Editor
{
    public static class PuzzleBoardInputVerification
    {
        public static void Run() => PuzzleGameplayVerification.Run();

        internal static async UniTask VerifyAsync(PuzzleWorldBoard board, Camera camera)
        {
            Directory.CreateDirectory("Logs/PuzzleBoardInputVerification");
            List<string> results = new List<string>();
            LevelDefinition level = (LevelDefinition)typeof(BoardActionVerification).GetMethod("RocketBoard", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
            GameObject owner = null;
            Mouse mouse = null; Touchscreen touch = null;
            InputSettings previousSettings = InputSystem.settings;
            InputSettings testSettings = UnityEngine.Object.Instantiate(previousSettings);
            try
            {
                owner = new GameObject("Input Verification");
                PuzzleGameSession session = owner.AddComponent<PuzzleGameSession>(); session.Configure(board, camera);
                Field(session, "started", true);
                PuzzleArtwork art = new PuzzleArtwork();
                LevelRuntimeState initial = LevelStateBuilder.Build(level, 12345).State;
                await art.PrepareAsync(initial, CancellationToken.None);
                Field(session, "artwork", art); Field(session, "ready", true);
                Reset();
                PuzzleBoardInput input = owner.AddComponent<PuzzleBoardInput>(); input.Configure(session, board, camera);
                Vector2 Screen(BoardCoordinate at) => camera.WorldToScreenPoint(board.transform.TransformPoint(PuzzleWorldBoard.CellPosition(at)));
                BoardCoordinate a = new BoardCoordinate(2, 3), b = new BoardCoordinate(3, 3);
                Check(input.TryGetCoordinate(Screen(a), out BoardCoordinate found) && found.Equals(a), "ScreenPointMapsToCell");
                Vector3 previous = board.transform.position;
                board.transform.position = new Vector3(0.3f, 0.4f, 0); board.transform.rotation = Quaternion.Euler(0, 0, 15);
                Check(input.TryGetCoordinate(Screen(b), out found) && found.Equals(b), "이동·회전 보드 좌표 변환");
                board.transform.SetPositionAndRotation(previous, Quaternion.identity);
                float previousAspect = camera.aspect, previousSize = camera.orthographicSize;
                string beforeResize = Snapshot(session.State);
                foreach (float aspect in new[] { 16f / 9f, 9f / 16f })
                {
                    camera.aspect = aspect; Call(session, "Update");
                    Check(input.TryGetCoordinate(Screen(b), out found) && found.Equals(b) && Snapshot(session.State) == beforeResize, "화면 비율 변경 후 좌표·상태 보존 " + aspect);
                }
                camera.aspect = previousAspect; camera.orthographicSize = previousSize;
                Check(!input.TryGetCoordinate(Screen(new BoardCoordinate(0, 0)), out _), "비활성 칸 거절");
                Check(!input.TryGetCoordinate(new Vector2(-100, -100), out _), "보드 밖 거절");
                BoardCoordinate last = new BoardCoordinate(BoardDefinition.DefaultRows - 1, BoardDefinition.DefaultColumns - 1);
                Check(!input.TryGetCoordinate(Screen(last), out found) && found.Equals(last), "마지막 비활성 칸 좌표 변환");
                Check(!input.TryGetCoordinate(Screen(new BoardCoordinate(last.Row, BoardDefinition.DefaultColumns)), out _), "오른쪽 보드 경계 밖 거절");
                Check(!input.TryGetCoordinate(Screen(new BoardCoordinate(BoardDefinition.DefaultRows, last.Column)), out _), "아래쪽 보드 경계 밖 거절");

                Call(input, "BeginPointer", 1, Screen(a)); Call(input, "EndPointer", 1, Screen(a));
                Check(input.Selected.HasValue && input.Selected.Value.Equals(a), "첫 탭 선택");
                Call(input, "BeginPointer", 1, Screen(b)); Call(input, "EndPointer", 1, Screen(b));
                Check(session.State.MovesRemaining == initial.MovesRemaining - 1, "두 탭 교환");
                string after = Snapshot(session.State);
                Call(input, "BeginPointer", 1, Screen(a)); Call(input, "EndPointer", 1, Screen(b));
                Check(Snapshot(session.State) == after, "BusyInputIgnored");
                Reset(); input.CancelGesture();
                Call(input, "BeginPointer", 1, Screen(a)); Call(input, "EndPointer", 2, Screen(b));
                Check(session.State.MovesRemaining == initial.MovesRemaining, "SecondaryTouchIgnored");
                Call(input, "EndPointer", 1, Screen(b)); Call(input, "EndPointer", 1, Screen(b));
                Check(session.State.MovesRemaining == initial.MovesRemaining - 1, "OneGestureOneCommand");
                Reset(); Call(input, "BeginPointer", 1, Screen(a)); Call(input, "OnApplicationFocus", false); Call(input, "EndPointer", 1, Screen(b));
                Check(session.State.MovesRemaining == initial.MovesRemaining && !input.Selected.HasValue, "FocusLossCancelsGesture");
                Vector2 shortDrag = camera.WorldToScreenPoint(board.transform.TransformPoint(PuzzleWorldBoard.CellPosition(a) + Vector3.down * 0.24f));
                Call(input, "BeginPointer", 1, Screen(a)); Call(input, "EndPointer", 1, shortDrag);
                Check(session.State.MovesRemaining == initial.MovesRemaining, "25% 미만 드래그는 교환하지 않음");
                input.CancelGesture();
                GameObject ui = new GameObject("Input Shield", typeof(Canvas), typeof(GraphicRaycaster));
                GameObject events = new GameObject("Verification Events", typeof(EventSystem));
                try
                {
                    ui.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                    GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image)); panel.transform.SetParent(ui.transform, false);
                    RectTransform rect = panel.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
                    Canvas.ForceUpdateCanvases();
                    await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
                    Canvas.ForceUpdateCanvases();
                    List<RaycastResult> hits = new List<RaycastResult>();
                    EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = Screen(a) }, hits);
                    Check(hits.Count > 0, "UI 차단 검사 전 실제 Raycast 준비");
                    Call(input, "BeginPointer", 1, Screen(a)); Call(input, "EndPointer", 1, Screen(b));
                    Check(session.State.MovesRemaining == initial.MovesRemaining && !input.Selected.HasValue, "UIStartIgnored");
                    panel.SetActive(false);
                    Call(input, "BeginPointer", 1, Screen(a));
                    panel.SetActive(true); Canvas.ForceUpdateCanvases();
                    Call(input, "EndPointer", 1, Screen(b));
                    Check(session.State.MovesRemaining == initial.MovesRemaining && !input.Selected.HasValue, "UIEndIgnored");
                }
                finally { UnityEngine.Object.DestroyImmediate(ui); UnityEngine.Object.DestroyImmediate(events); }

                // 배치에는 포커스된 Game View가 없으므로 검사 동안만 게임 입력을 전달한다.
                testSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                testSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                InputSystem.settings = testSettings;
                mouse = InputSystem.AddDevice<Mouse>();
                Vector2 from = Screen(a), to = Screen(b);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = from, buttons = 1 }); InputSystem.Update(); Call(input, "Update");
                InputSystem.QueueStateEvent(mouse, new MouseState { position = to }); InputSystem.Update(); Call(input, "Update");
                Check(session.State.MovesRemaining == initial.MovesRemaining - 1, "Input System 마우스 드래그");
                Reset(); input.CancelGesture();
                touch = InputSystem.AddDevice<Touchscreen>();
                InputSystem.QueueStateEvent(touch, new TouchState { touchId = 7, phase = UnityEngine.InputSystem.TouchPhase.Began, position = from });
                InputSystem.QueueStateEvent(mouse, new MouseState { position = from, buttons = 1 }); InputSystem.Update(); Call(input, "Update");
                InputSystem.QueueStateEvent(touch, new TouchState { touchId = 7, phase = UnityEngine.InputSystem.TouchPhase.Ended, position = to });
                InputSystem.QueueStateEvent(mouse, new MouseState { position = to }); InputSystem.Update(); Call(input, "Update");
                Check(session.State.MovesRemaining == initial.MovesRemaining - 1, "Input System 터치 및 합성 마우스 중복 차단");
                Reset(); input.CancelGesture(); InputSystem.Update(); Call(input, "Update");
                InputSystem.QueueStateEvent(mouse, new MouseState { position = from, buttons = 1 }); InputSystem.Update(); Call(input, "Update");
                InputSystem.QueueStateEvent(mouse, new MouseState { position = to }); InputSystem.Update(); Call(input, "Update");
                bool mouseRecovered = session.State.MovesRemaining == initial.MovesRemaining - 1;
                results.Add((mouseRecovered ? "PASS " : "FAIL ") + "터치 종료 다음 프레임부터 마우스 복구");
                Reset(); input.CancelGesture(); InputSystem.ResetDevice(touch); InputSystem.Update();
                InputSystem.QueueStateEvent(touch, new TouchState { touchId = 8, phase = UnityEngine.InputSystem.TouchPhase.Began, position = from });
                InputSystem.QueueStateEvent(touch, new TouchState { touchId = 8, phase = UnityEngine.InputSystem.TouchPhase.Moved, position = Vector2.Lerp(from, to, 0.5f) });
                InputSystem.Update(); Call(input, "Update");
                InputSystem.QueueStateEvent(touch, new TouchState { touchId = 8, phase = UnityEngine.InputSystem.TouchPhase.Ended, position = to });
                InputSystem.Update(); Call(input, "Update");
                bool beganMoved = session.State.MovesRemaining == initial.MovesRemaining - 1;
                results.Add((beganMoved ? "PASS " : "FAIL ") + "동일 업데이트 Began+Moved 드래그 보존");
                Reset(); input.CancelGesture(); InputSystem.ResetDevice(touch); InputSystem.Update();
                InputSystem.QueueStateEvent(touch, new TouchState { touchId = 9, phase = UnityEngine.InputSystem.TouchPhase.Began, position = from });
                InputSystem.QueueStateEvent(touch, new TouchState { touchId = 9, phase = UnityEngine.InputSystem.TouchPhase.Ended, position = from });
                InputSystem.Update(); Call(input, "Update"); InputSystem.Update(); Call(input, "Update");
                bool quickTap = input.Selected.HasValue && input.Selected.Value.Equals(a);
                results.Add((quickTap ? "PASS " : "FAIL ") + "동일 업데이트 Began+Ended 빠른 탭 보존");
                if (!mouseRecovered || !beganMoved || !quickTap) throw new InvalidOperationException("터치 회귀 검사 실패");
                typeof(RuntimeCell).GetProperty("Content").GetSetMethod(true).Invoke(initial.CellAt(a), new object[] { RuntimeContent.Rocket });
                Reset(); input.CancelGesture();
                Call(input, "BeginPointer", 1, Screen(a)); Call(input, "EndPointer", 1, Screen(a));
                Check(session.State.MovesRemaining == initial.MovesRemaining - 1, "파워 탭은 제자리 발동");
                typeof(RuntimeCell).GetProperty("Content").GetSetMethod(true).Invoke(initial.CellAt(b), new object[] { RuntimeContent.Rocket });
                Reset(); input.CancelGesture();
                Call(input, "BeginPointer", 1, Screen(a)); Call(input, "EndPointer", 1, Screen(b));
                Check(session.State.MovesRemaining == initial.MovesRemaining - 1, "파워 드래그는 결합 교환");

                void Reset()
                {
                    Call(session, "ResetPresentation");
                    Field(session, "executor", new BoardActionExecutor(initial));
                    board.Draw(session.State, art);
                }
            }
            catch (Exception error) { results.Add("FAIL " + error); throw; }
            finally
            {
                if (mouse != null) InputSystem.RemoveDevice(mouse);
                if (touch != null) InputSystem.RemoveDevice(touch);
                InputSystem.settings = previousSettings;
                UnityEngine.Object.Destroy(testSettings);
                if (owner != null) UnityEngine.Object.Destroy(owner);
                UnityEngine.Object.Destroy(level);
                File.WriteAllLines("Logs/PuzzleBoardInputVerification/results.txt", results);
            }
            void Check(bool pass, string name) { if (!pass) throw new InvalidOperationException(name); results.Add("PASS " + name); }
        }
        private static void Field(object item, string name, object value) => item.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(item, value);
        private static void Call(object item, string name, params object[] args) => item.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(item, args);
        private static string Snapshot(object state) => (string)typeof(LevelInitialStateVerification).GetMethod("Snapshot", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new[] { state });
    }
}
