using System.Linq;
using Simulation;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    public sealed partial class LevelInitialStateWindow
    {
        private void ExecuteSettlement()
        {
            CheckInput();
            if (execution == null) return;
            SettlementResult result = execution.Settle(); executionInfo.text = result.Message;
            if (!result.IsApplied) return;
            DisplayState(execution.State);
            rootVisualElement.Q<Label>("initial-boundary").text = result.Message + " · 연쇄 완료 후 종료 판정";
            executionInfo.text = "정착 완료. 자동 매칭 단계 또는 연쇄 끝까지 실행하세요.";
            int supplied = result.Records.Count(r => r.Kind == MovementKind.Supply);
            var matches = MatchQuery.Find(execution.State);
            querySummary.text = "이동 " + (result.Records.Count - supplied) + " · 공급 " + supplied + " · 빈칸 " + result.EmptyCells.Count + " · 정착 후 매칭 " + matches.Count;
            queryResults.Clear(); rootVisualElement.Q<Foldout>("query-foldout").style.display = DisplayStyle.Flex;
            for (int index = 0; index < matches.Count; index++)
            {
                MatchPattern match = matches[index];
                queryResults.Add(new Button(() => Highlight(match.Cells)) { name = "settled-match-" + index, text = "정착 매칭 · 토" + ((int)match.Color + 1) + " · " + match.Cells.Count + "칸" });
            }
            foreach (var coordinate in result.Records.Select(r => r.Target).Distinct())
                grid.Q<Button>($"initial-cell-{coordinate.Row}-{coordinate.Column}")?.AddToClassList("execution-removed");
            HighlightProtectedPowers();
            details.text = result.Message + "\n남은 이동 " + execution.State.MovesRemaining + " / 수 " + execution.Turn +
                "\n난수 " + result.RandomBefore + " → " + result.RandomAfter + "\n남은 빈칸: " + string.Join(", ", result.EmptyCells) +
                "\n\n생성구 커서 (목록 인덱스는 0부터)\n" + string.Join("\n", execution.State.Supply.Sources.Select(s => s.Coordinate + " 항목 " + s.ItemIndex + " / 소비 " + s.ItemConsumed));
            overview.text = "이동·공급 기록\n" + string.Join("\n", result.Records.Select(r => "묶음 " + r.Batch + " " +
                (r.Kind switch { MovementKind.Gravity => "직선", MovementKind.Path => "경로", MovementKind.Portal => "통로", MovementKind.Diagonal => "대각선", _ => "공급" }) +
                " " + r.Source + " → " + r.Target + " / " +
                (r.Content switch { RuntimeContent.Rocket => "청소로켓", RuntimeContent.Bomb => "달폭탄", RuntimeContent.Drone => "수거드론", RuntimeContent.Magnet => "무지개 자석", RuntimeContent.Obstacle => "고철 #" + r.ObstacleIndex, RuntimeContent.Recovery => "회수 부품", _ => "토" + ((int)r.Color.Value + 1) }) +
                (r.Protected ? " · 이번 수 보호" : "") +
                (r.Kind == MovementKind.Supply ? "\n목록 " + r.ItemBefore + ":" + r.ConsumedBefore + " → " + r.ItemAfter + ":" + r.ConsumedAfter : ""))) +
                "\n\n" + RecoverySummary(execution.State) + "\n\n도착 회수 미션을 반영했습니다. 다음 단계에서 자동 매칭과 종료 판정을 진행합니다.";
            ShowTargeting();
            UpdateExecutionControls();
        }
    }
}
