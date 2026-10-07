using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace GameScreen.Editor
{
    public static partial class PuzzlePresentationAcceptanceVerification
    {
        private const string Output = "Logs/Stage12/";
        private static readonly List<string> results = new List<string>();
        [Serializable] private sealed class BaselineFile { public string Path; public string Hash; }
        [Serializable] private sealed class BaselineFiles { public BaselineFile[] Files; }
        private static void Check(bool value, string name)
        { if (!value) throw new InvalidOperationException(name); results.Add("PASS " + name); }
        private static object Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static object Invoke(object target, string name, params object[] arguments)
            => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, arguments);
        private static string Snapshot(object value) => (string)typeof(Levels.Editor.LevelInitialStateVerification)
            .GetMethod("Snapshot", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new[] { value });
        private static void BaselineChecks()
        {
            BaselineFiles manifest = JsonUtility.FromJson<BaselineFiles>("{\"Files\":" + File.ReadAllText(Output + "baseline-files.json") + "}");
            Check(manifest.Files.Length >= 73, "11단계 이후 착수 기준 존재");
            using SHA256 sha = SHA256.Create();
            foreach (BaselineFile file in manifest.Files)
                Check(File.Exists(file.Path) && BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(file.Path))).Replace("-", "") == file.Hash,
                    "착수 원본·최신 runtime·11단계 검사 보존 " + file.Path);
        }
        public static void Data()
        {
            results.Clear(); int exit = 0;
            try
            {
                BaselineChecks();
                bool missingRejected = false;
                try { ValidatePng(Output + "absent-capture-must-fail.png", 450, 800); }
                catch (IOException) { missingRejected = true; }
                Check(missingRejected, "촬영 요청만 있고 실제 파일 없으면 거부");
                if (File.Exists(Output + "capture-manifest.csv"))
                {
                    string[] rows = File.ReadAllLines(Output + "capture-manifest.csv").Skip(1).ToArray();
                    Check(rows.Length == 20, "실제 촬영 manifest20장");
                    foreach (string row in rows)
                    {
                        string[] columns = row.Split(',');
                        ValidatePng(columns[0], int.Parse(columns[1]), int.Parse(columns[2]));
                    }
                }
            }
            catch (Exception error) { results.Add("FAIL " + error); exit = 1; }
            finally { File.WriteAllLines(Output + "data-results.txt", results); EditorApplication.Exit(exit); }
        }
        private static void ValidatePng(string path, int width, int height)
        {
            byte[] bytes = File.ReadAllBytes(path);
            Texture2D image = new Texture2D(2, 2);
            try
            {
                Check(bytes.Length > 1000 && image.LoadImage(bytes), "실제 PNG 저장·디코딩 " + path);
                Check(image.width == width && image.height == height, "실제 PNG 픽셀 해상도 " + path);
                Color32[] pixels = image.GetPixels32(); HashSet<uint> colors = new HashSet<uint>();
                for (int index = 0; index < pixels.Length; index += 29)
                { Color32 color = pixels[index]; colors.Add((uint)(color.r << 24 | color.g << 16 | color.b << 8 | color.a)); }
                Check(colors.Count > 32, "검은/단색/빈 화면 거부 " + path);
            }
            finally { UnityEngine.Object.DestroyImmediate(image); }
        }
    }
}
