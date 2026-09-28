using System;
using System.Collections.Generic;
using System.Linq;
using Board;
using Levels;

namespace AutoPlay
{
    /// <summary>선택 근거를 함께 반환한다. 점수는 미래 실행 결과나 난이도가 아니다.</summary>
    public sealed class BotChoice
    {
        public BotAction Action { get; }
        public int MissionValue { get; }
        public int PowerValue { get; }
        public int ClearValue { get; }
        public string Reason { get; }
        /// <param name="action">선택 후보.</param><param name="mission">미션 기여 점수.</param>
        /// <param name="power">파워 우선순위.</param><param name="clear">직접 확인한 범위 칸 수.</param>
        /// <param name="uncertainty">평가하지 않은 무작위·후속 효과 설명.</param>
        internal BotChoice(BotAction action, int mission, int power, int clear, string uncertainty)
        {
            Action = action; MissionValue = mission; PowerValue = power; ClearValue = clear;
            Reason = $"공개 미션 기여 {mission} → 파워 가치 {power} → 확인 범위 {clear}칸" +
                (string.IsNullOrEmpty(uncertainty) ? "" : " / " + uncertainty);
        }
    }

    /// <summary>공개 관찰만 평가하는 한 수 전략. 게임 실행기·난수·원본 데이터에 접근하지 않는다.</summary>
    public static class BasicBotStrategy
    {
        public const string Version = "basic-bot-v1";

        /// <summary>공개 기여, 파워, 범위 순으로 비교하고 동점은 종류·좌표 순으로 정한다.</summary>
        /// <param name="observation">입력 가능한 시점의 공개 값.</param><returns>선택과 근거. 후보가 없으면 null.</returns>
        public static BotChoice Choose(BotObservation observation)
        {
            if (observation == null) throw new ArgumentNullException(nameof(observation));
            return observation.Actions.Select(action => Evaluate(observation, action))
                .OrderByDescending(choice => choice.MissionValue).ThenByDescending(choice => choice.PowerValue)
                .ThenByDescending(choice => choice.ClearValue).ThenBy(choice => choice.Action.Kind)
                .ThenBy(choice => choice.Action.First.Row).ThenBy(choice => choice.Action.First.Column)
                .ThenBy(choice => choice.Action.Second?.Row ?? -1).ThenBy(choice => choice.Action.Second?.Column ?? -1).FirstOrDefault();
        }

