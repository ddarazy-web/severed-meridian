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
        public bool IsRestarting { get; private set; }
        public bool HasScreenLayout { get; set; }
        public Camera BoardCamera => boardCamera;
        public bool CanUseItems => CanAcceptInput && executor.CanUseItems;
        public bool HasFailed => failed;
        public bool IsReady => ready;
        public Sprite MissionSprite(int index) => ready && !failed && artwork != null
            ? artwork.Get(PuzzleArtworkPaths.Mission(State.Missions[index].Definition)) : null;

        public bool SetPaused(bool paused)
        {
            if (!ready || failed || IsRestarting || (Phase == BoardActionPhase.Stopped && !IsPresenting)) return false;
            IsPaused = paused;
            Changed?.Invoke();
            return true;
        }

        public bool CanSelectItemTarget(BoardItem item, BoardCoordinate at)
            => CanUseItems && executor.CanSelectItemTarget(item, at);

        public bool TryUseItem(BoardItem item, BoardCoordinate? first = null, BoardCoordinate? second = null)
        {
            if (!CanUseItems) return false;
            CapturePresentation();
            ItemUseResult result = executor.UseItem(item, first, second);
            Message = result.Message;
            if (result.IsApplied && item != BoardItem.Shuffle)
            {
                if (item == BoardItem.Swap && first.HasValue && second.HasValue)
                { presentationSnapshot.Swap(first.Value, second.Value); SwapPresentationState(first.Value, second.Value); }
                BeginEffects(result.Changes, result.Effects, result.PowerTrace);
            }
            else { ResetPresentation(); if (result.IsApplied) Draw(); else Changed?.Invoke(); }
            return result.IsApplied;
        }

        public async UniTask RestartAsync(CancellationToken token)
        {
            if (initialBytes == null || IsRestarting || lifetime.IsCancellationRequested) return;
            IsRestarting = true; ready = false; IsPaused = false; failed = false;
            ResetPresentation();
            Message = "다시 시작하는 중"; Changed?.Invoke();
            LevelDefinition definition = null;
            using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token);
            try
            {
                linked.Token.ThrowIfCancellationRequested();
                board.gameObject.SetActive(false);
                artwork?.Dispose(); artwork = null; executor = null;
                definition = LevelPackCodec.ReadLevel(initialBytes, levelNumber);
                await PrepareAsync(definition, seed, linked.Token);
                board.gameObject.SetActive(true);
            }
            catch (OperationCanceledException) { ready = false; artwork?.Dispose(); }
            catch (Exception error) { Fail("다시 시작 실패: " + error.Message); }
            finally
            {
                if (definition != null) Destroy(definition);
                IsRestarting = false;
                if (this != null && !lifetime.IsCancellationRequested) Changed?.Invoke();
            }
        }
    }
}
