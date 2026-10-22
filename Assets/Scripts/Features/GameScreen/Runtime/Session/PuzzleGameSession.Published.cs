using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameScreen.Navigation;
using Levels;

namespace GameScreen
{
    public sealed partial class PuzzleGameSession
    {
        private PublishedLevelSource publishedSource;
        private LobbyGameRoute publishedRoute;
        public event Action LobbyRequested;
        public bool CanReturnToLobby => publishedRoute != null && LobbyRequested != null;
        public bool HasNextRegisteredLevel => publishedRoute == null || publishedRoute.TryGetNext(levelNumber, out _);

        public void RequestLobby()
        {
            if (CanReturnToLobby) LobbyRequested.Invoke();
        }

        // 씬 전환자는 요청을 전달하고, 세션은 선택 구간 데이터의 수명을 소유한다.
        public UniTask InitializePublishedAsync(GameLevelRequest request, LobbyGameRoute route, int randomSeed, CancellationToken token)
        {
            if (started) throw new InvalidOperationException("이미 시작된 게임 세션입니다.");
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (route == null) throw new ArgumentNullException(nameof(route));
            if (!route.TryCreateRequest(request.LevelId, request.LevelNumber, request.Generation, out _))
                throw new ArgumentException("로비 목록과 일치하지 않는 게임 진입 요청입니다.", nameof(request));
            publishedRoute = route;
            publishedSource = new PublishedLevelSource(request.Generation);
            return InitializeCoreAsync(null, request.LevelNumber, randomSeed, token, context: PuzzlePlayContext.CreatePlayer(), explicitRequest: true);
        }
    }
}
