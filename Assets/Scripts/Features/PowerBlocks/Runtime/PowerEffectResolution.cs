using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Board;
using Levels;

namespace Simulation
{
    public sealed class EffectRecord
    {
        public BoardCoordinate Source { get; }
        public BoardCoordinate Target { get; }
        public DamageCause Cause { get; }
        public DamageResponse Response { get; }
        public RuntimeContent Content { get; }
        public RabbitColor? OriginalColor { get; }
        public int DurabilityBefore { get; }
        public int DurabilityAfter { get; }
        public int CoverBefore { get; }
        public int CoverAfter { get; }
        public int DustBefore { get; }
        public int DustAfter { get; }
        public string Message { get; }
        public int HitGroup { get; internal set; }
        public int ChargeBefore { get; internal set; }
        public int ChargeAfter { get; internal set; }
        // 이 타격에 딸린 발전기 작동·연결 해제까지 포함한 실제 제거 본체다. 저장 데이터가 아니다.
        public ReadOnlyCollection<int> RemovedObstacleIndices { get; internal set; } = System.Array.AsReadOnly(System.Array.Empty<int>());
        internal EffectRecord(BoardCoordinate source, BoardCoordinate target, DamageCause cause, DamageReaction reaction,
            RuntimeContent content, int before, int after, RabbitColor? color = null, int coverBefore = 0, int coverAfter = 0, int dustBefore = 0, int dustAfter = 0)
        { Source = source; Target = target; Cause = cause; Response = reaction.Response; Content = content; DurabilityBefore = before; DurabilityAfter = after; OriginalColor = color; Message = reaction.Message;
            CoverBefore = coverBefore; CoverAfter = coverAfter; DustBefore = dustBefore; DustAfter = dustAfter; }
    }

    // 범위 계산은 대상 반응과 분리한다. 벽/흐름/통로는 파워 범위를 바꾸지 않는다.
    public static class PowerEffectResolution
    {
        public const string Version = "power-effects-items-v9";
        public static ReadOnlyCollection<BoardCoordinate> Range(LevelRuntimeState state, BoardCoordinate origin)
        {
            RuntimeCell power = state.CellAt(origin);
            return state.Cells.Where(cell => cell.IsActive &&
                (power.Content == RuntimeContent.Rocket ?
                    (power.RocketDirection == Levels.RocketDirection.Horizontal ? cell.Coordinate.Row == origin.Row : cell.Coordinate.Column == origin.Column) :
                 power.Content == RuntimeContent.Bomb ? System.Math.Abs(cell.Coordinate.Row - origin.Row) <= 1 && System.Math.Abs(cell.Coordinate.Column - origin.Column) <= 1 :
                 power.Content == RuntimeContent.Drone && System.Math.Abs(cell.Coordinate.Row - origin.Row) + System.Math.Abs(cell.Coordinate.Column - origin.Column) <= 1))
                .Select(cell => cell.Coordinate).ToList().AsReadOnly();
        }

        internal static bool Apply(LevelRuntimeState work, IEnumerable<MatchedBlockChange> matches, BoardCoordinate? activation,
            TurnEffectContext context, List<EffectRecord> records, out string error)
            => ApplyWithColor(work, matches, activation, context, records, out error, null);

        internal static bool ApplyWithColor(LevelRuntimeState work, IEnumerable<MatchedBlockChange> matches, BoardCoordinate? activation,
            TurnEffectContext context, List<EffectRecord> records, out string error, RabbitColor? exchangeColor)
            => ApplyCore(work, matches, activation, context, records, out error, exchangeColor, null);

        internal static bool ApplyHammer(LevelRuntimeState work, BoardCoordinate target, TurnEffectContext context, List<EffectRecord> records, out string error)
            => ApplyCore(work, System.Array.Empty<MatchedBlockChange>(), target, context, records, out error, null, null, true);

