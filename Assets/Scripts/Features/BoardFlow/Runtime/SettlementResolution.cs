using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Board;
using Levels;

namespace Simulation
{
    public enum SettlementReason { Applied, WrongPhase, Unsupported, InvalidFlow, Repeating, LimitReached }

    public sealed class SettlementRecord
    {
        public int Batch { get; }
        public MovementKind Kind { get; }
        public BoardCoordinate Source { get; }
        public BoardCoordinate Target { get; }
        public RuntimeContent Content { get; }
        public RabbitColor? Color { get; }
        public RocketDirection? Direction { get; }
        public int? ObstacleIndex { get; }
        public bool Protected { get; }
        public int ItemBefore { get; }
        public int ConsumedBefore { get; }
        public int ItemAfter { get; }
        public int ConsumedAfter { get; }
        internal Elements.ElementDefinition ContentElement { get; }
        internal SettlementRecord(int batch, MovementKind kind, BoardCoordinate source, RuntimeCell cell, bool protectedPower,
            int itemBefore = 0, int consumedBefore = 0, int itemAfter = 0, int consumedAfter = 0)
        {
            Batch = batch; Kind = kind; Source = source; Target = cell.Coordinate; Content = cell.Content; Color = cell.Color;
            Direction = cell.RocketDirection; Protected = protectedPower; ItemBefore = itemBefore; ConsumedBefore = consumedBefore; ItemAfter = itemAfter; ConsumedAfter = consumedAfter;
            ObstacleIndex = cell.ObstacleIndex;
            ContentElement = cell.ContentElement;
        }
    }

    public sealed class SettlementResult
    {
        public SettlementReason Reason { get; }
        public bool IsApplied => Reason == SettlementReason.Applied;
        public string Message { get; }
        public ReadOnlyCollection<SettlementRecord> Records { get; }
        public ReadOnlyCollection<BoardCoordinate> EmptyCells { get; }
        public int RandomBefore { get; }
        public int RandomAfter { get; }
        public LevelRuntimeState State { get; }
        public TurnEffectContext TurnEffects { get; }
        internal SettlementResult(SettlementReason reason, string message, LevelRuntimeState original, LevelRuntimeState state = null,
            TurnEffectContext context = null, IEnumerable<SettlementRecord> records = null)
        {
            Reason = reason; Message = message; State = state; TurnEffects = context;
            Records = Array.AsReadOnly(records?.ToArray() ?? Array.Empty<SettlementRecord>());
            EmptyCells = Array.AsReadOnly((state ?? original).Cells.Where(c => c.IsActive && c.Content == RuntimeContent.Empty).Select(c => c.Coordinate).ToArray());
            RandomBefore = original.Random.DrawCount; RandomAfter = (state ?? original).Random.DrawCount;
        }
    }

