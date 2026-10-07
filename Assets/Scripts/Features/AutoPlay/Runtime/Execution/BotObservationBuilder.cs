using System;
using System.Collections.Generic;
using System.Linq;
using Board;
using Levels;
using Simulation;

namespace AutoPlay
{
    /// <summary>신뢰 경계: 실행 상태를 읽되 허용된 값만 새 객체로 복사한다. 전략에서 호출하지 않는다.</summary>
    public static class BotObservationBuilder
    {
        public const string Version = "bot-observation-v1";

        /// <summary>입력 가능한 안정 상태에서 공개 관찰을 생성한다. 조회는 난수를 소비하지 않는다.</summary>
        /// <param name="executor">관찰 시점을 검증할 실행기.</param><returns>원본과 독립된 관찰.</returns>
        public static BotObservation Capture(BoardActionExecutor executor)
        {
            if (executor == null) throw new ArgumentNullException(nameof(executor));
            if (executor.Phase != BoardActionPhase.Ready || executor.Outcome != null)
                throw new InvalidOperationException("봇은 미종료 판의 입력 가능한 안정 상태만 관찰합니다.");
            LevelRuntimeState state = executor.State;
            // 내부 본체 인덱스는 생성 이력에 따라 달라진다. 공개된 좌표에서 키를 다시 만들어
            // 같은 화면인데 과거 생성·제거 이력만 다를 때 관찰이 달라지는 일을 막는다.
            Dictionary<int, BoardCoordinate[]> occupied = state.Cells
                .Where(cell => cell.IsActive && cell.Cover != CoverKind.Mold && cell.ObstacleIndex.HasValue)
                .GroupBy(cell => cell.ObstacleIndex.Value)
                .ToDictionary(group => group.Key, group => group.Select(cell => cell.Coordinate)
                    .OrderBy(cell => cell.Row).ThenBy(cell => cell.Column).ToArray());
            Dictionary<int, int> keys = occupied.ToDictionary(pair => pair.Key,
                pair => pair.Value[0].Row * state.Columns + pair.Value[0].Column);
            List<BotBody> bodies = new List<BotBody>();
            foreach (var pair in occupied.OrderBy(pair => keys[pair.Key]))
            {
                RuntimeObstacle body = state.Obstacles[pair.Key];
                Elements.ElementDefinition definition = body.Element;
                Elements.ElementDamageSourcePolicy sources = definition.RequireDamageSourcePolicy();
                bool charge = definition.RequireReactionBehavior() == Elements.ElementReactionBehavior.GeneratorCharge;
                bool requiresColor = definition.ColorMatchPolicy?.RequiresMatchingColor == true;
                IEnumerable<int> targets = charge
                    ? GeneratorRules.ActiveConnections(state).Where(connection => connection.GeneratorId == body.Definition.Id)
                        .Select(connection => GeneratorRules.Find(state, connection.TargetId)).Where(keys.ContainsKey)
                        .Select(index => keys[index]).Distinct().OrderBy(key => key)
                    : Enumerable.Empty<int>();
                bodies.Add(new BotBody(keys[pair.Key], body.Definition.Kind, body.Durability,
                    requiresColor ? body.Definition.Color : (RabbitColor?)null,
                    charge ? body.Charge : 0, charge ? body.Definition.RequiredCharge : 0, pair.Value, targets,
                    sources.AdjacentMatch, sources.Power, sources.MagnetAdjacent, sources.Hammer, requiresColor,
                    definition.DamageAggregationPolicy?.PerHitCell == true, charge,
                    charge ? definition.RequireChargePlacement().ChargePerHit : 0,
                    charge ? definition.RequireChargePlacement().Size : definition.RequirePlacement().Size,
                    definition.RemovalMissionProfile?.Kind));
            }
            BotCell[] cells = state.Cells.Select(cell =>
            {
                bool hidden = cell.Cover == CoverKind.Mold;
                Elements.ElementDefinition cover = cell.Cover.HasValue
                    ? cell.CoverElement ?? Elements.LegacyElementDefinitions.Get(cell.Cover.Value) : null;
                Elements.ElementLayerProfile coverLayer = cover?.RequireLayer();
                Elements.ElementLayerProfile dustLayer = cell.DustDurability > 0
                    ? (cell.DustElement ?? Elements.LegacyElementDefinitions.GetDust()).RequireLayer() : null;
                BotContent content = hidden ? BotContent.Unknown : cell.Content switch
                {
                    RuntimeContent.Normal => BotContent.Normal, RuntimeContent.Rocket => BotContent.Rocket,
                    RuntimeContent.Bomb => BotContent.Bomb, RuntimeContent.Drone => BotContent.Drone,
                    RuntimeContent.Magnet => BotContent.Magnet, RuntimeContent.Obstacle => BotContent.Obstacle,
                    RuntimeContent.Recovery => BotContent.Recovery, _ => BotContent.Empty
                };
                return new BotCell(cell.Coordinate, cell.IsActive, content,
                    !hidden && cell.Content == RuntimeContent.Normal ? cell.Color : null,
                    !hidden && cell.Content == RuntimeContent.Rocket ? cell.RocketDirection : null,
                    cell.Cover, cell.CoverDurability, cell.DustDurability,
                    !hidden && cell.ObstacleIndex.HasValue && keys.TryGetValue(cell.ObstacleIndex.Value, out int key) ? key : (int?)null,
                    coverLayer?.Behavior == Elements.ElementLayerBehavior.CoverDurability,
                    coverLayer?.Damage ?? 0, coverLayer?.Mission, cover?.Turn != null,
                    cover?.Turn?.InitialDurability ?? 0, dustLayer?.Damage ?? 0, dustLayer?.Mission);
            }).ToArray();
            // ActionQuery는 벽·이동 제한·일반 색으로 유효성을 판정한다.
            // MatchQuery/HasMagnetTarget은 곰팡이 내부를 제외한다. 원본 ActionCandidate 대신
            // 공개 좌표와 패턴만 복사하며, 숨은 정보 변경 쌍의 비교 검사로 계약을 고정한다.
            BotAction[] actions = ActionQuery.Find(state).Select(action => new BotAction(action.Kind switch
            {
                QueryActionKind.SwapPower => BotActionKind.PowerSwap,
                QueryActionKind.SwapCombination => BotActionKind.CombinationSwap,
                QueryActionKind.Activate => BotActionKind.Activate, _ => BotActionKind.MatchSwap
            }, action.First, action.Second, action.Matches.Select(match => new BotMatch((BotMatchKind)match.Kind, match.Color, match.Cells)))).ToArray();
            return new BotObservation(state.Rows, state.Columns, state.MovesRemaining, cells, bodies,
                state.Missions.Select(mission => new BotMission(mission.Definition.Kind,
                    mission.Definition.Kind == MissionKind.Color ? mission.Definition.Color : (RabbitColor?)null, mission.Remaining)),
                actions, state.Flow.Walls, state.Flow.Arrivals, state.Flow.Portals);
        }
    }
}
