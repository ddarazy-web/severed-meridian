using Board;

namespace Levels
{
    public enum LevelValidationCode
    {
        MissingLevel,
        UnsupportedSchemaVersion,
        InvalidLevelNumber,
        InvalidMoveCount,
        InvalidColorCount,
        InvalidColor,
        DuplicateColor,
        MissingBoard,
        UnsupportedBoardSize,
        CellCountMismatch,
        MissingInitialBlocks,
        CoordinateOutOfRange,
        DuplicatePlacement,
        InactiveCellPlacement,
        MissingCell,
        InvalidBlockKind,
        UnusedFixedColor,
        DuplicateLevelNumber,
        MissingPlacementList,
        InvalidPlacementValue,
        PlacementConflict,
        InvalidCover,
        UnexpectedLegacyData,
        InvalidFlow,
        FlowCycle,
        InvalidMerge,
        InvalidWall,
        InvalidPortal,
        InvalidArrival,
        InvalidObstacleId,
        InvalidConnection,
        InvalidWire,
        InvalidSupply,
        SupplyConflict,
        InvalidRecovery,
        InvalidMission,
        InsufficientSupply,
        InvalidTutorial
    }

    public sealed class LevelValidationIssue
    {
        public LevelValidationCode Code { get; }
        public string Message { get; }
        public string PropertyPath { get; }
        public BoardCoordinate? Coordinate { get; }

        public LevelValidationIssue(LevelValidationCode code, string message,
            string propertyPath, BoardCoordinate? coordinate = null)
        {
            Code = code;
            Message = message;
            PropertyPath = propertyPath;
            Coordinate = coordinate;
        }

        public override string ToString()
        {
            return $"{Code}: {Message}" + (Coordinate.HasValue ? $" {Coordinate.Value}" : "") +
                $"\n필드: {PropertyPath}";
        }
    }
}
