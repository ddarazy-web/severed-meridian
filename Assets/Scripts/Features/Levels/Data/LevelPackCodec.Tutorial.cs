using System;
using System.Collections.Generic;
using System.Linq;
using Elements;
using MemoryPack;
using Tutorial;
using UnityEngine;

namespace Levels
{
    public static partial class LevelPackCodec
    {
        public const int TutorialFormatVersion = 3;
        private static readonly byte[] TutorialMagic = { 0x54, 0x46, 0x50, 0x4b };

        public static byte[] EncodeWithTutorial(IEnumerable<LevelDefinition> levels, ElementCatalog catalog)
        {
            if (levels == null) throw new ArgumentNullException(nameof(levels));
            LevelDefinition[] source = levels.OrderBy(level => level.LevelNumber).ToArray();
            foreach (LevelDefinition level in source)
            {
                List<LevelValidationIssue> issues = LevelTutorialValidator.Validate(level, catalog);
                if (issues.Count > 0) throw new ArgumentException(string.Join("; ", issues));
            }
            if (!source.Any(level => level.HasTutorial)) return Encode(source, catalog);
            PackedTutorialLevelPack pack = new PackedTutorialLevelPack
            {
                FirstLevel = FirstLevel(source[0].LevelNumber),
                ElementPack = EncodeElements(source, catalog, source.SelectMany(level => LevelTutorialValidator.References(level.Tutorial))),
                // 모든 레벨에 기록을 둬서 메타데이터 누락과 중복을 검출한다.
                Tutorials = source.Select(level => new PackedLevelTutorial
                { LevelNumber = level.LevelNumber, Tutorial = level.HasTutorial ? level.Tutorial : null }).ToArray()
            };
            byte[] payload = MemoryPackSerializer.Serialize(pack), bytes = new byte[payload.Length + 8];
            Array.Copy(TutorialMagic, bytes, 4); bytes[4] = TutorialFormatVersion;
            Array.Copy(payload, 0, bytes, 8, payload.Length); return EncodeComposer(bytes, pack);
        }

        private static bool IsTutorialPack(byte[] bytes) => bytes != null && bytes.Length >= 4 &&
            Enumerable.Range(0, 4).All(index => bytes[index] == TutorialMagic[index]);

        public static PackedTutorialLevelPack DecodeTutorial(byte[] bytes)
            => DecodeTutorial(bytes, true);

        private static PackedTutorialLevelPack DecodeTutorial(byte[] bytes, bool validate)
        {
            if (!IsTutorialPack(bytes) || bytes.Length < 8 || (bytes[4] != TutorialFormatVersion && bytes[4] != 4 && bytes[4] != 5) || bytes[5] != 0 || bytes[6] != 0 || bytes[7] != 0)
                throw new InvalidOperationException("지원하지 않는 튜토리얼 팩 헤더입니다.");
            if (bytes[4] == 5) return DecodeConditionDetails(bytes);
            if (bytes[4] == 4) return DecodeComposer(bytes);
            PackedTutorialLevelPack pack = MemoryPackSerializer.Deserialize<PackedTutorialLevelPack>(bytes.AsSpan(8));
            if (validate) ValidateTutorialPack(pack);
            return pack;
        }

        private static void ValidateTutorialPack(PackedTutorialLevelPack pack)
        {
            if (pack == null || pack.FormatVersion != TutorialFormatVersion || pack.Tutorials == null)
                throw new InvalidOperationException("튜토리얼 팩 버전·메타데이터 오류입니다.");
            ElementLevelPack inner = DecodeElements(pack.ElementPack);
            if (inner.FirstLevel != pack.FirstLevel || pack.Tutorials.Length != inner.Levels.Length ||
                pack.Tutorials.Any(item => item == null) || pack.Tutorials.Select(item => item.LevelNumber).Distinct().Count() != inner.Levels.Length ||
                !pack.Tutorials.Select(item => item.LevelNumber).OrderBy(number => number).SequenceEqual(inner.Levels.Select(item => item.LevelNumber).OrderBy(number => number)) ||
                !pack.Tutorials.Any(item => item.Tutorial?.steps?.Count > 0))
                throw new InvalidOperationException("튜토리얼 팩 구간·누락·중복 오류입니다.");
            ElementCatalog catalog = new ElementCatalog(inner.Definitions.Select(value => value.ToDefinition()));
            foreach (PackedLevelTutorial entry in pack.Tutorials)
            {
                LevelDefinition level = LevelDefinition.FromElementPacked(inner.Levels.Single(item => item.LevelNumber == entry.LevelNumber), catalog);
                try
                {
                    level.RestoreTutorial(entry.Tutorial);
                    List<LevelValidationIssue> issues = LevelTutorialValidator.Validate(level, catalog);
                    if (issues.Count > 0) throw new ArgumentException(string.Join("; ", issues));
                }
                finally { if (Application.isPlaying) UnityEngine.Object.Destroy(level); else UnityEngine.Object.DestroyImmediate(level); }
            }
            DefinitionClosure(inner.Levels, catalog, false, pack.Tutorials.SelectMany(item => LevelTutorialValidator.References(item.Tutorial)));
        }

        private static LevelWithCatalog ReadTutorialLevel(byte[] bytes, int number)
        {
            PackedTutorialLevelPack pack = DecodeTutorial(bytes);
            LevelWithCatalog result = ReadLevelWithCatalog(pack.ElementPack, number);
            result.Level.RestoreTutorial(pack.Tutorials.Single(item => item.LevelNumber == number).Tutorial);
            return result;
        }
    }
}
