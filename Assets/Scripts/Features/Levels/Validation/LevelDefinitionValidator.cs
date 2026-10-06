using System;
using System.Collections.Generic;
using Board;

namespace Levels
{
    public static class LevelDefinitionValidator
    {
        internal static List<LevelValidationIssue> ValidateElements(LevelDefinition source, Elements.ElementLevelLayout layout)
        {
            List<LevelValidationIssue> issues = new List<LevelValidationIssue>(layout.Issues);
            if (source.LevelNumber <= 0) issues.Add(new LevelValidationIssue(LevelValidationCode.InvalidLevelNumber, "레벨 번호는 양수여야 합니다.", "levelNumber"));
            if (source.MoveCount <= 0) issues.Add(new LevelValidationIssue(LevelValidationCode.InvalidMoveCount, "이동 횟수는 양수여야 합니다.", "moveCount"));
            ValidateColors(source, issues); ValidateBoard(source.Board, issues);
            LevelFlowRules.Validate(layout.Level, issues); LevelConnectionRules.Validate(layout.Level, issues, layout.Bodies);
            List<LevelValidationIssue> supplyIssues = new List<LevelValidationIssue>();
            LevelSupplyRules.Validate(layout.Level, supplyIssues,
                (sourceIndex, itemIndex) => layout.Supply.ItemError(layout.Level, sourceIndex, itemIndex), () => layout.Supply.ScrapMaximum, layout.Supply.HasFixed);
            foreach (LevelValidationIssue issue in supplyIssues)
                issues.Add(new LevelValidationIssue(issue.Code, issue.Message,
                    issue.PropertyPath.StartsWith("supply", StringComparison.Ordinal) ? "elementSupply" + issue.PropertyPath.Substring(6) : issue.PropertyPath,
                    issue.Coordinate));
            LevelMissionRules.Validate(layout.Level, issues, layout.MissionSupply);
            return issues;
        }
        public static List<LevelValidationIssue> Validate(LevelDefinition level)
        {
            if (level != null && level.SchemaVersion == 5)
            {
                try
                {
                    using (Elements.ElementLevelLayout layout = new Elements.ElementLevelLayout(level, level.CreateElementCatalog()))
                        return ValidateElements(level, layout);
                }
                catch (ArgumentException error)
                {
                    return new List<LevelValidationIssue> { new LevelValidationIssue(LevelValidationCode.InvalidPlacementValue,
                        error.Message, "elementCatalog") };
                }
            }
            List<LevelValidationIssue> issues = new List<LevelValidationIssue>();
            if (level == null)
            {
                issues.Add(new LevelValidationIssue(LevelValidationCode.MissingLevel, "레벨이 없습니다.", ""));
                return issues;
            }

            if (level.SchemaVersion < 1 || level.SchemaVersion > LevelDefinition.LegacySchemaVersion)
                issues.Add(new LevelValidationIssue(LevelValidationCode.UnsupportedSchemaVersion,
                    "지원하지 않는 저장 형식 버전입니다.", "schemaVersion"));
            if (level.LevelNumber <= 0)
                issues.Add(new LevelValidationIssue(LevelValidationCode.InvalidLevelNumber,
                    "레벨 번호는 양수여야 합니다.", "levelNumber"));
            if (level.MoveCount <= 0)
                issues.Add(new LevelValidationIssue(LevelValidationCode.InvalidMoveCount,
                    "이동 횟수는 양수여야 합니다.", "moveCount"));

            ValidateColors(level, issues);
            ValidateBoard(level.Board, issues);
            ValidatePlacements(level, issues);
            ValidateLayers(level, issues);
            LevelFlowRules.Validate(level, issues);
            LevelConnectionRules.Validate(level, issues);
            LevelSupplyRules.Validate(level, issues);
            LevelMissionRules.Validate(level, issues);
            return issues;
        }

        private static void ValidateColors(LevelDefinition level, List<LevelValidationIssue> issues)
        {
            if (level.Colors == null || level.Colors.Count < 3 || level.Colors.Count > 5)
                issues.Add(new LevelValidationIssue(LevelValidationCode.InvalidColorCount,
                    "사용 색은 다섯 종류 중 3~5종을 선택해야 합니다.", "colors"));
            if (level.Colors == null)
                return;

            HashSet<RabbitColor> seen = new HashSet<RabbitColor>();
            for (int i = 0; i < level.Colors.Count; i++)
            {
                RabbitColor color = level.Colors[i];
                string path = $"colors.Array.data[{i}]";
                if (!Enum.IsDefined(typeof(RabbitColor), color))
                    issues.Add(new LevelValidationIssue(LevelValidationCode.InvalidColor,
                        "정의되지 않은 달토끼 종류입니다.", path));
                if (!seen.Add(color))
                    issues.Add(new LevelValidationIssue(LevelValidationCode.DuplicateColor,
                        "사용 색이 중복되었습니다.", path));
            }
        }

