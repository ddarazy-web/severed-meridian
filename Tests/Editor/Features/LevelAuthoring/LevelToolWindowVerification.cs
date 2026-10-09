using System;
using System.Reflection;
using LevelTool;
using UnityEditor;
using UnityEngine;

namespace LevelAuthoring.Editor
{
    public static class LevelToolWindowVerification
    {
        public static void Run()
        {
            try
            {
                Type policy = typeof(LevelToolScreen).Assembly.GetType("LevelTool.LevelToolWindowGeometry");
                Check(policy != null, "window geometry policy exists");
                MethodInfo resize = policy.GetMethod("Resize", BindingFlags.Static | BindingFlags.Public);
                for (int edge = 1; edge <= 8; edge++)
                {
                    RectInt original = new RectInt(90, 70, 1000, 600);
                    RectInt result = (RectInt)resize.Invoke(null, new object[] { original, edge, 16, 39, original });
                    Check(result.width >= 1296 && result.height >= 839 && (result.width - 16) * 5 == (result.height - 39) * 8, "minimum client size and ratio edge " + edge);
                    Check((edge == 1 || edge == 4 || edge == 7) ? result.xMax == original.xMax : result.xMin == original.xMin, "opposite horizontal edge anchored " + edge);
                    Check((edge == 3 || edge == 4 || edge == 5) ? result.yMax == original.yMax : result.yMin == original.yMin, "opposite vertical edge anchored " + edge);
                }
                RectInt wide = (RectInt)resize.Invoke(null, new object[] { new RectInt(0, 0, 1616, 900), 2, 16, 39, new RectInt(0, 0, 1296, 839) });
                Check(wide.width == 1616 && wide.height == 1039, "client 1600 by 1000 excludes frame");
                RectInt corner = (RectInt)resize.Invoke(null, new object[] { new RectInt(0, 0, 1616, 1139), 8, 16, 39, wide });
                Check(corner.height == 1139 && corner.width == 1776, "corner follows vertical drag as well as horizontal drag");
                MethodInfo fit = policy.GetMethod("Fit", BindingFlags.Static | BindingFlags.Public);
                RectInt? fitted = (RectInt?)fit.Invoke(null, new object[] { new RectInt(-2500, 40, 2200, 1400), new RectInt(0, 0, 1920, 1080), 16, 39 });
                Check(fitted.HasValue && fitted.Value.x >= 0 && fitted.Value.xMax <= 1920 && fitted.Value.yMax <= 1080, "disconnected monitor and oversized window recovered");
                Check(fit.Invoke(null, new object[] { new RectInt(0, 0, 1280, 800), new RectInt(0, 0, 1366, 768), 16, 39 }) == null, "short display requests fullscreen instead of violating minimum");
                Type controller = typeof(LevelToolScreen).Assembly.GetType("LevelTool.LevelToolWindowController");
                Check(controller != null && !(bool)controller.GetProperty("Supported").GetValue(null), "editor native window is never controlled");
                object instance = Activator.CreateInstance(controller, true);
                string preferencesBefore = PlayerPrefs.GetString("LevelTool.Window.v1", "<missing>");
                controller.GetMethod("Tick").Invoke(instance, null);
                controller.GetMethod("ToggleFullscreen").Invoke(instance, null);
                ((IDisposable)instance).Dispose();
                Check(PlayerPrefs.GetString("LevelTool.Window.v1", "<missing>") == preferencesBefore, "editor lifecycle does not write standalone preferences");
                Type stateType = controller.GetNestedType("SavedWindow", BindingFlags.NonPublic);
                MethodInfo read = controller.GetMethod("ReadSavedWindow", BindingFlags.Static | BindingFlags.NonPublic);
                Check(read != null, "stored window sizes have a restore policy");
                object fallback = read.Invoke(null, new object[] { "" });
                Check((int)stateType.GetField("width").GetValue(fallback) == 1280 && (int)stateType.GetField("height").GetValue(fallback) == 800, "missing saved size defaults to 1280 by 800");
                object legacy = read.Invoke(null, new object[] { "{\"version\":1,\"width\":1600,\"height\":1000,\"mode\":1}" });
                Check(Convert.ToInt32(stateType.GetField("mode").GetValue(legacy)) == 0 && (int)stateType.GetField("width").GetValue(legacy) == 1600, "old maximized setting becomes windowed without losing saved size");
                object state = read.Invoke(null, new object[] { "{\"version\":1,\"x\":-1400,\"y\":80,\"width\":1600,\"height\":1000,\"mode\":2}" });
                object roundTrip = JsonUtility.FromJson(JsonUtility.ToJson(state), stateType);
                Check((int)stateType.GetField("width").GetValue(roundTrip) == 1600 && (int)stateType.GetField("height").GetValue(roundTrip) == 1000 &&
                    (int)stateType.GetField("x").GetValue(roundTrip) == -1400 && Convert.ToInt32(stateType.GetField("mode").GetValue(roundTrip)) == 2, "fullscreen retains independent normal size and secondary monitor position");
                object invalid = read.Invoke(null, new object[] { "{\"version\":1,\"width\":1920,\"height\":1080,\"mode\":2}" });
                Check((int)stateType.GetField("width").GetValue(invalid) == 1280 && (int)stateType.GetField("height").GetValue(invalid) == 800 &&
                    Convert.ToInt32(stateType.GetField("mode").GetValue(invalid)) == 2, "invalid saved ratio defaults to window size while preserving fullscreen mode");
                Debug.Log("PASS level tool window geometry and editor isolation"); EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
        private static void Check(bool value, string message) { if (!value) throw new Exception("FAIL " + message); Debug.Log("PASS " + message); }
    }
}
