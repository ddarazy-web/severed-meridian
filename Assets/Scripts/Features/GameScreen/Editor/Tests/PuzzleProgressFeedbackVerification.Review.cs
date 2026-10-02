using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Board;
using Levels;
using Levels.Editor;
using Simulation;
using UnityEditor;
using UnityEngine;

namespace GameScreen.Editor
{
    public static partial class PuzzleProgressFeedbackVerification
    {
        public static void ReviewData()
        {
            results.Clear(); int failures = 0;
            foreach (Action check in new Action[] { ReviewWebMatch, ReviewItemCascade })
                try { check(); } catch (Exception error) { failures++; results.Add("FAIL " + error); }
            File.WriteAllLines(Output + "review-data-results.txt", results); EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        private static void ReviewWebMatch()
        {
            LevelDefinition level = (LevelDefinition)typeof(PowerEffectVerification).GetMethod("Make", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            GameObject root = null;
            try
            {
                foreach (BoardCoordinate at in new[] { new BoardCoordinate(4, 3), new BoardCoordinate(4, 4), new BoardCoordinate(3, 5) })
                    typeof(PowerEffectVerification).GetMethod("Place", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,
                        new object[] { level, at, InitialBlockKind.FixedNormal, RocketDirection.Horizontal, RabbitColor.Type1 });
                typeof(PowerEffectVerification).GetMethod("Place", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,
                    new object[] { level, new BoardCoordinate(4, 5), InitialBlockKind.FixedNormal, RocketDirection.Horizontal, RabbitColor.Type2 });
                typeof(PowerEffectVerification).GetMethod("Place", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,
                    new object[] { level, new BoardCoordinate(4, 2), InitialBlockKind.FixedNormal, RocketDirection.Horizontal, RabbitColor.Type3 });
                LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Cover, Kind = (int)CoverKind.Web, Durability = 1 }, new[] { new BoardCoordinate(4, 4) });
                JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":" + (int)MissionKind.Web + ",\"count\":1}]}", level);
                LevelStateBuildResult built = LevelStateBuilder.Build(level, 12345); Check(built.IsBuilt, "리뷰 직접 매칭 거미줄 fixture 유효");
                BoardActionExecutor executor = new BoardActionExecutor(built.State);
                root = new GameObject("Review Web"); PuzzleGameSession session = root.AddComponent<PuzzleGameSession>();
                typeof(PuzzleGameSession).GetField("executor", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, executor);
                typeof(PuzzleGameSession).GetField("ready", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, true); Invoke(session, "InitializeProgress");
                BoardActionResult action = executor.Swap(new BoardCoordinate(4, 5), new BoardCoordinate(3, 5));
                Check(action.IsApplied && action.Changes.Any(change => change.CoverBefore > 0 && change.CoverAfter == 0) && executor.State.Missions[0].Progress == 1,
                    "실제 직접 매칭 거미줄 제거 기록: " + action.Message);
                Invoke(session, "ScheduleProgress", new PuzzleEffectTimeline(built.State, action.Changes, action.Effects, action.PowerTrace));
                Invoke(session, "TickProgress", .1f);
                Check(session.DisplayedMissionProgress(0) == 0 && session.ProgressFeedback.Flights.Count == 0, "직접 매칭 거미줄 .12초 제거 이전 수치·수집 대기");
                Invoke(session, "TickProgress", .021f);
                Check(session.ProgressFeedback.Flights.Count == 1 && session.ProgressFeedback.Flights[0].Source.Value.Equals(new BoardCoordinate(4, 4)) && session.DisplayedMissionProgress(0) == 0,
                    "직접 매칭 거미줄 제거 시각에 실제 원점 수집 출발");
                Invoke(session, "TickProgress", .32f);
                Check(session.DisplayedMissionProgress(0) == 1 && session.ProgressFeedback.CompletionCount(0) == 1, "직접 매칭 거미줄 도착 후 완료 한 번");
            }
            finally { if (root != null) UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(level); }
        }

        private static void ReviewItemCascade()
        {
            LevelDefinition level = (LevelDefinition)typeof(PowerEffectVerification).GetMethod("Make", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            LevelDefinition cascadeLevel = null; GameObject root = null;
            try
            {
                typeof(PowerEffectVerification).GetMethod("Place", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,
                    new object[] { level, new BoardCoordinate(4, 4), InitialBlockKind.Rocket, RocketDirection.Horizontal, RabbitColor.Type1 });
                BoardActionExecutor executor = new BoardActionExecutor(LevelStateBuilder.Build(level, 12345).State);
                root = new GameObject("Review Item Cascade"); PuzzleGameSession session = root.AddComponent<PuzzleGameSession>();
                typeof(PuzzleGameSession).GetField("executor", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, executor);
                typeof(PuzzleGameSession).GetField("ready", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, true); Invoke(session, "InitializeProgress");
                cascadeLevel = (LevelDefinition)typeof(CascadeVerification).GetMethod("Make", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,
                    new object[] { new[] { new BoardCoordinate(0, 0), new BoardCoordinate(0, 1), new BoardCoordinate(0, 2) }, 20 });
                BoardActionExecutor automatic = (BoardActionExecutor)typeof(CascadeVerification).GetMethod("Automatic", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,
                    new object[] { cascadeLevel, 12345 });
                CascadeStepResult match = automatic.AdvanceCascade(); Check(match.Reason == CascadeStepReason.Matched, "리뷰 실제 새 매칭 라운드 기록");
                Invoke(session, "ObserveCascadeStep", match); Invoke(session, "TickProgress", .5f);
                Check(executor.UseItem(BoardItem.Hammer, new BoardCoordinate(4, 3)).IsApplied, "이전 연쇄 후 이동 미소비 아이템 실제 행동");
                Invoke(session, "ObserveMoves"); Invoke(session, "ObserveCascadeStep", match);
                Check(session.MovesPulse == 0 && session.CascadePulse > 0, "아이템 새 행동의 연쇄 라운드 재시작·이동 강조 없음");
                Invoke(session, "TickProgress", .5f); Invoke(session, "ObserveCascadeStep", match);
                Check(session.CascadePulse == 0, "아이템 새 연쇄의 같은 라운드 중복 강조 없음");
            }
            finally { if (root != null) UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(level); if (cascadeLevel != null) UnityEngine.Object.DestroyImmediate(cascadeLevel); }
        }
    }
}
