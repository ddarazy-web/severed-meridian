#if UNITY_ANDROID && !UNITY_EDITOR && PRODUCT_LEVEL_EDITOR
using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Scripting;

namespace LevelTool
{
    public sealed class AndroidLevelToolFileTransfer : LevelToolFileTransfer
    {
        private const string Bridge = "com.serveredmeridian.leveltool.LevelToolDocumentPicker";
        private readonly ResultCallback callback;

        public AndroidLevelToolFileTransfer() { callback = new ResultCallback(this); }
        public override UniTask<Stream> OpenProject(CancellationToken token) => Select(false, null, token);
        public override UniTask<Stream> CreateProject(string name, CancellationToken token) => Select(true, name, token);

        private UniTask<Stream> Select(bool create, string name, CancellationToken token) =>
            Requests.Start(token, id => UniTask.Post(() =>
            {
                if (!Requests.IsActive(id)) return;
                try
                {
                    using (AndroidJavaClass player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    using (AndroidJavaClass picker = new AndroidJavaClass(Bridge))
                        picker.CallStatic("begin", activity, id, create, name ?? "Project.zip", callback);
                }
                catch (Exception error) { Requests.Complete(id, null, error); }
            }), id => UniTask.Post(() =>
            {
                try
                {
                    using (AndroidJavaClass picker = new AndroidJavaClass(Bridge)) picker.CallStatic("cancel", id);
                }
                catch (Exception error) { Debug.LogException(error); }
            }));

        private async UniTask Receive(long id, string uri, string error, bool write)
        {
            if (!Requests.TryClaimResult(id)) return;
            if (!string.IsNullOrEmpty(error)) { Requests.Complete(id, null, new IOException(error)); return; }
            if (string.IsNullOrEmpty(uri)) { Requests.Complete(id, null); return; }
            Stream stream = null;
            try
            {
                // 문서 제공자가 스트림을 여는 동안 Unity 메인 스레드를 막지 않는다.
                stream = await UniTask.RunOnThreadPool(() =>
                {
                    using (JniThreadScope scope = new JniThreadScope(true))
                    using (AndroidJavaClass player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    using (AndroidJavaClass picker = new AndroidJavaClass(Bridge))
                        return (Stream)new DocumentStream(picker.CallStatic<AndroidJavaObject>("open", activity, uri, write), write);
                });
                Requests.Complete(id, stream);
                stream = null;
            }
            catch (Exception failure)
            {
                stream?.Dispose();
                Requests.Complete(id, null, new IOException("Android 문서 스트림을 열지 못했습니다.", failure));
            }
        }

        [Preserve]
        private sealed class ResultCallback : AndroidJavaProxy
        {
            private readonly AndroidLevelToolFileTransfer owner;
            public ResultCallback(AndroidLevelToolFileTransfer owner) : base(Bridge + "$Callback") { this.owner = owner; }
            [Preserve]
            public void onResult(long id, string uri, string error, bool write) =>
                UniTask.Post(() => owner.Receive(id, uri, error, write).Forget());
        }

        private readonly struct JniThreadScope : IDisposable
        {
            private readonly bool attached;
            public JniThreadScope(bool attachWorkerThread)
            {
                attached = attachWorkerThread && !PlayerLoopHelper.IsMainThread;
                if (attached) AndroidJNI.AttachCurrentThread();
            }
            public void Dispose() { if (attached) AndroidJNI.DetachCurrentThread(); }
        }

        private sealed class DocumentStream : Stream
        {
            private AndroidJavaObject document;
            private readonly bool write;
            private long position;
            public DocumentStream(AndroidJavaObject document, bool write)
            {
                this.document = document ?? throw new IOException("문서 제공자가 스트림을 반환하지 않았습니다.");
                this.write = write;
            }
            public override bool CanRead => document != null && !write;
            public override bool CanWrite => document != null && write;
            public override bool CanSeek => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => position; set => throw new NotSupportedException(); }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (document == null) throw new ObjectDisposedException(nameof(DocumentStream));
                if (!CanRead) throw new NotSupportedException();
                if (buffer == null) throw new ArgumentNullException(nameof(buffer));
                if (offset < 0 || count < 0 || offset > buffer.Length - count) throw new ArgumentOutOfRangeException();
                if (count == 0) return 0;
                using (JniThreadScope scope = new JniThreadScope(true))
                {
                    sbyte[] bytes = document.Call<sbyte[]>("read", Math.Min(count, 65536));
                    if (bytes == null) return 0;
                    Buffer.BlockCopy(bytes, 0, buffer, offset, bytes.Length);
                    position += bytes.Length;
                    return bytes.Length;
                }
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                if (document == null) throw new ObjectDisposedException(nameof(DocumentStream));
                if (!CanWrite) throw new NotSupportedException();
                if (buffer == null) throw new ArgumentNullException(nameof(buffer));
                if (offset < 0 || count < 0 || offset > buffer.Length - count) throw new ArgumentOutOfRangeException();
                using (JniThreadScope scope = new JniThreadScope(true))
                {
                    while (count > 0)
                    {
                        int length = Math.Min(count, 65536);
                        sbyte[] bytes = new sbyte[length];
                        Buffer.BlockCopy(buffer, offset, bytes, 0, length);
                        document.Call("write", bytes);
                        position += length;
                        offset += length;
                        count -= length;
                    }
                }
            }

            public override void Flush()
            {
                if (document == null) throw new ObjectDisposedException(nameof(DocumentStream));
                using (JniThreadScope scope = new JniThreadScope(true)) document.Call("flush");
            }

            protected override void Dispose(bool disposing)
            {
                AndroidJavaObject current = document;
                document = null;
                if (disposing && current != null)
                {
                    using (JniThreadScope scope = new JniThreadScope(true))
                    {
                        try { current.Call("close"); }
                        finally { current.Dispose(); }
                    }
                }
                base.Dispose(disposing);
            }
        }
    }
}
#endif
