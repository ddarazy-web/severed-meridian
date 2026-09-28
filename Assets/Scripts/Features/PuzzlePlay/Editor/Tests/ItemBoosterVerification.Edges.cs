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
    public static partial class ItemBoosterVerification
    {
        public static void Edges()
        {
            Directory.CreateDirectory(Evidence); Results.Clear();
            try { EdgeChecks(); File.WriteAllLines(Evidence + "/edge-results.txt", Results); EditorApplication.Exit(0); }
            catch (Exception error) { Results.Add("FAIL " + error); File.WriteAllLines(Evidence + "/edge-results.txt", Results); Debug.LogException(error); EditorApplication.Exit(1); }
        }
        private static void EdgeChecks()
        {
            foreach (ObstacleKind kind in new[] { ObstacleKind.Crate, ObstacleKind.Safe, ObstacleKind.ColorLock, ObstacleKind.Appliance, ObstacleKind.Scrap })
            {
                LevelDefinition level = (LevelDefinition)Invoke(typeof(PowerEffectVerification), "Make");
                Invoke(typeof(FixedObstacleVerification), "Obstacle", level, kind, 3, C(4, 4), RabbitColor.Type5);
                BoardActionExecutor executor = new BoardActionExecutor(Build(level));
                ItemUseResult result = executor.UseItem(BoardItem.Hammer, C(4, 4));
                Check(result.IsApplied && executor.State.Obstacles[0].Durability == 2 && result.Effects.Count(e => e.Response == DamageResponse.Damage) == 1, "망치 본체 1피해 " + kind);
                Finish(executor);
                BoardCoordinate remaining = executor.State.Cells.First(c => c.ObstacleIndex == 0).Coordinate;
                Check(executor.UseItem(BoardItem.Hammer, remaining).IsApplied && executor.State.Obstacles[0].Durability == 1 && executor.Turn == 2 && executor.State.MovesRemaining == 20,
                    "다음 아이템 독립 피해 단위 " + kind);
                UnityEngine.Object.DestroyImmediate(level);
            }
            LevelDefinition generator = (LevelDefinition)Invoke(typeof(GeneratorVerification), "Make", ObstacleKind.Safe, 3);
            BoardActionExecutor charged = new BoardActionExecutor(Build(generator));
            Check(charged.UseItem(BoardItem.Hammer, C(4, 4)).IsApplied && charged.State.Obstacles[0].Charge == 1 && charged.TurnEffects.Generators.Count == 1, "망치 발전기 한 번 충전");
            Finish(charged);
            Check(charged.UseItem(BoardItem.Hammer, C(5, 5)).IsApplied && charged.State.Obstacles[0].Charge == 2, "다른 점유 칸도 다음 아이템 본체 충전");
            UnityEngine.Object.DestroyImmediate(generator);

            LevelDefinition matched = (LevelDefinition)Invoke(typeof(BoardActionVerification), "RocketBoard");
            BoardActionExecutor matching = new BoardActionExecutor(Build(matched));
            Check(matching.UseItem(BoardItem.Swap, C(2, 3), C(3, 3)).IsApplied && matching.ItemUses[0].Changes.Count(c => c.IsTransformation) == 1, "자리 바꾸기 일반 매칭·파워 생성");
            RuntimeCell rocket = matching.State.Cells.Single(c => c.Content == RuntimeContent.Rocket);
            Check(matching.TurnEffects.IsProtected(rocket.Coordinate), "아이템으로 생성한 파워 현재 수 보호");
            Finish(matching); rocket = matching.State.Cells.Single(c => c.Content == RuntimeContent.Rocket);
            Check(matching.UseItem(BoardItem.Hammer, rocket.Coordinate).Effects.Any(e => e.Response == DamageResponse.Activate), "다음 아이템은 이전 파워 보호 해제");
            UnityEngine.Object.DestroyImmediate(matched);

            LevelDefinition wall = (LevelDefinition)Invoke(typeof(PowerEffectVerification), "Make");
            LevelFlowEditing.SetWalls(wall, new[] { new BoardEdge(C(4, 4), C(4, 5)) }, false);
            Invoke(typeof(ScrapVerification), "Place", wall, C(4, 4), 3);
            BoardActionExecutor swapping = new BoardActionExecutor(Build(wall)); string before = Snapshot(swapping.State);
            Check(!swapping.UseItem(BoardItem.Swap, C(4, 4), C(4, 5)).IsApplied && Snapshot(swapping.State) == before && swapping.ItemUses.Count == 0, "자리 바꾸기 벽 차단·실패 원자성");
            Check(swapping.UseItem(BoardItem.Swap, C(4, 4), C(4, 3)).IsApplied && swapping.State.CellAt(C(4, 3)).ObstacleIndex == 0 && swapping.State.Obstacles[0].Durability == 3, "고철은 피해 없이 위치 교환");
            UnityEngine.Object.DestroyImmediate(wall);
            LevelDefinition recovery = (LevelDefinition)Invoke(typeof(RecoveryVerification), "Make", 1);
            Invoke(typeof(RecoveryVerification), "Place", recovery, C(8, 0));
            BoardActionExecutor recovered = new BoardActionExecutor(Build(recovery));
            Check(!recovered.CanSelectItemTarget(BoardItem.Hammer, C(8, 0)) && recovered.UseItem(BoardItem.Swap, C(8, 0), C(9, 0)).IsApplied &&
                recovered.State.Recoveries.Count == 1 && recovered.State.MovesRemaining == 20, "회수 부품 망치 불가·매칭 없이 도착 교환");
            Finish(recovered); Check(recovered.Outcome?.Kind == BoardOutcomeKind.Won && recovered.ItemUses.Count == 1, "아이템 후 정상 성공·라스트팡 종료");
            UnityEngine.Object.DestroyImmediate(recovery);

            foreach (int count in new[] { 2, 9 })
            {
                LevelDefinition isolated = (LevelDefinition)Invoke(typeof(BoardActionVerification), "Make",
                    Enumerable.Range(0, count).ToDictionary(i => C(i / 5 * 2, i % 5 * 2), i => i % 5), 20);
                BoardActionExecutor shuffling = new BoardActionExecutor(Build(isolated)); string untouched = Snapshot(shuffling.State);
                ItemUseResult rejected = shuffling.UseItem(BoardItem.Shuffle);
                Check(!rejected.IsApplied && rejected.Shuffle.Reason == (count == 2 ? ShuffleReason.Impossible : ShuffleReason.LimitReached) &&
                    Snapshot(shuffling.State) == untouched && shuffling.TurnEffects == null && shuffling.Turn == 0 && shuffling.ItemUses.Count == 0 && shuffling.Outcome == null,
                    "아이템 섞기 실패·한도는 무소모·판 유지 " + count);
                UnityEngine.Object.DestroyImmediate(isolated);
            }
            BoosterEdges();
            TurnBoundaryChecks();
        }

        private static void TurnBoundaryChecks()
        {
            LevelDefinition level = (LevelDefinition)Invoke(typeof(PowerEffectVerification), "Make");
            LevelRuntimeState mold = Build(level);
            Set(mold.CellAt(C(9, 9)), "Cover", (CoverKind?)CoverKind.Mold); Set(mold.CellAt(C(9, 9)), "CoverDurability", 1);
            BoardActionExecutor executor = new BoardActionExecutor(mold);
            executor.UseItem(BoardItem.Hammer, C(0, 0)); Finish(executor);
            Check(executor.State.Cells.Count(c => c.Cover == CoverKind.Mold) == 1 && executor.TurnEffects.MoldSpread.Reason == MoldSpreadReason.NoTurn, "실제 곰팡이가 있어도 아이템 후 확산 생략");
            ActionCandidate next = ActionQuery.Find(executor.State).First();
            BoardActionResult action = next.Second.HasValue ? executor.Swap(next.First, next.Second.Value) : executor.Activate(next.First);
            Check(action.IsApplied && executor.TurnEffects.ConsumesMove && executor.State.MovesRemaining == 19, "아이템 다음 일반 행동은 이동 소비 문맥 복구");
            Finish(executor);
            Check(executor.TurnEffects.MoldSpread.Reason != MoldSpreadReason.NoTurn, "일반 행동 종료의 곰팡이 판정 정상 복구");

            LevelRuntimeState exhausted = Build(level); Set(exhausted, "MovesRemaining", 0);
            BoardActionExecutor lost = new BoardActionExecutor(exhausted, Selected); Finish(lost);
            Check(lost.Outcome.Kind == BoardOutcomeKind.MovesExhausted && lost.BoosterPlacements.Count == 0 && lost.PendingBoosters.Count == 3 && !lost.UseItem(BoardItem.Shuffle).IsApplied,
                "소진 종료는 부스터 배치보다 우선·아이템 차단");
            Invoke(typeof(SettlementVerification), "Source", level, C(0, 0), SupplyExhaustion.Stop, new[] { new SupplyItem(SupplyKind.FixedNormal) });
            BoardActionExecutor interrupted = new BoardActionExecutor(Build(level)); interrupted.UseItem(BoardItem.Hammer, C(0, 0));
            // 이미 효과를 커밋한 다음 정착 단계의 오류를 주입한다. 성공한 아이템 사용을 취소로 바꾸면 안 된다.
            typeof(RuntimeSource).GetField("<Mode>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(interrupted.State.Supply.Sources[0], (SupplyMode)99);
            string applied = Snapshot(interrupted.State), record = Snapshot(interrupted.ItemUses);
            Check(interrupted.Settle().Reason == SettlementReason.Unsupported && interrupted.Outcome.Kind == BoardOutcomeKind.Aborted &&
                Snapshot(interrupted.State) == applied && Snapshot(interrupted.ItemUses) == record && interrupted.ItemUses.Count == 1,
                "후속 정착 오류는 실행 중단·이미 사용한 아이템 기록 보존");
            UnityEngine.Object.DestroyImmediate(level);
        }

        private static void BoosterEdges()
        {
            LevelDefinition level = (LevelDefinition)Invoke(typeof(PowerEffectVerification), "Make");
            LevelRuntimeState source = Build(level);
            foreach (RuntimeCell cell in source.Cells) { Set(cell, "Content", RuntimeContent.Empty); Set(cell, "Color", null); }
            Set(source.CellAt(C(0, 0)), "Content", RuntimeContent.Normal); Set(source.CellAt(C(0, 0)), "Color", (RabbitColor?)RabbitColor.Type1);
            Set(source.CellAt(C(0, 0)), "Cover", (CoverKind?)CoverKind.Web); Set(source.CellAt(C(0, 0)), "CoverDurability", 2);
            Set(source.CellAt(C(0, 1)), "Content", RuntimeContent.Normal); Set(source.CellAt(C(0, 1)), "Color", (RabbitColor?)RabbitColor.Type1);
            Set(source.CellAt(C(0, 1)), "Cover", (CoverKind?)CoverKind.Mold); Set(source.CellAt(C(0, 1)), "CoverDurability", 1);
            Set(source.CellAt(C(9, 9)), "Content", RuntimeContent.Rocket); Set(source.CellAt(C(9, 9)), "RocketDirection", (RocketDirection?)RocketDirection.Horizontal);
            BoardActionExecutor waiting = new BoardActionExecutor(source, Selected);
            Check(waiting.PendingBoosters.Count == 3 && waiting.BoosterPlacements.Count == 0 && Snapshot(source) == Snapshot(waiting.State), "덮개·기존 파워 제외·후보0 무난수 대기");
            Check(waiting.UseItem(BoardItem.Hammer, C(0, 1)).IsApplied, "곰팡이 내용물 노출로 부스터 후보 제공");
            Finish(waiting);
            Check(waiting.BoosterPlacements.Count == 1 && waiting.BoosterPlacements[0].Booster == StartBooster.Rocket && waiting.BoosterPlacements[0].Turn == 1 &&
                waiting.PendingBoosters.SequenceEqual(new[] { StartBooster.Bomb, StartBooster.Magnet }), "정착 뒤 후보에 첫 대기 부스터만 배치");
            string replay = Snapshot(waiting.State) + Snapshot(waiting.BoosterPlacements);
            BoardActionExecutor same = new BoardActionExecutor(source, Selected); same.UseItem(BoardItem.Hammer, C(0, 1)); Finish(same);
            Check(replay == Snapshot(same.State) + Snapshot(same.BoosterPlacements), "대기 해소 위치·방향·난수 재현");
            Check(waiting.UseItem(BoardItem.Hammer, C(0, 0)).IsApplied, "거미줄 1단계 손상"); Finish(waiting);
            Check(waiting.BoosterPlacements.Count == 1 && waiting.PendingBoosters.Count == 2, "거미줄 남으면 부스터 대기 유지");
            waiting.UseItem(BoardItem.Hammer, C(0, 0)); Finish(waiting);
            Check(waiting.BoosterPlacements.Count == 2 && waiting.BoosterPlacements[1].Booster == StartBooster.Bomb && waiting.PendingBoosters.Single() == StartBooster.Magnet, "다음 안정 경계에서 폭탄 순서 배치·자석 대기");
            bool duplicate = false, unsupported = false;
            try { _ = new BoardActionExecutor(source, new[] { StartBooster.Rocket, StartBooster.Rocket }); } catch (ArgumentException) { duplicate = true; }
            try { _ = new BoardActionExecutor(source, new[] { (StartBooster)99 }); } catch (ArgumentException) { unsupported = true; }
            Check(duplicate && unsupported, "부스터 중복·미지원 종류 거절");
            UnityEngine.Object.DestroyImmediate(level);
        }
    }
}
