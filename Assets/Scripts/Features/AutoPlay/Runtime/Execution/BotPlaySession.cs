using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Levels;
using Simulation;

namespace AutoPlay
{
    public enum BotSessionStatus { Preparing, Ready, Running, Stopping, Stopped, Won, MovesExhausted, Blocked, Error, Disposed }
    public enum BotStrategyKind { Basic, Planning }

    /// <summary>실행 경계 전용 진단 기록. 전략 입력에는 기록이나 실제 난수 정보를 전달하지 않는다.</summary>
    public sealed class BotTurnRecord
    {
        public int Turn { get; }
        public BotChoice Choice { get; }
        public int MovesBefore { get; }
        public int MovesAfter { get; }
        public int RandomBefore { get; }
        public int RandomAfter { get; }
        /// <param name="turn">실행한 턴.</param><param name="choice">공개 정보로 선택한 조작과 이유.</param>
        /// <param name="movesBefore">입력 직전 이동 수.</param><param name="movesAfter">후속 처리 후 이동 수.</param>
        /// <param name="randomBefore">입력 직전 난수 추출 수.</param><param name="randomAfter">후속 처리 후 난수 추출 수.</param>
        internal BotTurnRecord(int turn, BotChoice choice, int movesBefore, int movesAfter, int randomBefore, int randomAfter)
        { Turn = turn; Choice = choice; MovesBefore = movesBefore; MovesAfter = movesAfter; RandomBefore = randomBefore; RandomAfter = randomAfter; }
    }

    /// <summary>
    /// 한 판의 실행기를 독점 소유한다. Editor는 Advance를 갱신마다 한 번 호출하며,
    /// 전략에는 Observe로 만든 공개 값만 전달한다. 아이템·부스터 API는 노출하지 않는다.
    /// </summary>
    public sealed class BotPlaySession : IDisposable
    {
        public const string Version = "bot-session-v3";
        private PlanningSearch planning;
        private StartingBoardSearch search;
        private BoardActionExecutor executor;
        private BotObservation observation;
        private readonly List<BotTurnRecord> records = new List<BotTurnRecord>();
        private bool runRequested;
        private bool wholeGame;
        private bool stopRequested;
        private BotChoice pendingChoice;
        private int movesBefore;
        private int randomBefore;

        public BotSessionStatus Status { get; private set; } = BotSessionStatus.Preparing;
        public string Message { get; private set; } = "시작 보드 준비 중";
        public string LastRejection { get; private set; }
        public int Seed { get; }
        public string DefinitionFingerprint { get; }
        public ReadOnlyCollection<BotTurnRecord> Records { get; }
        public BotChoice LastChoice { get; private set; }
        public BotStrategyKind Strategy { get; }
        public string StrategyVersion => Strategy == BotStrategyKind.Planning ? PlanningSearch.Version : BasicBotStrategy.Version;
        public bool IsPlanning => planning != null;
        // 화면과 검증용 상태다. 이 속성을 포함한 세션 자체를 전략 인자로 넘기지 않는다.
        // 실행기를 공개하지 않으므로 수동 화면이 같은 판에 Swap/아이템을 호출할 수 없다.
        public LevelRuntimeState State => executor?.State;
        public BoardOutcome Outcome => executor?.Outcome;
        public bool NeedsAdvance => Status == BotSessionStatus.Preparing || Status == BotSessionStatus.Running || Status == BotSessionStatus.Stopping;

        /// <summary>원본의 값 복사본으로 시작 조건 검색을 구성한다. 긴 검색은 Advance에서 나누어 실행한다.</summary>
        /// <param name="definition">저장 여부와 무관한 현재 레벨.</param><param name="seed">실행 전용 시드.</param>
        public BotPlaySession(LevelDefinition definition, int seed) : this(definition, seed, BotStrategyKind.Basic) { }

        /// <param name="definition">현재 레벨.</param><param name="seed">실행 전용 시드.</param><param name="strategy">이 판에서 유지할 전략.</param>
        public BotPlaySession(LevelDefinition definition, int seed, BotStrategyKind strategy)
        {
            if (strategy != BotStrategyKind.Basic && strategy != BotStrategyKind.Planning) throw new ArgumentOutOfRangeException(nameof(strategy));
            Strategy = strategy;
            Seed = seed; Records = records.AsReadOnly();
            // 기존 StartingBoardSearch/LevelStateBuilder가 정의 구조체·목록을 복사한다.
            // Unity Object를 새로 만들거나 원본에 값을 쓰지 않는다.
            search = new StartingBoardSearch(definition, seed);
            DefinitionFingerprint = search.DefinitionFingerprint;
        }

