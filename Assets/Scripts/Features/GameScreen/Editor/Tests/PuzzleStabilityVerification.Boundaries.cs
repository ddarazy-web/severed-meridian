using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Board;
using Simulation;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace GameScreen.Editor
{
    public static partial class PuzzleStabilityVerification
    {
        private static async UniTask InterruptionBoundaryChecks()
        {
            PuzzleGameSession session = UnityEngine.Object.FindFirstObjectByType<PuzzleGameSession>();
            HashSet<string> observed = new HashSet<string>();
            for (int kind = 0; kind < 5; kind++)
            {
                var level = await PrepareFixture(session, kind);
                try
                {
                    await LayoutDuringPlaybackChecks(session, "preview");
                    BoardActionExecutor direct = BeginFixture(session, kind);
                    for (int frame = 0; frame < 20000 && Busy(session); frame++)
                    {
                        await Step(session, .02f);
                        foreach (string stage in Stages(session).ToArray())
                            if (observed.Add(stage))
                            {
                                string state = Snapshot(session.State), clock = Clocks(session);
                                Check(session.SetPaused(true), stage + " pause 진입");
                                await Step(session, .4f);
                                Check(Clocks(session) == clock && Snapshot(session.State) == state && !session.AudioPlayback.Play(PuzzleFeedbackCueKind.Match), stage + " pause 표시 시간·규칙·새 음성 정지");
                                Check(session.SetPaused(false), stage + " resume 진입");
                                await LayoutDuringPlaybackChecks(session, stage);
                                Invoke(session, "OnApplicationPause", true);
                                await Step(session, .01f);
                                Check(session.AudioPlayback.ActiveVoices == 0 && ((PuzzleFeedbackSchedule)Field(session, "audioSchedule")).PendingCount == 0, stage + " background 과거 음성·예약 없음");
                                Invoke(session, "OnApplicationPause", false);
                            }
                    }
                    await Finish(session, direct);
                }
                finally { UnityEngine.Object.Destroy(level); }
            }
            foreach (string stage in new[] { "swap", "return", "removal", "fall", "supply", "power", "drone", "collection", "lastpang" })
                Check(observed.Contains(stage), "실제 중단·레이아웃 경계 관찰 " + stage);
            await ExistingSceneCheck("ReviewChecks");
        }
        private static IEnumerable<string> Stages(PuzzleGameSession session)
        {
            object swap = Field(session, "swapPlayback"), power = Field(session, "powerPlayback"), settlement = Field(session, "settlementPlayback");
            if (Playing(swap)) yield return (bool)Field(swap, "returnTrip") && (float)Field(swap, "elapsed") >= (float)Field(swap, "duration") ? "return" : "swap";
            if (Playing(power))
            {
                var timeline = (PuzzleEffectTimeline)Field(power, "timeline"); float elapsed = (float)Field(power, "elapsed");
                if (elapsed < .12f && timeline.Changes.Any(change => change.IsConsumed)) yield return "removal";
                else yield return "power";
                if (((IEnumerable)Field(power, "clips")).Cast<object>().Any(clip => (string)Field(clip, "Label") == "Drone-flight" && elapsed >= (float)Field(clip, "Start") && elapsed < (float)Field(clip, "End"))) yield return "drone";
            }
            if (Playing(Field(session, "removalPlayback"))) yield return "removal";
            if (Playing(settlement))
            {
                float elapsed = (float)Field(settlement, "elapsed");
                foreach (object track in (IEnumerable)Field(settlement, "tracks"))
                    foreach (object move in (IEnumerable)Field(track, "Moves"))
                        if (elapsed >= (float)Field(move, "Begins") && elapsed < (float)Field(move, "Begins") + (float)Field(move, "Seconds"))
                            yield return ((SettlementRecord)Field(move, "Record")).Kind == MovementKind.Supply ? "supply" : "fall";
            }
            if (session.ProgressFeedback.Flights.Count > 0) yield return "collection";
            if ((bool)Field(session, "lastPangFeedback") || (float)Field(session, "lastPangEnding") > 0) yield return "lastpang";
        }
        private static string Clocks(PuzzleGameSession session)
        {
            List<float> values = new List<float> { (float)Field(session.ProgressFeedback, "clock"), (float)Field(session, "startRemaining"), (float)Field(session, "lastPangEnding") };
            foreach (string name in new[] { "swapPlayback", "removalPlayback", "settlementPlayback", "powerPlayback" })
            { object playback = Field(session, name); if (playback != null) values.Add((float)Field(playback, "elapsed")); }
            return string.Join("/", values.Select(value => value.ToString("R", System.Globalization.CultureInfo.InvariantCulture)));
        }
        private static async UniTask LayoutDuringPlaybackChecks(PuzzleGameSession session, string stage)
        {
            PuzzleWorldBoard board = UnityEngine.Object.FindFirstObjectByType<PuzzleWorldBoard>();
            PuzzleScreenLayout layout = UnityEngine.Object.FindFirstObjectByType<PuzzleScreenLayout>();
            PuzzleBoardInput input = UnityEngine.Object.FindFirstObjectByType<PuzzleBoardInput>();
            PuzzleHudView hud = UnityEngine.Object.FindFirstObjectByType<PuzzleHudView>();
            BoardCoordinate cell = session.State.Cells.First(candidate => candidate.IsActive && board.OccupantAt(candidate.Coordinate) != null).Coordinate;
            SpriteRenderer selected = board.OccupantAt(cell); int order = selected.sortingOrder;
            string original = Snapshot(session.State);
            PuzzleProgressFeedback.Flight flight = stage == "collection" ? session.ProgressFeedback.Flights.First() : null;
            int progressBefore = flight != null ? session.DisplayedMissionProgress(flight.MissionIndex) : 0;
            if (stage == "preview") { board.Preview(cell, Vector3.right * .2f); Check(selected.sortingOrder == 50, "preview 최상위 order"); }
            foreach (Vector2 size in new[] { new Vector2(1920, 1080), new Vector2(1080, 1920) })
            {
                PuzzleUIRenderVerification.SetSize((int)size.x, (int)size.y);
                for (int frame = 0; frame < 8; frame++) await UniTask.Yield();
                Check(Screen.width == (int)size.x && Screen.height == (int)size.y, "실제 Game View 크기 " + size);
                Rect safe = new Rect(24, 36, size.x - 48, size.y - 72); layout.ApplyLayout(safe, size); Canvas.ForceUpdateCanvases(); hud.Frame(session);
                Vector2 center = session.BoardCamera.WorldToScreenPoint(board.transform.TransformPoint(PuzzleWorldBoard.CellPosition(cell)));
                Check(input.TryGetCoordinate(center, out BoardCoordinate hit) && hit.Equals(cell), stage + " 화면 전환 후 입력 좌표 " + size + " point=" + center + " pixelRect=" + session.BoardCamera.pixelRect + " hit=" + hit + " cell=" + cell);
                Check(safe.Contains(layout.BoardScreenRect.min) && safe.Contains(layout.BoardScreenRect.max - Vector2.one * .01f), stage + " 안전 영역 내 보드 " + size);
                if (stage == "preview") Check(selected.sortingOrder == order && !input.Selected.HasValue, "회전으로 제스처 취소·order 복원 " + size);
                if (flight != null)
                {
                    Invoke(session, "TickProgress", Mathf.Max(0, .32f * .99f - flight.Progress * .32f)); hud.Frame(session);
                    Image icon = ((IEnumerable)Field(hud, "collections")).Cast<Image>().ElementAt(flight.Slot);
                    PuzzleMissionView target = ((IEnumerable)Field(hud, "views")).Cast<PuzzleMissionView>().ElementAt(flight.MissionIndex);
                    Vector2 actual = RectTransformUtility.WorldToScreenPoint(null, icon.rectTransform.position);
                    Vector2 destination = RectTransformUtility.WorldToScreenPoint(null, target.Icon.position);
                    Check(Vector2.Distance(actual, destination) < 3 && session.DisplayedMissionProgress(flight.MissionIndex) == progressBefore && !icon.raycastTarget,
                        "회전·inset 후 실제 수집 HUD 3px 도착·수치 대기 " + size);
                }
            }
            if (flight != null)
            { Invoke(session, "TickProgress", .02f); Check(!session.ProgressFeedback.Flights.Contains(flight) && session.DisplayedMissionProgress(flight.MissionIndex) >= progressBefore + flight.Amount, "실제 수집 도착 후 표시 수치 반영"); }
            if (stage == "preview") { board.ClearPreview(); Check(selected.sortingOrder == order, "회전 후 preview order 복원"); }
            Check(Snapshot(session.State) == original, stage + " 화면 전환 규칙 상태 불변");
        }
        private static int[] PoolCounts(PuzzleGameSession session)
        {
            PuzzleWorldBoard board = UnityEngine.Object.FindFirstObjectByType<PuzzleWorldBoard>(); PuzzleHudView hud = UnityEngine.Object.FindFirstObjectByType<PuzzleHudView>();
            return new[] { ((IList)Field(board, "cells")).Count, ((IList)Field(board, "bodies")).Count, ((IList)Field(board, "decorations")).Count,
                ((IList)Field(board, "supplyImages")).Count, ((IList)Field(board, "supplyClips")).Count, ((IList)Field(board, "effects")).Count,
                hud.CollectionPoolCount, session.AudioPlayback.SourceCount, session.AudioPlayback.ClipCount, ((PuzzleArtwork)Field(session, "artwork")).AtlasCount };
        }
        private static async UniTask PoolReuseChecks()
        {
            PuzzleGameSession session = UnityEngine.Object.FindFirstObjectByType<PuzzleGameSession>(); List<string> records = new List<string> { "fixture,repeat,cells,bodies,decorations,supplyImages,supplyClips,effects,collections,voices,clips,atlases" };
            List<string> phases = new List<string> { "fixture,repeat,stage,capacities,activeSupply,activeEffects,activeCollections,activeVoices" };
            int[] small = null;
            foreach (int kind in new[] { 2, 4 })
            {
                int[] capacity = null;
                for (int repeat = 0; repeat < 10; repeat++)
                {
                    var level = await PrepareFixture(session, kind);
                    try
                    {
                        HashSet<string> seen = new HashSet<string>();
                        BoardActionExecutor direct = BeginFixture(session, kind);
                        await Finish(session, direct, () =>
                        {
                            foreach (string stage in Stages(session))
                                if (seen.Add(stage))
                                {
                                    var images = UnityEngine.Object.FindFirstObjectByType<PuzzleWorldBoard>().GetComponentsInChildren<SpriteRenderer>();
                                    int collections = UnityEngine.Object.FindFirstObjectByType<PuzzleHudView>().GetComponentInParent<Canvas>().GetComponentsInChildren<Image>().Count(image => image.name.StartsWith("MissionCollection"));
                                    phases.Add(kind + "," + repeat + "," + stage + ",\"" + string.Join("/", PoolCounts(session)) + "\"," +
                                        images.Count(image => image.name == "Supply-playback" && image.enabled) + "," + images.Count(image => image.name == "Effect-playback" && image.enabled) + "," + collections + "," + session.AudioPlayback.ActiveVoices);
                                }
                        });
                        await Step(session, .6f);
                        int[] current = PoolCounts(session); records.Add(kind + "," + repeat + "," + string.Join(",", current));
                        if (repeat == 4) capacity = current;
                        if (repeat >= 5) Check(current.SequenceEqual(capacity), "워밍업5 이후 풀 용량 안정 " + kind + "/" + repeat);
                        Check(current[7] == 8 && current[8] == 14, "반복 음성8/클립14 " + kind + "/" + repeat);
                        PuzzleWorldBoard board = UnityEngine.Object.FindFirstObjectByType<PuzzleWorldBoard>();
                        Check(!board.GetComponentsInChildren<Transform>().Any(item => item.name == "Supply-playback") &&
                            !board.GetComponentsInChildren<SpriteRenderer>().Any(image => image.name == "Effect-playback" && image.enabled) &&
                            !UnityEngine.Object.FindFirstObjectByType<PuzzleHudView>().GetComponentInParent<Canvas>().GetComponentsInChildren<Image>().Any(image => image.name.StartsWith("MissionCollection")) && session.AudioPlayback.ActiveVoices == 0,
                            "반복 종료 비사용 공급·수집·음성 잔류 없음 " + kind + "/" + repeat);
                    }
                    finally { UnityEngine.Object.Destroy(level); }
                }
                if (kind == 2) small = capacity; else Check(capacity.Zip(small, (large, prior) => large >= prior).All(value => value), "추가 동시 표시량 풀 용량 확장 허용");
            }
            File.WriteAllLines(Output + "pool-observation.csv", records);
            File.WriteAllLines(Output + "pool-phase-observation.csv", phases);
        }
        private static async UniTask BackgroundResultChecks()
        {
            PuzzleGameSession session = UnityEngine.Object.FindFirstObjectByType<PuzzleGameSession>();
            foreach (bool won in new[] { true, false })
                foreach (bool paused in new[] { false, true })
                {
                    var level = await PrepareFixture(session, won ? 3 : 0);
                    List<PuzzleFeedbackCueKind> heard = new List<PuzzleFeedbackCueKind>();
                    void Played(PuzzleFeedbackCueKind kind) { heard.Add(kind); }
                    session.AudioPlayback.Played += Played;
                    try
                    {
                        if (!won)
                        {
                            JsonUtility.FromJsonOverwrite("{\"moveCount\":1}", level);
                            typeof(PuzzleGameSession).GetField("initialBytes", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                                .SetValue(session, Levels.LevelPackCodec.Encode(new[] { level }));
                            await session.RestartAsync(CancellationToken.None); Invoke(session, "TickProgress", .7f); heard.Clear();
                        }
                        BoardActionExecutor direct = BeginFixture(session, won ? 3 : 0);
                        for (int frame = 0; frame < 20000 && session.Outcome == null; frame++) await Step(session, .02f);
                        Check(session.Outcome != null && !session.ResultReady, "백그라운드 결과 표시 직전 " + won + "/" + paused);
                        Invoke(session, "OnApplicationPause", true);
                        if (paused)
                        {
                            Check(session.SetPaused(true), "결과 직전 pause/background 겹침");
                            string clocks = Clocks(session); await Step(session, .4f);
                            Invoke(session, "OnApplicationPause", false);
                            Check(Clocks(session) == clocks && !session.ResultReady && !session.AudioPlayback.Play(PuzzleFeedbackCueKind.Match), "pause 중 복귀 표시·새 음성 정지");
                            Check(session.SetPaused(false), "겹친 pause 재개");
                            Invoke(session, "OnApplicationPause", true);
                        }
                        await Finish(session, direct);
                        PuzzleScreenView screen = UnityEngine.Object.FindFirstObjectByType<PuzzleScreenView>();
                        Check(session.ResultReady && session.Outcome.Kind == (won ? BoardOutcomeKind.Won : BoardOutcomeKind.MovesExhausted) &&
                            ReferenceEquals(Field(screen, "shownOutcome"), session.Outcome), "백그라운드에서 실제 결과 Refresh 완료 " + won + "/" + paused);
                        Check(heard.All(kind => kind != PuzzleFeedbackCueKind.Win && kind != PuzzleFeedbackCueKind.Lose), "백그라운드 결과음 0회");
                        Invoke(session, "OnApplicationPause", false);
                        PuzzleFeedbackCueKind expected = won ? PuzzleFeedbackCueKind.Win : PuzzleFeedbackCueKind.Lose;
                        Check(heard.Count(kind => kind == expected) == 1, "백그라운드 완료 뒤 복귀 결과음 한 번 " + won + "/" + paused);
                        Invoke(screen, "Refresh"); session.PlayResultFeedback();
                        Invoke(session, "OnApplicationPause", true); Invoke(session, "OnApplicationPause", false);
                        Check(heard.Count(kind => kind == expected) == 1, "추가 Refresh·이미 재생한 결과 재복귀 중복0 " + won + "/" + paused);
                    }
                    finally { session.AudioPlayback.Played -= Played; Invoke(session, "OnApplicationPause", false); UnityEngine.Object.Destroy(level); }
                }
        }
        private static async UniTask LifetimeChecks()
        {
            PuzzleGameSession session = UnityEngine.Object.FindFirstObjectByType<PuzzleGameSession>();
            for (int repeat = 0; repeat < 5; repeat++)
            {
                var level = await PrepareFixture(session, 2);
                try
                {
                    string initial = Snapshot(session.State); BeginFixture(session, 2); await Step(session, .02f);
                    Check(session.IsPresenting, "다시하기 직전 실제 드론 표시 중 " + repeat);
                    await session.RestartAsync(CancellationToken.None); Invoke(session, "TickProgress", .7f); session.enabled = true;
                    Check(session.CanAcceptInput && Snapshot(session.State) == initial && !session.IsPresenting && !session.HasProgressFeedback &&
                        session.AudioPlayback.ActiveVoices == 0 && ((PuzzleFeedbackSchedule)Field(session, "audioSchedule")).PendingCount == 0,
                        "실제 표시 중 다시하기5회 이전 보드·소리·수집 잔류 없음 " + repeat);
                    session.enabled = false;
                }
                finally { UnityEngine.Object.Destroy(level); }
            }
            await PendingLifetimeChecks();
            await ExistingSceneCheck("LifetimeChecks", UnityEngine.Object.FindFirstObjectByType<PuzzleGameSession>());
            await ExistingSceneCheck("PendingAudioDestructionCheck");
        }
        private static async UniTask PendingLifetimeChecks()
        {
            PuzzleGameSession session = UnityEngine.Object.FindFirstObjectByType<PuzzleGameSession>();
            for (int mode = 0; mode < 3; mode++)
            {
                var level = await PrepareFixture(session, 5);
                List<PuzzleFeedbackCueKind> heard = new List<PuzzleFeedbackCueKind>();
                void Played(PuzzleFeedbackCueKind kind) { heard.Add(kind); }
                session.AudioPlayback.Played += Played;
                try
                {
                    PuzzleArtwork old = (PuzzleArtwork)Field(session, "artwork"); old.Dispose(); old = new PuzzleArtwork();
                    typeof(PuzzleGameSession).GetField("artwork", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(session, old);
                    await old.PrepareAsync(session.State, CancellationToken.None);
                    UnityEngine.Object.FindFirstObjectByType<PuzzleWorldBoard>().Draw(session.State, old);
                    session.enabled = true; Check(session.TryActivate(new BoardCoordinate(4, 4)), "미로드 효과 실제 파워 실행 " + mode); session.enabled = false;
                    Check((bool)Field(session, "preparingEffects") && (int)Field(old, "pending") > 0, "실제 효과 로드 pending 전제 " + mode);
                    if (mode == 0) await session.RestartAsync(CancellationToken.None);
                    else if (mode == 1)
                    {
                        using CancellationTokenSource cancelled = new CancellationTokenSource(); cancelled.Cancel();
                        await session.RestartAsync(cancelled.Token);
                    }
                    else Invoke(session, "Fail", "Stage11 의도한 실제 pending 중 오류 경계");
                    float deadline = Time.realtimeSinceStartup + 20;
                    while ((int)Field(old, "pending") > 0 && Time.realtimeSinceStartup < deadline) await UniTask.Yield();
                    Check((int)Field(old, "pending") == 0 && old.AtlasCount == 0 && !(bool)Field(session, "preparingEffects") &&
                        ((PuzzleFeedbackSchedule)Field(session, "audioSchedule")).PendingCount == 0 && heard.All(kind => kind == PuzzleFeedbackCueKind.Start) && heard.Count == (mode == 0 ? 1 : 0),
                        "pending 중 재시작/취소/오류 늦은 파워·예약·아틀라스 잔류 없음 " + mode);
                    if (mode != 0) await session.RestartAsync(CancellationToken.None);
                    Invoke(session, "TickProgress", .7f); session.enabled = true;
                    Check(session.CanAcceptInput && session.AudioPlayback.SourceCount == 8 && session.AudioPlayback.ClipCount == 14, "pending 경계 후 새 게임 입력·풀 정상 " + mode);
                    session.enabled = false;
                }
                finally { session.AudioPlayback.Played -= Played; UnityEngine.Object.Destroy(level); }
            }
        }
    }
}
