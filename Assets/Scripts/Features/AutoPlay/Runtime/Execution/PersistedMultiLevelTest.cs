using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Levels;

namespace AutoPlay
{
    // 한 실행 단위가 내보낸 판/추천/목록 체크포인트를 순서대로 저장한 뒤 다음 단위를 허용한다.
    public sealed class PersistedMultiLevelTest : IDisposable
    {
        private readonly MultiLevelTestSession session;
        private Task writing;
        private int writingIndex;
        private bool stopRequested, interrupted, disposed;
        public MultiLevelTestRecord Record => session.Record;
        public BotPlaySession Current => session.Current;
        public int Index => session.Index;
        public bool Paused => session.Paused;
        public bool IsSaving => writing != null;
        public string PersistenceError { get; private set; }
        public bool CanControl => !disposed && !IsSaving && PersistenceError == null && session.CanContinue;
        public bool HasWork => !disposed && (IsSaving || stopRequested || PersistenceError == null && session.CanContinue && !session.Paused);

        public PersistedMultiLevelTest(IEnumerable<LevelDefinition> levels, MultiLevelTestMode mode, int samples, string root,
            Func<LevelDefinition, BotTrialSourceContext> trialSource = null)
        {
            var store = new MultiLevelTestStore(root, QueueWrite);
            session = new MultiLevelTestSession(levels, mode, samples, store, trialSource: trialSource);
        }

        private void QueueWrite(Action write)
        {
            if (PersistenceError != null) throw new InvalidOperationException(PersistenceError);
            Task previous = writing;
            // 직렬화는 저장소가 호출 스레드에서 끝낸다. 작업 스레드는 문자열과 경로만 사용한다.
            writing = Task.Run(async () => { if (previous != null) await previous.ConfigureAwait(false); write(); });
        }

        public void Advance()
        {
            if (disposed) return;
            if (writing != null)
            {
                if (!writing.IsCompleted) return;
                try { writing.GetAwaiter().GetResult(); }
                catch (Exception error)
                {
                    PersistenceError = "시험 기록 저장 실패: " + error.Message; stopRequested = false;
                    session.Stop(true); session.Dispose();
                    if (writingIndex < Record.entries.Count)
                    { Record.entries[writingIndex].status = MultiLevelTestStatus.Error; Record.entries[writingIndex].message = PersistenceError; }
                }
                finally { writing = null; }
                return;
            }
            if (PersistenceError != null) return;
            writingIndex = session.Index;
            if (stopRequested) { stopRequested = false; session.Stop(interrupted); }
            else session.Advance();
            if (session.Error != null) PersistenceError = session.Error;
        }

        public bool Pause()
        { if (!CanControl) return false; writingIndex = session.Index; session.SetPaused(true); return true; }
        public bool Resume()
        { if (!CanControl) return false; writingIndex = session.Index; session.SetPaused(false); return true; }
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
                    try { await writing; } catch { /* Advance가 실패 상태를 기록한다. */ }
                    await UniTask.SwitchToMainThread();
                }
                Advance();
            }
            Dispose();
        }

        // 다음 프레임이 없는 앱 종료에서만 마지막 파일 작업을 기다린다.
        public void StopAndDisposeAtShutdown()
        {
            RequestStop(true);
            while (HasWork)
            {
                if (writing != null) { try { writing.GetAwaiter().GetResult(); } catch { /* Advance에서 처리한다. */ } }
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
