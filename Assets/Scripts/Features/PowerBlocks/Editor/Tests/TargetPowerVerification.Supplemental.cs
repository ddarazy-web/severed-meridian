using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Board;
using Simulation;
using UnityEditor;
using UnityEngine;

namespace Levels.Editor
{
    public static partial class TargetPowerVerification
    {
        public static void Supplemental()
        {
            Results.Clear();
            try { SupplementalChecks(); File.WriteAllLines(Evidence + "/supplemental-results.txt", Results); EditorApplication.Exit(0); }
            catch (Exception error) { Results.Add("FAIL " + error); File.WriteAllLines(Evidence + "/supplemental-results.txt", Results); Debug.LogException(error); EditorApplication.Exit(1); }
        }

        private static void SupplementalChecks()
        {
            foreach (bool reverse in new[] { false, true })
            {
                LevelDefinition level = (LevelDefinition)Invoke(typeof(PowerEffectVerification), "ProtectionBoard", null);
                Place(level, C(3, 3), InitialBlockKind.Magnet);
                BoardActionExecutor executor = new BoardActionExecutor(Build(level));
                BoardActionResult result = executor.Swap(reverse ? C(2, 3) : C(3, 3), reverse ? C(3, 3) : C(2, 3));
                Check(result.IsApplied && result.Changes.Count == 4 && result.Changes.Any(c => c.Coordinate.Equals(C(3, 3)) && c.IsTransformation), "자석 교환 상대가 첫 매칭으로 변환 " + reverse);
                Check(executor.TurnEffects.Targeting.Single(r => r.Event == TargetingEvent.ColorSelected).Color == RabbitColor.Type1 && executor.State.CellAt(C(3, 3)).Content == RuntimeContent.Rocket,
                    "상대 소실 후에도 교환 색 유지/매칭 파워 보호 " + reverse);
            }

            // 실제 부모 로켓의 남은 범위가 드론 예약 목표를 없앤 뒤 재탐색하는 통합 사례.
            LevelDefinition pending = Make(); Mission(pending, MissionKind.Color, 2, RabbitColor.Type5);
            Place(pending, C(4, 0), InitialBlockKind.Rocket); Place(pending, C(4, 2), InitialBlockKind.Drone);
            LevelRuntimeState pendingState = Build(pending);
            foreach (RuntimeCell cell in pendingState.Cells.Where(c => c.Content == RuntimeContent.Normal)) Set(cell, "Color", RabbitColor.Type2);
            Set(pendingState.CellAt(C(4, 9)), "Color", RabbitColor.Type5); Set(pendingState.CellAt(C(9, 9)), "Color", RabbitColor.Type5);
            bool retargeted = false;
            for (int seed = 0; seed < 16 && !retargeted; seed++)
            {
                LevelRuntimeState work = Build(pending, seed);
                foreach (RuntimeCell cell in work.Cells.Where(c => c.Content == RuntimeContent.Normal)) Set(cell, "Color", pendingState.CellAt(cell.Coordinate).Color);
                TurnEffectContext context = Context(); Effects(work, C(4, 0), context);
                if (!context.Targeting.Any(r => r.Event == TargetingEvent.Retargeted)) continue;
                Check(context.Targeting.Last(r => r.Event == TargetingEvent.Landed).Target.Value.Equals(C(9, 9)) && work.Missions[0].Progress == 2,
                    "실제 부모 로켓이 예약 파괴 → 남은 미션 재조준/실제 진행2");
                retargeted = true;
            }
            Check(retargeted, "실제 연쇄 중 목표 소실 재탐색 재현");

            LevelDefinition mixed = Make(); Mission(mixed, MissionKind.Color, 100);
            Place(mixed, C(4, 0), InitialBlockKind.Rocket); Place(mixed, C(4, 2), InitialBlockKind.Bomb);
            Place(mixed, C(4, 3), InitialBlockKind.Drone); Place(mixed, C(4, 4), InitialBlockKind.Magnet);
            BoardActionExecutor chain = new BoardActionExecutor(Build(mixed)); BoardActionResult fired = chain.Activate(C(4, 0));
            Check(fired.IsApplied && fired.Effects.Count(e => e.Response == DamageResponse.Activate) == 4 && fired.Effects.Where(e => e.Response == DamageResponse.Activate).Select(e => e.Target).Distinct().Count() == 4,
                "4종 혼합 피격 연쇄/중복 발동 없음");
            Check(chain.State.Missions[0].Progress == fired.Effects.Count(e => e.Response == DamageResponse.Remove && e.OriginalColor == RabbitColor.Type1), "중첩 효과 색 소비 무중복");

            LevelDefinition edge = Make(); Place(edge, C(0, 0), InitialBlockKind.Drone);
            LevelFlowEditing.SetWalls(edge, new[] { new BoardEdge(C(0, 0), C(0, 1)) }, false);
            LevelRuntimeState edgeState = Build(edge); TurnEffectContext edgeContext = Context();
            List<EffectRecord> edgeEffects = Effects(edgeState, C(0, 0), edgeContext);
            Check(edgeEffects.Any(e => e.Target.Equals(C(0, 1)) && e.Response == DamageResponse.Remove), "드론 +는 벽을 무시");
            LevelDefinition sparse = (LevelDefinition)Invoke(typeof(BoardActionVerification), "Make", null,
                new Dictionary<BoardCoordinate, int> { [C(0, 0)] = 0, [C(0, 1)] = 1, [C(9, 9)] = 0 }, 20);
            Place(sparse, C(0, 0), InitialBlockKind.Drone);
            TurnEffectContext sparseContext = Context(); List<EffectRecord> sparseEffects = Effects(Build(sparse), C(0, 0), sparseContext);
            Check(sparseEffects.Count(e => e.Response == DamageResponse.Remove) == 2, "비활성칸 제외 +1칸/추가1칸");

            LevelDefinition equal = Make(); Mission(equal, MissionKind.Color, 1); Mission(equal, MissionKind.Crate, 1);
            Invoke(typeof(PowerEffectVerification), "Crate", null, equal, C(9, 9), 1);
            HashSet<RuntimeContent> selected = new HashSet<RuntimeContent>();
            for (int seed = 0; seed < 80; seed++)
            {
                LevelRuntimeState state = Build(equal, seed); TurnEffectContext context = Context(); DroneTargetManager manager = Manager(state, context);
                DroneTarget[] candidates = manager.Query().ToArray(); int expected = new System.Random(seed).Next(candidates.Length);
                Invoke(typeof(DroneTargetManager), "Request", manager, C(0, 0));
                BoardCoordinate target = context.Targeting.Last().Target.Value; selected.Add(state.CellAt(target).Content);
                Check(target.Equals(candidates[expected].Coordinate), "색/상자 후보 동등 무작위 " + seed);
            }
            Check(selected.Contains(RuntimeContent.Normal) && selected.Contains(RuntimeContent.Obstacle), "색/상자 사이 추가 우선순위 없음");

            LevelDefinition last = ReplayBoard(); JsonUtility.FromJsonOverwrite("{\"moveCount\":1}", last);
            BoardActionExecutor finalMove = new BoardActionExecutor(Build(last)); finalMove.Activate(C(4, 4));
            while (finalMove.HasPendingCascade) Check(finalMove.AdvanceCascade().IsApplied, "마지막 수 연쇄 단계 성공");
            Check(finalMove.LastCascadeStep.Reason == CascadeStepReason.MovesExhausted && finalMove.TurnEffects.Targeting.Count(r => r.Event == TargetingEvent.Landed) == 2, "마지막 수 드론 착탄/정착 후 수 소진");

            LevelRuntimeState single = Build(sparse); Set(single.CellAt(C(0, 1)), "Content", RuntimeContent.Empty); Set(single.CellAt(C(0, 1)), "Color", null);
            TurnEffectContext oneContext = Context(); DroneTargetManager oneManager = Manager(single, oneContext);
            int draws = single.Random.DrawCount; int request = (int)Invoke(typeof(DroneTargetManager), "Request", oneManager, C(0, 0));
            Check(single.Random.DrawCount == draws && oneManager.ReservationCount == 1, "드론 단일 후보 선택 무난수");
            Set(single.CellAt(C(9, 9)), "Content", RuntimeContent.Empty); Set(single.CellAt(C(9, 9)), "Color", null); Invoke(typeof(DroneTargetManager), "Invalidate", oneManager);
            Check(Invoke(typeof(DroneTargetManager), "Land", oneManager, request, C(0, 0)) == null && oneManager.ReservationCount == 0 && oneManager.ExpectedComplete == 0,
                "예약 후 모든 후보 소실: 해제/재탐색/추가 타격 없음");

            // 내부 한도는 정상 보드로 도달할 수 없으므로 중복 단계 입력으로 방어 분기를 검증한다.
            LevelDefinition limitLevel = (LevelDefinition)Invoke(typeof(BoardActionVerification), "RocketBoard", null);
            BoardActionExecutor limitSource = new BoardActionExecutor(Build(limitLevel));
            BoardActionResult initialAction = limitSource.Swap(C(2, 3), C(3, 3));
            string successful = Snapshot(limitSource.State), successfulContext = Snapshot(limitSource.TurnEffects);
            LevelRuntimeState working = new BoardActionExecutor(limitSource.State).State;
            TurnEffectContext workingContext = (TurnEffectContext)Invoke(typeof(TurnEffectContext), "Copy", limitSource.TurnEffects);
            object[] arguments = { working, Enumerable.Repeat(initialAction.Changes.First(), 20001), null, workingContext, new List<EffectRecord>(), null };
            Check(!(bool)Invoke(typeof(PowerEffectResolution), "Apply", null, arguments) && ((string)arguments[5]).Contains("한도"), "효과 한도 초과 실패 진단 (중복 단계 입력)");
            Check(Snapshot(limitSource.State) == successful && Snapshot(limitSource.TurnEffects) == successfulContext, "실패 작업 사본은 앞선 성공 상태/문맥 보존");
        }
    }
}
