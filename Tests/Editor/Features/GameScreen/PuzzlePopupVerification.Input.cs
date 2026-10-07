using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using PopupUI;
using Simulation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace GameScreen.Editor
{
    public static partial class PuzzlePopupVerification
    {
        private static async UniTask InputAndOwnershipChecks(PuzzleScreenView screen, PuzzlePopupBinding binding, PuzzleGameSession session)
        {
            InputSettings originalSettings = InputSystem.settings;
            InputSettings settings = UnityEngine.Object.Instantiate(originalSettings);
            Keyboard keyboard = null; Mouse mouse = null;
            PuzzleBoardInput input = session.GetComponent<PuzzleBoardInput>();
            float scale = Time.timeScale;
            try
            {
                settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                InputSystem.settings = settings; keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>();
                Application.runInBackground = true; PuzzleUIRenderVerification.SetSize(1280, 720);
                PopupCatalog.Entry[] entries = binding.Catalog.Entries;
                Check(entries.Length == 3 && entries.All(entry => !entry.AllowMultiple && entry.Restorable) && entries.Count(entry => entry.PauseGameplay) == 1 && entries.Single(entry => entry.PauseGameplay).Id == PuzzlePopupBinding.PauseId && !entries.Single(entry => entry.Id == PuzzlePopupBinding.ResultId).CloseOnCancel && entries.Where(entry => entry.Id != PuzzlePopupBinding.ResultId).All(entry => entry.CloseOnCancel), "세 게임 종류 단일 복원 정지 취소 정책");
                PopupHandle d = binding.OpenDescription("값 복사 시험");
                PopupView descriptionView = binding.Service.GetView(d);
                PuzzleDescriptionState copied = (PuzzleDescriptionState)descriptionView.CaptureState().Copy(); copied.Text = "수정된 사본";
                Check(((PuzzleDescriptionState)descriptionView.CaptureState()).Text.Contains("값 복사 시험"), "실제 설명 값 캡처 복사본 수정 원본 불변");
                PopupHandle p = binding.OpenPause(); PopupView pauseView = binding.Service.GetView(p);
                for (int repeat = 0; repeat < 10; repeat++) binding.Refresh();
                Check(binding.Service.Count == 2 && binding.Service.Top == p && binding.Service.GetView(d) == descriptionView && binding.Service.GetView(p) == pauseView, "반복 Refresh 인스턴스 순서 Top 불변");
                await UniTask.Yield(); Canvas.ForceUpdateCanvases();
                Button below = descriptionView.DefaultSelection.GetComponent<Button>();
                ExecuteEvents.Execute(below.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
                Check(!below.IsInteractable() && binding.Service.Count == 2 && binding.Service.Top == p, "실제 하위 설명 Submit 호출0");
                string before = string.Join("|", session.State.Cells.Select(cell => cell.Content + ":" + cell.Color + ":" + cell.CoverDurability));
                int moves = session.State.MovesRemaining;
                Button[] background = screen.GetComponentInChildren<PuzzleItemBarView>().GetComponentsInChildren<Button>();
                Button mission = screen.GetComponentInChildren<PuzzleHudView>().GetComponentsInChildren<Button>().First();
                int backgroundCalls = 0;
                UnityEngine.Events.UnityAction countBackground = () => backgroundCalls++;
                foreach (Button button in background.Append(mission)) button.onClick.AddListener(countBackground);
                try
                {
                    foreach (Button button in background.Append(mission))
                    {
                        Vector2 point = RectTransformUtility.WorldToScreenPoint(null, button.transform.position);
                        List<RaycastResult> hits = new List<RaycastResult>(); PointerEventData data = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };
                        EventSystem.current.RaycastAll(data, hits);
                        Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() != button, "게임 HUD 아이템 실제 raycast 차단 " + button.name);
                        GameObject target = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
                        if (target != null) ExecuteEvents.Execute(target, data, ExecuteEvents.pointerClickHandler);
                    }
                    PuzzleWorldBoard board = UnityEngine.Object.FindFirstObjectByType<PuzzleWorldBoard>();
                    Vector2 start = session.BoardCamera.WorldToScreenPoint(board.transform.TransformPoint(PuzzleWorldBoard.CellPosition(new Board.BoardCoordinate(3, 3))));
                    Vector2 end = session.BoardCamera.WorldToScreenPoint(board.transform.TransformPoint(PuzzleWorldBoard.CellPosition(new Board.BoardCoordinate(3, 4))));
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = start, buttons = 1 }); InputSystem.Update(); await UniTask.Yield();
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = end, buttons = 1 }); InputSystem.Update(); await UniTask.Yield();
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = end }); InputSystem.Update(); await UniTask.Yield();
                    input.SelectItem(BoardItem.Shuffle);
                    Check(backgroundCalls == 0 && input.SelectedItem == null && moves == session.State.MovesRemaining && before == string.Join("|", session.State.Cells.Select(cell => cell.Content + ":" + cell.Color + ":" + cell.CoverDurability)) && binding.Service.Top == p, "팝업 아래 HUD 아이템 스와이프 호출0 보드 불변");
                }
                finally { foreach (Button button in background.Append(mission)) button.onClick.RemoveListener(countBackground); }
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.Escape)); InputSystem.Update(); await UniTask.Yield(); await UniTask.Yield();
                Check(binding.Service.Count == 1 && binding.Service.Top == d && !session.IsPaused && input.IsUIBlocked, "실제 Escape 최상위 일시정지만 한 번 닫기 하위 유지");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update(); await UniTask.Yield();
                ExecuteEvents.Execute(descriptionView.DefaultSelection, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
                Check(binding.Service.Count == 0 && !input.IsUIBlocked, "새 최상위 설명 실제 Submit 닫기");
                Check(session.SetPaused(true), "외부 정지 요청 허용");
                p = binding.OpenPause(); binding.Service.Close(p);
                Check(session.IsPaused && input.IsUIBlocked, "마지막 팝업 닫기 외부 정지 유지");
                session.SetPaused(false); Check(!session.IsPaused, "외부 소유자가 정지 해제");
                input.SetUIBlocked(true); d = binding.OpenDescription("외부 차단 보존"); binding.Service.Close(d);
                Check(input.IsUIBlocked, "마지막 팝업 닫기 외부 입력 차단 유지"); input.SetUIBlocked(false);
                Check(!input.IsUIBlocked && Time.timeScale == scale, "각 소유자 해제 뒤 입력 복귀 TimeScale 불변");
                Action<string> lateDescription = screen.GetComponentInChildren<PuzzleHudView>().Describe;
                PopupService retained = binding.Service; PopupHost host = binding.Host;
                session.SetPaused(true); input.SetUIBlocked(true); binding.OpenPause();
                screen.gameObject.SetActive(false);
                lateDescription("종료된 이전 HUD 설명");
                Check(retained.Count == 0 && host.Service == null && session.IsPaused && input.IsUIBlocked, "화면 종료 뷰0 Host해제 이전 설명 콜백0 외부 요청 보존");
                session.SetPaused(false); input.SetUIBlocked(false);
            }
            finally
            {
                if (keyboard != null) InputSystem.RemoveDevice(keyboard); if (mouse != null) InputSystem.RemoveDevice(mouse);
                InputSystem.settings = originalSettings; UnityEngine.Object.Destroy(settings);
            }
        }
    }
}
