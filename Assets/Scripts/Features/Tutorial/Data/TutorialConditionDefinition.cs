using System;
using System.Collections.Generic;
using Board;
using Levels;
using MemoryPack;
using Simulation;
using UnityEngine;

namespace Tutorial
{
    public enum TutorialConditionKind { SuccessfulSwap, Match, DurabilityDecrease, RemainingDurability, Removed, Generated, Activated, Combined, ItemUsed, MissionProgress }
    public enum TutorialDamageAggregation { Total, Each }
    public enum TutorialConditionCombination { All, Any }
    public enum TutorialMatchSizeComparison { [InspectorName("정확히")] Exactly, [InspectorName("이상")] AtLeast }
    public enum TutorialMatchOrigin { [InspectorName("직접 교환으로 만든 매칭만")] DirectSwap, [InspectorName("연쇄 매칭까지 포함")] IncludingCascade }

    /// <summary>동작 허용과 분리된 조립 조건. 집계 상태는 실행기가 별도로 소유한다.</summary>
    [Serializable, MemoryPackable(SerializeLayout.Explicit)]
    public partial class TutorialConditionDefinition
    {
        [MemoryPackOrder(0)] public TutorialConditionKind kind;
        [MemoryPackOrder(1)] public int requiredCount = 1;
        [MemoryPackOrder(2)] public int matchSize = 3;
        [MemoryPackOrder(3)] public TutorialMatchSizeComparison sizeComparison;
        [MemoryPackOrder(4)] public TutorialMatchOrigin origin;
        [MemoryPackOrder(5)] public bool anyColor = true;
        [MemoryPackOrder(6)] public RabbitColor color;
        // 팩4 조건 레이아웃은 유지하며 새 세부값은 팩5에 저장한다.
        [MemoryPackIgnore] public TutorialTargetDefinition target;
        [MemoryPackIgnore] public TutorialDamageAggregation aggregation;
        [MemoryPackIgnore] public List<EffectOrigin> allowedOrigins = new List<EffectOrigin>();
        [MemoryPackIgnore] public string powerDefinitionId = "";
        [MemoryPackIgnore] public bool anyDirection = true;
        [MemoryPackIgnore] public RocketDirection rocketDirection;
        [MemoryPackIgnore] public string bindGeneratedAs = "";
        [MemoryPackIgnore] public BoardItem item;
        [MemoryPackIgnore] public int missionIndex;
        [MemoryPackIgnore] public MissionKind missionKind = (MissionKind)(-1);
        [MemoryPackIgnore] public RabbitColor missionColor;

        public void SelectMission(int index, LevelMissionDefinition mission)
        { missionIndex = index; missionKind = mission.Kind; missionColor = mission.Color; }

        public TutorialConditionDefinition Copy() => new TutorialConditionDefinition
        {
            kind = kind, requiredCount = requiredCount, matchSize = matchSize, sizeComparison = sizeComparison,
            origin = origin, anyColor = anyColor, color = color, aggregation = aggregation,
            powerDefinitionId = powerDefinitionId, anyDirection = anyDirection, rocketDirection = rocketDirection, bindGeneratedAs = bindGeneratedAs, item = item, missionIndex = missionIndex,
            missionKind = missionKind, missionColor = missionColor,
            allowedOrigins = allowedOrigins == null ? null : new List<EffectOrigin>(allowedOrigins),
            target = target == null ? null : new TutorialTargetDefinition
            {
                kind = target.kind, layer = target.layer, coordinate = target.coordinate, definitionId = target.definitionId,
                binding = target.binding, cells = target.cells == null ? null : new List<BoardCoordinate>(target.cells)
            }
        };
    }
}
