using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using PopupUI;
using Simulation;
using UnityEngine;

namespace GameScreen
{
    public sealed class PuzzlePopupBinding : MonoBehaviour
    {
        public const string PauseId = "puzzle.pause", ResultId = "puzzle.result", DescriptionId = "puzzle.description";
        [SerializeField] private PopupCatalog catalog;
        [SerializeField] private PopupHost host;
        private PuzzleGameSession game;
        private PuzzleBoardInput input;
        private PopupContext context;
        private BoardOutcome shownOutcome;
        private CancellationTokenSource commands;
        private bool syncing, detached;
        public PopupService Service { get; private set; }
        public PopupHost Host => host;
        public PopupCatalog Catalog => catalog;
        public void ConfigureAssets(PopupCatalog value, PopupHost surface) { catalog = value; host = surface; }
        public void Configure(PuzzleGameSession session, PuzzleBoardInput boardInput, PopupCatalog value, PopupHost surface)
        {
            if (game == session && input == boardInput && commands != null && !commands.IsCancellationRequested) { Refresh(); return; }
            Release(); game = session; input = boardInput;
            if (value != null) catalog = value; if (surface != null) host = surface;
            if (game == null || input == null || catalog == null || host == null) throw new InvalidOperationException("게임 팝업의 세션/입력/카탈로그/Host 연결이 필요합니다.");
            commands = new CancellationTokenSource();
            detached = false;
            Service = host.Service ?? new PopupService(catalog);
            host.InputBlockChanged += BlockInput; host.PauseRequestChanged += PauseGame;
            Service.Changed += OnServiceChanged; game.Changed += Refresh;
            Refresh();
        }
        private void BlockInput(bool blocked) { if (input != null) input.SetPopupUIBlocked(blocked); }
        private void PauseGame(bool paused) { if (game != null) game.SetPopupPaused(paused); }
        private void OnServiceChanged()
        {
            if (host == null || host.Service != Service) { if (!syncing) detached = true; return; }
            BlockInput(Service.Count > 0); Refresh();
        }
        public void Refresh()
        {
            if (syncing || detached || game == null || host == null || commands == null || commands.IsCancellationRequested || !isActiveAndEnabled) return;
            syncing = true;
            try
            {
                if (!game.IsReady || string.IsNullOrEmpty(game.LogicalSessionId)) return;
                PopupContext next = new PopupContext(host.gameObject.scene.name, "GameScreen:" + game.State.LevelNumber, game.LogicalSessionId);
                if (host.Service != null && !host.Context.Equals(next))
                { Service.Discard(host.Context); host.Detach(); }
                if (!context.Equals(next)) { context = next; shownOutcome = null; }
                if (host.Service == null) host.Attach(Service, context);
                // 호출자가 보관 서비스를 연결했다면 Restore 전에는 새 팝업을 만들지 않는다.
                foreach (PopupStoredInfo stored in Service.Inspect().Stored)
                    if (stored.Context.Equals(context)) return;
                PuzzlePauseView pause = FindView(PauseId) as PuzzlePauseView;
                if (pause != null) pause.ApplyState(new PuzzlePauseState { Busy = game.IsRestarting || game.IsChangingLevel });
                PuzzleResultView result = FindView(ResultId) as PuzzleResultView;
                if (game.IsRestarting)
                {
                    // 후보 준비 중에도 기존 결과와 순서를 보존하고 사용자 명령만 잠근다.
                    if (result != null)
                    {
                        PuzzleResultState pending = (PuzzleResultState)result.CaptureState();
                        pending.Busy = true; pending.NextEnabled = false;
                        pending.Body = "남은 이동 " + game.State.MovesRemaining + "\n" + game.Message;
                        result.ApplyState(pending);
                    }
                    return;
                }
                if (!game.ResultReady)
                {
                    shownOutcome = null;
                    if (result != null) Service.Close(result.Handle);
                    return;
                }
                bool won = game.Outcome.Kind == BoardOutcomeKind.Won;
                string body = "남은 이동 " + game.State.MovesRemaining + "\n" + game.Message;
                if (won && !game.LevelAdvanceEnabled) body += "\n다음 레벨은 MemoryPack 모드에서 이어서 플레이할 수 있습니다";
                PuzzleResultState state = new PuzzleResultState { Title = won ? "정리 완료!" : "다시 도전해요", Body = body,
                    NextVisible = won && game.LevelAdvanceEnabled, NextEnabled = game.CanAdvanceLevel, Busy = game.IsChangingLevel || game.IsRestarting };
                if (result != null) { result.ApplyState(state); shownOutcome = game.Outcome; }
                else if (shownOutcome != game.Outcome)
                {
                    PopupHandle handle = Service.Open(ResultId, state); result = (PuzzleResultView)Service.GetView(handle);
                    BindResult(result); shownOutcome = game.Outcome; game.PlayResultFeedback();
                }
            }
            finally { syncing = false; }
        }
        private PopupView FindView(string id)
        {
            foreach (PopupInspectionItem item in Service.Inspect().Items)
                if (item.Id == id) return Service.GetView(item.Handle);
            return null;
        }
        public PopupHandle OpenPause()
        {
            Refresh();
            if (!CanCommand() || !game.IsReady || game.IsRestarting || game.IsChangingLevel || (game.Outcome != null && !game.IsPresenting && !game.HasProgressFeedback)) throw new InvalidOperationException("지금은 일시정지 팝업을 열 수 없습니다.");
            input.CancelGesture(); input.CancelItemSelection();
            PopupHandle handle = Service.Open(PauseId, new PuzzlePauseState()); BindPause((PuzzlePauseView)Service.GetView(handle)); return handle;
        }
        public PopupHandle OpenDescription(string text)
        {
            Refresh(); if (!CanCommand()) throw new InvalidOperationException("현재 게임 연결이 없습니다.");
            input.CancelGesture(); input.CancelItemSelection();
            return Service.Open(DescriptionId, new PuzzleDescriptionState { Text = text + "\n\n눌러서 닫기" });
        }
        internal void BindPause(PuzzlePauseView view) { view.Bind(() => { if (CanCommand()) view.Close(); }, Retry); }
        internal void BindResult(PuzzleResultView view) { view.Bind(Retry, NextLevel); }
        /// <summary>복원 후보가 현재 게임과 표시 영역에 연결됐는지 확인한다.</summary>
        /// <param name="view">아직 활성화되지 않은 복원 후보.</param>
        /// <param name="restoredContext">보관 값의 문맥.</param>
        internal void ValidateRestoreConnection(PopupView view, PopupContext restoredContext)
        {
            if (!CanCommand() || !game.IsReady || !game.isActiveAndEnabled || input == null || !input.isActiveAndEnabled ||
                !context.Equals(restoredContext) || view.GetComponentInParent<PopupHost>() != host)
                throw new InvalidOperationException("현재 게임 팝업 연결이 복원 준비되지 않았습니다.");
        }
        private bool CanCommand() => isActiveAndEnabled && game != null && host != null && commands != null && !commands.IsCancellationRequested &&
            host.Service == Service && host.Context.Equals(context) && context.SessionKey == game.LogicalSessionId;
        public void Retry()
        {
            if (!CanCommand() || game.IsRestarting || game.IsChangingLevel) return;
            input.CancelGesture(); input.CancelItemSelection(); game.RestartAsync(commands.Token).Forget(Debug.LogException);
        }
        public void NextLevel()
        {
            if (!CanCommand() || !game.CanAdvanceLevel) return;
            input.CancelGesture(); input.CancelItemSelection(); game.AdvanceLevelAsync(commands.Token).Forget(Debug.LogException);
        }
        public void Release()
        {
            bool ownsHost = host != null && Service != null && host.Service == Service;
            commands?.Cancel(); commands?.Dispose(); commands = null;
            if (game != null) game.Changed -= Refresh;
            if (Service != null) Service.Changed -= OnServiceChanged;
            if (host != null)
            {
                host.InputBlockChanged -= BlockInput; host.PauseRequestChanged -= PauseGame;
                if (ownsHost) host.Detach();
            }
            // 이전 Binding의 늦은 종료가 새 Host의 요청을 해제하지 않는다.
            if (ownsHost && input != null) input.SetPopupUIBlocked(false);
            if (ownsHost && game != null) game.SetPopupPaused(false);
            game = null; input = null; Service = null; shownOutcome = null;
        }
        private void OnDisable() { Release(); }
    }
}
