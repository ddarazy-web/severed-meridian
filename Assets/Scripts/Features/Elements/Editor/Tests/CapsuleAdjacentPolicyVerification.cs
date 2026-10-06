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
    /// <summary>회수캡슐 일반 인접 허용과 실제 공통 피해 실행의 연결을 검사한다.</summary>
    public static class CapsuleAdjacentPolicyVerification
    {
        private const string Evidence = "Logs/ElementFramework/Stage30";
        private static readonly List<string> Results = new List<string>();
        private static readonly List<string> Values = new List<string>();
        private static readonly List<string> Connections = new List<string>();
        private static readonly string[] Modes = { "fresh", "damaged", "next", "complete", "null", "wall", "far", "inactive", "deleted", "web", "mold", "protected", "second-body" };
        private static readonly DamageCause[] Causes = { DamageCause.AdjacentMatch, DamageCause.Power, DamageCause.Hammer, DamageCause.MagnetAdjacent, (DamageCause)(-1), (DamageCause)4, (DamageCause)99 };
        private static BoardCoordinate C(int row, int column) => new BoardCoordinate(row, column);
        private static object Invoke(Type type, string method, params object[] args) => type.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
        private static string Snapshot(object value) => (string)Invoke(typeof(LevelInitialStateVerification), "Snapshot", value);
        private static string Context(TurnEffectContext value) => (string)Invoke(typeof(DamageAggregationPolicyVerification), "Context", value);
        private static LevelRuntimeState Build(LevelDefinition level) => (LevelRuntimeState)Invoke(typeof(FixedObstacleVerification), "Build", level, 12345);
        private static TurnEffectContext Fresh() => (TurnEffectContext)Invoke(typeof(FixedObstacleVerification), "Context");
        private static void Set(object owner, string property, object value) => owner.GetType().GetProperty(property).GetSetMethod(true).Invoke(owner, new[] { value });
        private static void Check(bool pass, string name) { if (!pass) throw new InvalidOperationException(name); Results.Add("PASS " + name); }
        [Serializable] private sealed class Row { public string name, input, output; }
        private static string RowText(string name, string input, string output) => JsonUtility.ToJson(new Row { name = name, input = input, output = output });
        private static Dictionary<ElementId, ElementDefinition> Definitions()
        {
            object catalog = typeof(LegacyElementDefinitions).GetField("Catalog", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            return (Dictionary<ElementId, ElementDefinition>)typeof(ElementCatalog).GetField("definitions", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(catalog);
        }
        private static ElementDefinition Define(ElementDefinition original, ElementDamageSourcePolicy policy) => new ElementDefinition(original.Id, original.DisplayName, original.Placement, original.ChargePlacement, policy, original.ColorMatchPolicy, original.DamageAggregationPolicy, original.RemovalMissionProfile, original.ReactionBehavior);
        private static ElementDamageSourcePolicy Policy(ElementDefinition original, bool adjacent) => new ElementDamageSourcePolicy(adjacent, original.DamageSourcePolicy.Power, original.DamageSourcePolicy.MagnetAdjacent, original.DamageSourcePolicy.Hammer);
        private static LevelDefinition Fixture(int durability, bool wall, bool match = false)
        {
            string path = Evidence + "/" + (match ? "match" : "input") + "-" + durability + "-" + wall + ".json";
            LevelDefinition level = ScriptableObject.CreateInstance<LevelDefinition>();
            if (File.Exists(path)) { JsonUtility.FromJsonOverwrite(File.ReadAllText(path), level); return level; }
            LevelDefinition source = (LevelDefinition)Invoke(typeof(PowerEffectVerification), "Make");
            try { JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source), level); }
            finally { UnityEngine.Object.DestroyImmediate(source); }
            if (match)
            {
                foreach (BoardCoordinate cell in new[] { C(3, 2), C(3, 4), C(2, 3) }) Invoke(typeof(PowerEffectVerification), "Place", level, cell, InitialBlockKind.FixedNormal, RocketDirection.Horizontal, RabbitColor.Type1);
                Invoke(typeof(PowerEffectVerification), "Place", level, C(3, 3), InitialBlockKind.FixedNormal, RocketDirection.Horizontal, RabbitColor.Type2);
            }
            BoardCoordinate first = match ? C(4, 3) : C(4, 4), second = match ? C(4, 2) : C(1, 1);
            Invoke(typeof(FixedObstacleVerification), "Obstacle", level, ObstacleKind.Safe, durability, first, RabbitColor.Type1);
            Invoke(typeof(FixedObstacleVerification), "Obstacle", level, ObstacleKind.Safe, durability, second, RabbitColor.Type1);
            JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":5,\"count\":2}]}", level);
            if (wall) Check(LevelFlowEditing.SetWalls(level, new[] { new BoardEdge(match ? C(3, 3) : C(3, 4), first) }, false) == null, "인접 벽 준비");
            Check(LevelDefinitionValidator.Validate(level).Count == 0, "두 본체/미션 유효 입력 " + path);
            File.WriteAllText(path, JsonUtility.ToJson(level)); return level;
        }
        private static (LevelRuntimeState state, TurnEffectContext context, BoardCoordinate target, BoardCoordinate source) Case(LevelDefinition level, string mode)
        {
            LevelRuntimeState state = Build(level); TurnEffectContext context = Fresh();
            BoardCoordinate target = C(4, 4), source = mode == "far" ? C(2, 4) : C(3, 4);
            if (mode == "damaged" || mode == "next" || mode == "second-body") typeof(TurnEffectContext).GetMethod("RegisterDamage", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(context, new object[] { 0 });
            if (mode == "next") context = (TurnEffectContext)Invoke(typeof(GeneratorVerification), "Next", context, 2);
            if (mode == "second-body") { target = C(1, 1); source = C(1, 0); }
            if (mode == "complete") Set(state.Missions[0], "Progress", 2);
            if (mode == "inactive") typeof(RuntimeCell).GetField("<IsActive>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(state.CellAt(target), false);
            if (mode == "deleted") Invoke(typeof(DamageReaction).Assembly.GetType("Simulation.ObstacleDamageRules"), "Remove", state, 0);
            if (mode == "web" || mode == "mold") { Set(state.CellAt(target), "Cover", (CoverKind?)(mode == "web" ? CoverKind.Web : CoverKind.Mold)); Set(state.CellAt(target), "CoverDurability", 1); }
            if (mode == "protected") ((HashSet<BoardCoordinate>)typeof(TurnEffectContext).GetField("protectedPowers", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(context)).Add(target);
            if (mode == "null") context = null;
            return (state, context, target, source);
        }
        private static DamageReaction Query((LevelRuntimeState state, TurnEffectContext context, BoardCoordinate target, BoardCoordinate source) item, DamageCause cause)
            => DamageReaction.Evaluate(item.state, item.target, cause, item.source, item.context, RabbitColor.Type1, 7);
        public static void Before() => Execute("before");
        public static void Red() => Execute("red");
        public static void Run() => Execute("after");
        private static void Execute(string mode)
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 Editor에서만 실행한다.");
            Directory.CreateDirectory(Evidence); Results.Clear(); Values.Clear(); Connections.Clear(); int exit = 0;
            try
            {
                if (mode != "before") { ConnectionCheck(); BoundaryConnections(); MatchChecks(true); MissingChecks(); }
                if (mode != "red")
                {
                    DefaultChecks(); MatchChecks(false); ExistingChecks();
                    if (mode == "before") File.WriteAllLines(Evidence + "/baseline-values.jsonl", Values);
                    else Check(RecordedLogicComparison.Equal(File.ReadAllLines(Evidence + "/baseline-values.jsonl"), Values), "기존 논리/입력/팩 비교 · 추가 비행 표시 이력 별도");
                    File.WriteAllText(Evidence + "/definitions-" + mode + ".json", Snapshot(LegacyElementDefinitions.Get(ObstacleKind.Safe)));
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
        private static void ConnectionCheck()
        {
            ElementDefinition original = LegacyElementDefinitions.Get(ObstacleKind.Safe); Dictionary<ElementId, ElementDefinition> definitions = Definitions(); LevelDefinition level = Fixture(1, false);
            try
            {
                var item = Case(level, "fresh"); string initial = Snapshot(item.state), turn = Context(item.context), random = JsonUtility.ToJson(UnityEngine.Random.state);
                Check(!original.DamageSourcePolicy.AdjacentMatch, "기본 캡슐 인접 false 유지");
                ElementDefinition replacement = Define(original, Policy(original, true)); definitions[original.Id] = replacement;
                Check(replacement.Id == original.Id && replacement.DisplayName == original.DisplayName && ReferenceEquals(replacement.Placement, original.Placement) && ReferenceEquals(replacement.ChargePlacement, original.ChargePlacement) && ReferenceEquals(replacement.ColorMatchPolicy, original.ColorMatchPolicy) && ReferenceEquals(replacement.DamageAggregationPolicy, original.DamageAggregationPolicy) && ReferenceEquals(replacement.RemovalMissionProfile, original.RemovalMissionProfile), "일반 인접 이외 원래 정의/프로필 참조 유지");
                DamageReaction reaction = Query(item, DamageCause.AdjacentMatch);
                Connections.Add(RowText("same-id-adjacent", JsonUtility.ToJson(level), "id=" + original.Id.Value + ";query=" + Snapshot(reaction) + ";state=" + Snapshot(item.state) + ";context=" + Context(item.context)));
                Check(initial == Snapshot(item.state) && turn == Context(item.context) && random == JsonUtility.ToJson(UnityEngine.Random.state), "교체 정의 조회 상태/문맥/난수 무변경");
                Check(reaction.Response == DamageResponse.Damage && reaction.Amount == 1, "같은 ID 실제 인접 반응 actual=" + reaction.Response + "/" + reaction.Amount + " want=Damage/1");
            }
            finally { definitions[original.Id] = original; UnityEngine.Object.DestroyImmediate(level); Check(ReferenceEquals(LegacyElementDefinitions.Get(ObstacleKind.Safe), original), "finally 원래 등록 참조 정확한 복원"); }
        }
        private static void DefaultChecks()
        {
            for (int durability = 1; durability <= 5; durability++)
                foreach (string mode in Modes)
                {
                    LevelDefinition level = Fixture(durability, mode == "wall");
                    try
                    {
                        foreach (DamageCause cause in Causes)
                        {
                            var item = Case(level, mode); string initial = Snapshot(item.state), turn = Context(item.context), random = JsonUtility.ToJson(UnityEngine.Random.state), input = JsonUtility.ToJson(level);
                            DamageReaction reaction = Query(item, cause);
                            Check(initial == Snapshot(item.state) && turn == Context(item.context) && random == JsonUtility.ToJson(UnityEngine.Random.state) && input == JsonUtility.ToJson(level), "기본 반응 전체 상태/문맥/입력/난수 무변경");
                            Values.Add(RowText("capsule-default", durability + "/" + mode + "/" + (int)cause, "query=" + Snapshot(reaction) + ";state=" + initial + ";context=" + turn + ";input=" + input));
                        }
                    }
                    finally { UnityEngine.Object.DestroyImmediate(level); }
                }
        }
        private static void BoundaryConnections()
        {
            ElementDefinition original = LegacyElementDefinitions.Get(ObstacleKind.Safe); Dictionary<ElementId, ElementDefinition> definitions = Definitions();
            for (int durability = 1; durability <= 5; durability++)
                foreach (string mode in Modes)
                {
                    LevelDefinition level = Fixture(durability, mode == "wall");
                    try
                    {
                        var item = Case(level, mode); string initial = Snapshot(item.state), turn = Context(item.context), random = JsonUtility.ToJson(UnityEngine.Random.state);
                        DamageReaction[] before = Causes.Select(cause => Query(item, cause)).ToArray();
                        definitions[original.Id] = Define(original, Policy(original, true));
                        DamageReaction[] allowed = Causes.Select(cause => Query(item, cause)).ToArray();
                        DamageResponse expected = mode == "wall" ? DamageResponse.Wall : mode == "far" || mode == "inactive" || mode == "deleted" ? DamageResponse.None : mode == "web" || mode == "mold" ? DamageResponse.CoverDamage : mode == "protected" ? DamageResponse.Protected : mode == "damaged" ? DamageResponse.AlreadyDamaged : DamageResponse.Damage;
                        Check(allowed[0].Response == expected && allowed[0].Amount == (expected == DamageResponse.Damage || expected == DamageResponse.CoverDamage ? 1 : 0), "true 인접 전체 경계 " + durability + mode);
                        for (int i = 1; i < Causes.Length; i++) Check(Snapshot(before[i]) == Snapshot(allowed[i]), "다른/미정의 원인 전체 응답/메시지 보존 " + (int)Causes[i]);
                        definitions[original.Id] = Define(original, Policy(original, false));
                        DamageReaction[] denied = Causes.Select(cause => Query(item, cause)).ToArray();
                        Check(Snapshot(before) == Snapshot(denied), "false 재교체 전체 응답 동일");
                        Check(initial == Snapshot(item.state) && turn == Context(item.context) && random == JsonUtility.ToJson(UnityEngine.Random.state), "false true false 조회 무변경");
                        Connections.Add(RowText("adjacent-boundary", durability + "/" + mode, "before=" + Snapshot(before) + ";allowed=" + Snapshot(allowed) + ";denied=" + Snapshot(denied) + ";state=" + initial + ";context=" + turn));
                    }
                    finally { definitions[original.Id] = original; UnityEngine.Object.DestroyImmediate(level); Check(ReferenceEquals(LegacyElementDefinitions.Get(ObstacleKind.Safe), original), "finally 경계 등록 참조 복원"); }
                }
        }
        private static void MatchChecks(bool connected)
        {
            ElementDefinition original = LegacyElementDefinitions.Get(ObstacleKind.Safe); Dictionary<ElementId, ElementDefinition> definitions = Definitions();
            for (int durability = 1; durability <= 5; durability++)
                foreach (bool wall in new[] { false, true })
                {
                    LevelDefinition level = Fixture(durability, wall, true);
                    try
                    {
                        LevelRuntimeState state = Build(level); string input = JsonUtility.ToJson(level), random = JsonUtility.ToJson(UnityEngine.Random.state);
                        if (connected) definitions[original.Id] = Define(original, Policy(original, true));
                        BoardActionExecutor executor = new BoardActionExecutor(state); BoardActionResult result = executor.Swap(C(2, 3), C(3, 3));
                        int first = connected && !wall ? durability - 1 : durability, second = connected ? durability - 1 : durability;
                        Check(result.IsApplied && executor.State.Obstacles[0].Durability == first && executor.State.Obstacles[1].Durability == second, "실제 교환/매칭/공통 적용 두 본체 " + durability + wall + connected);
                        Check(executor.State.Missions[0].Progress == (first == 0 ? 1 : 0) + (second == 0 ? 1 : 0), "실제 제거 미션 본체수 집계");
                        Check(executor.TurnEffects.HasDamaged(0) == (connected && !wall) && executor.TurnEffects.HasDamaged(1) == connected, "실제 본체별 턴 피해 기록");
                        EffectRecord[] damage = result.Effects.Where(effect => effect.Response == DamageResponse.Damage).ToArray();
                        Check(damage.Length == (connected ? (wall ? 1 : 2) : 0) && damage.All(effect => effect.Cause == DamageCause.AdjacentMatch && effect.DurabilityBefore == durability && effect.DurabilityAfter == durability - 1), "허용된 인접 반응만 실제 효과로 기록");
                        Check(damage.SelectMany(effect => effect.RemovedObstacleIndices).Count() == (first == 0 ? 1 : 0) + (second == 0 ? 1 : 0), "제거 효과 본체수와 미션 일치");
                        Check(input == JsonUtility.ToJson(level) && random == JsonUtility.ToJson(UnityEngine.Random.state), "실제 적용 원본/전역 난수 보존");
                        string output = "state=" + Snapshot(executor.State) + ";result=" + Snapshot(result) + ";context=" + Context(executor.TurnEffects);
                        if (connected) Connections.Add(RowText("actual-match", durability + "/wall=" + wall, output));
                        else Values.Add(RowText("default-match", JsonUtility.ToJson(level), output));
                    }
                    finally { definitions[original.Id] = original; UnityEngine.Object.DestroyImmediate(level); Check(ReferenceEquals(LegacyElementDefinitions.Get(ObstacleKind.Safe), original), "finally 매칭 등록 참조 복원"); }
                }
        }
        private static void MissingChecks()
        {
            ElementDefinition original = LegacyElementDefinitions.Get(ObstacleKind.Safe); Dictionary<ElementId, ElementDefinition> definitions = Definitions(); LevelDefinition level = Fixture(1, false);
            try
            {
                var item = Case(level, "fresh"); string initial = Snapshot(item.state), turn = Context(item.context), random = JsonUtility.ToJson(UnityEngine.Random.state);
                definitions[original.Id] = Define(original, null); Exception error = null;
                try { Query(item, DamageCause.AdjacentMatch); } catch (InvalidOperationException found) { error = found; }
                Check(error is InvalidOperationException && error.Message.Contains(original.Id.Value), "유효 누락 정책 ID 오류/기본값 대체 없음");
                Check(initial == Snapshot(item.state) && turn == Context(item.context) && random == JsonUtility.ToJson(UnityEngine.Random.state), "누락 조회 무변경");
                Connections.Add(RowText("missing-source", original.Id.Value, error.Message));
            }
            finally { definitions[original.Id] = original; UnityEngine.Object.DestroyImmediate(level); Check(ReferenceEquals(LegacyElementDefinitions.Get(ObstacleKind.Safe), original), "finally 누락 등록 참조 복원"); }
        }
        private static void ExistingChecks()
        {
            Type previous = typeof(InitialMissionSupplyVerification);
            List<string> results = (List<string>)previous.GetField("Results", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            List<string> values = (List<string>)previous.GetField("Values", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            results.Clear(); values.Clear();
            Invoke(previous, "DefaultChecks"); Invoke(previous, "SupplyChecks"); Invoke(previous, "BoundaryChecks"); Invoke(previous, "ExistingChecks");
            Results.AddRange(results); Values.AddRange(values);
        }
    }
}
