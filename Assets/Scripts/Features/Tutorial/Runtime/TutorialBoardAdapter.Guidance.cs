using System;
using System.Collections.Generic;
using System.Linq;
using Board;
using Elements;
using Simulation;

namespace Tutorial
{
    public sealed partial class TutorialBoardAdapter
    {
        private int guidanceStep = -1, guidanceTurn = -1, guidanceItems = -1;

        private void RefreshGuidance()
        {
            if (Progress.State != TutorialProgressState.AwaitAction || Progress.Snapshot.ActionArea.Count == 0) return;
            if (guidanceStep == Progress.StepIndex && guidanceTurn == executor.Turn && guidanceItems == executor.ItemUses.Count) return;
            guidanceStep = Progress.StepIndex; guidanceTurn = executor.Turn; guidanceItems = executor.ItemUses.Count;
            if (TryFindGuidance(out TutorialInput input)) Progress.SetGuidance(input);
            else Progress.Fail($"tutorial.steps.Array.data[{Progress.StepIndex}].actionArea: 현재 조건에 기여하는 유효 행동이 없습니다.");
        }

        /// <summary>안정된 보드의 독립 사본에서 아직 충족하지 않은 조건에 기여하는 행동만 조회한다.</summary>
        public bool TryFindGuidance(out TutorialInput input)
        {
            input = default;
            if (executor == null || released || pending || executor.HasPendingCascade || Progress.State != TutorialProgressState.AwaitAction || Progress.IsPaused) return false;
            TutorialProgressSnapshot snapshot = Progress.Snapshot;
            foreach (BoardCoordinate first in snapshot.ActionArea.OrderBy(value => value.Row).ThenBy(value => value.Column))
            {
                if (snapshot.Item == BoardItem.Hammer)
                {
                    TutorialInput candidate = TutorialInput.UseItem(BoardItem.Hammer, first);
                    if (Progress.CanApprove(candidate) && Contributes(candidate)) { input = candidate; return true; }
                    continue;
                }
                foreach (BoardCoordinate second in snapshot.ActionArea.OrderBy(value => value.Row).ThenBy(value => value.Column))
                {
                    TutorialInput candidate = snapshot.Item.HasValue ? TutorialInput.UseItem(snapshot.Item.Value, first, second) : TutorialInput.Swap(first, second);
                    if (Progress.CanApprove(candidate) && Contributes(candidate)) { input = candidate; return true; }
                }
            }
            return false;
        }

