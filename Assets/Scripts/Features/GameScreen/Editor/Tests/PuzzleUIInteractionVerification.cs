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
using UnityEngine;
using UnityEngine.EventSystems;

namespace GameScreen.Editor
{
    [InitializeOnLoad]
    public static class PuzzleUIInteractionVerification
    {
        private const string Key = "StageFour.Interaction";
        private static readonly List<string> results = new List<string>();
        static PuzzleUIInteractionVerification()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
                { SessionState.SetBool(Key, false); VerifyAsync().Forget(Debug.LogException); }
            };
        }
        [MenuItem("Tools/Match/UI 조작 통합 검증")]
        public static void Run()
        { PuzzleUIRenderVerification.RememberSize(); SessionState.SetBool(Key, true); PuzzleGameSceneVerification.OpenInteractive(); }

        private static async UniTask VerifyAsync()
        {
            results.Clear();
            try
            {
                Application.runInBackground = true;
                PuzzleGameSession session = UnityEngine.Object.FindFirstObjectByType<PuzzleGameSession>();
                await Ready(session);
                PuzzleUIRenderVerification.SetSize(1280, 720);
                for (int i = 0; i < 12; i++) await UniTask.Yield();
                PuzzleScreenView screen = UnityEngine.Object.FindFirstObjectByType<PuzzleScreenView>();
                PuzzleWorldBoard board = UnityEngine.Object.FindFirstObjectByType<PuzzleWorldBoard>();
                PuzzleBoardInput input = session.GetComponent<PuzzleBoardInput>();
                Transform safe = screen.transform.Find("SafeArea");
                Transform pause = safe.Find("PuzzlePausePopup"), result = safe.Find("PuzzleResultPopup");
                string initial = Snapshot(session.State);
                Check(safe.Find("PuzzleHUD/Moves/Number").GetComponent<UnityEngine.UI.Text>().text == session.State.MovesRemaining.ToString(), "HUD 실제 이동 수");
                Check(safe.GetComponentsInChildren<PuzzleMissionView>().Length == session.State.Missions.Count, "HUD 실제 미션 수");
                var originalMissions = session.State.Missions;
                FieldInfo missionsField = typeof(LevelRuntimeState).GetField("<Missions>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
                foreach (Vector2Int size in new[] { new Vector2Int(1280, 720), new Vector2Int(450, 800), new Vector2Int(450, 975), new Vector2Int(600, 800) })
                {
                    PuzzleUIRenderVerification.SetSize(size.x, size.y);
                    for (int count = 1; count <= 4; count++)
                    {
                        missionsField.SetValue(session.State, Array.AsReadOnly(Enumerable.Repeat(originalMissions[0], count).ToArray()));
                        InvokeInstance(session, "Draw");
                        for (int frame = 0; frame < 8; frame++) await UniTask.Yield();
                        Rect tray = Bounds(safe.Find("PuzzleHUD/Missions") as RectTransform);
                        foreach (PuzzleMissionView mission in screen.GetComponentsInChildren<PuzzleMissionView>())
                        {
                            Rect rect = Bounds(mission.transform as RectTransform);
                            Check(rect.xMin >= tray.xMin - 1 && rect.yMin >= tray.yMin - 1 && rect.xMax <= tray.xMax + 1 && rect.yMax <= tray.yMax + 1 && !rect.Overlaps(session.BoardCamera.pixelRect),
                                "미션 범위/보드 비중첩 " + size + "/" + count);
                        }
                    }
                    Rect safeArea = new Rect(17, 24, size.x - 29, size.y - 65);
                    PuzzleScreenLayout layout = screen.GetComponent<PuzzleScreenLayout>();
                    layout.ApplyLayout(safeArea, size); Canvas.ForceUpdateCanvases();
                    foreach (string path in new[] { "PuzzleHUD", "PuzzleItemBar", "Pause", "ItemPrompt", "BoardArea" })
                    {
                        Rect bounds = Bounds(safe.Find(path) as RectTransform);
                        Check(bounds.xMin >= safeArea.xMin - 1 && bounds.yMin >= safeArea.yMin - 1 && bounds.xMax <= safeArea.xMax + 1 && bounds.yMax <= safeArea.yMax + 1,
                            "비대칭 안전 영역 실제 UI " + size + "/" + path);
                    }
                    layout.ApplyLayout(Screen.safeArea, size);
                }
                missionsField.SetValue(session.State, originalMissions); InvokeInstance(session, "Draw");
                PuzzleUIRenderVerification.SetSize(1280, 720);
                for (int frame = 0; frame < 8; frame++) await UniTask.Yield();
                Click("PuzzleHUD/Missions/PuzzleMission(Clone)/Target");
                Check(safe.Find("MissionDescription").gameObject.activeSelf, "미션 설명 열기");
                string before = Snapshot(session.State);
                BoardCoordinate target = session.State.Cells.First(c => session.CanSelectItemTarget(BoardItem.Hammer, c.Coordinate)).Coordinate;
                Tap(target); Check(Snapshot(session.State) == before && !input.Selected.HasValue, "설명 팝업 보드 입력 차단");
                Click("MissionDescription"); Check(!safe.Find("MissionDescription").gameObject.activeSelf, "미션 설명 닫기");
                Click("PuzzleItemBar/hammer"); Check(input.SelectedItem == BoardItem.Hammer, "망치 버튼 선택");
                Click("PuzzleItemBar/hammer"); Check(!input.SelectedItem.HasValue && Snapshot(session.State) == before, "동일 아이템 다시 눌러 취소");
                Click("PuzzleItemBar/hammer");
                await Capture("item-selected");
                Click("ItemPrompt/Cancel"); Check(!input.SelectedItem.HasValue && Snapshot(session.State) == before, "아이템 취소 원자성");
                Click("PuzzleItemBar/hammer"); int moves = session.State.MovesRemaining;
                Tap(target); Check(!input.SelectedItem.HasValue && session.State.MovesRemaining == moves, "망치 적용/이동 수 유지");
                Click("Pause");
                Check(session.IsPaused && pause.gameObject.activeSelf, "연쇄 도중 pause 버튼");
                before = Snapshot(session.State);
                for (int i = 0; i < 10; i++) await UniTask.Yield();
                Check(before == Snapshot(session.State), "pause 10프레임 동결");
                await Capture("pause");
                PuzzleUIRenderVerification.SetSize(450, 800);
                for (int i = 0; i < 12; i++) await UniTask.Yield();
                Check(session.IsPaused && before == Snapshot(session.State), "회전 후 pause/보드/난수 유지");
                Click("PuzzlePausePopup/Panel/Primary"); await Ready(session);
                Check(!session.IsPaused && !pause.gameObject.activeSelf, "재개 버튼");
                Click("Pause"); Click("PuzzlePausePopup/Panel/Retry"); await Ready(session);
                Check(initial == Snapshot(session.State), "pause 다시하기 원래 입력/시드");

                ActionCandidate swap = ActionQuery.Find(session.State).First(a => a.Second.HasValue);
                before = Snapshot(session.State); moves = session.State.MovesRemaining;
                Click("PuzzleItemBar/swap"); Tap(swap.First);
                if (!input.Selected.HasValue)
                {
                    Vector2 p = session.BoardCamera.WorldToScreenPoint(board.transform.TransformPoint(PuzzleWorldBoard.CellPosition(swap.First)));
                    var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = p }, hits);
                    results.Add("TRACE point=" + p + " rect=" + session.BoardCamera.pixelRect + " coordinate=" + input.TryGetCoordinate(p, out BoardCoordinate actual) + "/" + actual + " selectable=" + session.CanSelectItemTarget(BoardItem.Swap, swap.First) + " hits=" + string.Join(",", hits.Select(h => h.gameObject.name)));
                    foreach (var hit in hits)
                    {
                        var corners = new Vector3[4]; var rect = hit.gameObject.GetComponent<RectTransform>(); rect.GetWorldCorners(corners);
                        results.Add("TRACE hit=" + hit.gameObject.transform.parent.name + " corners=" + string.Join(";", corners.Select(v => v.ToString())) + " missionRect=" + safe.Find("PuzzleHUD/Missions").localPosition + " canvasScale=" + screen.GetComponent<Canvas>().scaleFactor);
                    }
                    await Capture("retry-raycast-failure");
                }
                Check(input.Selected.HasValue && before == Snapshot(session.State), "자리 바꾸기 첫 칸은 실행하지 않음: item=" + input.SelectedItem + ", selected=" + input.Selected + ", unchanged=" + (before == Snapshot(session.State)) + ", blocked=" + Get(input, "uiBlocked") + ", can=" + session.CanUseItems + ", target=" + swap.First);
                BoardCoordinate far = session.State.Cells.First(c => session.CanSelectItemTarget(BoardItem.Swap, c.Coordinate) && Math.Abs(c.Coordinate.Row - swap.First.Row) + Math.Abs(c.Coordinate.Column - swap.First.Column) > 1).Coordinate;
                Tap(far);
                Check(before == Snapshot(session.State) && input.SelectedItem == BoardItem.Swap && !string.IsNullOrEmpty(input.SelectionMessage), "잘못된 두 번째 칸 원자성/이유 표시");
                await UniTask.Yield();
                Check(safe.Find("SelectedCell").gameObject.activeSelf, "선택 칸 표시");
                await Capture("swap-selected");
                Tap(swap.Second.Value);
                Check(!input.SelectedItem.HasValue && session.State.MovesRemaining == moves, "자리 바꾸기 두 칸/이동 수 유지"); await Ready(session);
                Click("PuzzleItemBar/shuffle");
                Check(session.State.MovesRemaining == moves, "섞기 이동 수 유지"); await Ready(session);
                Check(MatchQuery.Find(session.State).Count == 0 && ActionQuery.Find(session.State).Count > 0, "섞기 안정된 진행 가능 보드");
                LevelDefinition isolated = (LevelDefinition)Invoke(typeof(BoardActionVerification), "Make",
                    Enumerable.Range(0, 2).ToDictionary(i => new BoardCoordinate(0, i * 2), i => i), 20);
                await Fixture(isolated); before = Snapshot(session.State);
                Click("PuzzleItemBar/shuffle");
                Check(before == Snapshot(session.State) && safe.Find("ItemPrompt/Status").GetComponent<UnityEngine.UI.Text>().text == session.Message,
                    "섞기 실패 원자성/실제 이유 표시");

                // 기존 종료 검증 fixture를 실제 화면 세션에 주입하여 UI 종료 시점을 검사한다.
                LevelDefinition won = (LevelDefinition)Invoke(typeof(RecoveryVerification), "PlayFixture");
                JsonUtility.FromJsonOverwrite("{\"moveCount\":3}", won);
                await Fixture(won); Check(session.TryActivate(new BoardCoordinate(BoardDefinition.DefaultRows - 1, 0)), "승리 fixture 발동");
                await ProgressUntil(session, () => session.Outcome != null);
                Check(session.Outcome?.Kind == BoardOutcomeKind.Won && session.Phase != BoardActionPhase.Stopped, "승리 확정/라스트팡 분리");
                Check(!result.gameObject.activeSelf, "라스트팡 전 결과 팝업 금지");
                await ProgressUntil(session, () => session.ResultReady);
                Check(result.gameObject.activeSelf && !session.CanAcceptInput, "라스트팡 완료 후 결과/입력 차단");
                Check(result.Find("Panel/Title").GetComponent<UnityEngine.UI.Text>().text == "정리 완료!", "승리 제목");
                await Capture("won");
                Click("PuzzleResultPopup/Panel/Primary"); await Ready(session); Check(initial == Snapshot(session.State), "결과 다시하기");

                LevelDefinition lost = (LevelDefinition)Invoke(typeof(PowerEffectVerification), "Make");
                Invoke(typeof(PowerEffectVerification), "Place", lost, new BoardCoordinate(BoardDefinition.DefaultRows - 1, 0), InitialBlockKind.Rocket, RocketDirection.Horizontal, RabbitColor.Type1);
                JsonUtility.FromJsonOverwrite("{\"moveCount\":1,\"missions\":[{\"kind\":0,\"color\":0,\"count\":100}]}", lost);
                await Fixture(lost); session.TryActivate(new BoardCoordinate(BoardDefinition.DefaultRows - 1, 0));
                await ProgressUntil(session, () => session.ResultReady);
                Check(session.Outcome?.Kind == BoardOutcomeKind.MovesExhausted && result.gameObject.activeSelf, "이동 소진 결과 팝업");
                await Capture("lost");
                Click("PuzzleResultPopup/Panel/Primary"); await Ready(session);
                Check(screen.GetComponentsInChildren<PuzzleMissionView>(true).Length <= 4, "반복 판 전환 미션 뷰 재사용");
                int subscribers = ((Delegate)Get(session, "Changed")).GetInvocationList().Length;
                await UniTask.Yield();
                int definitions = Resources.FindObjectsOfTypeAll<LevelDefinition>().Length;
                for (int repeat = 0; repeat < 5; repeat++)
                {
                    Click("Pause"); Click("PuzzlePausePopup/Panel/Retry"); await Ready(session); await UniTask.Yield();
                    Check(initial == Snapshot(session.State) && ((Delegate)Get(session, "Changed")).GetInvocationList().Length == subscribers &&
                        Resources.FindObjectsOfTypeAll<LevelDefinition>().Length == definitions && UnityEngine.Object.FindObjectsByType<PuzzleScreenView>(FindObjectsSortMode.None).Length == 1,
                        "실제 UI 5회 재시작 스냅샷/구독/사본/화면 보존 " + repeat);
                }

                async UniTask Fixture(LevelDefinition definition)
                {
                    session.enabled = false; InvokeInstance(session, "ClearProgress"); InvokeInstance(session, "ResetPresentation");
                    LevelRuntimeState state = LevelStateBuilder.Build(definition, 12345).State;
                    PuzzleArtwork art = new PuzzleArtwork(); await art.PrepareAsync(state, CancellationToken.None);
                    ((PuzzleArtwork)Get(session, "artwork")).Dispose(); Set(session, "artwork", art);
                    Set(session, "executor", new BoardActionExecutor(state));
                    InvokeInstance(session, "InitializeProgress"); session.enabled = true; InvokeInstance(session, "Draw"); UnityEngine.Object.Destroy(definition);
                }
                void Click(string path)
                {
                    var button = safe.Find(path)?.GetComponent<UnityEngine.UI.Button>();
                    if (button == null || !button.isActiveAndEnabled || !button.interactable) throw new Exception("버튼 사용 불가 " + path);
                    ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
                }
                void Tap(BoardCoordinate at)
                {
                    Vector2 point = session.BoardCamera.WorldToScreenPoint(board.transform.TransformPoint(PuzzleWorldBoard.CellPosition(at)));
                    InvokeInstance(input, "BeginPointer", 81, point); InvokeInstance(input, "EndPointer", 81, point);
                }
            }
            catch (Exception error) { results.Add("FAIL " + error); Debug.LogException(error); }
            finally { File.WriteAllLines(PuzzleUIStateVerification.Output + "interaction-results.txt", results); PuzzleUIRenderVerification.RestoreSize(); EditorApplication.ExitPlaymode(); }
        }
        private static async UniTask Ready(PuzzleGameSession session)
        { for (int i = 0; i < 1800 && !session.CanAcceptInput && !session.HasFailed; i++) await UniTask.Yield(); if (!session.CanAcceptInput) throw new Exception(session.Message); }
        private static async UniTask ProgressUntil(PuzzleGameSession session, Func<bool> complete)
        {
            float previous = Time.timeScale; bool enabled = session.enabled; Time.timeScale = 0; session.enabled = false;
            try
            {
                for (int frame = 0; frame < 20000 && !complete() && !session.HasFailed; frame++)
                {
                    float deadline = Time.realtimeSinceStartup + 20;
                    while ((bool)Get(session, "preparingEffects") && Time.realtimeSinceStartup < deadline) await UniTask.Yield();
                    if ((bool)Get(session, "preparingEffects")) throw new Exception("실제 UI 파워 준비 timeout");
                    InvokeInstance(session, "TickProgress", .02f);
                    if (session.IsPresenting) InvokeInstance(session, "AdvancePresentation", .02f); else InvokeInstance(session, "Advance");
                    await UniTask.Yield();
                }
                Check(complete() && !session.HasFailed, "실제 UI 표시·수집·결과 경계 완료");
            }
            finally { Time.timeScale = previous; session.enabled = enabled; }
        }
        private static async UniTask Capture(string name)
        { await UniTask.Yield(); ScreenCapture.CaptureScreenshot(PuzzleUIStateVerification.Output + name + ".png"); await UniTask.Delay(250); }
        private static Rect Bounds(RectTransform rect)
        { var corners = new Vector3[4]; rect.GetWorldCorners(corners); return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y); }
        private static string Snapshot(LevelRuntimeState state) => (string)Invoke(typeof(LevelInitialStateVerification), "Snapshot", state);
        private static object Invoke(Type type, string method, params object[] args) => type.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
        private static object InvokeInstance(object instance, string name, params object[] args) => instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(instance, args);
        private static object Get(object instance, string name) => instance.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(instance);
        private static void Set(object instance, string name, object value) => instance.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(instance, value);
        private static void Check(bool condition, string name) { if (!condition) throw new Exception(name); results.Add("PASS " + name); }
    }
}
