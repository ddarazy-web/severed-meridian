using System;
using System.Linq;
using Levels;
using Simulation;
using UnityEngine;

namespace AutoPlay
{
    public enum BotReplayStatus { Preparing, Ready, Playing, Paused, Completed, Partial, Error, Disposed }

    /// <summary>
    /// 저장한 행동을 공통 실행기에 제출하는 재생기. 전략을 생성하거나 다음 행동을 판단하지 않는다.
    /// 시작 검색·연쇄는 Advance 한 단위로 나누고 일시정지는 이 객체를 그대로 유지한다.
    /// </summary>
    public sealed class BotRecordReplay : IDisposable
    {
        private StartingBoardSearch search;
        private BoardActionExecutor executor;
        private readonly BotBatchGame game;
        private bool requested, wholeRecord, pending;
        public BotReplayStatus Status { get; private set; } = BotReplayStatus.Preparing;
        public string Message { get; private set; } = "기록의 시작 보드 준비 중";
        public int ActionIndex { get; private set; }
        public int ActionCount => game.actions.Length;
        public LevelRuntimeState State => executor?.State;
        public bool NeedsAdvance => Status == BotReplayStatus.Preparing || Status == BotReplayStatus.Playing;
        public bool IsTerminal => Status == BotReplayStatus.Completed || Status == BotReplayStatus.Partial || Status == BotReplayStatus.Error || Status == BotReplayStatus.Disposed;

        /// <param name="record">저장된 실행 조건.</param><returns>재생 불가 이유. 호환되면 null.</returns>
        public static string CompatibilityError(BotBatchRecord record)
        {
            if (record == null || record.formatVersion != 1) return "지원하지 않는 기록 형식입니다.";
            if (record.engineVersion != BoardActionExecutor.Version || record.sessionVersion != BotPlaySession.Version)
                return "기록 당시 게임 규칙 또는 행동 기록 방식이 다릅니다. 통계만 확인할 수 있습니다.";
            // 27단계 v1에는 시작 검색 버전 필드가 없었다. 당시 세션 v3는 DFS v1을 사용했다.
            string starting = string.IsNullOrEmpty(record.startingVersion) ? "starting-board-dfs-v1" : record.startingVersion;
            if (starting != StartingBoardSearch.AlgorithmVersion) return "시작 보드 구성 방식이 다릅니다. 통계만 확인할 수 있습니다.";
            // 기본/계획/가정 버전 차이는 행동 재생에 영향이 없다. 저장된 명령을 실행하기 때문이다.
            return null;
        }

        /// <param name="definition">기록에서 복원한 정의 사본.</param><param name="record">묶음 조건.</param>
        /// <param name="game">검증된 사례. 외부 변경과 분리하기 위해 값으로 복사한다.</param>
        public BotRecordReplay(LevelDefinition definition, BotBatchRecord record, BotBatchGame game)
        {
            string error = CompatibilityError(record);
            if (error != null) throw new InvalidOperationException(error);
            if (LevelStateBuilder.Fingerprint(definition) != record.fingerprint) throw new InvalidOperationException("재생 정의의 지문이 다릅니다.");
            if (game == null || game.batchId != record.id || game.actions == null || game.missions == null)
                throw new ArgumentException("재생할 판 기록이 올바르지 않습니다.", nameof(game));
            this.game = JsonUtility.FromJson<BotBatchGame>(JsonUtility.ToJson(game));
            if ((game.outcome == BotSessionStatus.Stopped || game.outcome == BotSessionStatus.Error) && game.actions.Length == 0 && game.remainingMoves < 0)
            { Status = BotReplayStatus.Partial; Message = "시작 상태가 기록되기 전 중단된 사례입니다. 재생할 완료 행동이 없습니다."; return; }
            search = new StartingBoardSearch(definition, game.seed);
        }

        /// <param name="all">true면 끝까지, false면 현재/다음 행동의 후속 처리까지만 진행한다.</param>
        /// <returns>재생 요청 수락 여부.</returns>
        public bool Begin(bool all)
        {
            if (IsTerminal) return false;
            requested = true; wholeRecord = all;
            Status = search == null ? BotReplayStatus.Playing : BotReplayStatus.Preparing;
            return true;
        }

        /// <summary>진행 객체를 버리지 않고 갱신만 멈춘다.</summary>
        public void Pause()
        {
            if (IsTerminal) return;
            Status = BotReplayStatus.Paused; requested = false; Message = "재생 일시정지 · 현재 처리 상태 유지";
        }

