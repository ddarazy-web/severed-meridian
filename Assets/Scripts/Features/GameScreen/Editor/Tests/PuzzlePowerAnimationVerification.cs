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

namespace GameScreen.Editor
{
    public static partial class PuzzlePowerAnimationVerification
    {
        private const string Output = "Logs/Stage08/";
        private static readonly List<string> results = new List<string>();
        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); results.Add("PASS " + message); }

        public static void Assets()
        {
            Directory.CreateDirectory(Output);
            List<string> lines = new List<string>();
            string[] names = { "UnityEditor.U2D.Sprites.SpriteDataProviderFactories", "UnityEditor.U2D.Sprites.ISpriteFrameEditCapability" };
            foreach (string name in names)
            {
                Type type = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(name)).FirstOrDefault(candidate => candidate != null);
                lines.Add(name + "=" + (type?.AssemblyQualifiedName ?? "unavailable"));
            }
            foreach (string path in Directory.GetFiles("Assets/Textures/PowerBlocks", "*.png").Concat(Directory.GetFiles("Assets/Textures/Effects", "*.png", SearchOption.AllDirectories)))
            {
                string asset = path.Replace('\\', '/');
                TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(asset);
                string[] sprites = AssetDatabase.LoadAllAssetsAtPath(asset).OfType<Sprite>().Select(sprite => sprite.name).ToArray();
                lines.Add(asset + " mode=" + importer.spriteImportMode + " sprites=" + string.Join(",", sprites));
            }
            File.WriteAllLines(Output + "asset-inventory.txt", lines);
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        public static void Data()
        {
            Directory.CreateDirectory(Output); results.Clear(); int exit = 0;
            try
            {
                PropertyInfo property = typeof(TurnEffectContext).GetProperty("PowerTrace");
                Check(property != null, "실제 발사 결정의 일회성 표시 기록 제공");
                Check(typeof(PuzzleGameSession).Assembly.GetType("GameScreen.PuzzleEffectTimeline") != null,
                    "발사 도착과 피해 반응을 연결하는 표시 시간표 존재");
                foreach (InitialBlockKind kind in new[] { InitialBlockKind.Rocket, InitialBlockKind.Bomb, InitialBlockKind.Drone, InitialBlockKind.Magnet })
                {
                    LevelDefinition level = (LevelDefinition)typeof(PowerEffectVerification).GetMethod("Make", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
                    try
                    {
                        BoardCoordinate origin = new BoardCoordinate(4, 4);
                        typeof(PowerEffectVerification).GetMethod("Place", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null,
                            new object[] { level, origin, kind, RocketDirection.Horizontal, RabbitColor.Type1 });
                        LevelRuntimeState state = LevelStateBuilder.Build(level, 12345).State;
                        BoardActionExecutor executor = new BoardActionExecutor(state);
                        BoardActionResult action = executor.Activate(origin);
                        Check(action.IsApplied, kind + " 직접 발동 fixture");
                        object trace = property.GetValue(executor.TurnEffects);
                        object[] attacks = ((IEnumerable)trace.GetType().GetProperty("Attacks").GetValue(trace)).Cast<object>().ToArray();
                        Check(attacks.Length > 0, kind + " 발사 기록 있음");
                        Check(attacks.Any(attack => ((BoardCoordinate)attack.GetType().GetProperty("Origin").GetValue(attack)).Equals(origin)), kind + " 소모 전 출발 좌표 보존");
                        Check(ReferenceEquals(action.PowerTrace, executor.TurnEffects.PowerTrace), kind + " 행동 결과에 표시 기록 전달");
                        PuzzleEffectTimeline timeline = new PuzzleEffectTimeline(state, action.Changes, action.Effects, action.PowerTrace);
                        Check(timeline.Attacks.Count == action.PowerTrace.Attacks.Count && timeline.Duration > 0, kind + " 모든 발사 시간표 연결");
                        if (kind == InitialBlockKind.Rocket)
                        {
                            var rocket = timeline.Attacks.First(attack => attack.Record.Power == RuntimeContent.Rocket);
                            float firstArrival = rocket.ImpactAt(new BoardCoordinate(4, 5));
                            float edgeArrival = rocket.ImpactAt(new BoardCoordinate(4, 8));
                            Check(UnityEngine.Mathf.Abs((firstArrival - rocket.Start - .06f) / (edgeArrival - rocket.Start - .06f) - .25f) < .001f,
                                "로켓 한 칸 도착은 네 칸 비행 거리의 1/4 지점");
                        }
                        Check(timeline.Reactions.Where(reaction => reaction.Record.Response == DamageResponse.Remove)
                            .All(reaction => reaction.Time > 0), kind + " 공격 도착 이전에 일반 블록 제거 없음");
                        Check(timeline.Attacks.All(attack => timeline.Reactions.Where(reaction => reaction.Record.HitGroup == attack.Record.HitGroup && reaction.Record.Cause == DamageCause.Power)
                            .All(reaction => reaction.Time >= attack.ImpactAt(reaction.Record.Target))), kind + " 도착과 피해 반응 시간 일치");
                        foreach (PowerAttackRecord attack in action.PowerTrace.Attacks)
                            Check(action.Effects.Where(effect => effect.HitGroup == attack.HitGroup).All(effect => attack.Targets.Contains(effect.Target)),
                                kind + " 타격 묶음의 실제 선택 범위 보존 " + attack.HitGroup);
                        if (kind == InitialBlockKind.Drone)
                        {
                            BoardCoordinate[] landed = executor.TurnEffects.Targeting.Where(record => record.Event == TargetingEvent.Landed).Select(record => record.Target.Value).ToArray();
                            Check(action.PowerTrace.Attacks.Where(attack => attack.IsFlight).Select(attack => attack.Center).SequenceEqual(landed),
                                "드론 최종 착탄 결정과 표시 기록 일치");
                        }
                    }
                    finally { UnityEngine.Object.DestroyImmediate(level); }
                }
                for (int pair = 0; pair < 10; pair++)
                {
                    LevelDefinition level = (LevelDefinition)typeof(CombinationVerification).GetMethod("Make", BindingFlags.NonPublic | BindingFlags.Static)
                        .Invoke(null, new object[] { pair, RocketDirection.Horizontal, null });
                    try
                    {
                        LevelRuntimeState initial = LevelStateBuilder.Build(level, 12345).State;
                        BoardActionExecutor executor = new BoardActionExecutor(initial);
                        BoardActionResult action = executor.Swap(new BoardCoordinate(4, 4), new BoardCoordinate(4, 5));
                        Check(action.IsApplied && action.PowerTrace.Combination.Kind == (PowerCombinationKind)pair, "조합 종류 표시 기록 " + pair);
                        Check(action.PowerTrace.Attacks.Count > 0 && action.PowerTrace.Attacks.Select(attack => attack.HitGroup).Distinct().Count() == action.PowerTrace.Attacks.Count,
                            "조합 발사 기록·고유 타격 묶음 " + pair);
                        Check(action.PowerTrace.Attacks.All(attack => attack.ParentHitGroup == 0 || action.Effects.Any(effect => effect.HitGroup == attack.ParentHitGroup && effect.Target.Equals(attack.Origin))),
                            "조합 연쇄 발사 원인 연결 " + pair);
                        PuzzleEffectTimeline timeline = new PuzzleEffectTimeline(initial, action.Changes, action.Effects, action.PowerTrace);
                        if (action.PowerTrace.Combination.IsTransformation)
                            Check(timeline.Reactions.All(reaction => reaction.Time >= .35f), "자석 변환 완료 전 대상 반응 없음 " + pair);
                        Check(timeline.Attacks.All(attack => timeline.Reactions.Where(reaction => reaction.Record.Response == DamageResponse.Activate &&
                            reaction.Record.HitGroup == attack.Record.ParentHitGroup && reaction.Record.Target.Equals(attack.Record.Origin)).All(reaction => attack.Start >= reaction.Time)),
                            "조합 후속 발사는 원인 타격 이후 " + pair);
                        Check(timeline.Attacks.Where(attack => attack.Record.IsFlight).All(attack => timeline.Attacks.Take(attack.Record.WaitForAttacks).All(prior => attack.Start >= prior.End)),
                            "드론은 선행 공격 후 실제 표적으로 비행 " + pair);
                    }
                    finally { UnityEngine.Object.DestroyImmediate(level); }
                }
                LevelDefinition pending = (LevelDefinition)typeof(TargetPowerVerification).GetMethod("Make", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
                try
                {
                    typeof(TargetPowerVerification).GetMethod("Mission", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null,
                        new object[] { pending, MissionKind.Color, 2, RabbitColor.Type5 });
                    foreach (var placement in new[] { (new BoardCoordinate(4, 0), InitialBlockKind.Rocket), (new BoardCoordinate(4, 2), InitialBlockKind.Drone) })
                        typeof(PowerEffectVerification).GetMethod("Place", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null,
                            new object[] { pending, placement.Item1, placement.Item2, RocketDirection.Horizontal, RabbitColor.Type1 });
                    bool found = false;
                    for (int seed = 0; seed < 16 && !found; seed++)
                    {
                        LevelRuntimeState initial = LevelStateBuilder.Build(pending, seed).State;
                        foreach (RuntimeCell cell in initial.Cells.Where(cell => cell.Content == RuntimeContent.Normal))
                            typeof(RuntimeCell).GetProperty("Color").SetValue(cell, cell.Coordinate.Equals(new BoardCoordinate(4, 8)) || cell.Coordinate.Equals(new BoardCoordinate(8, 8)) ? RabbitColor.Type5 : RabbitColor.Type2);
                        TurnEffectContext context = (TurnEffectContext)typeof(TargetPowerVerification).GetMethod("Context", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
                        var effects = (List<EffectRecord>)typeof(TargetPowerVerification).GetMethod("Effects", BindingFlags.NonPublic | BindingFlags.Static)
                            .Invoke(null, new object[] { initial, new BoardCoordinate(4, 0), context });
                        if (!context.Targeting.Any(record => record.Event == TargetingEvent.Retargeted)) continue;
                        PuzzleEffectTimeline timeline = new PuzzleEffectTimeline(initial, Array.Empty<MatchedBlockChange>(), effects, context.PowerTrace);
                        var flight = timeline.Attacks.Single(attack => attack.Record.IsFlight);
                        Check(flight.Record.Retargeted && flight.Record.Center.Equals(new BoardCoordinate(8, 8)), "실제 표적 소실 후 재탐색 기록과 최종 표적 전달");
                        float hoverStart = timeline.Attacks.Where(attack => !attack.Record.IsFlight && attack.Record.Origin.Equals(flight.Record.Origin))
                            .Select(attack => attack.Start).DefaultIfEmpty(0).Min();
                        Check(Mathf.Abs(flight.Start - Mathf.Max(timeline.Attacks.Take(flight.Record.WaitForAttacks).Max(attack => attack.End), hoverStart + .72f) - .18f) < .001f,
                            "재탐색 드론은 공중 이동·선행 공격 대기 후 0.18초 추가 선회하고 돌진");
                        found = true;
                    }
                    Check(found, "표적 소실 재탐색 표시 fixture 재현");
                }
                finally { UnityEngine.Object.DestroyImmediate(pending); }
                VerifyRocketBodyArrival();
                PropertyInfo removedBodies = typeof(EffectRecord).GetProperty("RemovedObstacleIndices");
                Check(removedBodies != null, "타격 결과에 발전기 간접 제거 본체 기록");
                foreach (bool charge in new[] { true, false })
                {
                    LevelDefinition level = (LevelDefinition)typeof(GeneratorVerification).GetMethod("Make", BindingFlags.NonPublic | BindingFlags.Static)
                        .Invoke(null, new object[] { ObstacleKind.Appliance, 3 });
                    try
                    {
                        LevelRuntimeState state = LevelStateBuilder.Build(level, 12345).State;
                        typeof(RuntimeObstacle).GetProperty(charge ? "Charge" : "Durability").SetValue(state.Obstacles[charge ? 0 : 1], charge ? 2 : 1);
                        TurnEffectContext context = (TurnEffectContext)typeof(FixedObstacleVerification).GetMethod("Context", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
                        var hits = (List<EffectRecord>)typeof(LayerVerification).GetMethod("Hit", BindingFlags.NonPublic | BindingFlags.Static)
                            .Invoke(null, new object[] { state, new BoardCoordinate(4, charge ? 4 : 7), context });
                        Check(!state.Cells.Any(cell => cell.ObstacleIndex.HasValue), "발전기 완충/철거 fixture " + charge);
                        int[] removed = hits.SelectMany(hit => ((IEnumerable)removedBodies.GetValue(hit)).Cast<int>()).OrderBy(index => index).ToArray();
                        Check(removed.SequenceEqual(new[] { 0, 1 }), "발전기와 연결 본체 실제 제거 기록 " + charge);
                    }
                    finally { UnityEngine.Object.DestroyImmediate(level); }
                }
            }
            catch (Exception error) { results.Add("FAIL " + error); exit = 1; }
            File.WriteAllLines(Output + "data-results.txt", results);
            if (Application.isBatchMode) EditorApplication.Exit(exit);
        }

        private static void VerifyRocketBodyArrival()
        {
            LevelDefinition level = (LevelDefinition)typeof(PowerEffectVerification).GetMethod("Make", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
            try
            {
                BoardCoordinate anchor = new BoardCoordinate(0, 4), origin = new BoardCoordinate(8, 4);
                LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Block, Erase = true }, LevelPlacementRules.Footprint(anchor, 2));
                LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)ObstacleKind.Appliance, Durability = 9 }, new[] { anchor });
                typeof(PowerEffectVerification).GetMethod("Place", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null,
                    new object[] { level, origin, InitialBlockKind.Rocket, RocketDirection.Vertical, RabbitColor.Type1 });
                LevelRuntimeState before = LevelStateBuilder.Build(level, 12345).State;
                BoardActionExecutor executor = new BoardActionExecutor(before);
                BoardActionResult action = executor.Activate(origin);
                PuzzleEffectTimeline timeline = new PuzzleEffectTimeline(before, action.Changes, action.Effects, action.PowerTrace);
                var rocket = timeline.Attacks.First(attack => attack.Record.Power == RuntimeContent.Rocket);
                var hits = timeline.Reactions.Where(reaction => reaction.Record.Response == DamageResponse.Damage).ToArray();
                Check(hits.Length == 2, "상향 로켓 2x2 실제 두 번 타격 fixture");
                float near = rocket.Start + .06f + .36f * 7 / 8;
                Check(Mathf.Abs(rocket.ImpactAt(new BoardCoordinate(1, 4)) - near) < .0001f,
                    "본체 반복 반응이 로켓 실제 도착 시간을 변경하지 않음");
                Check(Mathf.Abs(hits[0].Time - near) < .0001f, "상향 로켓의 첫 본체 반응은 가까운 점유 칸 접촉부터 시작");
                Check(hits[1].Time >= hits[0].Time + .059f && hits[1].Record.DurabilityAfter == hits[0].Record.DurabilityAfter - 1,
                    "연속 접촉에도 실제 내구도 기록 두 단계를 보존");
            }
            finally { UnityEngine.Object.DestroyImmediate(level); }
        }
    }
}