        /// <summary>한 수 또는 한 판 실행을 예약한다. 중지된 판은 명시적 요청으로만 다시 진행한다.</summary>
        /// <param name="allMoves">true면 판 종료까지, false면 한 행동의 후속 처리까지.</param><returns>예약 수락 여부.</returns>
        public bool Begin(bool allMoves)
        {
            if (Status != BotSessionStatus.Preparing && Status != BotSessionStatus.Ready && Status != BotSessionStatus.Stopped) return false;
            if (Status == BotSessionStatus.Stopped && executor == null) return false;
            runRequested = true; wholeGame = allMoves; stopRequested = false;
            if (Status != BotSessionStatus.Preparing) Status = BotSessionStatus.Running;
            Message = allMoves ? "한 판 실행 예약" : "한 수 진행 예약";
            return true;
        }

        /// <summary>안정된 판의 공개 관찰을 반환한다. 같은 입력 시점에는 같은 토큰 객체를 유지한다.</summary>
        /// <returns>현재 관찰. 처리 중·종료된 판은 null.</returns>
        public BotObservation Observe()
        {
            if (executor == null || executor.Phase != BoardActionPhase.Ready || executor.Outcome != null ||
                (Status != BotSessionStatus.Ready && Status != BotSessionStatus.Running)) return null;
            return observation ??= BotObservationBuilder.Capture(executor);
        }

        /// <summary>관찰 시점과 후보 소속을 확인한 뒤 공통 실행기로 조작을 재검증한다.</summary>
        /// <param name="expected">전략이 읽은 관찰 객체.</param><param name="action">그 관찰의 후보.</param>
        /// <returns>실제 행동 수락 여부. 거절은 이동 수·난수를 변경하지 않는다.</returns>
        public bool TrySubmit(BotObservation expected, BotAction action)
            => Submit(expected, action, null);

        /// <summary>실제 제출 경계는 두 전략과 외부 요청이 공유한다. 탐색 중 외부 제출도 차단한다.</summary>
        /// <param name="expected">관찰 토큰.</param><param name="action">공개 후보.</param><param name="chosen">완료한 전략 선택. 외부 요청은 기본 평가.</param>
        /// <returns>공통 실행기의 수락 여부.</returns>
        private bool Submit(BotObservation expected, BotAction action, BotChoice chosen)
        {
            if (planning != null || executor == null || executor.Phase != BoardActionPhase.Ready || executor.Outcome != null ||
                (Status != BotSessionStatus.Ready && Status != BotSessionStatus.Running) ||
                expected == null || !ReferenceEquals(expected, observation) || action == null || !expected.Actions.Contains(action))
            { LastRejection = "현재 관찰의 유효 후보가 아니거나 입력할 수 없는 시점입니다."; return false; }

            BotChoice choice = chosen ?? BasicBotStrategy.Evaluate(expected, action);
            int beforeMoves = State.MovesRemaining, beforeRandom = State.Random.DrawCount;
            BoardActionResult result = action.Kind == BotActionKind.Activate ? executor.Activate(action.First) :
                executor.Swap(action.First, action.Second.Value);
            if (!result.IsApplied)
            {
                // 허용 후보를 같은 안정 상태에 제출했는데 실행기가 거절하면 규칙 경계의
                // 불일치다. 이를 일반 패배 또는 다른 후보 재시도로 숨기지 않는다.
                LastRejection = result.Message;
                Fail("공개 후보 실행 불일치: " + result.Message);
                return false;
            }
            movesBefore = beforeMoves; randomBefore = beforeRandom;
            LastChoice = pendingChoice = choice; observation = null; LastRejection = null;
            Status = BotSessionStatus.Running; Message = "행동 적용 · 낙하와 연쇄 처리 중";
            return true;
        }