        /// <summary>검색 또는 실행기 처리 한 단위만 수행한다. 시간에 따라 결과를 건너뛰지 않는다.</summary>
        public void Advance()
        {
            if (!NeedsAdvance) return;
            try
            {
                if (search != null)
                {
                    if (!search.IsDone) search.Advance(128);
                    Message = search.Message;
                    if (!search.IsDone) return;
                    if (search.Status != StartingBoardStatus.Success)
                    {
                        if (game.outcome == BotSessionStatus.Error && game.actions.Length == 0)
                        { Status = BotReplayStatus.Partial; Message = "시작 검색 실패 사례 · " + search.Message; search = null; return; }
                        throw new InvalidOperationException("기록의 시작 보드를 구성하지 못했습니다: " + search.Message);
                    }
                    executor = new BoardActionExecutor(search.State); search = null;
                    if (!executor.HasPendingCascade) ReachBoundary();
                    return;
                }
                if (executor.HasPendingCascade)
                {
                    CascadeStepResult step = executor.AdvanceCascade(); Message = step.Message;
                    if (!step.IsApplied || step.Reason == CascadeStepReason.Aborted) throw new InvalidOperationException("후속 처리 불일치: " + step.Message);
                    if (!executor.HasPendingCascade) ReachBoundary();
                    return;
                }
                if (!requested) { Status = BotReplayStatus.Ready; return; }
                if (ActionIndex >= game.actions.Length) { ReachBoundary(); return; }
                BotBatchAction action = game.actions[ActionIndex];
                if (State.MovesRemaining != action.movesBefore || State.Random.DrawCount != action.randomBefore)
                    throw new InvalidOperationException("입력 전 이동 수 또는 난수 소비가 기록과 다릅니다.");
                BotObservation observation = BotObservationBuilder.Capture(executor);
                if (!observation.Actions.Any(a => a.Kind == action.kind && a.First.Equals(action.first) &&
                    a.Second.HasValue == action.hasSecond && (!action.hasSecond || a.Second.Value.Equals(action.second))))
                    throw new InvalidOperationException("기록한 행동이 현재 보드의 유효 후보에 없습니다.");
                BoardActionResult applied = action.kind == BotActionKind.Activate ? executor.Activate(action.first) : executor.Swap(action.first, action.second);
                if (!applied.IsApplied) throw new InvalidOperationException("행동 실행 거절: " + applied.Message);
                pending = true;
                if (!executor.HasPendingCascade) ReachBoundary();
            }
            catch (Exception error)
            {
                Status = BotReplayStatus.Error; requested = false;
                Message = $"행동 {ActionIndex + 1}에서 재생 불일치 · {error.Message}";
            }
        }

        /// <summary>한 행동의 모든 후속 처리가 끝난 경계에서 기록을 대조하고 다음 입력 여부를 결정한다.</summary>
        private void ReachBoundary()
        {
            if (pending)
            {
                BotBatchAction action = game.actions[ActionIndex];
                if (executor.Turn != action.turn || State.MovesRemaining != action.movesAfter || State.Random.DrawCount != action.randomAfter)
                    throw new InvalidOperationException("입력 후 턴·이동 수·난수 소비가 기록과 다릅니다.");
                ActionIndex++; pending = false;
                if (!wholeRecord) requested = false;
            }
            if (ActionIndex == game.actions.Length)
            {
                requested = false;
                if (game.outcome == BotSessionStatus.Stopped || game.outcome == BotSessionStatus.Error)
                { Status = BotReplayStatus.Partial; Message = "기록된 완료 행동까지 재생했습니다. 중단/오류 당시의 미완료 처리는 복원하지 않습니다.\n" + game.message; return; }
                BoardOutcomeKind? expected = game.outcome == BotSessionStatus.Won ? BoardOutcomeKind.Won :
                    game.outcome == BotSessionStatus.MovesExhausted ? BoardOutcomeKind.MovesExhausted :
                    game.outcome == BotSessionStatus.Blocked ? BoardOutcomeKind.Blocked : (BoardOutcomeKind?)null;
                if (!expected.HasValue || executor.Outcome?.Kind != expected || game.remainingMoves < 0 || game.usedMoves < 0 ||
                    State.MovesRemaining != game.remainingMoves || State.InitialMoves - State.MovesRemaining != game.usedMoves ||
                    State.Missions.Count != game.missions.Length || State.Missions.Where((m, i) =>
                        m.Remaining != game.missions[i].remaining || m.Definition.Kind != game.missions[i].kind || m.Definition.Color != game.missions[i].color).Any())
                    throw new InvalidOperationException("최종 종료 종류·이동 수·미션이 다르거나 비교할 기록이 없습니다.");
                Status = BotReplayStatus.Completed; Message = "재생 완료 · 행동·이동 수·난수 소비·종료 결과·미션 일치"; return;
            }
            Status = requested ? BotReplayStatus.Playing : BotReplayStatus.Ready;
            Message = $"행동 {ActionIndex}/{game.actions.Length} 대조 완료 · 다음 행동 대기";
        }

        public void Dispose()
        {
            search = null; executor = null; requested = pending = false; Status = BotReplayStatus.Disposed;
        }
    }
}
