using System;
using System.Linq;
using Elements;

namespace Levels
{
    public static partial class LevelPackCodec
    {
        // 게시 세대의 해시 확인은 로더가 담당한다. 여기서는 구조와 참조를 검사하고
        // 실행 객체는 만들지 않는다. 제작용 Decode/ReadLevel의 전체 검사는 유지한다.
        public static PublishedLevelPack DecodePublished(byte[] bytes)
        {
            if (!IsTutorialPack(bytes) && !IsElementPack(bytes))
                return new PublishedLevelPack(DecodeLegacy(bytes));
            PackedTutorialLevelPack tutorial = IsTutorialPack(bytes) ? DecodeTutorial(bytes, false) : null;
            ElementLevelPack elements = DecodeElementPayload(tutorial == null ? bytes : tutorial.ElementPack);
            ElementCatalog catalog = ValidateElementStructure(elements);
            if (tutorial != null && (tutorial.FirstLevel != elements.FirstLevel || tutorial.Tutorials.Length != elements.Levels.Length ||
                !tutorial.Tutorials.Select(entry => entry.LevelNumber).OrderBy(number => number)
                    .SequenceEqual(elements.Levels.Select(entry => entry.LevelNumber).OrderBy(number => number)) ||
                !tutorial.Tutorials.Any(entry => entry.Tutorial?.steps?.Count > 0)))
                throw new InvalidOperationException("튜토리얼 팩 구간·누락·중복 오류입니다.");
            DefinitionClosure(elements.Levels, catalog, false,
                tutorial?.Tutorials.SelectMany(entry => Tutorial.LevelTutorialValidator.References(entry.Tutorial)));
            return new PublishedLevelPack(elements, tutorial, catalog);
        }
    }
}
