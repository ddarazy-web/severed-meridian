#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace LevelTool
{
    public interface ILevelToolFileTransfer
    {
        UniTask<Stream> OpenProject(CancellationToken cancellationToken);
        UniTask<Stream> CreateProject(string suggestedName, CancellationToken cancellationToken);
    }

    // 화면 종료와 네이티브 결과의 순서가 달라도 요청별로 한 번만 스트림을 전달한다.
    public sealed class LevelToolFileRequest : IDisposable
    {
        private readonly object gate = new object();
        private static long sequence;
        private long active;
        private bool disposed, resultClaimed;
        private UniTaskCompletionSource<Stream> completion;
        private Action<long> cancelNative;

        public async UniTask<Stream> Start(CancellationToken token, Action<long> start, Action<long> cancel)
        {
            long id;
            UniTaskCompletionSource<Stream> pending;
            lock (gate)
            {
                if (disposed) throw new ObjectDisposedException(nameof(LevelToolFileRequest));
                if (active != 0) throw new InvalidOperationException("파일 선택 요청이 이미 진행 중입니다.");
                id = active = Interlocked.Increment(ref sequence);
                pending = completion = new UniTaskCompletionSource<Stream>();
                cancelNative = cancel;
                resultClaimed = false;
            }
            using (token.Register(() => Cancel(id)))
            {
                try { if (IsActive(id)) start(id); }
                catch (Exception error) { Complete(id, null, error); }
                return await pending.Task;
            }
        }

        public bool IsActive(long id) { lock (gate) return active == id && !disposed; }

        // URI 스트림을 열기 전 중복 콜백을 차단해 출력 문서가 다시 잘리지 않게 한다.
        public bool TryClaimResult(long id)
        {
            lock (gate)
            {
                if (active != id || id == 0 || disposed || resultClaimed) return false;
                resultClaimed = true;
                return true;
            }
        }

        public void Complete(long id, Stream stream, Exception error = null)
        {
            UniTaskCompletionSource<Stream> pending = null;
            lock (gate)
            {
                if (active == id && id != 0 && !disposed)
                {
                    active = 0;
                    pending = completion;
                    completion = null;
                    cancelNative = null;
                }
            }
            if (pending == null || error != null)
            {
                // 오래된 제공자의 close 오류가 새 요청의 상태를 바꾸면 안 된다.
                try { stream?.Dispose(); } catch { }
            }
            if (pending == null) return;
            if (error != null) pending.TrySetException(error);
            else pending.TrySetResult(stream);
        }

        public void CancelPending()
        {
            long id;
            lock (gate) id = active;
            Cancel(id);
        }

        private void Cancel(long id)
        {
            UniTaskCompletionSource<Stream> pending;
            Action<long> cancel;
            lock (gate)
            {
                if (id == 0 || active != id) return;
                active = 0;
                pending = completion;
                cancel = cancelNative;
                completion = null;
                cancelNative = null;
            }
            try { cancel?.Invoke(id); } catch { }
            pending.TrySetResult(null);
        }

        public void Dispose()
        {
            lock (gate) disposed = true;
            CancelPending();
        }
    }

    public abstract class LevelToolFileTransfer : ILevelToolFileTransfer, IDisposable
    {
        protected readonly LevelToolFileRequest Requests = new LevelToolFileRequest();
        public abstract UniTask<Stream> OpenProject(CancellationToken cancellationToken);
        public abstract UniTask<Stream> CreateProject(string suggestedName, CancellationToken cancellationToken);
        public void CancelPending() => Requests.CancelPending();
        public virtual void Dispose() => Requests.Dispose();

        public static LevelToolFileTransfer CreateDefault()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return new AndroidLevelToolFileTransfer();
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            return new WindowsLevelToolFileTransfer();
#else
            return new UnsupportedLevelToolFileTransfer();
#endif
        }

        private sealed class UnsupportedLevelToolFileTransfer : LevelToolFileTransfer
        {
            public override UniTask<Stream> OpenProject(CancellationToken token) =>
                UniTask.FromException<Stream>(new PlatformNotSupportedException("이 플랫폼에는 시스템 ZIP 선택기가 없습니다."));
            public override UniTask<Stream> CreateProject(string name, CancellationToken token) => OpenProject(token);
        }
    }
}
#endif
