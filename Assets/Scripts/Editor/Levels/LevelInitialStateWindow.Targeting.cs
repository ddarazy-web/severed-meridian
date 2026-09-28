using System.Linq;
using Simulation;

namespace Levels.Editor
{
    public sealed partial class LevelInitialStateWindow
    {
        private void ShowTargeting()
        {
            details.text += "\n\n" + EndingSummary();
            details.text += "\n\n" + RecoverySummary(execution.State);
            overview.text += "\n\n발전기 충전·연결 기록\n" + string.Join("\n", execution.TurnEffects.Generators.Select(r =>
                "#" + r.GeneratorIndex + " · " + (r.Event == GeneratorEvent.Charged ? "충전" : r.Event == GeneratorEvent.Activated ? "완충 작동" :
                r.Event == GeneratorEvent.Disconnected ? "연결 해제" : "자동 철거") + " " + r.ChargeBefore + "→" + r.ChargeAfter +
                (r.TargetIndex.HasValue ? " · 대상 #" + r.TargetIndex : "")));
            if (execution.TurnEffects.MoldSpread != null)
                details.text += "\n\n턴 종료 · " + execution.TurnEffects.MoldSpread.Message;
            details.text += "\n\n고철 현재 " + execution.State.LiveScrapCount + " / 유지 " + execution.State.Supply.ScrapTarget +
                " · 추가 생성 " + execution.State.Supply.ScrapGenerated + " / 한도 " + execution.State.Supply.ScrapLimit +
                " · 남음 " + execution.State.Supply.ScrapRemaining;
            PowerCombination combination = execution.TurnEffects.Combination;
            if (combination != null)
            {
                string name = combination.Kind switch
                {
                    PowerCombinationKind.RocketRocket => "청소로켓 + 청소로켓", PowerCombinationKind.RocketBomb => "청소로켓 + 달폭탄",
                    PowerCombinationKind.RocketDrone => "청소로켓 + 수거드론", PowerCombinationKind.BombBomb => "달폭탄 + 달폭탄",
                    PowerCombinationKind.BombDrone => "달폭탄 + 수거드론", PowerCombinationKind.DroneDrone => "수거드론 + 수거드론",
                    PowerCombinationKind.MagnetRocket => "무지개 자석 + 청소로켓", PowerCombinationKind.MagnetBomb => "무지개 자석 + 달폭탄",
                    PowerCombinationKind.MagnetDrone => "무지개 자석 + 수거드론", _ => "무지개 자석 + 무지개 자석"
                };
                details.text += "\n\n조합 " + name + "\n중심 " + combination.Center + " · 재료 2개 소모" +
                    (combination.Color.HasValue ? "\n선택 색 토" + ((int)combination.Color.Value + 1) : "") +
                    (combination.DroneCount > 0 ? "\n드론 " + combination.DroneCount + "대 · 도착 범위 " + combination.DroneArea : "");
                overview.text = "조합 변환 기록 · " + combination.Transformations.Count + "개\n" + string.Join("\n", combination.Transformations.Select(t =>
                    t.Coordinate + " · 토" + ((int)t.OriginalColor + 1) + " → " + t.Content + (t.Direction.HasValue ? " / " + (t.Direction == RocketDirection.Horizontal ? "가로" : "세로") : ""))) + "\n\n" + overview.text;
            }
            details.text += "\n\n실제 미션 진행\n" + string.Join("\n", execution.State.Missions.Select(m =>
                m.Definition.Kind == MissionKind.Mold ? "남은 곰팡이 " + m.Remaining :
                (m.Definition.Kind == MissionKind.Color ? "색상 " + m.Definition.Color : LevelMissionRules.Name(m.Definition.Kind)) + " " + m.Progress + "/" + m.Target));
            overview.text += "\n\n본체 공유 내구도\n" + string.Join("\n", execution.State.Obstacles.Select((body, index) =>
                "#" + index + " · " + body.Definition.Id + " · " + LevelPlacementRules.Name(body.Definition.Kind) + " " + body.Durability +
                " · 점유 " + string.Join(", ", execution.State.Cells.Where(c => c.ObstacleIndex == index).Select(c => c.Coordinate))));
            overview.text += "\n\n층별 현재 상태 (거미줄 / 먼지)\n" + string.Join("\n", execution.State.Cells.Where(c => c.Cover.HasValue || c.DustDurability > 0 ||
                execution.TurnEffects.HasDamagedWeb(c.Coordinate) || execution.TurnEffects.HasDamagedDust(c.Coordinate)).Select(c => c.Coordinate + " · " + c.CoverDurability + " / " + c.DustDurability +
                " · 이번 턴 피해 " + (execution.TurnEffects.HasDamagedWeb(c.Coordinate) ? "거미줄 " : "") + (execution.TurnEffects.HasDamagedDust(c.Coordinate) ? "먼지" : "")));
            overview.text += "\n\n색 선택·드론 목표 기록 (예약 예상량은 실제 진행과 별도)\n" +
                string.Join("\n", execution.TurnEffects.Targeting.Select(r => "요청 " + r.Request + " " + r.Origin +
                    (r.Target.HasValue ? " → " + r.Target : "") + " · " + r.Message +
                    "\n예약 " + r.ReservedCount + " · 예상 완료 " + r.ExpectedComplete + " · 예상 피해 " + r.ExpectedDamage + " · 예상 충전 " + r.ExpectedCharge));
        }
    }
}
