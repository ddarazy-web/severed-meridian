using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using LevelTool;
using UnityEditor;
using UnityEngine;

namespace LevelAuthoring.Editor
{
    public static class LevelToolFileRequestVerification
    {
        public static void Run() => Check().Forget();

        public static async UniTask Check()
        {
            try
            {
                using (LevelToolFileRequest request = new LevelToolFileRequest())
                using (CancellationTokenSource cancellation = new CancellationTokenSource())
                {
                    long first = 0;
                    bool nativeCancelled = false;
                    UniTask<Stream> pending = request.Start(cancellation.Token, id => first = id,
                        id => nativeCancelled = id == first);
                    bool duplicateRejected = false;
                    try { await request.Start(CancellationToken.None, id => { }, id => { }); }
                    catch (InvalidOperationException) { duplicateRejected = true; }
                    if (!duplicateRejected || !request.IsActive(first)) throw new Exception("Active request ownership failed.");
                    // 선택기가 pause 중 결과를 돌려주지 않아도 요청은 그대로 살아 있어야 한다.
                    await UniTask.Yield();
                    if (!request.IsActive(first)) throw new Exception("Pause-like delay lost the request.");
                    cancellation.Cancel();
                    if (await pending != null || !nativeCancelled) throw new Exception("Cancellation contract failed.");
                    MemoryStream stale = new MemoryStream();
                    request.Complete(first, stale);
                    if (stale.CanRead) throw new Exception("Stale stream leaked.");
                    long second = 0;
                    UniTask<Stream> next = request.Start(CancellationToken.None, id => second = id, id => { });
                    if (!request.TryClaimResult(second) || request.TryClaimResult(second))
                        throw new Exception("Duplicate provider callbacks could reopen the destination.");
                    MemoryStream result = new MemoryStream();
                    request.Complete(second, result);
                    if (await next != result || !result.CanRead) throw new Exception("Caller stream ownership failed.");
                    MemoryStream duplicate = new MemoryStream();
                    request.Complete(second, duplicate);
                    if (duplicate.CanRead) throw new Exception("Duplicate stream leaked.");
                    result.Dispose();
                    long third = 0;
                    UniTask<Stream> failure = request.Start(CancellationToken.None, id => third = id, id => { });
                    IOException expected = new IOException("provider denied");
                    request.Complete(third, null, expected);
                    bool failed = false;
                    try { await failure; } catch (IOException error) { failed = ReferenceEquals(error, expected); }
                    if (!failed) throw new Exception("Provider error was treated as cancellation.");
                    bool startedAfterCancel = false;
                    using (CancellationTokenSource alreadyCancelled = new CancellationTokenSource())
                    {
                        alreadyCancelled.Cancel();
                        if (await request.Start(alreadyCancelled.Token, id => startedAfterCancel = true, id => { }) != null || startedAfterCancel)
                            throw new Exception("A pre-cancelled request opened the picker.");
                    }
                    UniTask<Stream> disposed = request.Start(CancellationToken.None, id => { }, id => { });
                    request.Dispose();
                    if (await disposed != null) throw new Exception("Dispose did not cancel the pending request.");
                }
                Debug.Log("PASS LevelToolFileRequestVerification: cancel, stale, duplicate, ownership, provider error, pause, dispose");
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }
    }
}
