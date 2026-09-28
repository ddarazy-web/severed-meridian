using System;
using System.Collections.Generic;
using System.Linq;
using Board;

namespace Levels
{
    public static class LevelSupplyRules
    {
        public static bool HasNewData(LevelDefinition level) => (level.RecoveryParts?.Count ?? 0) > 0 ||
            (level.Missions?.Count ?? 0) > 0 || (level.Supply?.Sources?.Count ?? 0) > 0 ||
            (level.Supply != null && (level.Supply.ScrapTarget != 0 || level.Supply.ScrapLimit != 0 ||
                level.Supply.RecoveryTarget != 0 || level.Supply.ScrapDurability != 1));

        public static string Name(SupplyKind kind) => kind switch
        {
            SupplyKind.RandomNormal => "무작위 달토끼", SupplyKind.FixedNormal => "고정 달토끼",
            SupplyKind.Rocket => "청소로켓", SupplyKind.Bomb => "달폭탄", SupplyKind.Drone => "수거드론",
            SupplyKind.Magnet => "무지개 자석", SupplyKind.Scrap => "고철 뭉치", SupplyKind.Recovery => "회수 부품",
            _ => "잘못된 공급 종류"
        };

        public static int FindSource(LevelDefinition level, BoardCoordinate cell)
        {
            int found = -1;
            if (level?.Supply?.Sources == null) return found;
            for (int i = 0; i < level.Supply.Sources.Count; i++)
                if (level.Supply.Sources[i].Coordinate.Equals(cell))
                { if (found >= 0) return -2; found = i; }
            return found;
        }

        public static bool HasRecovery(LevelDefinition level, BoardCoordinate cell) => level?.RecoveryParts?.Contains(cell) == true;

        public static string SourceCellError(LevelDefinition level, BoardCoordinate cell)
        {
            string error = LevelPlacementRules.CellError(level, cell);
            if (error != null) return error;
            return level.Flow?.Arrivals?.Contains(cell) == true ? "생성구와 도착 바닥은 겹칠 수 없습니다." : null;
        }

        public static string RecoverySpaceError(LevelDefinition level, BoardCoordinate cell)
        {
            string error = LevelPlacementRules.CellError(level, cell);
            if (error != null) return error;
            if (LevelPlacementRules.Find(level, PlacementLayer.Block, cell) != -1 ||
                LevelPlacementRules.Find(level, PlacementLayer.Obstacle, cell) != -1 ||
                LevelPlacementRules.Find(level, PlacementLayer.Cover, cell) != -1)
                return "회수 부품은 블록·장애물·덮개와 겹칠 수 없습니다.";
            return null;
        }

        public static string ItemError(LevelDefinition level, SupplyItem item)
        {
            if (!Enum.IsDefined(typeof(SupplyKind), item.Kind)) return "이동 가능한 공급 종류만 지정하세요.";
            if (item.Count <= 0) return "공급 수량은 양수여야 합니다.";
            if (item.Kind == SupplyKind.FixedNormal && (!Enum.IsDefined(typeof(RabbitColor), item.Color) ||
                level.Colors == null || !level.Colors.Contains(item.Color))) return "레벨에서 사용하는 달토끼 색을 선택하세요.";
            if (item.Kind == SupplyKind.Rocket && !Enum.IsDefined(typeof(RocketDirection), item.Direction)) return "로켓 방향이 잘못되었습니다.";
            if (item.Kind == SupplyKind.Scrap && (item.Durability < 1 || item.Durability > LevelPlacementRules.MaxDurability(ObstacleKind.Scrap)))
                return "고철 내구도는 1~5입니다.";
            return null;
        }

        public static long FixedCount(LevelDefinition level, SupplyKind kind) => level.Supply?.Sources?
            .Where(source => source.Mode == SupplyMode.Fixed && source.Items != null)
            .SelectMany(source => source.Items).Where(item => item.Kind == kind && ItemError(level, item) == null)
            .Sum(item => (long)item.Count) ?? 0;

        public static bool HasMode(LevelDefinition level, SupplyMode mode) => level.Supply?.Sources?.Any(source => source.Mode == mode) == true;

