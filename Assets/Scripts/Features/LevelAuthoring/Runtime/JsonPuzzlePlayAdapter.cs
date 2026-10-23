#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Linq;
using GameScreen;
using LevelAuthoring.Documents;
using Levels;
using Tutorial;

namespace LevelAuthoring.Runtime
{
    public static class JsonPuzzlePlayAdapter
    {
        public static PuzzlePlayRequest CreateTutorialRequest(ContentSnapshot snapshot, string levelId, int seed, int lastStep)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (snapshot.Get(levelId).Kind != "level") throw new ContentFormatException("시험할 레벨 문서가 아닙니다.");
            using var graph = new AuthoringObjectGraph(snapshot.Documents);
            LevelDefinition level = (LevelDefinition)graph.Resolve(levelId);
            LevelTutorialDefinition tutorial = TutorialFlowResolver.Resolve(level);
            if (tutorial?.steps == null || lastStep < 0 || lastStep >= tutorial.steps.Count)
                throw new ArgumentOutOfRangeException(nameof(lastStep), "시험할 마지막 단계를 선택하세요.");
            // 독립 그래프의 실행 사본만 자른다. 초기 보드·공급과 첫 단계부터 그대로 실행한다.
            level.Tutorial.flow = null; level.Tutorial.bindings.Clear();
            level.Tutorial.steps = tutorial.steps.Take(lastStep + 1).ToList();
            level.Tutorial.supply = tutorial.supply;
            return PuzzlePlayRequest.Capture(level, seed);
        }

        public static PuzzlePlayRequest CreateRequest(ContentSnapshot snapshot, string levelId, int seed)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (snapshot.Get(levelId).Kind != "level") throw new ContentFormatException("게임 입력은 level 문서여야 합니다.");
            using var graph = new AuthoringObjectGraph(snapshot.Documents);
            // 공유 흐름은 Capture 내부의 기존 해석기를 통해 실행 사본에서만 펼친다.
            return PuzzlePlayRequest.Capture((LevelDefinition)graph.Resolve(levelId), seed);
        }
    }
}
#endif
