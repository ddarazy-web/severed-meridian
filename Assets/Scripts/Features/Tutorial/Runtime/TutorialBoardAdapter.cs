using System;
using System.Collections.Generic;
using System.Linq;
using Board;
using Elements;
using Levels;
using Simulation;

namespace Tutorial
{
    /// <summary>튜토리얼 실행과 보드 사이의 연결 수명 및 공급 사본을 소유한다.</summary>
    public sealed class TutorialBoardAdapter : IDisposable
    {
        private BoardActionExecutor executor;
        private RuntimeSupply normalSupply;
        private bool released;
        private bool pending;
        private int recordCursor;
        private int recoveryCursor;
        private TutorialActionTicket ticket;
        private long firstPower, secondPower;
        public BoardActionExecutor Executor => executor;
        public TutorialProgress Progress { get; }

        /// <param name="definition">검사할 제작 데이터. 보관하지 않으며 소유권도 받지 않는다.</param>
        /// <param name="executor">이 연결부가 제어할 실제 퍼즐 실행기.</param>
        public TutorialBoardAdapter(LevelDefinition definition, BoardActionExecutor executor)
            : this(definition, executor, executor == null ? null : new RuntimeSupply(executor.State.Supply), false) { }

        private TutorialBoardAdapter(LevelDefinition definition, BoardActionExecutor executor, RuntimeSupply normalSupply, bool prepared)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (executor == null) throw new ArgumentNullException(nameof(executor));
            if (!definition.HasTutorial) throw new ArgumentException("활성 튜토리얼이 없습니다.", nameof(definition));
            if (executor.PendingBoosters.Count > 0 || executor.BoosterPlacements.Count > 0)
                throw new ArgumentException("튜토리얼에서는 시작 부스터를 사용할 수 없습니다.", nameof(executor));
            Progress = new TutorialProgress(definition.Tutorial);
            if (Progress.State == TutorialProgressState.Error) throw new ArgumentException(Progress.Message, nameof(definition));
            this.normalSupply = normalSupply;
            this.executor = executor;
            if (!prepared) executor.State.Supply = PrepareSupply(definition, executor.State.ElementCatalog);
            executor.SetEndingDeferred(true);
        }

        private static RuntimeSupply PrepareSupply(LevelDefinition definition, ElementCatalog catalog)
        {
            List<LevelValidationIssue> issues = LevelDefinitionValidator.Validate(definition);
            if (issues.Count != 0) throw new ArgumentException(string.Join(" · ", issues.Select(issue => issue.PropertyPath + ": " + issue.Message)), nameof(definition));
            ElementLevelSupplyLayout layout = new ElementLevelSupplyLayout(definition.Tutorial.supply, catalog, issues);
            if (issues.Count != 0) throw new ArgumentException(string.Join(" · ", issues.Select(issue => issue.Message)), nameof(definition));
            RuntimeSupply fixedSupply = new RuntimeSupply(layout.Value);
            foreach (RuntimeSource source in fixedSupply.Sources)
                source.ItemDefinitions = Array.AsReadOnly((ElementDefinition[])layout.Items[source.Coordinate].Clone());
            return fixedSupply;
        }

        /// <summary>초기 실행기 정착보다 먼저 고정 공급을 적용한다. 입력 상태 자체는 보존한다.</summary>
        /// <param name="definition">준비한 레벨.</param><param name="startingState">고정 시드로 구성한 보드.</param>
        /// <returns>실행기와 사본을 소유하는 연결부.</returns>
        public static TutorialBoardAdapter Prepare(LevelDefinition definition, LevelRuntimeState startingState)
        {
            if (startingState == null) throw new ArgumentNullException(nameof(startingState));
            RuntimeSupply fixedSupply = PrepareSupply(definition, startingState.ElementCatalog);
            RuntimeSupply original = startingState.Supply;
            try
            {
                startingState.Supply = fixedSupply;
                return new TutorialBoardAdapter(definition, new BoardActionExecutor(startingState), new RuntimeSupply(original), true);
            }
            finally { startingState.Supply = original; }
        }

        public bool TryBegin(TutorialInput input)
        {
            if (executor == null || released || !Progress.TryApprove(input, out ticket)) return false;
            if (input.Kind == TutorialInputKind.Next) return true;
            pending = true; recordCursor = 0; recoveryCursor = executor.State.Recoveries.Count;
            firstPower = input.First.HasValue ? executor.State.CellAt(input.First.Value).ContentOccurrence : 0;
            secondPower = input.Second.HasValue ? executor.State.CellAt(input.Second.Value).ContentOccurrence : 0;
            return true;
        }

        public bool CanSelectBlock(BoardCoordinate coordinate)
        {
            TutorialProgressSnapshot snapshot = Progress.Snapshot;
            return snapshot.First.HasValue && snapshot.Second.HasValue &&
                (coordinate.Equals(snapshot.First.Value) || coordinate.Equals(snapshot.Second.Value)) &&
                Progress.CanApprove(TutorialInput.Swap(snapshot.First.Value, snapshot.Second.Value));
        }

