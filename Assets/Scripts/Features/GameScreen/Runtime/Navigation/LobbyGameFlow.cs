using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Lobby;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameScreen.Navigation
{
    // 게임 준비 후 로비 씬을 해제한다. 전환 객체만 게임 씬으로 옮겨 복귀를 소유한다.
    public sealed class LobbyGameFlow : MonoBehaviour
    {
        [SerializeField] private LobbyScreen screen;
        [SerializeField] private Canvas lobbyCanvas;
        [SerializeField] private Camera lobbyCamera;
        [SerializeField] private GameObject lobbyInput;
        [SerializeField] private string gameScenePath = "Assets/Scenes/PuzzleGame.unity";
        private CancellationTokenSource entering;
        private Scene gameScene;
        private Scene lobbyScene;
        private PuzzleGameSession session;
        private bool busy;
        private string lobbyScenePath;
        private LobbyIndexSnapshot returnSnapshot;
        private float returnOffset;
        public bool IsBusy => busy;

        private void Awake()
        {
            lobbyScene = gameObject.scene;
            lobbyScenePath = lobbyScene.path;
            screen.LevelSelected += Select;
            screen.CancelRequested += Cancel;
        }

        private void Select(string id, int number, string generation)
        {
            if (busy || gameScene.IsValid() || screen.Snapshot == null) return;
            var route = new LobbyGameRoute(screen.Snapshot);
            if (!route.TryCreateRequest(id, number, generation, out GameLevelRequest request)) return;
            EnterAsync(request, route).Forget(Debug.LogException);
        }

        private void Cancel()
        {
            if (entering == null || entering.IsCancellationRequested) return;
            entering.Cancel();
            screen.ShowTransition("취소 중…", false);
        }

        private async UniTask EnterAsync(GameLevelRequest request, LobbyGameRoute route)
        {
            busy = true;
            entering = new CancellationTokenSource();
            CancellationToken token = entering.Token;
            UniTask initialization = UniTask.CompletedTask;
            bool initialized = false, success = false;
            string failure = null;
            void Loaded(Scene scene, LoadSceneMode mode)
            {
                if (scene.path != gameScenePath || mode != LoadSceneMode.Additive) return;
                gameScene = scene;
                SceneManager.SetActiveScene(scene);
                try
                {
                    foreach (GameObject root in scene.GetRootGameObjects())
                        foreach (PuzzleGameSession candidate in root.GetComponentsInChildren<PuzzleGameSession>(true))
                        {
                            if (session != null) throw new InvalidOperationException("게임 씬에 세션이 둘 이상 있습니다.");
                            session = candidate;
                        }
                    if (session == null) throw new InvalidOperationException("게임 씬에 세션이 없습니다.");
                    initialized = true;
                    initialization = session.InitializePublishedAsync(request, route, UnityEngine.Random.Range(1, int.MaxValue), token).Preserve();
                }
                catch (Exception error) { initialization = UniTask.FromException(error); initialized = true; }
            }
            try
            {
                screen.ShowTransition($"레벨 {request.LevelNumber} 준비 중…", true);
                SceneManager.sceneLoaded += Loaded;
                // 씬 로드는 중간에 해제하지 않는다. 취소돼도 로드 완료를 기다려 소유 씬을 회수한다.
                AsyncOperation load = SceneManager.LoadSceneAsync(gameScenePath, LoadSceneMode.Additive);
                if (load == null) throw new InvalidOperationException("게임 씬을 불러올 수 없습니다.");
                load.allowSceneActivation = false;
                while (load.progress < 0.9f) await UniTask.Yield();
                // 읽는 동안에는 로비 취소 버튼을 유지하고, 새 EventSystem의 OnEnable 직전에 교대한다.
                if (lobbyInput != null) lobbyInput.SetActive(false);
                load.allowSceneActivation = true;
                await load.ToUniTask();
                if (!initialized) throw new InvalidOperationException("게임 시작 요청이 전달되지 않았습니다.");
                await initialization;
                token.ThrowIfCancellationRequested();
                session.LobbyRequested += Return;
                returnSnapshot = screen.Snapshot;
                returnOffset = screen.ScrollOffset;
                screen.LevelSelected -= Select;
                screen.CancelRequested -= Cancel;
                SceneManager.MoveGameObjectToScene(gameObject, gameScene);
                await SceneManager.UnloadSceneAsync(lobbyScene).ToUniTask();
                screen = null;
                lobbyCanvas = null;
                lobbyCamera = null;
                lobbyInput = null;
                await Resources.UnloadUnusedAssets().ToUniTask();
                success = true;
            }
            catch (OperationCanceledException) { failure = "게임 진입을 취소했습니다. 다시 선택하세요."; }
            catch (Exception error) { failure = "게임을 준비하지 못했습니다. " + error.Message; Debug.LogException(error, this); }
            finally
            {
                SceneManager.sceneLoaded -= Loaded;
                entering.Dispose(); entering = null;
                try
                {
                    if (!success)
                    {
                        await UnloadGameAsync();
                        if (this != null) screen.ShowSelection(failure, true);
                    }
                }
                finally { busy = false; }
            }
        }

        private void Return()
        {
            if (!busy) ReturnAsync().Forget(Debug.LogException);
        }

        private async UniTask ReturnAsync()
        {
            busy = true;
            UnityEngine.EventSystems.EventSystem gameInput = null;
            try
            {
                foreach (GameObject root in gameScene.GetRootGameObjects())
                {
                    gameInput = root.GetComponentInChildren<UnityEngine.EventSystems.EventSystem>();
                    if (gameInput != null) break;
                }
                if (gameInput != null) gameInput.gameObject.SetActive(false);
                await SceneManager.LoadSceneAsync(lobbyScenePath, LoadSceneMode.Additive).ToUniTask();
                Scene restoredScene = SceneManager.GetSceneByPath(lobbyScenePath);
                LobbyGameFlow restored = null;
                foreach (GameObject root in restoredScene.GetRootGameObjects())
                {
                    restored = root.GetComponent<LobbyGameFlow>();
                    if (restored != null) break;
                }
                if (restored == null) throw new InvalidOperationException("로비 전환 객체가 없습니다.");
                restored.busy = true;
                restored.screen.RestoreSelection(returnSnapshot, returnOffset);
                restored.screen.ShowTransition("로비로 돌아가는 중…", false);
                SceneManager.SetActiveScene(restoredScene);
                await UnloadGameAsync();
                restored.screen.ShowSelection();
                restored.busy = false;
            }
            finally
            {
                if (gameInput != null) gameInput.gameObject.SetActive(true);
                busy = false;
            }
        }

        private async UniTask UnloadGameAsync()
        {
            if (session != null) session.LobbyRequested -= Return;
            session = null;
            if (lobbyScene.IsValid() && lobbyScene.isLoaded) SceneManager.SetActiveScene(lobbyScene);
            if (gameScene.IsValid() && gameScene.isLoaded)
                await SceneManager.UnloadSceneAsync(gameScene).ToUniTask();
            gameScene = default;
            // 씬 직접 참조 UI/폰트도 게임을 나간 뒤 반환한다.
            await Resources.UnloadUnusedAssets().ToUniTask();
            if (this == null) return;
            lobbyCamera.enabled = true;
            lobbyCanvas.enabled = true;
            lobbyInput.SetActive(true);
        }

        private void OnDestroy()
        {
            if (screen != null)
            {
                screen.LevelSelected -= Select;
                screen.CancelRequested -= Cancel;
            }
            entering?.Cancel();
            if (session != null) session.LobbyRequested -= Return;
        }
    }
}
