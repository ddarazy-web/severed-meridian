using System;
using System.Linq;
using Board;
using Levels;
using MemoryPack;
using Simulation;
using UnityEngine;

namespace Tutorial.Editor
{
    public static partial class TutorialComposerConditionsVerification
    {
        private static void VerifyFinalBoundaries()
        {
            LevelDefinition level = TutorialSampleBoards.All.First(sample => sample.Id == "two").CreateBoard();
            try
            {
                byte[] valid = LevelPackCodec.Snapshot(level);
                foreach (string mutation in new[] { "missing-condition", "duplicate-condition", "null-condition", "condition-range", "null-origins", "unknown-origin", "null-target", "missing-step", "duplicate-step", "null-area", "step-range", "inner-version", "truncated" })
                {
                    PackedTutorialConditionDetails pack = MemoryPackSerializer.Deserialize<PackedTutorialConditionDetails>(valid.AsSpan(8));
                    switch (mutation)
                    {
                        case "missing-condition": pack.Conditions = pack.Conditions.Skip(1).ToArray(); break;
                        case "duplicate-condition": pack.Conditions[1] = pack.Conditions[0]; break;
                        case "null-condition": pack.Conditions[0] = null; break;
                        case "condition-range": pack.Conditions[0].ConditionIndex = 999; break;
                        case "null-origins": pack.Conditions[1].AllowedOrigins = null; break;
                        case "unknown-origin": pack.Conditions[1].AllowedOrigins = new[] { (EffectOrigin)999 }; break;
                        case "null-target": pack.Conditions[1].Target = null; break;
                        case "missing-step": pack.Steps = Array.Empty<PackedTutorialStepDetail>(); break;
                        case "duplicate-step": pack.Steps = pack.Steps.Concat(pack.Steps).ToArray(); break;
                        case "null-area": pack.Steps[0].ActionArea = null; break;
                        case "step-range": pack.Steps[0].StepIndex = 999; break;
                        case "inner-version": pack.ComposerPack[4] = 5; break;
                    }
                    byte[] payload = MemoryPackSerializer.Serialize(pack), bytes = new byte[payload.Length + 8];
                    Array.Copy(valid, bytes, 8); Array.Copy(payload, 0, bytes, 8, payload.Length);
                    if (mutation == "truncated") Array.Resize(ref bytes, bytes.Length - 1);
                    bool rejected = false;
                    try { LevelPackCodec.DecodeTutorial(bytes); } catch (Exception) { rejected = true; }
                    Check(rejected, "손상된 팩5 조건·단계 확장 거절 " + mutation);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(level); }

            level = TutorialSampleBoards.All.First(sample => sample.Id == "area").CreateBoard();
            try
            {
                TutorialStepDefinition step = level.Tutorial.steps[0];
                step.actionArea = Enumerable.Range(0, 81).Select(index => new BoardCoordinate(index / 9, index % 9)).ToList();
                foreach (bool possible in new[] { true, false })
                {
                    step.conditions[0] = possible ? new TutorialConditionDefinition() : new TutorialConditionDefinition { kind = TutorialConditionKind.Match, matchSize = 81 };
                    using TutorialBoardAdapter adapter = TutorialBoardAdapter.Prepare(level, StartingBoardBuilder.Build(level, level.Tutorial.seed).State);
                    int random = adapter.Executor.State.Random.DrawCount, moves = adapter.Executor.State.MovesRemaining;
                    string Snapshot() => string.Join(";", adapter.Executor.State.Cells.Select(cell => cell.Coordinate + ":" + cell.Content + ":" + cell.Color)) +
                        string.Join(";", TutorialTargetQuery.Capture(adapter.Executor.State).Select(entity => entity.Occurrence + ":" + entity.Durability));
                    string board = Snapshot();
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    bool found = adapter.TryFindGuidance(out TutorialInput input); watch.Stop();
                    Check(found == possible, "9×9 전체 조작 영역에서 기여 후보 유무 판정 " + possible);
                    Check(adapter.Executor.State.Random.DrawCount == random && adapter.Executor.State.MovesRemaining == moves &&
                        board == Snapshot(),
                        "전체 영역 후보 검색은 실행 보드·난수·이동을 변경하지 않음 " + possible);
                    Results.Add("MEASURE 9x9 guidance possible=" + possible + " elapsedMs=" + watch.Elapsed.TotalMilliseconds.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(level); }
        }
    }
}