        /// <summary>낙하·미래 공급·무작위 도착을 실행하지 않고 눈앞에서 확인되는 범위만 평가한다.</summary>
        /// <param name="board">공개 판.</param><param name="action">이 관찰에서 얻은 후보.</param><returns>결정적 평가와 한계 설명.</returns>
        public static BotChoice Evaluate(BotObservation board, BotAction action)
        {
            HashSet<BoardCoordinate> affected = new HashSet<BoardCoordinate>();
            int power = 0;
            string uncertainty = "추가 연쇄·새 공급은 미예측";
            bool matching = action.Kind == BotActionKind.MatchSwap;
            bool magnetColor = false;
            RabbitColor? selectedColor = null;
            if (matching)
            {
                // 여러 겹침 그룹을 실제 난수로 중재하지 않는다. 가장 높은 파워·길이의
                // 동률 후보 모두에 공통인 칸만 보수적으로 평가한다. 독립 그룹은 과소평가할 수
                // 있으나 제외된 패턴의 칸까지 실제로 제거된다고 주장하지 않는다.
                BotMatchKind kind = action.Matches.Max(match => match.Kind);
                int length = action.Matches.Where(match => match.Kind == kind).Max(match => match.Cells.Count);
                BotMatch[] tied = action.Matches.Where(match => match.Kind == kind && match.Cells.Count == length).ToArray();
                affected.UnionWith(tied[0].Cells);
                foreach (BotMatch match in tied.Skip(1)) affected.IntersectWith(match.Cells);
                power = (int)kind;
                if (tied.Length > 1) uncertainty = "동률 매칭의 공통 칸만 평가 · 실제 선택/연쇄는 미예측";
            }
            else
            {
                BotCell first = board.CellAt(action.First);
                BotCell second = action.Second.HasValue ? board.CellAt(action.Second.Value) : null;
                BoardCoordinate center = action.Second ?? action.First;
                BotContent low = second == null || first.Content <= second.Content ? first.Content : second.Content;
                BotContent high = second == null || first.Content >= second.Content ? first.Content : second.Content;
                if (action.Kind == BotActionKind.CombinationSwap)
                {
                    power = 5;
                    if (high == BotContent.Magnet && low != BotContent.Magnet)
                    {
                        // 변환 색과 로켓 방향은 실제 실행이 선택한다. 미리 난수를 뽑지 않는다.
                        uncertainty = "자석 조합의 변환 색·방향·후속 피해는 미예측";
                    }
                    else
                    {
                        foreach (BotCell cell in board.Cells.Where(cell => cell.IsActive))
                        {
                            int dr = Math.Abs(cell.Coordinate.Row - center.Row), dc = Math.Abs(cell.Coordinate.Column - center.Column);
                            bool hit = high == BotContent.Magnet ||
                                low == BotContent.Rocket && high == BotContent.Rocket && (dr == 0 || dc == 0) ||
                                low == BotContent.Rocket && high == BotContent.Bomb && (dr <= 1 || dc <= 1) ||
                                low == BotContent.Bomb && high == BotContent.Bomb && dr <= 2 && dc <= 2 ||
                                low == BotContent.Drone && high == BotContent.Drone && dr <= 1 && dc <= 1 ||
                                high == BotContent.Drone && low != BotContent.Drone && dr + dc <= 1;
                            if (hit) affected.Add(cell.Coordinate);
                        }
                        if (high == BotContent.Drone) uncertainty = "조합 지점만 평가 · 드론 도착과 추가 연쇄는 미예측";
                    }
                }
                else
                {
                    BotCell source = first.Content >= BotContent.Rocket && first.Content <= BotContent.Magnet ? first : second;
                    if (source == second) center = action.First;
                    power = source.Content == BotContent.Magnet ? 4 : source.Content == BotContent.Bomb ? 3 : source.Content == BotContent.Rocket ? 2 : 1;
                    if (source.Content == BotContent.Magnet)
                    {
                        BotCell partner = source == first ? second : first;
                        selectedColor = partner?.Content == BotContent.Normal ? partner.Color : null;
                        magnetColor = true;
                        if (selectedColor.HasValue)
                            affected.UnionWith(board.Cells.Where(cell => cell.IsActive && AfterSwap(board, action, cell.Coordinate).Content == BotContent.Normal &&
                                AfterSwap(board, action, cell.Coordinate).Color == selectedColor).Select(cell => cell.Coordinate));
                        else uncertainty = "자석 제자리 발동의 선택 색은 미예측";
                    }
                    else foreach (BotCell cell in board.Cells.Where(cell => cell.IsActive))
                    {
                        int dr = Math.Abs(cell.Coordinate.Row - center.Row), dc = Math.Abs(cell.Coordinate.Column - center.Column);
                        if (source.Content == BotContent.Rocket && (source.RocketDirection == RocketDirection.Horizontal ? dr == 0 : dc == 0) ||
                            source.Content == BotContent.Bomb && dr <= 1 && dc <= 1 || source.Content == BotContent.Drone && dr + dc <= 1)
                            affected.Add(cell.Coordinate);
                    }
                    if (source.Content == BotContent.Drone) uncertainty = "발동 지점만 평가 · 드론 도착과 추가 연쇄는 미예측";
                }
            }
            int mission = BotMissionEvaluation.Score(board, action, affected, matching, magnetColor, selectedColor);
            return new BotChoice(action, mission, power, affected.Count, uncertainty);
        }

        /// <summary>교환 후 점유자만 조회한다. 먼지 등 좌표에 남는 층은 원래 칸에서 읽어야 한다.</summary>
        /// <param name="board">공개 판.</param><param name="action">조작.</param><param name="at">조회 칸.</param><returns>교환 후 점유자.</returns>
        internal static BotCell AfterSwap(BotObservation board, BotAction action, BoardCoordinate at)
        {
            if (!action.Second.HasValue) return board.CellAt(at);
            if (at.Equals(action.First)) return board.CellAt(action.Second.Value);
            if (at.Equals(action.Second.Value)) return board.CellAt(action.First);
            return board.CellAt(at);
        }
    }
}
