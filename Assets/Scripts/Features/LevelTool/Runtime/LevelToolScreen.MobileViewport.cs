#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelTool
{
    public sealed partial class LevelToolScreen
    {
        private bool mobileViewportActive;
        private PanelSettings originalMobilePanel, runtimeMobilePanel;
        private Rect mobileVisibleArea;
        private Vector2 mobileScreenPixels;
        private Rect mobileSafePixels, mobileKeyboardPixels;
        private VisualElement mobileDialogFrame;
        private ScrollView mobileDialogScroll;
        private float nextAndroidViewportPoll;
        private readonly object androidViewportLock = new object();
        private int androidKeyboardInset, androidWindowHeight, androidViewportGeneration;
        private bool androidViewportPending;

        private void InitializeMobileViewport()
        {
            mobileViewportActive = UsesInternalProjects;
            if (!mobileViewportActive) return;
            UIDocument document = GetComponent<UIDocument>();
            originalMobilePanel = document.panelSettings;
            runtimeMobilePanel = Instantiate(originalMobilePanel);
            runtimeMobilePanel.name = "LevelTool Android runtime panel";
            runtimeMobilePanel.scaleMode = PanelScaleMode.ConstantPhysicalSize;
            runtimeMobilePanel.referenceDpi = runtimeMobilePanel.fallbackDpi = 160;
            document.panelSettings = runtimeMobilePanel;
        }

        private void UpdateMobileViewport()
        {
            if (!mobileViewportActive || !UsesInternalProjects || root == null || root.panel == null) return;
            PollAndroidKeyboardInset();
            int inset, windowHeight;
            lock (androidViewportLock) { inset = androidKeyboardInset; windowHeight = androidWindowHeight; }
            // OS가 이미 Unity 화면을 줄인 높이는 다시 빼지 않는다.
            float covered = Mathf.Max(0, inset - Mathf.Max(0, windowHeight - Screen.height));
            ApplyMobileViewport(new Vector2(Screen.width, Screen.height), Screen.safeArea,
                new Rect(0, 0, Screen.width, covered));
        }

        private void ApplyMobileViewport(Vector2 screenPixels, Rect safeAreaPixels, Rect keyboardPixels)
        {
            if (root == null || screenPixels.x <= 0 || screenPixels.y <= 0 || root.layout.width <= 0 || root.layout.height <= 0) return;
            mobileViewportActive = true;
            mobileScreenPixels = screenPixels; mobileSafePixels = safeAreaPixels; mobileKeyboardPixels = keyboardPixels;
            float left = Mathf.Clamp(safeAreaPixels.xMin, 0, screenPixels.x);
            float right = Mathf.Clamp(safeAreaPixels.xMax, left, screenPixels.x);
            float bottom = Mathf.Clamp(safeAreaPixels.yMin, 0, screenPixels.y);
            float top = Mathf.Clamp(safeAreaPixels.yMax, bottom, screenPixels.y);
            if (keyboardPixels.width > 0 && keyboardPixels.height > 0 && keyboardPixels.xMin < right && keyboardPixels.xMax > left)
                bottom = Mathf.Min(top, Mathf.Max(bottom, keyboardPixels.yMax));
            float xScale = root.layout.width / screenPixels.x, yScale = root.layout.height / screenPixels.y;
            Rect visible = new Rect(left * xScale, (screenPixels.y - top) * yScale,
                (right - left) * xScale, (top - bottom) * yScale);
            bool changed = visible != mobileVisibleArea;
            if (changed) CancelBoardTouch();
            mobileVisibleArea = visible;
            ApplyTrialReturnLayout();
            root.style.paddingLeft = visible.xMin;
            root.style.paddingRight = root.layout.width - visible.xMax;
            root.style.paddingTop = visible.yMin;
            root.style.paddingBottom = root.layout.height - visible.yMax;
            if (changed || mobileNavigation == null) ApplyMobileLayout(visible.width, visible.height);
            if (modal != null)
            {
                modal.style.left = visible.xMin; modal.style.right = root.layout.width - visible.xMax;
                modal.style.top = visible.yMin; modal.style.bottom = root.layout.height - visible.yMax;
            }
            if (mobileDialogFrame != null)
            {
                mobileDialogFrame.style.width = Mathf.Max(1, Mathf.Min(620, visible.width - 16));
                mobileDialogFrame.style.maxHeight = Mathf.Max(1, visible.height - 16);
            }
            if (changed) root.schedule.Execute(RevealMobileFocusedInput).StartingIn(1);
        }

        private void RevealMobileFocusedInput()
        {
            if (!mobileViewportActive || root?.focusController == null) return;
            VisualElement focused = root.focusController.focusedElement as VisualElement;
            if (focused == null) return;
            ScrollView scroll = focused.GetFirstAncestorOfType<ScrollView>();
            if (scroll != null && scroll.contentContainer.Contains(focused)) scroll.ScrollTo(focused);
        }

        private void PollAndroidKeyboardInset()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (Time.unscaledTime < nextAndroidViewportPoll || androidViewportPending) return;
            nextAndroidViewportPoll = Time.unscaledTime + .15f;
            androidViewportPending = true;
            int generation = androidViewportGeneration;
            using (AndroidJavaClass player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                {
                    int inset = 0, height = 0;
                    try
                    {
                        using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                        using (AndroidJavaObject current = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                        using (AndroidJavaObject window = current.Call<AndroidJavaObject>("getWindow"))
                        using (AndroidJavaObject view = window.Call<AndroidJavaObject>("getDecorView"))
                        using (AndroidJavaClass version = new AndroidJavaClass("android.os.Build$VERSION"))
                        {
                            height = view.Call<int>("getHeight");
                            if (version.GetStatic<int>("SDK_INT") >= 30)
                            {
                                using (AndroidJavaObject insets = view.Call<AndroidJavaObject>("getRootWindowInsets"))
                                using (AndroidJavaClass types = new AndroidJavaClass("android.view.WindowInsets$Type"))
                                {
                                    if (insets != null)
                                        using (AndroidJavaObject ime = insets.Call<AndroidJavaObject>("getInsets", types.CallStatic<int>("ime")))
                                            inset = ime.Get<int>("bottom");
                                }
                            }
                            else
                            {
                                using (AndroidJavaObject frame = new AndroidJavaObject("android.graphics.Rect"))
                                {
                                    view.Call("getWindowVisibleDisplayFrame", frame);
                                    int gap = height - frame.Get<int>("bottom");
                                    if (gap > height * .15f) inset = gap;
                                }
                            }
                        }
                    }
                    catch (System.Exception) { /* 창 생성·전환 중에는 다음 주기에 다시 읽는다. */ }
                    lock (androidViewportLock)
                    {
                        if (generation != androidViewportGeneration) return;
                        androidKeyboardInset = inset; androidWindowHeight = height; androidViewportPending = false;
                    }
                }));
            }
#endif
        }

        private void DisposeMobileViewport()
        {
            lock (androidViewportLock) { androidViewportGeneration++; androidViewportPending = false; }
            if (runtimeMobilePanel == null) return;
            GetComponent<UIDocument>().panelSettings = originalMobilePanel;
            Destroy(runtimeMobilePanel); runtimeMobilePanel = null;
        }
    }
}
#endif
