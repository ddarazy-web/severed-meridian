using System;
using System.IO;
using System.Reflection;
using System.Threading;
using Board;
using Cysharp.Threading.Tasks;
using Levels;
using Levels.Editor;
using Simulation;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GameScreen.Editor
{
    public static partial class PuzzleLevelTransitionVerification
    {
        public static void RunAssetScene()
        {
            LevelDefinition selected = (LevelDefinition)typeof(PowerEffectVerification).GetMethod("Make", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
            try
            {
                JsonUtility.FromJsonOverwrite("{\"moveCount\":37,\"missions\":[{\"kind\":0,\"color\":0,\"count\":1}]}", selected);
                typeof(PowerEffectVerification).GetMethod("Place", BindingFlags.NonPublic | BindingFlags.Static)
                    .Invoke(null, new object[] { selected, new BoardCoordinate(4, 4), InitialBlockKind.Rocket, RocketDirection.Horizontal, RabbitColor.Type1 });
                Check(LevelSupplyEditing.AddTopSources(selected) == null, "Asset 상단 공급 fixture");
                PuzzleEditorLaunchRequest request = PuzzleEditorLaunchRequest.Capture(selected, PuzzleEditorLevelSource.Asset, 8765);
                JsonUtility.FromJsonOverwrite("{\"moveCount\":49}", selected);
                Directory.CreateDirectory(Output); PuzzleUIRenderVerification.RememberSize(); SessionState.SetBool(PlayKey, true);
                SessionState.SetBool("Stage13.AssetScene", true); PuzzleEditorLauncher.Launch(request, 0);
            }
            finally { UnityEngine.Object.DestroyImmediate(selected); }
        }

        private static async UniTask AssetChecks(PuzzleGameSession session)
        {
            Check(!session.LevelAdvanceEnabled && session.State.MovesRemaining == 37 && (int)Private(session, "seed") == 8765,
                "실제 Asset launcher 미저장 클릭 사본·시드·전환 비활성");
            MethodInfo snapshot = typeof(LevelInitialStateVerification).GetMethod("Snapshot", BindingFlags.Static | BindingFlags.NonPublic);
            string initial = (string)snapshot.Invoke(null, new object[] { session.State });
            Check(session.TryActivate(new BoardCoordinate(4, 4)), "Asset 사본 실제 파워 행동");
            await ReadyOrResult(session, true);
            PuzzleResultView popup = UnityEngine.Object.FindFirstObjectByType<PuzzleResultView>();
            UnityEngine.UI.Button next = (UnityEngine.UI.Button)typeof(PuzzleResultView).GetField("nextLevel", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(popup);
            UnityEngine.UI.Button retry = (UnityEngine.UI.Button)typeof(PuzzleResultView).GetField("retry", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(popup);
            UnityEngine.UI.Text body = (UnityEngine.UI.Text)typeof(PuzzleResultView).GetField("detail", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(popup);
            Check(session.Outcome.Kind == BoardOutcomeKind.Won && !next.gameObject.activeSelf && !session.CanAdvanceLevel &&
                body.text.Contains("다음 레벨은 MemoryPack 모드에서 이어서 플레이할 수 있습니다"), "실제 Asset 승리 다음 버튼 숨김·안내");
            next.onClick.Invoke(); Check(session.State.LevelNumber == 1 && !session.IsChangingLevel, "숨긴 Asset 버튼 강제 이벤트도 전환0");
            await CapturePopups("asset-victory");
            ExecuteEvents.Execute(retry.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
            await ReadyOrResult(session, false);
            Check(session.State.MovesRemaining == 37 && initial == (string)snapshot.Invoke(null, new object[] { session.State }),
                "실제 Asset Retry 미저장 클릭 시점 전체 초기 상태");
        }

        private static async UniTask LossChecks(PuzzleGameSession session)
        {
            await WinAt(session, 1);
            LevelDefinition fixture = LevelPackCodec.ReadLevel((byte[])Private(session, "initialBytes"), 1);
            try
            {
                JsonUtility.FromJsonOverwrite("{\"moveCount\":1,\"missions\":[{\"kind\":0,\"color\":0,\"count\":1000}]}", fixture);
                typeof(PuzzleGameSession).GetField("initialBytes", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(session, LevelPackCodec.Encode(new[] { fixture }));
                await session.RestartAsync(CancellationToken.None); await ReadyOrResult(session, false);
                Check(session.TryActivate(new BoardCoordinate(4, 4)), "MemoryPack 실패 판 실제 행동");
                await ReadyOrResult(session, true);
                PuzzleResultView popup = UnityEngine.Object.FindFirstObjectByType<PuzzleResultView>();
                UnityEngine.UI.Button next = (UnityEngine.UI.Button)typeof(PuzzleResultView).GetField("nextLevel", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(popup);
                Check(session.LevelAdvanceEnabled && session.Outcome.Kind == BoardOutcomeKind.MovesExhausted && !session.CanAdvanceLevel && !next.gameObject.activeSelf &&
                    !await session.AdvanceLevelAsync(CancellationToken.None), "실제 MemoryPack 실패 다음 버튼 숨김·전환 거부");
                await CapturePopups("lost");
            }
            finally { UnityEngine.Object.Destroy(fixture); }
        }
    }
}
