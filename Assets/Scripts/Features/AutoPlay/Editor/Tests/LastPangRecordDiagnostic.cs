using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AutoPlay;
using Simulation;
using UnityEditor;
using UnityEngine;

namespace Levels.Editor
{
    /// <summary>지정된 오류 판 하나를 읽기 전용으로 재생한다. 봇 시험이나 원본 기록 저장은 하지 않는다.</summary>
    internal static class LastPangRecordDiagnostic
    {
        public static void Run() => RunDiagnostic(false);
        public static void ComparePerWaveLimit() => RunDiagnostic(true);
        public static void CheckUpdatedSupply() => RunDiagnostic(false, true);

        /// <param name="perWaveLimit">진단 객체의 누적 카운터만 차수별로 초기화하여 종료 조건의 영향을 비교한다. 제품 코드는 수정하지 않는다.</param>
        private static void RunDiagnostic(bool perWaveLimit, bool updatedSupply = false)
        {
            const string source = "Library/Match/MultiLevelTests/c5405c1ab0f34d3791ba1d8f872a3b7e/0000/306254a8dd2a4b63a87c418df1ba3a83";
            string output = "Logs/LastPangRecordDiagnostic" + (updatedSupply ? "/updated-supply" : perWaveLimit ? "/per-wave" : "");
            Directory.CreateDirectory(output);
            LevelDefinition level = ScriptableObject.CreateInstance<LevelDefinition>();
            List<string> trace = new List<string> { "step\taction\tphase\tround\twave\trandom\tpowers\tpatterns\tchangedCells\tmessage" };
            try
            {
                BotBatchRecord batch = JsonUtility.FromJson<BotBatchRecord>(File.ReadAllText(source + "/batch.json"));
                // 저장 기록이 아닌 진단용 메모리 사본만 새 실행 버전으로 설정한다.
                // 성공 전 행동의 난수 대조는 그대로 유지하며 성공 후 결과는 새 규칙으로 검사한다.
                if (updatedSupply) batch.engineVersion = BoardActionExecutor.Version;
                BotBatchGame game = JsonUtility.FromJson<BotBatchGame>(File.ReadAllText(source + "/000008.json"));
                JsonUtility.FromJsonOverwrite(batch.definitionJson, level);
                using (BotRecordReplay replay = new BotRecordReplay(level, batch, game))
                {
                    replay.Begin(true);
                    FieldInfo executorField = typeof(BotRecordReplay).GetField("executor", BindingFlags.Instance | BindingFlags.NonPublic);
                    string[] previous = null;
                    int step = 0;
                    while (replay.NeedsAdvance && step++ < 5000)
                    {
                        BoardActionExecutor before = (BoardActionExecutor)executorField.GetValue(replay);
                        if (perWaveLimit && before?.Phase == BoardActionPhase.WaitingForLastPang)
                            typeof(BoardActionExecutor).GetProperty("CascadeRounds").GetSetMethod(true).Invoke(before, new object[] { 0 });
                        replay.Advance();
                        BoardActionExecutor executor = (BoardActionExecutor)executorField.GetValue(replay);
                        if (executor == null) continue;
                        string[] cells = executor.State.Cells.Select(c => c.Content + ":" + c.Color + ":" + c.RocketDirection).ToArray();
                        int changed = previous == null ? cells.Length : cells.Where((c, i) => c != previous[i]).Count(); previous = cells;
                        trace.Add($"{step}\t{replay.ActionIndex}\t{executor.Phase}\t{executor.CascadeRounds}\t{executor.LastPangWaves}\t{executor.State.Random.DrawCount}\t" +
                            executor.State.Cells.Count(c => c.Content >= RuntimeContent.Rocket && c.Content <= RuntimeContent.Magnet) + "\t" +
                            MatchQuery.Find(executor.State).Count + $"\t{changed}\t{replay.Message}");
                    }
                    BoardActionExecutor final = (BoardActionExecutor)executorField.GetValue(replay);
                    File.WriteAllText(output + "/summary.txt", $"status={replay.Status}\nmessage={replay.Message}\nsteps={step}\nseed={game.seed}\n" +
                        $"rounds={final?.CascadeRounds}\nwaves={final?.LastPangWaves}\nconversions={final?.LastPangConversions}\nrandom={replay.State?.Random.DrawCount}\nexpectedRandom={game.actions.Last().randomAfter}\n" +
                        $"outcome={final?.Outcome?.Kind}\nlastStep={final?.LastCascadeStep?.Reason}\nlastPang={final?.LastPangMessage}\n");
                    if (updatedSupply && (final?.Outcome?.Kind != BoardOutcomeKind.Won || final.LastCascadeStep?.Reason != CascadeStepReason.LastPangComplete))
                        throw new InvalidOperationException("변경한 공급 규칙으로 오류 판이 정상 종료하지 못했습니다.");
                }
            }
            catch (Exception error) { File.WriteAllText(output + "/summary.txt", error.ToString()); throw; }
            finally { File.WriteAllLines(output + "/trace.tsv", trace); UnityEngine.Object.DestroyImmediate(level); }
        }
    }
}
