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
    /// <summary>조회 기여와 실제 제거 완료가 같은 정의의 미션을 사용하는지 검증한다.</summary>
    public static class RemovalMissionProfileVerification
    {
        private const string Evidence = "Logs/ElementFramework/Stage28";
        private static readonly List<string> Results = new List<string>();
        private static readonly List<string> Values = new List<string>();
        private static readonly List<string> Connections = new List<string>();
        private static readonly Type Rules = typeof(DamageReaction).Assembly.GetType("Simulation.ObstacleDamageRules");
        private static readonly ObstacleKind[] Kinds = { ObstacleKind.Crate, ObstacleKind.Scrap, ObstacleKind.Safe, ObstacleKind.ColorLock, ObstacleKind.Appliance };
        private static readonly MissionKind[] Missions = { MissionKind.Crate, MissionKind.Scrap, MissionKind.Safe, MissionKind.ColorLock, MissionKind.Appliance };
        private static BoardCoordinate C(int row, int column) => new BoardCoordinate(row, column);
        private static void Check(bool pass, string name) { if (!pass) throw new InvalidOperationException(name); Results.Add("PASS " + name); }
        private static object Invoke(Type type, string method, params object[] args) => type.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
        private static string Snapshot(object value) => (string)Invoke(typeof(LevelInitialStateVerification), "Snapshot", value);
        private static string Context(TurnEffectContext value) => (string)Invoke(typeof(DamageAggregationPolicyVerification), "Context", value);
        private static LevelRuntimeState Build(LevelDefinition level) => (LevelRuntimeState)Invoke(typeof(FixedObstacleVerification), "Build", level, 12345);
        private static TurnEffectContext Fresh() => (TurnEffectContext)Invoke(typeof(FixedObstacleVerification), "Context");
        private static TurnEffectContext Next(TurnEffectContext context) => (TurnEffectContext)Invoke(typeof(GeneratorVerification), "Next", context, context.Turn + 1);
        private static int Maximum(ObstacleKind kind) => kind == ObstacleKind.Crate ? 6 : kind == ObstacleKind.ColorLock ? 3 : kind == ObstacleKind.Appliance ? 9 : 5;
        [Serializable] private sealed class Row { public string name, input, output; }
        private static string RowText(string name, string input, string output) => JsonUtility.ToJson(new Row { name = name, input = input, output = output });
        private static Type ProfileType => typeof(ElementDefinition).Assembly.GetType("Elements.ElementRemovalMissionProfile");
        private static object Profile(MissionKind kind) => Activator.CreateInstance(ProfileType, new object[] { kind });
        private static object Define(ElementDefinition original, object profile) => Activator.CreateInstance(typeof(ElementDefinition), new object[] { original.Id, original.DisplayName, original.Placement, original.ChargePlacement, original.DamageSourcePolicy, original.ColorMatchPolicy, original.DamageAggregationPolicy, profile, original.ReactionBehavior });
        private static Exception Error(Action operation)
        { try { operation(); return null; } catch (Exception error) { return error is TargetInvocationException ? error.InnerException : error; } }
        private static LevelDefinition Fixture(int index, int durability, bool generator, int required, bool before)
        {
            string path = Evidence + "/input-" + index + "-" + durability + "-" + generator + "-" + required + ".json";
            LevelDefinition level = ScriptableObject.CreateInstance<LevelDefinition>();
            if (before && !File.Exists(path))
            {
                LevelDefinition source = generator ? (LevelDefinition)Invoke(typeof(GeneratorVerification), "Make", Kinds[index], required) : (LevelDefinition)Invoke(typeof(PowerEffectVerification), "Make");
                try { JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source), level); }
                finally { UnityEngine.Object.DestroyImmediate(source); }
                if (!generator) Invoke(typeof(FixedObstacleVerification), "Obstacle", level, Kinds[index], durability, C(4, 4), RabbitColor.Type1);
                int other = index == 0 ? 1 : 0;
                Invoke(typeof(FixedObstacleVerification), "Obstacle", level, Kinds[other], 1, C(1, 1), RabbitColor.Type1);
                JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":" + (int)Missions[index] + ",\"count\":1},{\"kind\":" + (int)Missions[other] + ",\"count\":1}]}", level);
                Check(LevelDefinitionValidator.Validate(level).Count == 0, "양쪽 미션 유효 원본 " + path);
                File.WriteAllText(path, JsonUtility.ToJson(level));
            }
            else JsonUtility.FromJsonOverwrite(File.ReadAllText(path), level);
            return level;
        }
        public static void Before() => Execute("before");
        public static void Contracts() => Execute("contracts");
        public static void Quick() => Execute("quick");
        public static void Run() => Execute("after");
        private static void Execute(string mode)
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 Editor에서만 실행한다.");
            Directory.CreateDirectory(Evidence); Results.Clear(); Values.Clear(); Connections.Clear(); int exit = 0;
            try
            {
                if (mode != "before") ContractChecks();
                if (mode == "after" || mode == "quick") ConnectionChecks();
                if (mode != "contracts" && mode != "quick")
                {
                    DefaultChecks(mode == "before"); ExistingChecks();
                    if (mode == "before") File.WriteAllLines(Evidence + "/baseline-values.jsonl", Values);
                    else Check(RecordedLogicComparison.Equal(File.ReadAllLines(Evidence + "/baseline-values.jsonl"), Values), "기존 논리/입력/팩 비교 · 추가 비행 표시 이력 별도");
                }
                if (mode == "before") File.WriteAllText(Evidence + "/definitions-before.json", Snapshot(Kinds.Select(LegacyElementDefinitions.Get).ToArray()));
                if (mode == "after") File.WriteAllText(Evidence + "/definitions-after.json", Snapshot(Kinds.Select(LegacyElementDefinitions.Get).ToArray()));
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
        private static void ContractChecks()
        {
            Type profile = ProfileType;
            Check(profile != null, "불변 제거 미션 프로필 계약 존재");
            Check(profile.IsSealed && !typeof(UnityEngine.Object).IsAssignableFrom(profile) && profile.GetProperties().All(p => p.SetMethod == null) && profile.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).All(f => f.IsInitOnly && f.FieldType == typeof(MissionKind)), "불변 미션 값만 보유");
            for (int i = 0; i < Kinds.Length; i++)
            {
                object value = Profile(Missions[i]); ElementDefinition original = LegacyElementDefinitions.Get(Kinds[i]);
                Check((MissionKind)profile.GetProperty("Kind").GetValue(value) == Missions[i], "유효 미션 값 " + Missions[i]);
                object defined = Define(original, value);
                Check(ReferenceEquals(typeof(ElementDefinition).GetProperty("RemovalMissionProfile").GetValue(defined), value) && ReferenceEquals(typeof(ElementDefinition).GetMethod("RequireRemovalMissionProfile").Invoke(defined, null), value), "등록 프로필 정확한 참조 " + Kinds[i]);
                object[] arguments = { original.Id, original.DisplayName, original.Placement, original.ChargePlacement, original.DamageSourcePolicy, original.ColorMatchPolicy, original.DamageAggregationPolicy };
                for (int count = 2; count <= 7; count++)
                {
                    ElementDefinition old = (ElementDefinition)Activator.CreateInstance(typeof(ElementDefinition), arguments.Take(count).ToArray());
                    Check(old.Id == original.Id && old.DisplayName == original.DisplayName && ReferenceEquals(old.Placement, count > 2 ? original.Placement : null) && ReferenceEquals(old.ChargePlacement, count > 3 ? original.ChargePlacement : null) && ReferenceEquals(old.DamageSourcePolicy, count > 4 ? original.DamageSourcePolicy : null) && ReferenceEquals(old.ColorMatchPolicy, count > 5 ? original.ColorMatchPolicy : null) && ReferenceEquals(old.DamageAggregationPolicy, count > 6 ? original.DamageAggregationPolicy : null), "기존 생성자 계약 보존 " + Kinds[i] + count);
                    Exception missing = Error(() => typeof(ElementDefinition).GetMethod("RequireRemovalMissionProfile").Invoke(old, null));
                    Check(typeof(ElementDefinition).GetProperty("RemovalMissionProfile").GetValue(old) == null && missing is InvalidOperationException && missing.Message.Contains(original.Id.Value), "기존 생성자 누락 ID 오류 " + Kinds[i] + count);
                }
                Check((MissionKind)profile.GetProperty("Kind").GetValue(typeof(ElementDefinition).GetMethod("RequireRemovalMissionProfile").Invoke(original, null)) == Missions[i], "기존5종 기본 제거 미션 등록 " + Kinds[i]);
            }
            foreach (int invalid in new[] { -1, 0, 2, 4, 8, 9, 99 }) Check(Error(() => Profile((MissionKind)invalid)) is ArgumentOutOfRangeException, "다른 미션/미정의 생성 거절 " + invalid);
            Check(typeof(ElementDefinition).GetProperty("RemovalMissionProfile").GetValue(LegacyElementDefinitions.Get(ObstacleKind.Generator)) == null, "발전기 필수 프로필 없음");
        }
        private static void DefaultChecks(bool before)
        {
            for (int i = 0; i < Kinds.Length; i++)
                for (int durability = 1; durability <= Maximum(Kinds[i]); durability++)
                {
                    LevelDefinition level = Fixture(i, durability, false, 0, before);
                    try
                    {
                        foreach (string mode in new[] { "fresh", "completed", "deleted", "null-context" })
                        {
                            LevelRuntimeState state = Build(level); TurnEffectContext context = Fresh();
                            if (mode == "completed") typeof(RuntimeMission).GetProperty("Progress").GetSetMethod(true).Invoke(state.Missions[0], new object[] { 1 });
                            if (mode == "deleted") Invoke(Rules, "Remove", state, 0);
                            string initial = Snapshot(state), turn = Context(context), random = JsonUtility.ToJson(UnityEngine.Random.state);
                            MissionContribution[] query = MissionProgressRules.Query(state, C(4, 4), mode == "null-context" ? null : context).ToArray();
                            Check(query.Length == (mode == "completed" || mode == "deleted" ? 0 : 1) && (query.Length == 0 || query[0].MissionIndex == 0 && query[0].Damage == 1 && query[0].ExpectedComplete == (durability == 1 ? 1 : 0)), "기본 직접 미션 기여 " + Kinds[i] + durability + mode);
                            Check(initial == Snapshot(state) && turn == Context(context), "미션 조회 무변경");
                            Values.Add(RowText("mission-query", i + "/d=" + durability + "/" + mode, "query=" + Snapshot(query) + ";state=" + initial + ";context=" + turn));
                            if (mode != "deleted")
                                for (int remaining = durability; remaining > 0; remaining--)
                                {
                                    context = Next(context); DamageReaction reaction = DamageReaction.Evaluate(state, C(4, 4), DamageCause.Power, C(4, 4), context, null, 7);
                                    Check(reaction.Response == DamageResponse.Damage, "실제 제거까지 허용");
                                    int after = (int)Invoke(Rules, "Apply", state, state.CellAt(C(4, 4)), context, 7);
                                    Check(after == remaining - 1 && state.Missions[0].Progress == (mode == "completed" || after == 0 ? 1 : 0) && state.Missions[1].Progress == 0, "올바른 본체 미션 단일 완료");
                                    Values.Add(RowText("mission-hit", i + "/d=" + durability + "/" + mode + "/remaining=" + remaining, "reaction=" + Snapshot(reaction) + ";after=" + after + ";state=" + Snapshot(state) + ";context=" + Context(context)));
                                }
                            string removed = Snapshot(state); Invoke(Rules, "Remove", state, 0);
                            Check(removed == Snapshot(state) && random == JsonUtility.ToJson(UnityEngine.Random.state), "삭제 재완료 없음/전역 난수 보존");
                        }
                    }
                    finally { UnityEngine.Object.DestroyImmediate(level); }
                }
            foreach (int kind in new[] { -1, 5, 6, 99 }) Check((MissionKind)Rules.GetMethod("Mission", BindingFlags.Static | BindingFlags.NonPublic, null,
                new[] { typeof(ObstacleKind) }, null).Invoke(null, new object[] { (ObstacleKind)kind }) == MissionKind.Crate, "발전기/미지원 직접 기존 반환 " + kind);
            GeneratorChecks(before);
        }
        private static void GeneratorChecks(bool before)
        {
            foreach (int i in new[] { 0, 2, 3, 4 })
                foreach (int required in new[] { 3, 4, 5 })
                {
                    LevelDefinition level = Fixture(i, Maximum(Kinds[i]), true, required, before);
                    try
                    {
                        LevelRuntimeState state = Build(level); TurnEffectContext context = Fresh();
                        for (int charge = 0; charge < required; charge++)
                        {
                            context = Next(context); string initial = Snapshot(state), turn = Context(context);
                            MissionContribution[] query = MissionProgressRules.Query(state, C(4, 4), context).ToArray();
                            Check(query.Length == 1 && query[0].MissionIndex == 0 && query[0].BodyIndex == 1 && query[0].ExpectedComplete == (charge == required - 1 ? 1 : 0), "연결 대상 기여/완료 예상 " + i + required + charge);
                            Check(initial == Snapshot(state) && turn == Context(context), "연결 기여 조회 무변경");
                            object records = Invoke(typeof(LayerVerification), "Hit", state, C(4, 4), context);
                            Check(state.Missions[0].Progress == (charge == required - 1 ? 1 : 0) && state.Missions[1].Progress == 0, "실제 발전기 연결 대상만 완료");
                            Values.Add(RowText("generator-mission", i + "/required=" + required + "/charge=" + charge, "query=" + Snapshot(query) + ";records=" + Snapshot(records) + ";state=" + Snapshot(state) + ";context=" + Context(context)));
                        }
                        Check(!GeneratorRules.ActiveConnections(state).Any() && state.CellAt(C(1, 1)).ObstacleIndex.HasValue, "작동 연결 해제/다른 본체 보존");
                    }
                    finally { UnityEngine.Object.DestroyImmediate(level); }
                }
        }
        private static void ConnectionChecks()
        {
            ElementCatalog catalog = (ElementCatalog)typeof(LegacyElementDefinitions).GetField("Catalog", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            Dictionary<ElementId, ElementDefinition> definitions = (Dictionary<ElementId, ElementDefinition>)typeof(ElementCatalog).GetField("definitions", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(catalog);
            for (int i = 0; i < Kinds.Length; i++)
            {
                ElementDefinition original = LegacyElementDefinitions.Get(Kinds[i]); int other = i == 0 ? 1 : 0;
                LevelDefinition level = Fixture(i, Maximum(Kinds[i]), false, 0, false);
                try
                {
                    LevelRuntimeState state = Build(level); LevelRuntimeState missingState = Build(level); TurnEffectContext context = Fresh();
                    definitions[original.Id] = (ElementDefinition)Define(original, Profile(Missions[other]));
                    MissionContribution[] query = MissionProgressRules.Query(state, C(4, 4), context).ToArray();
                    for (int hit = 0; hit < Maximum(Kinds[i]); hit++)
                    { context = Next(context); Check(DamageReaction.Evaluate(state, C(4, 4), DamageCause.Power, C(4, 4), context, null, hit + 1).Response == DamageResponse.Damage, "교체 정의 실제 허용"); Invoke(Rules, "Apply", state, state.CellAt(C(4, 4)), context, hit + 1); }
                    Connections.Add(RowText("same-id", original.Id.Value, "profile=" + Missions[other] + ";query=" + Snapshot(query) + ";state=" + Snapshot(state) + ";context=" + Context(context)));
                    Check(query.Length == 1 && query[0].MissionIndex == 1 && state.Missions[0].Progress == 0 && state.Missions[1].Progress == 1, "같은ID 실제 기여/제거 완료 actualQuery=" + string.Join(",", query.Select(q => q.MissionIndex)) + "/actualProgress=" + state.Missions[0].Progress + "," + state.Missions[1].Progress + "/wantQuery=1/wantProgress=0,1/" + Kinds[i]);
                    definitions[original.Id] = (ElementDefinition)Define(original, null);
                    Exception missing = Error(() => Invoke(Rules, "Mission", Kinds[i]));
                    Check(missing is InvalidOperationException && missing.Message.Contains(original.Id.Value), "유효 종류 누락 ID 오류 " + Kinds[i]);
                    string unchanged = Snapshot(missingState);
                    Exception missingQuery = Error(() => MissionProgressRules.Query(missingState, C(4, 4), Fresh()));
                    Check(missingQuery is InvalidOperationException && missingQuery.Message.Contains(original.Id.Value) && unchanged == Snapshot(missingState), "누락 실제 미션 조회 오류/무변경 " + Kinds[i]);
                }
                finally { definitions[original.Id] = original; UnityEngine.Object.DestroyImmediate(level); Check(ReferenceEquals(LegacyElementDefinitions.Get(Kinds[i]), original), "finally 원래 등록 참조 복원 " + Kinds[i]); }
            }
            foreach (int i in new[] { 0, 2, 3, 4 })
                foreach (bool direct in new[] { false, true })
                {
                    ElementDefinition original = LegacyElementDefinitions.Get(Kinds[i]); int other = i == 0 ? 1 : 0;
                    LevelDefinition level = Fixture(i, Maximum(Kinds[i]), true, 3, false);
                    try
                    {
                        LevelRuntimeState state = Build(level); TurnEffectContext context = Fresh();
                        definitions[original.Id] = (ElementDefinition)Define(original, Profile(Missions[other]));
                        string initial = Snapshot(state), turn = Context(context);
                        MissionContribution[] query = MissionProgressRules.Query(state, C(4, 4), context).ToArray();
                        Check(initial == Snapshot(state) && turn == Context(context), "교체 정의 발전기 기여 조회 무변경");
                        int count = direct ? Maximum(Kinds[i]) : 3;
                        for (int step = 0; step < count; step++)
                        {
                            context = Next(context);
                            Invoke(typeof(LayerVerification), "Hit", state, direct ? C(4, 7) : C(4, 4), context);
                        }
                        Connections.Add(RowText("same-id-generator", original.Id.Value + "/direct=" + direct, "profile=" + Missions[other] + ";query=" + Snapshot(query) + ";state=" + Snapshot(state) + ";context=" + Context(context)));
                        Check(query.Length == 1 && query[0].MissionIndex == 1 && query[0].BodyIndex == 1 && state.Missions[0].Progress == 0 && state.Missions[1].Progress == 1, "교체 정의 연결 대상 기여/실제 완료 " + Kinds[i] + direct);
                        Check(!GeneratorRules.ActiveConnections(state).Any() && !state.CellAt(C(4, 4)).ObstacleIndex.HasValue && state.CellAt(C(1, 1)).ObstacleIndex.HasValue && context.Generators.Any(g => g.Event == (direct ? GeneratorEvent.Retired : GeneratorEvent.Activated)), "교체 정의 작동/직접 철거·연결 해제");
                    }
                    finally { definitions[original.Id] = original; UnityEngine.Object.DestroyImmediate(level); Check(ReferenceEquals(LegacyElementDefinitions.Get(Kinds[i]), original), "finally 연결 대상 등록 참조 복원 " + Kinds[i] + direct); }
                }
        }
        private static void ExistingChecks()
        {
            Type previous = typeof(DamageRecordPolicyVerification);
            List<string> results = (List<string>)previous.GetField("Results", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            List<string> values = (List<string>)previous.GetField("Values", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            results.Clear(); values.Clear();
            Invoke(previous, "SequenceChecks", false); Invoke(previous, "DirectChecks", false); Invoke(previous, "ExistingChecks");
            Results.AddRange(results); Values.AddRange(values);
        }
    }
}
