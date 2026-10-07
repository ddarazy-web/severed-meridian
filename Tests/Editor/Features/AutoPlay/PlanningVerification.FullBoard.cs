using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using AutoPlay;
using Board;
using UnityEditor;
using UnityEngine;

namespace Levels.Editor
{
    public static partial class PlanningVerification
    {
        /// <summary>한도용 인위적 판과 구분하여, 시작 검사를 통과한 전체 10×10 판의 실제 한 수를 측정한다.</summary>
        public static void FullBoard()
        {
            LevelDefinition level = null; Exception failure = null; Results.Clear();
            try
            {
                BoardCoordinate[] active = Enumerable.Range(0, 100).Select(i => new BoardCoordinate(i / 10, i % 10)).ToArray();
                level = (LevelDefinition)typeof(SettlementVerification).GetMethod("Make", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { active });
                JsonUtility.FromJsonOverwrite("{\"initialBlocks\":[],\"moveCount\":10,\"missions\":[{\"kind\":0,\"color\":0,\"count\":100}]}", level);
                string original = JsonUtility.ToJson(level); bool dirty = EditorUtility.IsDirty(level);
                using BotPlaySession game = new BotPlaySession(level, 771, BotStrategyKind.Planning);
                for (int i = 0; game.NeedsAdvance && i < 20000; i++) game.Advance();
                Check(game.Status == BotSessionStatus.Ready && game.State.Cells.Count(cell => cell.IsActive) == 100, "전체 10×10 시작 조건 통과");
                game.Begin(false); Stopwatch elapsed = Stopwatch.StartNew(); double maximumStep = 0; int updates = 0;
                while (game.NeedsAdvance && updates++ < 20000)
                {
                    long before = Stopwatch.GetTimestamp(); game.Advance();
                    maximumStep = Math.Max(maximumStep, (Stopwatch.GetTimestamp() - before) * 1000d / Stopwatch.Frequency);
                }
                elapsed.Stop();
                File.WriteAllText(Evidence + "/full-board-measurement.txt", $"Version={PlanningSearch.Version}\nSeed=771\nActiveCells=100\nUpdates={updates}\nMilliseconds={elapsed.ElapsedMilliseconds}\nMaxStepMs={maximumStep:F3}\n{game.LastChoice?.Reason}\n{game.Message}\n");
                Check(game.Status == BotSessionStatus.Ready && game.Records.Count == 1, "전체 10×10 계획 후 실제 한 행동과 연쇄 정상 완료");
                Check(game.LastChoice.Reason.Contains("두 수 계획") && !game.LastChoice.Reason.Contains("기본 전략으로 전환"), "전체 10×10 유효 공통 표본으로 두 수 계획 선택");
                Check(game.State.MovesRemaining == 9, "전체 10×10 가정 수와 무관하게 실제 이동 비용 한 번");
                Check(original == JsonUtility.ToJson(level) && dirty == EditorUtility.IsDirty(level), "전체 10×10 계획 후 원본·dirty 보존");
            }
            catch (Exception error) { failure = error; Results.Add("FAIL " + error); }
            finally { if (level != null) UnityEngine.Object.DestroyImmediate(level); }
            File.WriteAllLines(Evidence + "/full-board-results.txt", Results); EditorApplication.Exit(failure == null ? 0 : 1);
        }
    }
}
