using System;
using System.Collections.Generic;
using Board;
using LevelAuthoring.Editing;
using LevelTool.Presentation;
using Newtonsoft.Json.Linq;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.AddressableAssets;
using UnityEngine.U2D;
using UnityEngine.UIElements;

namespace LevelAuthoring.Editor
{
    public static partial class LevelToolManualSavePerformanceVerification
    {
        private sealed class DelayedAtlas : AsyncOperationBase<SpriteAtlas>
        {
            protected override void Execute() { }
            internal void Release(SpriteAtlas atlas) => Complete(atlas, true, (string)null);
        }
        private static Action releaseArtworkTest;

        public static void RunArtworkRace()
        {
            singleScenario = "artwork-interleaving"; focusedRefreshOnly = true; BeginRepresentative();
        }

        private static Scenario ArtworkRaceScenario() => new Scenario
        {
            Name = "artwork-interleaving",
            Action = () =>
            {
                var view = (LevelToolBoardView)Field("boardView");
                object owner = typeof(LevelToolBoardView).GetField("artwork", Flags).GetValue(view);
                var atlases = (Dictionary<string, BoardSpriteAtlas>)owner.GetType().GetField("atlases", Flags).GetValue(owner);
                BoardSpriteAtlas atlas = atlases[BoardSpriteAtlas.Prefix + "Blocks"];
                var handleField = typeof(BoardSpriteAtlas).GetField("handle", Flags);
                var original = (AsyncOperationHandle<SpriteAtlas>)handleField.GetValue(atlas);
                ExtendedCheck(original.IsDone && original.Status == AsyncOperationStatus.Succeeded, "artwork race uses a loaded real atlas");
                var delayed = new DelayedAtlas();
                AsyncOperationHandle<SpriteAtlas> pending = Addressables.ResourceManager.StartOperation(delayed, default);
                handleField.SetValue(atlas, pending);
                releaseArtworkTest = () =>
                {
                    if (!pending.IsDone) delayed.Release(original.Result);
                    handleField.SetValue(atlas, original);
                    Addressables.Release(pending);
                };
                // 제품 코드를 바꾸지 않고 실제 로더 핸들의 완료만 늦춘다.
                view.Clear(); Call("DrawBoard");
                SetField("cellSize", 80f); Call("DrawBoard");
                EditSession.EditLevel("대기 중 다른 배치", level =>
                {
                    level["elements"] = new JArray();
                    LevelDocumentEditing.Place(level, measuredDefinitions, normalDefinition, 4, 4, true);
                });
                Call("Refresh");
                SetField("cellSize", 64f); Call("Refresh");
                ExtendedCheck((int)owner.GetType().GetField("pending", Flags).GetValue(owner) >= 2,
                    "old and new artwork requests overlap before atlas completion");
                delayed.Release(original.Result);
            },
            Verify = () =>
            {
                try
                {
                    var board = (VisualElement)Field("board");
                    List<Image> images = board.Query<Image>().ToList();
                    ExtendedCheck(images.Count == 1 && images[0].style.width.value.value > 0,
                        "late old artwork does not append stale placements");
                    ExtendedCheck(Math.Abs(board.style.width.value.value - 576) < .01 &&
                        Math.Abs(board.Q("cell-40").style.width.value.value - 64) < .01,
                        "delayed artwork completes at latest zoom");
                    ExtendedCheck(images[0].style.left.value.value >= 3 * 64 && images[0].style.left.value.value < 5 * 64,
                        "delayed artwork uses current placement coordinates");
                }
                finally { releaseArtworkTest?.Invoke(); releaseArtworkTest = null; }
            }
        };
    }
}
