using System;
using System.Linq;
using System.Reflection;
using Board;
using Levels;
using Levels.Editor;
using Simulation;
using UnityEngine;

namespace GameScreen.Editor
{
    public static partial class PuzzleProgressFeedbackVerification
    {
        private static void MissionPathChecks()
        {
            MissionKind[] kinds = ((MissionKind[])Enum.GetValues(typeof(MissionKind))).Concat(new[] { MissionKind.Web }).ToArray();
            for (int fixture = 0; fixture < kinds.Length; fixture++)
            {
                MissionKind kind = kinds[fixture]; bool protectedWeb = fixture == kinds.Length - 1;
                LevelDefinition level = (LevelDefinition)typeof(PowerEffectVerification).GetMethod("Make", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
                GameObject root = null;
                try
                {
                    BoardCoordinate origin = kind == MissionKind.Recovery ? new BoardCoordinate(8, 4) : new BoardCoordinate(4, 4), target = kind == MissionKind.Recovery ? new BoardCoordinate(8, 6) : new BoardCoordinate(4, 6);
                    typeof(PowerEffectVerification).GetMethod("Place", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null,
                        new object[] { level, origin, InitialBlockKind.Rocket, RocketDirection.Horizontal, RabbitColor.Type1 });
                    ObstacleKind? bodyKind = kind switch
                    {
                        MissionKind.Crate => ObstacleKind.Crate, MissionKind.Scrap => ObstacleKind.Scrap, MissionKind.Safe => ObstacleKind.Safe,
                        MissionKind.ColorLock => ObstacleKind.ColorLock, MissionKind.Appliance => ObstacleKind.Appliance, _ => (ObstacleKind?)null
                    };
                    if (bodyKind.HasValue)
                    {
                        var footprint = bodyKind == ObstacleKind.Appliance ? new[] { target, new BoardCoordinate(4, 7), new BoardCoordinate(5, 6), new BoardCoordinate(5, 7) } : new[] { target };
                        LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Block, Erase = true }, footprint);
                        Check(LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)bodyKind.Value,
                            Durability = bodyKind == ObstacleKind.Appliance ? 2 : 1, Color = RabbitColor.Type1 }, new[] { target }).Changed == 1, kind + " 실제 장애물 배치");
                    }
                    if (kind == MissionKind.Web || kind == MissionKind.Mold)
                        Check(LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Cover, Kind = (int)(kind == MissionKind.Web ? CoverKind.Web : CoverKind.Mold),
                            Durability = protectedWeb ? 2 : 1 }, new[] { target }).Changed == 1, kind + " 실제 덮개 배치");
                    if (kind == MissionKind.Dust)
                        Check(LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Dust, Durability = 1 }, new[] { target }).Changed == 1, "실제 먼지 배치");
                    if (kind == MissionKind.Recovery)
                    {
                        BoardCoordinate recovery = new BoardCoordinate(7, 6);
                        LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Block, Erase = true }, new[] { recovery });
                        Check(LevelSupplyEditing.PlaceRecovery(level, new[] { recovery }) == null && LevelFlowEditing.SetArrival(level, target, false) == null, "실제 회수·도착 배치");
                    }
                    JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":" + (int)kind + ",\"color\":0,\"count\":" + (kind == MissionKind.Mold ? 0 : 1) + "}]}", level);
                    LevelStateBuildResult built = LevelStateBuilder.Build(level, 12345);
                    Check(built.IsBuilt, kind + " 실제 규칙 fixture 유효: " + string.Join(" | ", built.Issues));
                    BoardActionExecutor executor = new BoardActionExecutor(built.State);
                    root = new GameObject("Mission path " + kind); PuzzleGameSession session = root.AddComponent<PuzzleGameSession>();
                    typeof(PuzzleGameSession).GetField("executor", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, executor);
                    typeof(PuzzleGameSession).GetField("ready", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, true);
                    Invoke(session, "InitializeProgress");
                    BoardActionResult action = executor.Activate(origin); Check(action.IsApplied, kind + " 실제 로켓 규칙 실행");
                    if (kind == MissionKind.Recovery) executor.AdvanceCascade();
                    if (protectedWeb)
                    {
                        Check(executor.State.Missions[0].Progress == 0 && executor.State.MissionProgressRecords.Count == 0, "거미줄 생존·유효 손상 1회는 미션 수집 기록 없음");
                        Invoke(session, "ScheduleProgress", new PuzzleEffectTimeline(built.State, action.Changes, action.Effects, action.PowerTrace));
                        Invoke(session, "TickProgress", 1f);
                        Check(!session.ProgressFeedback.IsBusy && session.ProgressFeedback.CompletionCount(0) == 0, "진행 0 타격은 가짜 수집·완료 강조 없음");
                        continue;
                    }
                    Check(executor.State.Missions[0].Progress == 1 && executor.State.MissionProgressRecords.Count == 1, kind + " 실제 갱신 지점에서 증가 1만 기록");
                    MissionProgressRecord record = executor.State.MissionProgressRecords[0];
                    Check(record.Amount == 1 && record.Source.HasValue && (kind == MissionKind.Color ?
                        record.Source.Value.Row == 4 && built.State.CellAt(record.Source.Value).Color == RabbitColor.Type1 : record.Source.Value.Equals(target)), kind + " 실제 제거·소비·도착 원점 유지");
                    if (bodyKind.HasValue) Check(record.BodyIndex == 0, kind + " 실제 제거 본체 인덱스 유지");
                    if (kind == MissionKind.Recovery)
                    {
                        Check(executor.State.Recoveries.Single().Batch > 0, "상단 회수 부품은 아래 블록 제거 후 실제 낙하로 도착");
                        session.ProgressFeedback.Schedule(executor.State, item => 0f);
                    }
                    else Invoke(session, "ScheduleProgress", new PuzzleEffectTimeline(built.State, action.Changes, action.Effects, action.PowerTrace));
                    Invoke(session, "TickProgress", 1f);
                    Check(session.ProgressFeedback.Flights.Count == 1 && session.DisplayedMissionProgress(0) == 0, kind + " 실제 진행 기록 수집·도착 전 수치 보존");
                    Invoke(session, "TickProgress", .32f);
                    Check(session.DisplayedMissionProgress(0) == 1 && session.ProgressFeedback.CompletionCount(0) == 1, kind + " 도착 시 실제 증가와 완료 1회 반영");
                }
                finally { if (root != null) UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(level); }
            }
            foreach (ObstacleKind kind in new[] { ObstacleKind.Crate, ObstacleKind.Safe, ObstacleKind.ColorLock, ObstacleKind.Appliance })
            {
                LevelDefinition level = (LevelDefinition)typeof(GeneratorVerification).GetMethod("Make", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,
                    new object[] { kind, 3 });
                try
                {
                    LevelStateBuildResult built = LevelStateBuilder.Build(level, 12345);
                    Check(built.IsBuilt, "발전기 간접 미션 레벨 유효 " + kind);
                    object context = typeof(GeneratorVerification).GetMethod("Context", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                    for (int turn = 1; turn <= 3; turn++)
                    {
                        context = typeof(GeneratorVerification).GetMethod("Next", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new[] { context, (object)turn });
                        typeof(GeneratorVerification).GetMethod("Hit", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,
                            new[] { (object)built.State, new BoardCoordinate(4, 4), context });
                        if (turn < 3) Check(built.State.MissionProgressRecords.Count == 0, "발전기 미완충은 수집 없음 " + kind + "/" + turn);
                    }
                    MissionProgressRecord record = built.State.MissionProgressRecords.Single();
                    Check(record.Amount == 1 && record.BodyIndex == 1 && record.Source.HasValue && record.Source.Value.Equals(new BoardCoordinate(4, 7)) && built.State.Missions[0].Progress == 1,
                        "발전기 완충 간접 제거도 실제 대상 본체·원점·증가 한 번 " + kind);
                }
                finally { UnityEngine.Object.DestroyImmediate(level); }
            }
        }
    }
}
