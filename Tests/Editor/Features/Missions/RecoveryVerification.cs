using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Board;
using Simulation;
using UnityEditor;
using UnityEngine;

namespace Levels.Editor
{
    public static partial class RecoveryVerification
    {
        private const string Evidence = "Logs/RecoveryVerification";
        private static readonly List<string> Results = new List<string>();
        private static BoardCoordinate C(int row, int column) => new BoardCoordinate(row, column);
        private static object Invoke(Type type, string method, params object[] args) =>
            type.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, args);
        private static void Set(object owner, string name, object value) => owner.GetType().GetProperty(name).GetSetMethod(true).Invoke(owner, new[] { value });
        private static void Check(bool condition, string name) { if (!condition) throw new InvalidOperationException(name); Results.Add("PASS " + name); }
        private static LevelDefinition Make(int target)
        {
            LevelDefinition level = (LevelDefinition)Invoke(typeof(PowerEffectVerification), "Make");
            JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":" + (int)MissionKind.Recovery + ",\"count\":" + target + "}]}", level);
            string error = LevelFlowEditing.SetArrival(level, C(8, 0), false);
            if (error != null) throw new InvalidOperationException(error);
            return level;
        }
        private static void Place(LevelDefinition level, BoardCoordinate cell)
        {
            LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Block, Erase = true }, new[] { cell });
            string error = LevelSupplyEditing.PlaceRecovery(level, new[] { cell });
            if (error != null) throw new InvalidOperationException(error);
        }
        private static LevelRuntimeState Build(LevelDefinition level, int seed = 12345)
        {
            LevelStateBuildResult result = LevelStateBuilder.Build(level, seed);
            if (!result.IsBuilt) throw new InvalidOperationException(string.Join(" | ", result.Issues));
            return result.State;
        }
        private static void Empty(LevelRuntimeState state, IEnumerable<BoardCoordinate> cells)
        {
            foreach (BoardCoordinate coordinate in cells)
            { RuntimeCell cell = state.CellAt(coordinate); Set(cell, "Content", RuntimeContent.Empty); Set(cell, "Color", null); Set(cell, "RocketDirection", null); Set(cell, "ObstacleIndex", null); }
        }
        public static void Data()
        {
            Directory.CreateDirectory(Evidence); Results.Clear();
            try { DataChecks(); File.WriteAllLines(Evidence + "/data-results.txt", Results); EditorApplication.Exit(0); }
            catch (Exception error) { Results.Add("FAIL " + error); File.WriteAllLines(Evidence + "/data-results.txt", Results); Debug.LogException(error); EditorApplication.Exit(1); }
        }
        private static void DataChecks()
        {
            LevelDefinition initial = Make(1); Place(initial, C(8, 0));
            LevelRuntimeState raw = Build(initial); BoardActionExecutor executor = new BoardActionExecutor(raw);
            Check(raw.Recoveries.Count == 0 && raw.CellAt(C(8, 0)).Content == RuntimeContent.Recovery, "읽기 전용 초기 구성 보존");
            Check(executor.State.Recoveries.Count == 1 && executor.State.Missions[0].Progress == 1 && executor.State.MovesRemaining == 20, "실행 초기 회수·미션·이동 수");
            SettlementResult initialized = executor.LastSettlement;
            Check(initialized.IsApplied && initialized.State.Recoveries.Count == 1, "초기 회수 후 정착 중복 없음");
            UnityEngine.Object.DestroyImmediate(initial);

            LevelDefinition falling = Make(2); Place(falling, C(1, 0)); Place(falling, C(2, 0));
            LevelRuntimeState state = Build(falling);
            Empty(state, state.Cells.Where(c => c.Content == RuntimeContent.Normal).Select(c => c.Coordinate).ToArray());
            SettlementResult settled = SettlementResolution.Resolve(state);
            Check(settled.IsApplied && settled.State.Recoveries.Count == 2 && settled.State.Missions[0].Progress == 2 && RecoveryRules.OnBoard(settled.State) == 0, "연속 낙하·도착 즉시 회수");
            Check(state.Recoveries.Count == 0 && RecoveryRules.OnBoard(state) == 2, "정착 사본 독립");
            Check(SettlementResolution.Resolve(settled.State).State.Recoveries.Count == 2, "반복 정착 중복 집계 없음");
            UnityEngine.Object.DestroyImmediate(falling);

            foreach (int seed in Enumerable.Range(1, 12))
            {
                LevelDefinition level = Make(2); Place(level, C(8, 8));
                BoardCoordinate[] sources = { C(0, 0), C(0, 2), C(0, 4) };
                string error = LevelSupplyEditing.PlaceSources(level, sources);
                if (error == null) error = LevelSupplyEditing.SetSourceProperty(level, new[] { 0, 1, 2 }, "mode", (int)SupplyMode.MaintainRecovery);
                if (error != null) throw new InvalidOperationException(error);
                SerializedObject data = new SerializedObject(level); data.FindProperty("supply.recoveryTarget").intValue = 3; data.ApplyModifiedPropertiesWithoutUndo();
                LevelRuntimeState input = Build(level, seed); Empty(input, sources);
                SettlementResult result = SettlementResolution.Resolve(input);
                Check(result.IsApplied && RecoveryRules.OnBoard(result.State) == 2 && RecoveryRules.Needed(result.State) == 0, "남은 목표 제한·다중 생성구 부족분1 " + seed);
                Check(result.Records.Count(r => r.Kind == MovementKind.Supply && r.Content == RuntimeContent.Recovery) == 1 && result.Records.Count(r => r.Kind == MovementKind.Supply) == 3, "회수 우선·나머지 일반 공급 " + seed);
                Check(SettlementResolution.Resolve(input).State.Cells.Select(c => c.Content).SequenceEqual(result.State.Cells.Select(c => c.Content)), "유지 공급 시드 재현 " + seed);
                Check(SettlementResolution.Resolve(result.State).RandomAfter == result.RandomAfter, "막힌 생성구 무난수 " + seed);
                Set(input.Missions[0], "Progress", 2);
                SettlementResult completed = SettlementResolution.Resolve(input);
                Check(RecoveryRules.OnBoard(completed.State) == 1 && completed.Records.All(r => r.Content != RuntimeContent.Recovery), "목표0 일반 공급·기존 부품 보존 " + seed);
                UnityEngine.Object.DestroyImmediate(level);
            }
            LevelDefinition fixedLevel = Make(2);
            Invoke(typeof(SettlementVerification), "Source", fixedLevel, C(0, 0), SupplyExhaustion.Stop,
                new[] { new SupplyItem(SupplyKind.Recovery, 3), new SupplyItem(SupplyKind.Bomb) });
            LevelRuntimeState fixedState = Build(fixedLevel);
            Empty(fixedState, fixedState.Cells.Select(c => c.Coordinate).ToArray());
            SettlementResult fixedResult = SettlementResolution.Resolve(fixedState);
            Check(fixedResult.IsApplied && fixedResult.State.Recoveries.Count == 3 && fixedResult.State.Missions[0].Progress == 2, "고정 목록 목표 초과도 순서대로 공급·집계 상한");
            Check(fixedResult.State.Supply.Sources[0].ItemIndex == 2 && fixedResult.State.CellAt(C(8, 0)).Content == RuntimeContent.Bomb, "목록 소진 Stop·일반 파워 도착 유지");
            UnityEngine.Object.DestroyImmediate(fixedLevel);
            TargetChecks();
        }

        private static void TargetChecks()
        {
            LevelDefinition level = Make(1); Place(level, C(4, 4));
            Invoke(typeof(PowerEffectVerification), "Crate", level, C(5, 4), 1);
            LevelRuntimeState state = Build(level);
            TurnEffectContext context = (TurnEffectContext)Invoke(typeof(TargetPowerVerification), "Context");
            MissionContribution contribution = MissionProgressRules.Query(state, C(5, 4), context).Single();
            Check(contribution.ExpectedComplete == 0 && contribution.Damage == 1, "직접 낙하 방해 상자 간접 기여·완료 예측0");
            Check(!MissionProgressRules.Query(state, C(4, 4), context).Any() &&
                DamageReaction.Evaluate(state, C(4, 4), DamageCause.Power, C(4, 4), context).Response == DamageResponse.None, "부품 자체 비파괴·미션 타겟 제외");
            DroneTargetManager manager = (DroneTargetManager)Activator.CreateInstance(typeof(DroneTargetManager), BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] { state, context }, null);
            DroneTarget target = manager.Query().First(t => t.Coordinate.Equals(C(5, 4)));
            Check(target.IsMission && target.Contributions.Single().ExpectedComplete == 0, "본체 투영도 회수 완료를 과대 계산하지 않음");
            int request = (int)typeof(DroneTargetManager).GetMethod("Request", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(manager, new object[] { C(0, 0) });
            Check(manager.ExpectedComplete == 0 && state.Missions[0].Progress == 0, "드론 예약은 실제 회수/예상 완료 미증가");
            Check(manager.Query().All(t => !t.Coordinate.Equals(context.Targeting.Last(t => t.Event == TargetingEvent.Reserved).Target.Value)), "예약 방해 요소 중복 조준 제외");
            Empty(state, new[] { C(4, 4) });
            typeof(DroneTargetManager).GetMethod("Invalidate", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(manager, null);
            Check(manager.Query().All(t => !t.IsMission), "부품 이동·소실 후 간접 후보 갱신");
            DroneTarget landed = (DroneTarget)typeof(DroneTargetManager).GetMethod("Land", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(manager, new object[] { request, C(0, 0) });
            Check(landed.Content == RuntimeContent.Normal && !landed.IsMission && manager.ReservationCount == 0, "미션 재검색 후 일반 대체·예약 해제");
            UnityEngine.Object.DestroyImmediate(level);
        }
    }
}
