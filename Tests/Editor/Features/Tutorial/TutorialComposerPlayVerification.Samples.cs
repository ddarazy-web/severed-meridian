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
using Simulation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tutorial.Editor
{
    public static partial class TutorialComposerPlayVerification
    {
        private static readonly string[] SampleIds = { "damage", "follow", "two", "hammer", "area", "mission", "item-swap", "shuffle" };

        private static void PrepareSample(LevelEditorWindow owner, int index)
        {
            string id = SampleIds[index / 2], path = SessionState.GetString(Key + "folder", "") + "/Sample.asset";
            LevelDefinition data = AssetDatabase.LoadAssetAtPath<LevelDefinition>(path);
            LevelDefinition source = TutorialSampleBoards.All.First(value => value.Id == id).CreateBoard();
            try { EditorUtility.CopySerialized(source, data); }
            finally { UnityEngine.Object.DestroyImmediate(source); }
            data.hideFlags = HideFlags.None;
            if (id == "damage") data.Tutorial.steps[0].conditions.Clear();
            EditorUtility.SetDirty(data); owner.SetLevel(data); owner.CreateGUI();
            if (id == "damage")
            {
                void Click(string name)
                {
                    Button button = owner.rootVisualElement.Q<Button>(name);
                    Check(button != null, "조건 조립 제어 제공 " + name);
                    using NavigationSubmitEvent click = NavigationSubmitEvent.GetPooled(); click.target = button; button.SendEvent(click);
                }
                Click("tutorial-condition-add-DurabilityDecrease");
                owner.rootVisualElement.Q<PopupField<string>>("tutorial-target-kind-0").value = "특정 개체";
                Click("tutorial-target-pick-0");
                owner.rootVisualElement.Q<LevelBoardView>().TutorialTargetPicked(new BoardCoordinate(4, 4));
                owner.rootVisualElement.Q<Toggle>("tutorial-origin-0-Rocket").value = true;
                Check(data.Tutorial.steps[0].conditions.Single().allowedOrigins.SequenceEqual(new[] { EffectOrigin.Rocket }), "편집 UI로 대상·감소 조건·원인을 조립");
            }
            string authored = JsonUtility.ToJson(data.Tutorial);
            AssetDatabase.SaveAssetIfDirty(data); owner.SetLevel(null); Resources.UnloadAsset(data);
            data = AssetDatabase.LoadAssetAtPath<LevelDefinition>(path);
            Check(JsonUtility.ToJson(data.Tutorial) == authored, "새 조건 실제 에셋 저장·언로드·재로드 " + id);
            File.WriteAllBytes(LevelPackBuild.FilePath(Number), LevelPackCodec.Snapshot(data)); AssetDatabase.ImportAsset(LevelPackBuild.FilePath(Number));
            SessionState.SetBool("Puzzle.EditorLaunch.tutorialCompleted." + Number, false);
        }

        private static void Gesture(PuzzleGameSession session, BoardCoordinate first, BoardCoordinate second)
        {
            PuzzleBoardInput input = UnityEngine.Object.FindFirstObjectByType<PuzzleBoardInput>();
            Vector2 start = session.BoardCamera.WorldToScreenPoint(session.TutorialCellWorldPosition(first));
            Vector2 end = session.BoardCamera.WorldToScreenPoint(session.TutorialCellWorldPosition(second));
            foreach (string method in new[] { "BeginPointer", "UpdatePointer", "EndPointer" })
                typeof(PuzzleBoardInput).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(input, new object[] { -1, method == "BeginPointer" ? start : end });
        }

        private static async UniTask VerifySamplePlay(PuzzleGameSession session, TutorialOverlayView view)
        {
            int index = SessionState.GetInt(Key + "case", 0); string id = SampleIds[index / 2];
            await Wait(() => session.CanAcceptInput && session.TutorialState.State == TutorialProgressState.AwaitAction);
            string initial = string.Join(";", session.State.Cells.Select(cell => cell.Coordinate + ":" + cell.Content + ":" + cell.Color));
            foreach (Vector2Int size in new[] { new Vector2Int(1280, 720), new Vector2Int(720, 1280) })
            {
                PuzzleUIRenderVerification.SetSize(size.x, size.y); await Wait(() => Screen.width == size.x && Screen.height == size.y);
                await UniTask.DelayFrame(3, PlayerLoopTiming.LastPostLateUpdate); Canvas.ForceUpdateCanvases();
                Check(view.Content.gameObject.activeInHierarchy, "샘플 안내 표시 " + id + "/" + index + "/" + size);
                foreach (BoardCoordinate cell in session.TutorialState.Highlights)
                {
                    UnityEngine.UI.Graphic graphic = view.Content.Find("Cell" + (cell.Row * 9 + cell.Column)).GetComponent<UnityEngine.UI.Graphic>();
                    Check(graphic.color.a == 0 && !graphic.raycastTarget, "조건·조작 대상의 투명 포커스 " + id + " " + cell);
                    Rect bubble = new Rect(view.Bubble.anchoredPosition - view.Bubble.rect.size / 2, view.Bubble.rect.size);
                    Rect focus = new Rect(graphic.rectTransform.anchoredPosition - graphic.rectTransform.rect.size / 2, graphic.rectTransform.rect.size);
                    Check(!bubble.Overlaps(focus), "안내 말풍선이 강조 대상을 가리지 않음 " + id + " " + cell);
                }
                ScreenCapture.CaptureScreenshot(Output + id + "-" + index + "-" + size.x + "x" + size.y + ".png");
                await UniTask.Delay(150, ignoreTimeScale: true);
            }
            int actions = 0, moves = session.State.MovesRemaining;
            while (session.TutorialState.State != TutorialProgressState.Completed && actions++ < 6)
            {
                TutorialProgressSnapshot snapshot = session.TutorialState;
                File.AppendAllText(Output + "play-results.txt", $"TRACE {id}/{actions} before state={snapshot.State} input={session.CanAcceptInput} phase={session.Phase} moves={session.State.MovesRemaining} counts={string.Join(",", snapshot.ConditionCounts)} first={snapshot.First} second={snapshot.Second}\n");
                if (snapshot.Item.HasValue)
                {
                    PuzzleItemBarView items = UnityEngine.Object.FindFirstObjectByType<PuzzleItemBarView>();
                    UnityEngine.UI.Button button = items.ButtonRect(snapshot.Item.Value).GetComponent<UnityEngine.UI.Button>();
                    Check(button.interactable, "무료 아이템 UI 버튼 허용 " + id); button.onClick.Invoke();
                    if (snapshot.First.HasValue) Gesture(session, snapshot.First.Value, snapshot.First.Value);
                    if (snapshot.Second.HasValue) Gesture(session, snapshot.Second.Value, snapshot.Second.Value);
                }
                else Gesture(session, snapshot.First.Value, snapshot.Second.Value);
                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
                await Wait(() => !session.IsPresenting && !session.HasProgressFeedback && session.TutorialState.State != TutorialProgressState.AwaitPresentation);
                Check(!session.HasFailed && session.TutorialState.State != TutorialProgressState.Error, "실제 입력·연쇄·표시 완료 " + id + "/" + actions);
                File.AppendAllText(Output + "play-results.txt", $"TRACE {id}/{actions} after state={session.TutorialState.State} input={session.CanAcceptInput} phase={session.Phase} moves={session.State.MovesRemaining} counts={string.Join(",", session.TutorialState.ConditionCounts)}\n");
                if (id == "follow" && actions == 1)
                    Check(session.TutorialState.StepIndex == 1 && session.TutorialState.First?.Equals(new BoardCoordinate(6, 3)) == true &&
                        session.TutorialState.Highlights.Contains(new BoardCoordinate(6, 3)), "생성 후 낙하한 동일 로켓으로 안내·포커스 이동");
            }
            Check(session.TutorialState.State == TutorialProgressState.Completed && actions == (id == "two" || id == "follow" ? 2 : 1), "새 조건 실제 게임 완료 " + id);
            Check(session.State.MovesRemaining == moves - (id == "hammer" || id == "item-swap" || id == "shuffle" ? 0 : actions), "아이템과 교환 이동 소비 구분 " + id);
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
            Check(!view.Content.gameObject.activeInHierarchy && session.CanAcceptInput, "완료 뒤 강조·입력 제한 해제 " + id);
            Check(SessionState.GetBool("Puzzle.EditorLaunch.tutorialCompleted." + Number, false), "에디터 시험 문맥 완료 기록 " + id);

            await session.RestartAsync(CancellationToken.None); await Wait(() => session.CanAcceptInput && !session.HasProgressFeedback);
            Check(session.TutorialState.StepIndex == 0 && session.TutorialState.ConditionCounts.All(count => count == 0) &&
                initial == string.Join(";", session.State.Cells.Select(cell => cell.Coordinate + ":" + cell.Content + ":" + cell.Color)), "실제 재시작은 고정 보드·첫 단계·집계0 복원 " + id);
            SessionState.SetBool("Puzzle.EditorLaunch.tutorialCompleted." + Number, false);
            if (id == "damage")
            {
                TutorialBoardAdapter adapter = (TutorialBoardAdapter)typeof(PuzzleGameSession).GetField("tutorial", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session);
                session.SetPaused(true); adapter.Progress.Fail("시험: 안내 대상 소실"); await UniTask.DelayFrame(2);
                Check(!session.CanAcceptInput && !adapter.IsReleased, "실게임 일시정지 중 오류 복귀 보류");
                session.SetPaused(false); await Wait(() => session.CanAcceptInput && session.TutorialState.State == TutorialProgressState.Error);
                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
                Check(!session.HasFailed && !view.Content.gameObject.activeInHierarchy && session.State.Supply.Sources.All(source => source.Mode == SupplyMode.Random) &&
                    !SessionState.GetBool("Puzzle.EditorLaunch.tutorialCompleted." + Number, false), "실게임 안내 오류는 일반 공급·입력 복귀·완료 미기록");
            }
            if (id == "two")
            {
                typeof(LevelRuntimeState).GetProperty("MovesRemaining").SetValue(session.State, 1);
                Gesture(session, session.TutorialState.First.Value, session.TutorialState.Second.Value);
                await Wait(() => session.ResultReady);
                Check(session.Outcome.Kind == BoardOutcomeKind.MovesExhausted && session.TutorialState.State == TutorialProgressState.Cancelled &&
                    !SessionState.GetBool("Puzzle.EditorLaunch.tutorialCompleted." + Number, false), "실게임 미충족 이동 소진은 정상 실패·완료 미기록");
                PuzzleResultView result = null;
                await Wait(() => (result = UnityEngine.Object.FindFirstObjectByType<PuzzleResultView>()) != null && result.isActiveAndEnabled);
                UnityEngine.UI.Button retry = (UnityEngine.UI.Button)typeof(PuzzleResultView).GetField("retry", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(result);
                Check(retry.interactable, "실패 팝업 재도전 버튼 사용 가능"); retry.onClick.Invoke();
                await Wait(() => session.CanAcceptInput && !session.HasProgressFeedback && session.Outcome == null);
                Check(session.TutorialState.StepIndex == 0 && session.State.MovesRemaining == moves, "실제 실패 팝업 재도전으로 처음부터 복귀");
            }
            if (id == "follow")
            {
                LevelDefinition invalid = TutorialSampleBoards.All.First(sample => sample.Id == "follow").CreateBoard();
                try
                {
                    invalid.Tutorial.steps[1].second = new BoardCoordinate(0, 0);
                    typeof(PuzzleGameSession).GetField("initialBytes", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, LevelPackCodec.Snapshot(invalid));
                }
                finally { UnityEngine.Object.Destroy(invalid); }
                await session.RestartAsync(CancellationToken.None);
                await Wait(() => session.CanAcceptInput && !session.HasProgressFeedback);
                Check(!session.HasFailed && session.TutorialState == null && session.State.Supply.Sources.All(source => source.Mode == SupplyMode.Random) &&
                    !SessionState.GetBool("Puzzle.EditorLaunch.tutorialCompleted." + Number, false), "재시작 준비의 안내 오류는 일반 공급·입력 복귀·완료 미기록");
            }
            view.GetComponentInParent<PuzzleScreenView>().enabled = false; EditorApplication.ExitPlaymode();
        }
    }
}
