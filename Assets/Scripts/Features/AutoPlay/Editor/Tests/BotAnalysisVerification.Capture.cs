using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace Levels.Editor
{
    public static partial class BotAnalysisVerification
    {
#if UNITY_EDITOR_WIN
        [StructLayout(LayoutKind.Sequential)] private struct CaptureRect { public int left, top, right, bottom; }
        [StructLayout(LayoutKind.Sequential)] private struct CaptureInfo
        {
            public uint size; public int width, height; public ushort planes, bits;
            public uint compression, imageSize; public int xResolution, yResolution;
            public uint colors, important, color;
        }
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr handle, StringBuilder text, int count);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr handle, out CaptureRect rect);
        [DllImport("user32.dll")] private static extern bool PrintWindow(IntPtr handle, IntPtr dc, uint flags);
        [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr handle);
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr handle, IntPtr dc);
        [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleBitmap(IntPtr dc, int width, int height);
        [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr dc, IntPtr value);
        [DllImport("gdi32.dll")] private static extern int GetDIBits(IntPtr dc, IntPtr bitmap, uint start, uint rows, byte[] pixels, ref CaptureInfo info, uint mode);
        [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr value);
#endif

        /// <param name="name">검증 로그 폴더에 저장할 이미지 이름.</param>
        private static void CaptureAnalysis(string name)
        {
#if UNITY_EDITOR_WIN
            // ReadScreenPixel은 이 PC의 백그라운드 Editor에서 바탕화면을 반환했다.
            // 같은 프로세스의 Match 창만 직접 그리게 하며 다른 앱 화면은 읽지 않는다.
            IntPtr target = IntPtr.Zero; uint owner = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
            EnumWindows((handle, value) => {
                GetWindowThreadProcessId(handle, out uint process);
                if (process != owner) return true;
                StringBuilder title = new StringBuilder(256); GetWindowText(handle, title, title.Capacity);
                if (title.ToString().StartsWith("Match", StringComparison.Ordinal)) target = handle;
                return true;
            }, IntPtr.Zero);
            if (target == IntPtr.Zero || !GetWindowRect(target, out CaptureRect rect)) throw new InvalidOperationException("검증 Match 창을 찾지 못했습니다.");
            int width = rect.right - rect.left, height = rect.bottom - rect.top;
            IntPtr dc = GetDC(target), memory = CreateCompatibleDC(dc), bitmap = CreateCompatibleBitmap(dc, width, height);
            IntPtr previous = SelectObject(memory, bitmap); Texture2D image = null;
            try
            {
                if (!PrintWindow(target, memory, 2)) throw new InvalidOperationException("검증 창 캡처 실패");
                SelectObject(memory, previous);
                CaptureInfo info = new CaptureInfo { size = 40, width = width, height = height, planes = 1, bits = 32 };
                byte[] pixels = new byte[width * height * 4];
                if (GetDIBits(dc, bitmap, 0, (uint)height, pixels, ref info, 0) != height) throw new InvalidOperationException("검증 창 이미지 읽기 실패");
                // Windows DIB의 BGRA를 Unity RGBA로 바꾼다. 양쪽 모두 아래 행부터 저장한다.
                for (int i = 0; i < pixels.Length; i += 4) { (pixels[i], pixels[i + 2]) = (pixels[i + 2], pixels[i]); pixels[i + 3] = 255; }
                image = new Texture2D(width, height, TextureFormat.RGBA32, false);
                image.LoadRawTextureData(pixels); image.Apply();
                File.WriteAllBytes(Evidence + "/" + name, image.EncodeToPNG());
            }
            finally
            {
                SelectObject(memory, previous); DeleteObject(bitmap); DeleteDC(memory); ReleaseDC(target, dc);
                if (image != null) UnityEngine.Object.DestroyImmediate(image);
            }
#else
            Rect rect = window.position;
            Texture2D image = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGB24, false);
            try
            {
                image.SetPixels(UnityEditorInternal.InternalEditorUtility.ReadScreenPixel(rect.position, (int)rect.width, (int)rect.height)); image.Apply();
                File.WriteAllBytes(Evidence + "/" + name, image.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(image); }
#endif
        }
    }
}
