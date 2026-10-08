using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Board;

namespace Tutorial
{
    /// <summary>단계 진입 때 고정한 개체 목록과 사건 위치 필터. 제작 데이터를 참조하여 변경하지 않는다.</summary>
    public sealed class TutorialTargetSelection
    {
        private readonly TutorialTargetKind kind;
        private readonly TutorialTargetLayer layer;
        private readonly string definitionId;
        private readonly HashSet<BoardCoordinate> cells;
        private readonly long occurrence;
        public ReadOnlyCollection<long> InitialOccurrences { get; }

        private TutorialTargetSelection(TutorialTargetDefinition source, IReadOnlyList<TutorialTargetEntity> current,
            IReadOnlyDictionary<string, long> bindings)
        {
            if (!Enum.IsDefined(typeof(TutorialTargetKind), source.kind) || !Enum.IsDefined(typeof(TutorialTargetLayer), source.layer))
                throw new InvalidOperationException("지원하지 않는 튜토리얼 대상 종류 또는 층입니다.");
            kind = source.kind; layer = source.layer; definitionId = source.definitionId;
            cells = new HashSet<BoardCoordinate>(source.cells ?? new List<BoardCoordinate>());
            if (kind == TutorialTargetKind.Area && cells.Count == 0)
                throw new InvalidOperationException("대상 영역의 셀을 선택하세요.");
            if (kind == TutorialTargetKind.Definition && string.IsNullOrWhiteSpace(definitionId))
                throw new InvalidOperationException("대상 종류를 선택하세요.");
            if (kind == TutorialTargetKind.Entity)
            {
                TutorialTargetEntity entity = current.SingleOrDefault(value => value.Layer == layer && value.Cells.Contains(source.coordinate));
                if (entity == null) throw new InvalidOperationException("선택한 칸의 대상 개체가 없습니다: " + source.coordinate);
                occurrence = entity.Occurrence;
            }
            if (kind == TutorialTargetKind.Generated)
            {
                if (string.IsNullOrWhiteSpace(source.binding) || !bindings.TryGetValue(source.binding, out occurrence))
                    throw new InvalidOperationException("생성 결과 연결이 없습니다: " + source.binding);
                if (!current.Any(value => value.Occurrence == occurrence && value.Layer == layer))
                    throw new InvalidOperationException("생성 결과 개체가 소실되었습니다: " + source.binding);
            }
            InitialOccurrences = Array.AsReadOnly(current.Where(value => value.Cells.Any(at => Matches(value, at)))
                .Select(value => value.Occurrence).Distinct().ToArray());
        }

        public static TutorialTargetSelection Bind(TutorialTargetDefinition source, IReadOnlyList<TutorialTargetEntity> current,
            IReadOnlyDictionary<string, long> bindings) => new TutorialTargetSelection(source, current, bindings);

        public bool Matches(TutorialTargetEntity entity, BoardCoordinate eventPosition)
        {
            if (entity.Layer != layer) return false;
            return kind switch
            {
                TutorialTargetKind.Board => true,
                TutorialTargetKind.Entity or TutorialTargetKind.Generated => entity.Occurrence == occurrence,
                TutorialTargetKind.Definition => entity.Definition.Id.Value == definitionId,
                TutorialTargetKind.Area => cells.Contains(eventPosition),
                _ => false
            };
        }

        internal IEnumerable<BoardCoordinate> HighlightCells(IReadOnlyList<TutorialTargetEntity> current, bool initialOnly)
        {
            // 보드 전체는 검색 범위이며 포커스 영역으로 펼치지 않는다. 영역은 빈칸도 유지한다.
            if (kind == TutorialTargetKind.Board) return Array.Empty<BoardCoordinate>();
            if (kind == TutorialTargetKind.Area) return cells;
            return current.Where(entity => !initialOnly || InitialOccurrences.Contains(entity.Occurrence))
                .SelectMany(entity => entity.Cells.Where(at => Matches(entity, at))).Distinct();
        }
    }
}
