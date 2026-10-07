using System;
using System.Collections.Generic;
using System.Linq;
using Board;
using Elements;
using Levels;

namespace Tutorial
{
    /// <summary>종류별 규칙이 공유하는 배치·정의 검사. 보드 없는 엔진 준비에서는 정적 대상 형식만 검사한다.</summary>
    public sealed class TutorialValidationContext
    {
        private readonly LevelDefinition level;
        private readonly ElementCatalog catalog;
        private readonly string path;
        private readonly Action<string, string, BoardCoordinate?> report;
        private readonly bool firstAction;
        internal TutorialValidationContext(LevelDefinition level, ElementCatalog catalog, string path,
            Action<string, string, BoardCoordinate?> report, bool firstAction = false)
        { this.level = level; this.catalog = catalog; this.path = path; this.report = report; this.firstAction = firstAction; }
        public void Error(string suffix, string message, BoardCoordinate? coordinate = null) => report(path + suffix, message, coordinate);
        internal TutorialValidationContext ForChild(string suffix) => new TutorialValidationContext(level, catalog, path + suffix, report);
        public void Cell(BoardCoordinate coordinate, string suffix)
        {
            if (level == null) return;
            string error = LevelPlacementRules.CellError(level, coordinate);
            if (error != null) Error(suffix, error, coordinate);
        }
        public ElementDefinition Resolve(string id, string suffix, bool power = false)
        {
            try
            {
                ElementId elementId = new ElementId(id);
                if (catalog == null) return null;
                ElementDefinition definition = catalog.Get(elementId);
                PackedElementDefinition.ValidateDefinition(definition);
                if (power && definition.Supply?.Behavior != ElementSupplyBehavior.Power) throw new ArgumentException("파워 생성 정의를 지정하세요.");
                return definition;
            }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException || error is KeyNotFoundException)
            { Error(suffix, $"정의 '{id}': {error.Message}"); return null; }
        }
        public void ValidateTargets(TutorialStepDefinition step, int count)
        {
            if (count > 0)
            {
                if (!step.hasFirst) Error(".hasFirst", "첫 번째 대상 칸이 필요합니다.");
                else Cell(step.first, ".first");
            }
            else if (step.hasFirst || step.hasSecond) Error("", "설명·섞기 단계는 행동 대상이 없습니다.");
            if (count == 2)
            {
                if (!step.hasSecond) Error(".hasSecond", "두 번째 대상 칸이 필요합니다.");
                else Cell(step.second, ".second");
                if (step.hasFirst && step.hasSecond)
                {
                    BoardEdge edge = new BoardEdge(step.first, step.second);
                    if (!edge.IsAdjacent) Error(".second", "교환 대상은 인접해야 합니다.", step.second);
                    else if (level?.Flow?.Walls?.Contains(edge) == true) Error(".second", "벽을 통과하는 교환입니다.", step.second);
                }
            }
            else if (count == 1 && step.hasSecond) Error(".hasSecond", "망치는 한 칸만 선택합니다.");
        }
        public void ValidateInitialTargets(TutorialStepDefinition step, int count, string powerId = null)
        {
            if (!firstAction || level == null || count == 0) return;
            IReadOnlyList<ElementPlacementDefinition> placements = level.Elements;
            try { if (level.SchemaVersion == 4) placements = LegacyElementLevelAdapter.Preview(level); }
            catch (Exception error) when (error is ArgumentException || error is NullReferenceException)
            { Error("", "초기 배치 데이터 오류: " + error.Message); placements = Array.Empty<ElementPlacementDefinition>(); }
            foreach (BoardCoordinate coordinate in count == 2 ? new[] { step.first, step.second } : new[] { step.first })
            {
                ElementPlacementDefinition placement = placements?.FirstOrDefault(value => value?.layer == PlacementLayer.Block && value.coordinate.Equals(coordinate));
                if (count == 2)
                {
                    ElementDefinition definition = placement == null ? null : Resolve(placement.definitionId, "");
                    if (definition?.Supply?.Behavior != ElementSupplyBehavior.FixedNormal && definition?.Supply?.Behavior != ElementSupplyBehavior.Power)
                        Error("", "첫 교환은 초기 고정 블록 또는 파워를 대상으로 지정하세요.", coordinate);
                    if (placements?.Any(value => value?.layer == PlacementLayer.Cover && value.coordinate.Equals(coordinate)) == true)
                        Error("", "덮개가 있는 초기 블록은 교환할 수 없습니다.", coordinate);
                }
                else if (placement == null && placements?.Any(value => value != null && LevelPlacementRules.Footprint(value.coordinate,
                    Resolve(value.definitionId, "")?.Placement?.Size ?? Resolve(value.definitionId, "")?.ChargePlacement?.Size ?? 1).Contains(coordinate)) != true)
                    Error("", "첫 망치 대상에 제거·피해를 받을 배치가 없습니다.", coordinate);
            }
            if (powerId != null && placements?.Any(value => value?.layer == PlacementLayer.Block && value.coordinate.Equals(step.first) && value.definitionId == powerId) != true)
                Error(".first", "첫 파워 교환의 첫 칸에 지정 파워가 없습니다.", step.first);
        }
    }
}
