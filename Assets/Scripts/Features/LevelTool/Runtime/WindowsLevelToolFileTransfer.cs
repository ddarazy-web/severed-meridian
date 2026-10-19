#if (UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN) && (UNITY_EDITOR || PRODUCT_LEVEL_EDITOR)
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using AOT;
using Cysharp.Threading.Tasks;

namespace LevelTool
{
    public sealed class WindowsLevelToolFileTransfer : LevelToolFileTransfer
    {
        [ThreadStatic] private static Dialog currentDialog;
        private static readonly Hook DialogHook = OnDialogMessage;

        public override UniTask<Stream> OpenProject(CancellationToken token) => Select(false, null, token);
        public override UniTask<Stream> CreateProject(string name, CancellationToken token) => Select(true, name, token);

        private UniTask<Stream> Select(bool create, string name, CancellationToken token)
        {
            Dialog dialog = new Dialog();
            return Requests.Start(token, id =>
            {
                Thread thread = new Thread(() =>
                {
                    Stream stream = null;
                    IntPtr file = IntPtr.Zero;
                    try
                    {
                        if (!Requests.IsActive(id)) return;
                        // 구조체 안의 StringBuilder 마샬링을 피하고 Win32의 LPWSTR 버퍼를 직접 소유한다.
                        const int capacity = 32768;
                        char[] initial = new char[capacity];
                        if (create)
                        {
                            string suggested = Path.GetFileName(name ?? "Project.zip");
                            suggested.CopyTo(0, initial, 0, Math.Min(suggested.Length, capacity - 1));
                        }
                        file = Marshal.AllocHGlobal(capacity * sizeof(char));
                        Marshal.Copy(initial, 0, file, capacity);
                        currentDialog = dialog;
                        OpenFileName options = new OpenFileName
                        {
                            size = Marshal.SizeOf<OpenFileName>(), filter = "ZIP 프로젝트\0*.zip\0\0",
                            file = file, maxFile = capacity, title = create ? "ZIP 내보내기" : "ZIP 가져오기",
                            defaultExtension = "zip", hook = DialogHook,
                            flags = 0x00080000 | 0x00000020 | 0x00000008 | 0x00000800 | (create ? 0x00000002 : 0x00001000)
                        };
                        bool selected = create ? GetSaveFileName(ref options) : GetOpenFileName(ref options);
                        uint error = selected ? 0 : CommDlgExtendedError();
                        dialog.Attach(IntPtr.Zero);
                        if (error != 0) throw new IOException("Windows 파일 선택기 오류: " + error);
                        if (selected && Requests.IsActive(id))
                            stream = new FileStream(Marshal.PtrToStringUni(file), create ? FileMode.Create : FileMode.Open,
                                create ? FileAccess.Write : FileAccess.Read, FileShare.None);
                        Stream result = stream;
                        stream = null;
                        // 네이티브 STA 스레드에서 화면 연속 실행을 하지 않는다.
                        UniTask.Post(() => Requests.Complete(id, result));
                    }
                    catch (Exception error)
                    {
                        stream?.Dispose();
                        UniTask.Post(() => Requests.Complete(id, null, error));
                    }
                    finally { if (file != IntPtr.Zero) Marshal.FreeHGlobal(file); currentDialog = null; }
                });
                thread.IsBackground = true;
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
            }, id => dialog.Cancel());
        }

        [MonoPInvokeCallback(typeof(Hook))]
        private static IntPtr OnDialogMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam)
        {
            if (message == 0x0110) currentDialog?.Attach(GetParent(window));
            return IntPtr.Zero;
        }

        private sealed class Dialog
        {
            private readonly object gate = new object();
            private IntPtr window;
            private bool cancelled;
            public void Attach(IntPtr handle)
            {
                lock (gate)
                {
                    window = handle;
                    if (cancelled && handle != IntPtr.Zero) PostMessage(handle, 0x0010, IntPtr.Zero, IntPtr.Zero);
                }
            }
            public void Cancel()
            {
                lock (gate)
                {
                    cancelled = true;
                    if (window != IntPtr.Zero) PostMessage(window, 0x0010, IntPtr.Zero, IntPtr.Zero);
                }
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate IntPtr Hook(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct OpenFileName
        {
            public int size;
            public IntPtr owner, instance;
            public string filter;
            public IntPtr customFilter;
            public int maxCustomFilter, filterIndex;
            public IntPtr file;
            public int maxFile;
            public IntPtr fileTitle;
            public int maxFileTitle;
            public string initialDirectory, title;
            public int flags;
            public short fileOffset, fileExtension;
            public string defaultExtension;
            public IntPtr customData;
            public Hook hook;
            public IntPtr templateName, reserved;
            public int reservedCount, extendedFlags;
        }

        [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetOpenFileNameW")]
        [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetOpenFileName(ref OpenFileName options);
        [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetSaveFileNameW")]
        [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetSaveFileName(ref OpenFileName options);
        [DllImport("comdlg32.dll")] private static extern uint CommDlgExtendedError();
        [DllImport("user32.dll")] private static extern IntPtr GetParent(IntPtr window);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)] private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    }
}
#endif
