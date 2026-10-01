using System;
using System.Threading;
using Board;
using Cysharp.Threading.Tasks;
using Levels;
using Simulation;
using UnityEngine;

namespace GameScreen
{
    public sealed partial class PuzzleGameSession : MonoBehaviour
    {
        [SerializeField, Min(1)] private int levelNumber = 1;
        [SerializeField] private int seed = 12345;
        [SerializeField] private PuzzleWorldBoard board;
        [SerializeField] private Camera boardCamera;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private PuzzleArtwork artwork;
        private BoardActionExecutor executor;
        private bool started;
        private bool ready;
        private bool failed;
        public LevelRuntimeState State => executor?.State;
        public BoardOutcome Outcome => executor?.Outcome;
        public BoardActionPhase Phase => executor?.Phase ?? BoardActionPhase.Stopped;
        public bool CanAcceptInput => isActiveAndEnabled && ready && !failed && !IsPaused && !IsRestarting &&
            !IsPresenting && Phase == BoardActionPhase.Ready && Outcome == null;
        public string Message { get; private set; } = "레벨 로딩 중";
        public event Action Changed;

        public void Configure(PuzzleWorldBoard worldBoard, Camera camera)
        { board = worldBoard; boardCamera = camera; }

        private void Start()
        {
            if (!started) InitializeAsync(levelNumber, seed, CancellationToken.None).Forget(Debug.LogException);
        }

        public UniTask InitializeAsync(int number, int randomSeed, CancellationToken token)
            => InitializeCoreAsync(null, number, randomSeed, token);

        // 호출 시 사본의 소유권을 받는다. 성공·실패·취소 모두 이 세션이 반환한다.
        public UniTask InitializeAsync(LevelDefinition ownedDefinition, int randomSeed, CancellationToken token)
            => InitializeCoreAsync(ownedDefinition, ownedDefinition != null ? ownedDefinition.LevelNumber : 0, randomSeed, token);

        private async UniTask InitializeCoreAsync(LevelDefinition definition, int number, int randomSeed, CancellationToken token)
        {
            if (started)
            {
                if (definition != null) Destroy(definition);
                throw new InvalidOperationException("이미 시작된 게임 세션입니다.");
            }
            started = true;
            using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token);
            try
            {
                linked.Token.ThrowIfCancellationRequested();
                if (definition == null) definition = await LevelPackLoader.LoadAsync(number);
                linked.Token.ThrowIfCancellationRequested();
                initialBytes = LevelPackCodec.Encode(new[] { definition });
                levelNumber = number; seed = randomSeed;
                await PrepareAsync(definition, randomSeed, linked.Token);
            }
            catch (OperationCanceledException) { ready = false; artwork?.Dispose(); }
            catch (Exception error) { Fail("레벨 " + number + " 시작 실패: " + error.Message); }
            finally
            {
                if (definition != null) Destroy(definition);
            }
        }

        private async UniTask PrepareAsync(LevelDefinition definition, int randomSeed, CancellationToken token)
        {
            Message = "시작 보드 구성 중"; Changed?.Invoke();
            StartingBoardSearch search = new StartingBoardSearch(definition, randomSeed);
            while (!search.IsDone)
            {
                token.ThrowIfCancellationRequested();
                search.Advance(128);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
            token.ThrowIfCancellationRequested();
            if (search.Status != StartingBoardStatus.Success) throw new InvalidOperationException(search.Message);
            executor = new BoardActionExecutor(search.State);
            artwork = new PuzzleArtwork();
            await artwork.PrepareAsync(State, token);
            token.ThrowIfCancellationRequested();
            board.Draw(State, artwork);
            CenterCamera();
            ready = true;
            Message = "블록을 선택하거나 드래그하세요"; Changed?.Invoke();
        }

        public bool TryActivate(BoardCoordinate at)
            => CanAcceptInput && Apply(executor.Activate(at));

        private bool Apply(BoardActionResult result)
        {
            Message = result.Message;
            if (result.IsApplied) Draw();
            else Changed?.Invoke();
            return result.IsApplied;
        }

        private void Update()
        {
            if (boardCamera != null && !HasScreenLayout) boardCamera.orthographicSize = (PuzzleWorldBoard.HalfHeight + 0.7f) / Mathf.Min(1, boardCamera.aspect);
            if (IsPresenting) { AdvancePresentation(Time.deltaTime); return; }
            Advance();
        }

        private void Advance()
        {
            if (!ready || failed || IsPaused || IsRestarting || IsPresenting || !executor.HasPendingCascade) return;
            try
            {
                CascadeStepResult step = executor.AdvanceCascade();
                Message = step.Message;
                Draw();
            }
            catch (Exception error) { Fail("게임 처리 중단: " + error.Message); }
        }

        private void Draw()
        {
            try { board.Draw(State, artwork); Changed?.Invoke(); }
            catch (Exception error) { Fail("보드 표시 중단: " + error.Message); }
        }

        private void Fail(string message)
        {
            failed = true; ready = false; Message = message;
            ResetPresentation();
            artwork?.Dispose();
            if (this != null && !lifetime.IsCancellationRequested) Changed?.Invoke();
            Debug.LogError(message);
        }

        private void CenterCamera()
        {
            Vector3 min = Vector3.one * 100, max = Vector3.one * -100;
            foreach (RuntimeCell cell in State.Cells)
            {
                if (!cell.IsActive) continue;
                Vector3 point = PuzzleWorldBoard.CellPosition(cell.Coordinate);
                min = Vector3.Min(min, point); max = Vector3.Max(max, point);
            }
            Vector3 center = board.transform.TransformPoint((min + max) * 0.5f);
            boardCamera.transform.position = center - boardCamera.transform.forward * 10;
        }

        private void OnDestroy()
        {
            ready = false;
            ResetPresentation();
            lifetime.Cancel(); artwork?.Dispose(); lifetime.Dispose();
            Changed = null;
        }
    }
}
