using System.Linq;
using Board;

namespace Levels
{
    // 직렬화 필드 순서와 버전 4를 유지하며 구형 보드의 마지막 행·열만 제거한다.
    internal static class LevelBoardSizeMigration
    {
        internal static void Crop(PackedLevel data)
        {
            if (data == null || data.SchemaVersion != LevelDefinition.CurrentSchemaVersion ||
                data.Board == null || !data.Board.CropLegacyBoard()) return;
            BoardDefinition board = data.Board;
            data.InitialBlocks?.RemoveAll(item => !board.Contains(item.Coordinate));
            string[] removedIds = data.Obstacles?.Where(item => !LevelPlacementRules.Footprint(item.Coordinate,
                LevelPlacementRules.Size(item.Kind)).All(board.Contains)).Select(item => item.Id).ToArray() ?? System.Array.Empty<string>();
            data.Obstacles?.RemoveAll(item => !LevelPlacementRules.Footprint(item.Coordinate,
                LevelPlacementRules.Size(item.Kind)).All(board.Contains));
            data.Covers?.RemoveAll(item => !board.Contains(item.Coordinate));
            data.Dust?.RemoveAll(item => !board.Contains(item.Coordinate));
            data.RecoveryParts?.RemoveAll(item => !board.Contains(item));
            data.Flow?.CropTo(board);
            data.Supply?.CropTo(board);
            data.Connections?.RemoveAll(item =>
                removedIds.Contains(item.GeneratorId) || removedIds.Contains(item.TargetId) ||
                (item.Vertices != null && item.Vertices.Any(vertex => vertex.Row < 0 || vertex.Column < 0 ||
                    vertex.Row > board.Rows || vertex.Column > board.Columns)));
        }
    }
}
