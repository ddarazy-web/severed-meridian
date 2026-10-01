using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Levels;
using Simulation;
using UnityEngine;

namespace GameScreen
{
    public sealed class PuzzleBoardPreview : MonoBehaviour
    {
        [SerializeField, Min(1)] private int levelNumber = 1;
        [SerializeField] private int seed = 12345;
        [SerializeField] private PuzzleWorldBoard board;
        [SerializeField] private Camera boardCamera;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private PuzzleArtwork artwork;
        public bool IsReady { get; private set; }
        public string Error { get; private set; }
        public LevelRuntimeState State { get; private set; }
        public void Configure(PuzzleWorldBoard worldBoard, Camera camera) { board = worldBoard; boardCamera = camera; }

        private void Start() => LoadAsync().Forget(Debug.LogException);
        private async UniTask LoadAsync()
        {
            LevelDefinition definition = null;
            CancellationToken token = lifetime.Token;
            artwork = new PuzzleArtwork();
            try
            {
                definition = await LevelPackLoader.LoadAsync(levelNumber);
                token.ThrowIfCancellationRequested();
                LevelStateBuildResult built = LevelStateBuilder.Build(definition, seed);
                if (!built.IsBuilt) throw new InvalidOperationException("레벨 " + levelNumber + " 구성 실패: " + string.Join(", ", built.Issues));
                State = built.State;
                await artwork.PrepareAsync(State, token);
                token.ThrowIfCancellationRequested();
                board.Draw(State, artwork);
                CenterCamera();
                IsReady = true;
            }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                if (!token.IsCancellationRequested)
                {
                    Error = "레벨 " + levelNumber + " 표시 실패: " + error.Message;
                    Debug.LogError(Error, this);
                }
                artwork.Dispose();
            }
            finally { if (definition != null) Destroy(definition); }
        }

        private void CenterCamera()
        {
            Vector3 min = new Vector3(100, 100, 0), max = new Vector3(-100, -100, 0);
            foreach (RuntimeCell cell in State.Cells)
            {
                if (!cell.IsActive) continue;
                Vector3 point = PuzzleWorldBoard.CellPosition(cell.Coordinate);
                min = Vector3.Min(min, point); max = Vector3.Max(max, point);
            }
            Vector3 center = board.transform.TransformPoint((min + max) * 0.5f);
            boardCamera.transform.position = new Vector3(center.x, center.y, -10);
        }
        private void Update()
        {
            // 작은 맵도 기본 보드 기준 크기를 유지한다. 세로 화면에서는 너비를 기준으로 맞춘다.
            if (boardCamera != null) boardCamera.orthographicSize = (PuzzleWorldBoard.HalfHeight + 0.7f) / Mathf.Min(1, boardCamera.aspect);
        }
        private void OnDestroy()
        {
            lifetime.Cancel();
            artwork?.Dispose();
            lifetime.Dispose();
        }
    }
}