        private bool Contributes(TutorialInput input)
        {
            var copy = new BoardActionExecutor(executor.State, Math.Max(1, executor.Turn), executor.TurnEffects);
            copy.SetEndingDeferred(true);
            long first = copy.State.CellAt(input.First.Value).ContentOccurrence;
            long second = input.Second.HasValue ? copy.State.CellAt(input.Second.Value).ContentOccurrence : 0;
            TutorialTargetEntity firstTarget = TutorialTargetQuery.Capture(copy.State).FirstOrDefault(value => value.Occurrence == first);
            int missionIndex = copy.State.MissionProgressRecords.Count, elementIndex = 0;
            bool applied = input.Kind == TutorialInputKind.Item ? copy.UseApprovedFreeItem(input.Item.Value, input.First, input.Second).IsApplied :
                copy.Swap(input.First.Value, input.Second.Value).IsApplied;
            if (!applied) return false;
            string requiredPower = Progress.Snapshot.ActionDefinitionId;
            if (Progress.Snapshot.Kind == TutorialStepKind.PowerSwap && !copy.TurnEffects.ElementRecords.Any(record =>
                record.Kind == ElementExecutionKind.Activated && (record.Occurrence == first || record.Occurrence == second) && record.DefinitionId == requiredPower)) return false;
            var events = new List<TutorialConditionEvent>();
            events.Add(input.Kind == TutorialInputKind.Item ? new TutorialConditionEvent("preview:item", input.Item.Value) :
                new TutorialConditionEvent(TutorialConditionKind.SuccessfulSwap, "preview:swap"));
            if (copy.TurnEffects.Combination != null)
                events.Add(new TutorialConditionEvent(TutorialConditionKind.Combined, "combination", firstTarget,
                    copy.TurnEffects.Combination.Center, 1, EffectOrigins.Combination(copy.TurnEffects.Combination.Kind), true));
            IEnumerable<MatchDecision> direct = input.Kind == TutorialInputKind.Item ? copy.ItemUses.Last().Decisions : copy.LastApplied.Decisions;
            foreach (MatchDecision decision in direct)
                events.Add(new TutorialConditionEvent(TutorialConditionKind.Match, "direct:" + decision.Selected.Key, decision.Selected.Cells.Count, decision.Selected.Color, true));
            void Read()
            {
                while (elementIndex < copy.TurnEffects.ElementRecords.Count)
                {
                    ElementExecutionRecord record = copy.TurnEffects.ElementRecords[elementIndex++];
                    events.Add(ConditionEvent(record, elementIndex, copy, first, second));
                }
                while (missionIndex < copy.State.MissionProgressRecords.Count)
                {
                    MissionProgressRecord record = copy.State.MissionProgressRecords[missionIndex++];
                    events.Add(new TutorialConditionEvent("mission:" + missionIndex, record.MissionIndex, record.Amount));
                }
            }
            Read();
            int budget = copy.CascadeLimit * 2 + 1;
            while (copy.HasPendingCascade && budget-- > 0)
            {
                CascadeStepResult step = copy.AdvanceCascade();
                if (!step.IsApplied || step.Reason == CascadeStepReason.Aborted || step.Settlement?.EmptyCells.Count > 0) return false;
                if (step.Reason == CascadeStepReason.Matched)
                    foreach (MatchDecision decision in step.Decisions)
                        events.Add(new TutorialConditionEvent(TutorialConditionKind.Match, "cascade:" + step.Round + ":" + decision.Selected.Key,
                            decision.Selected.Cells.Count, decision.Selected.Color));
                Read();
            }
            return !copy.HasPendingCascade && Progress.Contributes(events, TutorialTargetQuery.Capture(copy.State));
        }

        // 실제 집계와 안내 시뮬레이션이 같은 사건 변환 규칙을 사용한다.
        private static TutorialConditionEvent ConditionEvent(ElementExecutionRecord record, int index, BoardActionExecutor source, long first, long second)
        {
            ElementId id = new ElementId(record.DefinitionId);
            if (!source.State.ElementCatalog.TryGet(id, out ElementDefinition definition)) definition = LegacyElementDefinitions.DefaultCatalog.Get(id);
            TutorialTargetLayer layer = definition.Layer == null ? TutorialTargetLayer.Content :
                definition.Layer.Behavior == ElementLayerBehavior.NormalConsumption ? TutorialTargetLayer.Floor : TutorialTargetLayer.Cover;
            var entity = new TutorialTargetEntity(record.Occurrence, definition, layer, new[] { record.Coordinate },
                record.Kind == ElementExecutionKind.Damaged ? record.After : (int?)null, record.RocketDirection);
            TutorialConditionKind kind = record.Kind switch
            {
                ElementExecutionKind.Damaged => TutorialConditionKind.DurabilityDecrease,
                ElementExecutionKind.Generated => TutorialConditionKind.Generated,
                ElementExecutionKind.Activated => TutorialConditionKind.Activated,
                _ => TutorialConditionKind.Removed
            };
            string eventId = record.Kind == ElementExecutionKind.Damaged ? "damage:" + index : record.Kind + ":" + record.Occurrence;
            bool direct = record.Kind == ElementExecutionKind.Activated && source.TurnEffects.Combination == null && (record.Occurrence == first || record.Occurrence == second);
            return new TutorialConditionEvent(kind, eventId, entity, record.EventCoordinate,
                record.Kind == ElementExecutionKind.Damaged ? record.Before - record.After : 1, record.Origin, direct);
        }
    }
}
