using System;
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

namespace Elements.Editor
{
    /// <summary>등록 내구도 조회와 실제 교환 적용의 집계 정책 전달을 검사한다.</summary>
    public static class ElementDurabilityApplyPolicyVerification
    {
        private const string Evidence = "Logs/ElementFramework/Stage35";
        private static readonly List<string> Results = new List<string>();
        private static readonly List<string> Values = new List<string>();
        private static readonly List<string> Connections = new List<string>();
        private static object Invoke(Type type, string method, params object[] args) => type.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
        private static string Snapshot(object value) => (string)Invoke(typeof(LevelInitialStateVerification), "Snapshot", value);
        private static string Context(TurnEffectContext value) => (string)Invoke(typeof(DamageAggregationPolicyVerification), "Context", value);
        private static void Check(bool pass, string name) { if (!pass) throw new InvalidOperationException(name); Results.Add("PASS " + name); }
        [Serializable] private sealed class Row { public string name, input, output; }
        private static void Record(string name, string input, string output) => Connections.Add(JsonUtility.ToJson(new Row { name = name, input = input, output = output }));
        private static void NormalChecks()
        {
            Type previous = typeof(ElementReactionApplyVerification);
            foreach (string field in new[] { "Results", "Values", "Connections" })
                ((List<string>)previous.GetField(field, BindingFlags.Static | BindingFlags.NonPublic).GetValue(null)).Clear();
            Invoke(previous, "Inherit", "ExistingChecks");
            Invoke(previous, "AllPlay", false);
            Results.AddRange((List<string>)previous.GetField("Results", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null));
            Values.AddRange((List<string>)previous.GetField("Values", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null));
        }
        private static void PolicyCase(bool? perHitCell, bool applyMissing = false)
        {
            var definitions = (Dictionary<ElementId, ElementDefinition>)Invoke(typeof(CapsuleMagnetPolicyVerification), "Definitions");
            ElementDefinition original = LegacyElementDefinitions.Get(ObstacleKind.Generator);
            LevelDefinition level = ScriptableObject.CreateInstance<LevelDefinition>();
            JsonUtility.FromJsonOverwrite(File.ReadAllText(Evidence + "/play-input-Generator-3.json"), level);
            try
            {
                Check(LevelDefinitionValidator.Validate(level).Count == 0, "유효 원본 발전기 입력");
                LevelRuntimeState state = (LevelRuntimeState)Invoke(typeof(FixedObstacleVerification), "Build", level, 12345);
                var target = new BoardCoordinate(4, 4);
                RuntimeCell cell = state.CellAt(target);
                int index = cell.ObstacleIndex.Value;
                BoardActionExecutor executor = new BoardActionExecutor(state);
                Check(state.Obstacles[index].Durability == 1, "원래 내구도1 유지");
                ElementDefinition replacement = new ElementDefinition(original.Id, original.DisplayName,
                    LegacyElementDefinitions.Get(ObstacleKind.Appliance).Placement, null, original.DamageSourcePolicy,
                    original.ColorMatchPolicy, perHitCell.HasValue ? new ElementDamageAggregationPolicy(perHitCell.Value) : null,
                    original.RemovalMissionProfile, ElementReactionBehavior.Durability);
                definitions[original.Id] = replacement;
                string input = JsonUtility.ToJson(level), random = JsonUtility.ToJson(UnityEngine.Random.state), rules = Snapshot(state.Random);
                string before = Snapshot(state), turn = Context(executor.TurnEffects);
                if (!perHitCell.HasValue)
                {
                    Exception error = null;
                    try
                    {
                        if (applyMissing)
                        {
                            Type type = typeof(DamageReaction).Assembly.GetType("Simulation.ObstacleDamageRules");
                            object registry = type.GetField("reactionBehaviors", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                            registry.GetType().GetMethod("Apply", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(registry, new object[] {replacement, state, cell, executor.TurnEffects, 7});
                        }
                        else DamageReaction.Evaluate(state, target, DamageCause.Power, new BoardCoordinate(4, 0), executor.TurnEffects, null, 7);
                    }
                    catch (TargetInvocationException found) {error = found.InnerException;}
                    catch (InvalidOperationException found) {error = found;}
                    Record("missing-aggregation", applyMissing ? "apply" : "query", "error=" + error + ";state=" + Snapshot(state) + ";context=" + Context(executor.TurnEffects));
                    Check(error is InvalidOperationException && error.Message.Contains(original.Id.Value), "필수 집계 정책 누락 ID 오류 " + applyMissing);
                    Check(before == Snapshot(state) && turn == Context(executor.TurnEffects), "누락 정책 상태/비공개 문맥 무변경 " + applyMissing);
                }
                else
                {
                    DamageReaction query = DamageReaction.Evaluate(state, target, DamageCause.Power, new BoardCoordinate(4, 0), executor.TurnEffects, null, 7);
                    Check(query.Response == DamageResponse.Damage && before == Snapshot(state) && turn == Context(executor.TurnEffects), "등록 조회 피해/읽기 전용 " + perHitCell);
                    BoardActionResult result = executor.Swap(new BoardCoordinate(4, 0), new BoardCoordinate(4, 1));
                    Record("aggregation-public-swap", perHitCell.ToString(), "initial=" + before + ";state=" + Snapshot(executor.State) + ";result=" + Snapshot(result) + ";context=" + Context(executor.TurnEffects));
                    Check(result.IsApplied && result.Effects.Any(effect => effect.Content == RuntimeContent.Rocket && effect.Response == DamageResponse.Activate), "실제 public Swap 로켓 발동 " + perHitCell);
                    var damage = result.Effects.Single(effect => effect.Target.Equals(target) && effect.Response == DamageResponse.Damage);
                    Check(executor.State.Obstacles[index].Durability == 0 && !executor.State.Cells.Any(occupied => occupied.ObstacleIndex == index), "실제 내구도 감소/전체 점유칸 제거 " + perHitCell);
                    Check(executor.State.Obstacles[index].Charge == 0 && !executor.TurnEffects.HasCharged(index), "내구도 행동 충전 없음 " + perHitCell);
                    Check(damage.DurabilityBefore == 1 && damage.DurabilityAfter == 0 && damage.RemovedObstacleIndices.SequenceEqual(new[] {index}), "실제 피해 효과/제거 본체 기록 " + perHitCell);
                    int linked = state.CellAt(new BoardCoordinate(4, 7)).ObstacleIndex.Value;
                    Check(executor.State.Obstacles[linked].Durability == 7 && executor.State.Missions[0].Progress == 0 && GeneratorRules.ActiveConnections(executor.State).Count == 0 && executor.TurnEffects.Generators.Count == 0, "연결 대상 두 칸 피해/미션 미완료/활성 연결 소멸/충전 사건 없음 " + perHitCell);
                    Check(result.Effects.Where(effect => effect.Response == DamageResponse.Activate && effect.Content == RuntimeContent.Rocket).All(effect => executor.TurnEffects.HasFired(effect.Target) && executor.State.CellAt(effect.Target).Content == RuntimeContent.Empty), "로켓 발동 기록/실제 소비 " + perHitCell);
                    // 실제 효과가 발급한 hit를 사용하며 검사에서 임의로 타격 기록을 넣지 않는다.
                    bool hasHit = (bool)typeof(TurnEffectContext).GetMethod("HasHit", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(executor.TurnEffects, new object[] {damage.HitGroup, target});
                    Check(hasHit == perHitCell.Value && executor.TurnEffects.HasDamaged(index) == !perHitCell.Value, "등록 정책의 hit/좌표 대 본체 기록 " + perHitCell);
                }
                Check(input == JsonUtility.ToJson(level) && random == JsonUtility.ToJson(UnityEngine.Random.state) && rules == Snapshot(executor.State.Random), "원본/규칙·전역 난수 무변경 " + perHitCell + applyMissing);
            }
            finally
            {
                definitions[original.Id] = original;
                UnityEngine.Object.DestroyImmediate(level);
                Check(ReferenceEquals(LegacyElementDefinitions.Get(ObstacleKind.Generator), original), "finally 원래 정의 정확한 참조 복원");
            }
        }
        private static void PolicyChecks(bool red)
        {
            foreach (bool? policy in new bool?[] {true, false, null})
                try {PolicyCase(policy);}
                catch (Exception error) {if (!red) throw; Results.Add("FAIL " + policy + " " + error); Debug.LogException(error);}
            try {PolicyCase(null, true);}
            catch (Exception error) {if (!red) throw; Results.Add("FAIL missing-apply " + error); Debug.LogException(error);}
        }
        public static void Before() => Execute("before");
        public static void Red() => Execute("red");
        public static void Run() => Execute("after");
        public static void Quick() => Execute("quick");
        private static void Execute(string mode)
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 Editor에서만 실행한다.");
            Directory.CreateDirectory(Evidence); Results.Clear(); Values.Clear(); Connections.Clear(); int exit = 0;
            try
            {
                if (mode == "red" || mode == "quick") PolicyChecks(mode == "red");
                else
                {
                    NormalChecks();
                    if (mode == "before") File.WriteAllLines(Evidence + "/baseline-values.jsonl", Values);
                    else
                    {
                        Check(RecordedLogicComparison.Equal(File.ReadAllLines(Evidence + "/baseline-values.jsonl"), Values), "기존 논리/입력/팩 비교 · 추가 비행 표시 이력 별도");
                        PolicyChecks(false);
                    }
                    File.WriteAllText(Evidence + "/definitions-" + mode + ".json", Snapshot(Enum.GetValues(typeof(ObstacleKind)).Cast<ObstacleKind>().Select(LegacyElementDefinitions.Get).ToArray()));
                }
                if (Results.Any(result => result.StartsWith("FAIL "))) exit = 1;
            }
            catch (Exception error) {Results.Add("FAIL " + error); Debug.LogException(error); exit = 1;}
            finally
            {
                File.WriteAllLines(Evidence + "/" + mode + "-results.txt", Results);
                File.WriteAllLines(Evidence + "/" + mode + "-values.jsonl", Values);
                File.WriteAllLines(Evidence + "/" + mode + "-connection-values.jsonl", Connections);
            }
            EditorApplication.Exit(exit);
        }
    }
}
