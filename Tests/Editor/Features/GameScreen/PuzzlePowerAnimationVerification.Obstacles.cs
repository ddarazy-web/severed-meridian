using System;
using System.Linq;
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
    public static partial class PuzzlePowerAnimationVerification
    {
        private static async UniTask VerifyObstacleFramesAsync(PuzzleWorldBoard board, object playback, Type playbackType)
        {
            LevelDefinition contactLevel = (LevelDefinition)typeof(PowerEffectVerification).GetMethod("Make", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
            using (PuzzleArtwork contactArt = new PuzzleArtwork())
            try
            {
                BoardCoordinate anchor = new BoardCoordinate(0, 4), origin = new BoardCoordinate(8, 4);
                LevelObstacleEditing.Apply(contactLevel, new PlacementBrush { Layer = PlacementLayer.Block, Erase = true }, LevelPlacementRules.Footprint(anchor, 2));
                LevelObstacleEditing.Apply(contactLevel, new PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)ObstacleKind.Appliance, Durability = 9 }, new[] { anchor });
                typeof(PowerEffectVerification).GetMethod("Place", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null,
                    new object[] { contactLevel, origin, InitialBlockKind.Rocket, RocketDirection.Vertical, RabbitColor.Type1 });
                LevelRuntimeState before = LevelStateBuilder.Build(contactLevel, 12345).State;
                BoardActionExecutor executor = new BoardActionExecutor(before);
                BoardActionResult action = executor.Activate(origin);
                PuzzleEffectTimeline timeline = new PuzzleEffectTimeline(before, action.Changes, action.Effects, action.PowerTrace);
                await contactArt.PrepareAsync(before, CancellationToken.None);
                await (UniTask)playbackType.GetMethod("PrepareAsync", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(playback,
                    new object[] { before, executor.State, contactArt, timeline, CancellationToken.None });
                SceneCall(playback, "Begin", board);
                var hit = timeline.Reactions.First(reaction => reaction.Record.Response == DamageResponse.Damage);
                SceneCall(playback, "Tick", hit.ContactTime);
                Transform rocket = board.GetComponentsInChildren<Transform>().Single(item => item.name == "Rocket-flight");
                Check(Vector3.Distance(rocket.localPosition, PuzzleWorldBoard.CellPosition(new BoardCoordinate(1, 4))) < .001f,
                    "상향 로켓의 실제 그림이 가까운 본체 점유 칸에 도착");
                LevelRuntimeState visual = (LevelRuntimeState)playbackType.GetField("visual", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(playback);
                Check(visual.Obstacles[before.CellAt(anchor).ObstacleIndex.Value].Durability == 8, "실제 로켓 접촉 프레임에 첫 손상 표시");
                CapturePower(board, "rocket-body-contact", false);
                SceneCall(playback, "Tick", timeline.Duration);
                Check(visual.Obstacles[before.CellAt(anchor).ObstacleIndex.Value].Durability == 7, "접촉 뒤 두 번째 손상 pulse 보존");
            }
            finally { SceneCall(playback, "Reset"); UnityEngine.Object.Destroy(contactLevel); }
            foreach (int combination in new[] { -1, 3, 9 })
            {
                LevelDefinition overlapLevel = combination < 0 ?
                    (LevelDefinition)typeof(PowerEffectVerification).GetMethod("Make", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null) :
                    (LevelDefinition)typeof(CombinationVerification).GetMethod("Make", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { combination, RocketDirection.Horizontal, null });
                using PuzzleArtwork overlapArt = new PuzzleArtwork();
                try
                {
                    BoardCoordinate anchor = combination == 9 ? new BoardCoordinate(0, 0) : new BoardCoordinate(2, 3);
                    LevelObstacleEditing.Apply(overlapLevel, new PlacementBrush { Layer = PlacementLayer.Block, Erase = true }, LevelPlacementRules.Footprint(anchor, 2));
                    LevelObstacleEditing.Apply(overlapLevel, new PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)ObstacleKind.Appliance, Durability = 9 }, new[] { anchor });
                    if (combination < 0) typeof(PowerEffectVerification).GetMethod("Place", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null,
                        new object[] { overlapLevel, new BoardCoordinate(4, 4), InitialBlockKind.Bomb, RocketDirection.Horizontal, RabbitColor.Type1 });
                    LevelRuntimeState before = LevelStateBuilder.Build(overlapLevel, 12345).State;
                    int body = before.CellAt(anchor).ObstacleIndex.Value;
                    BoardActionExecutor executor = new BoardActionExecutor(before);
                    BoardActionResult action = combination < 0 ? executor.Activate(new BoardCoordinate(4, 4)) : executor.Swap(new BoardCoordinate(4, 4), new BoardCoordinate(4, 5));
                    PuzzleEffectTimeline timeline = new PuzzleEffectTimeline(before, action.Changes, action.Effects, action.PowerTrace);
                    var hits = timeline.Reactions.Where(reaction => reaction.BodyIndex == body && reaction.Record.Response == DamageResponse.Damage).ToArray();
                    Check(hits.Length == (combination < 0 ? 2 : 4), "폭탄/확대 폭탄/자석 겹침 피해 기록 수 " + combination);
                    await overlapArt.PrepareAsync(before, CancellationToken.None);
                    await (UniTask)playbackType.GetMethod("PrepareAsync", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(playback,
                        new object[] { before, executor.State, overlapArt, timeline, CancellationToken.None });
                    SceneCall(playback, "Begin", board);
                    float elapsed = 0;
                    foreach (var hit in hits)
                    {
                        SceneCall(playback, "Tick", hit.Time + .001f - elapsed); elapsed = hit.Time + .001f;
                        LevelRuntimeState visual = (LevelRuntimeState)playbackType.GetField("visual", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(playback);
                        Check(visual.Obstacles[body].Durability == hit.Record.DurabilityAfter, "겹침 칸마다 화면 내구도 단계 감소 " + combination + "/" + hit.Record.DurabilityAfter);
                    }
                    SceneCall(playback, "Tick", timeline.Duration);
                }
                finally { SceneCall(playback, "Reset"); UnityEngine.Object.Destroy(overlapLevel); }
            }
            foreach (ObstacleKind kind in new[] { ObstacleKind.Crate, ObstacleKind.Scrap, ObstacleKind.Safe, ObstacleKind.ColorLock, ObstacleKind.Appliance })
            foreach (bool destroy in new[] { false, true })
            {
                LevelDefinition level = (LevelDefinition)typeof(CombinationVerification).GetMethod("Make", BindingFlags.NonPublic | BindingFlags.Static)
                    .Invoke(null, new object[] { 1, RocketDirection.Horizontal, null });
                using PuzzleArtwork art = new PuzzleArtwork();
                try
                {
                    BoardCoordinate anchor = new BoardCoordinate(3, 6);
                    LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Block, Erase = true },
                        LevelPlacementRules.Footprint(anchor, LevelPlacementRules.Size(kind)));
                    LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)kind,
                        Durability = destroy ? 1 : LevelPlacementRules.MaxDurability(kind), Color = RabbitColor.Type1 }, new[] { anchor });
                    typeof(PowerEffectVerification).GetMethod("Place", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null,
                        new object[] { level, new BoardCoordinate(5, 6), InitialBlockKind.Rocket, RocketDirection.Vertical, RabbitColor.Type1 });
                    LevelRuntimeState before = LevelStateBuilder.Build(level, 12345).State;
                    Check(before != null, "장애물 표시 fixture " + kind + "/" + destroy);
                    int body = before.CellAt(anchor).ObstacleIndex.Value;
                    BoardActionExecutor executor = new BoardActionExecutor(before);
                    BoardActionResult action = executor.Swap(new BoardCoordinate(4, 4), new BoardCoordinate(4, 5));
                    Check(action.IsApplied, "장애물 조합 타격 " + kind + "/" + destroy);
                    PuzzleEffectTimeline timeline = new PuzzleEffectTimeline(before, action.Changes, action.Effects, action.PowerTrace);
                    var hits = timeline.Reactions.Where(reaction => reaction.BodyIndex == body && reaction.Record.Response == DamageResponse.Damage).OrderBy(reaction => reaction.Time).ToArray();
                    Check(hits.Length > 0 && (kind != ObstacleKind.Appliance || destroy || hits.Length > 1), "실제 유효 타격·2x2 반복 fixture " + kind + "/" + destroy);
                    await art.PrepareAsync(before, CancellationToken.None);
                    await (UniTask)playbackType.GetMethod("PrepareAsync", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(playback,
                        new object[] { before, executor.State, art, timeline, CancellationToken.None });
                    SceneCall(playback, "Begin", board);
                    float elapsed = 0; int expected = before.Obstacles[body].Durability;
                    foreach (var hit in hits)
                    {
                        float prior = Mathf.Max(elapsed, hit.Time - .001f);
                        SceneCall(playback, "Tick", prior - elapsed); elapsed = prior;
                        LevelRuntimeState visual = (LevelRuntimeState)playbackType.GetField("visual", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(playback);
                        Check(visual.Obstacles[body].Durability == expected, "타격 직전 단계 유지 " + kind + "/" + hit.Time);
                        SceneCall(playback, "Tick", hit.Time + .001f - elapsed); elapsed = hit.Time + .001f;
                        expected = hit.Record.DurabilityAfter;
                        Check(visual.Obstacles[body].Durability == expected, "타격 직후 기록과 내구도 일치 " + kind + "/" + hit.Time);
                        SpriteRenderer[] bodies = board.GetComponentsInChildren<SpriteRenderer>().Where(image => image.name == "Obstacle-" + before.Obstacles[body].Definition.Id).ToArray();
                        Check(expected == 0 ? bodies.Length == 0 : bodies.Length == 1 && bodies[0].sprite.name.Contains("durability-" + expected + "-"),
                            "본체 이미지 한 개·단계/파괴 반영 " + kind + "/" + hit.Time);
                    }
                    SceneCall(playback, "Tick", timeline.Duration);
                    Check(board.GetComponentsInChildren<SpriteRenderer>().All(image => image.name != "Effect-playback"), "장애물 효과 종료 " + kind + "/" + destroy);
                }
                finally { SceneCall(playback, "Reset"); UnityEngine.Object.Destroy(level); }
            }
        }
    }
}
