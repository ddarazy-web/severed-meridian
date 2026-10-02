using System;
using System.Collections;
using System.Collections.Generic;
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
        private const string Output = "Logs/Stage09/";
        private static readonly List<string> results = new List<string>();
        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); results.Add("PASS " + message); }

        public static void Data()
        {
            Directory.CreateDirectory(Output); results.Clear(); int exit = 0;
            LevelDefinition level = null;
            try
            {
                level = (LevelDefinition)typeof(PowerEffectVerification).GetMethod("Make", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
                JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":0,\"color\":0,\"count\":2},{\"kind\":0,\"color\":0,\"count\":1},{\"kind\":1,\"count\":1}]}", level);
                // 저장 레벨은 중복 미션을 금지한다. 표시 기록의 중복 대응은 런타임 값 구성으로 따로 검증한다.
                LevelRuntimeState state = (LevelRuntimeState)Activator.CreateInstance(typeof(LevelRuntimeState),
                    BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] { level, 12345, "progress-fixture" }, null);
                PropertyInfo records = typeof(LevelRuntimeState).GetProperty("MissionProgressRecords");
                Check(records != null, "실제 미션 증가 기록 제공");
                MethodInfo consume = typeof(MissionProgressRules).GetMethod("ConsumeColor", BindingFlags.NonPublic | BindingFlags.Static);
                BoardCoordinate source = new BoardCoordinate(2, 3);
                consume.Invoke(null, new object[] { state, (RabbitColor?)RabbitColor.Type1, (BoardCoordinate?)source });
                object[] first = ((IEnumerable)records.GetValue(state)).Cast<object>().ToArray();
                Check(first.Length == 2 && state.Missions[0].Progress == 1 && state.Missions[1].Progress == 1,
                    "중복 색 미션 각각의 실제 증가 기록");
                Check(first.Select(record => (int)record.GetType().GetProperty("MissionIndex").GetValue(record)).SequenceEqual(new[] { 0, 1 }), "미션 인덱스 유지");
                Check(first.All(record => (int)record.GetType().GetProperty("Amount").GetValue(record) == 1 &&
                    ((BoardCoordinate)record.GetType().GetProperty("Source").GetValue(record)).Equals(source)), "실제 증가량과 소비 원점 유지");
                consume.Invoke(null, new object[] { state, (RabbitColor?)RabbitColor.Type1, (BoardCoordinate?)source });
                consume.Invoke(null, new object[] { state, (RabbitColor?)RabbitColor.Type1, (BoardCoordinate?)source });
                consume.Invoke(null, new object[] { state, (RabbitColor?)null, (BoardCoordinate?)source });
                Check(((IEnumerable)records.GetValue(state)).Cast<object>().Count() == 3 && state.Missions[0].Progress == 2,
                    "포화 미션과 색 없는 소비는 추가 기록 없음");
                LevelRuntimeState copy = (LevelRuntimeState)Activator.CreateInstance(typeof(LevelRuntimeState), BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] { state }, null);
                MethodInfo complete = typeof(MissionProgressRules).GetMethod("Complete", BindingFlags.NonPublic | BindingFlags.Static);
                complete.Invoke(null, new object[] { copy, MissionKind.Crate, (BoardCoordinate?)source, (int?)4 });
                object last = ((IEnumerable)records.GetValue(copy)).Cast<object>().Last();
                Check(((IEnumerable)records.GetValue(copy)).Cast<object>().Count() == 4 && ((IEnumerable)records.GetValue(state)).Cast<object>().Count() == 3,
                    "작업 사본의 기록은 원본과 독립 누적");
                Check((int)last.GetType().GetProperty("BodyIndex").GetValue(last) == 4 && copy.Missions[2].Progress == 1,
                    "장애물 완료의 실제 본체 인덱스 유지");
                foreach (MissionKind kind in Enum.GetValues(typeof(MissionKind)))
                {
                    JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":" + (int)kind + ",\"color\":0,\"count\":1}]}", level);
                    LevelRuntimeState one = (LevelRuntimeState)Activator.CreateInstance(typeof(LevelRuntimeState),
                        BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] { level, 12345, "progress-kind" }, null);
                    typeof(RuntimeMission).GetProperty("Target").SetValue(one.Missions[0], 1);
                    complete.Invoke(null, new object[] { one, kind, (BoardCoordinate?)source, (int?)null });
                    complete.Invoke(null, new object[] { one, kind, (BoardCoordinate?)source, (int?)null });
                    Check(one.Missions[0].Progress == 1 && ((IEnumerable)records.GetValue(one)).Cast<object>().Count() == 1,
                        kind + " 실제 종류별 증가와 포화 기록");
                }
                Type displayType = typeof(PuzzleGameSession).Assembly.GetType("GameScreen.PuzzleMissionDisplay");
                Check(displayType != null, "미션 표시 진행을 실제 진행과 분리");
                object display = Activator.CreateInstance(displayType);
                displayType.GetMethod("Initialize").Invoke(display, new object[] { state });
                complete.Invoke(null, new object[] { state, MissionKind.Crate, (BoardCoordinate?)source, (int?)4 });
                object[] pending = ((IEnumerable)displayType.GetMethod("Collect").Invoke(display, new object[] { state })).Cast<object>().ToArray();
                Check(pending.Length == 1 && (int)displayType.GetMethod("Progress").Invoke(display, new object[] { 2 }) == 0,
                    "규칙 완료 후에도 도착 전 표시 수치는 증가하지 않음");
                Check(((IEnumerable)displayType.GetMethod("Collect").Invoke(display, new object[] { state })).Cast<object>().Count() == 0,
                    "동일 기록 재수집 금지");
                displayType.GetMethod("Arrive").Invoke(display, new object[] { 2, 1 });
                Check((int)displayType.GetMethod("Progress").Invoke(display, new object[] { 2 }) == 1,
                    "도착 시 실제 증가량만 표시 반영");
                foreach (RocketDirection direction in Enum.GetValues(typeof(RocketDirection)))
                {
                    JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":0,\"color\":0,\"count\":2}]}", level);
                    BoardCoordinate origin = new BoardCoordinate(4, 4);
                    typeof(PowerEffectVerification).GetMethod("Place", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null,
                        new object[] { level, origin, InitialBlockKind.Rocket, direction, RabbitColor.Type1 });
                    LevelStateBuildResult built = LevelStateBuilder.Build(level, 12345);
                    Check(built.IsBuilt, direction + " 실제 파워 fixture 구성");
                    BoardActionExecutor executor = new BoardActionExecutor(built.State);
                    BoardActionResult action = executor.Activate(origin);
                    Check(action.IsApplied, direction + " 실제 로켓 발동");
                    var actual = ((IEnumerable)records.GetValue(executor.State)).Cast<object>().ToArray();
                    Check(actual.Length == executor.State.Missions[0].Progress && actual.Length > 0,
                        direction + " 실행기가 반환한 실제 진행과 기록 수 일치");
                    Check(actual.All(record =>
                    {
                        BoardCoordinate cell = (BoardCoordinate)record.GetType().GetProperty("Source").GetValue(record);
                        return built.State.CellAt(cell).Color == RabbitColor.Type1 &&
                            (direction == RocketDirection.Horizontal ? cell.Row == 4 : cell.Column == 4);
                    }), direction + " 실제 공격 범위의 소비 칸만 원점으로 기록");
                    Check(built.State.Missions[0].Progress == 0 && ((IEnumerable)records.GetValue(built.State)).Cast<object>().Count() == 0,
                        direction + " 실행 전 원본 상태 보존");
                }
                FeedbackChecks(level);
                MissionPathChecks();
                ReviewWebMatch();
                ReviewItemCascade();
            }
            catch (Exception error) { exit = 1; results.Add("FAIL " + error); Debug.LogException(error); }
            finally { if (level != null) UnityEngine.Object.DestroyImmediate(level); }
            File.WriteAllLines(Output + "data-results.txt", results);
            if (Application.isBatchMode) EditorApplication.Exit(exit);
        }
    }
}
