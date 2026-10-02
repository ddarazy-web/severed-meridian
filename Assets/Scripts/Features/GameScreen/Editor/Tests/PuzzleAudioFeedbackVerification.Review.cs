using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Board;
using Levels;
using Levels.Editor;
using Simulation;
using Cysharp.Threading.Tasks;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameScreen.Editor
{
    public static partial class PuzzleAudioFeedbackVerification
    {
        private static async UniTask ReviewChecks()
        {
            List<Exception> failures = new List<Exception>();
            float previous = Time.timeScale; Time.timeScale = 0;
            try
            {
                for (int scenario = 0; scenario < 4; scenario++)
                {
                    PuzzleGameSession session = UnityEngine.Object.FindFirstObjectByType<PuzzleGameSession>();
                    LevelDefinition level = null;
                    List<PuzzleFeedbackCueKind> heard = new List<PuzzleFeedbackCueKind>();
                    void Played(PuzzleFeedbackCueKind kind) { heard.Add(kind); }
                    session.AudioPlayback.Played += Played;
                    try
                    {
                        string missions = scenario == 1 ? "[{\"kind\":0,\"color\":0,\"count\":1},{\"kind\":0,\"color\":1,\"count\":100}]" :
                            "[{\"kind\":0,\"color\":0,\"count\":" + (scenario == 2 ? 1 : 100) + "}]";
                        level = await ReviewFixture(session, scenario == 0 ? InitialBlockKind.Magnet : InitialBlockKind.Rocket, missions);
                        heard.Clear(); BoardCoordinate origin = new BoardCoordinate(4, 4);
                        if (scenario == 3)
                        {
                            BoardCoordinate first = default, second = default; bool found = false;
                            foreach (RuntimeCell cell in session.State.Cells)
                            {
                                BoardCoordinate right = new BoardCoordinate(cell.Coordinate.Row, cell.Coordinate.Column + 1);
                                if (right.Column < 9 && ActionQuery.Swap(session.State, cell.Coordinate, right).Reason == ActionReason.NoNewMatch &&
                                    session.CanSelectItemTarget(BoardItem.Swap, cell.Coordinate) && session.CanSelectItemTarget(BoardItem.Swap, right))
                                { first = cell.Coordinate; second = right; found = true; break; }
                            }
                            Check(found, "리뷰 교환 아이템 무매칭 유효 칸 fixture");
                            BoardActionExecutor direct = new BoardActionExecutor(session.State); direct.UseItem(BoardItem.Swap, first, second);
                            Check(session.TryUseItem(BoardItem.Swap, first, second), "실제 유효 교환 아이템 실행");
                            session.enabled = false;
                            Check(heard.Count(kind => kind == PuzzleFeedbackCueKind.Swap) == 1, "교환 아이템 실제 교환음 한 번");
                            Check(Snapshot(session.State) == Snapshot(direct.State), "교환 아이템 소리 추가 후 규칙 상태 동등");
                            continue;
                        }
                        Check(session.TryActivate(origin), "리뷰 실제 파워 실행 " + scenario); session.enabled = false;
                        await Frame(session, 0);
                        if (scenario == 0)
                        {
                            object playback = typeof(PuzzleGameSession).GetField("powerPlayback", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(session);
                            PuzzleEffectTimeline timeline = (PuzzleEffectTimeline)playback.GetType().GetField("timeline", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(playback);
                            float impact = timeline.Reactions.Where(reaction => reaction.Record.Response == DamageResponse.Remove || reaction.Record.Response == DamageResponse.Damage).Min(reaction => reaction.Time);
                            Check(impact > .01f, "자석 실제 유효 반응 시각 fixture");
                            await Frame(session, impact - .001f);
                            bool early = heard.Contains(PuzzleFeedbackCueKind.Magnet);
                            await Frame(session, .002f);
                            Check(!early && heard.Count(kind => kind == PuzzleFeedbackCueKind.Magnet) == 1, "자석 실제 타격 직전 0회·직후 한 번");
                        }
                        else if (scenario == 1)
                        {
                            bool completed = false, mixed = false;
                            for (int frame = 0; frame < 100 && session.HasProgressFeedback; frame++)
                            {
                                heard.Clear(); await Frame(session, .5f);
                                if (heard.Contains(PuzzleFeedbackCueKind.MissionComplete))
                                { completed = true; mixed |= heard.Contains(PuzzleFeedbackCueKind.MissionArrival); }
                            }
                            Check(completed && session.DisplayedMissionProgress(1) > 0, "다중 미션 같은 프레임 완료·부분 도착 fixture");
                            Check(!mixed, "다중 미션 동시 도착은 완료음만 재생");
                        }
                        else
                        {
                            for (int frame = 0; frame < 10000 && session.Outcome == null; frame++) await Frame(session, .02f);
                            Check(session.Outcome != null && !session.ResultReady, "백그라운드 결과 확정·표시 전 fixture");
                            Invoke(session, "OnApplicationPause", true); Invoke(session, "OnApplicationPause", false); heard.Clear();
                            BoardActionExecutor executor = (BoardActionExecutor)typeof(PuzzleGameSession).GetField("executor", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(session);
                            for (int frame = 0; frame < 10000 && (session.IsPresenting || executor.HasPendingCascade || session.HasProgressFeedback); frame++) await Frame(session, .02f);
                            session.PlayResultFeedback();
                            bool futurePlayed = heard.Count(kind => kind == PuzzleFeedbackCueKind.Win) == 1;
                            Invoke(session, "OnApplicationPause", true); Invoke(session, "OnApplicationPause", false); heard.Clear(); session.PlayResultFeedback();
                            Check(futurePlayed && heard.Count == 0, "표시 전 복귀는 미래 승리음 한 번·표시 후 복귀는 중복 없음");
                        }
                    }
                    catch (Exception error) { failures.Add(error); results.Add("FAIL 리뷰 재현 " + scenario + ": " + error.Message); }
                    finally
                    {
                        session.AudioPlayback.Played -= Played; if (level != null) UnityEngine.Object.Destroy(level);
                        Invoke(session, "ResetPresentation"); Invoke(session, "ClearProgress"); session.enabled = true;
                    }
                }
                PuzzleGameSession current = UnityEngine.Object.FindFirstObjectByType<PuzzleGameSession>();
                Type playbackType = typeof(PuzzleGameSession).Assembly.GetType("GameScreen.PuzzlePowerPlayback");
                object empty = Activator.CreateInstance(playbackType, true);
                playbackType.GetField("timeline", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(empty,
                    new PuzzleEffectTimeline(current.State, Array.Empty<MatchedBlockChange>(), Array.Empty<EffectRecord>(), null));
                Check(!((IEnumerable<PuzzleFeedbackCue>)playbackType.GetProperty("AudioCues", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(empty)).Any(),
                    "빈 변화·공격 시간표는 소리 0회");
                await LargeObstacleAudioCheck();
                await PendingAudioDestructionCheck();
                if (failures.Count > 0) throw new AggregateException("리뷰 재현 실패", failures);
            }
            finally { Time.timeScale = previous; }
        }

        private static async UniTask<LevelDefinition> ReviewFixture(PuzzleGameSession session, InitialBlockKind kind, string missions)
        {
            session.enabled = false; session.SoundEnabled = true;
            LevelDefinition level = (LevelDefinition)typeof(PowerEffectVerification).GetMethod("Make", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            typeof(PowerEffectVerification).GetMethod("Place", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,
                new object[] { level, new BoardCoordinate(4, 4), kind, RocketDirection.Horizontal, RabbitColor.Type1 });
            JsonUtility.FromJsonOverwrite("{\"missions\":" + missions + "}", level);
            LevelStateBuildResult built = LevelStateBuilder.Build(level, 12345); Check(built.IsBuilt, "리뷰 규칙 fixture 유효");
            Invoke(session, "ResetPresentation");
            typeof(PuzzleGameSession).GetField("executor", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(session, new BoardActionExecutor(built.State));
            Invoke(session, "InitializeProgress");
            PuzzleArtwork art = (PuzzleArtwork)typeof(PuzzleGameSession).GetField("artwork", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(session);
            await art.PrepareAsync(built.State, CancellationToken.None); UnityEngine.Object.FindFirstObjectByType<PuzzleWorldBoard>().Draw(session.State, art);
            session.enabled = true; return level;
        }

        private static async UniTask PendingAudioDestructionCheck()
        {
            PuzzleGameSession session = UnityEngine.Object.FindFirstObjectByType<PuzzleGameSession>();
            LevelDefinition level = await ReviewFixture(session, InitialBlockKind.Rocket, "[{\"kind\":0,\"color\":0,\"count\":100}]");
            try
            {
                PuzzleAudioPlayback player = session.AudioPlayback;
                PuzzleFeedbackSchedule schedule = (PuzzleFeedbackSchedule)typeof(PuzzleGameSession).GetField("audioSchedule", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(session);
                AudioClip[] owned = (AudioClip[])typeof(PuzzleAudioPlayback).GetField("clips", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(player);
                PuzzleArtwork art = (PuzzleArtwork)typeof(PuzzleGameSession).GetField("artwork", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(session);
                // 앞선 사례에서 준비된 효과 캐시를 반환하고 실제 미로드 효과를 준비한다.
                session.enabled = false; art.Dispose(); art = new PuzzleArtwork();
                typeof(PuzzleGameSession).GetField("artwork", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(session, art);
                await art.PrepareAsync(session.State, CancellationToken.None);
                UnityEngine.Object.FindFirstObjectByType<PuzzleWorldBoard>().Draw(session.State, art); session.enabled = true;
                List<PuzzleFeedbackCueKind> heard = new List<PuzzleFeedbackCueKind>(); player.Played += heard.Add;
                Check(session.TryActivate(new BoardCoordinate(4, 4)), "효과 준비 중 파괴 실제 파워 실행"); session.enabled = false;
                Check((bool)typeof(PuzzleGameSession).GetField("preparingEffects", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(session), "파괴 직전 실제 효과 준비 대기");
                await EditorSceneManager.LoadSceneAsyncInPlayMode(PuzzleGameAssets.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
                float deadline = Time.realtimeSinceStartup + 20;
                while ((int)typeof(PuzzleArtwork).GetField("pending", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(art) > 0 && Time.realtimeSinceStartup < deadline) await UniTask.Yield();
                await UniTask.Yield();
                Check(session == null && player == null && owned.All(clip => clip == null) && schedule.PendingCount == 0 && heard.Count == 0 && art.AtlasCount == 0,
                    "효과 준비 중 파괴·늦은 완료 후 음성·예약·클립·요청·아틀라스 잔류 없음");
                session = UnityEngine.Object.FindFirstObjectByType<PuzzleGameSession>();
                deadline = Time.realtimeSinceStartup + 30;
                while (!session.IsReady && !session.HasFailed && Time.realtimeSinceStartup < deadline) await UniTask.Yield();
                Invoke(session, "TickProgress", .7f);
                Check(session.CanAcceptInput && session.AudioPlayback.SourceCount == 8 && session.AudioPlayback.ClipCount == 14, "준비 중 파괴 뒤 새 씬 소리·입력 정상");
            }
            finally { UnityEngine.Object.Destroy(level); }
        }

        private static async UniTask LargeObstacleAudioCheck()
        {
            PuzzleGameSession session = UnityEngine.Object.FindFirstObjectByType<PuzzleGameSession>();
            session.enabled = false; session.SoundEnabled = true;
            LevelDefinition level = (LevelDefinition)typeof(CombinationVerification).GetMethod("Make", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { 1, RocketDirection.Horizontal, null });
            List<PuzzleFeedbackCueKind> heard = new List<PuzzleFeedbackCueKind>();
            void Played(PuzzleFeedbackCueKind kind) { heard.Add(kind); }
            session.AudioPlayback.Played += Played;
            try
            {
                BoardCoordinate anchor = new BoardCoordinate(3, 6), first = new BoardCoordinate(4, 4), second = new BoardCoordinate(4, 5);
                LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Block, Erase = true },
                    LevelPlacementRules.Footprint(anchor, LevelPlacementRules.Size(ObstacleKind.Appliance)));
                Check(LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)ObstacleKind.Appliance,
                    Durability = 9, Color = RabbitColor.Type1 }, new[] { anchor }).Changed == 1, "소리 켠 실제 2x2 내구도9 배치");
                typeof(PowerEffectVerification).GetMethod("Place", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null,
                    new object[] { level, new BoardCoordinate(5, 6), InitialBlockKind.Rocket, RocketDirection.Vertical, RabbitColor.Type1 });
                LevelStateBuildResult built = LevelStateBuilder.Build(level, 12345); Check(built.IsBuilt, "2x2 조합·연쇄 실제 fixture 유효");
                BoardActionExecutor direct = new BoardActionExecutor(built.State), executor = new BoardActionExecutor(built.State);
                BoardActionResult action = direct.Swap(first, second);
                int body = built.State.CellAt(anchor).ObstacleIndex.Value;
                Check(action.IsApplied && action.Effects.Count(effect => effect.Response == DamageResponse.Damage && built.State.CellAt(effect.Target).ObstacleIndex == body) > 1,
                    "2x2 조합 중첩 범위의 반복 내구도 피해 fixture");
                Invoke(session, "ResetPresentation");
                typeof(PuzzleGameSession).GetField("executor", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(session, executor);
                Invoke(session, "InitializeProgress");
                PuzzleArtwork art = (PuzzleArtwork)typeof(PuzzleGameSession).GetField("artwork", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(session);
                await art.PrepareAsync(built.State, CancellationToken.None);
                UnityEngine.Object.FindFirstObjectByType<PuzzleWorldBoard>().Draw(session.State, art);
                session.enabled = true; Check(session.TrySwap(first, second), "소리 켠 실제 세션 2x2 조합 실행"); session.enabled = false;
                for (int frame = 0; frame < 20000 && (session.IsPresenting || executor.HasPendingCascade || session.HasProgressFeedback); frame++) await Frame(session, .02f);
                while (direct.HasPendingCascade) direct.AdvanceCascade();
                Check(heard.Contains(PuzzleFeedbackCueKind.Rocket) && heard.Count(kind => kind == PuzzleFeedbackCueKind.Swap) == 1, "2x2 조합·연쇄 실제 파워 소리 요청");
                Check(!session.HasFailed && !session.IsPresenting && !executor.HasPendingCascade && Snapshot(session.State) == Snapshot(direct.State) &&
                    session.Phase == direct.Phase && Snapshot(session.Outcome) == Snapshot(direct.Outcome), "2x2 소리 포함 내구도·보드·미션·이동·공급·난수·승패 동등");
            }
            finally
            {
                session.AudioPlayback.Played -= Played; Invoke(session, "ResetPresentation"); Invoke(session, "ClearProgress");
                UnityEngine.Object.Destroy(level); session.enabled = true;
            }
        }
    }
}



