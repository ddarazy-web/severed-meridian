using System;
using System.Collections.Generic;
using System.Linq;
using Board;
using Elements;
using Levels;
using Simulation;

namespace Tutorial
{
    public sealed class TutorialConditionSample
    {
        public string Title { get; }
        public string Preview { get; }
        private readonly TutorialConditionDefinition[] conditions;
        internal TutorialConditionSample(string title, string preview, params TutorialConditionDefinition[] conditions)
        { Title = title; Preview = preview; this.conditions = conditions.Select(value => value.Copy()).ToArray(); }
        public List<TutorialConditionDefinition> CreateConditions() => conditions.Select(value => value.Copy()).ToList();
    }

    /// <summary>샘플은 적용할 때마다 독립된 제작 데이터를 만든다. 실제 레벨은 만들지 않는다.</summary>
    public static class TutorialSampleCatalog
    {
        public static TutorialConditionSample ItemSample(BoardItem item) => new TutorialConditionSample(
            (item == BoardItem.Hammer ? "망치" : item == BoardItem.Swap ? "교환" : "섞기") + " 사용 성공",
            "지정 아이템의 실제 성공1회. 필요한 횟수를 바꾸면 단계의 무료 체험 횟수도 충분히 지정하세요.",
            new TutorialConditionDefinition { kind = TutorialConditionKind.ItemUsed, item = item });
        public static IReadOnlyList<TutorialConditionSample> ConditionSamples(ElementCatalog catalog)
        {
            var samples = new List<TutorialConditionSample>
            {
                new TutorialConditionSample("내구도 합계 줄이기", "대상들의 실제 내구도 감소 합계1. 적용 후 대상과 수치를 지정하세요.",
                    new TutorialConditionDefinition { kind = TutorialConditionKind.DurabilityDecrease, target = new TutorialTargetDefinition() }),
                new TutorialConditionSample("미션 진행 늘리기", "선택한 미션의 진행이 이 단계에서 실제로1 증가하면 완료합니다. 적용 후 레벨 미션을 선택하세요. 스테이지 승리 조건은 변경하지 않습니다.",
                    new TutorialConditionDefinition { kind = TutorialConditionKind.MissionProgress, missionIndex = -1 }),
                new TutorialConditionSample("각 대상의 내구도 줄이기", "단계 시작의 각 대상이 내구도1씩 감소해야 완료합니다. 제거만으로 부족한 감소량을 대신하지 않습니다.",
                    new TutorialConditionDefinition { kind = TutorialConditionKind.DurabilityDecrease, aggregation = TutorialDamageAggregation.Each, target = new TutorialTargetDefinition() }),
                new TutorialConditionSample("남은 내구도 정확히1", "대상의 현재 내구도가 정확히1이면 완료합니다. 이미1이면 추가 행동 없이 진행합니다.",
                    new TutorialConditionDefinition { kind = TutorialConditionKind.RemainingDurability, target = new TutorialTargetDefinition() }),
                new TutorialConditionSample("로켓으로 제거", "마지막 내구도를 단독 로켓으로 제거한 개체1개. 로켓 조합은 별도 원인이며 2×2도1개입니다.",
                    new TutorialConditionDefinition { kind = TutorialConditionKind.Removed, target = new TutorialTargetDefinition(), allowedOrigins = new List<EffectOrigin> { EffectOrigin.Rocket } }),
                new TutorialConditionSample("두 번 교환 + 내구도 감소", "성공한 교환2회와 실제 감소 합계2를 모두 요구합니다. 조건 연결을 ‘모두 충족’으로 두고 대상·고정 공급을 지정하세요.",
                    new TutorialConditionDefinition { requiredCount = 2 },
                    new TutorialConditionDefinition { kind = TutorialConditionKind.DurabilityDecrease, requiredCount = 2, target = new TutorialTargetDefinition() })
            };
            foreach (ElementDefinition power in catalog.Definitions.Where(value => value.Supply?.Behavior == ElementSupplyBehavior.Power))
            {
                var generated = new TutorialConditionDefinition { kind = TutorialConditionKind.Generated, powerDefinitionId = power.Id.Value, target = new TutorialTargetDefinition() };
                samples.Add(new TutorialConditionSample(power.DisplayName + " 생성", "보드 전체에서 생성1개. 필요하면 생성 위치 영역과 다음 단계에서 사용할 연결 이름을 지정하세요.", generated));
                if (power.Supply.Content == RuntimeContent.Rocket)
                    foreach (RocketDirection direction in Enum.GetValues(typeof(RocketDirection)))
                    {
                        TutorialConditionDefinition directed = generated.Copy(); directed.anyDirection = false; directed.rocketDirection = direction;
                        samples.Add(new TutorialConditionSample((direction == RocketDirection.Horizontal ? "가로 " : "세로 ") + power.DisplayName + " 생성",
                            "지정 방향의 로켓이 생성된 순간1개를 집계합니다. 이후 낙하 위치와 구분합니다.", directed));
                    }
                samples.Add(new TutorialConditionSample(power.DisplayName + " 직접 단독 발동", "플레이어가 직접 조작한 단독 발동1회. 연쇄 및 조합 발동은 제외합니다.",
                    new TutorialConditionDefinition { kind = TutorialConditionKind.Activated, powerDefinitionId = power.Id.Value, target = new TutorialTargetDefinition() }));
            }
            foreach (EffectOrigin origin in Enum.GetValues(typeof(EffectOrigin)))
                if (origin >= EffectOrigin.RocketRocket)
                    samples.Add(new TutorialConditionSample(OriginLabel(origin) + " 조합", "지정한 두 파워 조합1회. 단독 발동 조건에는 중복 반영하지 않습니다.",
                        new TutorialConditionDefinition { kind = TutorialConditionKind.Combined, target = new TutorialTargetDefinition(), allowedOrigins = new List<EffectOrigin> { origin } }));
            return samples.AsReadOnly();
        }

        public static string OriginLabel(EffectOrigin origin) => origin switch
        {
            EffectOrigin.AdjacentMatch => "인접 매칭", EffectOrigin.Rocket => "로켓", EffectOrigin.Bomb => "폭탄", EffectOrigin.Drone => "드론",
            EffectOrigin.Magnet => "자석", EffectOrigin.Hammer => "망치", EffectOrigin.RocketRocket => "로켓 + 로켓", EffectOrigin.RocketBomb => "로켓 + 폭탄",
            EffectOrigin.RocketDrone => "로켓 + 드론", EffectOrigin.BombBomb => "폭탄 + 폭탄", EffectOrigin.BombDrone => "폭탄 + 드론", EffectOrigin.DroneDrone => "드론 + 드론",
            EffectOrigin.MagnetRocket => "자석 + 로켓", EffectOrigin.MagnetBomb => "자석 + 폭탄", EffectOrigin.MagnetDrone => "자석 + 드론", EffectOrigin.MagnetMagnet => "자석 + 자석", _ => origin.ToString()
        };

        public static TutorialStepDefinition CreateSwap(BoardCoordinate first, BoardCoordinate second, bool match)
        {
            return new TutorialStepDefinition
            {
                kind = TutorialStepKind.Swap, instructions = "표시된 두 블록을 바꿔 주세요.",
                hasFirst = true, first = first, hasSecond = true, second = second, automaticHighlights = true,
                highlights = new List<BoardCoordinate> { first, second },
                conditions = new List<TutorialConditionDefinition>
                {
                    new TutorialConditionDefinition { kind = match ? TutorialConditionKind.Match : TutorialConditionKind.SuccessfulSwap }
                }
            };
        }
    }
}