        /// <summary>검색 128개 배정, 후속 처리 한 단계 또는 행동 한 개만 진행한다.</summary>
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
                    if (search.Status != StartingBoardStatus.Success) { Fail(search.Message); search = null; return; }
                    executor = new BoardActionExecutor(search.State); search = null;
                    if (!executor.HasPendingCascade) ReachBoundary();
                    return;
                }
                if (executor.HasPendingCascade)
                {
                    CascadeStepResult step = executor.AdvanceCascade(); Message = step.Message;
                    if (!executor.HasPendingCascade) ReachBoundary();
                    // 공통 실행기는 라스트팡 실패에도 이미 확정한 성공을 보존한다.
                    // 판 결과만 읽으면 이 후속 오류가 가려지므로 실제 단계의 실패도 검사한다.
                    // Outcome은 원래 성공을 유지하고, 시험 세션의 상태·이유만 오류로 분리한다.
                    if (!step.IsApplied || step.Reason == CascadeStepReason.Aborted)
                        Fail((executor.Outcome?.Kind == BoardOutcomeKind.Won ? "성공 확정 후 라스트팡 처리 오류: " : "") + step.Message);
                    return;
                }
                if (executor.Outcome != null) { ReachBoundary(); return; }
                if (!runRequested) { ReachBoundary(); return; }
                BotObservation current = Observe();
                if (current == null) { Fail("공통 실행기가 입력 가능 상태로 돌아오지 않았습니다."); return; }
                BotChoice choice;
                if (Strategy == BotStrategyKind.Planning)
                {
                    planning ??= new PlanningSearch(current);
                    planning.Advance(); Message = planning.Message;
                    if (!planning.IsDone) return;
                    choice = planning.Result;
                    planning.Dispose(); planning = null;
                }
                else choice = BasicBotStrategy.Choose(current);
                if (choice == null)
                {
                    // 재배치·막힘 판정은 실행기의 안정 경계 책임이다. 봇은 여기서
                    // 임의로 섞거나 패배를 선언하지 않고 불일치를 기록한다.
                    Fail("입력 가능한 판에 공개 행동 후보가 없습니다."); return;
                }
                Submit(current, choice.Action, choice);
            }
            catch (Exception error) { Fail(error.GetType().Name + ": " + error.Message); }
        }

        /// <summary>다음 행동을 예약하지 않고 현재 행동의 후속 처리까지 끝낸 뒤 멈춘다.</summary>
        public void RequestStop()
        {
            if (Status >= BotSessionStatus.Won) return;
            planning?.Dispose(); planning = null;
            runRequested = false; stopRequested = true; observation = null;
            if (executor?.HasPendingCascade == true)
            { Status = BotSessionStatus.Stopping; Message = "현재 행동의 낙하·연쇄 완료 후 중지"; }
            else
            { search = null; Status = BotSessionStatus.Stopped; Message = "사용자 중지"; }
        }

        /// <summary>완료된 행동만 기록하고 종료 결과·중지·한 수 경계를 순서대로 판정한다.</summary>
        private void ReachBoundary()
        {
            if (pendingChoice != null)
            {
                records.Add(new BotTurnRecord(executor.Turn, pendingChoice, movesBefore, State.MovesRemaining, randomBefore, State.Random.DrawCount));
                pendingChoice = null;
                if (!wholeGame) runRequested = false;
            }
            observation = null;
            if (executor.Outcome != null)
            {
                Status = executor.Outcome.Kind switch {
                    BoardOutcomeKind.Won => BotSessionStatus.Won, BoardOutcomeKind.MovesExhausted => BotSessionStatus.MovesExhausted,
                    BoardOutcomeKind.Blocked => BotSessionStatus.Blocked, _ => BotSessionStatus.Error };
                Message = executor.Outcome.Message;
                runRequested = false;
            }
            else if (stopRequested) { Status = BotSessionStatus.Stopped; Message = "사용자 중지 · 현재 행동 처리 완료"; }
            else { Status = runRequested ? BotSessionStatus.Running : BotSessionStatus.Ready; Message = "안정 상태 · 다음 행동 대기"; }
        }

        /// <param name="message">실행 오류 원인. 정상 패배와 구분해 표시한다.</param>
        private void Fail(string message)
        { planning?.Dispose(); planning = null; Status = BotSessionStatus.Error; Message = message; observation = null; runRequested = false; }

        /// <summary>원본 변경·창 종료·재로드 시 실행기와 과거 관찰을 폐기한다. 원본은 건드리지 않는다.</summary>
        public void Dispose()
        {
            planning?.Dispose(); planning = null;
            search = null; executor = null; observation = null; pendingChoice = null;
            runRequested = false; Status = BotSessionStatus.Disposed; Message = "시험 폐기";
        }
    }
}
