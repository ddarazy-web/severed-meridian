using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Levels;

namespace AutoPlay
{
    // 기존 반복 실행기와 저장소 사이에서 한 체크포인트의 내구성만 보장한다.
    // 파일 작업 중 다음 판을 실행하지 않으며, 작업 스레드는 Unity 객체에 접근하지 않는다.
    public sealed class PersistedBotBatch : IDisposable
    {
        private readonly BotBatchStore store;
        private readonly BotBatchSession session;
        private readonly List<BotGameSummary> games = new List<BotGameSummary>();
        private Task writing;
        private BotBatchGame pendingGame;
        private bool stopRequested, interrupted, disposed;
        public BotBatchRecord Record => session.Record;
        public BotPlaySession Current => session.Current;
        public IReadOnlyList<BotGameSummary> Games => games;
        public bool IsSaving => writing != null;
        public bool HasWork => !disposed && (IsSaving || stopRequested || session.NeedsAdvance);
        public bool CanControl => !disposed && !IsSaving && PersistenceError == null && session.CanContinue;
        public string PersistenceError { get; private set; }
        public BotBatchGame UnsavedGame { get; private set; }

        public PersistedBotBatch(LevelDefinition definition, int[] seeds, BotBatchStore store)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            session = new BotBatchSession(definition, seeds, Checkpoint);
            PersistenceError = session.SaveError;
        }

        private void Checkpoint(BotBatchRecord record, BotBatchGame game)
        {
            if (writing != null) throw new InvalidOperationException("이전 기록 저장이 아직 끝나지 않았습니다.");
            Action write = store.PrepareSave(record, game);
            pendingGame = game;
            writing = Task.Run(write);
        }

        public void Advance()
        {
            if (disposed) return;
            if (writing != null)
            {
                if (!writing.IsCompleted) return;
                try
                {
                    writing.GetAwaiter().GetResult();
                    if (pendingGame != null) games.Add(new BotGameSummary(pendingGame));
                }
                catch (Exception error)
                {
                    PersistenceError = error.Message; UnsavedGame = pendingGame;
                    Record.status = BotBatchStatus.Error; Record.message = "기록 저장 실패 · " + error.Message;
                    Record.endedUtc = DateTime.UtcNow.ToString("O"); stopRequested = false;
                    session.Dispose();
                }
                finally { writing = null; pendingGame = null; }
                return;
            }
            if (PersistenceError != null) return;
            if (stopRequested)
            {
                stopRequested = false;
                session.Stop(interrupted, interrupted ? "시험 화면 종료" : "사용자 중지");
            }
            else session.Advance();
            if (session.SaveError != null) { PersistenceError = session.SaveError; UnsavedGame = session.UnsavedGame; }
        }

        public bool Pause() => CanControl && session.Pause();
        public bool Resume() => CanControl && session.Resume();
        public void RequestStop(bool ownerClosing = false)
        {
            if (disposed || PersistenceError != null) return;
            stopRequested = true; interrupted |= ownerClosing;
        }

        public async UniTask StopAndDisposeAsync()
        {
            RequestStop(true);
            while (HasWork)
            {
                if (writing != null)
                {
                    try { await writing; } catch { /* Advance가 실패 기록과 상태를 확정한다. */ }
                    await UniTask.SwitchToMainThread();
                }
                Advance();
            }
            Dispose();
        }

        // 앱/Play 종료는 다음 프레임을 보장하지 않으므로 마지막 체크포인트만 동기 확정한다.
        public void StopAndDisposeAtShutdown()
        {
            RequestStop(true);
            while (HasWork)
            {
                if (writing != null) { try { writing.GetAwaiter().GetResult(); } catch { /* Advance에서 기록한다. */ } }
                Advance();
            }
            Dispose();
        }

        public void Dispose()
        {
            if (disposed) return;
            if (IsSaving || session.CanContinue) throw new InvalidOperationException("StopAndDisposeAsync로 저장을 마친 뒤 종료하세요.");
            disposed = true; session.Dispose();
        }
    }
}
