using System;
using System.Collections.Generic;
using System.Linq;

namespace Tutorial
{
    /// <summary>튜토리얼 한 실행의 진행 상태. 퍼즐 명령과 연출은 실행하지 않고 관련 완료 신호만 받는다.</summary>
    public sealed class TutorialProgress : IDisposable
    {
        private readonly Guid session = Guid.NewGuid();
        private readonly TutorialHandlerRegistry registry;
        private readonly List<TutorialStepDefinition> steps = new List<TutorialStepDefinition>();
        private ITutorialStepHandler handler;
        private ITutorialResultEvaluator[] evaluators = Array.Empty<ITutorialResultEvaluator>();
        private HashSet<string>[] occurrences = Array.Empty<HashSet<string>>();
        private int[] counts = Array.Empty<int>();
        private TutorialActionTicket pending;
        private TutorialInput approvedInput;
        private long attempt;
        private bool hasPending, success, completionReceived, presented, freeUsed;
        public TutorialProgressState State { get; private set; }
        public int StepIndex { get; private set; }
        public bool IsPaused { get; private set; }
        public string Message { get; private set; } = "";
        public TutorialProgressSnapshot Snapshot => new TutorialProgressSnapshot(State, StepIndex, IsPaused,
            handler?.UsesFreeItem == true && !freeUsed && State != TutorialProgressState.Completed && State != TutorialProgressState.Cancelled && State != TutorialProgressState.Error,
            Message, StepIndex < steps.Count ? steps[StepIndex] : null, counts);

        public TutorialProgress(LevelTutorialDefinition definition, TutorialHandlerRegistry registry = null)
        {
            this.registry = registry ?? TutorialHandlerRegistry.CreateDefault();
            this.registry.Freeze();
            if (definition == null) { State = TutorialProgressState.Completed; return; }
            if (definition.steps == null) { Fail("tutorial.steps: 단계 목록이 없습니다."); return; }
            // 작성 중인 데이터와 실행을 분리한다. 공급의 실행 적용은 게임 연결부가 소유한다.
            foreach (TutorialStepDefinition source in definition.steps)
            {
                if (source == null) { Fail("tutorial.steps: 단계가 null입니다."); return; }
                steps.Add(new TutorialStepDefinition
                {
                    kind = source.kind, instructions = source.instructions, hasFirst = source.hasFirst, first = source.first,
                    hasSecond = source.hasSecond, second = source.second, item = source.item, actionDefinitionId = source.actionDefinitionId,
                    highlights = source.highlights == null ? null : new List<Board.BoardCoordinate>(source.highlights),
                    results = source.results?.Select(result => result == null ? null : new TutorialResultDefinition
                    { kind = result.kind, definitionId = result.definitionId, hasCoordinate = result.hasCoordinate, coordinate = result.coordinate, count = result.count }).ToList()
                });
            }
            for (int i = 0; i < steps.Count; i++)
            {
                TutorialValidationContext context = new TutorialValidationContext(null, null, $"tutorial.steps.Array.data[{i}]",
                    (path, message, coordinate) => Fail(path + ": " + message));
                LevelTutorialValidator.ValidateStep(steps[i], this.registry, context);
                if (State == TutorialProgressState.Error) return;
            }
            EnterStep();
        }

        /// <summary>허용된 입력만 예약한다. 설명 다음은 즉시 진행하고 조작은 실행 완료를 기다린다.</summary>
        /// <param name="input">실행 전 입력 의도.</param><param name="ticket">관련 결과에 부여할 식별값.</param><returns>입력 허용 여부.</returns>
        public bool TryApprove(TutorialInput input, out TutorialActionTicket ticket)
        {
            ticket = default;
            if (IsPaused || hasPending || handler == null || State != TutorialProgressState.AwaitAction && State != TutorialProgressState.AwaitDescription) return false;
            TutorialStepDefinition step = steps[StepIndex];
            if (freeUsed || !handler.Allows(step, input)) return false;
            ticket = new TutorialActionTicket(session, StepIndex, ++attempt);
            if (handler.IsDescription) { StepIndex++; EnterStep(); return true; }
            pending = ticket; approvedInput = input; hasPending = true;
            State = TutorialProgressState.AwaitPresentation; Message = "행동 결과 대기";
            return true;
        }

