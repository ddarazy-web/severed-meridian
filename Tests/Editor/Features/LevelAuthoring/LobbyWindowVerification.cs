using System;
using System.IO;
using System.Reflection;
using Levels;
using UnityEditor;
using UnityEngine;

namespace LevelAuthoring.Editor
{
    public static class LobbyWindowVerification
    {
        public static void Run()
        {
            string root = "Logs/LobbyStage01/window-" + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(root);
            int exit = 1;
            try
            {
                Type type = typeof(LevelPackCodec).Assembly.GetType("Lobby.LobbyListWindow");
                if (type == null) throw new Exception("Virtual list window missing");
                MethodInfo calculate = type.GetMethod("Calculate");
                foreach (int count in new[] { 0, 1, 15, 1000, 10000 })
                foreach (float height in new[] { 1f, 640f, 1800f })
                foreach (float offset in new[] { -100f, 0f, 600f, 5000000f })
                {
                    object window = calculate.Invoke(null, new object[] { count, 80f, height, offset });
                    int first = (int)type.GetProperty("First").GetValue(window);
                    int length = (int)type.GetProperty("Count").GetValue(window);
                    int cap = Math.Min(count, (int)Math.Ceiling(height / 80f) + 4);
                    if (first < 0 || length != cap || first + length > count)
                        throw new Exception($"Invalid range: {count}/{height}/{offset}: {first}+{length}");
                    int visible = Math.Min(Math.Max(0, count - 1), (int)(Math.Max(0, Math.Min(offset, count * 80f - height)) / 80f));
                    if (count > 0 && (visible < first || visible >= first + length))
                        throw new Exception("Visible row omitted");
                }
                File.WriteAllText(Path.Combine(root, "result.txt"), "PASS 0/1/15/1000/10000, overscroll and viewport bounds");
                exit = 0;
            }
            catch (Exception error)
            {
                File.WriteAllText(Path.Combine(root, "result.txt"), "FAIL " + error);
                Debug.LogException(error);
            }
            EditorApplication.Exit(exit);
        }
    }
}