    // 입력을 직접 변경하지 않는다. 완료한 작업 사본만 실행기가 반영한다.
    public static class SettlementResolution
    {
        public const string Version = "settlement-fresh-diagonal-v10";
        private static readonly RabbitColor[] LastPangColors = (RabbitColor[])Enum.GetValues(typeof(RabbitColor));
        /// <param name="original">정착 전 상태. 원본은 변경하지 않는다.</param>
        /// <param name="turnEffects">현재 턴의 보호·피격 기록.</param>
        /// <param name="lastPang">성공 후에는 예약·유지 공급 대신 일반 토끼 전체 5종을 공급한다.</param>
        /// <returns>완료한 정착 사본과 이동·공급 기록.</returns>
        public static SettlementResult Resolve(LevelRuntimeState original, TurnEffectContext turnEffects = null, bool lastPang = false)
        {
            SettlementResult Reject(SettlementReason reason, string message) => new SettlementResult(reason, message, original);
            if (original.Obstacles.Any(o => !ObstacleDamageRules.Supports(o.Definition.Kind)))
                return Reject(SettlementReason.Unsupported, "미지원 장애물 정착 미지원");
            foreach (RuntimeSource source in original.Supply.Sources)
            {
                if (!Enum.IsDefined(typeof(SupplyMode), source.Mode) ||
                    source.Items.Skip(source.ItemIndex).Any(i => !Enum.IsDefined(typeof(SupplyKind), i.Kind)))
                    return Reject(SettlementReason.Unsupported, "미지원 공급 방식 또는 목록 항목입니다.");
            }
            if (original.Supply.Sources.Any(s => s.Mode == SupplyMode.MaintainScrap) &&
                original.Supply.Sources.Any(s => s.HasFixed(SupplyKind.Scrap, original.ElementCatalog)))
                return Reject(SettlementReason.Unsupported, "고정 고철 목록과 고철 개수 유지는 한 레벨에서 혼용할 수 없습니다.");
            if (original.Supply.Sources.Any(s => s.Mode == SupplyMode.MaintainRecovery) &&
                original.Supply.Sources.Any(s => s.HasFixed(SupplyKind.Recovery, original.ElementCatalog)))
                return Reject(SettlementReason.Unsupported, "고정 회수 목록과 회수 개수 유지는 한 레벨에서 혼용할 수 없습니다.");
            string invalidFlow = MovementQuery.ValidateFlow(original);
            if (invalidFlow != null) return Reject(SettlementReason.InvalidFlow, invalidFlow);
            LevelRuntimeState work = new LevelRuntimeState(original);
            // 낙하와 공급은 작업 사본에서 끝까지 시도한다. 경로 순환이나 처리 한도 초과로
            // 실패하면 중간 보드·난수·공급 커서를 원본에 남기지 않고 전체 작업을 거절한다.
            TurnEffectContext context = (turnEffects ?? new TurnEffectContext(0, Array.Empty<MatchedBlockChange>())).CopyForFall();
            List<SettlementRecord> records = new List<SettlementRecord>();
            // 이번 정착에서 생성한 점유자만 추적한다. 다음 연쇄나 저장 데이터에는 전달하지 않는다.
            HashSet<BoardCoordinate> freshSupply = new HashSet<BoardCoordinate>();
            Dictionary<string, int> seen = new Dictionary<string, int>();
            RecoveryRules.Collect(work, context.Turn, 0);
            int limit = Math.Max(1024, work.Cells.Count * work.Cells.Count * 4);
            for (int batch = 1; batch <= limit; batch++)
            {
                // 난수를 쓰지 않은 동일 상태 재방문은 확정 순환이다. 난수가 달라졌으면 한도까지 계속 확인한다.
                string key = string.Join(";", work.Cells.Select(c => (int)c.Content + "," + c.Color + "," + c.RocketDirection + "," + c.ObstacleIndex + "," + context.IsProtected(c.Coordinate) + "," + freshSupply.Contains(c.Coordinate))) +
                    "|" + work.Supply.ScrapGenerated + "|" + work.Recoveries.Count + "|" +
                    string.Join(";", work.Supply.Sources.Select(s => s.ItemIndex + "," + s.ItemConsumed));
                if (seen.TryGetValue(key, out int drawCount) && drawCount == work.Random.DrawCount)
                    return Reject(SettlementReason.Repeating, "이동 상태가 반복됩니다. 정착 전체를 취소했습니다.");
                seen[key] = work.Random.DrawCount;
                List<MovementCandidate> candidates = MovementQuery.Find(work).Where(c => c.IsAllowed).ToList();
                List<MovementCandidate> moves = new List<MovementCandidate>();
                foreach (var group in candidates.GroupBy(c => c.Target).OrderBy(g => g.Key.Row).ThenBy(g => g.Key.Column))
                {
                    // 한 도착 칸으로 둘 이상의 블록이 들어오려면 에디터의 합류 순서를 사용한다.
                    // 도착 좌표 순으로 처리해 같은 입력의 결과가 컬렉션 열거 순서에 의존하지 않게 한다.
                    RuntimeMerge merge = work.Flow.Merges.FirstOrDefault(m => m.Coordinate.Equals(group.Key));
                    moves.Add(merge == null ? group.Single() : group.OrderBy(c => merge.Sources.IndexOf(c.Source)).First());
                }
                if (moves.Count == 0)
                {
                    // 상단에서 아직 생성·이동할 블록이 있으면 빈칸을 대각선으로 선점하지 않는다.
                    int beforeSupply = records.Count;
                    Supply(work, context, batch, records, lastPang);
                    if (records.Count > beforeSupply)
                    {
                        for (int i = beforeSupply; i < records.Count; i++) freshSupply.Add(records[i].Target);
                        continue;
                    }
                    moves = SelectDiagonal(work, freshSupply);
                }
                if (moves.Count > 0)
                {
                    context.MoveProtection(moves);
                    foreach (MovementCandidate move in moves)
                    {
                        RuntimeCell source = work.CellAt(move.Source), target = work.CellAt(move.Target);
                        context.RecordArrival(move.Source, move.Target, batch, source.Content == RuntimeContent.Normal);
                        target.Content = source.Content; target.Color = source.Color; target.RocketDirection = source.RocketDirection;
                        target.ContentElement = source.ContentElement;
                        target.ObstacleIndex = source.ObstacleIndex;
                        source.Content = RuntimeContent.Empty; source.Color = null; source.RocketDirection = null;
                        source.ObstacleIndex = null;
                        if (freshSupply.Remove(move.Source)) freshSupply.Add(move.Target);
                        records.Add(new SettlementRecord(batch, move.Kind, move.Source, target, context.IsProtected(move.Target)));
                    }
                    RecoveryRules.Collect(work, context.Turn, batch);
                    freshSupply.RemoveWhere(at => work.CellAt(at).Content == RuntimeContent.Empty);
                    continue;
                }
                return new SettlementResult(SettlementReason.Applied, "정착 완료 · 자동 매칭 대기", original, work, context, records);
            }
            return Reject(SettlementReason.LimitReached, "정착 처리 한도 " + limit + "회 초과 · 상태 전체 보존. 경로와 대각선 경쟁을 확인하세요.");
        }