        /// <summary>실제 행동의 성공을 보고한다. 실패는 권한을 소진하지 않고 같은 단계에서 재시도한다.</summary>
        /// <param name="ticket">승인받은 시도.</param><param name="succeeded">퍼즐 실행 성공.</param><param name="definitionId">실제로 교환 발동한 파워 정의 ID.</param>
        public void ReportCompletion(TutorialActionTicket ticket, bool succeeded, string definitionId = null)
        {
            if (!Accepts(ticket) || completionReceived) return;
            if (!succeeded)
            {
                hasPending = false; presented = false; success = false;
                foreach (HashSet<string> set in occurrences) set.Clear();
                Array.Clear(counts, 0, counts.Length);
                State = TutorialProgressState.AwaitAction; Message = "행동 실패: 같은 단계에서 재시도"; return;
            }
            if (!handler.IsSuccessful(steps[StepIndex], approvedInput, succeeded, definitionId))
            { Fail("지정 행동과 실제 성공 결과가 일치하지 않습니다."); return; }
            completionReceived = true; success = true; freeUsed = handler.UsesFreeItem;
            TryAdvance();
        }

        /// <summary>행동 및 관련 연쇄의 기록을 누적한다. 보드의 현재 빈칸 여부는 사용하지 않는다.</summary>
        /// <param name="ticket">관련 시도.</param><param name="records">실제 기록에서 변환한 결과.</param>
        public void ReportResults(TutorialActionTicket ticket, IEnumerable<TutorialResultRecord> records)
        {
            if (!Accepts(ticket) || records == null) return;
            TutorialStepDefinition step = steps[StepIndex];
            foreach (TutorialResultRecord record in records)
                if (record != null)
                    for (int i = 0; i < evaluators.Length; i++)
                        if (evaluators[i].Matches(step.results[i], record) && occurrences[i].Add(record.OccurrenceId)) counts[i]++;
            TryAdvance();
        }
        public void ReportPresentationComplete(TutorialActionTicket ticket)
        { if (!Accepts(ticket)) return; presented = true; TryAdvance(); }
        public void SetPaused(bool paused)
        { if (State == TutorialProgressState.Cancelled || State == TutorialProgressState.Error || State == TutorialProgressState.Completed) return; IsPaused = paused; if (!paused) TryAdvance(); }
        public void Fail(string reason)
        {
            if (State == TutorialProgressState.Cancelled || State == TutorialProgressState.Completed) return;
            State = TutorialProgressState.Error; Message = reason; hasPending = false;
        }
        public void Cancel()
        {
            if (State == TutorialProgressState.Completed || State == TutorialProgressState.Cancelled) return;
            State = TutorialProgressState.Cancelled; Message = "튜토리얼 취소"; hasPending = false; handler = null;
            evaluators = Array.Empty<ITutorialResultEvaluator>(); occurrences = Array.Empty<HashSet<string>>(); counts = Array.Empty<int>();
        }
        public void Dispose() => Cancel();

        private bool Accepts(TutorialActionTicket ticket) => hasPending && pending.Equals(ticket) && State == TutorialProgressState.AwaitPresentation;
        private void TryAdvance()
        {
            if (!hasPending) return;
            if (!success) { Message = "행동 결과 대기"; return; }
            for (int i = 0; i < counts.Length; i++)
                if (counts[i] < steps[StepIndex].results[i].count) { Message = $"결과 조건 {i + 1} 대기"; return; }
            if (!presented) { Message = "관련 보드 연출 대기"; return; }
            if (IsPaused) { Message = "일시정지: 진행 보류"; return; }
            StepIndex++; EnterStep();
        }
        private void EnterStep()
        {
            hasPending = false; success = false; completionReceived = false; presented = false; freeUsed = false;
            if (StepIndex >= steps.Count)
            {
                handler = null; evaluators = Array.Empty<ITutorialResultEvaluator>(); occurrences = Array.Empty<HashSet<string>>(); counts = Array.Empty<int>();
                State = TutorialProgressState.Completed; Message = "튜토리얼 완료"; return;
            }
            TutorialStepDefinition step = steps[StepIndex];
            registry.TryGetStep(step, out handler);
            evaluators = new ITutorialResultEvaluator[step.results.Count];
            occurrences = new HashSet<string>[step.results.Count]; counts = new int[step.results.Count];
            for (int i = 0; i < step.results.Count; i++)
            { registry.TryGetResult(step.results[i].kind, out evaluators[i]); occurrences[i] = new HashSet<string>(StringComparer.Ordinal); }
            State = handler.IsDescription ? TutorialProgressState.AwaitDescription : TutorialProgressState.AwaitAction;
            Message = handler.IsDescription ? "설명 확인 대기" : "지정 행동 대기";
        }
    }
}
