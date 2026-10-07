using System;
using System.Collections.Generic;
using System.Linq;
using Board;
using Levels;

namespace AutoPlay
{
    /// <summary>직접 보이는 미션 기여의 보수적 점수. 게임 상태를 변경하거나 피해 실행을 대신하지 않는다.</summary>
    internal static class BotMissionEvaluation
    {
        /// <summary>피해 조건별로 기여를 분리한다. 완료 8점, 내구도 진행 1점은 전략 비교용 값이다.</summary>
        /// <param name="board">공개 판.</param><param name="action">평가 조작.</param><param name="affected">확인 가능한 직접 범위.</param>
        /// <param name="matching">일반 매칭 여부.</param><param name="magnet">색 지정 자석 여부.</param><param name="color">지정 색.</param>
        /// <returns>공개 정보로 계산한 미션 진행 가치.</returns>
        internal static int Score(BotObservation board, BotAction action, HashSet<BoardCoordinate> affected,
            bool matching, bool magnet, RabbitColor? color)
        {
            Dictionary<(MissionKind, RabbitColor?), int> progress = new Dictionary<(MissionKind, RabbitColor?), int>();
            Dictionary<int, HashSet<BoardCoordinate>> bodyHits = new Dictionary<int, HashSet<BoardCoordinate>>();
            HashSet<BoardCoordinate> covers = new HashSet<BoardCoordinate>();
            HashSet<int> completedBodies = new HashSet<int>();
            int partial = 0;
            foreach (BoardCoordinate at in affected)
            {
                BotCell occupant = BasicBotStrategy.AfterSwap(board, action, at);
                if (occupant.Cover.HasValue)
                {
                    Cover(occupant, at);
                    // 거미줄 블록은 소비되지 않지만 매칭 참여 색으로 인접 피해를 준다.
                    // ApplyLayers/PowerEffectResolution도 IsConsumed와 인접 피해를 구분한다.
                    if (!matching || occupant.Cover != CoverKind.Web) continue;
                }
                if (occupant.Content == BotContent.Normal && !occupant.Cover.HasValue)
                {
                    Add(MissionKind.Color, occupant.Color);
                    BotCell floor = board.CellAt(at);
                    if (floor.DustDurability > 0 && Has(floor.DustMission))
                    {
                        if (floor.DustDurability <= floor.DustDamage) Add(floor.DustMission.Value);
                        else partial += Math.Min(floor.DustDurability, floor.DustDamage);
                    }
                }
                if (!matching && !magnet && occupant.BodyKey.HasValue) Body(occupant, at, true, null);
                // 파워의 일반 제거에는 인접 피해가 없다. 일반 매칭과 자석의 색 자물쇠 예외만 본다.
                if (!(matching || magnet) || occupant.Content != BotContent.Normal) continue;
                foreach (BotCell location in board.Cells.Where(cell => cell.IsActive && new BoardEdge(at, cell.Coordinate).IsAdjacent &&
                    !board.Walls.Contains(new BoardEdge(at, cell.Coordinate))))
                {
                    // 이동 가능한 고철도 교환 후 위치로 판정한다. BotCell.Coordinate는
                    // 교환 전 좌표이므로 실제 피해 위치는 location에서 따로 전달한다.
                    BotCell target = BasicBotStrategy.AfterSwap(board, action, location.Coordinate);
                    if (!magnet && target.Cover == CoverKind.Mold) Cover(target, location.Coordinate);
                    else if (target.BodyKey.HasValue)
                    {
                        BotBody body = board.Bodies.First(item => item.Key == target.BodyKey.Value);
                        if (!magnet || body.AcceptsMagnetAdjacent && !body.IsCharge) Body(target, location.Coordinate, false, color ?? occupant.Color);
                    }
                }
            }
            foreach (var hit in bodyHits)
            {
                BotBody body = board.Bodies.First(item => item.Key == hit.Key);
                if (body.IsCharge)
                {
                    BotBody[] targets = board.Bodies.Where(item => body.ConnectedTargets.Contains(item.Key)).ToArray();
                    if (body.Charge + body.ChargePerHit >= body.RequiredCharge) foreach (BotBody target in targets) completedBodies.Add(target.Key);
                    else if (targets.Any(target => Has(target.RemovalMission))) partial++;
                    continue;
                }
                MissionKind? kind = body.RemovalMission;
                if (!Has(kind)) continue;
                int damage = body.PerHitCell ? Math.Min(body.LogicalSize * body.LogicalSize, hit.Value.Count) : 1;
                if (damage >= body.Durability) completedBodies.Add(body.Key); else partial += damage;
            }
            // 직접 파괴와 발전기 연결 해제가 같은 본체를 가리켜도 완료량은 한 개다.
            foreach (BotBody body in board.Bodies.Where(body => completedBodies.Contains(body.Key) && body.RemovalMission.HasValue)) Add(body.RemovalMission.Value);
            // 도착 칸으로 직접 교환하는 회수는 확정 기여다. 숨은 낙하 경로를 가정하지 않는다.
            if (action.Second.HasValue)
                foreach (BoardCoordinate at in new[] { action.First, action.Second.Value })
                    if (board.Arrivals.Contains(at) && BasicBotStrategy.AfterSwap(board, action, at).Content == BotContent.Recovery) Add(MissionKind.Recovery);
            int completed = board.Missions.Sum(mission => Math.Min(mission.Remaining,
                progress.TryGetValue((mission.Kind, mission.Color), out int amount) ? amount : 0));
            return completed * 8 + partial;

            bool Has(MissionKind? kind) => kind.HasValue && board.Missions.Any(mission => mission.Kind == kind.Value && mission.Remaining > 0);
            void Add(MissionKind kind, RabbitColor? targetColor = null)
            {
                var key = (kind, kind == MissionKind.Color ? targetColor : null);
                progress.TryGetValue(key, out int count); progress[key] = count + 1;
            }
            void Cover(BotCell target, BoardCoordinate at)
            {
                if (!covers.Add(at)) return;
                if (!Has(target.CoverMission)) return;
                if (!target.CoverUsesDurability || target.CoverDurability <= target.CoverDamage) Add(target.CoverMission.Value);
                else partial += Math.Min(target.CoverDurability, target.CoverDamage);
            }
            void Body(BotCell target, BoardCoordinate at, bool directPower, RabbitColor? sourceColor)
            {
                BotBody body = board.Bodies.First(item => item.Key == target.BodyKey.Value);
                if (directPower ? !body.AcceptsPower : !(magnet ? body.AcceptsMagnetAdjacent : body.AcceptsAdjacentMatch)) return;
                if (!directPower && body.RequiresAdjacentColor && body.Color != sourceColor) return;
                if (!bodyHits.TryGetValue(body.Key, out HashSet<BoardCoordinate> cells)) bodyHits.Add(body.Key, cells = new HashSet<BoardCoordinate>());
                cells.Add(at);
            }
        }

    }
}
