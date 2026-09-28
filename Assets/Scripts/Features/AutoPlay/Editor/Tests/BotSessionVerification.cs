using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AutoPlay;
using Board;
using Simulation;
using UnityEditor;
using UnityEngine;

namespace Levels.Editor
{
    /// <summary>공통 실행기 동등성·관찰 수명·중지 경계를 전용 Unity에서 검사한다.</summary>
    public static class BotSessionVerification
    {
        private static readonly List<string> Results = new List<string>();

        /// <summary>별도 Unity 프로세스에서 같은 정의·시드를 구성하고 저장된 명령열·상태와 비교한다.</summary>
        public static void Replay()
        {
            LevelDefinition level = null;
            Exception failure = null;
            try
            {
                level = (LevelDefinition)typeof(ItemBoosterVerification).GetMethod("PlayFixture", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                JsonUtility.FromJsonOverwrite("{\"moveCount\":2}", level);
                using (BotPlaySession game = new BotPlaySession(level, 771))
                {
                    game.Begin(true); Drain(game);
                    string actual = BotPlaySession.Version + "\n" + BasicBotStrategy.Version + "\n" + BoardActionExecutor.Version + "\n" +
                        game.DefinitionFingerprint + "\n" + game.Seed + "\n" + Snapshot(game.Records) + "\n" + Snapshot(game.State) + "\n" + Snapshot(game.Outcome);
                    File.WriteAllText("Logs/BotBasicVerification/session-replay-second.txt", actual);
                    Check(actual == File.ReadAllText("Logs/BotBasicVerification/session-replay-first.txt"),
                        "독립 Unity 프로세스의 정의 지문·시드·버전·전 행동·점수·상태·난수·결과 일치");
                }
            }
            catch (Exception error) { failure = error; Results.Add("FAIL " + error); }
            finally { if (level != null) UnityEngine.Object.DestroyImmediate(level); }
            File.WriteAllLines("Logs/BotBasicVerification/session-replay-results.txt", Results);
            EditorApplication.Exit(failure == null ? 0 : 1);
        }

        /// <summary>메모리 레벨을 검사하고 원본 에셋 저장 없이 프로세스를 종료한다.</summary>
        public static void Run()
        {
            LevelDefinition level = null;
            Exception failure = null;
            try
            {
                level = (LevelDefinition)typeof(ItemBoosterVerification).GetMethod("PlayFixture", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                string json = JsonUtility.ToJson(level);
                bool dirty = EditorUtility.IsDirty(level);
                using (BotPlaySession session = new BotPlaySession(level, 771))
                {
                    Drain(session);
                    Check(session.Status == BotSessionStatus.Ready && session.State != null, "공통 시작 조건 통과 후 입력 대기");
                    BotObservation observation = session.Observe();
                    Check(ReferenceEquals(observation, session.Observe()), "같은 입력 시점의 관찰 토큰 유지");
                    string before = Snapshot(session.State);
                    BotAction forged = (BotAction)Activator.CreateInstance(typeof(BotAction), BindingFlags.Instance | BindingFlags.NonPublic,
                        null, new object[] { BotActionKind.Activate, new BoardCoordinate(-1, -1), null, Array.Empty<BotMatch>() }, null);
                    Check(!session.TrySubmit(observation, forged) && Snapshot(session.State) == before, "위조·범위 밖 후보는 무변경 거절");
                    Check(!session.TrySubmit(null, observation.Actions[0]) && Snapshot(session.State) == before, "관찰 없는 요청은 무변경 거절");

                    BotChoice choice = BasicBotStrategy.Choose(observation);
                    BoardActionExecutor manual = new BoardActionExecutor(StartingBoardBuilder.Build(level, 771).State);
                    Check(session.TrySubmit(observation, choice.Action), "현재 관찰 후보 실행 수락");
                    string pending = Snapshot(session.State);
                    Check(!session.TrySubmit(observation, choice.Action) && Snapshot(session.State) == pending, "같은 행동 중복·연쇄 중 입력 거절");
                    BoardActionResult applied = choice.Action.Kind == BotActionKind.Activate ? manual.Activate(choice.Action.First) :
                        manual.Swap(choice.Action.First, choice.Action.Second.Value);
                    Check(applied.IsApplied && Snapshot(manual.State) == pending, "첫 행동 직후 수동·봇 상태와 난수 동등");
                    while (manual.HasPendingCascade) manual.AdvanceCascade();
                    session.RequestStop();
                    Check(session.Status == BotSessionStatus.Stopping, "후속 처리 중 중지는 안전 경계까지 대기");
                    Drain(session);
                    Check(session.Status == BotSessionStatus.Stopped || session.Status == BotSessionStatus.Won, "중지와 정상 종료 구분");
                    Check(session.Records.Count == 1 && Snapshot(manual.State) == Snapshot(session.State), "한 수 전체 후속 처리 수동·봇 동등");
                    BoardActionExecutor owned = (BoardActionExecutor)typeof(BotPlaySession).GetField("executor", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session);
                    Check(Snapshot(manual.TurnEffects) == Snapshot(owned.TurnEffects), "드론 예약·피해·보호 기록까지 수동·봇 동등");
                    Check(owned.ItemUses.Count == 0 && owned.BoosterPlacements.Count == 0 && owned.PendingBoosters.Count == 0, "봇 아이템·부스터 미사용");
                    string stopped = Snapshot(session.State);
                    session.Advance(); session.Advance();
                    Check(Snapshot(session.State) == stopped && session.Records.Count == 1, "중지 후 갱신만으로 자동 재개 안 함");
                    if (session.Status == BotSessionStatus.Stopped)
                    {
                        Check(session.Begin(false), "명시적 한 수 요청으로 다시 진행");
                        Check(!session.TrySubmit(observation, choice.Action) && Snapshot(session.State) == stopped, "재개해도 과거 관찰은 무변경 거절");
                        Drain(session);
                        Check(session.Records.Count == 2, "한 수 요청은 정확히 한 행동 후 정착");
                    }
                    session.Dispose();
                    Check(session.State == null && session.Observe() == null && !session.Begin(true), "폐기된 세션의 상태·재진입 차단");
                    Check(!session.TrySubmit(observation, choice.Action), "폐기 후 오래된 요청 차단");
                }
                using (BotPlaySession limited = new BotPlaySession(level, 771))
                {
                    limited.Begin(true); Drain(limited);
                    Check(limited.Status == BotSessionStatus.Error && limited.Outcome?.Kind == BoardOutcomeKind.Aborted &&
                        limited.Message.Contains("재배치 탐색 한도"), "공통 재배치 탐색 한도를 패배와 구분해 오류로 기록");
                    BoardActionExecutor manual = new BoardActionExecutor(StartingBoardBuilder.Build(level, 771).State);
                    foreach (BotTurnRecord record in limited.Records)
                    {
                        BotAction action = record.Choice.Action;
                        if (action.Kind == BotActionKind.Activate) manual.Activate(action.First);
                        else manual.Swap(action.First, action.Second.Value);
                        while (manual.HasPendingCascade) manual.AdvanceCascade();
                    }
                    Check(Snapshot(manual.State) == Snapshot(limited.State) && Snapshot(manual.Outcome) == Snapshot(limited.Outcome),
                        "재배치 오류도 동일 명령열 수동 실행과 일치");
                }
                Check(JsonUtility.ToJson(level) == json && EditorUtility.IsDirty(level) == dirty, "시작·중지·한 판 실행 후 원본 JSON·dirty 보존");
                // 이후 승리/이동 소진 사례는 이 메모리 정의의 명시적인 별도 조건이다.
                // 앞의 재배치 오류를 허용 목록에 합쳐 정상 종료 검사를 약화하지 않는다.
                JsonUtility.FromJsonOverwrite("{\"moveCount\":2}", level);
                using (BotPlaySession game = new BotPlaySession(level, 771))
                {
                    Check(game.Begin(true), "준비 중 한 판 실행 예약");
                    Drain(game);
                    Check(game.Status == BotSessionStatus.MovesExhausted,
                        "한 판이 실제 공통 규칙 결과로 종료: " + game.Status + " / " + game.Message);
                    Check(game.Records.Count > 0 && game.Records.All(record => record.MovesBefore - record.MovesAfter == 1), "모든 행동의 이동 수 비용 1");
                    Check(!game.Begin(true) && game.Observe() == null, "종료 판 재조작 차단");
                    Directory.CreateDirectory("Logs/BotBasicVerification");
                    File.WriteAllText("Logs/BotBasicVerification/session-replay-first.txt",
                        BotPlaySession.Version + "\n" + BasicBotStrategy.Version + "\n" + BoardActionExecutor.Version + "\n" +
                        game.DefinitionFingerprint + "\n" + game.Seed + "\n" + Snapshot(game.Records) + "\n" + Snapshot(game.State) + "\n" + Snapshot(game.Outcome));
                }
                JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":0,\"color\":0,\"count\":1}]}", level);
                using (BotPlaySession won = new BotPlaySession(level, 771))
                {
                    won.Begin(true); Drain(won);
                    Check(won.Status == BotSessionStatus.Won && won.Outcome.Kind == BoardOutcomeKind.Won && !won.NeedsAdvance,
                        "목표 달성 후 라스트팡까지 공통 실행기로 완료");
                }
                using (BotPlaySession cancelled = new BotPlaySession(level, 771))
                {
                    cancelled.RequestStop(); cancelled.Advance();
                    Check(cancelled.Status == BotSessionStatus.Stopped && cancelled.State == null, "시작 전 중지는 검색과 실행 예약 정리");
                }
                JsonUtility.FromJsonOverwrite("{\"schemaVersion\":-1}", level);
                using (BotPlaySession invalid = new BotPlaySession(level, 771))
                {
                    Drain(invalid);
                    Check(invalid.Status == BotSessionStatus.Error && invalid.State == null, "잘못된 정의는 시작 오류로 분리");
                }
            }
            catch (Exception error) { failure = error; Results.Add("FAIL " + error); }
            finally { if (level != null) UnityEngine.Object.DestroyImmediate(level); }
            Directory.CreateDirectory("Logs/BotBasicVerification");
            File.WriteAllLines("Logs/BotBasicVerification/session-results.txt", Results);
            EditorApplication.Exit(failure == null ? 0 : 1);
        }

        /// <summary>검사만 동기 반복한다. 제품 세션/UI에는 이 루프를 사용하지 않는다.</summary>
        /// <param name="session">검사 세션.</param>
        private static void Drain(BotPlaySession session)
        {
            int ticks = 0;
            while (session.NeedsAdvance && ticks++ < 20000) session.Advance();
            if (session.NeedsAdvance) throw new InvalidOperationException("검사 갱신 한도 초과");
        }

        /// <param name="pass">검사 결과.</param><param name="name">검사명.</param>
        private static void Check(bool pass, string name)
        { if (!pass) throw new InvalidOperationException(name); Results.Add("PASS " + name); }

        /// <param name="value">비교할 값.</param><returns>기존 검증기의 결정적 공개 속성 직렬화.</returns>
        private static string Snapshot(object value) => (string)typeof(LevelInitialStateVerification)
            .GetMethod("Snapshot", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new[] { value });
    }
}
