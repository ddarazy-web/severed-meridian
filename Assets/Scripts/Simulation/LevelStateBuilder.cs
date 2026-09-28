using System;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using Board;
using Levels;
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
        {
            if (definition == null) return null;
            using (SHA256 hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(JsonUtility.ToJson(definition)))).Replace("-", "");
        }

        public static LevelStateBuildResult Build(LevelDefinition definition, int seed)
        {
            if (definition != null && definition.SchemaVersion != 4)
                return new LevelStateBuildResult(null, new[] { new LevelValidationIssue(LevelValidationCode.UnsupportedSchemaVersion,
                    "초기 실행 구성은 v4만 지원합니다. 원본을 자동 변환하지 않습니다.", "schemaVersion") });
            System.Collections.Generic.List<LevelValidationIssue> issues = LevelDefinitionValidator.Validate(definition);
            if (issues.Count > 0) return new LevelStateBuildResult(null, issues.ToArray());
            LevelRuntimeState state = new LevelRuntimeState(definition, seed, Fingerprint(definition));
            for (int i = 0; i < state.Obstacles.Count; i++)
                foreach (BoardCoordinate coordinate in LevelPlacementRules.Footprint(state.Obstacles[i].Definition.Coordinate,
                    LevelPlacementRules.Size(state.Obstacles[i].Definition.Kind)))
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
