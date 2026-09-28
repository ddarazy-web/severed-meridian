using System;
using System.Collections.Generic;
using System.Linq;
using Board;

namespace Levels
{
    public enum PlacementLayer { Block, Obstacle, Cover, Dust }

    // 저장 목록만 원본으로 사용한다. 조회 결과와 2×2 점유는 별도로 직렬화하지 않는다.
    public static class LevelPlacementRules
    {
        public static int Size(ObstacleKind kind) =>
            kind == ObstacleKind.Appliance || kind == ObstacleKind.Generator ? 2 : 1;

        public static IEnumerable<BoardCoordinate> Footprint(BoardCoordinate origin, int size)
        {
            for (int row = 0; row < size; row++)
                for (int column = 0; column < size; column++)
                {
                    long r = (long)origin.Row + row;
                    long c = (long)origin.Column + column;
                    if (r <= int.MaxValue && c <= int.MaxValue)
                        yield return new BoardCoordinate((int)r, (int)c);
                }
        }

        public static int MaxDurability(ObstacleKind kind) => kind switch
        {
            ObstacleKind.Crate => 6,
            ObstacleKind.Scrap => 5,
            ObstacleKind.Safe => 5,
            ObstacleKind.ColorLock => 3,
            ObstacleKind.Appliance => 9,
            _ => 0
        };

        public static bool IsNormal(InitialBlockKind kind) =>
            kind == InitialBlockKind.RandomNormal || kind == InitialBlockKind.FixedNormal;

        public static string Name(ObstacleKind kind) => kind switch
        {
            ObstacleKind.Crate => "나무상자", ObstacleKind.Scrap => "고철 뭉치",
            ObstacleKind.Safe => "잠긴 고물 금고", ObstacleKind.ColorLock => "색깔 자물쇠",
            ObstacleKind.Appliance => "대형 폐가전", ObstacleKind.Generator => "고장 난 발전기",
            _ => "잘못된 장애물"
        };

        public static string Name(InitialBlockKind kind) => kind switch
        {
            InitialBlockKind.RandomNormal => "무작위 ?", InitialBlockKind.FixedNormal => "고정 일반",
            InitialBlockKind.Rocket => "청소로켓", InitialBlockKind.Bomb => "달폭탄",
            InitialBlockKind.Drone => "수거드론", InitialBlockKind.Magnet => "무지개 자석",
            _ => "잘못된 블록"
        };

        // -1은 없음, -2는 중복이다. 알 수 없는 종류도 원래 기준 칸의 점유를 보존한다.
        public static int Find(LevelDefinition level, PlacementLayer layer, BoardCoordinate coordinate)
        {
            int found = -1;
            if (level == null) return found;
            int count = layer switch
            {
                PlacementLayer.Block => level.InitialBlocks?.Count ?? 0,
                PlacementLayer.Obstacle => level.Obstacles?.Count ?? 0,
                PlacementLayer.Cover => level.Covers?.Count ?? 0,
                PlacementLayer.Dust => level.Dust?.Count ?? 0,
                _ => 0
            };
            for (int i = 0; i < count; i++)
            {
                bool occupies = layer switch
                {
                    PlacementLayer.Block => level.InitialBlocks[i].Coordinate.Equals(coordinate),
                    PlacementLayer.Obstacle => Footprint(level.Obstacles[i].Coordinate, Size(level.Obstacles[i].Kind)).Contains(coordinate),
                    PlacementLayer.Cover => level.Covers[i].Coordinate.Equals(coordinate),
                    PlacementLayer.Dust => level.Dust[i].Coordinate.Equals(coordinate),
                    _ => false
                };
                if (!occupies) continue;
                if (found >= 0) return -2;
                found = i;
            }
            return found;
        }

        public static string CellError(LevelDefinition level, BoardCoordinate coordinate)
        {
            if (level?.Board == null || !level.Board.Contains(coordinate)) return "보드 범위 밖입니다.";
            if (!level.Board.TryGetCell(coordinate, out CellDefinition cell)) return "칸 기록이 없습니다.";
            return cell.IsActive ? null : "비활성 칸입니다.";
        }

        public static string ObstacleSpaceError(LevelDefinition level, BoardCoordinate origin, ObstacleKind kind, int self = -1)
        {
            if (level.Flow?.Walls != null && level.Flow.Walls.Any(wall => LevelFlowRules.InternalWall(wall, origin, Size(kind))))
                return "2×2 본체 내부에 고철 벽이 있습니다.";
            foreach (BoardCoordinate coordinate in Footprint(origin, Size(kind)))
            {
                string error = CellError(level, coordinate);
                if (error != null) return coordinate + " " + error;
                int obstacle = Find(level, PlacementLayer.Obstacle, coordinate);
                if (obstacle == -2 || (obstacle >= 0 && obstacle != self)) return coordinate + " 다른 장애물이 점유합니다.";
                if (Find(level, PlacementLayer.Block, coordinate) != -1) return coordinate + " 블록이 점유합니다.";
                if (LevelSupplyRules.HasRecovery(level, coordinate)) return coordinate + " 회수 부품이 점유합니다.";
                if (Find(level, PlacementLayer.Cover, coordinate) != -1) return coordinate + " 덮개와 겹칠 수 없습니다.";
            }
            return null;
        }

        public static string BlockSpaceError(LevelDefinition level, BoardCoordinate coordinate)
        {
            string error = CellError(level, coordinate);
            if (error != null) return error;
            if (LevelSupplyRules.HasRecovery(level, coordinate)) return "회수 부품이 점유합니다.";
            return Find(level, PlacementLayer.Obstacle, coordinate) == -1 ? null : "장애물이 점유합니다.";
        }

        public static string CoverSpaceError(LevelDefinition level, BoardCoordinate coordinate)
        {
            string error = BlockSpaceError(level, coordinate);
            if (error != null) return error;
            int block = Find(level, PlacementLayer.Block, coordinate);
            return block >= 0 && Enum.IsDefined(typeof(InitialBlockKind), level.InitialBlocks[block].Kind)
                ? null : "내부 일반/파워 블록이 없거나 중복·잘못된 유형입니다.";
        }

        public static string ObstacleValueError(LevelDefinition level, ObstacleKind kind, int durability, RabbitColor color, int charge)
        {
            if (!Enum.IsDefined(typeof(ObstacleKind), kind)) return "정의되지 않은 장애물입니다.";
            if (kind == ObstacleKind.Generator)
                return charge >= 3 && charge <= 5 ? null : "필요 충전량은 3~5입니다.";
            if (durability < 1 || durability > MaxDurability(kind)) return $"내구도는 1~{MaxDurability(kind)}입니다.";
            if (kind == ObstacleKind.ColorLock &&
                (!Enum.IsDefined(typeof(RabbitColor), color) || level.Colors == null || !level.Colors.Contains(color)))
                return "자물쇠 색은 레벨 사용 색 중 하나여야 합니다.";
            return null;
        }

        public static string CoverValueError(CoverKind kind, int durability)
        {
            if (!Enum.IsDefined(typeof(CoverKind), kind)) return "정의되지 않은 덮개입니다.";
            int max = kind == CoverKind.Web ? 3 : 1;
            return durability >= 1 && durability <= max ? null : $"덮개 내구도는 1~{max}입니다.";
        }
    }
}
