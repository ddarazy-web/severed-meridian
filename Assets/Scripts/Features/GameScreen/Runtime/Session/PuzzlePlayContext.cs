using System;
using Tutorial;

namespace GameScreen
{
    /// <summary>제품 종류와 별개로 정식 게임과 제작 시험의 저장 정책을 구분한다.</summary>
    public sealed class PuzzlePlayContext
    {
        public bool IsTest { get; }
        public bool AllowLevelAdvance { get; }
        public TutorialExecutionContext Tutorial { get; }

        private PuzzlePlayContext(bool isTest, bool allowLevelAdvance, TutorialExecutionContext tutorial)
        { IsTest = isTest; AllowLevelAdvance = allowLevelAdvance; Tutorial = tutorial; }

        public static PuzzlePlayContext CreatePlayer()
            => new PuzzlePlayContext(false, true, TutorialExecutionContext.CreatePlayer());

        public static PuzzlePlayContext CreateTest(TutorialRunMode mode = TutorialRunMode.Automatic, bool allowLevelAdvance = false)
            => CreateTest(TutorialExecutionContext.CreateTest((int)mode), allowLevelAdvance);

        // Editor SessionState 등 호출자가 소유한 시험 저장소도 같은 실행 경로를 사용한다.
        public static PuzzlePlayContext CreateTest(TutorialExecutionContext tutorial, bool allowLevelAdvance = false)
        {
            if (tutorial == null) throw new ArgumentNullException(nameof(tutorial));
            if (tutorial.UsesPlayerProgress) throw new ArgumentException("시험에는 정식 게임 완료 기록을 사용할 수 없습니다.", nameof(tutorial));
            return new PuzzlePlayContext(true, allowLevelAdvance, tutorial);
        }
    }
}
