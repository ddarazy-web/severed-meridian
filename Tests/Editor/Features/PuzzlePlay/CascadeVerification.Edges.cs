using System;
using System.Collections.Generic;
using System.Linq;
using Board;
using Simulation;

namespace Levels.Editor
{
    public static partial class CascadeVerification
    {
        private static LevelDefinition ChainBoard(int count = 9)
        {
            LevelDefinition level = Make(new[] { C(0, 0), C(1, 0), C(2, 0), C(1, 1), C(1, 3) });
            Invoke(typeof(PowerEffectVerification), "Crate", null, level, C(1, 1), 3);
            foreach (BoardCoordinate c in new[] { C(2, 0), C(1, 3) })
            {
                LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Block, Erase = true }, new[] { c });
                LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Block, Kind = (int)InitialBlockKind.Rocket,
                    Direction = c.Column == 0 ? RocketDirection.Vertical : RocketDirection.Horizontal }, new[] { c });
            }
            Invoke(typeof(SettlementVerification), "Source", null, level, C(0, 0), SupplyExhaustion.Stop, new[] { new SupplyItem(SupplyKind.FixedNormal, count) });
            return level;
        }

        private static void Finish(BoardActionExecutor executor)
        {
            int count = 0;
            while (executor.HasPendingCascade && count++ < executor.CascadeLimit * 2 + 4)
            {
                CascadeStepResult result = executor.AdvanceCascade();
                if (!result.IsApplied) throw new InvalidOperationException(result.Message);
            }
            if (executor.HasPendingCascade) throw new InvalidOperationException("검증 연쇄가 끝나지 않음");
        }

        private static void EdgeChecks()
        {
            LevelDefinition chain = ChainBoard(); BoardActionExecutor executor = new BoardActionExecutor(Build(chain));
            Check(executor.Activate(C(2, 0)).IsApplied, "3회 연쇄 실제 파워 입력"); Finish(executor);
            Check(executor.CascadeRounds == 3 && executor.State.Obstacles[0].Durability == 2 && executor.State.MovesRemaining == 19 && executor.TurnEffects.HasDamaged(0), "3회 자동 연쇄/상자 같은 턴 1피해/공급 9개");
            Check(executor.Activate(C(1, 3)).IsApplied && executor.State.Obstacles[0].Durability == 1 && executor.Turn == 2 && executor.State.MovesRemaining == 18, "다음 턴 상자 다시 피해/수 1회");

            BoardActionExecutor longChain = new BoardActionExecutor(Build(ChainBoard(150)));
            longChain.Activate(C(2, 0)); Finish(longChain);
            Check(longChain.CascadeHistory.TakeWhile(step => step.Reason != CascadeStepReason.Won).Count(step => step.Reason == CascadeStepReason.Matched) == 50 &&
                longChain.State.Supply.Sources[0].ItemIndex == 1 && longChain.State.MovesRemaining == 19 && longChain.Outcome.Kind == BoardOutcomeKind.Won, "정상 긴 50회 연쇄 완료 후 성공·라스트팡");
            BoardActionExecutor limited = new BoardActionExecutor(Build(ChainBoard(1203))); limited.Activate(C(2, 0));
            while (limited.CascadeRounds < limited.CascadeLimit) CheckStep(limited);
            CheckStep(limited); string beforeLimit = StablePayload(limited); int history = limited.CascadeHistory.Count;
            Check(limited.AdvanceCascade().Reason == CascadeStepReason.LimitReached && StablePayload(limited) == beforeLimit && limited.Outcome.Kind == BoardOutcomeKind.Aborted && limited.Phase == BoardActionPhase.Stopped && limited.CascadeHistory.Count == history, "연쇄 한도 실패/마지막 성공 정착 보존");

            BoardCoordinate[] line = Enumerable.Range(0, 4).Select(c => C(0, c)).ToArray();
            // 단계 중 미지원 반응을 만나는 실패 경계를 직접 구성한다.
            LevelDefinition withCrate = Make(line.Concat(new[] { C(1, 1) })); Invoke(typeof(PowerEffectVerification), "Crate", null, withCrate, C(1, 1), 3);
            BoardActionExecutor failed = Automatic(withCrate); typeof(RuntimeObstacle).GetField("<Definition>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(failed.State.Obstacles[0], UnityEngine.JsonUtility.FromJson<ObstaclePlacementDefinition>("{\"kind\":99,\"durability\":3}"));
            string beforeFailure = StablePayload(failed);
            Check(failed.AdvanceCascade().Reason == CascadeStepReason.Unsupported && StablePayload(failed) == beforeFailure && failed.Outcome.Kind == BoardOutcomeKind.Aborted && failed.Phase == BoardActionPhase.Stopped && failed.CascadeHistory.Count == 0, "자동 위치 난수 추출 뒤 미지원 효과 실패도 전체 문맥 보존");

            BoardActionExecutor replayA = new BoardActionExecutor(Build(chain)), replayB = new BoardActionExecutor(Build(chain));
            replayA.Activate(C(2, 0)); replayB.Activate(C(2, 0)); Finish(replayA);
            while (replayB.HasPendingCascade)
            {
                if (replayB.Phase == BoardActionPhase.WaitingForFall) replayB.Settle();
                else replayB.ResolveAutomaticMatch();
            }
            Check(Context(replayA) == Context(replayB) && Snapshot(replayA.CascadeHistory) == Snapshot(replayB.CascadeHistory), "단계 명령과 통합 Advance의 상태/난수/문맥/결과 동일");

            // 정착 회차와 최종 점유자의 실제 이동 기록을 대조한다.
            LevelDefinition moving = Make(Enumerable.Range(0, 4).Select(r => C(r, 0)));
            LevelRuntimeState state = Build(moving); Set(state.CellAt(C(3, 0)), "Content", RuntimeContent.Empty); Set(state.CellAt(C(3, 0)), "Color", null);
            SettlementResult settled = SettlementResolution.Resolve(state);
            Check(settled.IsApplied && settled.TurnEffects.LastArrival(C(3, 0)) == ((1L << 32) | 1) && settled.TurnEffects.LastArrival(C(2, 0)) == ((1L << 32) | 2) &&
                settled.TurnEffects.LastArrival(C(1, 0)) == ((1L << 32) | 3) && settled.TurnEffects.LastArrival(C(0, 0)) == 0, "통과 좌표 재점유/블록별 최종 도착/빈 좌표 삭제");
            SettlementResult again = SettlementResolution.Resolve(settled.State, settled.TurnEffects);
            Check(again.IsApplied && again.TurnEffects.SettlementCount == 2 && again.TurnEffects.LastArrival(C(1, 0)) == settled.TurnEffects.LastArrival(C(1, 0)), "움직이지 않은 점유자 이전 도착 유지");

            BoardActionExecutor repeated = Automatic(Make(new[] { C(0, 0), C(0, 1), C(0, 2) }));
            string repeatKey = (string)Invoke(typeof(BoardActionExecutor), "CascadeKey", repeated);
            var seen = (Dictionary<string, int>)typeof(BoardActionExecutor).GetField("seenCascadeStates", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(repeated);
            seen[repeatKey] = repeated.State.Random.DrawCount; string beforeRepeat = StablePayload(repeated);
            Check(repeated.AdvanceCascade().Reason == CascadeStepReason.Repeating && StablePayload(repeated) == beforeRepeat && repeated.Outcome.Kind == BoardOutcomeKind.Aborted && repeated.Phase == BoardActionPhase.Stopped, "무진행 반복 검사/보존 (진단 상태 직접 구성)");

            LevelDefinition protectedLevel = (LevelDefinition)Invoke(typeof(SettlementVerification), "FallingBoard", null);
            UnityEngine.JsonUtility.FromJsonOverwrite("{\"moveCount\":3}", protectedLevel);
            LevelSupplyEditing.SetItems(protectedLevel, 0, new[] { new SupplyItem(SupplyKind.FixedNormal, 6) });
            BoardActionExecutor protectedChain = new BoardActionExecutor(Build(protectedLevel));
            protectedChain.Swap(C(3, 3), C(2, 3)); Finish(protectedChain);
            Check(protectedChain.State.Cells.Count(c => protectedChain.TurnEffects.IsProtected(c.Coordinate)) == 2 &&
                protectedChain.State.Cells.Any(c => c.Content == RuntimeContent.Magnet && protectedChain.TurnEffects.IsProtected(c.Coordinate)), "최초 매칭과 자동 매칭 파워 보호 누적/낙하 유지");
            BoardCoordinate rocket = protectedChain.State.Cells.Single(c => c.Content == RuntimeContent.Rocket).Coordinate;
            BoardActionResult nextPower = protectedChain.Activate(rocket);
            Check(nextPower.IsApplied && nextPower.Effects.Any(e => e.Content == RuntimeContent.Magnet && e.Response == DamageResponse.Activate), "다음 턴 보호 해제된 자석 피격 단독 발동");
        }

        private static string StablePayload(BoardActionExecutor executor) => Snapshot(executor.State) + Snapshot(executor.TurnEffects) + executor.Turn + ":" + executor.CascadeRounds;
        private static void CheckStep(BoardActionExecutor executor)
        {
            CascadeStepResult step = executor.AdvanceCascade();
            if (!step.IsApplied) throw new InvalidOperationException(step.Message);
        }
    }
}


