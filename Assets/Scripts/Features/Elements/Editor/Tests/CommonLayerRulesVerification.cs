using System;
using System.Collections.Generic;
using System.Collections;
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
    /// <summary>층 정의 연결과 실제 효과 실행을 검사한다. 기존 증거 파일은 쓰지 않는다.</summary>
    public static class CommonLayerRulesVerification
    {
        private const string Evidence = "Logs/ElementFramework/Phase01/Layers";
        private static readonly List<string> Results = new List<string>();
        private static readonly List<string> Values = new List<string>();
        private static void Check(bool pass, string name)
        { Results.Add((pass ? "PASS " : "FAIL ") + name); }
        private static object Invoke(Type type, string name, params object[] args) =>
            type.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, args);
        private static string Snapshot(object value) => (string)Invoke(typeof(LevelInitialStateVerification), "Snapshot", value);
        public static void Before() => Execute(false);
        public static void Run() => Execute(true);
        private static void Execute(bool connected)
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 Editor에서만 실행한다.");
            Directory.CreateDirectory(Evidence); Results.Clear(); Values.Clear();
            try
            {
                Invoke(typeof(LayerVerification), "DataChecks");
                List<string> layerResults = (List<string>)typeof(LayerVerification).GetField("Results", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
                Results.AddRange(layerResults);
                Capture();
                if (!connected) File.WriteAllLines(Evidence + "/baseline-values.txt", Values);
                else
                {
                    Check(File.ReadAllLines(Evidence + "/baseline-values.txt").SequenceEqual(Values), "층 실제 상태/문맥/효과/난수 전후 동일");
                    DefinitionChecks();
                    ConnectionChecks();
                    AlternateChecks();
                }
            }
            catch (Exception error) { Results.Add("FAIL " + error); Debug.LogException(error); }
            string mode = connected ? "after" : "before";
            File.WriteAllLines(Evidence + "/" + mode + "-results.txt", Results);
            File.WriteAllLines(Evidence + "/" + mode + "-values.txt", Values);
            EditorApplication.Exit(Results.Any(value => value.StartsWith("FAIL ")) ? 1 : 0);
        }
        private static void Capture()
        {
            foreach (RuntimeContent content in new[] { RuntimeContent.Normal, RuntimeContent.Rocket, RuntimeContent.Bomb, RuntimeContent.Drone, RuntimeContent.Magnet })
                for (int durability = 1; durability <= 3; durability++)
                {
                    LevelDefinition level = (LevelDefinition)Invoke(typeof(LayerVerification), "Make");
                    try
                    {
                        Invoke(typeof(LayerVerification), "Missions", level);
                        LevelRuntimeState state = (LevelRuntimeState)Invoke(typeof(LayerVerification), "Build", level, 12345);
                        RuntimeCell cell = state.CellAt(new BoardCoordinate(4, 4));
                        Invoke(typeof(LayerVerification), "Set", cell, "Content", content);
                        Invoke(typeof(LayerVerification), "Set", cell, "Color", content == RuntimeContent.Normal ? (RabbitColor?)RabbitColor.Type1 : null);
                        Invoke(typeof(LayerVerification), "Set", cell, "RocketDirection", content == RuntimeContent.Rocket ? (RocketDirection?)RocketDirection.Horizontal : null);
                        Invoke(typeof(LayerVerification), "Web", cell, durability);
                        Invoke(typeof(LayerVerification), "Set", cell, "DustDurability", 3);
                        TurnEffectContext context = (TurnEffectContext)Invoke(typeof(LayerVerification), "Context");
                        for (int hit = 0; hit < 2; hit++)
                        {
                            object effects = Invoke(typeof(LayerVerification), "Hit", state, cell.Coordinate, context);
                            Values.Add(content + ":" + durability + ":" + hit + ":" + Snapshot(state) + ":" + Snapshot(context) + ":" + Snapshot(effects));
                        }
                    }
                    finally { UnityEngine.Object.DestroyImmediate(level); }
                }
        }
        private static void DefinitionChecks()
        {
            MethodInfo map = typeof(LegacyElementMap).GetMethod("Get", new[] { typeof(CoverKind) });
            MethodInfo get = typeof(LegacyElementDefinitions).GetMethod("Get", new[] { typeof(CoverKind) });
            MethodInfo dust = typeof(LegacyElementDefinitions).GetMethod("GetDust", Type.EmptyTypes);
            Check(map != null && get != null, "실제 덮개 공개 매핑/정의 조회 존재");
            Check(dust != null, "실제 먼지 정의 조회 존재");
            if (map == null || get == null || dust == null) return;
            foreach (CoverKind kind in new[] { CoverKind.Web, CoverKind.Mold })
            {
                ElementDefinition definition = (ElementDefinition)get.Invoke(null, new object[] { kind });
                string id = kind == CoverKind.Web ? "cover.web" : "cover.mold";
                Check(definition.Id.Value == id && ((ElementId)map.Invoke(null, new object[] { kind })).Value == id, "덮개 영구 ID " + kind);
                Check(ReferenceEquals(definition, get.Invoke(null, new object[] { kind })), "덮개 공유 정의 " + kind);
                Check(definition.RequirePlacement().MaxDurability == (kind == CoverKind.Web ? 3 : 1), "덮개 최대 내구도 " + kind);
                Check(typeof(ElementDefinition).GetProperty("Layer")?.GetValue(definition) != null, "덮개 실제 행동/미션 프로필 " + kind);
            }
            ElementDefinition floor = (ElementDefinition)dust.Invoke(null, null);
            Check(floor.Id.Value == "floor.dust" && floor.RequirePlacement().MaxDurability == 3, "먼지 ID/배치 수치");
            Check(typeof(ElementDefinition).GetProperty("Layer")?.GetValue(floor) != null, "먼지 소비 행동/미션 프로필");
            for (int durability = 0; durability <= 4; durability++)
                Check((LevelPlacementRules.CoverValueError(CoverKind.Web, durability) == null) == (durability >= 1 && durability <= 3), "거미줄 실제 배치 경계 " + durability);
            foreach (int invalid in new[] { -1, 2, 999 })
            {
                Exception failure = null;
                try { get.Invoke(null, new object[] { (CoverKind)invalid }); }
                catch (TargetInvocationException error) { failure = error.InnerException; }
                Check(failure is ArgumentOutOfRangeException, "미지원 덮개 거절 " + invalid);
            }
        }

        private static void ConnectionChecks()
        {
            ElementCatalog catalog = (ElementCatalog)typeof(LegacyElementDefinitions).GetField("Catalog", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            IDictionary definitions = (IDictionary)typeof(ElementCatalog).GetField("definitions", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(catalog);
            foreach (bool cover in new[] { true, false })
            {
                ElementDefinition original = cover ? LegacyElementDefinitions.Get(CoverKind.Web) : LegacyElementDefinitions.GetDust();
                ElementLayerProfile changed = new ElementLayerProfile(original.RequireLayer().Behavior, MissionKind.Dust, 2);
                ElementDefinition replacement = new ElementDefinition(original.Id, original.DisplayName, new ElementPlacementProfile(1, 2),
                    null, null, null, null, null, null, changed);
                definitions[original.Id] = replacement;
                LevelDefinition level = (LevelDefinition)Invoke(typeof(LayerVerification), "Make");
                try
                {
                    Invoke(typeof(LayerVerification), "Missions", level);
                    LevelRuntimeState state = (LevelRuntimeState)Invoke(typeof(LayerVerification), "Build", level, 12345);
                    RuntimeCell cell = state.CellAt(new BoardCoordinate(4, 4));
                    TurnEffectContext context = (TurnEffectContext)Invoke(typeof(LayerVerification), "Context");
                    if (cover) Invoke(typeof(LayerVerification), "Web", cell, 2);
                    else Invoke(typeof(LayerVerification), "Set", cell, "DustDurability", 2);
                    if (cover)
                    {
                        LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Cover, Kind = (int)CoverKind.Web, Durability = 2 }, new[] { cell.Coordinate });
                        Check(LevelMissionRules.Supply(level, level.Missions[3]).Initial == 1 && LevelMissionRules.Supply(level, level.Missions[2]).Initial == 0,
                            "층 정의 미션 최초 가용 수량 실제 소비");
                    }
                    string before = Snapshot(state) + Snapshot(context);
                    IReadOnlyList<MissionContribution> prediction = MissionProgressRules.Query(state, cell.Coordinate, context);
                    Check(prediction.Any(value => value.MissionIndex == 3 && value.ExpectedComplete == 1 && value.Damage == 2), "층 정의 미션/피해 예측 실제 소비 " + cover);
                    Check(Snapshot(state) + Snapshot(context) == before, "층 예측 상태 무변경 " + cover);
                    Invoke(typeof(LayerVerification), "Hit", state, cell.Coordinate, context);
                    Check((cover ? cell.CoverDurability : cell.DustDurability) == 0 && state.Missions[3].Progress == 1, "층 정의 피해량/제거 미션 실제 적용 " + cover);
                    if (cover) Check(cell.Content == RuntimeContent.Normal && state.Missions[2].Progress == 0, "비기본 거미줄 내용물 보존/구형 미션 분기 없음");
                    else
                    {
                        Check(LevelObstacleEditing.PlacementError(level, new PlacementBrush { Layer = PlacementLayer.Dust, Durability = 3 }, cell.Coordinate) != null,
                            "먼지 정의 최대값 실제 편집 배치 검증");
                    }
                }
                finally { definitions[original.Id] = original; UnityEngine.Object.DestroyImmediate(level); }
            }
        }

        private static void AlternateChecks()
        {
            Check(new[] { typeof(ElementLayerProfile), typeof(ElementTurnProfile), typeof(ElementSupplyProfile) }.All(type =>
                type.IsSealed && type.GetProperties().All(property => property.SetMethod == null) &&
                type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic).All(field => field.IsInitOnly)), "추가 프로필 불변 소유권");
            ElementDefinition alternate = new ElementDefinition(new ElementId("test.cover.alternate"), "검사용 덮개",
                new ElementPlacementProfile(1, 3), null, null, null, null, null, null,
                new ElementLayerProfile(ElementLayerBehavior.CoverDurability, MissionKind.Web));
            ElementCatalog catalog = new ElementCatalog(new[] { alternate });
            LevelDefinition level = (LevelDefinition)Invoke(typeof(LayerVerification), "Make");
            try
            {
                Invoke(typeof(LayerVerification), "Missions", level);
                LevelRuntimeState state = (LevelRuntimeState)Invoke(typeof(LayerVerification), "Build", level, 12345);
                RuntimeCell cell = state.CellAt(new BoardCoordinate(4, 4));
                Invoke(typeof(LayerVerification), "Web", cell, 1);
                TurnEffectContext context = (TurnEffectContext)Invoke(typeof(LayerVerification), "Context");
                Type registry = typeof(ElementDefinition).Assembly.GetType("Elements.ElementLayerBehaviorRegistry");
                DamageReaction query = (DamageReaction)Invoke(registry, "Query", catalog.Get(alternate.Id), cell, context);
                Check(query.Response == DamageResponse.CoverDamage && query.Amount == 1, "다른 ID 정의 등록 행동 실제 조회");
                Invoke(registry, "Apply", catalog.Get(alternate.Id), state, cell, context);
                Check(cell.Cover == null && cell.Content == RuntimeContent.Normal && state.Missions[2].Progress == 1,
                    "다른 ID 정의 공통 적용/내용물 보존/제거 미션");
            }
            finally { UnityEngine.Object.DestroyImmediate(level); }
        }
    }
}
