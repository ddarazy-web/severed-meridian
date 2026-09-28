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
    public static partial class ScrapVerification
    {
        private const string Evidence = "Logs/ScrapVerification";
        private static readonly List<string> Results = new List<string>();
        private static BoardCoordinate C(int r, int c) => new BoardCoordinate(r, c);
        private static object Invoke(Type type, string name, object owner, params object[] args)
            => type.GetMethod(name, BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, args);
        private static void Set(object owner, string name, object value) => owner.GetType().GetProperty(name).GetSetMethod(true).Invoke(owner, new[] { value });
        private static string Snapshot(object value) => (string)Invoke(typeof(LevelInitialStateVerification), "Snapshot", null, value);
        private static void Check(bool condition, string name) { if (!condition) throw new InvalidOperationException(name); Results.Add("PASS " + name); }
        private static LevelDefinition Make() => (LevelDefinition)Invoke(typeof(PowerEffectVerification), "Make", null);
        private static LevelRuntimeState Build(LevelDefinition level, int seed = 12345)
        {
            LevelStateBuildResult result = LevelStateBuilder.Build(level, seed);
            if (!result.IsBuilt) throw new InvalidOperationException(string.Join(" | ", result.Issues));
            return result.State;
        }
        private static TurnEffectContext Context() => (TurnEffectContext)Invoke(typeof(TargetPowerVerification), "Context", null);
        private static void Place(LevelDefinition level, BoardCoordinate coordinate, int durability)
        {
            LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Block, Erase = true }, new[] { coordinate });
            PlacementEditResult result = LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)ObstacleKind.Scrap, Durability = durability }, new[] { coordinate });
            if (result.Changed != 1) throw new InvalidOperationException("고철 배치 실패 " + coordinate);
        }
        private static void Power(LevelDefinition level, BoardCoordinate c, RuntimeContent power)
            => Invoke(typeof(PowerEffectVerification), "Place", null, level, c, (InitialBlockKind)((int)power), RocketDirection.Horizontal, RabbitColor.Type1);
        private static List<EffectRecord> Hit(LevelRuntimeState state, BoardCoordinate target, TurnEffectContext context)
            => (List<EffectRecord>)Invoke(typeof(LayerVerification), "Hit", null, state, target, context);
        private static void Empty(LevelRuntimeState state, IEnumerable<BoardCoordinate> cells)
        {
            foreach (BoardCoordinate c in cells)
            { RuntimeCell cell = state.CellAt(c); Set(cell, "Content", RuntimeContent.Empty); Set(cell, "Color", null); Set(cell, "RocketDirection", null); Set(cell, "ObstacleIndex", null); }
        }
        private static void Fixed(LevelDefinition level, BoardCoordinate c, params SupplyItem[] items)
            => Invoke(typeof(SettlementVerification), "Source", null, level, c, SupplyExhaustion.Stop, items);
        private static void Maintain(LevelDefinition level, BoardCoordinate[] cells, int target, int limit, int durability)
        {
            string error = LevelSupplyEditing.PlaceSources(level, cells);
            if (error == null) error = LevelSupplyEditing.SetSourceProperty(level, Enumerable.Range(0, cells.Length).ToArray(), "mode", (int)SupplyMode.MaintainScrap);
            if (error != null) throw new InvalidOperationException(error);
            SerializedObject data = new SerializedObject(level);
            data.FindProperty("supply.scrapTarget").intValue = target;
            data.FindProperty("supply.scrapLimit").intValue = limit;
            data.FindProperty("supply.scrapDurability").intValue = durability;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void Mission(LevelDefinition level, int count)
            => JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":" + (int)MissionKind.Scrap + ",\"count\":" + count + "}]}", level);
        public static void Data()
        {
            Directory.CreateDirectory(Evidence); Results.Clear();
            try { DataChecks(); File.WriteAllLines(Evidence + "/data-results.txt", Results); EditorApplication.Exit(0); }
            catch (Exception error) { Results.Add("FAIL " + error); File.WriteAllLines(Evidence + "/data-results.txt", Results); Debug.LogException(error); EditorApplication.Exit(1); }
        }
        private static void DataChecks()
        {
            for (int durability = 1; durability <= 5; durability++)
            {
                LevelDefinition level = Make(); Place(level, C(4, 4), durability); Mission(level, 1);
                LevelRuntimeState state = Build(level); TurnEffectContext context = Context(); RuntimeCell cell = state.CellAt(C(4, 4));
                Set(cell, "DustDurability", 3); string before = Snapshot(state);
                MissionContribution contribution = MissionProgressRules.Query(state, C(4, 4), context).Single();
                Check(contribution.Damage == 1 && contribution.ExpectedComplete == (durability == 1 ? 1 : 0) && Snapshot(state) == before, "고철 미션 조회/부분·완전/무변경 " + durability);
                Check(DamageReaction.Evaluate(state, C(4, 4), DamageCause.AdjacentMatch, C(4, 3), context).Response == DamageResponse.Damage, "고철 인접 매칭 반응 " + durability);
                Hit(state, C(4, 4), context);
                Check(state.Obstacles[0].Durability == durability - 1 && state.Missions[0].Progress == (durability == 1 ? 1 : 0) && cell.DustDurability == 3, "고철 실제 피해/완전 제거만 집계/바닥 보존 " + durability);
                if (durability > 1)
                {
                    Empty(state, state.Cells.Where(c => c.Content == RuntimeContent.Normal).Select(c => c.Coordinate).ToArray());
                    SettlementResult fallen = SettlementResolution.Resolve(state, context);
                    RuntimeCell moved = fallen.State.Cells.Single(c => c.ObstacleIndex == 0);
                    Check(fallen.IsApplied && moved.Coordinate.Equals(C(9, 4)) && fallen.State.Obstacles[0].Durability == durability - 1 && !state.CellAt(C(9, 4)).ObstacleIndex.HasValue, "고철 낙하/본체·내구도/원본 독립 " + durability);
                    Hit(fallen.State, moved.Coordinate, fallen.TurnEffects);
                    Check(fallen.State.Obstacles[0].Durability == durability - 1 && !MissionProgressRules.Query(fallen.State, moved.Coordinate, fallen.TurnEffects).Any(), "이동 후 같은 턴 재피해·드론 후보 금지 " + durability);
                    Hit(fallen.State, moved.Coordinate, Context());
                    Check(fallen.State.Obstacles[0].Durability == durability - 2 && state.Obstacles[0].Durability == durability - 1, "다음 턴 재피해/본체 사본 독립 " + durability);
                }
                foreach (RuntimeContent power in new[] { RuntimeContent.Rocket, RuntimeContent.Bomb, RuntimeContent.Drone, RuntimeContent.Magnet })
                    foreach (bool reverse in new[] { false, true })
                    {
                        LevelDefinition swap = Make(); Place(swap, C(4, 4), durability); Power(swap, C(4, 5), power);
                        BoardActionExecutor executor = new BoardActionExecutor(Build(swap)); string original = Snapshot(executor.State);
                        BoardActionResult action = executor.Swap(reverse ? C(4, 5) : C(4, 4), reverse ? C(4, 4) : C(4, 5));
                        if (power == RuntimeContent.Magnet) Check(!action.IsApplied && Snapshot(executor.State) == original, "자석 고철 양방향 무비용 취소 " + durability + reverse);
                        else Check(action.IsApplied && action.Effects.Any(e => e.Response == DamageResponse.Activate && e.Target.Equals(C(4, 4))) && executor.State.MovesRemaining == 19 &&
                            (executor.State.Obstacles[0].Durability == 0 || executor.State.CellAt(C(4, 5)).ObstacleIndex == 0), "고철 파워 실제 도착 발동/본체 교환 " + power + durability + reverse);
                    }
            }
            SupplyChecks();
        }
        private static void SupplyChecks()
        {
            for (int durability = 1; durability <= 5; durability++)
            {
                LevelDefinition level = Make(); Fixed(level, C(0, 0), new SupplyItem(SupplyKind.Scrap, 2, durability: durability), new SupplyItem(SupplyKind.Bomb));
                LevelRuntimeState state = Build(level); string original = Snapshot(state); Empty(state, new[] { C(0, 0) });
                Check(state.Supply.Sources[0].ItemConsumed == 0 && state.Obstacles.Count == 0, "고정 공급 초기 구성 비소비 " + durability);
                TurnEffectContext context = Context();
                for (int n = 0; n < 3; n++)
                {
                    SettlementResult result = SettlementResolution.Resolve(state, context); Check(result.IsApplied, "고정 혼합 공급 정착 " + durability + n);
                    state = result.State; context = result.TurnEffects;
                    if (n < 2)
                    {
                        RuntimeCell cell = state.CellAt(C(0, 0));
                        Check(cell.ObstacleIndex == n && state.Obstacles[n].Durability == durability && state.Supply.ScrapGenerated == 0, "새 고철 키/내구도/고정 카운터 분리 " + durability + n);
                        Hit(state, C(0, 0), context);
                        Check(state.Obstacles[n].Durability == durability - 1, "같은 좌표 새 본체는 이전 턴 피해 미상속 " + durability + n);
                        while (state.Obstacles[n].Durability > 0) Hit(state, C(0, 0), Context());
                    }
                    else Check(state.CellAt(C(0, 0)).Content == RuntimeContent.Bomb && !context.IsProtected(C(0, 0)), "혼합 파워 공급 보호 없음 " + durability);
                    Empty(state, new[] { C(0, 0) });
                }
                Check(SettlementResolution.Resolve(state, context).State.CellAt(C(0, 0)).Content == RuntimeContent.Empty && state.Supply.Sources[0].ItemIndex == 2, "목록 소진 Stop " + durability);
            }
            foreach (int seed in Enumerable.Range(1, 12))
            {
                LevelDefinition level = Make(); Place(level, C(9, 8), 2); Place(level, C(9, 9), 2);
                Maintain(level, new[] { C(0, 0), C(0, 2), C(0, 4) }, 3, 6, 4);
                LevelRuntimeState state = Build(level, seed);
                Check(state.Supply.ScrapGenerated == 0 && state.LiveScrapCount == 2, "최초 고철 한도 제외 " + seed);
                Empty(state, new[] { C(0, 0), C(0, 2), C(0, 4) }); string before = Snapshot(state);
                SettlementResult result = SettlementResolution.Resolve(state);
                Check(result.IsApplied && result.State.LiveScrapCount == 3 && result.State.Supply.ScrapGenerated == 1 && result.State.Supply.ScrapRemaining == 5, "부족량만 유지 공급 " + seed);
                Check(result.Records.Count(r => r.Kind == MovementKind.Supply) == 3 && result.State.Obstacles[2].Durability == 4 && Snapshot(state) == before, "고철 우선 후 일반 공급/원본 보존 " + seed);
                Check(Snapshot(result.State) == Snapshot(SettlementResolution.Resolve(state).State), "유지 배정 동일 시드 재현 " + seed);
                Check(SettlementResolution.Resolve(result.State).RandomAfter == result.RandomAfter, "막힌 생성구 무소비 " + seed);
            }
            foreach (int target in new[] { 0, 1, 5 }) foreach (int limit in new[] { 0, 1, 6 })
            {
                LevelDefinition level = Make(); Maintain(level, new[] { C(0, 0) }, target, limit, 3);
                if (target == 0)
                {
                    Check(!LevelStateBuilder.Build(level, 12345).IsBuilt, "목표0 유지 생성구는 기존 편집 검증에서 거절 " + limit);
                    continue;
                }
                LevelRuntimeState state = Build(level);
                Empty(state, new[] { C(0, 0) }); SettlementResult result = SettlementResolution.Resolve(state);
                bool scrap = target > 0 && limit > 0;
                Check(result.IsApplied && result.State.LiveScrapCount == (scrap ? 1 : 0) && result.RandomAfter - result.RandomBefore == (scrap ? 0 : 1), "목표/한도 경계 및 단일 후보 무난수 " + target + limit);
            }
        }
    }
}
