using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Levels;
using Simulation;
using UnityEngine;

namespace GameScreen
{
    public sealed partial class PuzzleGameSession
    {
        private bool levelAdvanceEnabled = true;
        private bool levelTransitionRefreshPending;
        public bool LevelAdvanceEnabled => levelAdvanceEnabled;
        public bool IsChangingLevel { get; private set; }
        public bool CanAdvanceLevel => isActiveAndEnabled && levelAdvanceEnabled && ResultReady &&
            Outcome.Kind == BoardOutcomeKind.Won && !IsChangingLevel && !IsPaused && !audioBackground;

        public void SetLevelAdvanceEnabled(bool enabled)
        {
            if (started) throw new System.InvalidOperationException("실행 소스는 시작 전에 설정하세요.");
            levelAdvanceEnabled = enabled;
        }

        private void OnEnable()
        {
            // 비활성 상태에서 취소한 전환의 버튼 잠금만 복귀 시 한 번 갱신한다.
            if (!levelTransitionRefreshPending) return;
            levelTransitionRefreshPending = false;
            if (!ready) return;
            try { Changed?.Invoke(); }
            catch (Exception error) { Debug.LogException(error); }
        }

        // 후보는 표시 성공까지 현재 승리 상태·재시작 기준과 분리해 보유한다.
        public async UniTask<bool> AdvanceLevelAsync(CancellationToken token)
        {
            if (!CanAdvanceLevel) return false;
            IsChangingLevel = true;
            string previousMessage = Message;
            LevelDefinition definition = null;
            PuzzleArtwork candidateArtwork = null;
            using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token);
            try
            {
                int nextNumber;
                byte[] candidateBytes;
                BoardActionExecutor candidateExecutor;
                // 복구 가능한 준비 실패는 상태 교체 전에만 처리한다.
                try
                {
                    Message = "다음 레벨 준비 중"; Changed?.Invoke();
                    linked.Token.ThrowIfCancellationRequested();
                    nextNumber = checked(levelNumber + 1);
                    definition = await LevelPackLoader.LoadAsync(nextNumber);
                    linked.Token.ThrowIfCancellationRequested();
                    if (!isActiveAndEnabled) throw new OperationCanceledException();
                    candidateBytes = LevelPackCodec.Snapshot(definition);
                    StartingBoardSearch search = new StartingBoardSearch(definition, seed);
                    while (!search.IsDone)
                    {
                        linked.Token.ThrowIfCancellationRequested();
                        if (!isActiveAndEnabled) throw new OperationCanceledException();
                        search.Advance(128);
                        await UniTask.Yield(PlayerLoopTiming.Update, linked.Token);
                    }
                    if (search.Status != StartingBoardStatus.Success) throw new InvalidOperationException(search.Message);
                    candidateExecutor = new BoardActionExecutor(search.State);
                    candidateArtwork = new PuzzleArtwork();
                    await candidateArtwork.PrepareAsync(candidateExecutor.State, linked.Token);
                    // 아틀라스 로드 성공만으로는 HUD 전용 미션 그림의 존재를 보장하지 않는다.
                    foreach (RuntimeMission mission in candidateExecutor.State.Missions)
                        candidateArtwork.Get(PuzzleArtworkPaths.Mission(mission.Definition));
                    // 준비는 계속하되 사용자가 돌아와 재개하기 전에는 새 판을 시작하지 않는다.
                    while (audioBackground || IsPaused)
                    {
                        if (!isActiveAndEnabled) throw new OperationCanceledException();
                        await UniTask.Yield(PlayerLoopTiming.Update, linked.Token);
                    }
                    linked.Token.ThrowIfCancellationRequested();
                    if (!isActiveAndEnabled) throw new OperationCanceledException();

                    // 이미지 표시가 실패해도 이전 판의 상태와 재시작 기준을 유지한다.
                    try { board.Draw(candidateExecutor.State, candidateArtwork); }
                    catch { board.Draw(State, artwork); throw; }
                }
                catch (OperationCanceledException) { Message = previousMessage; return false; }
                catch (Exception error)
                { Message = "다음 레벨을 불러올 수 없습니다: " + error.Message; return false; }

                PuzzleArtwork previousArtwork = artwork;
                ClearProgress(); ResetPresentation();
                executor = candidateExecutor; artwork = candidateArtwork; candidateArtwork = null;
                initialBytes = candidateBytes; levelNumber = nextNumber;
                LogicalSessionId = Guid.NewGuid().ToString("N");
                previousArtwork?.Dispose();
                InitializeProgress(); CenterCamera();
                Message = "블록을 선택하거나 드래그하세요";
                // 이미 교체한 판의 외부 알림 오류를 준비 실패/이전 Retry 보존으로 오표시하지 않는다.
                try { BeginStartFeedback(); }
                catch (Exception error) { Debug.LogException(error); }
                return true;
            }
            finally
            {
                candidateArtwork?.Dispose();
                if (definition != null) Destroy(definition);
                IsChangingLevel = false;
                if (this != null && ready)
                {
                    levelTransitionRefreshPending = !isActiveAndEnabled;
                    if (!levelTransitionRefreshPending)
                    {
                        try { Changed?.Invoke(); }
                        catch (Exception error) { Debug.LogException(error); }
                    }
                }
            }
        }
    }
}
