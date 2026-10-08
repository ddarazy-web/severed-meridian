using System;
using System.Collections.Generic;
using System.Linq;
using MemoryPack;
using Simulation;
using Tutorial;
using Board;

namespace Levels
{
    [MemoryPackable(SerializeLayout.Explicit)]
    public partial class PackedTutorialConditionDetails
    {
        [MemoryPackOrder(0)] public byte[] ComposerPack;
        [MemoryPackOrder(1)] public PackedTutorialConditionDetail[] Conditions;
        [MemoryPackOrder(2)] public PackedTutorialStepDetail[] Steps;
    }

    [MemoryPackable(SerializeLayout.Explicit)]
    public partial class PackedTutorialStepDetail
    {
        [MemoryPackOrder(0)] public int LevelNumber;
        [MemoryPackOrder(1)] public int StepIndex;
        [MemoryPackOrder(2)] public int FreeItemCount;
        [MemoryPackOrder(3)] public BoardCoordinate[] ActionArea;
        [MemoryPackOrder(4)] public string FirstBinding;
        [MemoryPackOrder(5)] public string SecondBinding;
    }

    [MemoryPackable(SerializeLayout.Explicit)]
    public partial class PackedTutorialConditionDetail
    {
        [MemoryPackOrder(0)] public int LevelNumber;
        [MemoryPackOrder(1)] public int StepIndex;
        [MemoryPackOrder(2)] public int ConditionIndex;
        [MemoryPackOrder(3)] public TutorialTargetDefinition Target;
        [MemoryPackOrder(4)] public TutorialDamageAggregation Aggregation;
        [MemoryPackOrder(5)] public EffectOrigin[] AllowedOrigins;
        [MemoryPackOrder(6)] public string PowerDefinitionId;
        [MemoryPackOrder(7)] public bool AnyDirection;
        [MemoryPackOrder(8)] public RocketDirection RocketDirection;
        [MemoryPackOrder(9)] public string BindGeneratedAs;
        [MemoryPackOrder(10)] public BoardItem Item;
        [MemoryPackOrder(11)] public int MissionIndex;
        [MemoryPackOrder(12)] public MissionKind MissionKind;
        [MemoryPackOrder(13)] public RabbitColor MissionColor;
    }

    public static partial class LevelPackCodec
    {
        private static byte[] EncodeConditionDetails(byte[] composer, PackedTutorialLevelPack pack)
        {
            var details = new List<PackedTutorialConditionDetail>();
            PackedTutorialStepDetail[] steps = pack.Tutorials.SelectMany(level => (level.Tutorial?.steps ?? new List<TutorialStepDefinition>()).Select((step, index) =>
                new PackedTutorialStepDetail { LevelNumber = level.LevelNumber, StepIndex = index, FreeItemCount = step.freeItemCount, ActionArea = step.actionArea?.ToArray(), FirstBinding = step.firstBinding, SecondBinding = step.secondBinding })).ToArray();
            foreach (PackedLevelTutorial level in pack.Tutorials)
                for (int step = 0; step < (level.Tutorial?.steps.Count ?? 0); step++)
                    for (int index = 0; index < level.Tutorial.steps[step].conditions.Count; index++)
                    {
                        TutorialConditionDefinition condition = level.Tutorial.steps[step].conditions[index];
                        details.Add(new PackedTutorialConditionDetail { LevelNumber = level.LevelNumber, StepIndex = step, ConditionIndex = index,
                            Target = condition.target, Aggregation = condition.aggregation, AllowedOrigins = condition.allowedOrigins?.ToArray(),
                            PowerDefinitionId = condition.powerDefinitionId, AnyDirection = condition.anyDirection, RocketDirection = condition.rocketDirection,
                            BindGeneratedAs = condition.bindGeneratedAs, Item = condition.item, MissionIndex = condition.missionIndex,
                            MissionKind = condition.missionKind, MissionColor = condition.missionColor });
                    }
            if (!details.Any(value => value.Target != null || value.Aggregation != TutorialDamageAggregation.Total || value.AllowedOrigins?.Length > 0 ||
                !string.IsNullOrEmpty(value.PowerDefinitionId) || !value.AnyDirection || value.RocketDirection != default || !string.IsNullOrEmpty(value.BindGeneratedAs) || value.Item != default) &&
                !details.Any(value => value.MissionIndex != 0) && !steps.Any(step => step.FreeItemCount != 1 || step.ActionArea?.Length > 0 || !string.IsNullOrEmpty(step.FirstBinding) || !string.IsNullOrEmpty(step.SecondBinding)) && !pack.Tutorials.Any(level => level.Tutorial?.steps.Any(step => step.conditions.Any(condition => condition.kind == TutorialConditionKind.ItemUsed || condition.kind == TutorialConditionKind.MissionProgress)) == true)) return composer;
            byte[] payload = MemoryPackSerializer.Serialize(new PackedTutorialConditionDetails { ComposerPack = composer, Conditions = details.ToArray(), Steps = steps });
            byte[] bytes = new byte[payload.Length + 8]; Array.Copy(TutorialMagic, bytes, 4); bytes[4] = 5;
            Array.Copy(payload, 0, bytes, 8, payload.Length); return bytes;
        }

