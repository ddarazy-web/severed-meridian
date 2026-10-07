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
    /// <summary>회수캡슐 자석 인접 정책과 실제 자석 소비/공통 피해 실행의 연결을 검사한다.</summary>
    public static class CapsuleMagnetPolicyVerification
    {
        private const string Evidence = "Logs/ElementFramework/Stage31";
        private static readonly List<string> Results = new List<string>();
        private static readonly List<string> Values = new List<string>();
        private static readonly List<string> Connections = new List<string>();
        private static readonly string[] Modes = { "fresh", "damaged", "next", "complete", "null", "wall", "far", "inactive", "deleted", "web", "mold", "protected", "second-body" };
        private static readonly DamageCause[] Causes = { DamageCause.MagnetAdjacent, DamageCause.Power, DamageCause.Hammer, DamageCause.AdjacentMatch, (DamageCause)(-1), (DamageCause)4, (DamageCause)99 };
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
        private static ElementDamageSourcePolicy Policy(ElementDefinition original, bool magnet) => new ElementDamageSourcePolicy(original.DamageSourcePolicy.AdjacentMatch, original.DamageSourcePolicy.Power, magnet, original.DamageSourcePolicy.Hammer);
        private static LevelDefinition Fixture(int durability, bool wall, bool match = false)
        {
            string path = Evidence + "/" + (match ? "magnet" : "input") + "-" + durability + "-" + wall + ".json";
            LevelDefinition level = ScriptableObject.CreateInstance<LevelDefinition>();
            if (File.Exists(path)) { JsonUtility.FromJsonOverwrite(File.ReadAllText(path), level); return level; }
            LevelDefinition source = (LevelDefinition)Invoke(typeof(PowerEffectVerification), "Make");
            try { JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source), level); }
            finally { UnityEngine.Object.DestroyImmediate(source); }
            if (match)
            {
                Invoke(typeof(PowerEffectVerification), "Place", level, C(7, 0), InitialBlockKind.Magnet, RocketDirection.Horizontal, RabbitColor.Type1);
                foreach (BoardCoordinate cell in new[] { C(7, 1), C(3, 4), C(1, 0) }) Invoke(typeof(PowerEffectVerification), "Place", level, cell, InitialBlockKind.FixedNormal, RocketDirection.Horizontal, RabbitColor.Type1);
            }
            BoardCoordinate first = C(4, 4), second = C(1, 1);
            Invoke(typeof(FixedObstacleVerification), "Obstacle", level, ObstacleKind.Safe, durability, first, RabbitColor.Type1);
            Invoke(typeof(FixedObstacleVerification), "Obstacle", level, ObstacleKind.Safe, durability, second, RabbitColor.Type1);
            JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":5,\"count\":2}]}", level);
            if (wall) Check(LevelFlowEditing.SetWalls(level, new[] { new BoardEdge(C(3, 4), first) }, false) == null, "인접 벽 준비");
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
                if (mode != "before") { ConnectionCheck(); BoundaryConnections(); MagnetChecks(true); ColorChecks(); MissingChecks(true); }
                if (mode != "red")
                {
                    DefaultChecks(); MagnetChecks(false); OtherChecks(); ExistingChecks();
                    if (mode == "before") { MissingChecks(false); File.WriteAllLines(Evidence + "/baseline-values.jsonl", Values); }
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
                Check(!original.DamageSourcePolicy.MagnetAdjacent, "기본 캡슐 자석 인접 false 유지");
                ElementDefinition replacement = Define(original, Policy(original, true)); definitions[original.Id] = replacement;
                Check(replacement.DamageSourcePolicy.AdjacentMatch == original.DamageSourcePolicy.AdjacentMatch && replacement.DamageSourcePolicy.Power == original.DamageSourcePolicy.Power && replacement.DamageSourcePolicy.Hammer == original.DamageSourcePolicy.Hammer && replacement.DamageSourcePolicy.MagnetAdjacent, "MagnetAdjacent만 true 교체");
                Check(replacement.Id == original.Id && replacement.DisplayName == original.DisplayName && ReferenceEquals(replacement.Placement, original.Placement) && ReferenceEquals(replacement.ChargePlacement, original.ChargePlacement) && ReferenceEquals(replacement.ColorMatchPolicy, original.ColorMatchPolicy) && ReferenceEquals(replacement.DamageAggregationPolicy, original.DamageAggregationPolicy) && ReferenceEquals(replacement.RemovalMissionProfile, original.RemovalMissionProfile), "자석 인접 이외 원래 정의/프로필 참조 유지");
                DamageReaction reaction = Query(item, DamageCause.MagnetAdjacent);
                Connections.Add(RowText("same-id-magnet", JsonUtility.ToJson(level), "id=" + original.Id.Value + ";query=" + Snapshot(reaction) + ";state=" + Snapshot(item.state) + ";context=" + Context(item.context)));
                Check(initial == Snapshot(item.state) && turn == Context(item.context) && random == JsonUtility.ToJson(UnityEngine.Random.state), "교체 정의 조회 상태/문맥/난수 무변경");
                Check(reaction.Response == DamageResponse.Damage && reaction.Amount == 1, "같은 ID 실제 자석 인접 반응 actual=" + reaction.Response + "/" + reaction.Amount + " want=Damage/1");
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
                            Values.Add(RowText("magnet-default", durability + "/" + mode + "/" + (int)cause, "query=" + Snapshot(reaction) + ";state=" + initial + ";context=" + turn + ";input=" + input));
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
                        Connections.Add(RowText("magnet-boundary", durability + "/" + mode, "before=" + Snapshot(before) + ";allowed=" + Snapshot(allowed) + ";denied=" + Snapshot(denied) + ";state=" + initial + ";context=" + turn));
                    }
                    finally { definitions[original.Id] = original; UnityEngine.Object.DestroyImmediate(level); Check(ReferenceEquals(LegacyElementDefinitions.Get(ObstacleKind.Safe), original), "finally 경계 등록 참조 복원"); }
                }
        }
        private static void MagnetChecks(bool connected)
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
                        BoardActionExecutor executor = new BoardActionExecutor(state); BoardActionResult result = executor.Swap(C(7, 0), C(7, 1));
                        int first = connected && !wall ? durability - 1 : durability, second = connected ? durability - 1 : durability;
                        if (!result.IsApplied || executor.State.Obstacles[0].Durability != first || executor.State.Obstacles[1].Durability != second)
                            File.WriteAllText(Evidence + "/magnet-failure.json", RowText("actual-magnet-failure", JsonUtility.ToJson(level), "state=" + Snapshot(executor.State) + ";result=" + Snapshot(result) + ";context=" + Context(executor.TurnEffects)));
                        Check(result.IsApplied && executor.State.Obstacles[0].Durability == first && executor.State.Obstacles[1].Durability == second, "실제 자석 교환/일반 블록 소비/공통 적용 두 본체 " + durability + wall + connected + " actual=" + result.Reason + "/" + executor.State.Obstacles[0].Durability + "/" + executor.State.Obstacles[1].Durability + " want=Applied/" + first + "/" + second);
                        Check(result.Effects.Any(effect => effect.Content == RuntimeContent.Magnet && effect.Response == DamageResponse.Activate) && result.Effects.Any(effect => effect.Content == RuntimeContent.Normal && effect.Response == DamageResponse.Remove), "실제 자석 발동과 일반 블록 소비에서 인접 생성");
                        Check(executor.State.Missions[0].Progress == (first == 0 ? 1 : 0) + (second == 0 ? 1 : 0), "실제 제거 미션 본체수 집계");
                        Check(executor.TurnEffects.HasDamaged(0) == (connected && !wall) && executor.TurnEffects.HasDamaged(1) == connected, "실제 본체별 턴 피해 기록");
                        EffectRecord[] damage = result.Effects.Where(effect => effect.Response == DamageResponse.Damage).ToArray();
                        Check(damage.Length == (connected ? (wall ? 1 : 2) : 0) && damage.All(effect => effect.Cause == DamageCause.MagnetAdjacent && effect.DurabilityBefore == durability && effect.DurabilityAfter == durability - 1), "허용된 자석 인접 반응만 실제 효과로 기록");
                        Check(damage.SelectMany(effect => effect.RemovedObstacleIndices).Count() == (first == 0 ? 1 : 0) + (second == 0 ? 1 : 0), "제거 효과 본체수와 미션 일치");
                        Check(input == JsonUtility.ToJson(level) && random == JsonUtility.ToJson(UnityEngine.Random.state), "실제 적용 원본/전역 난수 보존");
                        string output = "state=" + Snapshot(executor.State) + ";result=" + Snapshot(result) + ";context=" + Context(executor.TurnEffects);
                        if (connected) Connections.Add(RowText("actual-magnet", durability + "/wall=" + wall, output));
                        else Values.Add(RowText("default-magnet", JsonUtility.ToJson(level), output));
                    }
                    finally { definitions[original.Id] = original; UnityEngine.Object.DestroyImmediate(level); Check(ReferenceEquals(LegacyElementDefinitions.Get(ObstacleKind.Safe), original), "finally 자석 등록 참조 복원"); }
                }
        }
        private static void MissingChecks(bool connected)
        {
            ElementDefinition original = LegacyElementDefinitions.Get(ObstacleKind.Safe); Dictionary<ElementId, ElementDefinition> definitions = Definitions(); LevelDefinition level = Fixture(1, false);
            try
            {
                var item = Case(level, "fresh"); string initial = Snapshot(item.state), turn = Context(item.context), random = JsonUtility.ToJson(UnityEngine.Random.state);
                definitions[original.Id] = Define(original, null); Exception error = null; DamageReaction reaction = null;
                try { reaction = Query(item, DamageCause.MagnetAdjacent); } catch (InvalidOperationException found) { error = found; }
                Check(connected ? error is InvalidOperationException && error.Message.Contains(original.Id.Value) : error == null && reaction.Response == DamageResponse.None && reaction.Amount == 0, connected ? "유효 누락 정책 ID 오류/false 대체 없음" : "전환 전 종류 제한이 누락을 가린 실제 None/0");
                Check(initial == Snapshot(item.state) && turn == Context(item.context) && random == JsonUtility.ToJson(UnityEngine.Random.state), "누락 조회 무변경");
                Connections.Add(RowText("missing-source", original.Id.Value, error == null ? Snapshot(reaction) : error.Message));
                foreach (string mode in new[] { "wall", "far", "inactive", "deleted" })
                {
                    definitions[original.Id] = original; LevelDefinition guardLevel = Fixture(1, mode == "wall");
                    try
                    {
                        var guard = Case(guardLevel, mode); string stateBefore = Snapshot(guard.state), contextBefore = Context(guard.context);
                        definitions[original.Id] = Define(original, null); DamageReaction guarded = Query(guard, DamageCause.MagnetAdjacent);
                        Check(guarded.Response == (mode == "wall" ? DamageResponse.Wall : DamageResponse.None) && guarded.Amount == 0 && stateBefore == Snapshot(guard.state) && contextBefore == Context(guard.context), "누락이어도 선행 거부/조회 무변경 " + mode);
                        Connections.Add(RowText("missing-guard", mode, Snapshot(guarded) + ";state=" + stateBefore + ";context=" + contextBefore));
                    }
                    finally { definitions[original.Id] = original; UnityEngine.Object.DestroyImmediate(guardLevel); Check(ReferenceEquals(LegacyElementDefinitions.Get(ObstacleKind.Safe), original), "finally 누락 선행 등록 복원"); }
                }
            }
            finally { definitions[original.Id] = original; UnityEngine.Object.DestroyImmediate(level); Check(ReferenceEquals(LegacyElementDefinitions.Get(ObstacleKind.Safe), original), "finally 누락 등록 참조 복원"); }
        }
        private static void ColorChecks()
        {
            ElementDefinition original = LegacyElementDefinitions.Get(ObstacleKind.Safe); Dictionary<ElementId, ElementDefinition> definitions = Definitions();
            for (int durability = 1; durability <= 5; durability++)
            {
                LevelDefinition level = Fixture(durability, false);
                try
                {
                    var item = Case(level, "fresh"); string initial = Snapshot(item.state), turn = Context(item.context), random = JsonUtility.ToJson(UnityEngine.Random.state);
                    definitions[original.Id] = Define(original, Policy(original, true));
                    foreach (RabbitColor? color in Enum.GetValues(typeof(RabbitColor)).Cast<RabbitColor>().Select(color => (RabbitColor?)color).Concat(new RabbitColor?[] { null }))
                    {
                        DamageReaction reaction = DamageReaction.Evaluate(item.state, item.target, DamageCause.MagnetAdjacent, item.source, item.context, color, 7);
                        Check(reaction.Response == DamageResponse.Damage && reaction.Amount == 1 && initial == Snapshot(item.state) && turn == Context(item.context) && random == JsonUtility.ToJson(UnityEngine.Random.state), "Safe는 자석 인접에 색 비교 추가 없이 피해 " + durability + color);
                        Connections.Add(RowText("color-independent", durability + "/color=" + color, Snapshot(reaction) + ";state=" + initial + ";context=" + turn));
                    }
                }
                finally { definitions[original.Id] = original; UnityEngine.Object.DestroyImmediate(level); Check(ReferenceEquals(LegacyElementDefinitions.Get(ObstacleKind.Safe), original), "finally 색 독립 등록 복원"); }
            }
        }
        private static LevelDefinition OtherFixture(ObstacleKind kind)
        {
            string path = Evidence + "/other-" + kind + ".json"; LevelDefinition level = ScriptableObject.CreateInstance<LevelDefinition>();
            if (File.Exists(path)) { JsonUtility.FromJsonOverwrite(File.ReadAllText(path), level); return level; }
            LevelDefinition source = kind == ObstacleKind.Generator ? (LevelDefinition)Invoke(typeof(GeneratorVerification), "Make", ObstacleKind.Appliance, 3) : (LevelDefinition)Invoke(typeof(PowerEffectVerification), "Make");
            try { JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source), level); }
            finally { UnityEngine.Object.DestroyImmediate(source); }
            if (kind != ObstacleKind.Generator)
            {
                foreach (BoardCoordinate cell in new[] { C(4, 4), C(1, 1) }) Invoke(typeof(FixedObstacleVerification), "Obstacle", level, kind, 1, cell, RabbitColor.Type1);
                JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":0,\"count\":1,\"color\":0}]}", level);
            }
            Check(LevelDefinitionValidator.Validate(level).Count == 0, "다른 종류 유효 저장 입력 " + kind);
            File.WriteAllText(path, JsonUtility.ToJson(level)); return level;
        }
        private static void OtherChecks()
        {
            ElementDefinition original = LegacyElementDefinitions.Get(ObstacleKind.Safe); Dictionary<ElementId, ElementDefinition> definitions = Definitions();
            foreach (ObstacleKind kind in new[] { ObstacleKind.Crate, ObstacleKind.Scrap, ObstacleKind.ColorLock, ObstacleKind.Appliance, ObstacleKind.Generator })
                foreach (string mode in Modes)
                {
                    LevelDefinition level = OtherFixture(kind);
                    try
                    {
                        if (mode == "wall") Check(LevelFlowEditing.SetWalls(level, new[] { new BoardEdge(C(3, 4), C(4, 4)) }, false) == null, "다른 종류 벽 준비");
                        var item = Case(level, mode);
                        if (mode == "second-body" && kind == ObstacleKind.Generator) { item.target = C(4, 7); item.source = C(3, 7); }
                        string initial = Snapshot(item.state), turn = Context(item.context), random = JsonUtility.ToJson(UnityEngine.Random.state);
                        DamageReaction[] before = Causes.Select(cause => Query(item, cause)).ToArray();
                        definitions[original.Id] = Define(original, Policy(original, true));
                        DamageReaction[] allowed = Causes.Select(cause => Query(item, cause)).ToArray();
                        Check(Snapshot(before) == Snapshot(allowed) && initial == Snapshot(item.state) && turn == Context(item.context) && random == JsonUtility.ToJson(UnityEngine.Random.state), "Safe 교체가 다른 종류/원인/메시지에 영향 없음 " + kind + mode);
                        for (int i = 0; i < Causes.Length; i++) Values.Add(RowText("other-magnet", kind + "/" + mode + "/cause=" + (int)Causes[i], "before=" + Snapshot(before[i]) + ";allowed=" + Snapshot(allowed[i]) + ";state=" + initial + ";context=" + turn));
                    }
                    finally { definitions[original.Id] = original; UnityEngine.Object.DestroyImmediate(level); Check(ReferenceEquals(LegacyElementDefinitions.Get(ObstacleKind.Safe), original), "finally 다른 종류 등록 복원"); }
                }
            foreach (RuntimeContent content in Enum.GetValues(typeof(RuntimeContent)).Cast<RuntimeContent>().Where(content => content != RuntimeContent.Obstacle))
                foreach (string mode in new[] { "fresh", "web", "mold", "protected" })
                {
                    LevelDefinition level = Fixture(1, false);
                    try
                    {
                        var item = Case(level, "deleted"); Set(item.state.CellAt(item.target), "Content", content); Set(item.state.CellAt(item.target), "Color", (RabbitColor?)RabbitColor.Type1);
                        if (mode == "web" || mode == "mold") { Set(item.state.CellAt(item.target), "Cover", (CoverKind?)(mode == "web" ? CoverKind.Web : CoverKind.Mold)); Set(item.state.CellAt(item.target), "CoverDurability", 1); }
                        if (mode == "protected") ((HashSet<BoardCoordinate>)typeof(TurnEffectContext).GetField("protectedPowers", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(item.context)).Add(item.target);
                        string initial = Snapshot(item.state), turn = Context(item.context), random = JsonUtility.ToJson(UnityEngine.Random.state);
                        DamageReaction[] before = Causes.Select(cause => Query(item, cause)).ToArray();
                        definitions[original.Id] = Define(original, Policy(original, true)); DamageReaction[] allowed = Causes.Select(cause => Query(item, cause)).ToArray();
                        Check(Snapshot(before) == Snapshot(allowed) && initial == Snapshot(item.state) && turn == Context(item.context) && random == JsonUtility.ToJson(UnityEngine.Random.state), "비장애물 자석 제한/모든 원인/메시지 보존 " + content + mode);
                        for (int i = 0; i < Causes.Length; i++) Values.Add(RowText("non-obstacle", content + "/" + mode + "/cause=" + (int)Causes[i], "before=" + Snapshot(before[i]) + ";allowed=" + Snapshot(allowed[i]) + ";state=" + initial + ";context=" + turn));
                    }
                    finally { definitions[original.Id] = original; UnityEngine.Object.DestroyImmediate(level); Check(ReferenceEquals(LegacyElementDefinitions.Get(ObstacleKind.Safe), original), "finally 비장애물 등록 복원"); }
                }
        }
        private static void ExistingChecks()
        {
            Type previous = typeof(CapsuleAdjacentPolicyVerification);
            List<string> results = (List<string>)previous.GetField("Results", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            List<string> values = (List<string>)previous.GetField("Values", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            results.Clear(); values.Clear();
            Invoke(previous, "DefaultChecks"); Invoke(previous, "MatchChecks", false); Invoke(previous, "ExistingChecks");
            Results.AddRange(results); Values.AddRange(values);
        }
    }
}
