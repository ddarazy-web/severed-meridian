using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Board;
using Simulation;

namespace Tutorial
{
    public enum TutorialProgressState { AwaitDescription, AwaitAction, AwaitPresentation, Completed, Error, Cancelled }
    public enum TutorialInputKind { Next, Swap, Activate, Item }

    /// <summary>게임을 변경하지 않는 입력 의도다. 승인 이후 실제 실행은 게임 연결부가 소유한다.</summary>
    public readonly struct TutorialInput
    {
        public TutorialInputKind Kind { get; }
        public BoardCoordinate? First { get; }
        public BoardCoordinate? Second { get; }
        public BoardItem? Item { get; }
        private TutorialInput(TutorialInputKind kind, BoardCoordinate? first = null, BoardCoordinate? second = null, BoardItem? item = null)
        { Kind = kind; First = first; Second = second; Item = item; }
        public static TutorialInput Next() => new TutorialInput(TutorialInputKind.Next);
        public static TutorialInput Swap(BoardCoordinate first, BoardCoordinate second) => new TutorialInput(TutorialInputKind.Swap, first, second);
        public static TutorialInput Activate(BoardCoordinate at) => new TutorialInput(TutorialInputKind.Activate, at);
        public static TutorialInput UseItem(BoardItem item, BoardCoordinate? first = null, BoardCoordinate? second = null)
            => new TutorialInput(TutorialInputKind.Item, first, second, item);
    }

    /// <summary>세션·단계·시도를 구분한다. 이전 시도의 결과를 새 실행에 반영하지 않는다.</summary>
    public readonly struct TutorialActionTicket : IEquatable<TutorialActionTicket>
    {
        public Guid Session { get; }
        public int Step { get; }
        public long Attempt { get; }
        internal TutorialActionTicket(Guid session, int step, long attempt) { Session = session; Step = step; Attempt = attempt; }
        public bool Equals(TutorialActionTicket other) => Session == other.Session && Step == other.Step && Attempt == other.Attempt;
        public override bool Equals(object obj) => obj is TutorialActionTicket other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Session, Step, Attempt);
    }

    /// <summary>실제 실행 기록에서 변환한다. 같은 본체의 여러 footprint 좌표는 같은 OccurrenceId를 사용한다.</summary>
    public sealed class TutorialResultRecord
    {
        public TutorialResultKind Kind { get; }
        public string DefinitionId { get; }
        public string OccurrenceId { get; }
        public BoardCoordinate? Coordinate { get; }
        public TutorialResultRecord(TutorialResultKind kind, string definitionId, string occurrenceId, BoardCoordinate? coordinate = null)
        {
            if (string.IsNullOrWhiteSpace(definitionId)) throw new ArgumentException("결과 정의 ID가 필요합니다.", nameof(definitionId));
            if (string.IsNullOrWhiteSpace(occurrenceId)) throw new ArgumentException("결과 발생 식별값이 필요합니다.", nameof(occurrenceId));
            Kind = kind; DefinitionId = definitionId; OccurrenceId = occurrenceId; Coordinate = coordinate;
        }
    }

    public interface ITutorialStepHandler
    {
        bool IsDescription { get; }
        bool UsesFreeItem { get; }
        bool ConsumesMove { get; }
        void Validate(TutorialStepDefinition step, TutorialValidationContext context);
        IEnumerable<string> References(TutorialStepDefinition step);
        bool Allows(TutorialStepDefinition step, TutorialInput input);
        bool IsSuccessful(TutorialStepDefinition step, TutorialInput input, bool succeeded, string definitionId);
    }

    public interface ITutorialResultEvaluator
    {
        void Validate(TutorialResultDefinition result, TutorialValidationContext context);
        IEnumerable<string> References(TutorialResultDefinition result);
        bool Matches(TutorialResultDefinition result, TutorialResultRecord record);
    }

    /// <summary>표현 계층용 불변 사본이다. 제작 데이터나 진행 상태의 변경 권한을 제공하지 않는다.</summary>
    public sealed class TutorialProgressSnapshot
    {
        public TutorialProgressState State { get; }
        public int StepIndex { get; }
        public bool IsPaused { get; }
        public bool FreeItemAvailable { get; }
        public string Message { get; }
        public string Instructions { get; }
        public TutorialStepKind? Kind { get; }
        public BoardItem? Item { get; }
        public BoardCoordinate? First { get; }
        public BoardCoordinate? Second { get; }
        public string ActionDefinitionId { get; }
        public ReadOnlyCollection<BoardCoordinate> Highlights { get; }
        public ReadOnlyCollection<int> ConditionCounts { get; }
        internal TutorialProgressSnapshot(TutorialProgressState state, int index, bool paused, bool free, string message,
            TutorialStepDefinition step, int[] counts)
        {
            State = state; StepIndex = index; IsPaused = paused; FreeItemAvailable = free; Message = message;
            Instructions = step?.instructions ?? ""; Kind = step?.kind; Item = step?.kind == TutorialStepKind.Item ? step.item : (BoardItem?)null;
            First = step?.hasFirst == true ? step.first : (BoardCoordinate?)null;
            Second = step?.hasSecond == true ? step.second : (BoardCoordinate?)null;
            ActionDefinitionId = step?.actionDefinitionId ?? "";
            Highlights = Array.AsReadOnly(step?.highlights?.ToArray() ?? Array.Empty<BoardCoordinate>());
            ConditionCounts = Array.AsReadOnly((int[])counts.Clone());
        }
    }
}
