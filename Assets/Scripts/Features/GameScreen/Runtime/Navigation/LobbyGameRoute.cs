using System;
using Lobby;

namespace GameScreen.Navigation
{
    // 로비 선택과 다음 레벨 순서만 담당한다. 씬이나 게임 리소스를 소유하지 않는다.
    public sealed class LobbyGameRoute
    {
        private readonly LobbyIndexSnapshot index;
        public LobbyGameRoute(LobbyIndexSnapshot index)
        {
            this.index = index ?? throw new ArgumentNullException(nameof(index));
        }

        public bool TryCreateRequest(string levelId, int levelNumber, string generation, out GameLevelRequest request)
        {
            request = null;
            if (!string.Equals(index.Generation, generation, StringComparison.Ordinal)) return false;
            int position = Find(levelNumber);
            if (position < 0 || !string.Equals(index.Levels[position].Id, levelId, StringComparison.Ordinal)) return false;
            request = new GameLevelRequest(levelId, levelNumber, generation);
            return true;
        }

        public bool TryGetNext(int levelNumber, out LobbyLevel next)
        {
            next = null;
            int position = Find(levelNumber);
            if (position < 0 || position + 1 >= index.Levels.Count) return false;
            next = index.Levels[position + 1];
            return true;
        }

        private int Find(int number)
        {
            int low = 0, high = index.Levels.Count - 1;
            while (low <= high)
            {
                int middle = low + (high - low) / 2;
                int candidate = index.Levels[middle].Number;
                if (candidate == number) return middle;
                if (candidate < number) low = middle + 1;
                else high = middle - 1;
            }
            return -1;
        }
    }
}
