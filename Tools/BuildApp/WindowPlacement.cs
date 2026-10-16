using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace MoonRabbit.BuildApp;

public sealed class PixelBounds
{
    public int X{get;set;} public int Y{get;set;} public int Width{get;set;} public int Height{get;set;}
    [System.Text.Json.Serialization.JsonIgnore]
    public Rect Rectangle=>new(X,Y,Math.Max(1,Width),Math.Max(1,Height));
}

public sealed class WindowPlacement
{
    private readonly Window window;
    private bool ready;
    private bool fitting;
    private IntPtr currentMonitor;
    private HwndSource? source;
    public PixelBounds? NormalBounds{get;private set;}
    public WindowPlacement(Window window,WorkspacePreferences preferences)
    {
        this.window=window;
        window.Loaded+=(_,_)=>
        {
            source=HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);source?.AddHook(OnMessage);
            if(preferences.PhysicalBounds is {Width:>0,Height:>0} saved)
            {
                window.WindowState=WindowState.Normal;
                MoveTo(saved.Rectangle);
            }
            ready=true;Capture();QueueFit();
            if(preferences.Maximized)window.WindowState=WindowState.Maximized;
        };
        window.LocationChanged+=(_,_)=>{Capture();if(ready&&MonitorFor(ReadBounds(window).Rectangle)!=currentMonitor)QueueFit();};
        window.SizeChanged+=(_,_)=>Capture();
        window.StateChanged+=(_,_)=>{if(window.WindowState==WindowState.Normal)QueueFit();};
        window.Closed+=(_,_)=>{ready=false;source?.RemoveHook(OnMessage);};
    }
    private IntPtr OnMessage(IntPtr hwnd,int message,IntPtr wParam,IntPtr lParam,ref bool handled)
    {
        if(message is 0x02E0 or 0x007E or 0x001A)QueueFit();
        return IntPtr.Zero;
    }
    private void QueueFit()
    {
        if(!ready||fitting)return;fitting=true;
        window.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded,new Action(()=>
        {
            try{if(ready&&window.WindowState==WindowState.Normal)MoveTo(ReadBounds(window).Rectangle);}
            finally{fitting=false;}
        }));
    }
    private static IntPtr MonitorFor(Rect bounds)
    {
        var rect=new NativeRect{Left=(int)bounds.Left,Top=(int)bounds.Top,Right=(int)bounds.Right,Bottom=(int)bounds.Bottom};
        return MonitorFromRect(ref rect,2);
    }
    private void Capture()
    {
        if(!ready||window.WindowState!=WindowState.Normal)return;
        NormalBounds=ReadBounds(window);
    }
    public static PixelBounds ReadBounds(Window window)
    {
        if(!GetWindowRect(new WindowInteropHelper(window).Handle,out var rect))throw new InvalidOperationException("창 위치를 읽을 수 없습니다.");
        return new(){X=rect.Left,Y=rect.Top,Width=rect.Right-rect.Left,Height=rect.Bottom-rect.Top};
    }
    public void MoveTo(Rect requested)
    {
        IntPtr monitor=MonitorFor(requested);currentMonitor=monitor;
        var info=new MonitorInfo{Size=Marshal.SizeOf<MonitorInfo>()};
        if(!GetMonitorInfo(monitor,ref info))throw new InvalidOperationException("모니터 작업 영역을 읽을 수 없습니다.");
        var work=new Rect(info.Work.Left,info.Work.Top,info.Work.Right-info.Work.Left,info.Work.Bottom-info.Work.Top);
        GetDpiForMonitor(monitor,0,out uint dpi,out _);double scale=Math.Max(96,dpi)/96d;
        window.MinWidth=Math.Min(1000,work.Width/scale);window.MinHeight=Math.Min(700,work.Height/scale);
        Rect clamped=Clamp(new Rect(requested.X,requested.Y,Math.Max(requested.Width,window.MinWidth*scale),Math.Max(requested.Height,window.MinHeight*scale)),work);
        if(!SetWindowPos(new WindowInteropHelper(window).Handle,IntPtr.Zero,(int)clamped.X,(int)clamped.Y,(int)clamped.Width,(int)clamped.Height,0x14))throw new InvalidOperationException("창 위치를 복원하지 못했습니다.");
        // 모니터 이동 중 WM_DPICHANGED가 크기를 재계산하므로 새 DPI 적용 후 물리 크기를 확정한다.
        if(!SetWindowPos(new WindowInteropHelper(window).Handle,IntPtr.Zero,(int)clamped.X,(int)clamped.Y,(int)clamped.Width,(int)clamped.Height,0x14))throw new InvalidOperationException("창 크기를 복원하지 못했습니다.");
    }
    public static Rect Clamp(Rect requested,Rect work)
    {
        double width=Math.Min(requested.Width,work.Width),height=Math.Min(requested.Height,work.Height);
        return new(Math.Clamp(requested.X,work.Left,work.Right-width),Math.Clamp(requested.Y,work.Top,work.Bottom-height),width,height);
    }
    public static IReadOnlyList<Rect> WorkAreas()
    {
        var result=new List<Rect>();
        EnumDisplayMonitors(IntPtr.Zero,IntPtr.Zero,(IntPtr monitor,IntPtr dc,ref NativeRect rect,IntPtr data)=>
        {
            var info=new MonitorInfo{Size=Marshal.SizeOf<MonitorInfo>()};
            if(GetMonitorInfo(monitor,ref info))result.Add(new(info.Work.Left,info.Work.Top,info.Work.Right-info.Work.Left,info.Work.Bottom-info.Work.Top));return true;
        },IntPtr.Zero);return result;
    }
    [StructLayout(LayoutKind.Sequential)]struct NativeRect{public int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential)]struct MonitorInfo{public int Size;public NativeRect Monitor,Work;public uint Flags;}
    delegate bool MonitorCallback(IntPtr monitor,IntPtr dc,ref NativeRect rect,IntPtr data);
    [DllImport("user32.dll")]static extern bool GetWindowRect(IntPtr window,out NativeRect rect);
    [DllImport("user32.dll")]static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
    [DllImport("user32.dll")]static extern IntPtr MonitorFromRect(ref NativeRect rect,uint flags);
    [DllImport("user32.dll",CharSet=CharSet.Auto)]static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfo info);
    [DllImport("user32.dll")]static extern bool EnumDisplayMonitors(IntPtr dc,IntPtr clip,MonitorCallback callback,IntPtr data);
    [DllImport("shcore.dll")]static extern int GetDpiForMonitor(IntPtr monitor,int type,out uint x,out uint y);
}