        internal static bool ApplyCombination(LevelRuntimeState work, BoardCoordinate first, BoardCoordinate second,
            TurnEffectContext context, List<EffectRecord> records, out string error)
        {
            PowerCombination combination;
            try { combination = PowerCombinationResolution.Prepare(work, first, second, context); }
            catch (System.InvalidOperationException failure) { error = failure.Message; return false; }
            context.Combination = combination;
            return ApplyCore(work, System.Array.Empty<MatchedBlockChange>(), null, context, records, out error, null, combination);
        }

        private static bool ApplyCore(LevelRuntimeState work, IEnumerable<MatchedBlockChange> matches, BoardCoordinate? activation,
            TurnEffectContext context, List<EffectRecord> records, out string error, RabbitColor? exchangeColor, PowerCombination combination, bool hammer = false)
        {
            if (work.Missions.Any(m => !MissionProgressRules.Supports(m.Definition.Kind)))
            { error = "미지원 미션 포함"; return false; }
            MatchedBlockChange[] consumed = matches.ToArray();
            foreach (MatchedBlockChange match in consumed.Where(m => m.IsConsumed)) MissionProgressRules.ConsumeColor(work, match.OriginalColor);
            DroneTargetManager targets = new DroneTargetManager(work, context);
            // 라스트팡은 같은 결과 목록에 여러 파워를 누적하므로 표시 기록도 함께 누적한다.
            PowerPresentationTrace trace = records.Count > 0 && context.PowerTrace != null ? context.PowerTrace : new PowerPresentationTrace(combination);
            context.PowerTrace = trace;
            // 역순으로 쌓아 행·열 순서로 처리한다. 피격 파워의 범위는 남은 부모 범위보다 먼저 처리한다.
            Stack<(BoardCoordinate source, BoardCoordinate target, DamageCause cause, bool request, RabbitColor? color, PowerArea area, int hit, bool magnet)> pending =
                new Stack<(BoardCoordinate, BoardCoordinate, DamageCause, bool, RabbitColor?, PowerArea, int, bool)>();
            void PushRange(BoardCoordinate source, IEnumerable<BoardCoordinate> range, int hit, bool magnet = false,
                RuntimeContent power = RuntimeContent.Empty, int parent = 0, PowerArea area = PowerArea.Point)
            {
                BoardCoordinate[] selected = range.ToArray();
                trace.Add(new PowerAttackRecord(hit, parent, source, source, power,
                    work.CellAt(source).RocketDirection, area, false, 0, selected));
                foreach (BoardCoordinate target in selected.Reverse())
                    pending.Push((source, target, DamageCause.Power, false, null, PowerArea.Point, hit, magnet));
            }
            void PushAdjacent(BoardCoordinate source, DamageCause cause, RabbitColor? color, int hit)
            {
                foreach (BoardCoordinate target in new[] {
                    new BoardCoordinate(source.Row + 1, source.Column), new BoardCoordinate(source.Row, source.Column + 1),
                    new BoardCoordinate(source.Row, source.Column - 1), new BoardCoordinate(source.Row - 1, source.Column) })
                    pending.Push((source, target, cause, false, color, PowerArea.Point, hit, false));
            }
            Queue<(int request, BoardCoordinate origin)> landings = new Queue<(int, BoardCoordinate)>();
            if (activation.HasValue) pending.Push((activation.Value, activation.Value, hammer ? DamageCause.Hammer : DamageCause.Power, false, exchangeColor, PowerArea.Point, context.NextHit(), false));
            if (combination != null)
            {
                if (combination.IsTransformation)
                {
                    foreach (PowerTransformation transformation in combination.Transformations.Reverse())
                        pending.Push((transformation.Coordinate, transformation.Coordinate, DamageCause.Power, false, null, PowerArea.Point, context.NextHit(), false));
                    foreach (BoardCoordinate covered in combination.CoveredTargets.Reverse())
                        pending.Push((combination.Center, covered, DamageCause.Power, false, null, PowerArea.Point, context.NextHit(), false));
                }
                else
                {
                    for (int i = 0; i < combination.DroneCount; i++)
                        pending.Push((combination.Center, combination.Center, DamageCause.Power, true, null, combination.DroneArea, 0, false));
                    PushRange(combination.Center, PowerCombinationResolution.Range(work, combination.Center, combination.InitialArea), context.NextHit(),
                        power: combination.Kind == PowerCombinationKind.MagnetMagnet ? RuntimeContent.Magnet :
                            combination.Kind == PowerCombinationKind.RocketRocket || combination.Kind == PowerCombinationKind.RocketBomb ? RuntimeContent.Rocket :
                            combination.Kind == PowerCombinationKind.BombBomb ? RuntimeContent.Bomb : RuntimeContent.Drone, area: combination.InitialArea);
                }
            }
            foreach (MatchedBlockChange match in consumed.Reverse())
                PushAdjacent(match.Coordinate, DamageCause.AdjacentMatch, match.OriginalColor, match.HitGroup);
            int events = 0;
            // 9×9의 모든 파워가 전판을 타격해도 충분한 상한. 비정상 반복은 작업 사본 전체를 거절한다.
            int limit = System.Math.Max(1024, work.Cells.Count * work.Cells.Count * 8);
            while (pending.Count > 0 || landings.Count > 0)
            {
                if (++events > limit) { error = "효과/드론 처리 한도 " + limit + "회 초과 · 행동 전체 취소"; return false; }
                if (pending.Count == 0)
                {
                    var landing = landings.Dequeue();
                    DroneTarget target = targets.Land(landing.request, landing.origin);
                    if (target != null)
                    {
                        // 타격 Source는 기존 규칙대로 유지하되 별도 표시 기록에는 실제 착탄점도 보존한다.
                        int landingHit = context.NextHit();
                        BoardCoordinate source = target.Area == PowerArea.Point ? landing.origin : target.Coordinate;
                        BoardCoordinate[] selected = PowerCombinationResolution.Range(work, target.Coordinate, target.Area).ToArray();
                        trace.Add(new PowerAttackRecord(landingHit, 0, landing.origin, target.Coordinate, RuntimeContent.Drone,
                            null, target.Area, true, trace.Attacks.Count, selected,
                            context.Targeting.Any(record => record.Request == landing.request && record.Event == TargetingEvent.Retargeted)));
                        foreach (BoardCoordinate coordinate in selected.Reverse())
                            pending.Push((source, coordinate, DamageCause.Power, false, null, PowerArea.Point, landingHit, false));
                    }
                    continue;
                }
                var hit = pending.Pop();
                if (hit.request)
                {
                    landings.Enqueue((targets.RequestArea(hit.source, hit.area), hit.source));
                    continue;
                }
                DamageReaction reaction = DamageReaction.Evaluate(work, hit.target, hit.cause, hit.source, context,
                    hit.cause == DamageCause.Power ? null : hit.color, hit.hit);
                if (reaction.Response == DamageResponse.None) continue;
                if (reaction.Response == DamageResponse.Unsupported) { error = hit.target + " " + reaction.Message + " · 행동 전체 취소"; return false; }
                RuntimeCell cell = work.CellAt(hit.target);
                int? originalBody = cell.ObstacleIndex;
                int generatorRecordStart = context.Generators.Count;
                RuntimeContent original = cell.Content;
                RabbitColor? originalColor = cell.Color;
                int coverBefore = cell.CoverDurability, dustBefore = cell.DustDurability;
                int before = cell.ObstacleIndex.HasValue ? work.Obstacles[cell.ObstacleIndex.Value].Durability : 0, after = before;
                int chargeBefore = cell.ObstacleIndex.HasValue ? work.Obstacles[cell.ObstacleIndex.Value].Charge : 0, chargeAfter = chargeBefore;
                if (reaction.Response == DamageResponse.Charge)
                {
                    int index = cell.ObstacleIndex.Value;
                    GeneratorRules.Apply(work, index, context);
                    chargeAfter = work.Obstacles[index].Charge;
                    targets.Invalidate();
                }
                if (reaction.Response == DamageResponse.Activate)
                {
                    IEnumerable<BoardCoordinate> range = Range(work, hit.target);
                    if (original == RuntimeContent.Drone)
                        pending.Push((hit.target, hit.target, DamageCause.Power, true, null, PowerArea.Point, 0, false));
                    if (original == RuntimeContent.Magnet)
                    {
                        RabbitColor[] colors = work.Cells.Where(c => c.IsActive && c.Content == RuntimeContent.Normal && c.Cover != CoverKind.Mold && c.Color.HasValue)
                            .Select(c => c.Color.Value).Distinct().OrderBy(c => c).ToArray();
                        RabbitColor? color = hit.color ?? (colors.Length == 0 ? (RabbitColor?)null : colors[colors.Length == 1 ? 0 : work.Random.Next(colors.Length)]);
                        context.RecordTargeting(new TargetingRecord(TargetingEvent.ColorSelected, 0, hit.target, null, color,
                            targets.ReservationCount, targets.ExpectedComplete, color.HasValue ? "자석 선택 색 " + color + (hit.color.HasValue ? " · 교환 상대" : " · 존재 색 무작위") : "일반 블록 없음 · 피격 자석 소모", targets.ExpectedDamage, targets.ExpectedCharge));
                        range = work.Cells.Where(c => c.IsActive && c.Content == RuntimeContent.Normal && c.Cover != CoverKind.Mold && color.HasValue && c.Color == color).Select(c => c.Coordinate).ToArray();
                    }
                    context.RegisterFire(hit.target);
                    PushRange(hit.target, range, context.NextHit(), original == RuntimeContent.Magnet, original, hit.hit,
                        original == RuntimeContent.Rocket ? (cell.RocketDirection == RocketDirection.Horizontal ? PowerArea.Horizontal : PowerArea.Vertical) :
                        original == RuntimeContent.Bomb ? PowerArea.Blast3 : original == RuntimeContent.Drone ? PowerArea.Plus : PowerArea.Point);
                }
                if (reaction.Response == DamageResponse.Damage)
                {
                    after = ObstacleDamageRules.Apply(work, cell, context, hit.hit);
                }
                if (reaction.Response == DamageResponse.CoverDamage)
                { if (cell.Cover == CoverKind.Mold) MoldRules.Remove(work, cell, context); else WebRules.Apply(work, cell, context); }
                if (reaction.Response == DamageResponse.Remove)
                {
                    MissionProgressRules.ConsumeColor(work, originalColor);
                    if (hit.cause != DamageCause.Hammer) DustRules.ConsumeNormal(work, cell, context);
                    if (hit.magnet) PushAdjacent(hit.target, DamageCause.MagnetAdjacent, originalColor, hit.hit);
                }
                if (reaction.Response == DamageResponse.Remove || reaction.Response == DamageResponse.Activate ||
                    (reaction.Response == DamageResponse.Damage && after == 0))
                { cell.Content = RuntimeContent.Empty; cell.Color = null; cell.RocketDirection = null; cell.ObstacleIndex = null; }
                if (reaction.Response == DamageResponse.Remove || reaction.Response == DamageResponse.Activate || reaction.Response == DamageResponse.Damage || reaction.Response == DamageResponse.CoverDamage)
                    targets.Invalidate();
                HashSet<int> removedBodies = new HashSet<int>();
                if (reaction.Response == DamageResponse.Damage && after == 0 && originalBody.HasValue) removedBodies.Add(originalBody.Value);
                foreach (GeneratorRecord generator in context.Generators.Skip(generatorRecordStart))
                {
                    if (generator.Event == GeneratorEvent.Activated || generator.Event == GeneratorEvent.Retired) removedBodies.Add(generator.GeneratorIndex);
                    if (generator.Event == GeneratorEvent.Disconnected && generator.TargetIndex.HasValue) removedBodies.Add(generator.TargetIndex.Value);
                }
                records.Add(new EffectRecord(hit.source, hit.target, hit.cause, reaction, original, before, after, originalColor, coverBefore, cell.CoverDurability, dustBefore, cell.DustDurability)
                { HitGroup = hit.hit, ChargeBefore = chargeBefore, ChargeAfter = chargeAfter, RemovedObstacleIndices = removedBodies.OrderBy(index => index).ToList().AsReadOnly() });
            }
            if (targets.ReservationCount != 0) { error = "미해제 드론 예약 · 행동 전체 취소"; return false; }
            error = null; return true;
        }
    }
}
