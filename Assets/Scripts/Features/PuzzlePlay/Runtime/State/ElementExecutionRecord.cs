using System.Threading;
using Board;

namespace Simulation
{
    internal enum ElementExecutionKind { Generated, Activated, Removed }

    /// <summary>실제 규칙이 발생시킨 요소 기록. 저장 데이터 및 Unity 표시 개체의 수명과 독립적이다.</summary>
    internal sealed class ElementExecutionRecord
    {
        public ElementExecutionKind Kind { get; }
        public string DefinitionId { get; }
        public long Occurrence { get; }
        public BoardCoordinate Coordinate { get; }
        internal ElementExecutionRecord(ElementExecutionKind kind, string definitionId, long occurrence, BoardCoordinate coordinate)
        { Kind = kind; DefinitionId = definitionId; Occurrence = occurrence; Coordinate = coordinate; }
    }

    internal static class RuntimeElementOccurrence
    {
        private static long next;
        // 논리 사본은 식별을 유지한다. 신규 생성은 게임 난수를 소비하지 않고 새 식별을 받는다.
        internal static long Next() => Interlocked.Increment(ref next);
    }
}