        private static PackedTutorialLevelPack DecodeConditionDetails(byte[] bytes)
        {
            var details = MemoryPackSerializer.Deserialize<PackedTutorialConditionDetails>(bytes.AsSpan(8));
            if (details?.ComposerPack == null || details.ComposerPack.Length < 8 || !IsTutorialPack(details.ComposerPack) ||
                details.ComposerPack[4] != 4 || details.ComposerPack[5] != 0 || details.ComposerPack[6] != 0 || details.ComposerPack[7] != 0 || details.Conditions == null || details.Steps == null)
                throw new InvalidOperationException("조건 세부 팩의 기본 데이터가 없습니다.");
            PackedTutorialLevelPack pack = DecodeComposer(details.ComposerPack, false);
            if (details.Steps.Length != pack.Tutorials.Sum(level => level.Tutorial?.steps.Count ?? 0)) throw new InvalidOperationException("단계 세부 확장 누락입니다.");
            var stepKeys = new HashSet<(int level, int step)>();
            foreach (PackedTutorialStepDetail detail in details.Steps)
            {
                if (detail == null || !stepKeys.Add((detail.LevelNumber, detail.StepIndex))) throw new InvalidOperationException("단계 세부 확장이 중복이거나 null입니다.");
                PackedLevelTutorial level = pack.Tutorials.SingleOrDefault(value => value.LevelNumber == detail.LevelNumber);
                if (level?.Tutorial == null || detail.StepIndex < 0 || detail.StepIndex >= level.Tutorial.steps.Count) throw new InvalidOperationException("단계 세부 참조 오류입니다.");
                level.Tutorial.steps[detail.StepIndex].freeItemCount = detail.FreeItemCount;
                if (detail.ActionArea == null) throw new InvalidOperationException("조작 영역 확장 누락입니다.");
                level.Tutorial.steps[detail.StepIndex].actionArea = detail.ActionArea.ToList();
                level.Tutorial.steps[detail.StepIndex].firstBinding = detail.FirstBinding; level.Tutorial.steps[detail.StepIndex].secondBinding = detail.SecondBinding;
            }
            int expected = pack.Tutorials.Sum(level => level.Tutorial?.steps.Sum(step => step.conditions.Count) ?? 0);
            if (details.Conditions.Length != expected) throw new InvalidOperationException("조건 세부 확장 누락입니다.");
            var seen = new HashSet<(int level, int step, int condition)>();
            foreach (PackedTutorialConditionDetail detail in details.Conditions)
            {
                if (detail == null || !seen.Add((detail.LevelNumber, detail.StepIndex, detail.ConditionIndex)))
                    throw new InvalidOperationException("조건 세부 확장이 중복이거나 null입니다.");
                PackedLevelTutorial level = pack.Tutorials.SingleOrDefault(value => value.LevelNumber == detail.LevelNumber);
                if (level?.Tutorial == null || detail.StepIndex < 0 || detail.StepIndex >= level.Tutorial.steps.Count || detail.ConditionIndex < 0 ||
                    detail.ConditionIndex >= level.Tutorial.steps[detail.StepIndex].conditions.Count || detail.AllowedOrigins == null)
                    throw new InvalidOperationException("조건 세부 참조 오류입니다.");
                TutorialConditionDefinition condition = level.Tutorial.steps[detail.StepIndex].conditions[detail.ConditionIndex];
                condition.target = detail.Target; condition.aggregation = detail.Aggregation; condition.allowedOrigins = detail.AllowedOrigins.ToList();
                condition.powerDefinitionId = detail.PowerDefinitionId; condition.anyDirection = detail.AnyDirection; condition.rocketDirection = detail.RocketDirection;
                condition.bindGeneratedAs = detail.BindGeneratedAs;
                condition.item = detail.Item;
                condition.missionIndex = detail.MissionIndex;
                condition.missionKind = detail.MissionKind; condition.missionColor = detail.MissionColor;
            }
            ValidateTutorialPack(pack); return pack;
        }
    }
}
