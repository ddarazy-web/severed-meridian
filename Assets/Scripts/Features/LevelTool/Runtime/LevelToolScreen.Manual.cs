#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.IO;
using UnityEngine;

namespace LevelTool
{
    public sealed partial class LevelToolScreen
    {
        private void OpenManual()
        {
            try
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                    {
                        try
                        {
                            using (var currentPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                            using (var currentActivity = currentPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                            {
                                var web = new AndroidJavaObject("android.webkit.WebView", currentActivity);
                                var dialog = new AndroidJavaObject("android.app.Dialog", currentActivity);
                                using (var settings = web.Call<AndroidJavaObject>("getSettings"))
                                {
                                    // 동봉한 정적 문서만 읽으며 스크립트·외부 파일 접근은 허용하지 않는다.
                                    settings.Call("setJavaScriptEnabled", false);
                                    settings.Call("setAllowFileAccess", false);
                                    settings.Call("setAllowContentAccess", false);
                                    settings.Call("setBlockNetworkLoads", true);
                                    settings.Call("setUseWideViewPort", true);
                                    settings.Call("setBuiltInZoomControls", true);
                                    settings.Call("setDisplayZoomControls", false);
                                }
                                using (var client = new AndroidJavaObject("android.webkit.WebViewClient"))
                                    web.Call("setWebViewClient", client);
                                using (var layout = new AndroidJavaObject("android.widget.LinearLayout", currentActivity))
                                using (var close = new AndroidJavaObject("android.widget.Button", currentActivity))
                                using (var size = new AndroidJavaObject("android.widget.LinearLayout$LayoutParams", -1, 0, 1f))
                                {
                                    layout.Call("setOrientation", 1);
                                    close.Call("setText", "닫기 · 레벨툴로 돌아가기");
                                    close.Call("setOnClickListener", new ManualCloseListener(dialog));
                                    layout.Call("addView", close);
                                    layout.Call("addView", web, size);
                                    dialog.Call("setTitle", "레벨툴 사용 설명서");
                                    dialog.Call("setContentView", layout);
                                }
                                dialog.Call("setOnDismissListener", new ManualDismissListener(dialog, web));
                                web.Call("loadUrl", "file:///android_asset/LevelToolManual/index.html");
                                dialog.Call("show");
                                using (var window = dialog.Call<AndroidJavaObject>("getWindow")) window.Call("setLayout", -1, -1);
                            }
                        }
                        catch (Exception error)
                        {
                            Cysharp.Threading.Tasks.UniTask.Post(() => Show("사용 설명서를 열 수 없습니다: " + error.Message));
                        }
                    }));
#else
#if UNITY_EDITOR
                string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Docs/MoonRabbitJunkyard/Manual/index.html"));
#else
                string path = Path.Combine(Application.streamingAssetsPath, "LevelToolManual/index.html");
#endif
                if (!File.Exists(path)) { Show("사용 설명서 파일이 없습니다. 레벨툴 배포본의 LevelToolManual 폴더를 확인하세요."); return; }
                Application.OpenURL(new Uri(path).AbsoluteUri);
#endif
            }
            catch (Exception error) { Show("사용 설명서를 열 수 없습니다: " + error.Message); }
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        [UnityEngine.Scripting.Preserve]
        private sealed class ManualCloseListener : AndroidJavaProxy
        {
            private readonly AndroidJavaObject dialog;
            internal ManualCloseListener(AndroidJavaObject dialog) : base("android.view.View$OnClickListener") { this.dialog = dialog; }
            [UnityEngine.Scripting.Preserve]
            public void onClick(AndroidJavaObject view) => dialog.Call("dismiss");
        }

        [UnityEngine.Scripting.Preserve]
        private sealed class ManualDismissListener : AndroidJavaProxy
        {
            private readonly AndroidJavaObject dialog, web;
            internal ManualDismissListener(AndroidJavaObject dialog, AndroidJavaObject web) : base("android.content.DialogInterface$OnDismissListener")
            { this.dialog = dialog; this.web = web; }
            [UnityEngine.Scripting.Preserve]
            public void onDismiss(AndroidJavaObject source)
            {
                using (var parent = web.Call<AndroidJavaObject>("getParent")) parent?.Call("removeView", web);
                web.Call("destroy"); web.Dispose(); dialog.Dispose();
            }
        }
#endif
    }
}
#endif