        public bool CanSelectItem(BoardItem item)
        {
            TutorialProgressSnapshot snapshot = Progress.Snapshot;
            return Progress.CanApprove(TutorialInput.UseItem(item, snapshot.First, snapshot.Second));
        }

        public bool CanSelectItemTarget(BoardItem item, BoardCoordinate coordinate)
        {
            TutorialProgressSnapshot snapshot = Progress.Snapshot;
            return CanSelectItem(item) && (snapshot.First?.Equals(coordinate) == true || snapshot.Second?.Equals(coordinate) == true) &&
                executor.CanSelectApprovedItemTarget(item, coordinate);
        }

        public void ReportAction(bool succeeded)
        {
            if (executor == null || !pending) return;
            if (!succeeded) { Progress.ReportCompletion(ticket, false); pending = false; return; }
            string actualPower = executor.TurnEffects.ElementRecords.FirstOrDefault(record => record.Kind == ElementExecutionKind.Activated &&
                (record.Occurrence == firstPower || record.Occurrence == secondPower) &&
                (string.IsNullOrEmpty(Progress.Snapshot.ActionDefinitionId) || record.DefinitionId == Progress.Snapshot.ActionDefinitionId))?.DefinitionId;
            Progress.ReportCompletion(ticket, true, actualPower);
            ReadResults();
        }

        private void ReadResults()
        {
            if (!pending || executor?.TurnEffects == null) return;
            List<ElementExecutionRecord> records = executor.TurnEffects.ElementRecords;
            while (recordCursor < records.Count)
            {
                ElementExecutionRecord record = records[recordCursor++];
                TutorialResultKind kind = record.Kind == ElementExecutionKind.Generated ? TutorialResultKind.Generated :
                    record.Kind == ElementExecutionKind.Activated ? TutorialResultKind.Activated : TutorialResultKind.Removed;
                Progress.ReportResults(ticket, new[] { new TutorialResultRecord(kind, record.DefinitionId, record.Occurrence.ToString(System.Globalization.CultureInfo.InvariantCulture), record.Coordinate) });
            }
            // 회수는 행동 시작과 낙하 양쪽에서 발생하므로 상태의 실제 회수 기록을 읽는다.
            while (recoveryCursor < executor.State.Recoveries.Count)
            {
                ElementExecutionRecord record = executor.State.Recoveries[recoveryCursor++].Removal;
                Progress.ReportResults(ticket, new[] { new TutorialResultRecord(TutorialResultKind.Removed, record.DefinitionId,
                    record.Occurrence.ToString(System.Globalization.CultureInfo.InvariantCulture), record.Coordinate) });
            }
        }

        public void ObserveCascade(CascadeStepResult step)
        {
            if (executor == null || released) return;
            ReadResults();
            if (executor.Outcome?.Kind == BoardOutcomeKind.Aborted || executor.Phase == BoardActionPhase.Stopped)
                Progress.Fail($"tutorial.steps.Array.data[{Progress.StepIndex}]: {step.Message}");
            if (step.Settlement?.IsApplied == true && step.Settlement.EmptyCells.Count > 0)
                Progress.Fail($"tutorial.steps.Array.data[{Progress.StepIndex}].supply: 고정 공급 부족 또는 생성구 누락 · 빈칸 {step.Settlement.EmptyCells[0]}");
        }

        /// <summary>논리 연쇄와 표시를 별도로 확인하며 정지 중에는 진행하지 않는다.</summary>
        /// <param name="presentationReady">보드·드론·낙하·수집 표시가 모두 끝났는지 여부.</param>
        /// <param name="paused">팝업 등을 포함한 일시정지 여부.</param>
        public void Tick(bool presentationReady, bool paused)
        {
            if (executor == null || released) return;
            Progress.SetPaused(paused);
            if (paused || executor.HasPendingCascade) return;
            if (pending)
            {
                ReadResults();
                Progress.ReportResultsComplete(ticket);
                if (presentationReady) Progress.ReportPresentationComplete(ticket);
                if (Progress.State != TutorialProgressState.AwaitPresentation) pending = false;
            }
            if (presentationReady && ReleaseCompleted()) executor.ResumeDeferredEnding();
        }

        /// <summary>완료된 튜토리얼의 공급만 복귀시킨다. 종료 판단은 세션의 연출 경계에서 별도로 재개한다.</summary>
        /// <returns>이번 호출에서 공급을 전환했는지 여부.</returns>
        public bool ReleaseCompleted()
        {
            if (executor == null || released || Progress.State != TutorialProgressState.Completed || executor.HasPendingCascade) return false;
            executor.State.Supply = normalSupply;
            normalSupply = null;
            released = true;
            executor.SetEndingDeferred(false);
            return true;
        }

        public void Dispose()
        {
            Progress.Dispose();
            pending = false;
            executor = null;
            normalSupply = null;
        }
    }
}
