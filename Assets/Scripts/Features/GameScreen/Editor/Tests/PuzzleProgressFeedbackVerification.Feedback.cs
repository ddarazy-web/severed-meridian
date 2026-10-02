using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Board;
using Levels;
using Levels.Editor;
using Simulation;
using UnityEngine;

namespace GameScreen.Editor
{
    public static partial class PuzzleProgressFeedbackVerification
    {
        private static void FeedbackChecks(LevelDefinition level)
        {
            Type type = typeof(PuzzleGameSession).Assembly.GetType("GameScreen.PuzzleProgressFeedback");
            Check(type != null, "수집 비행과 HUD 도착을 분리한 표시기 제공");
            JsonUtility.FromJsonOverwrite("{\"missions\":[" + string.Join(",", Enumerable.Range(0, 10).Select(i =>
                "{\"kind\":" + i + ",\"color\":0,\"count\":2}")) + "]}", level);
            LevelRuntimeState state = (LevelRuntimeState)Activator.CreateInstance(typeof(LevelRuntimeState), BindingFlags.NonPublic | BindingFlags.Instance,
                null, new object[] { level, 12345, "feedback-fixture" }, null);
            foreach (RuntimeMission mission in state.Missions) typeof(RuntimeMission).GetProperty("Target").SetValue(mission, 2);
            object feedback = Activator.CreateInstance(type);
            type.GetMethod("Initialize").Invoke(feedback, new object[] { state });
            MethodInfo complete = typeof(MissionProgressRules).GetMethod("Complete", BindingFlags.NonPublic | BindingFlags.Static);
            foreach (MissionKind kind in Enum.GetValues(typeof(MissionKind)))
                complete.Invoke(null, new object[] { state, kind, (BoardCoordinate?)new BoardCoordinate(2, 3), (int?)null });
            Func<MissionProgressRecord, float?> delay = record => .2f;
            type.GetMethod("Schedule").Invoke(feedback, new object[] { state, delay });
            PuzzleMissionDisplay display = (PuzzleMissionDisplay)type.GetProperty("Display").GetValue(feedback);
            MethodInfo tick = type.GetMethod("Tick");
            tick.Invoke(feedback, new object[] { .1f });
            Check(display.Progress(0) == 0 && ((IEnumerable)type.GetProperty("Flights").GetValue(feedback)).Cast<object>().Count() == 0,
                "실제 타격 시각 전에 수집 출발·진행 반영 없음");
            tick.Invoke(feedback, new object[] { .1f });
            Check(((IEnumerable)type.GetProperty("Flights").GetValue(feedback)).Cast<object>().Count() == 8 && display.Progress(0) == 0,
                "동시 수집 표시 최대 8개와 도착 전 수치 보존");
            tick.Invoke(feedback, new object[] { .16f });
            Check(display.Progress(0) == 0, "비행 중간에도 표시 수치 유지");
            tick.Invoke(feedback, new object[] { .16f });
            Check(display.Progress(0) == 1, "비행 도착 시 실제 증가량 반영");
            for (int i = 0; i < 20; i++) tick.Invoke(feedback, new object[] { .1f });
            Check(Enumerable.Range(0, 10).All(i => display.Progress(i) == 1) && !(bool)type.GetProperty("IsBusy").GetValue(feedback),
                "8개 초과 미션도 큐에서 누락 없이 도착하고 종료");
            foreach (MissionKind kind in Enum.GetValues(typeof(MissionKind)))
                complete.Invoke(null, new object[] { state, kind, (BoardCoordinate?)null, (int?)null });
            type.GetMethod("Schedule").Invoke(feedback, new object[] { state, new Func<MissionProgressRecord, float?>(record => null) });
            tick.Invoke(feedback, new object[] { 0f });
            Check(Enumerable.Range(0, 10).All(i => display.Progress(i) == 2) && ((IEnumerable)type.GetProperty("Flights").GetValue(feedback)).Cast<object>().Count() == 0,
                "원점 불확실 수집은 가짜 비행 없이 HUD만 반영");
            Check(Enumerable.Range(0, 10).All(i => (int)type.GetMethod("CompletionCount").Invoke(feedback, new object[] { i }) == 1),
                "미션 완료 강조는 실제 달성 순간 한 번");
            type.GetMethod("Schedule").Invoke(feedback, new object[] { state, delay });
            tick.Invoke(feedback, new object[] { 1f });
            Check(!(bool)type.GetProperty("IsBusy").GetValue(feedback) && Enumerable.Range(0, 10).All(i =>
                (int)type.GetMethod("CompletionCount").Invoke(feedback, new object[] { i }) == 1), "재수집·중복 완료 강조 없음");
            type.GetMethod("Clear").Invoke(feedback, null);
            Check(!(bool)type.GetProperty("IsBusy").GetValue(feedback) && ((IEnumerable)type.GetProperty("Flights").GetValue(feedback)).Cast<object>().Count() == 0,
                "수집 표시 취소 후 잔류 없음");
            JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":8,\"count\":0}]}", level);
            LevelRuntimeState zero = (LevelRuntimeState)Activator.CreateInstance(typeof(LevelRuntimeState), BindingFlags.NonPublic | BindingFlags.Instance,
                null, new object[] { level, 12345, "initial-complete" }, null);
            type.GetMethod("Initialize").Invoke(feedback, new object[] { zero });
            complete.Invoke(null, new object[] { zero, MissionKind.Mold, (BoardCoordinate?)new BoardCoordinate(2, 3), (int?)null });
            type.GetMethod("Schedule").Invoke(feedback, new object[] { zero, delay }); tick.Invoke(feedback, new object[] { 1f });
            Check(!(bool)type.GetProperty("IsBusy").GetValue(feedback) && (int)type.GetMethod("CompletionCount").Invoke(feedback, new object[] { 0 }) == 0,
                "목표 0·초기 완료는 가짜 수집·완료 강조 없음");
            SessionFeedbackChecks(level);
        }

