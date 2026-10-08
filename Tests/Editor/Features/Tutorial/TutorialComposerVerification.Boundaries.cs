using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Levels;
using MemoryPack;
using UnityEngine;
using Board;
using Simulation;

namespace Tutorial.Editor
{
    public static partial class TutorialComposerVerification
    {
        private static void VerifyActualGroups()
        {
            foreach (string shape in new[] { "separate", "T", "L" })
            {
                LevelDefinition level = LevelPackCodec.ReadLevel(File.ReadAllBytes("Tests/Fixtures/Tutorial/legacy-v3.bytes"), 1);
                try
                {
                    BoardCoordinate first = shape == "separate" ? new BoardCoordinate(2, 2) : shape == "T" ? new BoardCoordinate(3, 3) : new BoardCoordinate(3, 2);
                    BoardCoordinate second = shape == "separate" ? new BoardCoordinate(3, 2) : new BoardCoordinate(2, first.Column);
                    TutorialStepDefinition step = TutorialSampleCatalog.CreateSwap(first, second, true);
                    step.conditions[0].matchSize = shape == "separate" ? 3 : 5;
                    if (shape == "separate") step.conditions.Add(new TutorialConditionDefinition
                    { kind = TutorialConditionKind.Match, origin = TutorialMatchOrigin.IncludingCascade });
                    level.Tutorial.steps = new List<TutorialStepDefinition> { step };
                    LevelRuntimeState state = LevelStateBuilder.Build(level, level.Tutorial.seed).State;
                    void SetCell(int row, int column, RabbitColor? color)
                    {
                        RuntimeCell cell = state.CellAt(new BoardCoordinate(row, column));
                        typeof(RuntimeCell).GetProperty("Content").SetValue(cell, color.HasValue ? RuntimeContent.Normal : RuntimeContent.Empty);
                        typeof(RuntimeCell).GetProperty("Color").SetValue(cell, color);
                    }
                    foreach (RuntimeCell cell in state.Cells) SetCell(cell.Coordinate.Row, cell.Coordinate.Column, null);
                    if (shape == "separate")
                    {
                        SetCell(2, 1, RabbitColor.Type1); SetCell(2, 2, RabbitColor.Type2); SetCell(2, 3, RabbitColor.Type1);
                        SetCell(3, 1, RabbitColor.Type2); SetCell(3, 2, RabbitColor.Type1); SetCell(3, 3, RabbitColor.Type2);
                    }
                    else
                    {
                        SetCell(3, 2, RabbitColor.Type1); SetCell(3, 3, RabbitColor.Type1); SetCell(3, 4, RabbitColor.Type1);
                        SetCell(4, first.Column, RabbitColor.Type1); SetCell(5, first.Column, RabbitColor.Type1);
                        SetCell(first.Row, first.Column, RabbitColor.Type2); SetCell(second.Row, second.Column, RabbitColor.Type1);
                    }
                    BoardActionExecutor executor = new BoardActionExecutor(state);
                    using TutorialBoardAdapter adapter = new TutorialBoardAdapter(level, executor);
                    Check(adapter.TryBegin(TutorialInput.Swap(first, second)), shape + " 실제 교환 승인");
                    BoardActionResult result = executor.Swap(first, second);
                    Check(result.IsApplied && result.Decisions.Count == (shape == "separate" ? 2 : 1), shape + " 실제 게임 매칭 묶음 수");
                    adapter.ReportAction(true);
                    Check(adapter.Progress.Snapshot.ConditionCounts[0] == (shape == "separate" ? 2 : 1), shape + " 튜토리얼 매칭 묶음 집계");
                    if (shape == "separate")
                    {
                        // 자동 매칭 직전 상태만 구성하고, 판정·사건 전달은 실제 연쇄 실행기를 사용한다.
                        foreach (int column in new[] { 1, 2, 3 })
                        {
                            RuntimeCell cell = executor.State.CellAt(new BoardCoordinate(6, column));
                            typeof(RuntimeCell).GetProperty("Content").SetValue(cell, RuntimeContent.Normal);
                            typeof(RuntimeCell).GetProperty("Color").SetValue(cell, RabbitColor.Type1);
                        }
                        typeof(BoardActionExecutor).GetProperty("Phase").SetValue(executor, BoardActionPhase.WaitingForAutomaticMatch);
                        CascadeStepResult cascade = executor.ResolveAutomaticMatch();
                        Check(cascade.Reason == CascadeStepReason.Matched && cascade.Decisions.Count == 1, "실제 자동 매칭 판정 한 묶음");
                        adapter.ObserveCascade(cascade); adapter.ObserveCascade(cascade);
                        Check(adapter.Progress.Snapshot.ConditionCounts.SequenceEqual(new[] { 2, 3 }), "실제 연쇄는 직접 전용에서 제외·포함 조건에만 한 번 반영");
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(level); }
            }
        }

        private static void VerifyMoveBudget()
        {
            LevelDefinition level = LevelPackCodec.ReadLevel(File.ReadAllBytes("Tests/Fixtures/Tutorial/legacy-v3.bytes"), 1);
            try
            {
                TutorialStepDefinition step = level.Tutorial.steps.First(value => value.kind == TutorialStepKind.Swap);
                level.Tutorial.steps = new List<TutorialStepDefinition> { step };
                step.conditions.Add(new TutorialConditionDefinition { requiredCount = level.MoveCount + 1 });
                Check(LevelTutorialValidator.Validate(level).Any(issue => issue.Message.Contains("이동")), "가능한 이동 수보다 많은 교환 요구는 제작 오류");
                step.conditions.Add(new TutorialConditionDefinition { kind = TutorialConditionKind.Match });
                step.combination = TutorialConditionCombination.Any;
                Check(!LevelTutorialValidator.Validate(level).Any(issue => issue.Message.Contains("이동")), "하나 이상 조건에서 한 번 매칭 대안을 불가능하다고 오판하지 않음");
            }
            finally { UnityEngine.Object.DestroyImmediate(level); }
        }

        private static void VerifyPackBoundaries()
        {
            LevelDefinition level = LevelPackCodec.ReadLevel(File.ReadAllBytes("Tests/Fixtures/Tutorial/legacy-v3.bytes"), 1);
            try
            {
                TutorialStepDefinition step = level.Tutorial.steps.First(value => value.kind == TutorialStepKind.Swap);
                step.conditions.Add(new TutorialConditionDefinition());
                byte[] valid = LevelPackCodec.Snapshot(level);
                foreach (string mutation in new[] { "missing", "duplicate", "range", "unknown" })
                {
                    PackedTutorialComposer pack = MemoryPackSerializer.Deserialize<PackedTutorialComposer>(valid.AsSpan(8));
                    if (mutation == "missing") pack.Steps = Array.Empty<PackedTutorialComposerStep>();
                    if (mutation == "duplicate") pack.Steps[1] = pack.Steps[0];
                    if (mutation == "range") pack.Steps[0].StepIndex = 999;
                    if (mutation == "unknown") pack.Steps.First(value => value.Conditions.Length > 0).Conditions[0].kind = (TutorialConditionKind)999;
                    byte[] payload = MemoryPackSerializer.Serialize(pack), bytes = new byte[payload.Length + 8];
                    Array.Copy(valid, bytes, 8); Array.Copy(payload, 0, bytes, 8, payload.Length);
                    bool rejected = false;
                    try { LevelPackCodec.DecodeTutorial(bytes); } catch (Exception) { rejected = true; }
                    Check(rejected, "손상된 팩4 확장 거절 " + mutation);
                }
                level.Tutorial.steps.Clear();
                byte[] plain = LevelPackCodec.Snapshot(level);
                LevelDefinition restored = LevelPackCodec.ReadLevel(plain, 1);
                try { Check(!restored.HasTutorial, "새 저장 경로에서도 무튜토리얼 레벨 보존"); }
                finally { UnityEngine.Object.DestroyImmediate(restored); }
            }
            finally { UnityEngine.Object.DestroyImmediate(level); }
        }
    }
}
