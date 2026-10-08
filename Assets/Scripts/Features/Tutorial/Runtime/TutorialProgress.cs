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
        private int legacyConditionCount;
        private HashSet<string>[] occurrences = Array.Empty<HashSet<string>>();
        private int[] counts = Array.Empty<int>();
        private ITutorialConditionState[] conditionStates = Array.Empty<ITutorialConditionState>();
        private TutorialTargetSelection[] highlightTargets = Array.Empty<TutorialTargetSelection>();
        private Board.BoardCoordinate[] targetHighlights = Array.Empty<Board.BoardCoordinate>();
        private readonly Func<IReadOnlyList<TutorialTargetEntity>> readTargets;
        private readonly Func<IReadOnlyList<int>> readMissionRemaining;
        private readonly Dictionary<string, long> bindings = new Dictionary<string, long>(StringComparer.Ordinal);
        private TutorialActionTicket pending;
        private TutorialInput approvedInput;
        private long attempt;
        private bool hasPending, success, completionReceived, presented, resultsComplete;
        private int freeUses;
        private TutorialInput? guidance;
        internal int GuidanceRevision { get; private set; }
        public TutorialProgressState State { get; private set; }
        public int StepIndex { get; private set; }
        public bool IsPaused { get; private set; }
        public string Message { get; private set; } = "";
        public TutorialProgressSnapshot Snapshot => new TutorialProgressSnapshot(State, StepIndex, IsPaused,
            handler?.UsesFreeItem == true && StepIndex < steps.Count && freeUses < steps[StepIndex].freeItemCount && State != TutorialProgressState.Completed && State != TutorialProgressState.Cancelled && State != TutorialProgressState.Error,
            Message, StepIndex < steps.Count ? steps[StepIndex] : null, counts, guidance, targetHighlights);

        internal void SetGuidance(TutorialInput input)
        { guidance = input; GuidanceRevision++; }

        public TutorialProgress(LevelTutorialDefinition definition, TutorialHandlerRegistry registry = null,
            Func<IReadOnlyList<TutorialTargetEntity>> readTargets = null, Func<IReadOnlyList<int>> readMissionRemaining = null)
        {
            this.readTargets = readTargets;
            this.readMissionRemaining = readMissionRemaining;
            this.registry = registry ?? TutorialHandlerRegistry.CreateDefault();
            this.registry.Freeze();
            if (definition == null) { State = TutorialProgressState.Completed; return; }
            if (definition.steps == null) { Fail("tutorial.steps: 단계 목록이 없습니다."); return; }
            // 작성 중인 데이터와 실행을 분리한다. 공급의 실행 적용은 게임 연결부가 소유한다.
            foreach (TutorialStepDefinition source in definition.steps)
            {
                if (source == null) { Fail("tutorial.steps: 단계가 null입니다."); return; }
                steps.Add(source.Copy());
            }
            LevelTutorialValidator.ValidateBindings(steps, (path, message) => Fail(path + ": " + message));
            if (State == TutorialProgressState.Error) return;
            for (int i = 0; i < steps.Count; i++)
            {
                TutorialValidationContext context = new TutorialValidationContext(null, null, $"tutorial.steps.Array.data[{i}]",
                    (path, message, coordinate) => Fail(path + ": " + message));
                LevelTutorialValidator.ValidateStep(steps[i], this.registry, context);
                if (State == TutorialProgressState.Error) return;
            }
            EnterStep();
        }

        /// <summary>선택 미리보기에서 승인 권한을 소비하지 않고 실제 승인과 같은 규칙을 조회한다.</summary>
        /// <param name="input">조회할 입력 의도.</param><returns>현재 입력 허용 여부.</returns>
        public bool CanApprove(TutorialInput input)
        {
            if (IsPaused || hasPending || handler == null || State != TutorialProgressState.AwaitAction && State != TutorialProgressState.AwaitDescription) return false;
            return (!handler.UsesFreeItem || freeUses < steps[StepIndex].freeItemCount) && handler.Allows(steps[StepIndex], input);
        }

        /// <summary>허용된 입력만 예약한다. 설명 다음은 즉시 진행하고 조작은 실행 완료를 기다린다.</summary>
        /// <param name="input">실행 전 입력 의도.</param><param name="ticket">관련 결과에 부여할 식별값.</param><returns>입력 허용 여부.</returns>
        public bool TryApprove(TutorialInput input, out TutorialActionTicket ticket)
        {
            ticket = default;
            if (!CanApprove(input)) return false;
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
                hasPending = false; presented = false; success = false; resultsComplete = false;
                if (steps[StepIndex].conditions.Count == 0)
                {
                    foreach (HashSet<string> set in occurrences) set.Clear();
                    Array.Clear(counts, 0, counts.Length);
                }
                State = TutorialProgressState.AwaitAction; Message = "행동 실패: 같은 단계에서 재시도"; return;
            }
            if (!handler.IsSuccessful(steps[StepIndex], approvedInput, succeeded, definitionId))
            { Fail("지정 행동과 실제 성공 결과가 일치하지 않습니다."); return; }
            completionReceived = true; success = true;
            if (handler.UsesFreeItem) freeUses++;
            if (approvedInput.Kind == TutorialInputKind.Swap)
                ReportConditionEvents(ticket, new[] { new TutorialConditionEvent(TutorialConditionKind.SuccessfulSwap, "swap:" + ticket.Attempt) });
            if (approvedInput.Kind == TutorialInputKind.Item)
                ReportConditionEvents(ticket, new[] { new TutorialConditionEvent("item:" + ticket.Attempt, approvedInput.Item.Value) });
            TryAdvance();
        }

        /// <summary>행동 및 관련 연쇄의 기록을 누적한다. 보드의 현재 빈칸 여부는 사용하지 않는다.</summary>
        /// <param name="ticket">관련 시도.</param><param name="records">실제 기록에서 변환한 결과.</param>
        public void ReportResults(TutorialActionTicket ticket, IEnumerable<TutorialResultRecord> records)
        {
            if (!Accepts(ticket) || resultsComplete || records == null) return;
            ReportConditionEvents(ticket, records.Where(record => record != null).Select(record => new TutorialConditionEvent(record)));
            TryAdvance();
        }
        /// <summary>실제 논리 연쇄가 끝났음을 보고한다. 결과가 부족하면 무기한 기다리지 않고 진단한다.</summary>
        /// <param name="ticket">논리 처리가 끝난 시도.</param>
        public void ReportResultsComplete(TutorialActionTicket ticket)
        { if (Accepts(ticket)) { RefreshConditions(); resultsComplete = true; TryAdvance(); } }

        public void ReportConditionEvents(TutorialActionTicket ticket, IEnumerable<TutorialConditionEvent> records)
        {
            if (!Accepts(ticket) || resultsComplete || records == null) return;
            foreach (TutorialConditionEvent record in records)
                for (int i = 0; i < conditionStates.Length; i++)
                {
                    int index = i;
                    string key = record.LegacyResult == null ? ticket.Attempt + ":" + record.Id : record.Id;
                    if (!string.IsNullOrWhiteSpace(record.Id) && !occurrences[index].Contains(key) && conditionStates[i].Accept(record))
                    { occurrences[index].Add(key); counts[index] = conditionStates[i].Count; }
                }
        }

        internal bool Contributes(IReadOnlyList<TutorialConditionEvent> records, IReadOnlyList<TutorialTargetEntity> current)
        {
            bool improves = false;
            foreach (ITutorialConditionState original in conditionStates.Skip(legacyConditionCount))
            {
                ITutorialConditionState copy = original.Fork();
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (TutorialConditionEvent record in records)
                    if (!seen.Contains(record.Id) && copy.Accept(record)) seen.Add(record.Id);
                try { copy.Refresh(current); }
                catch (InvalidOperationException) { return false; }
                if (steps[StepIndex].combination == TutorialConditionCombination.All && original.IsSatisfied && !copy.IsSatisfied) return false;
                if (!original.IsSatisfied && copy.ProgressValue > original.ProgressValue) improves = true;
            }
            return improves;
        }

        private void RefreshConditions()
        {
            IReadOnlyList<TutorialTargetEntity> current = readTargets?.Invoke() ?? Array.Empty<TutorialTargetEntity>();
            for (int i = 0; i < conditionStates.Length; i++)
            {
                try { conditionStates[i].Refresh(current); counts[i] = conditionStates[i].Count; }
                catch (InvalidOperationException error) { Fail($"tutorial.steps.Array.data[{StepIndex}].conditions.Array.data[{i}]: {error.Message}"); return; }
            }
        }

        private void RefreshHighlights(IReadOnlyList<TutorialTargetEntity> current)
        {
            TutorialStepDefinition step = steps[StepIndex];
            targetHighlights = highlightTargets.SelectMany((target, index) => target == null ? Array.Empty<Board.BoardCoordinate>() :
                target.HighlightCells(current, step.conditions[index].kind == TutorialConditionKind.RemainingDurability ||
                    step.conditions[index].kind == TutorialConditionKind.DurabilityDecrease && step.conditions[index].aggregation == TutorialDamageAggregation.Each))
                .Distinct().ToArray();
        }

        private bool ConditionsSatisfied => steps[StepIndex].combination == TutorialConditionCombination.All
            ? conditionStates.All(condition => condition.IsSatisfied) : conditionStates.Skip(legacyConditionCount).Any(condition => condition.IsSatisfied);

        public void ReportPresentationComplete(TutorialActionTicket ticket)
        { if (!Accepts(ticket)) return; presented = true; TryAdvance(); }
        public void SetPaused(bool paused)
        { if (State == TutorialProgressState.Cancelled || State == TutorialProgressState.Error || State == TutorialProgressState.Completed) return; IsPaused = paused; if (!paused) TryAdvance(); }
        public void Fail(string reason)
        {
            if (State == TutorialProgressState.Cancelled || State == TutorialProgressState.Completed) return;
            State = TutorialProgressState.Error; Message = reason; hasPending = false;
        }
        public void Cancel(string reason = "튜토리얼 취소")
        {
            if (State == TutorialProgressState.Completed || State == TutorialProgressState.Cancelled) return;
            State = TutorialProgressState.Cancelled; Message = reason; hasPending = false; handler = null;
            legacyConditionCount = 0; conditionStates = Array.Empty<ITutorialConditionState>(); occurrences = Array.Empty<HashSet<string>>(); counts = Array.Empty<int>();
            highlightTargets = Array.Empty<TutorialTargetSelection>(); targetHighlights = Array.Empty<Board.BoardCoordinate>();
        }
        public void Dispose() => Cancel();

        private bool Accepts(TutorialActionTicket ticket) => hasPending && pending.Equals(ticket) && State == TutorialProgressState.AwaitPresentation;
        private void TryAdvance()
        {
            if (!hasPending) return;
            if (!success) { Message = "행동 결과 대기"; return; }
            bool composed = steps[StepIndex].conditions.Count > 0;
            bool legacySatisfied = true;
            for (int i = 0; i < legacyConditionCount; i++)
                if (!conditionStates[i].IsSatisfied)
                {
                    if (composed) { legacySatisfied = false; continue; }
                    if (resultsComplete) Fail($"tutorial.steps.Array.data[{StepIndex}].results.Array.data[{i}]: 실제 결과 부족 {counts[i]}/{steps[StepIndex].results[i].count}");
                    else Message = $"결과 조건 {i + 1} 대기";
                    return;
                }
            if (!presented) { Message = "관련 보드 연출 대기"; return; }
            if (IsPaused) { Message = "일시정지: 진행 보류"; return; }
            if (composed)
            {
                if (!resultsComplete) { Message = "관련 논리 결과 대기"; return; }
                bool satisfied = ConditionsSatisfied;
                if (!legacySatisfied || !satisfied)
                {
                    if (handler.UsesFreeItem && freeUses >= steps[StepIndex].freeItemCount)
                    { Fail($"tutorial.steps.Array.data[{StepIndex}].freeItemCount: 무료 체험 횟수를 모두 사용했지만 조건을 충족하지 못했습니다."); return; }
                    hasPending = false; success = false; completionReceived = false; presented = false; resultsComplete = false;
                    State = TutorialProgressState.AwaitAction; Message = "조건 누적 중 · 다음 행동 대기";
                    IReadOnlyList<TutorialTargetEntity> current = readTargets?.Invoke() ?? Array.Empty<TutorialTargetEntity>();
                    if (ResolveActionTargets(steps[StepIndex], current)) RefreshHighlights(current);
                    return;
                }
            }
            StepIndex++; EnterStep();
        }
        private bool ResolveActionTargets(TutorialStepDefinition step, IReadOnlyList<TutorialTargetEntity> current)
        {
            foreach (bool first in new[] { true, false })
            {
                string name = first ? step.firstBinding : step.secondBinding;
                if (string.IsNullOrEmpty(name)) continue;
                TutorialTargetEntity entity = bindings.TryGetValue(name, out long id) ? current.SingleOrDefault(value => value.Occurrence == id) : null;
                if (entity == null || entity.Layer != TutorialTargetLayer.Content || entity.Cells.Count != 1)
                { Fail($"tutorial.steps.Array.data[{StepIndex}].{(first ? "firstBinding" : "secondBinding")}: 조작할 생성 개체가 없거나 한 칸으로 확정할 수 없습니다: {name}"); return false; }
                if (first) { step.first = entity.Cells[0]; step.hasFirst = true; }
                else { step.second = entity.Cells[0]; step.hasSecond = true; }
            }
            if ((!string.IsNullOrEmpty(step.firstBinding) || !string.IsNullOrEmpty(step.secondBinding)) && step.actionArea.Count == 0 && step.hasFirst && step.hasSecond &&
                !new Board.BoardEdge(step.first, step.second).IsAdjacent)
            { Fail($"tutorial.steps.Array.data[{StepIndex}].second: 생성 개체의 현재 위치와 교환 대상이 인접하지 않습니다."); return false; }
            return true;
        }

        private void EnterStep()
        {
            while (true)
            {
                hasPending = false; success = false; completionReceived = false; presented = false; freeUses = 0; resultsComplete = false;
                guidance = null;
                highlightTargets = Array.Empty<TutorialTargetSelection>(); targetHighlights = Array.Empty<Board.BoardCoordinate>();
                if (StepIndex >= steps.Count)
                {
                    handler = null; legacyConditionCount = 0; conditionStates = Array.Empty<ITutorialConditionState>(); occurrences = Array.Empty<HashSet<string>>(); counts = Array.Empty<int>();
                    State = TutorialProgressState.Completed; Message = "튜토리얼 완료"; return;
                }
                TutorialStepDefinition step = steps[StepIndex];
                IReadOnlyList<TutorialTargetEntity> current = readTargets?.Invoke() ?? Array.Empty<TutorialTargetEntity>();
                if (!ResolveActionTargets(step, current)) return;
                registry.TryGetStep(step, out handler);
                legacyConditionCount = step.results.Count;
                conditionStates = new ITutorialConditionState[legacyConditionCount + step.conditions.Count];
                highlightTargets = new TutorialTargetSelection[step.conditions.Count];
                occurrences = new HashSet<string>[step.results.Count + step.conditions.Count]; counts = new int[occurrences.Length];
                for (int i = 0; i < step.results.Count; i++)
                { conditionStates[i] = TutorialLegacyConditionAdapter.Create(step.results[i], registry); occurrences[i] = new HashSet<string>(StringComparer.Ordinal); }
                TutorialConditionContext context = new TutorialConditionContext(current, bindings, (name, id) =>
                {
                    if (bindings.TryGetValue(name, out long prior) && prior != id) throw new InvalidOperationException("생성 연결 이름이 중복됩니다: " + name);
                    bindings[name] = id;
                }, readMissionRemaining?.Invoke());
                for (int i = 0; i < step.conditions.Count; i++)
                {
                    registry.TryGetCondition(step.conditions[i].kind, out ITutorialConditionEvaluator evaluator);
                    occurrences[step.results.Count + i] = new HashSet<string>(StringComparer.Ordinal);
                    try
                    {
                        conditionStates[legacyConditionCount + i] = evaluator.Create(step.conditions[i], context); counts[legacyConditionCount + i] = conditionStates[legacyConditionCount + i].Count;
                        TutorialConditionDefinition condition = step.conditions[i];
                        if (step.automaticHighlights && condition.kind >= TutorialConditionKind.DurabilityDecrease && condition.kind <= TutorialConditionKind.Combined)
                            highlightTargets[i] = TutorialTargetSelection.Bind(condition.target, current, bindings);
                    }
                    catch (InvalidOperationException error) { Fail($"tutorial.steps.Array.data[{StepIndex}].conditions.Array.data[{i}]: {error.Message}"); return; }
                }
                State = handler.IsDescription ? TutorialProgressState.AwaitDescription : TutorialProgressState.AwaitAction;
                Message = handler.IsDescription ? "설명 확인 대기" : "지정 행동 대기";
                if (!handler.IsDescription && legacyConditionCount == 0 && conditionStates.Length > 0 && ConditionsSatisfied)
                { StepIndex++; continue; }
                RefreshHighlights(current);
                return;
                }
        }
    }
}
