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

namespace Elements.Editor
{
    /// <summary>실제 교환의 등록 적용과 전체 실행 전후 상태를 검사한다.</summary>
    public static class ElementReactionApplyVerification
    {
        private const string Evidence = "Logs/ElementFramework/Stage34";
        private static readonly List<string> Results = new List<string>();
        private static readonly List<string> Values = new List<string>();
        private static readonly List<string> Connections = new List<string>();
        private static object Invoke(Type type, string method, params object[] args) => type.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
        private static string Snapshot(object value) => (string)Invoke(typeof(LevelInitialStateVerification), "Snapshot", value);
        private static string Context(TurnEffectContext value) => (string)Invoke(typeof(DamageAggregationPolicyVerification), "Context", value);
        private static void Check(bool pass, string name) { if (!pass) throw new InvalidOperationException(name); Results.Add("PASS " + name); }
        [Serializable] private sealed class Row { public string name, input, output; }
        private static string RowText(string name, string input, string output) => JsonUtility.ToJson(new Row { name = name, input = input, output = output });
        private static void Inherit(string method)
        {
            Type previous = typeof(ElementReactionBehaviorVerification);
            foreach (string field in new[] { "Results", "Values", "Connections" })
                ((List<string>)previous.GetField(field, BindingFlags.Static | BindingFlags.NonPublic).GetValue(null)).Clear();
            Invoke(previous, method);
            Results.AddRange((List<string>)previous.GetField("Results", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null));
            Values.AddRange((List<string>)previous.GetField("Values", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null));
            Connections.AddRange((List<string>)previous.GetField("Connections", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null));
        }
        private static object Registry()
        {
            Type rules = typeof(DamageReaction).Assembly.GetType("Simulation.ObstacleDamageRules");
            return rules.GetField("reactionBehaviors", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        }
        private static IDictionary Entries()
        {
            object registry = Registry();
            return (IDictionary)registry.GetType().GetField("queries", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(registry);
        }
        private static void ApplyContract()
        {
            IDictionary entries = Entries(); object registry = Registry();
            Check(entries.Count == 3, "기존2개와 추가 행동의 등록 짝 정확히3개");
            Check(registry.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly).Length == 0, "공개 등록 수정 API 없음");
            foreach (ElementReactionBehavior key in Enum.GetValues(typeof(ElementReactionBehavior)))
            {
                object entry = entries[key];
                FieldInfo query = entry.GetType().GetField("Query", BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo apply = entry.GetType().GetField("Apply", BindingFlags.Instance | BindingFlags.NonPublic);
                Check(query != null && apply != null && query.IsInitOnly && apply.IsInitOnly && query.GetValue(entry) is Delegate && apply.GetValue(entry) is Delegate, "불변 조회/적용 짝 " + key);
                Check(apply.FieldType.GetMethod("Invoke").ReturnType == typeof(int), "실제 내구도 반환 계약 " + key);
            }
            foreach (ObstacleKind kind in Enum.GetValues(typeof(ObstacleKind)))
            {
                ElementDefinition original = LegacyElementDefinitions.Get(kind);
                LevelDefinition level = ScriptableObject.CreateInstance<LevelDefinition>();
                JsonUtility.FromJsonOverwrite(File.ReadAllText(Evidence + "/reaction-input-" + kind + ".json"), level);
                try
                {
                    LevelRuntimeState state = (LevelRuntimeState)Invoke(typeof(FixedObstacleVerification), "Build", level, 12345);
                    TurnEffectContext context = (TurnEffectContext)Invoke(typeof(FixedObstacleVerification), "Context");
                    RuntimeCell cell = state.CellAt(new BoardCoordinate(4, 4));
                    string initial = Snapshot(state), turn = Context(context), input = JsonUtility.ToJson(level), random = JsonUtility.ToJson(UnityEngine.Random.state);
                    foreach (string mode in new[] { "missing", "unsupported", "profile-missing", "profile-conflict" })
                    {
                        ElementReactionBehavior? key = mode == "missing" ? null : mode == "unsupported" ? (ElementReactionBehavior?)99 : original.ReactionBehavior;
                        ElementPlacementProfile placement = original.Placement;
                        ElementChargePlacementProfile charge = original.ChargePlacement;
                        if (mode == "profile-missing") {placement = null; charge = null;}
                        if (mode == "profile-conflict")
                        {
                            placement = LegacyElementDefinitions.Get(ObstacleKind.Crate).Placement;
                            charge = LegacyElementDefinitions.Get(ObstacleKind.Generator).ChargePlacement;
                        }
                        var definition = new ElementDefinition(original.Id, original.DisplayName, placement, charge, original.DamageSourcePolicy,
                            original.ColorMatchPolicy, original.DamageAggregationPolicy, original.RemovalMissionProfile, key);
                        Exception error = null;
                        try {registry.GetType().GetMethod("Apply", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(registry, new object[] {definition, state, cell, context, 7});}
                        catch (TargetInvocationException found) {error = found.InnerException;}
                        Check(error is InvalidOperationException && error.Message.Contains(original.Id.Value), "적용 누락/미지원/프로필 ID 오류 " + kind + mode);
                        Check(initial == Snapshot(state) && turn == Context(context) && input == JsonUtility.ToJson(level) && random == JsonUtility.ToJson(UnityEngine.Random.state), "잘못된 등록 적용 전 상태/문맥/원본/난수 무변경 " + kind + mode);
                        Connections.Add(RowText("apply-definition-error", kind + "/" + mode, error.Message + ";state=" + Snapshot(state) + ";context=" + Context(context)));
                    }
                }
                finally {UnityEngine.Object.DestroyImmediate(level);}
            }
        }
        // 검사에서만 등록 위임을 감싼다. 실제 원래 적용을 반드시 실행하며 생산 수정 API는 만들지 않는다.
        private sealed class Observation
        {
            internal Delegate Original;
            internal int Calls;
            internal readonly List<int> Bodies = new List<int>();
            internal readonly List<string> States = new List<string>();
            public int Apply(ElementDefinition definition, LevelRuntimeState state, RuntimeCell cell, TurnEffectContext context, int hit)
            {
                string before = Snapshot(state), turn = Context(context);
                int index = cell.ObstacleIndex.Value;
                int result;
                try { result = (int)Original.DynamicInvoke(definition, state, cell, context, hit); }
                catch (TargetInvocationException error) { throw error.InnerException; }
                Calls++;
                Bodies.Add(index);
                States.Add(RowText("actual-registered-apply", definition.Id.Value + "/" + definition.ReactionBehavior + "/hit=" + hit,
                    "before=" + before + ";contextBefore=" + turn + ";returned=" + result + ";after=" + Snapshot(state) + ";contextAfter=" + Context(context)));
                return result;
            }
        }
        private static Dictionary<object, object> Observe(IDictionary entries, Dictionary<object, Observation> observers)
        {
            var originals = new Dictionary<object, object>();
            foreach (ElementReactionBehavior key in Enum.GetValues(typeof(ElementReactionBehavior)))
            {
                object entry = entries[key]; originals.Add(key, entry);
                var observer = new Observation(); observers.Add(key, observer);
                FieldInfo apply = entry.GetType().GetField("Apply", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                // EF-33에는 조회만 있다. 컴파일 오류 대신 실제 게임 결과와 적용 경로 불일치로 RED를 남긴다.
                if (apply == null) continue;
                FieldInfo query = entry.GetType().GetField("Query", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                observer.Original = (Delegate)apply.GetValue(entry);
                Delegate wrapped = Delegate.CreateDelegate(apply.FieldType, observer, typeof(Observation).GetMethod("Apply"));
                entries[key] = Activator.CreateInstance(entry.GetType(), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null, new object[] { query.GetValue(entry), wrapped }, null);
            }
            return originals;
        }
        private static void Play(ObstacleKind kind, int value, int charge, bool observe)
        {
            LevelDefinition level = ScriptableObject.CreateInstance<LevelDefinition>();
            JsonUtility.FromJsonOverwrite(File.ReadAllText(Evidence + "/play-input-" + kind + "-" + value + ".json"), level);
            IDictionary entries = Entries(); var observers = new Dictionary<object, Observation>(); Dictionary<object, object> originals = null;
            try
            {
                Check(LevelDefinitionValidator.Validate(level).Count == 0, "유효 실제 입력 " + kind + value);
                LevelRuntimeState state = (LevelRuntimeState)Invoke(typeof(FixedObstacleVerification), "Build", level, 12345);
                int index = state.CellAt(new BoardCoordinate(4, 4)).ObstacleIndex.Value;
                if (kind == ObstacleKind.Generator) typeof(RuntimeObstacle).GetProperty("Charge").GetSetMethod(true).Invoke(state.Obstacles[index], new object[] { charge });
                BoardActionExecutor executor = new BoardActionExecutor(state);
                string input = JsonUtility.ToJson(level), random = JsonUtility.ToJson(UnityEngine.Random.state), rules = Snapshot(state.Random);
                if (observe) originals = Observe(entries, observers);
                BoardActionResult result = executor.Swap(new BoardCoordinate(4, 0), new BoardCoordinate(4, 1));
                string record = RowText("reaction-apply-public-swap", kind + "/" + value + "/charge=" + charge,
                    "input=" + input + ";state=" + Snapshot(executor.State) + ";result=" + Snapshot(result) + ";context=" + Context(executor.TurnEffects));
                Values.Add(record);
                Check(result.IsApplied && result.Effects.Any(effect => effect.Content == RuntimeContent.Rocket && effect.Response == DamageResponse.Activate), "실제 교환/로켓 발동 " + kind + value + charge);
                int hits;
                if (kind == ObstacleKind.Generator)
                {
                    hits = 1;
                    Check(executor.State.Obstacles[index].Charge == charge + 1 && executor.TurnEffects.HasCharged(index) && result.Effects.Count(effect => effect.Response == DamageResponse.Charge) == 1, "실제 충전/턴 기록/효과 " + value + charge);
                    bool activated = charge + 1 == value;
                    Check(executor.TurnEffects.Generators.Any(recording => recording.Event == GeneratorEvent.Activated) == activated && executor.State.Missions[0].Progress == (activated ? 1 : 0), "실제 임계/연결 철거/제거 미션 " + value + charge);
                }
                else
                {
                    hits = Math.Min(value, kind == ObstacleKind.Appliance ? 2 : 1);
                    int expected = value - hits;
                    Check(executor.State.Obstacles[index].Durability == expected && result.Effects.Count(effect => effect.Response == DamageResponse.Damage) == hits, "실제 내구도/점유칸 피해 효과 " + kind + value);
                    Check(executor.State.Missions[0].Progress == (expected == 0 ? 1 : 0), "실제 본체 제거 미션 " + kind + value);
                }
                Check(input == JsonUtility.ToJson(level) && random == JsonUtility.ToJson(UnityEngine.Random.state) && rules == Snapshot(executor.State.Random), "교환 원본/규칙·전역 난수 보존 " + kind);
                if (observe)
                {
                    foreach (Observation observer in observers.Values) Connections.AddRange(observer.States);
                    ElementReactionBehavior selected = LegacyElementDefinitions.Get(kind).RequireReactionBehavior();
                    int calls = observers[selected].Bodies.Count(body => body == index);
                    Connections.Add(RowText("registered-route", kind.ToString(), "actualBodyCalls=" + calls + ";expected=" + hits + ";allCalls=" + observers.Values.Sum(observer => observer.Calls) + ";actualGame=" + record));
                    Check(calls == hits, "등록된 실제 본체 적용만 정확히 호출 " + kind + " actual=" + calls + " expected=" + hits);
                    Check(observers.Where(pair => !pair.Key.Equals(selected)).All(pair => !pair.Value.Bodies.Contains(index)), "해당 본체에 다른 행동 적용 없음 " + kind);
                    Check(observers.Values.Sum(observer => observer.Calls) == result.Effects.Count(effect => effect.Response == DamageResponse.Damage || effect.Response == DamageResponse.Charge), "전체 실제 피격 응답마다 적용 정확히1회 " + kind);
                }
            }
            finally
            {
                if (originals != null) foreach (var original in originals) entries[original.Key] = original.Value;
                UnityEngine.Object.DestroyImmediate(level);
                if (originals != null) Check(originals.All(pair => ReferenceEquals(entries[pair.Key], pair.Value)), "finally 원래 등록 짝 정확한 참조 복원 " + kind);
            }
        }
        private static void AllPlay(bool observe)
        {
            foreach (ObstacleKind kind in Enum.GetValues(typeof(ObstacleKind)))
            {
                ElementDefinition definition = LegacyElementDefinitions.Get(kind);
                int min = kind == ObstacleKind.Generator ? definition.ChargePlacement.MinRequiredCharge : 1;
                int max = kind == ObstacleKind.Generator ? definition.ChargePlacement.MaxRequiredCharge : definition.Placement.MaxDurability;
                for (int value = min; value <= max; value++)
                    for (int charge = 0; charge < (kind == ObstacleKind.Generator ? value : 1); charge++) Play(kind, value, charge, observe);
            }
        }
        private static void AlternateApply()
        {
            var definitions = (Dictionary<ElementId, ElementDefinition>)Invoke(typeof(CapsuleMagnetPolicyVerification), "Definitions");
            foreach (ObstacleKind kind in new[] {ObstacleKind.Crate, ObstacleKind.Generator})
            {
                ElementDefinition original = LegacyElementDefinitions.Get(kind);
                ElementDefinition durable = LegacyElementDefinitions.Get(ObstacleKind.Crate), generator = LegacyElementDefinitions.Get(ObstacleKind.Generator);
                LevelDefinition level = ScriptableObject.CreateInstance<LevelDefinition>();
                JsonUtility.FromJsonOverwrite(File.ReadAllText(Evidence + "/play-input-" + kind + "-" + (kind == ObstacleKind.Generator ? 3 : 2) + ".json"), level);
                IDictionary entries = Entries(); var observers = new Dictionary<object, Observation>(); Dictionary<object, object> originals = null;
                try
                {
                    LevelRuntimeState state = (LevelRuntimeState)Invoke(typeof(FixedObstacleVerification), "Build", level, 12345);
                    int index = state.CellAt(new BoardCoordinate(4, 4)).ObstacleIndex.Value;
                    BoardActionExecutor executor = new BoardActionExecutor(state);
                    int initialDurability = state.Obstacles[index].Durability, requiredCharge = state.Obstacles[index].Definition.RequiredCharge;
                    string input = JsonUtility.ToJson(level), random = JsonUtility.ToJson(UnityEngine.Random.state), rules = Snapshot(state.Random);
                    bool toDurable = kind == ObstacleKind.Generator;
                    ElementReactionBehavior key = toDurable ? ElementReactionBehavior.Durability : ElementReactionBehavior.GeneratorCharge;
                    // 런타임 구성 후 같은ID의 호환 프로필/다른 행동으로 실제 적용 선택을 확인한다.
                    definitions[original.Id] = new ElementDefinition(original.Id, original.DisplayName, toDurable ? durable.Placement : null,
                        toDurable ? null : generator.ChargePlacement, original.DamageSourcePolicy, original.ColorMatchPolicy,
                        toDurable ? durable.DamageAggregationPolicy : original.DamageAggregationPolicy, original.RemovalMissionProfile, key);
                    originals = Observe(entries, observers);
                    BoardActionResult result = executor.Swap(new BoardCoordinate(4, 0), new BoardCoordinate(4, 1));
                    DamageResponse expected = toDurable ? DamageResponse.Damage : DamageResponse.Charge;
                    foreach (Observation observer in observers.Values) Connections.AddRange(observer.States);
                    Connections.Add(RowText("alternate-key-public-swap", kind + "/" + key, "initialDurability=" + initialDurability + ";requiredCharge=" + requiredCharge + ";state=" + Snapshot(executor.State) + ";result=" + Snapshot(result) + ";context=" + Context(executor.TurnEffects)));
                    Check(result.IsApplied && result.Effects.Any(effect => effect.Content == RuntimeContent.Rocket && effect.Response == DamageResponse.Activate), "같은ID 대체 행동 실제 교환/발동 " + kind);
                    Check(result.Effects.Any(effect => effect.Target.Equals(new BoardCoordinate(4, 4)) && effect.Response == expected), "종류 대신 키의 실제 피격 응답 " + kind);
                    Check(observers[key].Bodies.Count(body => body == index) == 1 && observers.Where(pair => !pair.Key.Equals(key)).All(pair => !pair.Value.Bodies.Contains(index)), "종류 대신 키의 실제 적용1회 " + kind);
                    int expectedDurability = toDurable ? Math.Max(0, initialDurability - 1) : (1 >= requiredCharge ? 0 : initialDurability);
                    Check(executor.State.Obstacles[index].Durability == expectedDurability && executor.State.Obstacles[index].Charge == (toDurable ? 0 : 1) && executor.TurnEffects.HasCharged(index) == !toDurable, "대체 키의 실제 피해/충전/임계/턴 기록 " + kind);
                    Check(input == JsonUtility.ToJson(level) && random == JsonUtility.ToJson(UnityEngine.Random.state) && rules == Snapshot(executor.State.Random), "대체 키 원본/규칙·전역 난수 보존 " + kind);
                }
                finally
                {
                    if (originals != null) foreach (var originalEntry in originals) entries[originalEntry.Key] = originalEntry.Value;
                    definitions[original.Id] = original;
                    UnityEngine.Object.DestroyImmediate(level);
                    Check(ReferenceEquals(LegacyElementDefinitions.Get(kind), original) && (originals == null || originals.All(pair => ReferenceEquals(entries[pair.Key], pair.Value))), "finally 대체 정의/적용 짝 정확한 원래 참조 복원 " + kind);
                }
            }
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
                if (mode == "quick") {ApplyContract(); AlternateApply(); Inherit("ContractChecks"); Inherit("DispatchChecks"); Inherit("BoundaryChecks");}
                else if (mode == "red")
                {
                    foreach (ObstacleKind kind in Enum.GetValues(typeof(ObstacleKind)))
                        try { Play(kind, kind == ObstacleKind.Generator ? 3 : 2, kind == ObstacleKind.Generator ? 2 : 0, true); }
                        catch (Exception error) { Results.Add("FAIL " + kind + " " + error); Debug.LogException(error); exit = 1; }
                }
                else
                {
                    Inherit("ExistingChecks"); AllPlay(mode != "before");
                    if (mode == "before") File.WriteAllLines(Evidence + "/baseline-values.jsonl", Values);
                    else
                    {
                        Check(RecordedLogicComparison.Equal(File.ReadAllLines(Evidence + "/baseline-values.jsonl"), Values), "기존 논리/입력/팩 비교 · 추가 비행 표시 이력 별도");
                        ApplyContract(); AlternateApply(); Inherit("ContractChecks"); Inherit("DispatchChecks"); Inherit("BoundaryChecks");
                    }
                    File.WriteAllText(Evidence + "/definitions-" + mode + ".json", Snapshot(Enum.GetValues(typeof(ObstacleKind)).Cast<ObstacleKind>().Select(LegacyElementDefinitions.Get).ToArray()));
                }
            }
            catch (Exception error) { Results.Add("FAIL " + error); Debug.LogException(error); exit = 1; }
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
