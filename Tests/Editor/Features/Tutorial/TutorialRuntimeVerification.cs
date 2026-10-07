using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Board;
using Levels;
using Simulation;
using Tutorial;
using Tutorial.Editor;
using UnityEditor;
using UnityEngine;

public static class TutorialRuntimeVerification
{
    private static readonly List<string> Results = new List<string>();
    private static BoardCoordinate A => new BoardCoordinate(2, 2);
    private static BoardCoordinate B => new BoardCoordinate(2, 3);
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); Results.Add("PASS " + message); }
    private static void Reject(Action action, string message)
    { bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } catch (InvalidOperationException) { rejected = true; } Check(rejected, message); }
    private static TutorialStepDefinition Step(TutorialStepKind kind = TutorialStepKind.Swap)
        => new TutorialStepDefinition { kind = kind, instructions = "안내", hasFirst = kind != TutorialStepKind.Description,
            first = A, hasSecond = kind == TutorialStepKind.Swap || kind == TutorialStepKind.PowerSwap, second = B,
            actionDefinitionId = kind == TutorialStepKind.PowerSwap ? "power.rocket" : "" };
    private static TutorialProgress Progress(params TutorialStepDefinition[] steps)
        => new TutorialProgress(new LevelTutorialDefinition { steps = steps.ToList() });
    private static TutorialActionTicket Approve(TutorialProgress progress, TutorialInput input)
    { Check(progress.TryApprove(input, out TutorialActionTicket ticket), "지정 행동 승인"); return ticket; }
    private static void Finish(TutorialProgress progress, TutorialActionTicket ticket, string power = null)
    { progress.ReportCompletion(ticket, true, power); progress.ReportPresentationComplete(ticket); }
    private static TutorialResultRecord Record(TutorialResultKind kind, string id, string occurrence, BoardCoordinate? at = null)
        => new TutorialResultRecord(kind, id, occurrence, at);

    public static void Run()
    {
        Results.Clear();
        foreach (Action test in new Action[] { VerifyRegistry, VerifySteps, VerifyResults, VerifyLifetime, VerifyExtensions, VerifyValidation, VerifyBoundaryCases })
            try { test(); } catch (Exception error) { Results.Add("FAIL " + test.Method.Name + " " + error); Debug.LogException(error); }
        Directory.CreateDirectory("Logs/Tutorial/Stage02");
        File.WriteAllLines("Logs/Tutorial/Stage02/runtime-results.txt", Results);
        EditorApplication.Exit(Results.Any(line => line.StartsWith("FAIL")) ? 1 : 0);
    }

    private static void VerifyRegistry()
    {
        TutorialHandlerRegistry registry = TutorialHandlerRegistry.CreateDefault();
        foreach (TutorialStepKind kind in Enum.GetValues(typeof(TutorialStepKind)))
        {
            TutorialStepDefinition step = Step(kind);
            Check(registry.TryGetStep(step, out _), "초기 단계 지원 " + kind);
        }
        foreach (BoardItem item in Enum.GetValues(typeof(BoardItem)))
            Check(registry.TryGetStep(new TutorialStepDefinition { kind = TutorialStepKind.Item, item = item }, out _), "아이템 등록 " + item);
        foreach (TutorialResultKind kind in Enum.GetValues(typeof(TutorialResultKind)))
            Check(registry.TryGetResult(kind, out _), "결과 등록 " + kind);
        TutorialHandlerRegistry mutable = new TutorialHandlerRegistry();
        mutable.RegisterStep(TutorialStepKind.Swap, new TutorialSwapHandler());
        Reject(() => mutable.RegisterStep(TutorialStepKind.Swap, new TutorialSwapHandler()), "중복 단계 거절");
        mutable.RegisterItem(BoardItem.Hammer, new TutorialItemHandler(BoardItem.Hammer, 1));
        Reject(() => mutable.RegisterItem(BoardItem.Hammer, new TutorialItemHandler(BoardItem.Hammer, 1)), "중복 아이템 거절");
        mutable.RegisterResult(TutorialResultKind.Removed, new TutorialResultEvaluator(TutorialResultKind.Removed));
        Reject(() => mutable.RegisterResult(TutorialResultKind.Removed, new TutorialResultEvaluator(TutorialResultKind.Removed)), "중복 결과 거절");
        Reject(() => registry.RegisterStep((TutorialStepKind)99, new TutorialSwapHandler()), "실행 등록 목록 변경 거절");
        Check(!registry.TryGetStep(Step((TutorialStepKind)99), out _), "미등록 행동은 fallback 없음");
        Check(Progress(Step((TutorialStepKind)99)).State == TutorialProgressState.Error, "미등록 단계 진단");
        TutorialStepDefinition unknown = Step(); unknown.results.Add(new TutorialResultDefinition { kind = (TutorialResultKind)99, definitionId = "x" });
        Check(Progress(unknown).State == TutorialProgressState.Error, "미등록 조건 진단");
        Check(Progress().State == TutorialProgressState.Completed && new TutorialProgress(null).State == TutorialProgressState.Completed, "튜토리얼 없음");
        Reject(() => new TutorialHandlerRegistry().RegisterStep(TutorialStepKind.Swap, null), "null 처리기 거절");
    }

    private static void VerifySteps()
    {
        TutorialProgress description = Progress(Step(TutorialStepKind.Description), Step());
        Check(description.State == TutorialProgressState.AwaitDescription, "설명 대기");
        Check(!description.TryApprove(TutorialInput.Swap(A, B), out _), "설명에서 보드 차단");
        Check(description.TryApprove(TutorialInput.Next(), out _) && description.StepIndex == 1, "다음으로 한 단계 진행");
        Check(!description.TryApprove(TutorialInput.Next(), out _), "조작에서 다음 차단");
        Check(!description.TryApprove(TutorialInput.Swap(A, new BoardCoordinate(3, 2)), out _), "다른 좌표 거절");
        Check(!description.TryApprove(TutorialInput.Activate(A), out _), "제자리 발동 거절");
        Check(!description.TryApprove(TutorialInput.UseItem(BoardItem.Swap, A, B), out _), "일반 교환에서 아이템 우회 차단");
        TutorialActionTicket swap = Approve(description, TutorialInput.Swap(B, A));
        Check(description.StepIndex == 1 && description.State == TutorialProgressState.AwaitPresentation, "승인은 성공 아님, 반대 방향 허용");
        Check(!description.TryApprove(TutorialInput.Swap(A, B), out _), "실행 중 추가 승인 차단");
        description.ReportPresentationComplete(swap);
        Check(description.StepIndex == 1, "연출만으로 진행 불가");
        description.ReportCompletion(swap, false);
        Check(description.State == TutorialProgressState.AwaitAction && description.StepIndex == 1, "실패는 동일 단계 재시도");
        TutorialActionTicket retry = Approve(description, TutorialInput.Swap(A, B));
        description.ReportCompletion(swap, true);
        Check(description.StepIndex == 1, "실패한 이전 시도 성공 신호 무시");
        description.ReportCompletion(retry, true);
        Check(description.State != TutorialProgressState.Completed, "이전 시도의 연출 완료 재사용 불가");
        description.ReportPresentationComplete(retry);
        Check(description.State == TutorialProgressState.Completed, "새 시도 성공과 연출 완료");

        TutorialProgress power = Progress(Step(TutorialStepKind.PowerSwap));
        TutorialActionTicket pt = Approve(power, TutorialInput.Swap(A, B));
        power.ReportCompletion(pt, true, "power.bomb");
        Check(power.State == TutorialProgressState.Error, "다른 파워 실행 성공은 설정 불일치 오류");

        power = Progress(Step(TutorialStepKind.PowerSwap)); pt = Approve(power, TutorialInput.Swap(A, B));
        Finish(power, pt, "power.rocket");
        Check(power.State == TutorialProgressState.Completed, "지정 파워 실행");
        foreach (BoardItem item in Enum.GetValues(typeof(BoardItem)))
        {
            TutorialStepDefinition step = Step(TutorialStepKind.Item); step.item = item;
            step.hasFirst = item != BoardItem.Shuffle; step.hasSecond = item == BoardItem.Swap;
            TutorialProgress p = Progress(step);
            Check(p.Snapshot.FreeItemAvailable && !p.TryApprove(TutorialInput.UseItem((BoardItem)99), out _), "지정 아이템만 무료 " + item);
            TutorialInput input = TutorialInput.UseItem(item, step.hasFirst ? A : (BoardCoordinate?)null, step.hasSecond ? B : (BoardCoordinate?)null);
            if (item == BoardItem.Hammer) Check(!p.TryApprove(TutorialInput.UseItem(item, A, B), out _), "망치 두 칸 거절");
            if (item == BoardItem.Shuffle) Check(!p.TryApprove(TutorialInput.UseItem(item, A), out _), "섞기 불필요 대상 거절");
            TutorialActionTicket ticket = Approve(p, input); p.ReportCompletion(ticket, false);
            Check(p.Snapshot.FreeItemAvailable, "아이템 실패 권한 보존 " + item);
            ticket = Approve(p, input); p.ReportCompletion(ticket, true);
            Check(!p.Snapshot.FreeItemAvailable && !p.TryApprove(input, out _), "성공 시 무료 1회 소진 " + item);
            p.ReportPresentationComplete(ticket);
            Check(p.State == TutorialProgressState.Completed && !p.Snapshot.FreeItemAvailable, "권한 종료 " + item);
        }
    }

    private static void VerifyResults()
    {
        TutorialStepDefinition swap = Step();
        swap.results.Add(new TutorialResultDefinition { kind = TutorialResultKind.Generated, definitionId = "power.rocket", hasCoordinate = true, coordinate = B });
        TutorialStepDefinition power = Step(TutorialStepKind.PowerSwap);
        power.results.Add(new TutorialResultDefinition { kind = TutorialResultKind.Activated, definitionId = "power.rocket" });
        TutorialProgress p = Progress(swap, power, Step(TutorialStepKind.Description));
        TutorialActionTicket ticket = Approve(p, TutorialInput.Swap(A, B));
        p.ReportCompletion(ticket, true); p.ReportPresentationComplete(ticket);
        Check(p.StepIndex == 0 && p.State == TutorialProgressState.AwaitPresentation, "조건 부족은 성공으로 대체 안 함");
        p.ReportResults(ticket, new[] { Record(TutorialResultKind.Removed, "power.rocket", "wrong-kind", B),
            Record(TutorialResultKind.Generated, "power.bomb", "wrong-id", B), Record(TutorialResultKind.Generated, "power.rocket", "wrong-cell", A) });
        Check(p.StepIndex == 0, "결과 종류·ID·좌표 구별");
        p.ReportResults(ticket, new[] { Record(TutorialResultKind.Generated, "power.rocket", "rocket-1", B) });
        Check(p.StepIndex == 1, "나중에 도착한 관련 결과로 진행");
        TutorialActionTicket next = Approve(p, TutorialInput.Swap(A, B));
        p.ReportResults(ticket, new[] { Record(TutorialResultKind.Activated, "power.rocket", "stale") });
        Finish(p, next, "power.rocket");
        Check(p.StepIndex == 1, "지난 행동의 조건은 다음 단계에 안 섞임");
        p.ReportResults(next, new[] { Record(TutorialResultKind.Activated, "power.rocket", "activated") });
        Check(p.StepIndex == 2 && p.State == TutorialProgressState.AwaitDescription, "발동 조건 후 설명");
        p.ReportPresentationComplete(next); p.ReportCompletion(next, true);
        Check(p.StepIndex == 2, "중복 완료가 설명을 넘기지 않음");

        TutorialStepDefinition remove = Step();
        remove.results.Add(new TutorialResultDefinition { kind = TutorialResultKind.Removed, definitionId = "obstacle.a", count = 2 });
        remove.results.Add(new TutorialResultDefinition { kind = TutorialResultKind.Removed, definitionId = "obstacle.b", count = 1, hasCoordinate = true, coordinate = B });
        p = Progress(remove); ticket = Approve(p, TutorialInput.Swap(A, B)); Finish(p, ticket);
        p.ReportResults(ticket, new[] { Record(TutorialResultKind.Removed, "obstacle.a", "body1", A), Record(TutorialResultKind.Removed, "obstacle.a", "body1", B),
            Record(TutorialResultKind.Removed, "obstacle.a", "body1", new BoardCoordinate(3, 2)), Record(TutorialResultKind.Removed, "obstacle.a", "body1", new BoardCoordinate(3, 3)) });
        Check(p.Snapshot.ConditionCounts[0] == 1 && p.State != TutorialProgressState.Completed, "2x2 피해 4칸은 제거 본체 1개");
        p.ReportResults(ticket, new[] { Record(TutorialResultKind.Removed, "obstacle.a", "body2", A), Record(TutorialResultKind.Generated, "obstacle.a", "refill", A) });
        Check(p.Snapshot.ConditionCounts[0] == 2 && p.Snapshot.ConditionCounts[1] == 0, "재보충 무관·다른 개체만 수량 증가");
        p.ReportResults(ticket, new[] { Record(TutorialResultKind.Removed, "obstacle.b", "body3", A), Record(TutorialResultKind.Removed, "obstacle.b", "body3", B) });
        Check(p.State == TutorialProgressState.Completed, "같은 판정기로 서로 다른 ID 및 footprint 좌표 처리");
        Reject(() => Record(TutorialResultKind.Removed, "x", ""), "집계 식별 없는 기록 거절");
        TutorialStepDefinition bad = Step(); bad.results.Add(new TutorialResultDefinition { count = 0, definitionId = "x" });
        Check(Progress(bad).State == TutorialProgressState.Error, "잘못된 조건 수량 진단");
    }

    private static void VerifyLifetime()
    {
        TutorialStepDefinition source = Step(); source.highlights.Add(A);
        TutorialProgress p = Progress(source, Step(TutorialStepKind.Description));
        source.first = new BoardCoordinate(8, 8); source.instructions = "변경"; source.highlights.Clear();
        Check(p.Snapshot.Instructions == "안내" && p.Snapshot.Highlights.Count == 1, "원본 편집과 진행 사본 분리");
        TutorialProgressSnapshot snapshot = p.Snapshot;
        TutorialActionTicket ticket = Approve(p, TutorialInput.Swap(A, B)); p.SetPaused(true);
        Check(!p.TryApprove(TutorialInput.Next(), out _), "일시정지 입력 차단");
        Finish(p, ticket);
        Check(p.StepIndex == 0 && p.Snapshot.IsPaused, "정지 중 신호 보관·진행 보류");
        p.SetPaused(false);
        Check(p.StepIndex == 1 && snapshot.StepIndex == 0 && snapshot.State == TutorialProgressState.AwaitAction, "재개 진행·기존 snapshot 불변");
        p.SetPaused(true);
        Check(!p.TryApprove(TutorialInput.Next(), out _) && p.StepIndex == 1, "설명 정지");
        p.SetPaused(false); p.TryApprove(TutorialInput.Next(), out _);
        Check(p.State == TutorialProgressState.Completed, "설명 재개");

        TutorialProgress first = Progress(Step()), second = Progress(Step());
        TutorialActionTicket t1 = Approve(first, TutorialInput.Swap(A, B)), t2 = Approve(second, TutorialInput.Swap(A, B));
        Finish(second, t1);
        Check(second.State != TutorialProgressState.Completed, "다른 세션 ticket 무시");
        first.Cancel(); Finish(first, t1);
        Check(first.State == TutorialProgressState.Cancelled && !first.TryApprove(TutorialInput.Swap(A, B), out _), "취소 후 모든 신호/입력 차단");
        Finish(second, t2); Check(second.State == TutorialProgressState.Completed, "취소가 다른 세션에 영향 없음");
        TutorialProgress disposed = Progress(Step()); TutorialActionTicket td = Approve(disposed, TutorialInput.Swap(A, B));
        disposed.Dispose(); disposed.Dispose(); Finish(disposed, td);
        Check(disposed.State == TutorialProgressState.Cancelled, "반복 Dispose 및 지연 신호 안전");
        TutorialStepDefinition item = Step(TutorialStepKind.Item);
        p = Progress(item); ticket = Approve(p, TutorialInput.UseItem(BoardItem.Hammer, A));
        p.Cancel(); Check(!p.Snapshot.FreeItemAvailable, "취소 권한 해제");
    }

    private sealed class ExtensionHandler : ITutorialStepHandler
    {
        public int Calls;
        public bool IsDescription => false;
        public bool UsesFreeItem => false;
        public bool ConsumesMove => false;
        public void Validate(TutorialStepDefinition step, TutorialValidationContext context) { }
        public IEnumerable<string> References(TutorialStepDefinition step) { yield return "obstacle.scrap"; }
        public bool Allows(TutorialStepDefinition step, TutorialInput input)
        { Calls++; return input.Kind == TutorialInputKind.Activate && input.First.HasValue && input.First.Value.Equals(A); }
        public bool IsSuccessful(TutorialStepDefinition step, TutorialInput input, bool succeeded, string definitionId) => succeeded;
    }
    private sealed class ExtensionEvaluator : ITutorialResultEvaluator
    {
        public int Calls;
        public void Validate(TutorialResultDefinition result, TutorialValidationContext context) { }
        public IEnumerable<string> References(TutorialResultDefinition result) { yield return result.definitionId; }
        public bool Matches(TutorialResultDefinition result, TutorialResultRecord record)
        { Calls++; return record.Kind == result.kind && record.DefinitionId == result.definitionId; }
    }
    private static void VerifyExtensions()
    {
        ExtensionHandler action = new ExtensionHandler(), unused = new ExtensionHandler();
        ExtensionEvaluator condition = new ExtensionEvaluator(), unusedCondition = new ExtensionEvaluator();
        TutorialHandlerRegistry registry = new TutorialHandlerRegistry();
        registry.RegisterStep((TutorialStepKind)91, action);
        registry.RegisterStep((TutorialStepKind)92, unused);
        registry.RegisterResult((TutorialResultKind)91, condition);
        registry.RegisterResult((TutorialResultKind)92, unusedCondition);
        TutorialStepDefinition step = Step((TutorialStepKind)91);
        step.results.Add(new TutorialResultDefinition { kind = (TutorialResultKind)91, definitionId = "obstacle.scrap" });
        TutorialProgress p = new TutorialProgress(new LevelTutorialDefinition { steps = new List<TutorialStepDefinition> { step } }, registry);
        TutorialActionTicket t = Approve(p, TutorialInput.Activate(A));
        p.ReportResults(t, new[] { Record((TutorialResultKind)91, "obstacle.scrap", "custom") }); Finish(p, t);
        Check(p.State == TutorialProgressState.Completed && action.Calls > 0 && condition.Calls > 0, "기존 엔진 수정 없이 신규 종류 등록 실행");
        Check(unused.Calls == 0 && unusedCondition.Calls == 0, "미사용 행동/조건 호출 0");
        Check(!TutorialHandlerRegistry.CreateDefault().TryGetStep(step, out _), "시험 등록이 운영 목록에 안 남음");
        registry = new TutorialHandlerRegistry(); registry.RegisterStep(TutorialStepKind.Swap, action);
        p = new TutorialProgress(new LevelTutorialDefinition { steps = new List<TutorialStepDefinition> { Step() } }, registry);
        Check(!p.TryApprove(TutorialInput.Swap(A, B), out _), "기존 종류 처리기 교체 허용 규칙 반영");
        t = Approve(p, TutorialInput.Activate(A)); Finish(p, t);
        Check(p.State == TutorialProgressState.Completed, "대체 처리기로 성공");
        registry = new TutorialHandlerRegistry(); registry.RegisterStep(TutorialStepKind.Swap, new TutorialSwapHandler());
        registry.RegisterResult(TutorialResultKind.Removed, new ExtensionEvaluator());
        step = Step(); step.results.Add(new TutorialResultDefinition { kind = TutorialResultKind.Removed, definitionId = "x", hasCoordinate = true, coordinate = B });
        p = new TutorialProgress(new LevelTutorialDefinition { steps = new List<TutorialStepDefinition> { step } }, registry);
        t = Approve(p, TutorialInput.Swap(A, B));
        p.ReportResults(t, new[] { Record(TutorialResultKind.Removed, "x", "replacement", A) }); Finish(p, t);
        Check(p.State == TutorialProgressState.Completed, "기존 조건 판정기 교체로 다른 좌표 정책 적용");
    }

    private static void VerifyValidation()
    {
        LevelDefinition level = LevelTutorialDataVerification.Fixture();
        try
        {
            LevelTutorialDataVerification.Populate(level);
            Check(LevelTutorialValidator.Validate(level).Count == 0, "기존 4단계 정상 설정·후속 파워 초기 배치 불요");
            Check(LevelTutorialValidator.References(level.Tutorial).Contains("power.rocket"), "기존 참조 수집");
            level.Tutorial.steps[0].results.Add(new TutorialResultDefinition { kind = TutorialResultKind.Removed, definitionId = "obstacle.scrap" });
            Check(LevelTutorialValidator.Validate(level).Any(issue => issue.PropertyPath.EndsWith(".results")), "설명 결과 모순을 실제 레벨 검사에도 표시");
            level.Tutorial.steps[0].results.Clear();
            level.Tutorial.steps[1].kind = (TutorialStepKind)91;
            Check(LevelTutorialValidator.Validate(level).Any(issue => issue.PropertyPath.Contains("kind")), "정적 검사 미등록 단계 진단");
            TutorialHandlerRegistry registry = new TutorialHandlerRegistry();
            ExtensionHandler handler = new ExtensionHandler();
            registry.RegisterStep((TutorialStepKind)91, handler);
            level.Tutorial.steps.Clear(); level.Tutorial.steps.Add(Step((TutorialStepKind)91));
            Check(LevelTutorialValidator.Validate(level, null, registry).Count == 0, "등록된 신규 종류 정적 검사 지원");
            Check(LevelTutorialValidator.References(level.Tutorial, registry).Contains("obstacle.scrap"), "등록된 처리기의 참조 수집");
            Reject(() => LevelTutorialValidator.References(level.Tutorial).ToArray(), "지원 없는 참조 수집 거절");
        }
        finally { UnityEngine.Object.DestroyImmediate(level); }
    }

    private static void VerifyBoundaryCases()
    {
        // 결과/성공/연출의 모든 순서를 실제 엔진에 전달한다. 순서가 달라도 정확히 한 단계만 진행해야 한다.
        foreach (string order in new[] { "rsp", "rps", "srp", "spr", "prs", "psr" })
        {
            TutorialStepDefinition step = Step();
            step.results.Add(new TutorialResultDefinition { kind = TutorialResultKind.Removed, definitionId = "obstacle.scrap" });
            TutorialProgress p = Progress(step, Step(TutorialStepKind.Description));
            TutorialActionTicket t = Approve(p, TutorialInput.Swap(A, B));
            for (int i = 0; i < order.Length; i++)
            {
                switch (order[i])
                {
                    case 'r': p.ReportResults(t, new[] { Record(TutorialResultKind.Removed, "obstacle.scrap", "body") }); break;
                    case 's': p.ReportCompletion(t, true); break;
                    case 'p': p.ReportPresentationComplete(t); break;
                }
                Check(p.StepIndex == (i == 2 ? 1 : 0), "신호 순서 독립 " + order + " " + i);
            }
        }
        TutorialHandlerRegistry shared = TutorialHandlerRegistry.CreateDefault();
        TutorialStepDefinition item = Step(TutorialStepKind.Item);
        LevelTutorialDefinition data = new LevelTutorialDefinition { steps = new List<TutorialStepDefinition> { item } };
        TutorialProgress one = new TutorialProgress(data, shared), two = new TutorialProgress(data, shared);
        TutorialActionTicket token = Approve(one, TutorialInput.UseItem(BoardItem.Hammer, A)); one.ReportCompletion(token, true);
        Check(!one.Snapshot.FreeItemAvailable && two.Snapshot.FreeItemAvailable, "같은 등록 목록의 두 실행 권한 격리");
        TutorialStepDefinition copy = Step(); copy.results.Add(new TutorialResultDefinition { kind = TutorialResultKind.Removed, definitionId = "x", count = 2 });
        TutorialProgress original = Progress(copy); copy.results[0].count = 1; copy.results[0].definitionId = "changed";
        token = Approve(original, TutorialInput.Swap(A, B)); Finish(original, token);
        original.ReportResults(token, new[] { Record(TutorialResultKind.Removed, "x", "first") });
        Check(original.State != TutorialProgressState.Completed && original.Snapshot.ConditionCounts[0] == 1, "원본 조건 편집과 실행 사본 격리");
        original.ReportResults(token, new[] { Record(TutorialResultKind.Removed, "x", "second") });
        Check(original.State == TutorialProgressState.Completed, "사본의 원래 조건 수량 적용");
        Reject(() => new TutorialHandlerRegistry().RegisterItem(BoardItem.Hammer, null), "null 아이템 처리기 거절");
        Reject(() => new TutorialHandlerRegistry().RegisterResult(TutorialResultKind.Removed, null), "null 판정기 거절");
        TutorialHandlerRegistry reverse = new TutorialHandlerRegistry();
        reverse.RegisterResult(TutorialResultKind.Removed, new TutorialResultEvaluator(TutorialResultKind.Removed));
        reverse.RegisterStep(TutorialStepKind.Swap, new TutorialSwapHandler());
        TutorialStepDefinition ordered = Step(); ordered.results.Add(new TutorialResultDefinition { kind = TutorialResultKind.Removed, definitionId = "x" });
        original = new TutorialProgress(new LevelTutorialDefinition { steps = new List<TutorialStepDefinition> { ordered } }, reverse);
        token = Approve(original, TutorialInput.Swap(A, B)); original.ReportResults(token, new[] { Record(TutorialResultKind.Removed, "x", "ordered") }); Finish(original, token);
        Check(original.State == TutorialProgressState.Completed, "등록 순서가 진행에 영향 없음");
        TutorialStepDefinition description = Step(TutorialStepKind.Description);
        description.results.Add(new TutorialResultDefinition { kind = TutorialResultKind.Removed, definitionId = "x" });
        Check(Progress(description).State == TutorialProgressState.Error, "설명에 실행 결과 조건을 붙인 모순 진단");
        original = Progress(Step()); token = Approve(original, TutorialInput.Swap(A, B));
        original.Fail("공급 부족"); Finish(original, token);
        Check(original.State == TutorialProgressState.Error && original.Message == "공급 부족", "외부 설정 오류 후 지연 성공 차단");
    }
}
