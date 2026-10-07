using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Board;
using Levels;
using Levels.Editor;
using Simulation;
using UnityEditor;
using UnityEngine;

namespace GameScreen.Editor
{
    public static partial class PuzzleStabilityVerification
    {
        private const string Output = "Logs/Stage11/";
        private static readonly List<string> results = new List<string>();
        [Serializable] private sealed class BaselineFile { public string Path; public string Hash; }
        [Serializable] private sealed class BaselineFiles { public BaselineFile[] Files; }
        private static void Check(bool value, string name)
        { if (!value) throw new InvalidOperationException(name); results.Add("PASS " + name); }
        private static object Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static object Invoke(object target, string name, params object[] arguments)
            => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, arguments);
        private static string Snapshot(object state) => (string)typeof(LevelInitialStateVerification).GetMethod("Snapshot", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new[] { state });
        public static void Data()
        {
            Directory.CreateDirectory(Output); results.Clear(); int exit = 0; GameObject owner = null;
            try
            {
                BaselineInvariantChecks();
                Check(BoardDefinition.DefaultRows == 9 && BoardDefinition.DefaultColumns == 9 && LevelPackCodec.LevelsPerPack == 50, "9x9·50레벨 MemoryPack 기준");
                owner = new GameObject("Stage11-Baseline"); PuzzleGameSession session = owner.AddComponent<PuzzleGameSession>();
                foreach (var expected in new[] { ("swapSeconds", .15f), ("removalSeconds", .12f), ("fallSeconds", .12f), ("supplySeconds", .16f), ("landingSeconds", .06f) })
                    Check(Mathf.Approximately((float)Field(session, expected.Item1), expected.Item2), "착수 시 기본 시간 " + expected.Item1);
                PuzzleAudioPlayback player = owner.AddComponent<PuzzleAudioPlayback>(); player.Initialize();
                Check(player.ClipCount == 14 && player.SourceCount == 8, "기존 14 합성 클립·8음성 기준");
                PuzzleFeedbackSchedule schedule = new PuzzleFeedbackSchedule();
                schedule.Schedule(PuzzleFeedbackCueKind.Swap, 0); schedule.Schedule(PuzzleFeedbackCueKind.Swap, .03f); schedule.Schedule(PuzzleFeedbackCueKind.Swap, .07f);
                Check(schedule.Tick(.1f).Count == 2 && schedule.Tick(.1f).Count == 0, "기존 .06초 제한과 일회 소비");
                foreach (string name in new[] { "swap", "shuffle" })
                {
                    Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/UI/Puzzle/" + name + ".png");
                    Check(texture != null && texture.width == 256 && texture.height == 256, name + " 256x256 이미지 유지");
                }
                LevelDefinition level = AssetDatabase.LoadAssetAtPath<LevelDefinition>("Assets/Data/Levels/Level_01.asset");
                string original = JsonUtility.ToJson(level);
                LevelStateBuildResult built = LevelStateBuilder.Build(level, 12345);
                Check(built.IsBuilt && JsonUtility.ToJson(level) == original, "원본 Level_01 초기화 불변");
                Check(Snapshot(built.State) == Snapshot(new BoardActionExecutor(built.State).State), "전체 상태 Snapshot 기준 정상");
                string settlement = File.ReadAllText("Assets/Scripts/Features/GameScreen/Runtime/World/PuzzleBoardSettlementPlayback.cs");
                Check(settlement.Contains("moveTime /= 1.44f; supplyTime /= 1.44f;"), "최신 낙하/공급 속도 보존 감사");
                foreach (string path in new[] { "Runtime/World/PuzzlePowerPlayback.Clips.cs", "Runtime/World/PuzzleEffectTimeline.cs" })
                {
                    BaselineFiles manifest = JsonUtility.FromJson<BaselineFiles>("{\"Files\":" + File.ReadAllText(Output + "baseline-files.json") + "}");
                    string full = "Assets/Scripts/Features/GameScreen/" + path;
                    BaselineFile entry = manifest.Files.Single(file => file.Path == full);
                    using SHA256 sha = SHA256.Create();
                    Check(BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(full))).Replace("-", "") == entry.Hash, "최신 드론 시간표/선회 소스 보존 " + path);
                }
            }
            catch (Exception error) { results.Add("FAIL " + error); exit = 1; }
            finally { if (owner != null) UnityEngine.Object.DestroyImmediate(owner); File.WriteAllLines(Output + "data-results.txt", results); EditorApplication.Exit(exit); }
        }
        private static void BaselineInvariantChecks()
        {
            BaselineFiles manifest = JsonUtility.FromJson<BaselineFiles>("{\"Files\":" + File.ReadAllText(Output + "baseline-files.json") + "}");
            Check(manifest.Files.Length > 0, "착수 해시 기준 존재");
            using SHA256 sha = SHA256.Create();
            foreach (BaselineFile entry in manifest.Files.Where(file => !file.Path.StartsWith("Assets/Scripts/")))
                Check(File.Exists(entry.Path) && BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(entry.Path))).Replace("-", "") == entry.Hash,
                    "원본 레벨·씬·프리팹·아이콘/GUID 불변 " + entry.Path);
        }
    }
}
