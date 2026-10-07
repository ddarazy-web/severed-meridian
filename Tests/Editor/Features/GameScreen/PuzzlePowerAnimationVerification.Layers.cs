using System;
using System.Collections.Generic;
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
        private static async UniTask VerifyCreationFramesAsync(PuzzleWorldBoard board, object playback, Type playbackType)
        {
            MatchKind[] kinds = { MatchKind.Drone, MatchKind.Rocket, MatchKind.Bomb, MatchKind.Magnet, MatchKind.Rocket };
            string[] colors = { "pink", "yellow", "blue", "green", "purple" };
            for (int color = 0; color < 5; color++)
            {
                MatchKind kind = kinds[color]; BoardCoordinate spawn = new BoardCoordinate(3, 3), rocket = new BoardCoordinate(3, 0);
                BoardCoordinate[] cells = kind == MatchKind.Drone ? new[] { spawn, new BoardCoordinate(3, 4), new BoardCoordinate(4, 3), new BoardCoordinate(4, 4) } :
                    kind == MatchKind.Bomb ? new[] { spawn, new BoardCoordinate(3, 4), new BoardCoordinate(3, 5), new BoardCoordinate(4, 5), new BoardCoordinate(5, 5) } :
                    Enumerable.Range(3, kind == MatchKind.Rocket ? 4 : 5).Select(column => new BoardCoordinate(3, column)).ToArray();
                Dictionary<BoardCoordinate, int> placement = cells.ToDictionary(cell => cell, cell => color); placement[rocket] = (color + 1) % 5;
                LevelDefinition level = (LevelDefinition)typeof(BoardActionVerification).GetMethod("Make", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { placement, 20 });
                using PuzzleArtwork art = new PuzzleArtwork();
                try
                {
                    typeof(PowerEffectVerification).GetMethod("Place", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null,
                        new object[] { level, rocket, InitialBlockKind.Rocket, RocketDirection.Horizontal, RabbitColor.Type1 });
                    LevelRuntimeState after = LevelStateBuilder.Build(level, 12345).State, before = new BoardActionExecutor(after).State;
                    object decisions = typeof(BoardActionVerification).GetMethod("Resolve", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { after, spawn, spawn });
                    var changes = (IEnumerable<MatchedBlockChange>)typeof(BoardActionVerification).GetMethod("Apply", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new[] { (object)after, decisions });
                    TurnEffectContext context = (TurnEffectContext)Activator.CreateInstance(typeof(TurnEffectContext), BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] { 1, changes }, null);
                    List<EffectRecord> records = new List<EffectRecord>(); object[] args = { after, changes, (BoardCoordinate?)rocket, context, records, null };
                    Check((bool)typeof(PowerEffectResolution).GetMethod("Apply", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, args) && records.Any(record => record.Response == DamageResponse.Protected), "매칭 생성과 피격 보호 fixture " + kind + "/" + color);
                    PuzzleEffectTimeline timeline = new PuzzleEffectTimeline(before, changes, records, context.PowerTrace);
                    await art.PrepareAsync(before, CancellationToken.None);
                    await (UniTask)playbackType.GetMethod("PrepareAsync", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(playback, new object[] { before, after, art, timeline, CancellationToken.None });
                    SceneCall(playback, "Begin", board); SceneCall(playback, "Tick", .06f);
                    Sprite matchFrame = art.Get("Effects/Match/Animations/match-" + colors[color] + "-frame-02-v1-256");
                    Check(matchFrame != null && board.GetComponentsInChildren<SpriteRenderer>().Any(image => image.enabled &&
                        image.sprite == matchFrame && image.transform.parent.name == "match"), "다섯 색상 실제 매칭 효과 " + color);
                    SceneCall(playback, "Tick", .07f);
                    LevelRuntimeState visual = (LevelRuntimeState)playbackType.GetField("visual", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(playback);
                    Check(visual.CellAt(spawn).Content == after.CellAt(spawn).Content && board.OccupantAt(spawn) != null && board.OccupantAt(spawn).color.a == 1,
                        "생성 파워를 이전 일반 블록 제거로 숨기지 않음 " + kind);
                    SceneCall(playback, "Tick", timeline.Duration);
                    Check(board.OccupantAt(spawn) != null && visual.CellAt(spawn).Content == after.CellAt(spawn).Content, "피격 후에도 생성 파워 보존 " + kind);
                }
                finally { SceneCall(playback, "Reset"); UnityEngine.Object.Destroy(level); }
            }
        }

        private static async UniTask VerifyLayerFramesAsync(PuzzleWorldBoard board, object playback, Type playbackType)
        {
            foreach (DamageResponse expected in new[] { DamageResponse.CoverDamage, DamageResponse.Protected, DamageResponse.AlreadyDamaged, DamageResponse.Wall })
            {
                LevelDefinition level = (LevelDefinition)typeof(PowerEffectVerification).GetMethod("Make", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
                using PuzzleArtwork art = new PuzzleArtwork();
                try
                {
                    BoardCoordinate target = new BoardCoordinate(4, 4), source = new BoardCoordinate(4, 3);
                    if (expected == DamageResponse.AlreadyDamaged || expected == DamageResponse.Wall)
                    {
                        LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Block, Erase = true }, new[] { target });
                        LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)ObstacleKind.Crate, Durability = 3 }, new[] { target });
                    }
                    if (expected == DamageResponse.Wall) LevelFlowEditing.SetWalls(level, new[] { new BoardEdge(source, target) }, false);
                    LevelRuntimeState after = LevelStateBuilder.Build(level, 12345).State;
                    if (expected == DamageResponse.CoverDamage)
                    {
                        typeof(RuntimeCell).GetProperty("Cover").SetValue(after.CellAt(target), CoverKind.Mold);
                        typeof(RuntimeCell).GetProperty("CoverDurability").SetValue(after.CellAt(target), 1);
                    }
                    if (expected == DamageResponse.Protected) typeof(RuntimeCell).GetProperty("Content").SetValue(after.CellAt(target), RuntimeContent.Bomb);
                    TurnEffectContext context = (TurnEffectContext)typeof(FixedObstacleVerification).GetMethod("Context", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
                    if (expected == DamageResponse.Protected)
                        ((HashSet<BoardCoordinate>)typeof(TurnEffectContext).GetField("protectedPowers", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(context)).Add(target);
                    if (expected == DamageResponse.AlreadyDamaged) typeof(TurnEffectContext).GetMethod("RegisterDamage", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(context, new object[] { after.CellAt(target).ObstacleIndex.Value });
                    LevelRuntimeState before = new BoardActionExecutor(after).State;
                    MatchedBlockChange[] changes = expected == DamageResponse.Wall ? new[] {
                        (MatchedBlockChange)Activator.CreateInstance(typeof(MatchedBlockChange), BindingFlags.NonPublic | BindingFlags.Instance, null,
                            new object[] { source, RabbitColor.Type1, RuntimeContent.Empty, null, 1, true, 0, 0 }, null) } : Array.Empty<MatchedBlockChange>();
                    List<EffectRecord> records = new List<EffectRecord>();
                    object[] args = { after, changes, expected == DamageResponse.Wall ? (BoardCoordinate?)null : target, context, records, null };
                    Check((bool)typeof(PowerEffectResolution).GetMethod("Apply", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, args) && records.Any(record => record.Target.Equals(target) && record.Response == expected),
                        "실제 규칙 레이어/무효 반응 fixture " + expected);
                    PuzzleEffectTimeline timeline = new PuzzleEffectTimeline(before, changes, records, context.PowerTrace);
                    await art.PrepareAsync(before, CancellationToken.None);
                    await (UniTask)playbackType.GetMethod("PrepareAsync", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(playback, new object[] { before, after, art, timeline, CancellationToken.None });
                    SceneCall(playback, "Begin", board); SceneCall(playback, "Tick", .13f);
                    LevelRuntimeState visual = (LevelRuntimeState)playbackType.GetField("visual", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(playback);
                    Check(visual.CellAt(target).Content == before.CellAt(target).Content, "덮개 제거/보호/차단은 내용물 조기 제거 없음 " + expected);
                    Check(visual.CellAt(target).CoverDurability == after.CellAt(target).CoverDurability, "기록된 레이어 상태만 반영 " + expected);
                    if (before.CellAt(target).ObstacleIndex.HasValue)
                        Check(visual.Obstacles[before.CellAt(target).ObstacleIndex.Value].Durability == 3, "무효 타격 내구도 유지 " + expected);
                    string[] labels = board.GetComponentsInChildren<Transform>().Select(item => item.name).ToArray();
                    Check(expected == DamageResponse.CoverDamage ? labels.Contains("mold-clear") : !labels.Contains("wood-break") && !labels.Contains("metal-break"),
                        "유효 곰팡이 제거만 표시·무효 손상 효과 없음 " + expected);
                    SceneCall(playback, "Tick", timeline.Duration);
                }
                finally { SceneCall(playback, "Reset"); UnityEngine.Object.Destroy(level); }
            }
        }
    }
}
