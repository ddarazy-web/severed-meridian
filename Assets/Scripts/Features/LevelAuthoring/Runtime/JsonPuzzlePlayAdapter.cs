#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using GameScreen;
using LevelAuthoring.Documents;
using Levels;

namespace LevelAuthoring.Runtime
{
    public static class JsonPuzzlePlayAdapter
    {
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
