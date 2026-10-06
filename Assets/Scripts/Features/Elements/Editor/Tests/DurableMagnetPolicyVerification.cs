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
    /// <summary>세 내구도형의 자석 정책 연결을 실제 반응과 공통 실행으로 검사한다.</summary>
    public static class DurableMagnetPolicyVerification
    {
        private const string Evidence = "Logs/ElementFramework/Stage32";
        private static readonly ObstacleKind[] Kinds = { ObstacleKind.Crate, ObstacleKind.Scrap, ObstacleKind.Appliance };
        private static readonly string[] Modes = { "fresh", "damaged", "other-hit", "next", "complete", "null", "wall", "far", "inactive", "deleted", "web", "mold", "protected", "second-body" };
        private static readonly DamageCause[] Causes = { DamageCause.MagnetAdjacent, DamageCause.AdjacentMatch, DamageCause.Power, DamageCause.Hammer, (DamageCause)(-1), (DamageCause)4, (DamageCause)99 };
        private static readonly List<string> Results = new List<string>();
        private static readonly List<string> Values = new List<string>();
        private static readonly List<string> Connections = new List<string>();
        private static BoardCoordinate C(int row, int column) => new BoardCoordinate(row, column);
        private static object Invoke(Type type, string method, params object[] args) => type.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
        private static string Snapshot(object value) => (string)Invoke(typeof(LevelInitialStateVerification), "Snapshot", value);
        private static string Context(TurnEffectContext value) => (string)Invoke(typeof(DamageAggregationPolicyVerification), "Context", value);
        private static LevelRuntimeState Build(LevelDefinition level) => (LevelRuntimeState)Invoke(typeof(FixedObstacleVerification), "Build", level, 12345);
        private static TurnEffectContext Fresh() => (TurnEffectContext)Invoke(typeof(FixedObstacleVerification), "Context");
        private static Dictionary<ElementId, ElementDefinition> Definitions() => (Dictionary<ElementId, ElementDefinition>)Invoke(typeof(CapsuleMagnetPolicyVerification), "Definitions");
        private static ElementDefinition Define(ElementDefinition original, ElementDamageSourcePolicy policy) => (ElementDefinition)Invoke(typeof(CapsuleMagnetPolicyVerification), "Define", original, policy);
        private static ElementDamageSourcePolicy Policy(ElementDefinition original, bool magnet) => (ElementDamageSourcePolicy)Invoke(typeof(CapsuleMagnetPolicyVerification), "Policy", original, magnet);
        private static void Check(bool pass, string name) { if (!pass) throw new InvalidOperationException(name); Results.Add("PASS " + name); }
        [Serializable] private sealed class Row { public string name, input, output; }
        private static string RowText(string name, string input, string output) => JsonUtility.ToJson(new Row { name = name, input = input, output = output });
        private static void Set(object owner, string property, object value) => owner.GetType().GetProperty(property).GetSetMethod(true).Invoke(owner, new[] { value });
        private static void Register(TurnEffectContext context, string method, params object[] args) => typeof(TurnEffectContext).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(context, args);
        private static bool HasHit(TurnEffectContext context, EffectRecord effect) => (bool)typeof(TurnEffectContext).GetMethod("HasHit", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(context, new object[] { effect.HitGroup, effect.Target });
        private static void Place(LevelDefinition level, BoardCoordinate cell, InitialBlockKind kind, RabbitColor color) => Invoke(typeof(PowerEffectVerification), "Place", level, cell, kind, RocketDirection.Horizontal, color);
        private static LevelDefinition Fixture(ObstacleKind kind, int durability, bool wall, bool magnet = false, bool repeat = false)
        {
            string path = Evidence + "/" + (repeat ? "repeat" : magnet ? "magnet" : "input") + "-" + kind + "-" + durability + "-" + wall + ".json";
            LevelDefinition level = ScriptableObject.CreateInstance<LevelDefinition>();
            if (File.Exists(path)) { JsonUtility.FromJsonOverwrite(File.ReadAllText(path), level); return level; }
            LevelDefinition source = (LevelDefinition)Invoke(typeof(PowerEffectVerification), "Make");
            try { JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source), level); }
            finally { UnityEngine.Object.DestroyImmediate(source); }
            if (magnet)
            {
                Place(level, C(7, 0), InitialBlockKind.Magnet, RabbitColor.Type1);
                foreach (BoardCoordinate cell in new[] { C(7, 1), C(3, 4), C(4, 3), C(1, 0) }) Place(level, cell, InitialBlockKind.FixedNormal, RabbitColor.Type1);
                if (kind == ObstacleKind.Appliance)
                {
                    foreach (BoardCoordinate cell in new[] { C(3, 5), C(6, 4), C(6, 5) }) Place(level, cell, InitialBlockKind.FixedNormal, RabbitColor.Type1);
                    Place(level, C(6, 3), InitialBlockKind.FixedNormal, RabbitColor.Type3);
                }
                if (repeat)
                {
                    Place(level, C(8, 8), InitialBlockKind.Magnet, RabbitColor.Type1);
                    foreach (BoardCoordinate cell in new[] { C(4, 3), C(4, 6), C(5, 3), C(5, 6) }) Place(level, cell, InitialBlockKind.FixedNormal, RabbitColor.Type2);
                }
            }
            foreach (BoardCoordinate target in new[] { C(4, 4), C(1, 1) }) Invoke(typeof(FixedObstacleVerification), "Obstacle", level, kind, durability, target, RabbitColor.Type1);
            JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":" + (int)LegacyElementDefinitions.Get(kind).RequireRemovalMissionProfile().Kind + ",\"count\":2}]}", level);
            if (wall)
            {
                List<BoardEdge> walls = new List<BoardEdge> { new BoardEdge(C(3, 4), C(4, 4)) };
                if (magnet) walls.Add(new BoardEdge(C(4, 3), C(4, 4)));
                Check(LevelFlowEditing.SetWalls(level, walls, false) == null, "인접 벽 준비");
            }
            Check(LevelDefinitionValidator.Validate(level).Count == 0, "유효 본체/제거 미션 저장 입력 " + path);
            File.WriteAllText(path, JsonUtility.ToJson(level)); return level;
        }
        private static (LevelRuntimeState state, TurnEffectContext context, BoardCoordinate target, BoardCoordinate source, int hit) Case(LevelDefinition level, ObstacleKind kind, string mode)
        {
            LevelRuntimeState state = Build(level); TurnEffectContext context = Fresh();
            BoardCoordinate target = C(4, 4), source = mode == "far" ? C(2, 4) : C(3, 4); int hit = mode == "other-hit" ? 8 : 7;
            if (mode == "damaged" || mode == "other-hit" || mode == "next" || mode == "second-body")
            {
                if (kind == ObstacleKind.Appliance) Register(context, "RegisterHit", 7, target);
                else Register(context, "RegisterDamage", 0);
            }
            if (mode == "next") context = (TurnEffectContext)Invoke(typeof(GeneratorVerification), "Next", context, 2);
            if (mode == "second-body") { target = C(1, 1); source = C(1, 0); }
            if (mode == "complete") Set(state.Missions[0], "Progress", 2);
            if (mode == "inactive") typeof(RuntimeCell).GetField("<IsActive>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(state.CellAt(target), false);
            if (mode == "deleted") Invoke(typeof(DamageReaction).Assembly.GetType("Simulation.ObstacleDamageRules"), "Remove", state, 0);
            if (mode == "web" || mode == "mold") { Set(state.CellAt(target), "Cover", (CoverKind?)(mode == "web" ? CoverKind.Web : CoverKind.Mold)); Set(state.CellAt(target), "CoverDurability", 1); }
            if (mode == "protected") ((HashSet<BoardCoordinate>)typeof(TurnEffectContext).GetField("protectedPowers", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(context)).Add(target);
            if (mode == "null") context = null;
            return (state, context, target, source, hit);
        }
        private static DamageReaction Query((LevelRuntimeState state, TurnEffectContext context, BoardCoordinate target, BoardCoordinate source, int hit) item, DamageCause cause)
            => DamageReaction.Evaluate(item.state, item.target, cause, item.source, item.context, RabbitColor.Type1, item.hit);
        private static void DefaultChecks()
        {
            foreach (ObstacleKind kind in Kinds)
                for (int durability = 1; durability <= LegacyElementDefinitions.Get(kind).Placement.MaxDurability; durability++)
                    foreach (string mode in Modes)
                    {
                        LevelDefinition level = Fixture(kind, durability, mode == "wall");
                        try
                        {
                            foreach (DamageCause cause in Causes)
                            {
                                var item = Case(level, kind, mode); string initial = Snapshot(item.state), turn = Context(item.context), random = JsonUtility.ToJson(UnityEngine.Random.state), input = JsonUtility.ToJson(level);
                                DamageReaction reaction = Query(item, cause);
                                Check(initial == Snapshot(item.state) && turn == Context(item.context) && input == JsonUtility.ToJson(level) && random == JsonUtility.ToJson(UnityEngine.Random.state), "기본 전체 조회 무변경 " + kind + durability + mode + cause);
                                Values.Add(RowText("durable-magnet-default", kind + "/" + durability + "/" + mode + "/" + (int)cause, "query=" + Snapshot(reaction) + ";state=" + initial + ";context=" + turn + ";input=" + input));
                            }
                        }
                        finally { UnityEngine.Object.DestroyImmediate(level); }
                    }
        }
        private static void BoundaryConnections()
        {
            foreach (ObstacleKind kind in Kinds)
            {
                ElementDefinition original = LegacyElementDefinitions.Get(kind); Dictionary<ElementId, ElementDefinition> definitions = Definitions();
                for (int durability = 1; durability <= original.Placement.MaxDurability; durability++)
                    foreach (string mode in Modes)
                    {
                        LevelDefinition level = Fixture(kind, durability, mode == "wall");
                        try
                        {
                            var item = Case(level, kind, mode); string initial = Snapshot(item.state), turn = Context(item.context), random = JsonUtility.ToJson(UnityEngine.Random.state);
                            DamageReaction[] before = Causes.Select(cause => Query(item, cause)).ToArray();
                            definitions[original.Id] = Define(original, Policy(original, true));
                            DamageReaction[] allowed = Causes.Select(cause => Query(item, cause)).ToArray();
                            DamageResponse expected = mode == "wall" ? DamageResponse.Wall : mode == "far" || mode == "inactive" || mode == "deleted" ? DamageResponse.None : mode == "web" || mode == "mold" ? DamageResponse.CoverDamage : mode == "protected" ? DamageResponse.Protected : mode == "damaged" || (mode == "other-hit" && kind != ObstacleKind.Appliance) ? DamageResponse.AlreadyDamaged : DamageResponse.Damage;
                            Check(allowed[0].Response == expected && allowed[0].Amount == (expected == DamageResponse.Damage || expected == DamageResponse.CoverDamage ? 1 : 0), "허용 전체 경계 " + kind + durability + mode);
                            for (int i = 1; i < Causes.Length; i++) Check(Snapshot(before[i]) == Snapshot(allowed[i]), "다른 원인 전체 응답 보존 " + kind + (int)Causes[i]);
                            definitions[original.Id] = Define(original, Policy(original, false));
                            DamageReaction[] denied = Causes.Select(cause => Query(item, cause)).ToArray();
                            Check(Snapshot(before) == Snapshot(denied) && initial == Snapshot(item.state) && turn == Context(item.context) && random == JsonUtility.ToJson(UnityEngine.Random.state), "false true false 조회/전체 응답 보존 " + kind);
                            Connections.Add(RowText("durable-magnet-boundary", kind + "/" + durability + "/" + mode, "before=" + Snapshot(before) + ";allowed=" + Snapshot(allowed) + ";denied=" + Snapshot(denied) + ";state=" + initial + ";context=" + turn));
                        }
                        finally { definitions[original.Id] = original; UnityEngine.Object.DestroyImmediate(level); Check(ReferenceEquals(LegacyElementDefinitions.Get(kind), original), "finally 경계 원래 등록 복원 " + kind); }
                    }
            }
        }
        private static void MagnetChecks(bool connected)
        {
            foreach (ObstacleKind kind in Kinds)
            {
                ElementDefinition original = LegacyElementDefinitions.Get(kind); Dictionary<ElementId, ElementDefinition> definitions = Definitions();
                for (int durability = 1; durability <= original.Placement.MaxDurability; durability++)
                    foreach (bool wall in new[] { false, true })
                    {
                        LevelDefinition level = Fixture(kind, durability, wall, true);
                        try
                        {
                            LevelRuntimeState state = Build(level); string input = JsonUtility.ToJson(level), random = JsonUtility.ToJson(UnityEngine.Random.state), ruleRandom = Snapshot(state.Random);
                            if (connected) definitions[original.Id] = Define(original, Policy(original, true));
                            BoardActionExecutor executor = new BoardActionExecutor(state); BoardActionResult result = executor.Swap(C(7, 0), C(7, 1));
                            int firstHits = connected ? kind == ObstacleKind.Appliance ? (wall ? 3 : 4) : wall ? 0 : 1 : 0;
                            int secondHits = connected ? kind == ObstacleKind.Appliance ? 2 : 1 : 0;
                            int first = Math.Max(0, durability - firstHits), second = Math.Max(0, durability - secondHits);
                            if (!result.IsApplied || executor.State.Obstacles[0].Durability != first || executor.State.Obstacles[1].Durability != second)
                                File.WriteAllText(Evidence + "/magnet-failure.json", RowText("actual-magnet-failure", input, "state=" + Snapshot(executor.State) + ";result=" + Snapshot(result) + ";context=" + Context(executor.TurnEffects)));
                            Check(result.IsApplied && executor.State.Obstacles[0].Durability == first && executor.State.Obstacles[1].Durability == second, "실제 자석 Swap/소비/공통 적용 " + kind + durability + wall + connected + " actual=" + result.Reason + "/" + executor.State.Obstacles[0].Durability + "/" + executor.State.Obstacles[1].Durability + " want=Applied/" + first + "/" + second);
                            Check(result.Effects.Any(effect => effect.Content == RuntimeContent.Magnet && effect.Response == DamageResponse.Activate) && result.Effects.Any(effect => effect.Content == RuntimeContent.Normal && effect.Response == DamageResponse.Remove), "실제 자석 발동/일반 블록 소비 " + kind);
                            EffectRecord[] damage = result.Effects.Where(effect => effect.Response == DamageResponse.Damage).ToArray();
                            Check(damage.Length == Math.Min(durability, firstHits) + Math.Min(durability, secondHits) && damage.All(effect => effect.Cause == DamageCause.MagnetAdjacent && effect.DurabilityAfter == effect.DurabilityBefore - 1), "실제 중첩 피해 수/원인/내구도 전후 " + kind);
                            foreach (IGrouping<int, EffectRecord> body in damage.GroupBy(effect => effect.Target.Row <= 2 ? 1 : 0))
                                Check(body.Select(effect => effect.DurabilityBefore).SequenceEqual(Enumerable.Range(0, body.Count()).Select(index => durability - index)), "본체별 실제 감소 순서 " + kind);
                            if (kind == ObstacleKind.Appliance)
                                Check(!executor.TurnEffects.HasDamaged(0) && !executor.TurnEffects.HasDamaged(1) && damage.All(effect => HasHit(executor.TurnEffects, effect)) && damage.Select(effect => (effect.HitGroup, effect.Target)).Distinct().Count() == damage.Length, "실제 칸/hit 기록·같은칸 중복 없음");
                            else Check(executor.TurnEffects.HasDamaged(0) == (firstHits > 0) && executor.TurnEffects.HasDamaged(1) == (secondHits > 0), "실제 본체별 턴 기록 " + kind);
                            if (connected && !wall && durability > (kind == ObstacleKind.Appliance ? 2 : 1))
                                Check(result.Effects.Any(effect => effect.Cause == DamageCause.MagnetAdjacent && effect.Response == DamageResponse.AlreadyDamaged && effect.Target.Equals(C(4, 4))), "실제 중복 소비 인접은 이미 피해로 거부 " + kind);
                            int removed = (first == 0 ? 1 : 0) + (second == 0 ? 1 : 0);
                            Check(executor.State.Missions[0].Progress == removed && damage.SelectMany(effect => effect.RemovedObstacleIndices).Count() == removed, "제거 직후 후속 인접/미션/제거 효과 중복 없음 " + kind);
                            Check(input == JsonUtility.ToJson(level) && random == JsonUtility.ToJson(UnityEngine.Random.state) && ruleRandom == Snapshot(executor.State.Random), "실제 적용 원본/규칙·전역 난수 보존 " + kind);
                            string output = "state=" + Snapshot(executor.State) + ";result=" + Snapshot(result) + ";context=" + Context(executor.TurnEffects);
                            if (connected) Connections.Add(RowText("actual-durable-magnet", kind + "/" + durability + "/wall=" + wall, output));
                            else Values.Add(RowText("default-durable-magnet", input, output));
                        }
                        finally { definitions[original.Id] = original; UnityEngine.Object.DestroyImmediate(level); Check(ReferenceEquals(LegacyElementDefinitions.Get(kind), original), "finally 실제 자석 등록 복원 " + kind); }
                    }
            }
        }
        private static void RepeatChecks(bool connected)
        {
            foreach (ObstacleKind kind in Kinds)
            {
                ElementDefinition original = LegacyElementDefinitions.Get(kind); Dictionary<ElementId, ElementDefinition> definitions = Definitions(); int durability = original.Placement.MaxDurability;
                LevelDefinition level = Fixture(kind, durability, false, true, true);
                try
                {
                    LevelRuntimeState state = Build(level); string input = JsonUtility.ToJson(level), random = JsonUtility.ToJson(UnityEngine.Random.state), ruleRandom = Snapshot(state.Random);
                    if (connected) definitions[original.Id] = Define(original, Policy(original, true));
                    BoardActionExecutor executor = new BoardActionExecutor(state); BoardActionResult first = executor.Swap(C(7, 0), C(7, 1));
                    Check(first.IsApplied && executor.State.CellAt(C(8, 8)).Content == RuntimeContent.Magnet, "첫 실제 Swap 후 두 번째 자석 보존 " + kind);
                    List<EffectRecord> second = new List<EffectRecord>(); object[] args = { executor.State, Array.Empty<MatchedBlockChange>(), (BoardCoordinate?)C(8, 8), executor.TurnEffects, second, null, (RabbitColor?)RabbitColor.Type2 };
                    bool applied = (bool)typeof(PowerEffectResolution).GetMethod("ApplyWithColor", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
                    Check(applied && args[5] == null && second.Any(effect => effect.Content == RuntimeContent.Magnet && effect.Response == DamageResponse.Activate) && second.Any(effect => effect.Content == RuntimeContent.Normal && effect.Response == DamageResponse.Remove), "같은 턴 두 번째 실제 자석 소비/공통 효과 실행 " + kind);
                    EffectRecord[] a = first.Effects.Where(effect => effect.Response == DamageResponse.Damage && effect.Target.Row >= 4).ToArray(), b = second.Where(effect => effect.Response == DamageResponse.Damage && effect.Target.Row >= 4).ToArray();
                    int expected = connected ? kind == ObstacleKind.Appliance ? 4 : 1 : 0;
                    Check(a.Length == expected && b.Length == (connected && kind == ObstacleKind.Appliance ? 4 : 0) && executor.State.Obstacles[0].Durability == durability - a.Length - b.Length, "실제 다른hit 같은칸·본체 턴 제한 " + kind);
                    if (connected && kind == ObstacleKind.Appliance)
                        Check(a.Select(effect => effect.Target).OrderBy(cell => cell.Row * 9 + cell.Column).SequenceEqual(b.Select(effect => effect.Target).OrderBy(cell => cell.Row * 9 + cell.Column)) && a.Select(effect => effect.HitGroup).Distinct().Count() == 1 && b.Select(effect => effect.HitGroup).Distinct().Count() == 1 && a[0].HitGroup != b[0].HitGroup && b.All(effect => effect.Cause == DamageCause.MagnetAdjacent && HasHit(executor.TurnEffects, effect)), "실제 2×2 동일 네 칸에 다른hit 재피해");
                    if (connected && kind != ObstacleKind.Appliance) Check(second.Any(effect => effect.Target.Equals(C(4, 4)) && effect.Response == DamageResponse.AlreadyDamaged), "다른 자석hit도 본체별 턴1회 " + kind);
                    Check(input == JsonUtility.ToJson(level) && random == JsonUtility.ToJson(UnityEngine.Random.state) && ruleRandom == Snapshot(executor.State.Random), "두 실제 소비 원본/난수 보존 " + kind);
                    string output = "state=" + Snapshot(executor.State) + ";first=" + Snapshot(first) + ";second=" + Snapshot(second) + ";context=" + Context(executor.TurnEffects);
                    if (connected) Connections.Add(RowText("actual-repeat-magnet", kind.ToString(), output));
                    else Values.Add(RowText("default-repeat-magnet", input, output));
                }
                finally { definitions[original.Id] = original; UnityEngine.Object.DestroyImmediate(level); Check(ReferenceEquals(LegacyElementDefinitions.Get(kind), original), "finally 반복 등록 복원 " + kind); }
            }
        }
        private static void ColorChecks()
        {
            foreach (ObstacleKind kind in Kinds)
            {
                ElementDefinition original = LegacyElementDefinitions.Get(kind); Dictionary<ElementId, ElementDefinition> definitions = Definitions();
                for (int durability = 1; durability <= original.Placement.MaxDurability; durability++)
                {
                    LevelDefinition level = Fixture(kind, durability, false);
                    try
                    {
                        foreach (RabbitColor? color in Enum.GetValues(typeof(RabbitColor)).Cast<RabbitColor>().Select(color => (RabbitColor?)color).Concat(new RabbitColor?[] { null }))
                        {
                            var item = Case(level, kind, "fresh"); Set(item.state.CellAt(item.source), "Color", color);
                            string initial = Snapshot(item.state), turn = Context(item.context), random = JsonUtility.ToJson(UnityEngine.Random.state);
                            definitions[original.Id] = Define(original, Policy(original, true));
                            DamageReaction reaction = DamageReaction.Evaluate(item.state, item.target, DamageCause.MagnetAdjacent, item.source, item.context, color, item.hit);
                            Check(reaction.Response == DamageResponse.Damage && reaction.Amount == 1 && initial == Snapshot(item.state) && turn == Context(item.context) && random == JsonUtility.ToJson(UnityEngine.Random.state), "색/null 조건 추가 없는 실제 인접 " + kind + durability + color);
                            Connections.Add(RowText("durable-color-independent", kind + "/" + durability + "/" + color, Snapshot(reaction) + ";state=" + initial + ";context=" + turn));
                            definitions[original.Id] = original;
                        }
                    }
                    finally { definitions[original.Id] = original; UnityEngine.Object.DestroyImmediate(level); Check(ReferenceEquals(LegacyElementDefinitions.Get(kind), original), "finally 색 등록 복원 " + kind); }
                }
            }
        }
        private static void MissingChecks(bool connected)
        {
            foreach (ObstacleKind kind in Kinds)
            {
                ElementDefinition original = LegacyElementDefinitions.Get(kind); Dictionary<ElementId, ElementDefinition> definitions = Definitions();
                foreach (string mode in new[] { "fresh", "wall", "far", "inactive", "deleted" })
                {
                    LevelDefinition level = Fixture(kind, 1, mode == "wall");
                    try
                    {
                        var item = Case(level, kind, mode); string initial = Snapshot(item.state), turn = Context(item.context), random = JsonUtility.ToJson(UnityEngine.Random.state);
                        definitions[original.Id] = Define(original, null); Exception error = null; DamageReaction reaction = null;
                        try { reaction = Query(item, DamageCause.MagnetAdjacent); } catch (InvalidOperationException found) { error = found; }
                        if (connected && mode == "fresh") Check(error is InvalidOperationException && error.Message.Contains(original.Id.Value), "유효 누락ID 오류/false 대체 없음 " + kind);
                        else Check(error == null && reaction.Response == (mode == "wall" ? DamageResponse.Wall : DamageResponse.None) && reaction.Amount == 0, "누락 전단/선행 거부 보존 " + kind + mode);
                        Check(initial == Snapshot(item.state) && turn == Context(item.context) && random == JsonUtility.ToJson(UnityEngine.Random.state), "누락 조회 무변경 " + kind + mode);
                        Connections.Add(RowText("durable-missing-source", kind + "/" + mode, error == null ? Snapshot(reaction) : error.Message));
                    }
                    finally { definitions[original.Id] = original; UnityEngine.Object.DestroyImmediate(level); Check(ReferenceEquals(LegacyElementDefinitions.Get(kind), original), "finally 누락 등록 복원 " + kind); }
                }
            }
        }
        private static LevelDefinition OtherGeneratorFixture(ObstacleKind changed)
        {
            string path = Evidence + "/other-generator-for-" + changed + ".json"; LevelDefinition level = ScriptableObject.CreateInstance<LevelDefinition>();
            if (File.Exists(path)) { JsonUtility.FromJsonOverwrite(File.ReadAllText(path), level); return level; }
            LevelDefinition source = (LevelDefinition)Invoke(typeof(GeneratorVerification), "Make", changed == ObstacleKind.Appliance ? ObstacleKind.Crate : ObstacleKind.Appliance, 3);
            try { JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source), level); }
            finally { UnityEngine.Object.DestroyImmediate(source); }
            Check(LevelDefinitionValidator.Validate(level).Count == 0, "다른 발전기/연결 대상 유효 입력 " + changed);
            File.WriteAllText(path, JsonUtility.ToJson(level)); return level;
        }
        private static void OtherConnections()
        {
            foreach (ObstacleKind changed in Kinds)
            {
                ElementDefinition original = LegacyElementDefinitions.Get(changed); Dictionary<ElementId, ElementDefinition> definitions = Definitions();
                foreach (ObstacleKind other in Enum.GetValues(typeof(ObstacleKind)).Cast<ObstacleKind>().Where(other => other != changed))
                    foreach (string mode in Modes.Where(mode => mode != "other-hit"))
                    {
                        LevelDefinition level = other == ObstacleKind.Generator ? OtherGeneratorFixture(changed) : Fixture(other, 1, mode == "wall");
                        try
                        {
                            if (other == ObstacleKind.Generator && mode == "wall") Check(LevelFlowEditing.SetWalls(level, new[] { new BoardEdge(C(3, 4), C(4, 4)) }, false) == null, "발전기 벽 입력");
                            var item = Case(level, other, mode);
                            if (other == ObstacleKind.Generator && mode == "second-body") { item.target = C(4, 7); item.source = C(3, 7); }
                            string initial = Snapshot(item.state), turn = Context(item.context), random = JsonUtility.ToJson(UnityEngine.Random.state);
                            DamageReaction[] before = Causes.Select(cause => Query(item, cause)).ToArray();
                            definitions[original.Id] = Define(original, Policy(original, true));
                            DamageReaction[] after = Causes.Select(cause => Query(item, cause)).ToArray();
                            Check(Snapshot(before) == Snapshot(after) && initial == Snapshot(item.state) && turn == Context(item.context) && random == JsonUtility.ToJson(UnityEngine.Random.state), "다른 종류 전체 반응/조회 보존 " + changed + other + mode);
                            for (int i = 0; i < Causes.Length; i++) Connections.Add(RowText("durable-unaffected-kind", changed + "/" + other + "/" + mode + "/" + (int)Causes[i], "before=" + Snapshot(before[i]) + ";after=" + Snapshot(after[i]) + ";state=" + initial + ";context=" + turn));
                        }
                        finally { definitions[original.Id] = original; UnityEngine.Object.DestroyImmediate(level); Check(ReferenceEquals(LegacyElementDefinitions.Get(changed), original), "finally 다른 종류 등록 복원"); }
                    }
                foreach (RuntimeContent content in Enum.GetValues(typeof(RuntimeContent)).Cast<RuntimeContent>().Where(content => content != RuntimeContent.Obstacle))
                    foreach (string mode in new[] { "fresh", "web", "mold", "protected" })
                    {
                        LevelDefinition level = Fixture(ObstacleKind.Safe, 1, false);
                        try
                        {
                            var item = Case(level, ObstacleKind.Safe, "deleted"); Set(item.state.CellAt(item.target), "Content", content); Set(item.state.CellAt(item.target), "Color", (RabbitColor?)RabbitColor.Type1);
                            if (mode == "web" || mode == "mold") { Set(item.state.CellAt(item.target), "Cover", (CoverKind?)(mode == "web" ? CoverKind.Web : CoverKind.Mold)); Set(item.state.CellAt(item.target), "CoverDurability", 1); }
                            if (mode == "protected") ((HashSet<BoardCoordinate>)typeof(TurnEffectContext).GetField("protectedPowers", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(item.context)).Add(item.target);
                            string initial = Snapshot(item.state), turn = Context(item.context), random = JsonUtility.ToJson(UnityEngine.Random.state);
                            DamageReaction[] before = Causes.Select(cause => Query(item, cause)).ToArray();
                            definitions[original.Id] = Define(original, Policy(original, true));
                            DamageReaction[] after = Causes.Select(cause => Query(item, cause)).ToArray();
                            Check(Snapshot(before) == Snapshot(after) && initial == Snapshot(item.state) && turn == Context(item.context) && random == JsonUtility.ToJson(UnityEngine.Random.state), "비장애물 전체 반응/조회 보존 " + changed + content + mode);
                            for (int i = 0; i < Causes.Length; i++) Connections.Add(RowText("durable-non-obstacle", changed + "/" + content + "/" + mode + "/" + (int)Causes[i], "before=" + Snapshot(before[i]) + ";after=" + Snapshot(after[i]) + ";state=" + initial + ";context=" + turn));
                        }
                        finally { definitions[original.Id] = original; UnityEngine.Object.DestroyImmediate(level); Check(ReferenceEquals(LegacyElementDefinitions.Get(changed), original), "finally 비장애물 등록 복원"); }
                    }
            }
        }
        private static void ConnectionCheck(ObstacleKind kind)
        {
            ElementDefinition original = LegacyElementDefinitions.Get(kind); Dictionary<ElementId, ElementDefinition> definitions = Definitions(); LevelDefinition level = Fixture(kind, 1, false);
            try
            {
                LevelRuntimeState state = Build(level); TurnEffectContext context = Fresh();
                string initial = Snapshot(state), turn = Context(context), random = JsonUtility.ToJson(UnityEngine.Random.state), input = JsonUtility.ToJson(level);
                Check(!original.DamageSourcePolicy.MagnetAdjacent, "기본 자석 false " + kind);
                ElementDefinition replacement = Define(original, Policy(original, true)); definitions[original.Id] = replacement;
                Check(replacement.DamageSourcePolicy.MagnetAdjacent && replacement.DamageSourcePolicy.AdjacentMatch == original.DamageSourcePolicy.AdjacentMatch && replacement.DamageSourcePolicy.Power == original.DamageSourcePolicy.Power && replacement.DamageSourcePolicy.Hammer == original.DamageSourcePolicy.Hammer, "MagnetAdjacent만 true " + kind);
                Check(replacement.Id == original.Id && replacement.DisplayName == original.DisplayName && ReferenceEquals(replacement.Placement, original.Placement) && ReferenceEquals(replacement.ChargePlacement, original.ChargePlacement) && ReferenceEquals(replacement.ColorMatchPolicy, original.ColorMatchPolicy) && ReferenceEquals(replacement.DamageAggregationPolicy, original.DamageAggregationPolicy) && ReferenceEquals(replacement.RemovalMissionProfile, original.RemovalMissionProfile), "다른 정의/프로필 원래 참조 " + kind);
                DamageReaction reaction = DamageReaction.Evaluate(state, C(4, 4), DamageCause.MagnetAdjacent, C(3, 4), context, RabbitColor.Type1, 7);
                Connections.Add(RowText("same-id-magnet", input, "id=" + original.Id.Value + ";query=" + Snapshot(reaction) + ";state=" + Snapshot(state) + ";context=" + Context(context)));
                Check(initial == Snapshot(state) && turn == Context(context) && input == JsonUtility.ToJson(level) && random == JsonUtility.ToJson(UnityEngine.Random.state), "조회 상태/문맥/입력/난수 무변경 " + kind);
                Check(reaction.Response == DamageResponse.Damage && reaction.Amount == 1, "같은 ID 실제 자석 인접 " + kind + " actual=" + reaction.Response + "/" + reaction.Amount + " want=Damage/1");
            }
            finally { definitions[original.Id] = original; UnityEngine.Object.DestroyImmediate(level); Check(ReferenceEquals(LegacyElementDefinitions.Get(kind), original), "finally 정확한 원래 등록 참조 복원 " + kind); }
        }
        private static int RedChecks()
        {
            int failed = 0;
            foreach (ObstacleKind kind in Kinds)
                try { ConnectionCheck(kind); }
                catch (Exception error) { Results.Add("FAIL " + kind + " " + error); Debug.LogException(error); failed++; }
            return failed == 0 ? 0 : 1;
        }
        private static void ExistingChecks()
        {
            Type previous = typeof(CapsuleMagnetPolicyVerification);
            List<string> results = (List<string>)previous.GetField("Results", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            List<string> values = (List<string>)previous.GetField("Values", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            results.Clear(); values.Clear();
            Invoke(previous, "DefaultChecks"); Invoke(previous, "MagnetChecks", false); Invoke(previous, "OtherChecks"); Invoke(previous, "ExistingChecks");
            Results.AddRange(results); Values.AddRange(values);
        }
        public static void Before() => Execute("before");
        public static void Red() => Execute("red");
        public static void Run() => Execute("after");
        private static void Execute(string mode)
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 Editor에서만 실행한다.");
            Directory.CreateDirectory(Evidence); Results.Clear(); Values.Clear(); Connections.Clear(); int exit = 0;
            try
            {
                if (mode == "red") exit = RedChecks();
                else
                {
                    if (mode == "after") { foreach (ObstacleKind kind in Kinds) ConnectionCheck(kind); BoundaryConnections(); MagnetChecks(true); RepeatChecks(true); ColorChecks(); MissingChecks(true); OtherConnections(); }
                    DefaultChecks(); MagnetChecks(false); RepeatChecks(false);
                    ExistingChecks();
                    if (mode == "before") { MissingChecks(false); OtherConnections(); File.WriteAllLines(Evidence + "/baseline-values.jsonl", Values); }
                    else
                    {
                        Check(RecordedLogicComparison.Equal(File.ReadAllLines(Evidence + "/baseline-values.jsonl"), Values), "기존 논리/입력/팩 비교 · 추가 비행 표시 이력 별도");
                        Func<string, bool> unaffected = line => { string name = JsonUtility.FromJson<Row>(line).name; return name == "durable-unaffected-kind" || name == "durable-non-obstacle"; };
                        Check(File.ReadAllLines(Evidence + "/before-connection-values.jsonl").Where(unaffected).SequenceEqual(Connections.Where(unaffected)), "다른 종류/비장애물 동일 입력 전체 응답 전후 동일");
                    }
                    File.WriteAllText(Evidence + "/definitions-" + mode + ".json", Snapshot(Kinds.Select(kind => LegacyElementDefinitions.Get(kind)).ToArray()));
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
