using System;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using Board;
using Levels;
using Elements;
using UnityEngine;

namespace Simulation
{
    public sealed class LevelStateBuildResult
    {
        public LevelRuntimeState State { get; }
        public ReadOnlyCollection<LevelValidationIssue> Issues { get; }
        // 구성 완료는 시작 조건 또는 플레이 가능 판정이 아니다.
        public bool IsBuilt => State != null;
        internal LevelStateBuildResult(LevelRuntimeState state, LevelValidationIssue[] issues)
        { State = state; Issues = Array.AsReadOnly(issues); }
    }

    public static class LevelStateBuilder
    {
        public static string Fingerprint(LevelDefinition definition)
            => Fingerprint(definition, definition?.SchemaVersion == 5 ? definition.CreateElementCatalog() : null);

        private static string Fingerprint(LevelDefinition definition, ElementCatalog catalog)
        {
            if (definition == null) return null;
            if (definition.HasTutorial)
            {
                using (SHA256 hash = SHA256.Create())
                    return BitConverter.ToString(hash.ComputeHash(LevelPackCodec.EncodeWithTutorial(new[] { definition }, catalog ?? definition.CreateElementCatalog()))).Replace("-", "");
            }
            if (definition.SchemaVersion == 5)
            {
                using (SHA256 hash = SHA256.Create())
                    return BitConverter.ToString(hash.ComputeHash(LevelPackCodec.Encode(new[] { definition }, catalog))).Replace("-", "");
            }
            string json = JsonUtility.ToJson(definition);
            // v4 영구 지문은 기존 필드 집합의 원문을 유지한다. 새 제작 필드는 끝에 추가한다.
            if (definition.SchemaVersion == 4)
            {
                int appended = json.LastIndexOf(",\"elements\":", StringComparison.Ordinal);
                if (appended >= 0) json = json.Substring(0, appended) + "}";
            }
            using (SHA256 hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(json))).Replace("-", "");
        }

        public static LevelStateBuildResult Build(LevelDefinition definition, int seed, ElementCatalog catalog)
        {
            if (definition == null || definition.SchemaVersion == 4) return Build(definition, seed);
            if (definition.SchemaVersion != 5)
                return new LevelStateBuildResult(null, new[] { new LevelValidationIssue(LevelValidationCode.UnsupportedSchemaVersion,
                    "ID 실행 구성은 v4/v5만 지원합니다.", "schemaVersion") });
            if (catalog == null)
                return new LevelStateBuildResult(null, new[] { new LevelValidationIssue(LevelValidationCode.InvalidPlacementValue,
                    "ID 배치의 정의 카탈로그가 없습니다.", "elementCatalog") });
            using (ElementLevelLayout layout = new ElementLevelLayout(definition, catalog))
            {
                System.Collections.Generic.List<LevelValidationIssue> issues = LevelDefinitionValidator.ValidateElements(definition, layout, catalog);
                if (issues.Count > 0) return new LevelStateBuildResult(null, issues.ToArray());
                LevelRuntimeState state = BuildValidated(layout.Level, seed, Fingerprint(definition, catalog), layout.Bodies, 5).State;
                state.ElementCatalog = catalog;
                foreach (RuntimeSource source in state.Supply.Sources)
                {
                    source.ItemDefinitions = Array.AsReadOnly((ElementDefinition[])layout.Supply.Items[source.Coordinate].Clone());
                    if (layout.Supply.Random.TryGetValue(source.Coordinate, out ElementDefinition random)) source.RandomDefinition = random;
                }
                state.Supply.ScrapDefinition = layout.Supply.Scrap; state.Supply.RecoveryDefinition = layout.Supply.Recovery;
                foreach (RuntimeCell cell in state.Cells) cell.ContentElement = LegacyElementDefinitions.GetContent(cell.Content, catalog);
                foreach (System.Collections.Generic.KeyValuePair<BoardCoordinate, ElementDefinition> item in layout.Covers)
                    state.CellAt(item.Key).CoverElement = item.Value;
                foreach (System.Collections.Generic.KeyValuePair<BoardCoordinate, ElementDefinition> item in layout.Floors)
                    state.CellAt(item.Key).DustElement = item.Value;
                foreach (System.Collections.Generic.KeyValuePair<BoardCoordinate, ElementDefinition> item in layout.Contents)
                    state.CellAt(item.Key).ContentElement = item.Value;
                foreach (RuntimeMission mission in state.Missions)
                    if (mission.Definition.Kind == MissionKind.Mold) mission.Target = (int)layout.MissionSupply(mission.Definition).Initial;
                return new LevelStateBuildResult(state, Array.Empty<LevelValidationIssue>());
            }
        }

