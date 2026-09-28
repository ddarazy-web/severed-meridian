using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using Board;
using Simulation;
using UnityEditor;
using UnityEngine;

namespace Levels.Editor
{
    public static partial class FixedObstacleVerification
    {
        private static List<EffectRecord> Matches(LevelRuntimeState state, TurnEffectContext context, BoardCoordinate[] cells, BoardCoordinate spawn)
        {
            ReadOnlyCollection<MatchDecision> decisions = (ReadOnlyCollection<MatchDecision>)Invoke(typeof(MatchResolution), "Select", null,
                MatchQuery.Find(state).Where(p => p.Cells.All(cells.Contains)), cells[0], spawn, state.Random);
            if (decisions.Count == 0) throw new InvalidOperationException("검증 매칭 없음");
            ReadOnlyCollection<MatchedBlockChange> changes = (ReadOnlyCollection<MatchedBlockChange>)Invoke(typeof(MatchResolution), "ApplyLayers", null, state, decisions, 1, context);
            Invoke(typeof(TurnEffectContext), "Protect", context, changes);
            List<EffectRecord> records = new List<EffectRecord>(); object[] args = { state, changes, null, context, records, null };
            if (!(bool)Invoke(typeof(PowerEffectResolution), "Apply", null, args)) throw new InvalidOperationException((string)args[5]);
            return records;
        }
        private static DroneTargetManager Manager(LevelRuntimeState state, TurnEffectContext context) =>
            (DroneTargetManager)Activator.CreateInstance(typeof(DroneTargetManager), BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] { state, context }, null);
        public static void Supplemental()
        {
            Results.Clear();
            try { MatchChecks(); MagnetChecks(); DroneChecks(); File.WriteAllLines(Evidence + "/supplemental-results.txt", Results); EditorApplication.Exit(0); }
            catch (Exception error) { Results.Add("FAIL " + error); File.WriteAllLines(Evidence + "/supplemental-results.txt", Results); Debug.LogException(error); EditorApplication.Exit(1); }
        }
        private static void MatchChecks()
        {
            foreach (bool separate in new[] { false, true })
            {
                LevelDefinition level = Make(); Obstacle(level, ObstacleKind.Appliance, 9, C(4, 4)); SetMission(level, ObstacleKind.Appliance);
                BoardCoordinate[] cells = separate ? new[] { C(3, 4), C(3, 5), C(3, 6), C(4, 3), C(5, 3), C(6, 3) } : new[] { C(3, 2), C(3, 3), C(3, 4), C(4, 3), C(5, 3) };
                for (int i = 0; i < cells.Length; i++) Place(level, cells[i], InitialBlockKind.FixedNormal, color: separate && i >= 3 ? RabbitColor.Type2 : RabbitColor.Type1);
                LevelRuntimeState state = Build(level); TurnEffectContext context = Context(); List<EffectRecord> effects = Matches(state, context, cells, cells[0]);
                Check(state.Obstacles[0].Durability == (separate ? 5 : 7), "별개 동시 매칭/같은 매칭 중복 접촉 " + separate);
                Check(effects.Where(e => e.Response == DamageResponse.Damage).Select(e => e.HitGroup).Distinct().Count() == (separate ? 2 : 1), "매칭별 타격 식별 " + separate);
                TurnEffectContext copy = (TurnEffectContext)Invoke(typeof(TurnEffectContext), "Copy", context);
                EffectRecord prior = effects.First(e => e.Response == DamageResponse.Damage);
                Check(DamageReaction.Evaluate(state, prior.Target, DamageCause.Power, prior.Source, copy, null, prior.HitGroup).Response == DamageResponse.AlreadyDamaged, "사본에 칸별 타격 이력 보존 " + separate);
                Hit(state, prior.Target, copy); Check(state.Obstacles[0].Durability == (separate ? 4 : 6), "같은 수 후속 별도 타격 허용 " + separate);
            }
            foreach (bool wall in new[] { false, true })
            {
                LevelDefinition level = Make(); Obstacle(level, ObstacleKind.ColorLock, 3, C(4, 3));
                BoardCoordinate[] cells = { C(3, 1), C(3, 2), C(3, 3), C(3, 4) };
                foreach (BoardCoordinate c in cells) Place(level, c, InitialBlockKind.FixedNormal);
                if (wall) LevelFlowEditing.SetWalls(level, new[] { new BoardEdge(C(3, 3), C(4, 3)) }, false);
                LevelRuntimeState state = Build(level); TurnEffectContext context = Context(); Matches(state, context, cells, C(3, 3));
                Check(state.CellAt(C(3, 3)).Content == RuntimeContent.Rocket && state.Obstacles[0].Durability == (wall ? 3 : 2), "파워 생성 칸 원래 색/벽 인접 " + wall);
            }
            LevelDefinition repeat = Make(); Obstacle(repeat, ObstacleKind.Appliance, 9, C(4, 4)); SetMission(repeat, ObstacleKind.Appliance);
            Place(repeat, C(4, 0), InitialBlockKind.Rocket); Place(repeat, C(0, 0), InitialBlockKind.Bomb); Place(repeat, C(0, 1), InitialBlockKind.Drone);
            LevelRuntimeState repeating = Build(repeat); TurnEffectContext repeats = Context(); Hit(repeating, C(4, 0), repeats);
            // 범위 일부만 닿는 주변 일반 칸 대신 본체 네 칸만 조준 후보로 남긴다.
            foreach (RuntimeCell cell in repeating.Cells.Where(c => c.Content == RuntimeContent.Normal))
            { Set(cell, "Content", RuntimeContent.Empty); Set(cell, "Color", null); }
            List<EffectRecord> repeatRecords = new List<EffectRecord>(); object[] repeatArgs = { repeating, C(0, 0), C(0, 1), repeats, repeatRecords, null };
            Check((bool)Invoke(typeof(PowerEffectResolution), "ApplyCombination", null, repeatArgs) && repeating.Obstacles[0].Durability == 3, "로켓2 이후 폭탄드론 네 칸 타격4 합계6");
        }
        private static void MagnetChecks()
        {
            foreach (string cover in new[] { "none", "web", "mold", "wall", "wrong" })
            {
                LevelDefinition level = Make(); Obstacle(level, ObstacleKind.ColorLock, 3, C(4, 4));
                // 노출된 선택 색을 하나로 제한하고 자석의 실제 색 제거 경로를 실행한다.
                foreach (InitialBlockDefinition block in level.InitialBlocks.ToArray()) Place(level, block.Coordinate, InitialBlockKind.FixedNormal, color: RabbitColor.Type2);
                Place(level, C(4, 3), InitialBlockKind.FixedNormal, color: cover == "wrong" ? RabbitColor.Type2 : RabbitColor.Type1);
                Place(level, C(0, 0), InitialBlockKind.Magnet);
                if (cover == "web" || cover == "mold") LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Cover, Kind = (int)(cover == "web" ? CoverKind.Web : CoverKind.Mold), Durability = 1 }, new[] { C(4, 3) });
                if (cover == "wall") LevelFlowEditing.SetWalls(level, new[] { new BoardEdge(C(4, 3), C(4, 4)) }, false);
                LevelRuntimeState state = Build(level); TurnEffectContext context = Context(); List<EffectRecord> records = new List<EffectRecord>();
                object[] args = { state, Array.Empty<MatchedBlockChange>(), (BoardCoordinate?)C(0, 0), context, records, null, (RabbitColor?)RabbitColor.Type1 };
                Check((bool)Invoke(typeof(PowerEffectResolution), "ApplyWithColor", null, args), "자석 실행 " + cover);
                Check(state.Obstacles[0].Durability == (cover == "none" ? 2 : 3) && records.All(e => !e.Target.Equals(C(4, 4)) || e.Cause != DamageCause.Power), "실제 색 제거만 인접 피해/자물쇠 직접 색 대상 제외 " + cover);
            }
        }
        private static void DroneChecks()
        {
            LevelDefinition level = Make(); Obstacle(level, ObstacleKind.Appliance, 2, C(4, 4)); SetMission(level, ObstacleKind.Appliance);
            LevelRuntimeState state = Build(level); TurnEffectContext context = Context(); DroneTargetManager manager = Manager(state, context);
            string before = Snapshot(state); Check(manager.Query().Count == 4 && Snapshot(state) == before, "폐가전 네 칸 미션 후보/조회 보존");
            string boardBefore = Snapshot(state.Cells) + Snapshot(state.Obstacles) + Snapshot(state.Missions);
            int first = (int)Invoke(typeof(DroneTargetManager), "Request", manager, C(0, 0));
            Check(manager.ReservationCount == 1 && manager.ExpectedComplete == 0 && manager.ExpectedDamage == 1 && manager.Query().Count == 3, "첫 한 칸 예약/다른 칸 허용");
            int second = (int)Invoke(typeof(DroneTargetManager), "Request", manager, C(0, 1));
            Check(manager.ExpectedComplete == 1 && manager.ExpectedDamage == 2 && manager.Query().All(t => !t.IsMission) && Snapshot(state.Cells) + Snapshot(state.Obstacles) + Snapshot(state.Missions) == boardBefore, "내구도2의 1+1 예약 제거 예정1/실제 미변경/추가 대체");
            DroneTarget landing = (DroneTarget)Invoke(typeof(DroneTargetManager), "Land", manager, first, C(0, 0)); Hit(state, landing.Coordinate, context); Invoke(typeof(DroneTargetManager), "Invalidate", manager);
            Check(manager.ExpectedComplete == 1 && manager.ExpectedDamage == 1 && state.Missions[0].Progress == 0, "부분 타격 뒤 남은 예약 예상 재계산");
            DroneTarget landing2 = (DroneTarget)Invoke(typeof(DroneTargetManager), "Land", manager, second, C(0, 1));
            Check(landing2 != null && !landing2.Coordinate.Equals(landing.Coordinate), "기존 다른 칸 예약 유지"); Hit(state, landing2.Coordinate, context); Invoke(typeof(DroneTargetManager), "Invalidate", manager);
            Check(state.Missions[0].Progress == 1 && manager.ReservationCount == 0 && manager.ExpectedComplete == 0 && manager.Query().All(t => !t.IsMission), "실제 본체1제거/예약해제/일반 대체");
            foreach (int durability in new[] { 2, 9 })
            {
                LevelDefinition areaLevel = Make(); Obstacle(areaLevel, ObstacleKind.Appliance, durability, C(4, 4)); SetMission(areaLevel, ObstacleKind.Appliance);
                LevelRuntimeState areaState = Build(areaLevel); TurnEffectContext areaContext = Context(); DroneTargetManager area = Manager(areaState, areaContext);
                DroneTarget range = area.QueryArea(PowerArea.Blast3).First(t => t.Coordinate.Equals(C(4, 4)));
                Check(range.Contributions.Single().Damage == Math.Min(4, durability) && range.Contributions.Single().ExpectedComplete == (durability <= 4 ? 1 : 0), "범위 후보 본체 합산/상한 " + durability);
                int request = (int)Invoke(typeof(DroneTargetManager), "RequestArea", area, C(0, 0), PowerArea.Blast3);
                Invoke(typeof(DroneTargetManager), "Release", area, request, C(0, 0), "검증 취소");
                Check(area.ReservationCount == 0 && area.ExpectedDamage == 0 && areaContext.LastHit == 0, "예약 취소 실제 타격 이력 불변 " + durability);
            }
        }
    }
}
