using System.Reflection;
using System.Threading;
using Board;
using Cysharp.Threading.Tasks;
using Levels;
using Levels.Editor;
using Simulation;
using UnityEngine;

namespace GameScreen.Editor
{
    public static partial class PuzzleSettlementAnimationVerification
    {
        private static async UniTask VerifyAutomaticCascadeAsync(PuzzleGameSession session, PuzzleWorldBoard board)
        {
            LevelDefinition level = (LevelDefinition)typeof(SettlementVerification).GetMethod("FallingBoard", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            try
            {
                JsonUtility.FromJsonOverwrite("{\"moveCount\":3}", level);
                LevelSupplyEditing.SetItems(level, 0, new[] { new SupplyItem(SupplyKind.FixedNormal, 3), new SupplyItem(SupplyKind.FixedNormal, 1, RabbitColor.Type2) });
                LevelRuntimeState initial = LevelStateBuilder.Build(level, 12345).State;
                PuzzleArtwork art = (PuzzleArtwork)typeof(PuzzleGameSession).GetField("artwork", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session);
                await art.PrepareAsync(initial, CancellationToken.None);
                session.enabled = false;
                foreach (Vector2Int size in new[] { new Vector2Int(1280, 720), new Vector2Int(450, 800) })
                {
                    PuzzleUIRenderVerification.SetSize(size.x, size.y);
                    for (int frame = 0; frame < 15; frame++) await UniTask.Yield();
                    Call(session, "ResetPresentation");
                    BoardActionExecutor executor = new BoardActionExecutor(initial), direct = new BoardActionExecutor(initial);
                    Set(session, "executor", executor); board.Draw(initial, art);
                    session.enabled = true;
                    Check(session.TrySwap(new BoardCoordinate(3, 3), new BoardCoordinate(2, 3)), "자동 연쇄 실제 씬 교환 " + size);
                    session.enabled = false;
                    direct.Swap(new BoardCoordinate(3, 3), new BoardCoordinate(2, 3));
                    bool automaticRemoval = false, followingSettlement = false;
                    for (int frame = 0; frame < 20000 && (session.IsPresenting || executor.HasPendingCascade); frame++)
                    {
                        if (session.IsPresenting) { Tick(session, .02f); continue; }
                        int rounds = executor.CascadeRounds;
                        Call(session, "Advance");
                        if (!automaticRemoval && executor.CascadeRounds > rounds && session.IsPresenting)
                        {
                            Tick(session, .06f);
                            Check(!session.CanAcceptInput && session.IsPresenting, "공급 후 자동 매칭 제거 중간 프레임 " + size);
                            await Shot("scene-" + size.x + "-automatic-remove-mid"); automaticRemoval = true;
                        }
                        else if (automaticRemoval && !followingSettlement && session.IsPresenting && executor.Phase == BoardActionPhase.WaitingForAutomaticMatch)
                        {
                            Tick(session, .06f); await Shot("scene-" + size.x + "-automatic-fall-mid"); followingSettlement = true;
                        }
                    }
                    for (int step = 0; step < 1000 && direct.HasPendingCascade; step++) direct.AdvanceCascade();
                    Check(automaticRemoval && followingSettlement && !session.IsPresenting && !executor.HasPendingCascade && executor.CascadeRounds >= 1,
                        "자동 매칭 추가 제거·재정착 완료 " + size);
                    Check(Snapshot(executor.State) == Snapshot(direct.State) && Snapshot(executor.Outcome) == Snapshot(direct.Outcome) && executor.Phase == direct.Phase,
                        "자동 매칭 전체 상태·승패 동일 " + size);
                    session.enabled = true;
                    Check(session.CanAcceptInput, "추가 연쇄 완료 후 입력 복원 " + size);
                    session.enabled = false; await Shot("scene-" + size.x + "-automatic-end");
                }
            }
            finally { Object.Destroy(level); session.enabled = true; }
        }
    }
}
