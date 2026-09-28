using System;
using System.Linq;

namespace AutoPlay
{
    /// <summary>
    /// 공개 관찰에서 시작하는 두 수 탐색 작업. 공통 규칙 접근은 가정 어댑터에 한정한다.
    /// 실행 세션/게임 난수/원본 에셋을 받을 경로가 없다. 갱신당 작업량을 바꾸어도 방문 순서는 같다.
    /// </summary>
    public sealed class PlanningSearch : IDisposable
    {
        public const string Version = "planning-bot-v2";
        public const int CandidateLimit = 6;
        public const int SampleLimit = 3;
        public const int WorkLimit = 4096;
        public const int BranchStepLimit = 128;
        private readonly BotObservation initial;
        private readonly BotChoice[] roots;
        private readonly long[] totals, round;
        private readonly int initialRemaining;
        private int sample, root, child, branchSteps;
        private bool validRound = true;
        private BotChoice[] children;
        private PlanningBranch first, second;
        private long bestChild;
        private string limitation;
        public int WorkDone { get; private set; }
        public int CompletedSamples { get; private set; }
        public int RejectedSamples { get; private set; }
        public int SecondActions { get; private set; }
        public bool IsDone { get; private set; }
        public bool IsCancelled { get; private set; }
        public bool UsedFallback { get; private set; }
        public BotChoice Result { get; private set; }
        public string Message { get; private set; } = "계획 계산 준비";
        public long SelectedValue { get; private set; }

        /// <param name="observation">실제 안정 경계에서 복사한 공개 값.</param>
        public PlanningSearch(BotObservation observation)
        {
            initial = observation ?? throw new ArgumentNullException(nameof(observation));
            roots = Rank(observation); totals = new long[roots.Length]; round = new long[roots.Length];
            initialRemaining = initial.Missions.Sum(m => m.Remaining);
            if (roots.Length == 0) { IsDone = true; Message = "공개 후보 없음"; }
        }

        /// <summary>한 단위는 가정 구성·한 명령·한 연쇄·한 결과 집계 중 하나다. 긴 한 판 루프를 만들지 않는다.</summary>
        /// <param name="units">이번 갱신에 허용한 작업 수. 결과 순서에는 영향을 주지 않는다.</param>
        public void Advance(int units = 1)
        {
            if (units < 1) throw new ArgumentOutOfRangeException(nameof(units));
            for (int i = 0; i < units && !IsDone; i++)
            {
                if (WorkDone >= WorkLimit) { limitation = "탐색 작업량 한도"; Finish(); break; }
                WorkDone++;
                if (first == null)
                {
                    first = PlanningBranch.Create(initial, sample); first.Apply(roots[root].Action);
                    branchSteps = 0;
                }
                else if (first.Pending && !first.Failed)
                {
                    if (++branchSteps > BranchStepLimit) RejectRoot("첫 수 가정 연쇄 한도");
                    else first.Advance();
                }
                else if (first.Failed) RejectRoot(first.Failure);
                else if (children == null)
                {
                    if (first.Terminal) CompleteRoot(first.Value(initialRemaining, initial.MovesRemaining) * 1000L);
                    else
                    {
                        children = Rank(first.Observe()); child = 0; bestChild = long.MinValue;
                        if (children.Length == 0) throw new InvalidOperationException("안정된 가정 판의 행동 후보 누락");
                    }
                }
                else if (second == null)
                {
                    second = first.Fork(); second.Apply(children[child].Action); branchSteps = 0; SecondActions++;
                }
                else if (second.Pending && !second.Failed)
                {
                    if (++branchSteps > BranchStepLimit) RejectRoot("둘째 수 가정 연쇄 한도");
                    else second.Advance();
                }
                else if (second.Failed) RejectRoot(second.Failure);
                else
                {
                    // 목표 진행과 이동 비용이 같을 때 다음 행동의 파워·직접 범위를 보조로 본다.
                    // 현재 기본 평가의 파워는 0~5, 범위는 최대 100칸이므로 보조값은 605 이하다.
                    // 주 가치를 1,000배 해 이동 비용 1회의 차이조차 이 보조값이 뒤집지 못하게 한다.
                    long value = second.Value(initialRemaining, initial.MovesRemaining) * 1000L +
                        children[child].PowerValue * 101L + children[child].ClearValue;
                    bestChild = Math.Max(bestChild, value);
                    second = null; child++;
                    if (child == children.Length) CompleteRoot(bestChild);
                }
                if (!IsDone) Message = $"계획 계산 중 · 작업 {WorkDone}/{WorkLimit} · 공통 표본 {CompletedSamples}/{SampleLimit}";
            }
        }

        /// <param name="reason">모델 처리 한계. 프로그램 예외는 이 경로에서 숨기지 않는다.</param>
        private void RejectRoot(string reason)
        { validRound = false; limitation = reason; CompleteRoot(0); }

        /// <summary>후보별 가정 결과를 모으되 모든 후보가 성공한 표본 묶음만 동시에 합산한다.</summary>
        /// <param name="value">두 수 후 누적 가치 또는 첫 수 종료 가치.</param>
        private void CompleteRoot(long value)
        {
            round[root] = value; first = second = null; children = null; root++;
            if (root < roots.Length) return;
            if (validRound)
            {
                for (int i = 0; i < totals.Length; i++) totals[i] += round[i];
                CompletedSamples++;
            }
            else RejectedSamples++;
            sample++; root = 0; validRound = true;
            if (sample == SampleLimit) Finish();
        }

        /// <summary>부분 후보 평가를 버리고 공통 표본만 비교한다. 한 번의 우연한 성공은 실제 성공이 아니다.</summary>
        private void Finish()
        {
            int best = 0;
            // roots는 기본 평가와 좌표 순으로 정렬되어 있다. 동점은 먼저 나온 후보를 유지한다.
            if (CompletedSamples > 0)
                for (int i = 1; i < totals.Length; i++) if (totals[i] > totals[best]) best = i;
            UsedFallback = CompletedSamples == 0;
            SelectedValue = totals[best];
            string reason = UsedFallback ? "기본 전략으로 전환 · 공통 가정 비교 미완료" :
                $"두 수 계획 · 공통 표본 {CompletedSamples}개 · 누적 가정 가치 {SelectedValue} · 둘째 행동 실행 {SecondActions}회";
            if (!string.IsNullOrEmpty(limitation)) reason += " · " + limitation;
            Result = new BotChoice(roots[best], reason + " / " + PlanningBranch.Assumptions);
            Message = reason; IsDone = true; first = second = null; children = null;
        }

        /// <param name="board">공개 후보와 평가.</param><returns>기본 최선 후보를 포함한 결정적 상위 목록.</returns>
        private static BotChoice[] Rank(BotObservation board) => board.Actions.Select(a => BasicBotStrategy.Evaluate(board, a))
            .OrderByDescending(c => c.MissionValue).ThenByDescending(c => c.PowerValue).ThenByDescending(c => c.ClearValue)
            .ThenBy(c => c.Action.Kind).ThenBy(c => c.Action.First.Row).ThenBy(c => c.Action.First.Column)
            .ThenBy(c => c.Action.Second?.Row ?? -1).ThenBy(c => c.Action.Second?.Column ?? -1).Take(CandidateLimit).ToArray();

        /// <summary>결과와 가정 작업을 폐기한다. 실제 행동은 이 작업 안에서 한 번도 제출하지 않는다.</summary>
        public void Dispose()
        { first = second = null; children = null; Result = null; IsDone = true; IsCancelled = true; Message = "계획 계산 취소"; }
    }
}
