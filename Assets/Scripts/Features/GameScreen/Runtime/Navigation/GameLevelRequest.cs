namespace GameScreen.Navigation
{
    // 검증된 로비 목록에서만 만드는 불변 게임 진입 요청.
    public sealed class GameLevelRequest
    {
        public string LevelId { get; }
        public int LevelNumber { get; }
        public string Generation { get; }

        internal GameLevelRequest(string levelId, int levelNumber, string generation)
        {
            LevelId = levelId;
            LevelNumber = levelNumber;
            Generation = generation;
        }
    }
}
