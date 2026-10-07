using System;
using System.Threading;
using Board;
using Cysharp.Threading.Tasks;
using Levels;
using Simulation;
using UnityEngine;

namespace GameScreen
{
    public sealed partial class PuzzleGameSession
    {
        private byte[] initialBytes;
        public bool IsPaused { get; private set; }
        private bool externalPaused, popupPaused;
        public string LogicalSessionId { get; private set; }
        public bool IsRestarting { get; private set; }
        public bool HasScreenLayout { get; set; }
        public Camera BoardCamera => boardCamera;
        public bool CanUseItems => CanAcceptInput && (TutorialActive ? tutorial.Progress.Snapshot.FreeItemAvailable : executor.CanUseItems);
        public bool HasFailed => failed;
        public bool IsReady => ready;
        public Sprite MissionSprite(int index) => ready && !failed && artwork != null
            ? artwork.Get(PuzzleArtworkPaths.Mission(State.Missions[index].Definition)) : null;

        public bool SetPaused(bool paused)
        {
            if (!ready || failed || (IsRestarting && paused) || (Phase == BoardActionPhase.Stopped && !IsPresenting && !HasProgressFeedback)) return false;
            externalPaused = paused; ApplyPauseRequests();
            return true;
        }
        public bool SetPopupPaused(bool paused)
        {
            if (paused && (!ready || failed || IsRestarting)) return false;
            popupPaused = paused; ApplyPauseRequests(); return true;
        }
        private void ApplyPauseRequests()
        {
            bool paused = externalPaused || popupPaused;
            if (IsPaused == paused) return;
            IsPaused = paused; audioPlayback?.SetPaused(paused);
            tutorial?.Progress.SetPaused(paused);
            if (!paused) PlayResultFeedback();
            Changed?.Invoke();
        }

        public bool CanSelectItemTarget(BoardItem item, BoardCoordinate at)
            => CanUseItems && (TutorialActive ? tutorial.CanSelectItemTarget(item, at) : executor.CanSelectItemTarget(item, at));

        public bool TryUseItem(BoardItem item, BoardCoordinate? first = null, BoardCoordinate? second = null)
        {
            if (!CanUseItems || !TryBeginTutorial(Tutorial.TutorialInput.UseItem(item, first, second))) return false;
            CapturePresentation();
            ItemUseResult result = TutorialActive ? executor.UseApprovedFreeItem(item, first, second) : executor.UseItem(item, first, second);
            tutorial?.ReportAction(result.IsApplied);
            ObserveMoves();
            Message = result.Message;
            if (result.IsApplied && item != BoardItem.Shuffle)
            {
                if (item == BoardItem.Swap && first.HasValue && second.HasValue)
                { EmitAudio(PuzzleFeedbackCueKind.Swap); presentationSnapshot.Swap(first.Value, second.Value); SwapPresentationState(first.Value, second.Value); }
                BeginEffects(result.Changes, result.Effects, result.PowerTrace);
            }
            else { ResetPresentation(); if (result.IsApplied) Draw(); else Changed?.Invoke(); }
            return result.IsApplied;
        }

        public async UniTask RestartAsync(CancellationToken token)
        {
            if (initialBytes == null || IsRestarting || IsChangingLevel || lifetime.IsCancellationRequested || !isActiveAndEnabled) return;
            IsRestarting = true;
            string previousMessage = Message;
            LevelDefinition definition = null;
            PuzzleArtwork candidateArtwork = null;
            Tutorial.TutorialBoardAdapter candidateTutorial = null;
            using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token);
            try
            {
                Message = "다시 시작하는 중"; Changed?.Invoke();
                linked.Token.ThrowIfCancellationRequested();
                definition = LevelPackCodec.ReadLevel(initialBytes, levelNumber);
                bool runTutorial = ShouldRunTutorial(definition);
                if (runTutorial) CheckTutorialReplay(definition);
                StartingBoardSearch search = new StartingBoardSearch(definition, runTutorial ? definition.Tutorial.seed : seed);
                while (!search.IsDone)
                {
                    linked.Token.ThrowIfCancellationRequested();
                    if (!isActiveAndEnabled) throw new OperationCanceledException();
                    search.Advance(128);
                    await UniTask.Yield(PlayerLoopTiming.Update, linked.Token);
                }
                if (search.Status != StartingBoardStatus.Success) throw new InvalidOperationException(search.Message);
                candidateTutorial = runTutorial ? Tutorial.TutorialBoardAdapter.Prepare(definition, search.State) : null;
                BoardActionExecutor candidateExecutor = candidateTutorial?.Executor ?? new BoardActionExecutor(search.State);
                candidateArtwork = new PuzzleArtwork(visualCatalog);
                await candidateArtwork.PrepareAsync(candidateExecutor.State, linked.Token);
                foreach (RuntimeMission mission in candidateExecutor.State.Missions)
                    candidateArtwork.Get(PuzzleArtworkPaths.Mission(mission.Definition));
                // Retry를 누른 자신의 팝업 정지는 기다리지 않고 외부 정지와 백그라운드만 기다린다.
                while (audioBackground || externalPaused)
                {
                    if (!isActiveAndEnabled) throw new OperationCanceledException();
                    await UniTask.Yield(PlayerLoopTiming.Update, linked.Token);
                }
                linked.Token.ThrowIfCancellationRequested();
                if (!isActiveAndEnabled) throw new OperationCanceledException();
                try { board.Draw(candidateExecutor.State, candidateArtwork); }
                catch { if (State != null && artwork != null) board.Draw(State, artwork); throw; }

                PuzzleArtwork previousArtwork = artwork;
                ClearProgress(); ResetPresentation();
                DisposeTutorial(); tutorialFinalState = null; tutorialCompletionRecorded = false;
                tutorial = candidateTutorial; candidateTutorial = null;
                executor = candidateExecutor; artwork = candidateArtwork; candidateArtwork = null;
                ready = true; failed = false; LogicalSessionId = Guid.NewGuid().ToString("N");
                previousArtwork?.Dispose();
                InitializeProgress(); CenterCamera();
                Message = "블록을 선택하거나 드래그하세요";
                try { BeginStartFeedback(); }
                catch (Exception error) { Debug.LogException(error); }
            }
            catch (OperationCanceledException) { Message = previousMessage; }
            catch (Exception error) { Message = "다시 시작 실패: " + error.Message; }
            finally
            {
                candidateArtwork?.Dispose();
                candidateTutorial?.Dispose();
                if (definition != null) Destroy(definition);
                IsRestarting = false;
                if (this != null && !lifetime.IsCancellationRequested)
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