        private static void ValidateBoard(BoardDefinition board, List<LevelValidationIssue> issues)
        {
            if (board == null)
            {
                issues.Add(new LevelValidationIssue(LevelValidationCode.MissingBoard, "보드가 없습니다.", "board"));
                return;
            }

            if (board.Rows != BoardDefinition.DefaultRows || board.Columns != BoardDefinition.DefaultColumns)
                issues.Add(new LevelValidationIssue(LevelValidationCode.UnsupportedBoardSize,
                    "현재 단계에서는 9×9 보드만 지원합니다.", "board"));
            if (board.Cells == null || board.Rows <= 0 || board.Columns <= 0 ||
                board.Cells.Count != (long)board.Rows * board.Columns)
                issues.Add(new LevelValidationIssue(LevelValidationCode.CellCountMismatch,
                    "칸 목록의 수가 보드 크기와 일치하지 않습니다.", "board.cells"));
        }

        private static void ValidatePlacements(LevelDefinition level, List<LevelValidationIssue> issues)
        {
            if (level.InitialBlocks == null)
            {
                issues.Add(new LevelValidationIssue(LevelValidationCode.MissingInitialBlocks,
                    "초기 배치 목록이 없습니다.", "initialBlocks"));
                return;
            }

            HashSet<BoardCoordinate> occupied = new HashSet<BoardCoordinate>();
            HashSet<RabbitColor> selectedColors = level.Colors == null
                ? new HashSet<RabbitColor>() : new HashSet<RabbitColor>(level.Colors);
            for (int i = 0; i < level.InitialBlocks.Count; i++)
            {
                InitialBlockDefinition block = level.InitialBlocks[i];
                BoardCoordinate coordinate = block.Coordinate;
                string path = $"initialBlocks.Array.data[{i}]";
                if (!occupied.Add(coordinate))
                    issues.Add(new LevelValidationIssue(LevelValidationCode.DuplicatePlacement,
                        "같은 칸에 두 개 이상의 초기 블록이 있습니다.", path + ".coordinate", coordinate));
                if (level.Board != null)
                {
                    if (!level.Board.Contains(coordinate))
                        issues.Add(new LevelValidationIssue(LevelValidationCode.CoordinateOutOfRange,
                            "배치가 보드 범위를 벗어났습니다.", path + ".coordinate", coordinate));
                    else if (!level.Board.TryGetCell(coordinate, out CellDefinition cell))
                        issues.Add(new LevelValidationIssue(LevelValidationCode.MissingCell,
                            "배치 좌표에 해당하는 칸 기록이 없습니다.", path + ".coordinate", coordinate));
                    else if (!cell.IsActive)
                        issues.Add(new LevelValidationIssue(LevelValidationCode.InactiveCellPlacement,
                            "비활성 칸의 배치입니다. 배치 기록은 보존됩니다.", path + ".coordinate", coordinate));
                }

                if (!Enum.IsDefined(typeof(InitialBlockKind), block.Kind))
                    issues.Add(new LevelValidationIssue(LevelValidationCode.InvalidBlockKind,
                        "정의되지 않은 초기 블록 유형입니다.", path + ".kind", coordinate));
                if (block.Kind == InitialBlockKind.Rocket && !Enum.IsDefined(typeof(RocketDirection), block.RocketDirection))
                    issues.Add(new LevelValidationIssue(LevelValidationCode.InvalidPlacementValue,
                        "정의되지 않은 로켓 방향입니다.", path + ".rocketDirection", coordinate));
                if (block.FixedColor.HasValue)
                {
                    RabbitColor color = block.FixedColor.Value;
                    if (!Enum.IsDefined(typeof(RabbitColor), color))
                        issues.Add(new LevelValidationIssue(LevelValidationCode.InvalidColor,
                            "정의되지 않은 고정 색입니다.", path + ".fixedColor", coordinate));
                    else if (!selectedColors.Contains(color))
                        issues.Add(new LevelValidationIssue(LevelValidationCode.UnusedFixedColor,
                            "이 레벨의 사용 색에 포함되지 않은 고정 색입니다.", path + ".fixedColor", coordinate));
                }
            }
        }

