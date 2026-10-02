using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using Board;
using Cysharp.Threading.Tasks;
using Levels;
using Simulation;
using UnityEngine;

namespace GameScreen.Editor
{
    public static partial class PuzzleSwapAnimationVerification
    {
        private static void Property(object target, string property, object value)
            => target.GetType().GetProperty(property).GetSetMethod(true).Invoke(target, new[] { value });

        private static async UniTask VerifyContentsAsync(PuzzleWorldBoard board, Camera camera, Transform root, LevelRuntimeState original)
        {
            BoardCoordinate a = new BoardCoordinate(2, 3), b = new BoardCoordinate(3, 3);
            RuntimeContent[] powers = { RuntimeContent.Rocket, RuntimeContent.Bomb, RuntimeContent.Drone, RuntimeContent.Magnet };
            foreach (RuntimeContent first in powers)
            foreach (RuntimeContent second in new[] { RuntimeContent.Normal, RuntimeContent.Rocket, RuntimeContent.Bomb, RuntimeContent.Drone, RuntimeContent.Magnet, RuntimeContent.Recovery, RuntimeContent.Obstacle })
            {
                if (second >= RuntimeContent.Rocket && second <= RuntimeContent.Magnet && second < first) continue;
                LevelRuntimeState state = new BoardActionExecutor(original).State;
                Property(state.CellAt(a), "Content", first); Property(state.CellAt(a), "Color", null);
                if (first == RuntimeContent.Rocket) Property(state.CellAt(a), "RocketDirection", RocketDirection.Horizontal);
                if (second == RuntimeContent.Obstacle)
                    typeof(LevelRuntimeState).GetMethod("SupplyScrap", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(state, new object[] { state.CellAt(b), 3 });
                else
                {
                    Property(state.CellAt(b), "Content", second);
                    if (second != RuntimeContent.Normal) Property(state.CellAt(b), "Color", null);
                    if (second == RuntimeContent.Rocket) Property(state.CellAt(b), "RocketDirection", RocketDirection.Vertical);
                }
                GameObject owner = new GameObject("Content Swap " + first + " " + second); owner.transform.SetParent(root);
                PuzzleGameSession session = owner.AddComponent<PuzzleGameSession>(); session.Configure(board, camera); Set(session, "started", true);
                PuzzleArtwork art = new PuzzleArtwork(); Set(session, "artwork", art);
                try
                {
                    await art.PrepareAsync(state, CancellationToken.None);
                    BoardActionExecutor baseline = new BoardActionExecutor(state);
                    Set(session, "executor", new BoardActionExecutor(state)); Set(session, "ready", true);
                    board.Draw(session.State, art);
                    SpriteRenderer left = board.OccupantAt(a), right = board.OccupantAt(b);
                    Vector3 leftStart = left.transform.position, rightStart = right.transform.position;
                    Sprite leftSprite = left.sprite, rightSprite = right.sprite;
                    Check(leftSprite != null && rightSprite != null, "콘텐츠 원화 준비 " + first + "/" + second);
                    bool applied = baseline.Swap(a, b).IsApplied;
                    Check(session.TrySwap(a, b) == applied, "교환 판정 동일 " + first + "/" + second);
                    Tick(session, .075f);
                    Check(left.sprite == leftSprite && right.sprite == rightSprite, "파워/점유자 원화 유지 " + first + "/" + second);
                    if (applied)
                        Check(Vector3.Distance(right.transform.position, rightStart + board.transform.TransformVector(PuzzleWorldBoard.CellPosition(a) - PuzzleWorldBoard.CellPosition(b)) * .5f) < .001f, "점유자 본체 중간 이동 " + first + "/" + second);
                    else Check(right.transform.position == rightStart, "자석 제한 대상 고정 " + second);
                    Tick(session, .3f);
                    await FinishPresentation(session);
                    Check(!session.IsPresenting && Snapshot(session.State) == Snapshot(baseline.State), "콘텐츠 최종 결과 동일 " + first + "/" + second);
                }
                finally { UnityEngine.Object.DestroyImmediate(owner); }
            }

            foreach (string reason in new[] { "Wall", "Cover", "Empty", "MagnetTarget" })
            {
                LevelRuntimeState state = new BoardActionExecutor(original).State;
                if (reason == "Wall")
                {
                    // 기존 편집 경로로 벽을 만든 원본 사본에서 실행 상태를 생성한다.
                    LevelDefinition level = (LevelDefinition)typeof(Levels.Editor.BoardActionVerification).GetMethod("RocketBoard", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                    try
                    {
                        Levels.Editor.LevelFlowEditing.SetWalls(level, new[] { new BoardEdge(a, b) }, false);
                        state = LevelStateBuilder.Build(level, 12345).State;
                    }
                    finally { UnityEngine.Object.DestroyImmediate(level); }
                }
                else if (reason == "Cover") { Property(state.CellAt(a), "Cover", CoverKind.Web); Property(state.CellAt(a), "CoverDurability", 1); }
                else if (reason == "Empty") Property(state.CellAt(b), "Content", RuntimeContent.Empty);
                else
                {
                    foreach (RuntimeCell cell in state.Cells.Where(c => c.IsActive)) { Property(cell, "Content", RuntimeContent.Bomb); Property(cell, "Color", null); }
                    Property(state.CellAt(a), "Content", RuntimeContent.Magnet);
                }
                GameObject owner = new GameObject("Rejected " + reason); owner.transform.SetParent(root);
                PuzzleGameSession session = owner.AddComponent<PuzzleGameSession>(); session.Configure(board, camera); Set(session, "started", true);
                PuzzleArtwork art = new PuzzleArtwork(); Set(session, "artwork", art);
                try
                {
                    await art.PrepareAsync(state, CancellationToken.None);
                    Set(session, "executor", new BoardActionExecutor(state)); Set(session, "ready", true); board.Draw(session.State, art);
                    string before = Snapshot(session.State);
                    Check(!session.TrySwap(a, b), "거절 " + reason);
                    Tick(session, .075f);
                    SpriteRenderer occupant = board.OccupantAt(a);
                    Check(occupant == null || Vector3.Distance(occupant.transform.position, board.transform.TransformPoint(PuzzleWorldBoard.CellPosition(a))) < .1f, "거절 경계 통과 없음 " + reason);
                    Tick(session, 1);
                    Check(Snapshot(session.State) == before && !session.IsPresenting, "거절 후 보드/RNG/미션 불변 " + reason);
                }
                finally { UnityEngine.Object.DestroyImmediate(owner); }
            }
        }

        private static async UniTask VerifyRestartAsync(PuzzleWorldBoard board, Camera camera, Transform root)
        {
            GameObject owner = new GameObject("Restart Swap"); owner.transform.SetParent(root);
            PuzzleGameSession session = owner.AddComponent<PuzzleGameSession>(); session.Configure(board, camera);
            try
            {
                await session.InitializeAsync(1, 12345, CancellationToken.None);
                Call(session, "TickProgress", .7f);
                Check(session.CanAcceptInput, "재시작 검사 실제 팩 준비");
                string initial = Snapshot(session.State);
                int count = board.GetComponentsInChildren<SpriteRenderer>(true).Length;
                for (int i = 0; i < 5; i++)
                {
                    ActionCandidate action = ActionQuery.Find(session.State).First(c => c.Second.HasValue);
                    Check(session.TrySwap(action.First, action.Second.Value), "재시작 직전 교환 " + i);
                    Tick(session, .075f);
                    await session.RestartAsync(CancellationToken.None);
                    Call(session, "TickProgress", .7f);
                    Check(session.CanAcceptInput && !session.IsPresenting && Snapshot(session.State) == initial, "재생 중 다시하기 상태/잠금 복원 " + i);
                    Check(board.GetComponentsInChildren<SpriteRenderer>(true).Length == count, "표시 객체 누적 없음 " + i);
                    Check(session.State.Cells.Where(c => c.Content == RuntimeContent.Normal).All(cell =>
                        Vector3.Distance(board.OccupantAt(cell.Coordinate).transform.position, board.transform.TransformPoint(PuzzleWorldBoard.CellPosition(cell.Coordinate))) < .001f), "다시하기 전체 점유자 위치 복원 " + i);
                }
                ActionCandidate final = ActionQuery.Find(session.State).First(c => c.Second.HasValue);
                session.TrySwap(final.First, final.Second.Value); Tick(session, .075f);
                PuzzleArtwork art = (PuzzleArtwork)typeof(PuzzleGameSession).GetField("artwork", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session);
                UnityEngine.Object.DestroyImmediate(owner); owner = null;
                Check(art.AtlasCount == 0, "교환 중 종료 아틀라스 반환");
            }
            finally { if (owner != null) UnityEngine.Object.DestroyImmediate(owner); }
        }

        private static async UniTask VerifyFixedLayersAsync(PuzzleWorldBoard board, Camera camera, Transform root)
        {
            LevelDefinition level = (LevelDefinition)typeof(Levels.Editor.PowerEffectVerification).GetMethod("Make", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            GameObject owner = new GameObject("Fixed Layers"); owner.transform.SetParent(root);
            PuzzleGameSession session = owner.AddComponent<PuzzleGameSession>(); session.Configure(board, camera); Set(session, "started", true);
            PuzzleArtwork art = new PuzzleArtwork(); Set(session, "artwork", art);
            try
            {
                BoardCoordinate[] footprint = { new BoardCoordinate(0, 0), new BoardCoordinate(0, 1), new BoardCoordinate(1, 0), new BoardCoordinate(1, 1) };
                Levels.Editor.LevelObstacleEditing.Apply(level, new Levels.Editor.PlacementBrush { Layer = PlacementLayer.Block, Erase = true }, footprint);
                var edit = Levels.Editor.LevelObstacleEditing.Apply(level, new Levels.Editor.PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)ObstacleKind.Appliance, Durability = 3 }, new[] { footprint[0] });
                Check(edit.Changed == 1, "2x2 장애물 fixture 배치");
                Levels.Editor.LevelObstacleEditing.Apply(level, new Levels.Editor.PlacementBrush { Layer = PlacementLayer.Dust, Durability = 2 }, new[] { new BoardCoordinate(0, 2) });
                LevelRuntimeState state = LevelStateBuilder.Build(level, 12345).State;
                await art.PrepareAsync(state, CancellationToken.None);
                Set(session, "executor", new BoardActionExecutor(state)); Set(session, "ready", true); board.Draw(session.State, art);
                var stationary = board.GetComponentsInChildren<SpriteRenderer>().ToDictionary(r => r, r => r.transform.position);
                string before = Snapshot(session.State);
                Check(!session.TrySwap(new BoardCoordinate(0, 2), new BoardCoordinate(0, 1)), "2x2 고정 장애물 교환 거절");
                Tick(session, .075f);
                SpriteRenderer moving = board.OccupantAt(new BoardCoordinate(0, 2));
                Check(stationary.Where(p => p.Key != moving).All(p => p.Key.transform.position == p.Value), "2x2 본체/먼지/바닥 위치 고정");
                Tick(session, 1);
                Check(stationary.All(p => p.Key.transform.position == p.Value) && Snapshot(session.State) == before, "고정 장애물 거절 후 표시와 규칙 복원");
            }
            finally { UnityEngine.Object.DestroyImmediate(owner); UnityEngine.Object.DestroyImmediate(level); }
        }
    }
}