        private static void SessionFeedbackChecks(LevelDefinition level)
        {
            PropertyInfo moves = typeof(PuzzleGameSession).GetProperty("MovesPulse");
            Check(moves != null, "이동 소비에 대응하는 HUD 강조 시간 제공");
            JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":0,\"color\":0,\"count\":2}]}", level);
            BoardActionExecutor executor = new BoardActionExecutor(LevelStateBuilder.Build(level, 12345).State);
            GameObject root = new GameObject("Session feedback data");
            LevelDefinition cascadeLevel = null;
            try
            {
                PuzzleGameSession session = root.AddComponent<PuzzleGameSession>();
                typeof(PuzzleGameSession).GetField("executor", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, executor);
                typeof(PuzzleGameSession).GetField("ready", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, true);
                Invoke(session, "InitializeProgress");
                Check((float)moves.GetValue(session) == 0, "초기 이동 강조 없음");
                executor.Activate(new BoardCoordinate(4, 4)); Invoke(session, "ObserveMoves");
                Check((float)moves.GetValue(session) > 0, "실제 이동 소비 시 강조");
                float pulse = (float)moves.GetValue(session); session.SetPaused(true); Invoke(session, "TickProgress", .5f);
                Check((float)moves.GetValue(session) == pulse, "pause 중 이동 강조 정지");
                session.SetPaused(false); Invoke(session, "TickProgress", .5f);
                executor.Activate(new BoardCoordinate(4, 4)); Invoke(session, "ObserveMoves");
                Check((float)moves.GetValue(session) == 0, "거절 행동과 이동 변화 없는 상태는 강조 없음");
                cascadeLevel = (LevelDefinition)typeof(CascadeVerification).GetMethod("Make", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null,
                    new object[] { new[] { new BoardCoordinate(0, 0), new BoardCoordinate(0, 1), new BoardCoordinate(0, 2) }, 20 });
                BoardActionExecutor automatic = (BoardActionExecutor)typeof(CascadeVerification).GetMethod("Automatic", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null,
                    new object[] { cascadeLevel, 12345 });
                CascadeStepResult match = automatic.AdvanceCascade();
                Check(match.Reason == CascadeStepReason.Matched, "실제 새 매칭 라운드 fixture");
                PropertyInfo cascade = typeof(PuzzleGameSession).GetProperty("CascadePulse");
                Check(cascade != null, "실제 매칭 라운드 강조 제공");
                Invoke(session, "ObserveCascadeStep", match);
                Check((float)cascade.GetValue(session) > 0, "새 매칭 라운드에서만 연쇄 강조");
                Check(session.FeedbackStatus == "2 연쇄!", "새 매칭 라운드의 실제 연쇄 수 표시");
                Invoke(session, "TickProgress", .5f); Invoke(session, "ObserveCascadeStep", match);
                Check((float)cascade.GetValue(session) == 0, "같은 매칭 라운드의 중복 강조 없음");
                CascadeStepResult settled = automatic.AdvanceCascade();
                Check(settled.Reason == CascadeStepReason.Settled, "실제 단순 정착 fixture");
                Invoke(session, "ObserveCascadeStep", settled);
                Check((float)cascade.GetValue(session) == 0, "정착 Batch는 새 연쇄로 세지 않음");
                PropertyInfo resultReady = typeof(PuzzleGameSession).GetProperty("ResultReady");
                Check(resultReady != null, "최종 결과와 수집 표시 완료 경계 제공");
                int steps = 0; bool sawLastPang = false;
                while (executor.HasPendingCascade && steps++ < 200)
                {
                    CascadeStepResult step = executor.AdvanceCascade();
                    if (step.Reason == CascadeStepReason.Matched) Invoke(session, "TickProgress", .5f);
                    Invoke(session, "ObserveCascadeStep", step);
                    if (step.Reason == CascadeStepReason.Matched) Check(session.CascadePulse > 0, "라스트팡으로 라운드가 초기화돼도 실제 새 매칭 강조");
                    if (step.Reason == CascadeStepReason.LastPang)
                    { sawLastPang = true; Check(session.FeedbackStatus.Contains("남은 파워"), "실제 라스트팡 동안 시작 표시 유지"); }
                }
                Check(steps < 200 && executor.Outcome != null && executor.Phase == BoardActionPhase.Stopped, "실제 라스트팡 이후 최종 승패 확정");
                Check(sawLastPang, "실제 라스트팡 발동 경계 관찰");
                session.ProgressFeedback.Schedule(executor.State, record => null); Invoke(session, "TickProgress", 0f);
                Check(!(bool)resultReady.GetValue(session), "보드가 끝나도 수집 HUD 강조 중에는 결과 대기");
                Check(session.SetPaused(true), "보드 종료 후 남은 수집 반응도 pause 가능");
                Invoke(session, "TickProgress", 1f);
                Check(!(bool)resultReady.GetValue(session), "pause 중 종료 표시 시간 정지");
                session.SetPaused(false); Invoke(session, "TickProgress", 1f);
                Check((bool)resultReady.GetValue(session), "수집·라스트팡 종료 반응 정리 후 결과 허용");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); if (cascadeLevel != null) UnityEngine.Object.DestroyImmediate(cascadeLevel); }
        }
    }
}
