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
        private const string BaselineEvidence = "Logs/ElementFramework/Stage07";
        private static readonly List<string> Observations = new List<string>();

        public static void Baseline()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 Editor에서만 실행한다.");
            Directory.CreateDirectory(BaselineEvidence); Results.Clear(); Observations.Clear();
            try
            {
                ObserveInitial(); ObservePower(); ObserveFixed(); ObserveMaintain(); ObserveNeed(); ObserveFailure();
                File.WriteAllLines(BaselineEvidence + "/baseline-results.txt", Results);
                File.WriteAllLines(BaselineEvidence + "/recovery-observations.jsonl", Observations);
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Results.Add("FAIL " + error); File.WriteAllLines(BaselineEvidence + "/baseline-results.txt", Results);
                File.WriteAllLines(BaselineEvidence + "/recovery-observations.jsonl", Observations);
                Debug.LogException(error); EditorApplication.Exit(1);
            }
        }

        private static void ObserveInitial()
        {
            LevelDefinition level = Make(1);
            try
            {
                Place(level, C(8, 0)); LevelRuntimeState raw = Build(level); string before = Snapshot(raw);
                BoardActionExecutor executor = new BoardActionExecutor(raw);
                Record("initial", "new BoardActionExecutor", level, before, executor.State, executor.LastSettlement, executor.TurnEffects);
                Check(raw.Recoveries.Count == 0 && Snapshot(raw) == before && executor.State.Recoveries.Count == 1 &&
                    executor.State.Missions[0].Progress == 1 && executor.State.MovesRemaining == 20, "초기 출구 수집/입력/이동 수 실제 기록");

                LevelRuntimeState duplicate = Build(level);
                // 제작 데이터의 유효성 검사와 별개로 수집 경계의 좌표 중복 제거를 확인한다.
                typeof(RuntimeFlow).GetField("<Arrivals>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(duplicate.Flow, Array.AsReadOnly(new[] { C(8, 0), C(8, 0) }));
                before = Snapshot(duplicate);
                int first = (int)Invoke(typeof(RecoveryRules), "Collect", duplicate, 7, 3);
                Record("duplicate-arrival", "Collect turn7 batch3; duplicated runtime arrival", level, before, duplicate, null, null);
                int repeat = (int)Invoke(typeof(RecoveryRules), "Collect", duplicate, 7, 4);
                Check(first == 1 && repeat == 0 && duplicate.Recoveries.Count == 1 && duplicate.Missions[0].Progress == 1 &&
                    duplicate.Recoveries[0].Turn == 7 && duplicate.Recoveries[0].Batch == 3, "중복 출구/반복 수집 단일 집계/턴·묶음");
                before = Snapshot(executor.State); SettlementResult again = SettlementResolution.Resolve(executor.State);
                Record("initial-repeat", "Resolve again", level, before, again.State, again, again.TurnEffects);
                Check(again.IsApplied && again.State.Recoveries.Count == 1 && again.State.Missions[0].Progress == 1, "초기 수집 후 정착 중복 없음");
            }
            finally { UnityEngine.Object.DestroyImmediate(level); }
        }

        private static void ObservePower()
        {
            foreach (InitialBlockKind power in new[] { InitialBlockKind.Rocket, InitialBlockKind.Bomb })
            {
                LevelDefinition level = Make(1);
                try
                {
                    Place(level, C(4, 4));
                    Invoke(typeof(PowerEffectVerification), "Place", level, C(4, 3), power, RocketDirection.Horizontal, RabbitColor.Type1);
                    LevelRuntimeState state = Build(level); string before = Snapshot(state);
                    bool inRange = PowerEffectResolution.Range(state, C(4, 3)).Contains(C(4, 4));
                    TurnEffectContext context = (TurnEffectContext)Invoke(typeof(TargetPowerVerification), "Context");
                    List<EffectRecord> hits = (List<EffectRecord>)typeof(LayerVerification).GetMethod("Hit", BindingFlags.NonPublic | BindingFlags.Static)
                        .Invoke(null, new object[] { state, C(4, 3), context });
                    Record("power-" + power, "actual Apply at (4,3)", level, before, state, null, context, hits);
                    // 무반응 타격은 기록에서 생략되므로 적용 범위와 실제 상태를 함께 검사한다.
                    Check(inRange && hits.Any(h => h.Response == DamageResponse.Activate) &&
                        hits.Any(h => h.Response == DamageResponse.Remove) && hits.All(h => !h.Target.Equals(C(4, 4))) &&
                        state.CellAt(C(4, 4)).Content == RuntimeContent.Recovery && state.Missions[0].Progress == 0 && state.Recoveries.Count == 0,
                        "실제 파워 범위 타격 비파괴/미수집 " + power);
                }
                finally { UnityEngine.Object.DestroyImmediate(level); }
            }
        }

        private static void ObserveFixed()
        {
            LevelDefinition level = Make(2);
            try
            {
                Invoke(typeof(SettlementVerification), "Source", level, C(0, 0), SupplyExhaustion.Stop,
                    new[] { new SupplyItem(SupplyKind.Recovery, 3), new SupplyItem(SupplyKind.Bomb) });
                LevelRuntimeState input = Build(level); Empty(input, input.Cells.Select(c => c.Coordinate).ToArray()); string before = Snapshot(input);
                SettlementResult result = SettlementResolution.Resolve(input);
                Record("fixed", "empty board; Resolve Recovery3 then Bomb", level, before, result.State, result, result.TurnEffects);
                Check(result.IsApplied && Snapshot(input) == before && result.State.Recoveries.Count == 3 && result.State.Missions[0].Progress == 2,
                    "고정 공급 목표 초과3개 수집/미션 상한2/원본 보존");
                SettlementRecord[] supplied = result.Records.Where(r => r.Kind == MovementKind.Supply).ToArray();
                Check(supplied.Select(r => r.Content).SequenceEqual(new[] { RuntimeContent.Recovery, RuntimeContent.Recovery, RuntimeContent.Recovery, RuntimeContent.Bomb }) &&
                    supplied.Select(r => r.ItemAfter).SequenceEqual(new[] { 0, 0, 1, 2 }) &&
                    supplied.Select(r => r.ConsumedAfter).SequenceEqual(new[] { 1, 2, 0, 0 }), "고정 목록 실제 공급 순서/개별 소비 커서");
                Check(result.State.Recoveries.All(r => r.Coordinate.Equals(C(8, 0))) &&
                    result.State.Recoveries.Select(r => r.Batch).Distinct().Count() == 3, "한 정착 내 같은 출구의 개별 수집 묶음");
                LevelRuntimeState state = result.State; Empty(state, new[] { C(8, 0) }); before = Snapshot(state);
                SettlementResult stop = SettlementResolution.Resolve(state);
                Record("fixed-stop", "clear fixture Bomb; Resolve exhausted Stop", level, before, stop.State, stop, stop.TurnEffects);
                Check(stop.IsApplied && stop.Records.Count == 0 && stop.State.Supply.Sources[0].ItemIndex == 2 && stop.State.Recoveries.Count == 3,
                    "고정 Stop 추가 공급/중복 수집 없음");
            }
            finally { UnityEngine.Object.DestroyImmediate(level); }
        }

        private static void ObserveMaintain()
        {
            LevelDefinition level = Sparse(C(0, 0), C(1, 0), C(2, 0));
            try
            {
                JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":" + (int)MissionKind.Recovery + ",\"count\":4}]}", level);
                LevelFlowEditing.SetArrival(level, C(2, 0), false); LevelSupplyEditing.PlaceSources(level, new[] { C(0, 0) });
                LevelSupplyEditing.SetSourceProperty(level, new[] { 0 }, "mode", (int)SupplyMode.MaintainRecovery);
                using (SerializedObject data = new SerializedObject(level))
                { data.FindProperty("supply.recoveryTarget").intValue = 2; data.ApplyModifiedPropertiesWithoutUndo(); }
                LevelRuntimeState state = Build(level); Empty(state, state.Cells.Where(c => c.IsActive).Select(c => c.Coordinate).ToArray()); string before = Snapshot(state);
                SettlementResult result = SettlementResolution.Resolve(state);
                Record("maintain-collect", "empty 3-cell column; Resolve target2 mission4", level, before, result.State, result, result.TurnEffects);
                Check(result.IsApplied && Snapshot(state) == before && result.State.Recoveries.Count == 4 && result.State.Missions[0].Progress == 4 &&
                    RecoveryRules.OnBoard(result.State) == 0 && RecoveryRules.Needed(result.State) == 0, "수집 후 부족량 재계산/남은 미션4개 완료/과잉 보충 없음");
                Check(result.Records.Count(r => r.Kind == MovementKind.Supply && r.Content == RuntimeContent.Recovery) == 4 &&
                    result.Records.Count(r => r.Kind == MovementKind.Supply && r.Content == RuntimeContent.Normal) == 3 && result.State.Random.DrawCount == 3,
                    "완료 후 일반3개 공급/난수3/회수4개 공급");
                Check(result.State.Recoveries.Select(r => r.Batch).SequenceEqual(result.State.Recoveries.Select(r => r.Batch).OrderBy(b => b)) &&
                    result.State.Recoveries.Select(r => r.Batch).Distinct().Count() == 4, "수집 순서/묶음 실제 기록");
                state = result.State; Empty(state, state.Cells.Where(c => c.IsActive).Select(c => c.Coordinate).ToArray()); before = Snapshot(state);
                SettlementResult completed = SettlementResolution.Resolve(state);
                Record("maintain-completed", "empty after mission complete; Resolve", level, before, completed.State, completed, completed.TurnEffects);
                Check(completed.IsApplied && completed.State.Recoveries.Count == 4 && completed.Records.All(r => r.Content != RuntimeContent.Recovery) &&
                    completed.RandomAfter == completed.RandomBefore + 3, "완료 미션 보충 중단/일반 난수만 소비");
            }
            finally { UnityEngine.Object.DestroyImmediate(level); }
        }

        private static void ObserveNeed()
        {
            LevelDefinition level = Make(4);
            try
            {
                Place(level, C(4, 4)); Place(level, C(5, 5)); LevelSupplyEditing.PlaceSources(level, new[] { C(0, 0) });
                LevelSupplyEditing.SetSourceProperty(level, new[] { 0 }, "mode", (int)SupplyMode.MaintainRecovery);
                using (SerializedObject data = new SerializedObject(level))
                { data.FindProperty("supply.recoveryTarget").intValue = 3; data.ApplyModifiedPropertiesWithoutUndo(); }
                LevelRuntimeState state = Build(level);
                for (int progress = 0; progress <= 4; progress++)
                {
                    Set(state.Missions[0], "Progress", progress); string before = Snapshot(state);
                    int needed = RecoveryRules.Needed(state);
                    Record("needed", "set fixture progress=" + progress + "; Needed", level, before, state, null, null);
                    Check(needed == (progress <= 1 ? 1 : 0) && Snapshot(state) == before && RecoveryRules.OnBoard(state) == 2,
                        "min(목표3,남은 미션)-보드2/읽기 전용 " + progress);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(level); }
        }

        private static void ObserveFailure()
        {
            foreach (bool maintain in new[] { false, true })
            {
                LevelDefinition level = Sparse(C(0, 0), C(1, 1), C(2, 1), C(8, 8));
                try
                {
                    LevelFlowEditing.SetPortal(level, C(2, 1), C(0, 0));
                    if (maintain)
                    {
                        JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":" + (int)MissionKind.Recovery + ",\"count\":1}]}", level);
                        LevelFlowEditing.SetArrival(level, C(8, 8), false);
                        string error = LevelSupplyEditing.PlaceSources(level, new[] { C(0, 0) });
                        if (error == null) error = LevelSupplyEditing.SetSourceProperty(level, new[] { 0 }, "mode", (int)SupplyMode.MaintainRecovery);
                        if (error != null) throw new InvalidOperationException(error);
                        using (SerializedObject data = new SerializedObject(level))
                        { data.FindProperty("supply.recoveryTarget").intValue = 1; data.ApplyModifiedPropertiesWithoutUndo(); }
                    }
                    else Invoke(typeof(SettlementVerification), "Source", level, C(0, 0), SupplyExhaustion.Stop, new[] { new SupplyItem(SupplyKind.Recovery) });
                    LevelRuntimeState state = Build(level); Empty(state, state.Cells.Where(c => c.IsActive).Select(c => c.Coordinate).ToArray()); string before = Snapshot(state);
                    SettlementResult result = SettlementResolution.Resolve(state);
                    Record("failure-" + maintain, "fresh diagonal/portal cycle; Resolve", level, before, state, result, null);
                    Check(result.Reason == SettlementReason.Repeating && result.State == null && Snapshot(state) == before &&
                        state.Recoveries.Count == 0 && state.Supply.Sources[0].ItemIndex == 0 && state.Random.DrawCount == 0,
                        "고정/유지 순환 실패 원본·커서·난수·수집 보존 " + maintain);
                }
                finally { UnityEngine.Object.DestroyImmediate(level); }
            }
        }

        [Serializable]
        private sealed class RecoveryObservation
        {
            public string name, input, levelJson, beforeProperties, afterProperties, contextProperties, recordProperties, settlementReason;
            public int seed, remaining, onBoard, needed, randomDraws;
            public int[] missionProgress, sourceIndices, sourceConsumed, recoveryTurns, recoveryBatches;
            public string[] occupiedCoordinates, recoveryCoordinates;
        }

        private static void Record(string name, string input, LevelDefinition level, string before, LevelRuntimeState state,
            SettlementResult result, TurnEffectContext context, object effects = null)
        {
            Observations.Add(JsonUtility.ToJson(new RecoveryObservation
            {
                name = name, input = input, seed = 12345, levelJson = JsonUtility.ToJson(level), beforeProperties = before,
                afterProperties = Snapshot(state), contextProperties = context == null ? null : Snapshot(context),
                recordProperties = Snapshot(effects ?? (object)result?.Records), settlementReason = result?.Reason.ToString(),
                remaining = RecoveryRules.Remaining(state), onBoard = RecoveryRules.OnBoard(state), needed = RecoveryRules.Needed(state), randomDraws = state.Random.DrawCount,
                missionProgress = state.Missions.Select(m => m.Progress).ToArray(), sourceIndices = state.Supply.Sources.Select(s => s.ItemIndex).ToArray(),
                sourceConsumed = state.Supply.Sources.Select(s => s.ItemConsumed).ToArray(),
                occupiedCoordinates = state.Cells.Where(c => c.Content == RuntimeContent.Recovery).Select(c => c.Coordinate.Row + "," + c.Coordinate.Column).ToArray(),
                recoveryCoordinates = state.Recoveries.Select(r => r.Coordinate.Row + "," + r.Coordinate.Column).ToArray(),
                recoveryTurns = state.Recoveries.Select(r => r.Turn).ToArray(), recoveryBatches = state.Recoveries.Select(r => r.Batch).ToArray()
            }));
        }
    }
}