        private static List<MovementCandidate> SelectDiagonal(LevelRuntimeState work, ISet<BoardCoordinate> freshSupply)
        {
            List<MovementCandidate> pending = MovementQuery.Find(work, true, freshSupply).Where(c => c.IsAllowed).ToList();
            List<MovementCandidate> selected = new List<MovementCandidate>();
            while (pending.Count > 0)
            {
                // 서로 경쟁하는 연결만 섞는다. 독립 이동 둘이라는 이유로 난수를 소비하지 않는다.
                HashSet<BoardCoordinate> sources = new HashSet<BoardCoordinate> { pending[0].Source }, targets = new HashSet<BoardCoordinate> { pending[0].Target };
                bool expanded;
                do
                {
                    expanded = false;
                    foreach (MovementCandidate candidate in pending)
                        if (sources.Contains(candidate.Source) || targets.Contains(candidate.Target))
                        { expanded |= sources.Add(candidate.Source); expanded |= targets.Add(candidate.Target); }
                } while (expanded);
                List<BoardCoordinate> orderedTargets = targets.OrderBy(c => c.Row).ThenBy(c => c.Column).ToList();
                while (orderedTargets.Count > 0)
                {
                    int index = orderedTargets.Count == 1 ? 0 : work.Random.Next(orderedTargets.Count);
                    BoardCoordinate target = orderedTargets[index]; orderedTargets.RemoveAt(index);
                    MovementCandidate[] options = pending.Where(c => c.Target.Equals(target)).OrderBy(c => c.Source.Row).ThenBy(c => c.Source.Column).ToArray();
                    if (options.Length == 0) continue;
                    MovementCandidate move = options[options.Length == 1 ? 0 : work.Random.Next(options.Length)]; selected.Add(move);
                    pending.RemoveAll(c => c.Source.Equals(move.Source) || c.Target.Equals(move.Target));
                    orderedTargets.RemoveAll(c => !pending.Any(p => p.Target.Equals(c)));
                }
            }
            return selected;
        }

