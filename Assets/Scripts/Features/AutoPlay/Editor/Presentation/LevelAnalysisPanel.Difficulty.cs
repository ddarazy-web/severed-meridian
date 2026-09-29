using System.Linq;
using AutoPlay;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    internal sealed partial class LevelAnalysisPanel
    {
        /// <summary>기존 통계와 저장 조건으로만 추천을 표시한다. 현재 편집 레벨로 과거 기록을 재해석하지 않는다.</summary>
        /// <param name="parent">통계 또는 비교 영역.</param><param name="reader">검증한 저장 사본.</param>
        /// <param name="name">실제 UI 검증과 접근에 사용할 고유 이름.</param>
        private void AddDifficulty(VisualElement parent, BotAnalysisReader reader, string name)
        {
            BotStrategyStatistics basic = BotBatchStatistics.Calculate(reader.Record, reader.Games, reader.Missions, BotStrategyKind.Basic);
            BotStrategyStatistics planning = BotBatchStatistics.Calculate(reader.Record, reader.Games, reader.Missions, BotStrategyKind.Planning);
            BotPairedStatistics pairs = BotBatchStatistics.Pair(reader.Record.seeds,
                reader.Games.Where(g => g.Strategy == BotStrategyKind.Basic), reader.Games.Where(g => g.Strategy == BotStrategyKind.Planning));
            string versionIssue = BotRecordReplay.CompatibilityError(reader.Record);
            // 행동 재생과 난이도 해석은 호환 조건이 다르다. 과거 전략을 현재 전략의 실력으로 취급하지 않는다.
            if (reader.Record.basicVersion != BasicBotStrategy.Version || reader.Record.planningVersion != PlanningSearch.Version ||
                reader.Record.observationVersion != BotObservationBuilder.Version || reader.Record.assumptionVersion != BotBatchSession.AssumptionVersion)
                versionIssue = (versionIssue == null ? "" : versionIssue + " ") + "전략·관찰 또는 가정 방식의 버전이 현재 평가 기준과 다릅니다.";
            BotDifficultyResult result = BotDifficultyRules.Evaluate(reader.Record, basic, planning, pairs, reader.InitialMoves, reader.Error ?? reader.DefinitionIssue, versionIssue);
            BotStrategyStatistics representative = result.Representative == BotStrategyKind.Basic ? basic : planning;
            VisualElement card = new VisualElement { name = name };
            card.style.paddingLeft = 8; card.style.paddingRight = 8; card.style.paddingTop = 8; card.style.paddingBottom = 8;
            card.style.marginBottom = 8; card.style.backgroundColor = new Color(.13f, .20f, .22f); parent.Add(card);
            Label title = Text(result.Grade.HasValue ? "예상 난이도 · " + result.Title : result.Title, name + "-title");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            card.Add(title);
            LevelEditorHelp.Link(title, "두 전략 중 높은 성공률로 계산한 제작용 참고값입니다. 보류 사유와 표본 수를 함께 확인하세요.", "difficulty.html#criteria");
            card.Add(Text("선택한 저장 사본의 결과 · 현재 기준 " + BotDifficultyRules.Version + "로 재계산", name + "-version"));
            card.Add(Text($"정상 종료: 기본 {basic.Normal}판 / 계획 {planning.Normal}판 · 같은 시드 {pairs.Included}쌍", name + "-samples"));
            if (result.HoldReasons.Count > 0) card.Add(Text(string.Join("\n", result.HoldReasons.Select(r => "• " + r)), name + "-hold"));
            else card.Add(Text($"대표: {(result.Representative == BotStrategyKind.Basic ? "기본" : "계획")} 전략 · 성공 {representative.Won}/{representative.Normal}판 ({result.SuccessPercent:F2}%)", name + "-representative"));
            if (result.Tags.Count > 0) card.Add(Text(string.Join(" · ", result.Tags), name + "-tags"));
            Foldout evidence = new Foldout { text = "추천 근거와 한계", value = false, name = name + "-evidence" }; card.Add(evidence);
            evidence.Add(Text($"기본 성공 {basic.Won}/{basic.Normal}판 ({basic.SuccessPercent:F2}%) · 계획 성공 {planning.Won}/{planning.Normal}판 ({planning.SuccessPercent:F2}%)\n" +
                $"기본만 성공 {pairs.LeftOnlyWon} · 계획만 성공 {pairs.RightOnlyWon} · 제외 {pairs.Excluded}쌍\n" +
                $"최초 이동 수 {reader.InitialMoves} · 대표 전략 성공 표본 {representative.Remaining.Count}판 · 남은 이동 수 중앙값 " +
                (representative.Remaining.Median.HasValue ? representative.Remaining.Median.Value.ToString("F1") : "기록 없음"), null));
            evidence.Add(Text("등급 구간: 60% 이상 쉬움 / 40% 이상 보통 / 20% 이상 어려움 / 0% 초과 매우 어려움. 반올림 전 값을 사용합니다.", null));
            evidence.Add(Text("계획 요구: 정상 쌍 100개 이상, 계획 우세 20%p 이상, 서로 다른 결과 20쌍 이상. 이동 여유 부족: 성공 20판 이상, 남은 이동 중앙값이 최초의 10% 이하.", null));
            foreach (string note in result.Notes) evidence.Add(Text(note, null));
            if (parent == statistics)
                AddButton(card, "사례 확인", "difficulty-cases", () => {
                    SelectPage(casePage);
                    // 두 필터의 변경 알림마다 목록을 재생성하면 첫 선택이 후속 갱신에서 지워질 수 있다.
                    // 값을 한 번에 바꾸고 목록 갱신 뒤 선택한다. 그 사이 다른 기록/화면을 열면 선택하지 않는다.
                    strategyFilter.SetValueWithoutNotify(strategyFilter.choices[(int)result.Representative + 1]);
                    outcomeFilter.SetValueWithoutNotify(outcomeFilter.choices[0]);
                    FilterCases();
                    cases.schedule.Execute(() => {
                        if (analysis == reader && casePage.style.display == DisplayStyle.Flex && filtered.Count > 0 && cases.selectedIndex < 0)
                            cases.SetSelection(0);
                    });
                }, "선택한 시험의 실제 기록을 확인합니다. 봇을 새로 실행하거나 자동 재생하지 않습니다.");
        }
    }
}