        public static void Validate(LevelDefinition level, List<LevelValidationIssue> issues)
        {
            if (level.SchemaVersion < 4)
            {
                if (HasNewData(level)) issues.Add(new LevelValidationIssue(LevelValidationCode.UnexpectedLegacyData,
                    "구버전에 예상 밖 공급·회수·미션 데이터가 있습니다.", "schemaVersion"));
                return;
            }
            if (level.Supply?.Sources == null || level.RecoveryParts == null)
            {
                issues.Add(new LevelValidationIssue(LevelValidationCode.InvalidSupply, "공급/회수 목록이 누락되었습니다.", "supply"));
                return;
            }
            HashSet<BoardCoordinate> sources = new HashSet<BoardCoordinate>();
            for (int i = 0; i < level.Supply.Sources.Count; i++)
            {
                SupplySourceDefinition source = level.Supply.Sources[i];
                string path = $"supply.sources.Array.data[{i}]";
                string error = SourceCellError(level, source.Coordinate);
                if (!sources.Add(source.Coordinate)) error = "한 칸에 생성구가 중복되었습니다.";
                if (error != null) issues.Add(new LevelValidationIssue(LevelValidationCode.InvalidSupply, error, path, source.Coordinate));
                if (!Enum.IsDefined(typeof(SupplyMode), source.Mode) || !Enum.IsDefined(typeof(SupplyExhaustion), source.Exhaustion))
                    issues.Add(new LevelValidationIssue(LevelValidationCode.InvalidSupply, "공급 방식/소진 정책이 잘못되었습니다.", path, source.Coordinate));
                if (source.Items == null)
                    issues.Add(new LevelValidationIssue(LevelValidationCode.InvalidSupply, "고정 공급 목록이 누락되었습니다.", path + ".items", source.Coordinate));
                else for (int j = 0; j < source.Items.Count; j++)
                {
                    error = ItemError(level, source.Items[j]);
                    if (error != null) issues.Add(new LevelValidationIssue(LevelValidationCode.InvalidSupply, error, path + $".items.Array.data[{j}]", source.Coordinate));
                }
                if (source.Mode != SupplyMode.Fixed && (source.Items?.Count ?? 0) > 0)
                    issues.Add(new LevelValidationIssue(LevelValidationCode.SupplyConflict, "고정 목록이 남아 있습니다. 명시적으로 비우거나 고정 공급 방식을 사용하세요.", path, source.Coordinate));
            }
            foreach ((SupplyMode mode, SupplyKind kind) in new[] { (SupplyMode.MaintainScrap, SupplyKind.Scrap), (SupplyMode.MaintainRecovery, SupplyKind.Recovery) })
                if (HasMode(level, mode) && level.Supply.Sources.Any(source => source.Mode == SupplyMode.Fixed && source.Items?.Any(item => item.Kind == kind) == true))
                    issues.Add(new LevelValidationIssue(LevelValidationCode.SupplyConflict, Name(kind) + " 고정 공급과 개수 유지는 같은 레벨에서 함께 사용할 수 없습니다.", "supply.sources"));
            bool scrap = HasMode(level, SupplyMode.MaintainScrap), recovery = HasMode(level, SupplyMode.MaintainRecovery);
            if (level.Supply.ScrapTarget < 0 || level.Supply.ScrapLimit < 0 || level.Supply.ScrapDurability < 1 || level.Supply.ScrapDurability > 5 ||
                (scrap && level.Supply.ScrapTarget == 0) || (!scrap && (level.Supply.ScrapTarget > 0 || level.Supply.ScrapLimit > 0)))
                issues.Add(new LevelValidationIssue(LevelValidationCode.InvalidSupply, "고철 유지 목표·추가 한도·내구도와 담당 생성구를 확인하세요.", "supply.scrapTarget"));
            if (level.Supply.RecoveryTarget < 0 || (recovery && level.Supply.RecoveryTarget == 0) || (!recovery && level.Supply.RecoveryTarget > 0) ||
                (recovery && level.Missions?.Any(mission => mission.Kind == MissionKind.Recovery && mission.Count > 0) != true))
                issues.Add(new LevelValidationIssue(LevelValidationCode.InvalidSupply, "회수 유지 목표·담당 생성구·양수 회수 미션이 필요합니다.", "supply.recoveryTarget"));
            HashSet<BoardCoordinate> parts = new HashSet<BoardCoordinate>();
            for (int i = 0; i < level.RecoveryParts.Count; i++)
            {
                BoardCoordinate cell = level.RecoveryParts[i];
                string error = RecoverySpaceError(level, cell);
                if (!parts.Add(cell)) error = "회수 부품이 중복 배치되었습니다.";
                if (error != null) issues.Add(new LevelValidationIssue(LevelValidationCode.InvalidRecovery, error, $"recoveryParts.Array.data[{i}]", cell));
            }
        }
    }
}
