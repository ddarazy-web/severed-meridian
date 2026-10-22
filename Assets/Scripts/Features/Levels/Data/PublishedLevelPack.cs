using System;
using System.Collections.Generic;
using System.Linq;
using Elements;
using MemoryPack;
using UnityEngine;

namespace Levels
{
    // 한 구간의 해석 결과만 소유한다. 호출자가 수명을 정하고 전역 캐시에 남기지 않는다.
    public sealed class PublishedLevelPack
    {
        private readonly LevelPack legacy;
        private readonly ElementLevelPack elements;
        private readonly PackedTutorialLevelPack tutorials;
        private readonly ElementCatalog catalog;
        public int FirstLevel => legacy?.FirstLevel ?? elements.FirstLevel;

        internal PublishedLevelPack(LevelPack pack) { legacy = pack; }
        internal PublishedLevelPack(ElementLevelPack pack, PackedTutorialLevelPack tutorial, ElementCatalog definitions)
        { elements = pack; tutorials = tutorial; catalog = definitions; }

        // 생성한 레벨은 호출자가 해제한다. 선택 레벨만 복사하여 재시작/다음 레벨이
        // 저장된 팩의 가변 목록이나 튜토리얼 상태를 변경하지 않게 한다.
        public LevelDefinition CreateLevel(int number)
        {
            if (LevelPackCodec.FirstLevel(number) != FirstLevel) throw new InvalidOperationException("요청한 레벨 구간과 파일이 다릅니다.");
            LevelDefinition level;
            if (legacy != null)
            {
                PackedLevel data = legacy.Levels.SingleOrDefault(entry => entry.LevelNumber == number);
                if (data == null) throw new KeyNotFoundException($"MemoryPack에 레벨 {number}이 없습니다.");
                level = LevelDefinition.FromPacked(MemoryPackSerializer.Deserialize<PackedLevel>(MemoryPackSerializer.Serialize(data)));
            }
            else
            {
                PackedElementLevel data = elements.Levels.SingleOrDefault(entry => entry.LevelNumber == number);
                if (data == null) throw new KeyNotFoundException($"MemoryPack에 레벨 {number}이 없습니다.");
                level = LevelDefinition.FromElementPacked(MemoryPackSerializer.Deserialize<PackedElementLevel>(MemoryPackSerializer.Serialize(data)), catalog);
            }
            try
            {
                Tutorial.LevelTutorialDefinition tutorial = tutorials?.Tutorials.Single(entry => entry.LevelNumber == number).Tutorial;
                if (tutorial != null) level.RestoreTutorial(JsonUtility.FromJson<Tutorial.LevelTutorialDefinition>(JsonUtility.ToJson(tutorial)));
                List<LevelValidationIssue> issues = LevelDefinitionValidator.Validate(level);
                if (issues.Count > 0) throw new ArgumentException($"레벨 {number}: " + string.Join("; ", issues));
                return level;
            }
            catch
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(level); else UnityEngine.Object.DestroyImmediate(level);
                throw;
            }
        }
    }
}
