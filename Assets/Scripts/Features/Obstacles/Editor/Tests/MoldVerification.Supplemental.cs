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
    public static partial class MoldVerification
    {
        public static void Supplemental()
        {
            Results.Clear();
            try { SupplementalChecks(); File.WriteAllLines(Evidence + "/supplemental-results.txt", Results); EditorApplication.Exit(0); }
            catch(Exception error) { Results.Add("FAIL " + error); File.WriteAllLines(Evidence + "/supplemental-results.txt", Results); Debug.LogException(error); EditorApplication.Exit(1); }
        }
        private static void SupplementalChecks()
        {
            LevelDefinition wall = Make(); Missions(wall); Mold(wall, C(4, 4));
            LevelFlowEditing.SetWalls(wall, new[] { new BoardEdge(C(4, 4), C(4, 5)), new BoardEdge(C(4, 4), C(3, 4)), new BoardEdge(C(4, 4), C(5, 4)) }, false);
            LevelObstacleEditing.Apply(wall, new PlacementBrush { Layer = PlacementLayer.Cover, Kind = (int)CoverKind.Web, Durability = 2 }, new[] { C(4, 3) });
            LevelRuntimeState blocked = Build(wall);
            Check(End(blocked, Context()).Reason == MoldSpreadReason.NoCandidate, "벽/거미줄 후보 제외");
            Check(DamageReaction.Evaluate(blocked, C(4, 4), DamageCause.AdjacentMatch, C(4, 5), Context()).Response == DamageResponse.Wall, "벽 너머 인접 피해 금지");
            Hit(blocked, C(4, 4), Context()); Check(blocked.CellAt(C(4, 4)).Cover == null && blocked.CellAt(C(4, 4)).Content == RuntimeContent.Normal, "직접 파워 벽과 무관 덮개 제거");

            LevelDefinition portal = Make(); Mold(portal, C(0, 0)); LevelFlowEditing.SetPortal(portal, C(0, 0), C(9, 9)); LevelFlowEditing.SetMerge(portal, C(9, 9), new[] { C(0,0), C(8,9) });
            LevelRuntimeState remote = Build(portal);
            foreach(RuntimeCell cell in remote.Cells.Where(c => c.Cover == null && !c.Coordinate.Equals(C(9, 9)))) { Set(cell, "Content", RuntimeContent.Empty); Set(cell, "Color", null); }
            Check(End(remote, Context()).Reason == MoldSpreadReason.NoCandidate, "원격 통로 출구 확산 금지");
            LevelDefinition objects = Make(); Mold(objects, C(4, 4));
            Invoke(typeof(ScrapVerification), "Place", null, objects, C(4, 5), 3); Invoke(typeof(PowerEffectVerification), "Crate", null, objects, C(3, 4), 3);
            LevelObstacleEditing.Apply(objects, new PlacementBrush { Layer = PlacementLayer.Cover, Kind = (int)CoverKind.Web, Durability = 2 }, new[] { C(4, 3) });
            LevelRuntimeState exclusions = Build(objects); Set(exclusions.CellAt(C(5, 4)), "Content", RuntimeContent.Recovery); Set(exclusions.CellAt(C(5, 4)), "Color", null);
            Check(End(exclusions, Context()).Reason == MoldSpreadReason.NoCandidate, "고철/상자/회수/다른 덮개 확산 제외");

            foreach(bool complete in new[] { false, true })
            {
                LevelDefinition last = (LevelDefinition)Invoke(typeof(CombinationVerification), "Make", null, 1, RocketDirection.Horizontal, null);
                Mold(last, C(9, 0)); JsonUtility.FromJsonOverwrite("{\"moveCount\":1}", last);
                BoardActionExecutor executor = new BoardActionExecutor(Build(last)); Check(executor.Swap(C(4, 4), C(4, 5)).IsApplied, "곰팡이 마지막 수 조합 " + complete);
                if(complete) foreach(RuntimeMission mission in executor.State.Missions) Set(mission, "Progress", mission.Target);
                Check(executor.TurnEffects.MoldSpread == null, "효과 중 확산 없음 " + complete);
                Finish(executor);
                TurnEffectContext finalTurn = complete ? executor.WinningTurnEffects : executor.TurnEffects;
                Check(finalTurn.MoldSpread.Reason == (complete ? MoldSpreadReason.MissionsComplete : MoldSpreadReason.Spread) && executor.State.MovesRemaining == 0 &&
                    executor.Outcome.Kind == (complete ? BoardOutcomeKind.Won : BoardOutcomeKind.MovesExhausted), "달성 선확인/미달성 확산 후 마지막 수 종료 " + complete);
                string before = Snapshot(executor.State) + ContextSnapshot(executor); executor.AdvanceCascade(); executor.Activate(C(0, 0));
                Check(Snapshot(executor.State) + ContextSnapshot(executor) == before, "종료 후 반복 조작 확산/난수 무변경 " + complete);
            }
            LevelDefinition match = (LevelDefinition)Invoke(typeof(BoardActionVerification), "Make", null, new Dictionary<BoardCoordinate,int> { [C(3,2)]=0, [C(3,3)]=1, [C(3,4)]=0, [C(3,5)]=0, [C(2,3)]=0, [C(3,6)]=2 },20); Mold(match, C(3, 6)); Missions(match);
            BoardActionExecutor matching = new BoardActionExecutor(Build(match));
            Check(matching.Swap(C(3, 3), C(2, 3)).IsApplied && matching.TurnEffects.RemovedMold && matching.State.Missions[1].Remaining == 0, "실제 매칭 인접 곰팡이 제거/미션");
            Finish(matching); Check(matching.TurnEffects.MoldSpread.Reason == MoldSpreadReason.RemovedThisTurn, "여러 연쇄 후에도 제거 이력 유지");
            TurnEffectContext next = (TurnEffectContext)Invoke(typeof(TurnEffectContext), "NextTurn", matching.TurnEffects, 2);
            Check(!next.RemovedMold && next.MoldSpread == null, "다음 수 제거/확산 이력 초기화");

            LevelDefinition exposed = (LevelDefinition)Invoke(typeof(BoardActionVerification), "Make", null,
                new Dictionary<BoardCoordinate,int> { [C(0,0)]=0, [C(0,1)]=0, [C(0,2)]=0 }, 20);
            Mold(exposed, C(0, 1)); BoardActionExecutor automatic = (BoardActionExecutor)Invoke(typeof(CascadeVerification), "Automatic", null, exposed, 12345);
            Check(MatchQuery.Find(automatic.State).Count == 0, "곰팡이 숨은 칸 매칭 제외"); Hit(automatic.State, C(0,1), automatic.TurnEffects);
            Check(automatic.ResolveAutomaticMatch().IsApplied && automatic.State.Cells.All(c => c.Content == RuntimeContent.Empty), "노출 후 새 자동 매칭 처리");

            LevelDefinition protectedLevel = (LevelDefinition)Invoke(typeof(BoardActionVerification), "RocketBoard", null);
            BoardActionExecutor protection = new BoardActionExecutor(Build(protectedLevel)); protection.Swap(C(3,3), C(2,3));
            RuntimeCell power = protection.State.Cells.First(c => protection.TurnEffects.IsProtected(c.Coordinate)); Set(power, "Cover", CoverKind.Mold); Set(power, "CoverDurability", 1);
            Hit(protection.State, power.Coordinate, protection.TurnEffects); Hit(protection.State, power.Coordinate, protection.TurnEffects);
            Check(power.Content != RuntimeContent.Empty && power.Cover == null && protection.TurnEffects.IsProtected(power.Coordinate), "곰팡이 제거 후에도 신규 파워 보호 보존");

            LevelDefinition drones = Make(); Mold(drones, C(4,4), C(4,5)); Missions(drones); LevelRuntimeState state = Build(drones);
            Set(state.Missions[0], "Progress", state.Missions[0].Target); TurnEffectContext context = Context();
            DroneTargetManager manager = (DroneTargetManager)Invoke(typeof(TargetPowerVerification), "Manager", null, state, context);
            Check(manager.Query().Count == 2 && manager.Query().All(t => t.Contributions.Single().MissionIndex == 1), "드론 숨은 색 대신 곰팡이 미션 후보");
            int request = (int)Invoke(typeof(DroneTargetManager), "Request", manager, C(0,0)); BoardCoordinate reserved = context.Targeting.Last().Target.Value;
            Check(manager.Query().Count == 1 && manager.ExpectedComplete == 1 && state.Missions[1].Remaining == 2, "공유 예약 예상/실제 진행 분리");
            Hit(state, reserved, context); Invoke(typeof(DroneTargetManager), "Invalidate", manager);
            DroneTarget landed = (DroneTarget)Invoke(typeof(DroneTargetManager), "Land", manager, request, C(0,0));
            Check(landed.IsMission && !landed.Coordinate.Equals(reserved) && manager.ReservationCount == 0 && context.RemovedMold, "곰팡이 소실 재탐색/예약 해제/제거 기록 유지");
            Hit(state, landed.Coordinate, context); Invoke(typeof(DroneTargetManager), "Invalidate", manager);
            Check(manager.Query().All(t => !t.IsMission && t.Cover != CoverKind.Mold), "곰팡이 미션 종료 후 노출 일반 대체");
            LevelRuntimeState areaState = Build(drones); Set(areaState.Missions[0], "Progress", areaState.Missions[0].Target);
            DroneTargetManager areaManager = (DroneTargetManager)Invoke(typeof(TargetPowerVerification), "Manager", null, areaState, Context());
            int areaRequest = (int)Invoke(typeof(DroneTargetManager), "RequestArea", areaManager, C(0,0), PowerArea.Blast3);
            Check(areaManager.ExpectedComplete >= 1 && areaManager.ExpectedDamage >= 1, "곰팡이 범위 예약 기여"); Invoke(typeof(DroneTargetManager), "Land", areaManager, areaRequest, C(0,0)); Check(areaManager.ReservationCount == 0, "범위 예약 착탄 해제");

            LevelDefinition failure = (LevelDefinition)Invoke(typeof(BoardActionVerification), "Make", null,
                new Dictionary<BoardCoordinate,int> { [C(0,0)]=0, [C(0,1)]=0, [C(0,2)]=0, [C(1,0)]=1, [C(1,1)]=1 },20);
            Mold(failure,C(1,0)); Invoke(typeof(PowerEffectVerification), "Crate", null, failure, C(1,1),2);
            BoardActionExecutor failed = (BoardActionExecutor)Invoke(typeof(CascadeVerification), "Automatic", null, failure,12345);
            typeof(RuntimeObstacle).GetField("<Definition>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(failed.State.Obstacles[0],JsonUtility.FromJson<ObstaclePlacementDefinition>("{\"kind\":99,\"durability\":2}"));
            string snapshot=Snapshot(failed.State)+ContextSnapshot(failed);
            Check(!failed.ResolveAutomaticMatch().IsApplied && Snapshot(failed.State)+ContextSnapshot(failed)==snapshot,"곰팡이 제거 뒤 실패에서 덮개·미션·턴 제거 기록 원자적 보존");
        }
    }
}