        private static void ValidateLayers(LevelDefinition level, List<LevelValidationIssue> issues)
        {
            if (level.SchemaVersion == 1)
            {
                bool unexpected = (level.Obstacles?.Count ?? 0) > 0 || (level.Covers?.Count ?? 0) > 0 || (level.Dust?.Count ?? 0) > 0;
                if (level.InitialBlocks != null)
                    foreach (InitialBlockDefinition block in level.InitialBlocks)
                        unexpected |= ((int)block.Kind >= 2 && (int)block.Kind <= 5) || block.RocketDirection != RocketDirection.Horizontal;
                if (unexpected) issues.Add(new LevelValidationIssue(LevelValidationCode.UnexpectedLegacyData,
                    "버전 1에 신규 배치 데이터가 있습니다. 자동 전환하지 않습니다.", "schemaVersion"));
            }
            if (level.Obstacles == null || level.Covers == null || level.Dust == null)
            {
                if (level.SchemaVersion != 1)
                    issues.Add(new LevelValidationIssue(LevelValidationCode.MissingPlacementList,
                        "장애물·덮개·먼지 목록이 누락되었습니다.", ""));
            }
            if (level.Obstacles != null)
                for (int i = 0; i < level.Obstacles.Count; i++)
                {
                    ObstaclePlacementDefinition obstacle = level.Obstacles[i];
                    string path = $"obstacles.Array.data[{i}]";
                    string valueError = LevelPlacementRules.ObstacleValueError(level, obstacle.Kind, obstacle.Durability, obstacle.Color, obstacle.RequiredCharge);
                    if (valueError != null) issues.Add(new LevelValidationIssue(LevelValidationCode.InvalidPlacementValue,
                        valueError, path, obstacle.Coordinate));
                    foreach (BoardCoordinate cell in LevelPlacementRules.Footprint(obstacle.Coordinate, LevelPlacementRules.Size(obstacle.Kind)))
                    {
                        ValidateLayerCell(level, cell, path + ".coordinate", issues);
                        if (LevelPlacementRules.Find(level, PlacementLayer.Obstacle, cell) == -2 ||
                            LevelPlacementRules.Find(level, PlacementLayer.Block, cell) != -1 ||
                            LevelPlacementRules.Find(level, PlacementLayer.Cover, cell) != -1)
                            issues.Add(new LevelValidationIssue(LevelValidationCode.PlacementConflict,
                                $"{LevelPlacementRules.Name(obstacle.Kind)} 기준 {obstacle.Coordinate}: 다른 점유 또는 덮개와 충돌합니다.", path, cell));
                    }
                }
            if (level.Covers != null)
                for (int i = 0; i < level.Covers.Count; i++)
                {
                    CoverPlacementDefinition cover = level.Covers[i];
                    string path = $"covers.Array.data[{i}]";
                    ValidateLayerCell(level, cover.Coordinate, path + ".coordinate", issues);
                    string valueError = LevelPlacementRules.CoverValueError(cover.Kind, cover.Durability);
                    if (valueError != null) issues.Add(new LevelValidationIssue(LevelValidationCode.InvalidPlacementValue,
                        valueError, path, cover.Coordinate));
                    string spaceError = LevelPlacementRules.CoverSpaceError(level, cover.Coordinate);
                    if (spaceError != null) issues.Add(new LevelValidationIssue(LevelValidationCode.InvalidCover,
                        spaceError, path, cover.Coordinate));
                    if (LevelPlacementRules.Find(level, PlacementLayer.Cover, cover.Coordinate) == -2)
                        issues.Add(new LevelValidationIssue(LevelValidationCode.DuplicatePlacement,
                            "덮개가 중복되었습니다.", path, cover.Coordinate));
                }
            if (level.Dust != null)
                for (int i = 0; i < level.Dust.Count; i++)
                {
                    DustPlacementDefinition dust = level.Dust[i];
                    string path = $"dust.Array.data[{i}]";
                    ValidateLayerCell(level, dust.Coordinate, path + ".coordinate", issues);
                    string dustError = LevelPlacementRules.DustValueError(dust.Durability);
                    if (dustError != null)
                        issues.Add(new LevelValidationIssue(LevelValidationCode.InvalidPlacementValue,
                            dustError, path + ".durability", dust.Coordinate));
                    if (LevelPlacementRules.Find(level, PlacementLayer.Dust, dust.Coordinate) == -2)
                        issues.Add(new LevelValidationIssue(LevelValidationCode.DuplicatePlacement,
                            "먼지가 중복되었습니다.", path, dust.Coordinate));
                }
        }

        private static void ValidateLayerCell(LevelDefinition level, BoardCoordinate coordinate, string path, List<LevelValidationIssue> issues)
        {
            string error = LevelPlacementRules.CellError(level, coordinate);
            if (error == null) return;
            LevelValidationCode code = level.Board == null || !level.Board.Contains(coordinate)
                ? LevelValidationCode.CoordinateOutOfRange
                : !level.Board.TryGetCell(coordinate, out _) ? LevelValidationCode.MissingCell : LevelValidationCode.InactiveCellPlacement;
            issues.Add(new LevelValidationIssue(code, error, path, coordinate));
        }
    }
}
