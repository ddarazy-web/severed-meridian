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
    public static partial class PuzzleSettlementAnimationVerification
    {
        private static async UniTask VerifyRoutesAsync(PuzzleGameSession session, PuzzleWorldBoard board, PuzzleArtwork art)
        {
            BoardCoordinate C(int row, int column) => new BoardCoordinate(row, column);
            BoardCoordinate[] vertical = { C(0, 0), C(1, 0), C(2, 0) };
            BoardCoordinate[] horizontal = { C(0, 0), C(0, 1), C(0, 2) };
            BoardCoordinate[] convoy = Enumerable.Range(0, 5).Select(row => C(row, 0)).ToArray();
            await Route("simultaneous-column", convoy, convoy.Skip(3).ToArray(), level => Source(level, convoy[0], 2), null, MovementKind.Gravity);
            await Route("no-records", new[] { C(0, 0) }, Array.Empty<BoardCoordinate>(), null, null, null);
            await Route("down", vertical, new[] { vertical[1], vertical[2] }, null, null, MovementKind.Gravity);
            await Route("down-fast", vertical, new[] { vertical[1], vertical[2] }, null, null, MovementKind.Gravity);
            await Route("down-slow", vertical, new[] { vertical[1], vertical[2] }, null, null, MovementKind.Gravity);
            await Route("moving-rocket", vertical, new[] { vertical[1], vertical[2] }, null, state =>
            {
                typeof(RuntimeCell).GetProperty("Content").SetValue(state.CellAt(vertical[0]), RuntimeContent.Rocket);
                typeof(RuntimeCell).GetProperty("RocketDirection").SetValue(state.CellAt(vertical[0]), RocketDirection.Horizontal);
                typeof(RuntimeCell).GetProperty("Color").SetValue(state.CellAt(vertical[0]), null);
            }, MovementKind.Gravity);
            await Route("moving-scrap", vertical, new[] { vertical[1], vertical[2] }, null, state =>
                typeof(LevelRuntimeState).GetMethod("SupplyScrap", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(state, new object[] { state.CellAt(vertical[0]), 3 }), MovementKind.Gravity);
            await Route("up", vertical, new[] { vertical[0], vertical[1] }, level => LevelFlowEditing.SetGravity(level, vertical, GravityDirection.Up), null, MovementKind.Gravity);
            await Route("right", horizontal, new[] { horizontal[1], horizontal[2] }, level => LevelFlowEditing.SetGravity(level, horizontal, GravityDirection.Right), null, MovementKind.Gravity);
            await Route("left", horizontal, new[] { horizontal[0], horizontal[1] }, level => LevelFlowEditing.SetGravity(level, horizontal, GravityDirection.Left), null, MovementKind.Gravity);
            BoardCoordinate[] diagonal = { C(0, 0), C(1, 1), C(0, 4), C(1, 5) };
            await Route("existing-diagonal-stays", diagonal, new[] { diagonal[1], diagonal[3] }, null, null, null);
            await Route("fresh-diagonal-batch", diagonal, diagonal, level => { Source(level, diagonal[0]); Source(level, diagonal[2]); }, null, MovementKind.Supply);
            BoardCoordinate[] path = { C(2, 2), C(2, 3), C(3, 3) };
            await Route("path", path, path.Skip(1).ToArray(), level => LevelFlowEditing.SetPath(level, path), null, MovementKind.Path);
            BoardCoordinate[] portal = { C(1, 1), C(6, 6) };
            await Route("portal", portal, new[] { portal[1] }, level => LevelFlowEditing.SetPortal(level, portal[0], portal[1]), null, MovementKind.Portal);
            BoardCoordinate[] supply = { C(4, 4), C(5, 4) };
            await Route("supply-stop", supply, supply, level => Source(level, supply[0]), null, MovementKind.Supply);
            BoardCoordinate[] sidewaysSupply = { C(4, 4), C(4, 5) };
            await Route("supply-path", sidewaysSupply, sidewaysSupply, level =>
            { LevelFlowEditing.SetPath(level, sidewaysSupply); Source(level, sidewaysSupply[0]); }, null, MovementKind.Supply, Vector3.right);
            await Route("adjacent-supply", supply, supply, level =>
            {
                LevelFlowEditing.SetGravity(level, new[] { supply[0] }, GravityDirection.Up);
                Source(level, supply[0]); Source(level, supply[1]);
            }, null, MovementKind.Supply);
            BoardCoordinate[] fixedBoard = Enumerable.Range(0, 4).SelectMany(row => Enumerable.Range(0, 4).Select(column => C(row, column))).ToArray();
            await Route("fixed-layers", fixedBoard, new[] { C(2, 2), C(3, 2) }, level =>
            {
                LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Block, Erase = true }, new[] { C(0, 0), C(0, 1), C(1, 0), C(1, 1) });
                LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)ObstacleKind.Appliance, Durability = 9 }, new[] { C(0, 0) });
                LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Cover, Kind = (int)CoverKind.Web, Durability = 2 }, new[] { C(1, 3) });
                LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Dust, Durability = 2 }, new[] { C(2, 2) });
                LevelFlowEditing.SetWalls(level, new[] { new BoardEdge(C(1, 2), C(1, 3)) }, false);
            }, null, MovementKind.Gravity);
            await Route("recovery", vertical, new[] { vertical[1], vertical[2] }, level =>
            {
                LevelFlowEditing.SetArrival(level, vertical[2], false);
                LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Block, Erase = true }, new[] { vertical[0] });
                string error = LevelSupplyEditing.PlaceRecovery(level, new[] { vertical[0] });
                if (error != null) throw new InvalidOperationException(error);
                JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":" + (int)MissionKind.Recovery + ",\"count\":1}]}", level);
            }, state =>
            {
                typeof(RuntimeCell).GetProperty("Content").SetValue(state.CellAt(vertical[0]), RuntimeContent.Recovery);
                typeof(RuntimeCell).GetProperty("Color").SetValue(state.CellAt(vertical[0]), null);
            }, MovementKind.Gravity);

            await Route("invalid-flow", vertical, Array.Empty<BoardCoordinate>(), null, state =>
                typeof(RuntimeCell).GetProperty("Gravity").SetValue(state.CellAt(vertical[1]), GravityDirection.Up), null, rejected: true);
            await Route("missing-renderer", vertical, new[] { vertical[1], vertical[2] }, null, null, MovementKind.Gravity, breakPresentation: true);

            async UniTask Route(string name, BoardCoordinate[] active, BoardCoordinate[] empty, Action<LevelDefinition> configure,
                Action<LevelRuntimeState> arrange, MovementKind? expected, Vector3? entryDirection = null, bool rejected = false, bool breakPresentation = false)
            {
                LevelDefinition level = (LevelDefinition)typeof(SettlementVerification).GetMethod("Make", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { active });
                try
                {
                    Call(session, "ResetPresentation"); Set(session, "ready", false);
                    Set(session, "fallSeconds", name == "down-fast" ? .06f : name == "down-slow" ? .24f : .12f);
                    configure?.Invoke(level);
                    LevelStateBuildResult built = LevelStateBuilder.Build(level, 12345);
                    Check(built.IsBuilt, name + " fixture valid: " + string.Join(" / ", built.Issues));
                    LevelRuntimeState state = built.State;
                    foreach (BoardCoordinate at in empty)
                    {
                        RuntimeCell cell = state.CellAt(at);
                        typeof(RuntimeCell).GetProperty("Content").SetValue(cell, RuntimeContent.Empty);
                        typeof(RuntimeCell).GetProperty("Color").SetValue(cell, null);
                    }
                    arrange?.Invoke(state);
                    await art.PrepareAsync(state, CancellationToken.None);
                    SettlementResult direct = SettlementResolution.Resolve(state);
                    Check(rejected ? !direct.IsApplied : direct.IsApplied && (expected.HasValue ? direct.Records.Any(record => record.Kind == expected) : direct.Records.Count == 0), name + " expected records");
                    BoardActionExecutor executor = new BoardActionExecutor(state);
                    typeof(BoardActionExecutor).GetProperty("Phase").SetValue(executor, BoardActionPhase.WaitingForFall);
                    Set(session, "executor", executor); Set(session, "ready", true);
                    board.Draw(state, art);
                    Dictionary<BoardCoordinate, SpriteRenderer> owners = state.Cells.Where(cell => board.OccupantAt(cell.Coordinate) != null)
                        .ToDictionary(cell => cell.Coordinate, cell => board.OccupantAt(cell.Coordinate));
                    Dictionary<SpriteRenderer, Vector3> scales = owners.Values.ToDictionary(image => image, image => image.transform.localScale);
                    Dictionary<SpriteRenderer, Vector3> initialPositions = owners.Values.ToDictionary(image => image, image => image.transform.position);
                    Dictionary<SpriteRenderer, Color> colors = owners.Values.ToDictionary(image => image, image => image.color);
                    Dictionary<SpriteRenderer, int> orders = owners.Values.ToDictionary(image => image, image => image.sortingOrder);
                    SettlementRecord first = direct.Records.FirstOrDefault();
                    SpriteRenderer original = first == null || first.Kind == MovementKind.Supply ? null : board.OccupantAt(first.Source);
                    Vector3 sourcePosition = original == null ? Vector3.zero : original.transform.position;
                    Dictionary<SpriteRenderer, Vector3> floorPositions = board.GetComponentsInChildren<SpriteRenderer>()
                        .Where(image => image.name == "Floor" || image.name == "Cover" || image.name == "Dust" || image.name == "Wall" || (name == "fixed-layers" && image.name.StartsWith("Obstacle-")))
                        .ToDictionary(image => image, image => image.transform.position);
                    if (breakPresentation) original.enabled = false;
                    Call(session, "Advance");
                    if (breakPresentation)
                    {
                        Check(session.HasFailed && !session.IsPresenting && !session.CanAcceptInput && art.AtlasCount == 0, "연출 오류 시 표시·입력 잠금·아틀라스 정리");
                        return;
                    }
                    if (rejected)
                    {
                        Check(!session.IsPresenting && session.Phase == BoardActionPhase.Stopped && Snapshot(session.State) == Snapshot(state), "정착 거절 상태 보존·재생 잠금 없음");
                        return;
                    }
                    if (!expected.HasValue)
                    {
                        Check(!session.IsPresenting && !session.HasFailed && Snapshot(session.State) == Snapshot(direct.State), "기록 없는 정착 즉시 표시·잠금 없음");
                        return;
                    }
                    Check(session.IsPresenting && !session.CanAcceptInput, name + " starts and gates input");
                    Tick(session, .03f);
                    if (name == "simultaneous-column")
                    {
                        Check(owners.All(pair => Vector3.Distance(pair.Value.transform.position, initialPositions[pair.Value]) > .01f),
                            "위·중간·아래 기존 블록이 첫 프레임부터 함께 낙하");
                        Check(owners.Values.All(image => Mathf.Abs(Vector3.Distance(image.transform.position, initialPositions[image]) - .36f) < .001f),
                            "기존 세 블록의 낙하 속도 추가 20% 증가·간격 일치");
                        Check(board.GetComponentsInChildren<SpriteRenderer>().Count(image => image.name == "Supply-playback") == 2,
                            "신규 공급 블록도 낙하 시작부터 대기열 준비");
                    }
                    if (expected == MovementKind.Supply)
                    {
                        SpriteRenderer supplied = board.GetComponentsInChildren<SpriteRenderer>().First(image => image.name == "Supply-playback");
                        Check(supplied.maskInteraction == SpriteMaskInteraction.VisibleInsideMask, name + " supply clipped to entry cell");
                        if (name == "adjacent-supply") VerifySupplyIsolation(board);
                        if (entryDirection.HasValue)
                        {
                            Vector3 delta = PuzzleWorldBoard.CellPosition(first.Target) - board.transform.InverseTransformPoint(supplied.transform.position);
                            Check(Vector3.Dot(delta.normalized, entryDirection.Value) > .99f, name + " source flow entry direction");
                        }
                    }
                    if (original != null)
                    {
                        if (expected == MovementKind.Portal)
                            Check(original.transform.position == sourcePosition && original.color.a < 1, name + " portal stays at entrance while fading");
                        else Check(Vector3.Distance(original.transform.position, sourcePosition) > .001f, name + " intermediate movement");
                    }
                    Vector3[] paused = board.GetComponentsInChildren<SpriteRenderer>().Select(image => image.transform.position).ToArray();
                    Check(session.SetPaused(true), name + " pause allowed"); Tick(session, .5f);
                    Check(board.GetComponentsInChildren<SpriteRenderer>().Select(image => image.transform.position).SequenceEqual(paused), name + " pause freezes positions");
                    session.SetPaused(false);
                    if (expected != MovementKind.Supply && name != "simultaneous-column") VerifyRecordedFrames(session, board, direct, owners, name);
                    for (int frame = 0; frame < 1000 && session.IsPresenting; frame++) Tick(session, .02f);
                    Check(!session.IsPresenting && !session.HasFailed, name + " playback ends");
                    bool Restored(SpriteRenderer image) => image.sprite == null
                        ? !image.enabled && image.transform.localScale == Vector3.one && image.color == Color.white && image.sortingOrder == 0 && !image.flipX && !image.flipY
                        : image.transform.localScale == scales[image] && image.color == colors[image] && image.sortingOrder == orders[image];
                    foreach (KeyValuePair<SpriteRenderer, Vector3> pair in scales.Where(pair => !Restored(pair.Key)))
                        results.Add("INFO " + name + " restored slot=" + pair.Key.name + " sprite=" + pair.Key.sprite?.name +
                            " scale=" + pair.Key.transform.localScale + "/" + pair.Value + " color=" + pair.Key.color + "/" + colors[pair.Key] +
                            " order=" + pair.Key.sortingOrder + "/" + orders[pair.Key]);
                    Check(scales.Keys.All(Restored), name + " 활성 그림의 축척·색·순서 복원 및 빈 슬롯 초기화");
                    Check(Snapshot(session.State) == Snapshot(direct.State), name + " state/random/supply/recovery identical");
                    Check(floorPositions.All(pair => pair.Key.transform.position == pair.Value), name + " fixed layers stay");
                    Check(!board.GetComponentsInChildren<SpriteRenderer>().Any(image => image.name == "Supply-playback"), name + " temporary supply returned");
                    foreach (RuntimeCell cell in session.State.Cells.Where(cell => cell.IsActive && cell.Content != RuntimeContent.Empty &&
                        !(cell.Content == RuntimeContent.Obstacle && LevelPlacementRules.Size(session.State.Obstacles[cell.ObstacleIndex.Value].Definition.Kind) == 2)))
                        Check(board.OccupantAt(cell.Coordinate)?.sprite != null, name + " final occupant " + cell.Coordinate);
                    if (name == "recovery") Check(session.State.Recoveries.Count == 1 && board.OccupantAt(vertical[2]) == null, "recovered item not resurrected");
                    if (name == "supply-stop") Check(session.State.Cells.Count(cell => cell.IsActive && cell.Content == RuntimeContent.Empty) == 1, "exhausted supply leaves empty cell");
                }
                finally { UnityEngine.Object.Destroy(level); }
            }

            void Source(LevelDefinition level, BoardCoordinate at, int count = 1)
            {
                typeof(SettlementVerification).GetMethod("Source", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,
                    new object[] { level, at, SupplyExhaustion.Stop, new[] { new SupplyItem(SupplyKind.RandomNormal, count) } });
            }
        }
    }
}
