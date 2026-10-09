#if (UNITY_EDITOR || PRODUCT_LEVEL_EDITOR) && (UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN)
using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace LevelTool
{
    // 이 어댑터는 실제 Windows 레벨툴에서만 생성한다. 에디터 HWND는 검색하지 않는다.
    internal sealed class LevelToolNativeWindow : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)] private struct Rect
        {
            public int left, top, right, bottom;
            public RectInt Value => new RectInt(left, top, right - left, bottom - top);
            public Rect(RectInt value) { left = value.x; top = value.y; right = value.xMax; bottom = value.yMax; }
        }
        [StructLayout(LayoutKind.Sequential)] private struct Point { public int x, y; }
        [StructLayout(LayoutKind.Sequential)] private struct MinMax { public Point reserved, maximum, position, minimumTrack, maximumTrack; }
        [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int size; public Rect monitor, work; public uint flags; }
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate bool EnumCallback(IntPtr window, IntPtr data);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr SubclassCallback(IntPtr window, uint message, UIntPtr wParam, IntPtr lParam, UIntPtr id, UIntPtr data);
        private static readonly EnumCallback Enumeration = FindWindow;
        private static readonly SubclassCallback Callback = WindowMessage;
        private static readonly UIntPtr SubclassId = new UIntPtr(0x4C54574E);
        private static IntPtr discovered;
        private static LevelToolNativeWindow active;
        private IntPtr handle;
        private bool restoreMaximizeButton;
        public bool Constrain = true;
        public bool ToggleRequested;
        public bool Sizing { get; private set; }
        public bool Alive => handle != IntPtr.Zero && IsWindow(handle);
        public bool Minimized => IsIconic(handle);
        public RectInt Bounds { get { GetWindowRect(handle, out Rect rect); return rect.Value; } }
        public Vector2Int Frame
        {
            get
            {
                GetWindowRect(handle, out Rect outer); GetClientRect(handle, out Rect inner);
                return new Vector2Int(Math.Max(0, outer.Value.width - inner.Value.width), Math.Max(0, outer.Value.height - inner.Value.height));
            }
        }

        public static LevelToolNativeWindow TryOpen()
        {
            if (!LevelToolWindowController.Supported) return null;
            if (active != null) throw new InvalidOperationException("레벨툴 창 제어기가 이미 연결되어 있습니다.");
            discovered = IntPtr.Zero; EnumWindows(Enumeration, IntPtr.Zero);
            if (discovered == IntPtr.Zero) return null;
            if (GetWindowThreadProcessId(discovered, out _) != GetCurrentThreadId())
                throw new InvalidOperationException("Windows 창 스레드가 달라 창 제약을 연결할 수 없습니다.");
            LevelToolNativeWindow value = new LevelToolNativeWindow { handle = discovered };
            active = value;
            if (!SetWindowSubclass(value.handle, Callback, SubclassId, UIntPtr.Zero))
            { active = null; throw new InvalidOperationException("Windows 창 제약 연결에 실패했습니다."); }
            value.restoreMaximizeButton = (GetWindowLong(value.handle, -16) & 0x10000) != 0;
            return value;
        }

        [AOT.MonoPInvokeCallback(typeof(EnumCallback))]
        private static bool FindWindow(IntPtr window, IntPtr data)
        {
            GetWindowThreadProcessId(window, out uint process);
            if (process != GetCurrentProcessId() || !IsWindowVisible(window)) return true;
            StringBuilder name = new StringBuilder(128); GetClassName(window, name, name.Capacity);
            if (name.ToString() != "UnityWndClass") return true;
            discovered = window; return false;
        }

        [AOT.MonoPInvokeCallback(typeof(SubclassCallback))]
        private static IntPtr WindowMessage(IntPtr window, uint message, UIntPtr wParam, IntPtr lParam, UIntPtr id, UIntPtr data)
        {
            LevelToolNativeWindow value = active;
            if (value == null || window != value.handle) return DefSubclassProc(window, message, wParam, lParam);
            // Alt+Enter와 F11은 같은 전환을 요청하며 키를 누르고 있어도 한 번만 처리한다.
            bool shortcut = message == 0x100 && wParam.ToUInt64() == 0x7A ||
                message == 0x104 && wParam.ToUInt64() == 0x0D && (lParam.ToInt64() & (1L << 29)) != 0;
            if (shortcut)
            {
                if ((lParam.ToInt64() & (1L << 30)) == 0) value.ToggleRequested = true;
                return IntPtr.Zero;
            }
            // 제목 표시줄 더블클릭, 시스템 메뉴, Win+위쪽 화살표의 최대화 요청도 차단한다.
            if (message == 0x112 && (wParam.ToUInt64() & 0xFFF0) == 0xF030) return IntPtr.Zero;
            if (message == 0x231) value.Sizing = true;
            if (message == 0x232) value.Sizing = false;
            if (value.Constrain && message == 0x214)
            {
                Rect rect = Marshal.PtrToStructure<Rect>(lParam); Vector2Int frame = value.Frame;
                rect = new Rect(LevelToolWindowGeometry.Resize(rect.Value, (int)wParam.ToUInt64(), frame.x, frame.y, value.Bounds));
                Marshal.StructureToPtr(rect, lParam, false); return new IntPtr(1);
            }
            if (value.Constrain && message == 0x24)
            {
                IntPtr result = DefSubclassProc(window, message, wParam, lParam);
                MinMax size = Marshal.PtrToStructure<MinMax>(lParam); Vector2Int frame = value.Frame;
                size.minimumTrack.x = 1280 + frame.x; size.minimumTrack.y = 800 + frame.y;
                Marshal.StructureToPtr(size, lParam, false); return result;
            }
            if (message == 0x82)
            {
                RemoveWindowSubclass(window, Callback, SubclassId);
                value.handle = IntPtr.Zero; active = null;
            }
            return DefSubclassProc(window, message, wParam, lParam);
        }

        public RectInt WorkArea(RectInt near)
        {
            Rect rect = new Rect(near);
            MonitorInfo info = new MonitorInfo { size = Marshal.SizeOf<MonitorInfo>() };
            if (!GetMonitorInfo(MonitorFromRect(ref rect, 2), ref info)) throw new InvalidOperationException("모니터 작업 영역을 읽을 수 없습니다.");
            return info.work.Value;
        }
        public void Place(RectInt rect)
        {
            if (!SetWindowPos(handle, IntPtr.Zero, rect.x, rect.y, rect.width, rect.height, 0x14))
                throw new InvalidOperationException("창 크기와 위치 적용에 실패했습니다.");
        }
        public void Restore() => ShowWindow(handle, 9);
        public void RestoreIfMaximized() { if (IsZoomed(handle)) Restore(); }
        public void DisableMaximize()
        {
            int style = GetWindowLong(handle, -16);
            if ((style & 0x10000) == 0) return;
            if (SetWindowLong(handle, -16, style & ~0x10000) == 0 || !SetWindowPos(handle, IntPtr.Zero, 0, 0, 0, 0, 0x37))
                throw new InvalidOperationException("최대화 버튼을 비활성화할 수 없습니다.");
        }
        public void Dispose()
        {
            if (Alive)
            {
                RemoveWindowSubclass(handle, Callback, SubclassId);
                if (restoreMaximizeButton)
                {
                    SetWindowLong(handle, -16, GetWindowLong(handle, -16) | 0x10000);
                    SetWindowPos(handle, IntPtr.Zero, 0, 0, 0, 0, 0x37);
                }
            }
            if (active == this) active = null;
            handle = IntPtr.Zero;
        }

        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumCallback callback, IntPtr data);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentProcessId();
        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder name, int size);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr window);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
        [DllImport("user32.dll")] private static extern bool IsZoomed(IntPtr window);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong(IntPtr window, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] private static extern int SetWindowLong(IntPtr window, int index, int value);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out Rect rect);
        [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr window, out Rect rect);
        [DllImport("user32.dll")] private static extern IntPtr MonitorFromRect(ref Rect rect, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window, int command);
        [DllImport("comctl32.dll")] private static extern bool SetWindowSubclass(IntPtr window, SubclassCallback callback, UIntPtr id, UIntPtr data);
        [DllImport("comctl32.dll")] private static extern bool RemoveWindowSubclass(IntPtr window, SubclassCallback callback, UIntPtr id);
        [DllImport("comctl32.dll")] private static extern IntPtr DefSubclassProc(IntPtr window, uint message, UIntPtr wParam, IntPtr lParam);
    }
}
#endif