        public static LevelStateBuildResult Build(LevelDefinition definition, int seed)
        {
            if (definition != null && definition.SchemaVersion == 5)
            {
                try { return Build(definition, seed, definition.CreateElementCatalog()); }
                catch (ArgumentException error)
                {
                    return new LevelStateBuildResult(null, new[] { new LevelValidationIssue(LevelValidationCode.InvalidPlacementValue,
                        error.Message, "elementCatalog") });
                }
            }
            if (definition != null && definition.SchemaVersion != 4)
                return new LevelStateBuildResult(null, new[] { new LevelValidationIssue(LevelValidationCode.UnsupportedSchemaVersion,
                    "초기 실행 구성은 v4만 지원합니다. 원본을 자동 변환하지 않습니다.", "schemaVersion") });
            System.Collections.Generic.List<LevelValidationIssue> issues = LevelDefinitionValidator.Validate(definition);
            if (issues.Count > 0) return new LevelStateBuildResult(null, issues.ToArray());
            return BuildValidated(definition, seed, Fingerprint(definition));
        }

        private static LevelStateBuildResult BuildValidated(LevelDefinition definition, int seed, string fingerprint,
            System.Collections.Generic.IReadOnlyDictionary<string, ElementDefinition> selected = null, int? schema = null)
        {
            LevelRuntimeState state = new LevelRuntimeState(definition, seed, fingerprint, selected, schema);
            for (int i = 0; i < state.Obstacles.Count; i++)
                foreach (BoardCoordinate coordinate in LevelPlacementRules.Footprint(state.Obstacles[i].Definition.Coordinate,
                    state.Obstacles[i].Element.ChargePlacement?.Size ?? state.Obstacles[i].Element.RequirePlacement().Size))
                { RuntimeCell cell = state.CellAt(coordinate); cell.Content = RuntimeContent.Obstacle; cell.ObstacleIndex = i; }
            foreach (BoardCoordinate coordinate in definition.RecoveryParts) state.CellAt(coordinate).Content = RuntimeContent.Recovery;
            foreach (InitialBlockDefinition block in state.InitialBlocks)
            {
                RuntimeCell cell = state.CellAt(block.Coordinate);
                cell.Content = block.Kind switch
                {
                    InitialBlockKind.RandomNormal => RuntimeContent.Normal, InitialBlockKind.FixedNormal => RuntimeContent.Normal,
                    InitialBlockKind.Rocket => RuntimeContent.Rocket, InitialBlockKind.Bomb => RuntimeContent.Bomb,
                    InitialBlockKind.Drone => RuntimeContent.Drone, InitialBlockKind.Magnet => RuntimeContent.Magnet,
                    _ => throw new InvalidOperationException("검증되지 않은 초기 블록 종류입니다.")
                };
                cell.Color = block.FixedColor;
                if (block.Kind == InitialBlockKind.Rocket) cell.RocketDirection = block.RocketDirection;
            }
            // 후보의 직접 구성이다. 플레이 중 낙하/공급이나 고정 공급 목록 소비에 사용하지 않는다.
            foreach (RuntimeCell cell in state.Cells)
                if (cell.IsActive && (cell.Content == RuntimeContent.Empty || (cell.Content == RuntimeContent.Normal && !cell.Color.HasValue)))
                { cell.Content = RuntimeContent.Normal; cell.Color = state.Colors[state.Random.Next(state.Colors.Count)]; }
            foreach (CoverPlacementDefinition cover in definition.Covers)
            { RuntimeCell cell = state.CellAt(cover.Coordinate); cell.Cover = cover.Kind; cell.CoverDurability = cover.Durability; }
            foreach (DustPlacementDefinition dust in definition.Dust) state.CellAt(dust.Coordinate).DustDurability = dust.Durability;
            foreach (GravityCell gravity in state.Flow.Gravity) state.CellAt(gravity.Coordinate).Gravity = gravity.Direction;
            return new LevelStateBuildResult(state, Array.Empty<LevelValidationIssue>());
        }
    }
}
