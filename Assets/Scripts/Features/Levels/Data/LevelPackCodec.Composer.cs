using System;
using System.Collections.Generic;
using System.Linq;
using MemoryPack;
using Tutorial;

namespace Levels
{
    [MemoryPackable(SerializeLayout.Explicit)]
    public partial class PackedTutorialComposer
    {
        [MemoryPackOrder(0)] public byte[] LegacyPack;
        [MemoryPackOrder(1)] public PackedTutorialComposerStep[] Steps;
    }

    [MemoryPackable(SerializeLayout.Explicit)]
    public partial class PackedTutorialComposerStep
    {
        [MemoryPackOrder(0)] public int LevelNumber;
        [MemoryPackOrder(1)] public int StepIndex;
        [MemoryPackOrder(2)] public TutorialConditionDefinition[] Conditions;
        [MemoryPackOrder(3)] public TutorialConditionCombination Combination;
        [MemoryPackOrder(4)] public bool AutomaticHighlights;
    }

    public static partial class LevelPackCodec
    {
        private static byte[] EncodeComposer(byte[] legacy, PackedTutorialLevelPack source)
        {
            PackedTutorialComposerStep[] entries = source.Tutorials
                .SelectMany(level => (level.Tutorial?.steps ?? new List<TutorialStepDefinition>())
                    .Select((step, index) => new PackedTutorialComposerStep
                    {
                        LevelNumber = level.LevelNumber, StepIndex = index,
                        Conditions = step.conditions.ToArray(), Combination = step.combination,
                        AutomaticHighlights = step.automaticHighlights
                    })).ToArray();
            if (!entries.Any(step => step.Conditions.Length > 0 || step.AutomaticHighlights || step.Combination != TutorialConditionCombination.All) &&
                !source.Tutorials.Any(level => level.Tutorial?.steps.Any(step => step.freeItemCount != 1 || step.actionArea?.Count > 0) == true)) return legacy;
            byte[] payload = MemoryPackSerializer.Serialize(new PackedTutorialComposer { LegacyPack = legacy, Steps = entries });
            byte[] bytes = new byte[payload.Length + 8];
            Array.Copy(TutorialMagic, bytes, 4); bytes[4] = 4;
            Array.Copy(payload, 0, bytes, 8, payload.Length);
            return EncodeConditionDetails(bytes, source);
        }

        private static PackedTutorialLevelPack DecodeComposer(byte[] bytes, bool validate = true)
        {
            PackedTutorialComposer data = MemoryPackSerializer.Deserialize<PackedTutorialComposer>(bytes.AsSpan(8));
            if (data?.LegacyPack == null || data.LegacyPack.Length < 8 || data.LegacyPack[4] != 3 || data.Steps == null)
                throw new InvalidOperationException("조합형 팩의 기본 데이터가 없습니다.");
            // 확장 필드를 복원한 최종 계약에서 전체 검사한다.
            PackedTutorialLevelPack pack = DecodeTutorial(data.LegacyPack, false);
            int expected = pack.Tutorials.Sum(level => level.Tutorial?.steps?.Count ?? 0);
            if (data.Steps.Length != expected) throw new InvalidOperationException("조합형 단계 확장 누락입니다.");
            HashSet<(int level, int step)> seen = new HashSet<(int level, int step)>();
            foreach (PackedTutorialComposerStep entry in data.Steps)
            {
                if (entry == null || !seen.Add((entry.LevelNumber, entry.StepIndex)))
                    throw new InvalidOperationException("조합형 단계 확장이 중복이거나 null입니다.");
                PackedLevelTutorial level = pack.Tutorials.SingleOrDefault(value => value.LevelNumber == entry.LevelNumber);
                if (level?.Tutorial?.steps == null || entry.StepIndex < 0 || entry.StepIndex >= level.Tutorial.steps.Count || entry.Conditions == null)
                    throw new InvalidOperationException("조합형 단계 참조 오류입니다.");
                TutorialStepDefinition step = level.Tutorial.steps[entry.StepIndex];
                step.conditions = entry.Conditions.ToList(); step.combination = entry.Combination;
                step.automaticHighlights = entry.AutomaticHighlights;
            }
            if (validate) ValidateTutorialPack(pack);
            return pack;
        }
    }
}
