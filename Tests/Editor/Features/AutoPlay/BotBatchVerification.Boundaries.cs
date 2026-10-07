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
    public static partial class BotBatchVerification
    {
        /// <summary>각 실제 처리 단계에서 멈추고 재개·중지한다. 최종 행동열은 무중단 실행과 비교한다.</summary>
        public static void Boundaries()
        {
            Results.Clear(); Directory.CreateDirectory(Evidence);
            LevelDefinition level = null; Exception failure = null;
            try
            {
                BoardCoordinate[] cells = Enumerable.Range(0, 16).Select(i => new BoardCoordinate(i / 4, i % 4)).ToArray();
                level = (LevelDefinition)typeof(SettlementVerification).GetMethod("Make", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { cells });
                JsonUtility.FromJsonOverwrite("{\"initialBlocks\":[],\"moveCount\":5,\"missions\":[{\"kind\":0,\"color\":0,\"count\":1}]}", level);
                List<string> baseline = new List<string>();
                using (BotBatchSession reference = new BotBatchSession(level, new[] { 771 }, (_, game) => {
                    if (game != null) { game.batchId = "comparison"; baseline.Add(JsonUtility.ToJson(game)); }
                }))
                {
                    for (int i = 0; reference.NeedsAdvance && i < 100000; i++) reference.Advance();
                    Check(reference.Record.status == BotBatchStatus.Completed && reference.Record.won == 2, "경계 기준 두 판은 라스트팡까지 실제 성공");
                }
                foreach (string boundary in new[] { "준비", "계획", "낙하", "자동매칭", "라스트팡", "판사이" })
                foreach (bool stop in new[] { false, true })
                {
                    List<string> records = new List<string>();
                    using BotBatchSession batch = new BotBatchSession(level, new[] { 771 }, (_, game) => {
                        if (game != null) { game.batchId = "comparison"; records.Add(JsonUtility.ToJson(game)); }
                    });
                    bool reached = false;
                    for (int i = 0; batch.NeedsAdvance && i < 100000; i++)
                    {
                        batch.Advance();
                        BotPlaySession game = batch.Current;
                        BoardActionExecutor executor = game == null ? null : (BoardActionExecutor)typeof(BotPlaySession)
                            .GetField("executor", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);
                        reached = boundary switch {
                            "준비" => game?.Status == BotSessionStatus.Preparing,
                            "계획" => game?.IsPlanning == true,
                            "낙하" => executor?.Phase == BoardActionPhase.WaitingForFall,
                            "자동매칭" => executor?.Phase == BoardActionPhase.WaitingForAutomaticMatch,
                            "라스트팡" => executor?.Phase == BoardActionPhase.WaitingForLastPang,
                            _ => batch.Record.finished == 1 && game == null };
                        if (reached) break;
                    }
                    Check(reached, boundary + (stop ? " 중지" : " 재개") + " 실제 경계 도달");
                    BotPlaySession current = batch.Current;
                    string before = StateSnapshot(current?.State);
                    int count = records.Count, completed = batch.Record.finished;
                    Check(batch.Pause(), boundary + " 일시정지 수락 " + stop);
                    for (int i = 0; i < 10; i++) batch.Advance();
                    Check(batch.Current == current && StateSnapshot(current?.State) == before && records.Count == count,
                        boundary + " 일시정지 중 상태·난수·기록 불변 " + stop);
                    if (boundary == "라스트팡") Check(batch.Record.finished == 0, "성공 확정이어도 라스트팡 도중 완료 기록 조기 확정 금지 " + stop);
                    if (stop)
                    {
                        batch.Stop(); batch.Stop();
                        for (int i = 0; i < 10; i++) batch.Advance();
                        Check(batch.Record.finished == completed && batch.Record.stopped == (current == null ? 0 : 1) &&
                            records.Count == count + (current == null ? 0 : 1) && batch.Current == null && !batch.Resume(),
                            boundary + " 중지 후 완료 보존·미완료 1회 기록·다음 판 차단");
                    }
                    else
                    {
                        Check(batch.Resume(), boundary + " 같은 처리 객체 재개");
                        for (int i = 0; batch.NeedsAdvance && i < 100000; i++) batch.Advance();
                        Check(batch.Record.status == BotBatchStatus.Completed && records.SequenceEqual(baseline),
                            boundary + " 재개 후 무중단 실행과 모든 행동·난수·결과 일치");
                    }
                }
                // 한 판 세션의 오류 경계를 주입한다. 게임 규칙 자체의 오류 원인은 기존 엔진 검사 범위이며,
                // 여기서는 반복 관리자가 오류 판을 패배로 바꾸거나 다음 판으로 넘기지 않는지 확인한다.
                int errorRecords = 0;
                using (BotBatchSession failed = new BotBatchSession(level, new[] { 771 }, (_, game) => {
                    if (game != null && game.outcome == BotSessionStatus.Error) errorRecords++;
                }))
                {
                    failed.Advance();
                    typeof(BotPlaySession).GetMethod("Fail", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(failed.Current, new object[] { "검증용 한 판 실행 오류" });
                    failed.Advance();
                    for (int i = 0; i < 10; i++) failed.Advance();
                    Check(failed.Record.status == BotBatchStatus.Error && failed.Record.errors == 1 && failed.Record.finished == 0 &&
                        failed.Record.Unrun == 1 && errorRecords == 1 && failed.Current == null,
                        "한 판 실행 오류는 1회 오류 기록·묶음 중단·다음 판 금지");
                }
                // 데이터 오류도 일반 패배로 집계하지 않는다.
                JsonUtility.FromJsonOverwrite("{\"moveCount\":0}", level);
                bool rejected = false;
                try { using BotBatchSession invalid = new BotBatchSession(level, new[] { 771 }, (_, _) => { }); }
                catch (ArgumentException) { rejected = true; }
                Check(rejected, "유효하지 않은 레벨은 묶음 시작 전에 거절");
            }
            catch (Exception error) { failure = error; Results.Add("FAIL " + error); }
            finally { if (level != null) UnityEngine.Object.DestroyImmediate(level); }
            File.WriteAllLines(Evidence + "/boundary-results.txt", Results);
            if (failure != null) Debug.LogException(failure);
            EditorApplication.Exit(failure == null ? 0 : 1);
        }

        /// <param name="state">공통 실행기의 실제 값.</param><returns>기존 검증기가 만드는 결정적 상태 표현.</returns>
        private static string StateSnapshot(object state) => state == null ? "null" : (string)typeof(LevelInitialStateVerification)
            .GetMethod("Snapshot", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new[] { state });
    }
}
