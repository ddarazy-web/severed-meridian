using System;
using System.Reflection;
using Cysharp.Threading.Tasks;
using PopupUI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GameScreen.Editor
{
    public static partial class PuzzlePopupVerification
    {
        private static async UniTask RestoreChecks(PuzzleScreenView screen, PuzzlePopupBinding previousBinding, PuzzleGameSession session)
        {
            Scene original = SceneManager.GetActiveScene();
            Scene first = default, away = default;
            GameObject nextScreen = null, invalidHostObject = null;
            PopupService service = previousBinding.Service;
            PuzzleBoardInput input = session.GetComponent<PuzzleBoardInput>();
            int commands = 0, resultSounds = 0; bool wasRestarting = false;
            Action changed = () => { if (session.IsRestarting && !wasRestarting) commands++; wasRestarting = session.IsRestarting; };
            Action<PuzzleFeedbackCueKind> audio = kind => { if (kind == PuzzleFeedbackCueKind.Win || kind == PuzzleFeedbackCueKind.Lose) resultSounds++; };
            session.Changed += changed;
            if (session.AudioPlayback != null) session.AudioPlayback.Played += audio;
            try
            {
                first = SceneManager.CreateScene("PopupStage03OwnedReturn");
                away = SceneManager.CreateScene("PopupStage03OwnedAway");
                SceneManager.MoveGameObjectToScene(screen.transform.root.gameObject, first);
                SceneManager.SetActiveScene(first); previousBinding.Refresh();
                previousBinding.OpenDescription("씬 왕복 설명 값");
                PopupHandle pause = service.Open(PuzzlePopupBinding.PauseId, new PuzzlePauseState());
                PuzzlePauseView oldPause = (PuzzlePauseView)service.GetView(pause);
                typeof(PuzzlePopupBinding).GetMethod("BindPause", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(previousBinding, new object[] { oldPause });
                UnityEngine.Events.UnityAction oldRetry = (UnityEngine.Events.UnityAction)typeof(PuzzlePauseView)
                    .GetField("onRetry", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(oldPause);
                PopupContext context = previousBinding.Host.Context;
                int count = service.Count; string logical = session.LogicalSessionId; object state = session.State;
                PopupExitTicket rollback = service.BeginSceneExit(true); service.RollbackSceneExit(rollback);
                Check(service.Count == count && service.Top == pause && session.IsPaused, "실제 게임 이동 취소 현재 팝업 순서 정지 보존");
                PopupExitTicket ticket = service.BeginSceneExit(true); service.CommitSceneExit(ticket);
                oldRetry(); previousBinding.Retry();
                Check(service.Count == 0 && previousBinding.Host.Service == null && commands == 0 && !session.IsPaused,
                    "실제 게임 이동 확정 뷰 요청 정리 이전 Retry 호출0");
                screen.gameObject.SetActive(false);
                SceneManager.SetActiveScene(away);
                await SceneManager.UnloadSceneAsync(first); first = default;
                Check(SceneManager.GetActiveScene() == away && service.Inspect().Stored.Count == 1 &&
                    session.LogicalSessionId == logical && ReferenceEquals(session.State, state),
                    "시험 씬 실제 이탈 보관만 유지 게임 진행 저장 없음");
                first = SceneManager.CreateScene("PopupStage03OwnedReturn"); SceneManager.SetActiveScene(first);
                invalidHostObject = new GameObject("Owned-invalid-game-host", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(PopupHost));
                SceneManager.MoveGameObjectToScene(invalidHostObject, first);
                PopupHost invalid = invalidHostObject.GetComponent<PopupHost>(); invalid.Attach(service, context);
                PopupRestoreResult failed = service.Restore(invalid, context);
                await UniTask.Yield();
                Check(failed.Status == PopupRestoreStatus.Failed && service.Count == 0 && service.Inspect().Stored.Count == 1 &&
                    invalid.GetComponentsInChildren<PopupView>(true).Length == 0 && !session.IsPaused,
                    "현재 게임 Binding 없는 복원 후보 실패 뷰0 보관 유지 재시도 가능");
                PuzzlePopupBinding unreadyBinding = invalidHostObject.AddComponent<PuzzlePopupBinding>();
                failed = service.Restore(invalid, context); await UniTask.Yield();
                Check(failed.Status == PopupRestoreStatus.Failed && service.Count == 0 && service.Inspect().Stored.Count == 1 &&
                    invalid.GetComponentsInChildren<PopupView>(true).Length == 0 && commands == 0 && resultSounds == 0,
                    "미구성 Binding 복원 거부 후보0 보관 유지 명령 결과음0");
                unreadyBinding.enabled = false;
                failed = service.Restore(invalid, context); await UniTask.Yield();
                Check(failed.Status == PopupRestoreStatus.Failed && service.Count == 0 && service.Inspect().Stored.Count == 1 &&
                    invalid.GetComponentsInChildren<PopupView>(true).Length == 0 && commands == 0 && resultSounds == 0,
                    "비활성 Binding 복원 거부 후보0 보관 유지 명령 결과음0");
                invalid.Detach(); UnityEngine.Object.Destroy(invalidHostObject); invalidHostObject = null;
                nextScreen = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Puzzle/PuzzleScreen.prefab"));
                SceneManager.MoveGameObjectToScene(nextScreen, first);
                PuzzlePopupBinding current = nextScreen.GetComponent<PuzzlePopupBinding>(); current.Host.Attach(service, context);
                nextScreen.GetComponent<PuzzleScreenView>().Configure(session, input);
                Check(current.Service == service && service.Count == 0, "새 게임 Binding 보관 서비스 채택 자동 복원 없음");
                PopupRestoreResult restored = service.Restore(current.Host, context);
                Check(restored.Status == PopupRestoreStatus.Restored && restored.Handles.Count == count &&
                    service.Count == count && session.IsPaused && input.IsUIBlocked && commands == 0 && resultSounds == 0,
                    "실제 씬 왕복 명시 복원 순서 정지 차단 현재 명령0 결과음0");
                PopupInspectionItem top = service.Inspect().Items[service.Count - 1];
                Check(top.Id == PuzzlePopupBinding.PauseId && service.Inspect().Stored.Count == 0,
                    "게임 복원 최상위 pause 보관 소비");
                PuzzlePauseView newPause = (PuzzlePauseView)service.GetView(top.Handle);
                Button retry = (Button)typeof(PuzzlePauseView).GetField("retry", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(newPause);
                // 백그라운드 대기 중 실제 두 입력으로 첫 명령만 수신하는지 관찰한다.
                session.SendMessage("OnApplicationPause", true);
                ExecuteEvents.Execute(retry.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
                ExecuteEvents.Execute(retry.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
                Check(commands == 1 && session.IsRestarting && !retry.interactable && current.Service == service,
                    "복원 버튼 현재 Binding 실제 Retry 수신1 중복0");
                session.SendMessage("OnApplicationPause", false);
                await UniTask.WaitUntil(() => !session.IsRestarting).Timeout(TimeSpan.FromSeconds(30));
                Check(session.LogicalSessionId != logical && service.Count == 0 && service.Inspect().Stored.Count == 0 && !session.IsPaused,
                    "복원 후 Retry 성공 새 문맥 이전 팝업 보관 폐기");
                await UniTask.WaitUntil(() => session.CanAcceptInput).Timeout(TimeSpan.FromSeconds(30));
            }
            finally
            {
                session.Changed -= changed;
                if (session.AudioPlayback != null) session.AudioPlayback.Played -= audio;
                session.SendMessage("OnApplicationPause", false);
                if (nextScreen != null) UnityEngine.Object.Destroy(nextScreen);
                if (invalidHostObject != null) UnityEngine.Object.Destroy(invalidHostObject);
                SceneManager.SetActiveScene(original);
                if (first.IsValid() && first.isLoaded) await SceneManager.UnloadSceneAsync(first);
                if (away.IsValid() && away.isLoaded) await SceneManager.UnloadSceneAsync(away);
            }
        }
    }
}