        private static void Supply(LevelRuntimeState work, TurnEffectContext context, int batch, List<SettlementRecord> records, bool lastPang)
        {
            if (lastPang)
            {
                // 성공 후의 공급만 바꾼다. 레벨 색상, 예약 목록·커서, 고철/회수 생성 수량은 보존한다.
                // 소진 후 정지하는 생성구도 라스트팡 동안에는 5종 일반 블록으로 빈칸을 채운다.
                foreach (RuntimeSource source in work.Supply.Sources.OrderBy(s => s.Coordinate.Row).ThenBy(s => s.Coordinate.Column))
                {
                    RuntimeCell cell = work.CellAt(source.Coordinate);
                    if (!cell.IsActive || cell.Content != RuntimeContent.Empty) continue;
                    cell.Content = RuntimeContent.Normal; cell.Color = LastPangColors[work.Random.Next(LastPangColors.Length)];
                    cell.ContentElement = Elements.LegacyElementDefinitions.GetContent(cell.Content, work.ElementCatalog);
                    cell.RocketDirection = null; cell.ObstacleIndex = null;
                    context.RecordArrival(source.Coordinate, source.Coordinate, batch, true);
                    records.Add(new SettlementRecord(batch, MovementKind.Supply, source.Coordinate, cell, false,
                        source.ItemIndex, source.ItemConsumed, source.ItemIndex, source.ItemConsumed));
                }
                return;
            }
            List<RuntimeSource> available = work.Supply.Sources.Where(s => s.Mode == SupplyMode.MaintainScrap &&
                work.CellAt(s.Coordinate).IsActive && work.CellAt(s.Coordinate).Content == RuntimeContent.Empty)
                .OrderBy(s => s.Coordinate.Row).ThenBy(s => s.Coordinate.Column).ToList();
            int needed = Math.Min(work.Supply.ScrapTarget - work.LiveScrapCount, work.Supply.ScrapRemaining);
            while (needed > 0 && available.Count > 0)
            {
                int index = available.Count == 1 ? 0 : work.Random.Next(available.Count);
                RuntimeSource source = available[index]; available.RemoveAt(index);
                RuntimeCell cell = work.CellAt(source.Coordinate);
                Elements.ElementSupplyBehaviorRegistry.Apply(work.Supply.ScrapDefinition ?? Elements.LegacyElementDefinitions.GetSupply(SupplyKind.Scrap),
                    work, cell, new SupplyItem(SupplyKind.Scrap, durability: work.Supply.ScrapDurability));
                work.Supply.ScrapGenerated++; needed--;
                context.RecordArrival(source.Coordinate, source.Coordinate, batch, false);
                records.Add(new SettlementRecord(batch, MovementKind.Supply, source.Coordinate, cell, false));
            }
            available = work.Supply.Sources.Where(s => s.Mode == SupplyMode.MaintainRecovery &&
                work.CellAt(s.Coordinate).IsActive && work.CellAt(s.Coordinate).Content == RuntimeContent.Empty)
                .OrderBy(s => s.Coordinate.Row).ThenBy(s => s.Coordinate.Column).ToList();
            while (RecoveryRules.Needed(work) > 0 && available.Count > 0)
            {
                int index = available.Count == 1 ? 0 : work.Random.Next(available.Count);
                RuntimeSource source = available[index]; available.RemoveAt(index);
                RuntimeCell cell = work.CellAt(source.Coordinate);
                Elements.ElementSupplyBehaviorRegistry.Apply(work.Supply.RecoveryDefinition ?? Elements.LegacyElementDefinitions.GetSupply(SupplyKind.Recovery), work, cell, new SupplyItem(SupplyKind.Recovery));
                context.RecordArrival(source.Coordinate, source.Coordinate, batch, false);
                records.Add(new SettlementRecord(batch, MovementKind.Supply, source.Coordinate, cell, false));
            }
            foreach (RuntimeSource source in work.Supply.Sources.OrderBy(s => s.Coordinate.Row).ThenBy(s => s.Coordinate.Column))
            {
                RuntimeCell cell = work.CellAt(source.Coordinate);
                if (!cell.IsActive || cell.Content != RuntimeContent.Empty) continue;
                bool fixedItem = source.Mode == SupplyMode.Fixed && source.ItemIndex < source.Items.Count;
                if (!fixedItem && source.Mode == SupplyMode.Fixed && source.Exhaustion == SupplyExhaustion.Stop) continue;
                SupplyItem item = fixedItem ? source.Items[source.ItemIndex] : new SupplyItem(SupplyKind.RandomNormal);
                int beforeIndex = source.ItemIndex, beforeConsumed = source.ItemConsumed;
                // 목록에는 랜덤 항목을 그대로 남기고, 실제 공급하는 한 개마다 종류를 뽑는다.
                // 공통 상태의 난수를 사용하므로 게임·봇·같은 시드 재실행 결과가 일치한다.
                // 자석은 기존 개별 공급만 지원하며 랜덤 후보에는 포함하지 않는다.
                Elements.ElementDefinition selected = fixedItem ? source.ItemDefinitions?[source.ItemIndex] : source.RandomDefinition;
                Elements.ElementSupplyBehaviorRegistry.Apply(selected ?? Elements.LegacyElementDefinitions.GetSupply(item.Kind), work, cell, item);
                context.RecordArrival(source.Coordinate, source.Coordinate, batch, cell.Content == RuntimeContent.Normal);
                if (fixedItem && ++source.ItemConsumed == item.Count) { source.ItemIndex++; source.ItemConsumed = 0; }
                records.Add(new SettlementRecord(batch, MovementKind.Supply, source.Coordinate, cell, false, beforeIndex, beforeConsumed, source.ItemIndex, source.ItemConsumed));
            }
        }
    }
}
