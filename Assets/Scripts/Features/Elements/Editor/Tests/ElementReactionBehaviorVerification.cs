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
    /// <summary>행동 키가 없는 같은 정의의 실제 조회와 전체 전환 전 기준을 검사한다.</summary>
    public static class ElementReactionBehaviorVerification
    {
        private const string Evidence = "Logs/ElementFramework/Stage33";
        private static readonly List<string> Results = new List<string>();
        private static readonly List<string> Values = new List<string>();
        private static readonly List<string> Connections = new List<string>();
        private static object Invoke(Type type, string method, params object[] args) => type.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
        private static string Snapshot(object value) => (string)Invoke(typeof(LevelInitialStateVerification), "Snapshot", value);
        private static string Context(TurnEffectContext value) => (string)Invoke(typeof(DamageAggregationPolicyVerification), "Context", value);
        private static Dictionary<ElementId, ElementDefinition> Definitions() => (Dictionary<ElementId, ElementDefinition>)Invoke(typeof(CapsuleMagnetPolicyVerification), "Definitions");
        private static void Check(bool pass, string name) { if (!pass) throw new InvalidOperationException(name); Results.Add("PASS " + name); }
        [Serializable] private sealed class Row { public string name, input, output; }
        private static string RowText(string name, string input, string output) => JsonUtility.ToJson(new Row { name = name, input = input, output = output });
        private static LevelDefinition Fixture(ObstacleKind kind)
        {
            string path = Evidence + "/reaction-input-" + kind + ".json";
            LevelDefinition level = ScriptableObject.CreateInstance<LevelDefinition>();
            if (File.Exists(path)) { JsonUtility.FromJsonOverwrite(File.ReadAllText(path), level); return level; }
            LevelDefinition source = kind == ObstacleKind.Generator ?
                (LevelDefinition)Invoke(typeof(DurableMagnetPolicyVerification), "OtherGeneratorFixture", ObstacleKind.Crate) :
                (LevelDefinition)Invoke(typeof(DurableMagnetPolicyVerification), "Fixture", kind, 1, false, false, false);
            try { JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source), level); }
            finally { UnityEngine.Object.DestroyImmediate(source); }
            Check(LevelDefinitionValidator.Validate(level).Count == 0, "유효 실제 행동 조회 입력 " + kind);
            File.WriteAllText(path, JsonUtility.ToJson(level)); return level;
        }
        private static void MissingKey(ObstacleKind kind)
        {
            ElementDefinition original = LegacyElementDefinitions.Get(kind);
            Dictionary<ElementId, ElementDefinition> definitions = Definitions();
            LevelDefinition level = Fixture(kind);
            try
            {
                LevelRuntimeState state = (LevelRuntimeState)Invoke(typeof(FixedObstacleVerification), "Build", level, 12345);
                TurnEffectContext context = (TurnEffectContext)Invoke(typeof(FixedObstacleVerification), "Context");
                BoardCoordinate target = state.Cells.First(cell => cell.ObstacleIndex.HasValue && state.Obstacles[cell.ObstacleIndex.Value].Definition.Kind == kind).Coordinate;
                string initial = Snapshot(state), turn = Context(context), input = JsonUtility.ToJson(level), random = JsonUtility.ToJson(UnityEngine.Random.state);
                // 구형 전체 생성자는 새 행동을 추론하지 않아야 한다. 기존 프로필은 모두 전달한다.
                ElementDefinition missing = new ElementDefinition(original.Id, original.DisplayName, original.Placement, original.ChargePlacement,
                    original.DamageSourcePolicy, original.ColorMatchPolicy, original.DamageAggregationPolicy, original.RemovalMissionProfile);
                definitions[original.Id] = missing;
                Check(missing.Id == original.Id && missing.DisplayName == original.DisplayName && ReferenceEquals(missing.Placement, original.Placement) &&
                    ReferenceEquals(missing.ChargePlacement, original.ChargePlacement) && ReferenceEquals(missing.DamageSourcePolicy, original.DamageSourcePolicy) &&
                    ReferenceEquals(missing.ColorMatchPolicy, original.ColorMatchPolicy) && ReferenceEquals(missing.DamageAggregationPolicy, original.DamageAggregationPolicy) &&
                    ReferenceEquals(missing.RemovalMissionProfile, original.RemovalMissionProfile), "같은ID 기존 값/프로필 정확한 참조 " + kind);
                DamageReaction reaction = null; InvalidOperationException error = null;
                try { reaction = DamageReaction.Evaluate(state, target, DamageCause.Power, target, context, RabbitColor.Type1, 7); }
                catch (InvalidOperationException found) { error = found; }
                string actual = error == null ? reaction.Response + "/" + reaction.Amount : error.Message;
                Connections.Add(RowText("missing-reaction-key", input, "id=" + original.Id.Value + ";actual=" + actual + ";state=" + Snapshot(state) + ";context=" + Context(context)));
                Check(initial == Snapshot(state) && turn == Context(context) && input == JsonUtility.ToJson(level) && random == JsonUtility.ToJson(UnityEngine.Random.state), "실제 조회 상태/문맥/원본/난수 무변경 " + kind);
                Check(error != null && error.Message.Contains(original.Id.Value) && error.Message.Contains("행동"),
                    "같은ID 행동 키 누락 " + kind + " actual=" + actual + " want=ID 포함 행동 오류");
            }
            finally
            {
                definitions[original.Id] = original;
                UnityEngine.Object.DestroyImmediate(level);
                Check(ReferenceEquals(LegacyElementDefinitions.Get(kind), original), "finally 정확한 원래 등록 참조 복원 " + kind);
            }
        }
        private static int RedChecks()
        {
            int failed = 0;
            foreach (ObstacleKind kind in Enum.GetValues(typeof(ObstacleKind)).Cast<ObstacleKind>())
                try { MissingKey(kind); }
                catch (Exception error) { Results.Add("FAIL " + kind + " " + error); Debug.LogException(error); failed++; }
            return failed == 0 ? 0 : 1;
        }
        private static Type BehaviorType => typeof(ElementDefinition).Assembly.GetType("Elements.ElementReactionBehavior");
        private static ElementDefinition Define(ElementDefinition original, object key, ElementPlacementProfile placement,
            ElementChargePlacementProfile charge, ElementColorMatchPolicy color, ElementDamageAggregationPolicy aggregation, ElementRemovalMissionProfile removal)
            => (ElementDefinition)Activator.CreateInstance(typeof(ElementDefinition), new object[] { original.Id, original.DisplayName, placement, charge,
                original.DamageSourcePolicy, color, aggregation, removal, key });
        private static void ContractChecks()
        {
            Type keyType = BehaviorType;
            Check(keyType != null && keyType.IsEnum && Enum.GetNames(keyType).SequenceEqual(new[] { "Durability", "GeneratorCharge" }), "작은 행동2종 계약");
            PropertyInfo property = typeof(ElementDefinition).GetProperty("ReactionBehavior");
            MethodInfo require = typeof(ElementDefinition).GetMethod("RequireReactionBehavior");
            Check(property != null && property.SetMethod == null && property.PropertyType == typeof(Nullable<>).MakeGenericType(keyType) && require != null, "불변 nullable 행동 키/필수 조회");
            foreach (ObstacleKind kind in Enum.GetValues(typeof(ObstacleKind)).Cast<ObstacleKind>())
            {
                ElementDefinition original = LegacyElementDefinitions.Get(kind);
                string expected = kind == ObstacleKind.Generator ? "GeneratorCharge" : "Durability";
                Check(property.GetValue(original).ToString() == expected && require.Invoke(original, null).ToString() == expected, "기존6종 명시적 행동 등록 " + kind);
                object[] arguments = { original.Id, original.DisplayName, original.Placement, original.ChargePlacement, original.DamageSourcePolicy,
                    original.ColorMatchPolicy, original.DamageAggregationPolicy, original.RemovalMissionProfile };
                for (int count = 2; count <= 8; count++)
                {
                    ElementDefinition old = (ElementDefinition)Activator.CreateInstance(typeof(ElementDefinition), arguments.Take(count).ToArray());
                    Exception error = null; try { require.Invoke(old, null); } catch (TargetInvocationException found) { error = found.InnerException; }
                    Check(property.GetValue(old) == null && error is InvalidOperationException && error.Message.Contains(original.Id.Value), "기존 생성자 호환/행동 추론 없음 " + kind + count);
                }
            }
            Type rules = typeof(DamageReaction).Assembly.GetType("Simulation.ObstacleDamageRules");
            object registry = rules.GetField("reactionBehaviors", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            Check(registry.GetType().FullName == "Elements.ElementBehaviorRegistry" && registry.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly).Length == 0,
                "명시적 등록표/등록 수정 공개 API 없음");
            System.Collections.IDictionary entries = (System.Collections.IDictionary)registry.GetType().GetField("queries", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(registry);
            Check(entries.Count == 2 && entries.Contains(Enum.Parse(keyType, "Durability")) && entries.Contains(Enum.Parse(keyType, "GeneratorCharge")), "전체 순회 없는 정확한 두 조회 등록");
        }
        private static void DispatchChecks()
        {
            ElementDefinition durable = LegacyElementDefinitions.Get(ObstacleKind.Crate), charge = LegacyElementDefinitions.Get(ObstacleKind.Generator);
            foreach (ObstacleKind kind in Enum.GetValues(typeof(ObstacleKind)).Cast<ObstacleKind>())
            {
                ElementDefinition original = LegacyElementDefinitions.Get(kind); Dictionary<ElementId, ElementDefinition> definitions = Definitions();
                LevelDefinition level = Fixture(kind);
                try
                {
                    LevelRuntimeState state = (LevelRuntimeState)Invoke(typeof(FixedObstacleVerification), "Build", level, 12345);
                    TurnEffectContext context = (TurnEffectContext)Invoke(typeof(FixedObstacleVerification), "Context");
                    BoardCoordinate target = state.Cells.First(cell => cell.ObstacleIndex.HasValue && state.Obstacles[cell.ObstacleIndex.Value].Definition.Kind == kind).Coordinate;
                    if (kind == ObstacleKind.Generator) typeof(RuntimeObstacle).GetProperty("Durability").GetSetMethod(true).Invoke(state.Obstacles[state.CellAt(target).ObstacleIndex.Value], new object[] { 0 });
                    string initial = Snapshot(state), turn = Context(context), input = JsonUtility.ToJson(level), random = JsonUtility.ToJson(UnityEngine.Random.state);
                    bool toDurable = kind == ObstacleKind.Generator;
                    object key = Enum.Parse(BehaviorType, toDurable ? "Durability" : "GeneratorCharge");
                    // 조회 전용 호환 프로필을 바꿔 종류가 아닌 실제 키 선택을 증명한다. 이 반응을 강제 적용하지 않는다.
                    definitions[original.Id] = Define(original, key, toDurable ? durable.Placement : null, toDurable ? null : charge.ChargePlacement,
                        null, toDurable ? durable.DamageAggregationPolicy : null, toDurable ? durable.RemovalMissionProfile : null);
                    DamageReaction reaction = DamageReaction.Evaluate(state, target, DamageCause.Power, target, context, RabbitColor.Type1, 7);
                    Check(reaction.Response == (toDurable ? DamageResponse.None : DamageResponse.Charge) && reaction.Amount == (toDurable ? 0 : 1), "종류 대신 같은ID 행동 키 실제 선택 " + kind);
                    Check(initial == Snapshot(state) && turn == Context(context) && input == JsonUtility.ToJson(level) && random == JsonUtility.ToJson(UnityEngine.Random.state), "행동 키 선택 조회 무변경 " + kind);
                    Connections.Add(RowText("alternate-reaction-key", input, "key=" + key + ";query=" + Snapshot(reaction) + ";state=" + initial + ";context=" + turn));
                    definitions[original.Id] = Define(original, Enum.ToObject(BehaviorType, 99), original.Placement, original.ChargePlacement, original.ColorMatchPolicy, original.DamageAggregationPolicy, original.RemovalMissionProfile);
                    InvalidOperationException error = null;
                    try { DamageReaction.Evaluate(state, target, DamageCause.Power, target, context); } catch (InvalidOperationException found) { error = found; }
                    Check(error != null && error.Message.Contains(original.Id.Value), "미지원 행동 키 ID 오류/기본 대체 없음 " + kind);
                    Connections.Add(RowText("unsupported-reaction-key", input, error.Message));
                }
                finally { definitions[original.Id] = original; UnityEngine.Object.DestroyImmediate(level); Check(ReferenceEquals(LegacyElementDefinitions.Get(kind), original), "finally 행동 선택 정확한 원래 등록 복원 " + kind); }
            }
        }
        private static void ExistingChecks()
        {
            Type previous = typeof(DurableMagnetPolicyVerification);
            List<string> results = (List<string>)previous.GetField("Results", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            List<string> values = (List<string>)previous.GetField("Values", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            results.Clear(); values.Clear();
            Invoke(previous, "DefaultChecks"); Invoke(previous, "MagnetChecks", false); Invoke(previous, "RepeatChecks", false); Invoke(previous, "ExistingChecks");
            Results.AddRange(results); Values.AddRange(values);
        }
        private static void Set(object owner, string property, object value) => owner.GetType().GetProperty(property).GetSetMethod(true).Invoke(owner, new[] { value });
        private static void Register(TurnEffectContext context, string method, params object[] args) => typeof(TurnEffectContext).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(context, args);
        private static ElementDefinition WithKey(ElementDefinition original, ElementReactionBehavior? key, ElementDamageSourcePolicy source = null)
            => new ElementDefinition(original.Id, original.DisplayName, original.Placement, original.ChargePlacement, source ?? original.DamageSourcePolicy,
                original.ColorMatchPolicy, original.DamageAggregationPolicy, original.RemovalMissionProfile, key);
        private static void BoundaryChecks()
        {
            string[] modes = { "fresh", "damaged", "other-hit", "next", "null", "wall", "far", "inactive", "deleted", "web", "mold", "protected" };
            DamageCause[] causes = { DamageCause.AdjacentMatch, DamageCause.Power, DamageCause.MagnetAdjacent, DamageCause.Hammer, (DamageCause)(-1), (DamageCause)4, (DamageCause)99 };
            foreach (ObstacleKind kind in Enum.GetValues(typeof(ObstacleKind)).Cast<ObstacleKind>())
            {
                ElementDefinition original = LegacyElementDefinitions.Get(kind); Dictionary<ElementId, ElementDefinition> definitions = Definitions();
                foreach (string mode in modes)
                {
                    LevelDefinition level = Fixture(kind);
                    try
                    {
                        BoardCoordinate target = new BoardCoordinate(4, 4), source = new BoardCoordinate(mode == "far" ? 2 : 3, 4);
                        if (mode == "wall") Check(LevelFlowEditing.SetWalls(level, new[] { new BoardEdge(source, target) }, false) == null, "새 키 선행 벽 준비");
                        foreach (DamageCause cause in causes)
                            foreach (ElementReactionBehavior? key in new ElementReactionBehavior?[] { null, (ElementReactionBehavior)99 })
                            {
                                LevelRuntimeState state = (LevelRuntimeState)Invoke(typeof(FixedObstacleVerification), "Build", level, 12345);
                                TurnEffectContext context = (TurnEffectContext)Invoke(typeof(FixedObstacleVerification), "Context");
                                int index = state.CellAt(target).ObstacleIndex.Value, hit = mode == "other-hit" ? 8 : 7;
                                if (mode == "damaged" || mode == "other-hit" || mode == "next")
                                {
                                    if (kind == ObstacleKind.Generator) Register(context, "RegisterCharge", index);
                                    else if (kind == ObstacleKind.Appliance) Register(context, "RegisterHit", 7, target);
                                    else Register(context, "RegisterDamage", index);
                                }
                                if (mode == "next") context = (TurnEffectContext)Invoke(typeof(GeneratorVerification), "Next", context, 2);
                                if (mode == "null") context = null;
                                if (mode == "inactive") typeof(RuntimeCell).GetField("<IsActive>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(state.CellAt(target), false);
                                if (mode == "deleted") Invoke(typeof(DamageReaction).Assembly.GetType("Simulation.ObstacleDamageRules"), "Remove", state, index);
                                if (mode == "web" || mode == "mold") { Set(state.CellAt(target), "Cover", (CoverKind?)(mode == "web" ? CoverKind.Web : CoverKind.Mold)); Set(state.CellAt(target), "CoverDurability", 1); }
                                if (mode == "protected") ((HashSet<BoardCoordinate>)typeof(TurnEffectContext).GetField("protectedPowers", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(context)).Add(target);
                                string initial = Snapshot(state), turn = Context(context), input = JsonUtility.ToJson(level), random = JsonUtility.ToJson(UnityEngine.Random.state);
                                DamageReaction before = DamageReaction.Evaluate(state, target, cause, source, context, RabbitColor.Type1, hit);
                                bool adjacent = cause == DamageCause.AdjacentMatch || cause == DamageCause.MagnetAdjacent;
                                bool gatedMagnet = cause == DamageCause.MagnetAdjacent && (kind != ObstacleKind.ColorLock && (kind == ObstacleKind.Generator || !original.DamageSourcePolicy.MagnetAdjacent));
                                bool deniedSource = cause >= DamageCause.AdjacentMatch && cause <= DamageCause.Hammer && !original.DamageSourcePolicy.Allows(cause);
                                bool enters = mode != "inactive" && mode != "deleted" && mode != "web" && mode != "mold" && mode != "protected" &&
                                    !(adjacent && (mode == "wall" || mode == "far")) && !gatedMagnet && !deniedSource;
                                definitions[original.Id] = WithKey(original, key); InvalidOperationException error = null; DamageReaction after = null;
                                try { after = DamageReaction.Evaluate(state, target, cause, source, context, RabbitColor.Type1, hit); } catch (InvalidOperationException found) { error = found; }
                                if (enters) Check(error != null && error.Message.Contains(original.Id.Value), "행동 조회에 도달한 누락/미지원 ID 오류 " + kind + mode + cause + key);
                                else Check(error == null && Snapshot(before) == Snapshot(after), "행동 키보다 선행하는 전체 응답/메시지 보존 " + kind + mode + cause + key);
                                Check(initial == Snapshot(state) && turn == Context(context) && input == JsonUtility.ToJson(level) && random == JsonUtility.ToJson(UnityEngine.Random.state), "전체 키 경계 조회 무변경 " + kind + mode + cause);
                                Connections.Add(RowText("reaction-key-boundary", kind + "/" + mode + "/" + (int)cause + "/" + key, "before=" + Snapshot(before) + ";after=" + (error == null ? Snapshot(after) : error.Message) + ";state=" + initial + ";context=" + turn));
                                definitions[original.Id] = original;
                            }
                    }
                    finally { definitions[original.Id] = original; UnityEngine.Object.DestroyImmediate(level); Check(ReferenceEquals(LegacyElementDefinitions.Get(kind), original), "finally 전체 키 경계 등록 복원 " + kind + mode); }
                }
                LevelDefinition fresh = Fixture(kind);
                try
                {
                    LevelRuntimeState state = (LevelRuntimeState)Invoke(typeof(FixedObstacleVerification), "Build", fresh, 12345);
                    TurnEffectContext context = (TurnEffectContext)Invoke(typeof(FixedObstacleVerification), "Context"); BoardCoordinate target = new BoardCoordinate(4, 4);
                    string initial = Snapshot(state), turn = Context(context), random = JsonUtility.ToJson(UnityEngine.Random.state);
                    foreach (string missing in new[] { "source", "placement", "both-placement", "aggregation", "color" })
                    {
                        if (missing == "aggregation" && kind == ObstacleKind.Generator || missing == "color" && kind != ObstacleKind.ColorLock) continue;
                        ElementDefinition changed = new ElementDefinition(original.Id, original.DisplayName,
                            missing == "placement" ? null : missing == "both-placement" && kind == ObstacleKind.Generator ? LegacyElementDefinitions.Get(ObstacleKind.Crate).Placement : original.Placement,
                            missing == "placement" ? null : missing == "both-placement" && kind != ObstacleKind.Generator ? LegacyElementDefinitions.Get(ObstacleKind.Generator).ChargePlacement : original.ChargePlacement,
                            missing == "source" ? null : original.DamageSourcePolicy, missing == "color" ? null : original.ColorMatchPolicy,
                            missing == "aggregation" ? null : original.DamageAggregationPolicy, original.RemovalMissionProfile,
                            missing == "source" ? (ElementReactionBehavior?)99 : original.ReactionBehavior);
                        definitions[original.Id] = changed; InvalidOperationException error = null;
                        try { DamageReaction.Evaluate(state, target, missing == "color" ? DamageCause.AdjacentMatch : DamageCause.Power, new BoardCoordinate(3, 4), context, RabbitColor.Type1, 7); }
                        catch (InvalidOperationException found) { error = found; }
                        string required = missing == "source" ? "피해 원인 정책" : missing == "aggregation" ? "피해 집계 정책" : missing == "color" ? "색 일치 정책" : null;
                        Check(error != null && error.Message.Contains(original.Id.Value) && (required == null || error.Message.Contains(required)), "기존 프로필 누락 순서/잘못된 행동 조합 ID 오류 " + kind + missing);
                        Check(initial == Snapshot(state) && turn == Context(context) && random == JsonUtility.ToJson(UnityEngine.Random.state), "누락/프로필 조합 조회 무변경 " + kind + missing);
                        Connections.Add(RowText("reaction-profile-error", kind + "/" + missing, error.Message)); definitions[original.Id] = original;
                    }
                    definitions[original.Id] = WithKey(original, null, new ElementDamageSourcePolicy(false, false, false, false));
                    DamageReaction denied = DamageReaction.Evaluate(state, target, DamageCause.Power, target, context);
                    Check(denied.Response == DamageResponse.None && denied.Amount == 0 && denied.Message == "인접 피해 대상 아님", "원인 거부 뒤에만 행동 키 조회 " + kind);
                    Connections.Add(RowText("reaction-source-denied", kind.ToString(), Snapshot(denied)));
                }
                finally { definitions[original.Id] = original; UnityEngine.Object.DestroyImmediate(fresh); Check(ReferenceEquals(LegacyElementDefinitions.Get(kind), original), "finally 프로필/거부 순서 등록 복원 " + kind); }
            }
        }
        private static LevelDefinition PlayFixture(ObstacleKind kind, int value)
        {
            string path = Evidence + "/play-input-" + kind + "-" + value + ".json"; LevelDefinition level = ScriptableObject.CreateInstance<LevelDefinition>();
            if (File.Exists(path)) { JsonUtility.FromJsonOverwrite(File.ReadAllText(path), level); return level; }
            LevelDefinition original = Fixture(kind);
            try
            {
                string input = JsonUtility.ToJson(original);
                if (kind == ObstacleKind.Generator) input = System.Text.RegularExpressions.Regex.Replace(input, "(\"kind\":5,\"durability\":1,\"color\":0,\"requiredCharge\":)3", "${1}" + value);
                else input = input.Replace("\"durability\":1", "\"durability\":" + value);
                JsonUtility.FromJsonOverwrite(input, level);
            }
            finally { UnityEngine.Object.DestroyImmediate(original); }
            Invoke(typeof(PowerEffectVerification), "Place", level, new BoardCoordinate(4, 0), InitialBlockKind.Rocket, RocketDirection.Horizontal, RabbitColor.Type1);
            Check(LevelDefinitionValidator.Validate(level).Count == 0, "정상 행동 실제 public Swap 입력 " + kind + value);
            File.WriteAllText(path, JsonUtility.ToJson(level)); return level;
        }
        private static void PlayChecks()
        {
            foreach (ObstacleKind kind in Enum.GetValues(typeof(ObstacleKind)).Cast<ObstacleKind>())
            {
                ElementDefinition definition = LegacyElementDefinitions.Get(kind);
                int min = kind == ObstacleKind.Generator ? definition.ChargePlacement.MinRequiredCharge : 1;
                int max = kind == ObstacleKind.Generator ? definition.ChargePlacement.MaxRequiredCharge : definition.Placement.MaxDurability;
                for (int value = min; value <= max; value++)
                    for (int charge = 0; charge < (kind == ObstacleKind.Generator ? value : 1); charge++)
                    {
                        LevelDefinition level = PlayFixture(kind, value);
                        try
                        {
                            LevelRuntimeState state = (LevelRuntimeState)Invoke(typeof(FixedObstacleVerification), "Build", level, 12345);
                            int index = state.CellAt(new BoardCoordinate(4, 4)).ObstacleIndex.Value;
                            if (kind == ObstacleKind.Generator) Set(state.Obstacles[index], "Charge", charge);
                            string input = JsonUtility.ToJson(level), random = JsonUtility.ToJson(UnityEngine.Random.state), rules = Snapshot(state.Random);
                            BoardActionExecutor executor = new BoardActionExecutor(state); BoardActionResult result = executor.Swap(new BoardCoordinate(4, 0), new BoardCoordinate(4, 1));
                            Check(result.IsApplied && result.Effects.Any(effect => effect.Content == RuntimeContent.Rocket && effect.Response == DamageResponse.Activate), "정상6종 실제 교환/로켓 발동 " + kind + value + charge + " actual=" + result.Reason);
                            if (kind == ObstacleKind.Generator)
                            {
                                Check(executor.State.Obstacles[index].Charge == charge + 1 && executor.TurnEffects.HasCharged(index) && result.Effects.Count(effect => effect.Response == DamageResponse.Charge) == 1, "등록된 충전 조회/실제 적용·본체 수당1회 " + value + charge);
                                bool activated = charge + 1 == value;
                                Check(executor.TurnEffects.Generators.Any(record => record.Event == GeneratorEvent.Activated) == activated && executor.State.Missions[0].Progress == (activated ? 1 : 0), "등록된 충전의 연결 철거/제거 미션 " + value + charge);
                            }
                            else
                            {
                                int hits = kind == ObstacleKind.Appliance ? 2 : 1, expected = Math.Max(0, value - hits);
                                Check(executor.State.Obstacles[index].Durability == expected && result.Effects.Count(effect => effect.Response == DamageResponse.Damage) == Math.Min(value, hits), "등록된 내구도 조회/전체 내구도 실제 적용 " + kind + value);
                                Check(executor.State.Missions[0].Progress == (expected == 0 ? 1 : 0), "정상 내구도 제거/본체 미션 " + kind + value);
                            }
                            Check(input == JsonUtility.ToJson(level) && random == JsonUtility.ToJson(UnityEngine.Random.state) && rules == Snapshot(executor.State.Random), "public Swap 원본/규칙·전역 난수 보존 " + kind);
                            Connections.Add(RowText("reaction-public-swap", kind + "/" + value + "/charge=" + charge, "input=" + input + ";state=" + Snapshot(executor.State) + ";result=" + Snapshot(result) + ";context=" + Context(executor.TurnEffects)));
                        }
                        finally { UnityEngine.Object.DestroyImmediate(level); }
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
                if (mode == "red") exit = RedChecks();
                else
                {
                    if (mode != "before")
                    {
                        foreach (ObstacleKind kind in Enum.GetValues(typeof(ObstacleKind)).Cast<ObstacleKind>()) MissingKey(kind);
                        ContractChecks(); DispatchChecks(); BoundaryChecks(); PlayChecks();
                    }
                    if (mode != "quick")
                    {
                        ExistingChecks();
                        if (mode == "before") File.WriteAllLines(Evidence + "/baseline-values.jsonl", Values);
                        else Check(RecordedLogicComparison.Equal(File.ReadAllLines(Evidence + "/baseline-values.jsonl"), Values), "기존 논리/입력/팩 비교 · 추가 비행 표시 이력 별도");
                        File.WriteAllText(Evidence + "/definitions-" + mode + ".json", Snapshot(Enum.GetValues(typeof(ObstacleKind)).Cast<ObstacleKind>().Select(kind => LegacyElementDefinitions.Get(kind)).ToArray()));
                    }
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
