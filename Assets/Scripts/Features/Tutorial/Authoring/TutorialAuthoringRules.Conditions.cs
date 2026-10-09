using System;
using System.Linq;
using Board;
using Elements;
using Simulation;

namespace Tutorial
{
    public static partial class TutorialAuthoringRules
    {
        public static string ConditionLabel(TutorialConditionKind kind) => kind switch
        {
            TutorialConditionKind.SuccessfulSwap => "교환 횟수", TutorialConditionKind.Match => "매칭 조건",
            TutorialConditionKind.DurabilityDecrease => "내구도 줄이기", TutorialConditionKind.RemainingDurability => "남은 내구도",
            TutorialConditionKind.Removed => "지정 원인으로 제거", TutorialConditionKind.Generated => "파워 생성",
            TutorialConditionKind.Activated => "직접 단독 발동", TutorialConditionKind.Combined => "파워 조합", TutorialConditionKind.ItemUsed => "아이템 사용 성공", TutorialConditionKind.MissionProgress => "미션 진행 증가", _ => kind.ToString()
        };

        public static TutorialConditionDefinition CreateCondition(TutorialConditionKind kind, ElementCatalog catalog, BoardItem item)
        {
            var condition = new TutorialConditionDefinition { kind = kind };
            if (kind >= TutorialConditionKind.DurabilityDecrease && kind <= TutorialConditionKind.Combined) condition.target = new TutorialTargetDefinition();
            if (kind == TutorialConditionKind.ItemUsed) condition.item = item;
            if (kind == TutorialConditionKind.MissionProgress) condition.missionIndex = -1;
            if (kind == TutorialConditionKind.Removed) condition.allowedOrigins.Add(EffectOrigin.Rocket);
            if (kind == TutorialConditionKind.Combined) condition.allowedOrigins.Add(EffectOrigin.RocketBomb);
            if (kind == TutorialConditionKind.Generated || kind == TutorialConditionKind.Activated)
                condition.powerDefinitionId = catalog.Definitions.FirstOrDefault(value => value.Supply?.Behavior == ElementSupplyBehavior.Power)?.Id.Value ?? "";
            condition.authoringId = Guid.NewGuid().ToString("N");
            return condition;
        }
    }
}
