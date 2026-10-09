#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using UnityEngine;

namespace LevelTool
{
    // 기존 저장본의 전체 화면 값(2)을 유지한다. 이전 최대화 값(1)은 읽을 때 일반 창으로 전환한다.
    internal enum LevelToolWindowMode { Windowed = 0, Fullscreen = 2 }

    internal sealed class LevelToolWindowController : IDisposable
    {
        public static bool Supported => !Application.isEditor && Application.platform == RuntimePlatform.WindowsPlayer;
        public bool Ready { get; private set; }
        public string Error { get; private set; }
        public LevelToolWindowMode Mode { get; private set; }

        [Serializable]
        private sealed class SavedWindow
        {
            public int version = 1, width = 1280, height = 800, x = 40, y = 40;
            public LevelToolWindowMode mode;
        }

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        private const string Preference = "LevelTool.Window.v1";
        private LevelToolNativeWindow native;
        private SavedWindow saved;
        private string written, observed;
        private float changedAt, transitionAt, startedAt = Time.realtimeSinceStartup;
        private bool transitioning, placed;

        private static SavedWindow ReadSavedWindow(string json)
        {
            SavedWindow value;
            try { value = JsonUtility.FromJson<SavedWindow>(json); }
            catch (ArgumentException) { value = null; }
            if (value == null || value.version != 1) return new SavedWindow();
            if (!Enum.IsDefined(typeof(LevelToolWindowMode), value.mode)) value.mode = LevelToolWindowMode.Windowed;
            if (value.width < 1280 || value.width > 32768 || value.height < 800 || value.height > 32768 || value.width * 5 != value.height * 8)
            { value.width = 1280; value.height = 800; }
            if (Math.Abs((long)value.x) > 1000000 || Math.Abs((long)value.y) > 1000000) { value.x = 40; value.y = 40; }
            return value;
        }

        public void Tick()
        {
            if (!Supported || Error != null) return;
            try
            {
                if (native == null)
                {
                    native = LevelToolNativeWindow.TryOpen();
                    if (native == null)
                    {
                        if (Time.realtimeSinceStartup - startedAt > 10) throw new InvalidOperationException("레벨툴 Windows 창을 찾지 못했습니다.");
                        return;
                    }
                    written = PlayerPrefs.GetString(Preference, "");
                    saved = ReadSavedWindow(written);
                    Ready = true;
                    Mode = saved.mode;
                    SetMode(saved.mode, false);
                }
                if (!native.Alive) return;
                if (native.ToggleRequested)
                {
                    native.ToggleRequested = false;
                    if (!transitioning) ToggleFullscreen();
                }
                if (transitioning)
                {
                    float elapsed = Time.realtimeSinceStartup - transitionAt;
                    bool fullscreen = Mode == LevelToolWindowMode.Fullscreen;
                    if (elapsed > 5) throw new InvalidOperationException("창 모드 전환 시간이 초과되었습니다.");
                    if (Screen.fullScreen != fullscreen || elapsed < 0.25f) return;
                    if (!placed)
                    {
                        if (!fullscreen)
                        {
                            native.Restore();
                            native.DisableMaximize();
                            Vector2Int frame = native.Frame;
                            RectInt requested = new RectInt(saved.x, saved.y, saved.width + frame.x, saved.height + frame.y);
                            RectInt? fitted = LevelToolWindowGeometry.Fit(requested, native.WorkArea(requested), frame.x, frame.y);
                            if (!fitted.HasValue)
                            {
                                // 작업 표시줄과 창 테두리를 포함해 최소 크기를 확보할 수 없으면 전체 화면으로 연다.
                                SetMode(LevelToolWindowMode.Fullscreen); return;
                            }
                            native.Place(fitted.Value);
                        }
                        placed = true; transitionAt = Time.realtimeSinceStartup; return;
                    }
                    transitioning = false; native.Constrain = !fullscreen;
                }
                Capture();
                string current = JsonUtility.ToJson(saved);
                if (current != observed) { observed = current; changedAt = Time.realtimeSinceStartup; }
                if (!native.Sizing && Time.realtimeSinceStartup - changedAt >= 0.5f) Persist(current);
            }
            catch (Exception error)
            {
                Error = "창 설정 적용 실패: " + error.Message; Ready = false;
                native?.Dispose(); native = null; Debug.LogError(Error);
            }
        }

        public void ToggleFullscreen() => SetMode(Mode == LevelToolWindowMode.Fullscreen ? LevelToolWindowMode.Windowed : LevelToolWindowMode.Fullscreen);

        public void SetMode(LevelToolWindowMode mode, bool capture = true)
        {
            if (!Ready || !native.Alive || (transitioning && !placed && mode == Mode)) return;
            if (capture && !transitioning) Capture();
            Mode = mode; saved.mode = mode;
            native.Constrain = false;
            transitioning = true; placed = false; transitionAt = Time.realtimeSinceStartup;
            // Unity는 프레임 종료 후 모드를 바꾼다. 그 뒤 Tick에서 실제 창 장식을 측정하고 복원한다.
            if (mode == LevelToolWindowMode.Fullscreen)
                Screen.SetResolution(Display.main.systemWidth, Display.main.systemHeight, FullScreenMode.FullScreenWindow);
            else
                Screen.SetResolution(saved.width, saved.height, FullScreenMode.Windowed);
        }

        private void Capture()
        {
            if (saved == null || native == null || !native.Alive || native.Minimized || transitioning) return;
            if (Screen.fullScreen) { Mode = saved.mode = LevelToolWindowMode.Fullscreen; return; }
            Mode = saved.mode = LevelToolWindowMode.Windowed;
            native.DisableMaximize(); native.RestoreIfMaximized();
            if (native.Sizing) return;
            Vector2Int frame = native.Frame;
            RectInt rect = native.Bounds;
            RectInt normalized = LevelToolWindowGeometry.Resize(rect, 2, frame.x, frame.y, rect);
            // 키보드 스냅처럼 WM_SIZING을 거치지 않는 일반 창 변경에도 같은 크기 규칙을 적용한다.
            if (normalized != rect) { native.Place(normalized); rect = normalized; }
            saved.width = rect.width - frame.x; saved.height = rect.height - frame.y; saved.x = rect.x; saved.y = rect.y;
        }

        private void Persist(string json)
        {
            if (json == written) return;
            PlayerPrefs.SetString(Preference, json); PlayerPrefs.Save(); written = json;
        }

        public void Dispose()
        {
            try
            {
                if (Supported && saved != null) { Capture(); Persist(JsonUtility.ToJson(saved)); }
            }
            catch (Exception error) { Debug.LogError("창 설정 저장 실패: " + error.Message); }
            finally { native?.Dispose(); native = null; Ready = false; }
        }
#else
        public void Tick() { }
        public void ToggleFullscreen() { }
        public void SetMode(LevelToolWindowMode mode) { }
        public void Dispose() { }
#endif
    }
}
#endif
