using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Board;
using Levels;
using Levels.Editor;
using Simulation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tutorial.Editor
{
    public static partial class TutorialComposerConditionsVerification
    {
        private static readonly List<string> Results = new List<string>();
        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); Results.Add("PASS " + message); }

        public static void Run()
        {
            Results.Clear();
            try { VerifyTargetIdentity(); VerifyTargetSelection(); VerifyLargeTarget(); VerifyDamageOrigins(); VerifyDamageConditions(); VerifyConditionStateBoundaries(); VerifyPowerConditions(); VerifyGeneratedBindings(); VerifyConditionEditor(); VerifyRepeatedItems(); VerifyMissionConditions(); VerifyActionArea(); VerifyGeneratedActions(); VerifyAutomaticTargets(); VerifyGuidanceExit(); VerifySampleBoards(); VerifyStartupReplay(); VerifyFinalBoundaries(); }
            catch (Exception error) { Results.Add("FAIL " + error); Debug.LogException(error); }
            Directory.CreateDirectory("Logs/Tutorial/Composer02");
            File.WriteAllLines("Logs/Tutorial/Composer02/results.txt", Results);
            EditorApplication.Exit(Results.Any(value => value.StartsWith("FAIL")) ? 1 : 0);
        }

        private static void VerifyStartupReplay()
        {
            LevelDefinition level = TutorialSampleBoards.All.First(sample => sample.Id == "follow").CreateBoard();
            try
            {
                level.Tutorial.steps[1].second = new BoardCoordinate(0, 0);
                Check(LevelTutorialValidator.Validate(level).Count == 0 && LevelTutorialReplayValidator.Validate(level).Count > 0,
                    "구조는 유효하지만 생성 후 후속 교환이 불가능한 준비 오류 재현");
                MethodInfo check = typeof(GameScreen.PuzzleGameSession).GetMethod("CheckTutorialReplay", BindingFlags.NonPublic | BindingFlags.Static);
                Check(check.ReturnType == typeof(bool), "시작 전 재생 오류는 튜토리얼 실행 여부로 반환");
                Check(!(bool)check.Invoke(null, new object[] { level }), "시작 전 안내 재생 오류는 정상 보드 진행을 막지 않음");
            }
            finally { UnityEngine.Object.DestroyImmediate(level); }
        }

        private static void VerifySampleBoards()
        {
            Type library = typeof(TutorialSampleCatalog).Assembly.GetType("Tutorial.TutorialSampleBoards");
            Check(library != null, "기본 샘플의 독립 시험 보드 라이브러리 제공");
            foreach (object sample in (IEnumerable)library.GetProperty("All").GetValue(null))
            {
                string title = (string)sample.GetType().GetProperty("Title").GetValue(sample);
                Check(!string.IsNullOrWhiteSpace((string)sample.GetType().GetProperty("ExpectedResult").GetValue(sample)), "샘플 예상 결과 제공: " + title);
                LevelDefinition level = (LevelDefinition)sample.GetType().GetMethod("CreateBoard").Invoke(sample, null);
                try
                {
                    string source = JsonUtility.ToJson(level);
                    var issues = LevelTutorialReplayValidator.Validate(level);
                    if (issues.Count > 0)
                    {
                        LevelRuntimeState diagnostic = LevelStateBuilder.Build(level, level.Tutorial.seed).State;
                        Results.Add("FAIL 샘플 실제 재생: " + title + " · " + string.Join(" / ", issues) +
                            (diagnostic == null ? "" : " · 시작 매칭 " + string.Join(" / ", MatchQuery.Find(diagnostic).Select(match => match.Key)))); continue;
                    }
                    Check(issues.Count == 0, "샘플 실제 재생: " + title + " · " + string.Join(" / ", issues));
                    Check(AssetDatabase.GetAssetPath(level) == "" && JsonUtility.ToJson(level) == source, "샘플은 미저장 독립 사본이며 검사 중 원본 보존: " + title);
                    LevelDefinition packed = LevelPackCodec.ReadLevel(LevelPackCodec.Snapshot(level), level.LevelNumber);
                    try { Check(LevelTutorialReplayValidator.Validate(packed).Count == 0, "샘플 MemoryPack 실행: " + title); }
                    finally { UnityEngine.Object.DestroyImmediate(packed); }
                }
                finally { UnityEngine.Object.DestroyImmediate(level); }
            }
            MethodInfo open = typeof(LevelEditorWindow).GetMethod("OpenTutorialSample", BindingFlags.Public | BindingFlags.Static);
            Check(open != null, "시험 보드를 기존 레벨 에디터의 별도 창에서 여는 기능 제공");
            object chosen = ((IEnumerable)library.GetProperty("All").GetValue(null)).Cast<object>().First();
            LevelEditorWindow window = (LevelEditorWindow)open.Invoke(null, new[] { chosen });
            LevelDefinition temporary = window.CurrentLevel;
            try
            {
                Check(window.rootVisualElement.Q("tutorial-sample-notice") != null && window.rootVisualElement.Q("tutorial-sample-boards") != null,
                    "기존 편집 화면 안에서 예상 결과와 샘플 선택을 제공");
                Check(AssetDatabase.GetAssetPath(temporary) == "" && !window.rootVisualElement.Q<Button>("save-level").enabledSelf,
                    "시험 보드는 출시 에셋으로 자동 저장되지 않음");
            }
            finally { window.Close(); UnityEngine.Object.DestroyImmediate(window); }
            Check(temporary == null, "시험 창을 닫으면 소유한 임시 보드 사본 해제");
        }

        private static void VerifyGuidanceExit()
        {
            LevelDefinition level = LevelPackCodec.ReadLevel(File.ReadAllBytes("Tests/Fixtures/Tutorial/legacy-v3.bytes"), 1);
            GameObject owner = null;
            try
            {
                TutorialStepDefinition step = level.Tutorial.steps.First(value => value.kind == TutorialStepKind.Swap);
                step.results.Clear(); step.conditions = new List<TutorialConditionDefinition> { new TutorialConditionDefinition { requiredCount = 2 } };
                level.Tutorial.steps = new List<TutorialStepDefinition> { step };
                using TutorialBoardAdapter adapter = TutorialBoardAdapter.Prepare(level, StartingBoardBuilder.Build(level, level.Tutorial.seed).State);
                RuntimeSupply fixedSupply = adapter.Executor.State.Supply;
                adapter.Progress.Fail("시험: 다음 안내 대상 소실");
                adapter.Tick(false, false);
                Check(ReferenceEquals(fixedSupply, adapter.Executor.State.Supply), "안내 오류가 나도 연출 종료 전 공급을 전환하지 않음");
                adapter.Tick(true, true);
                Check(ReferenceEquals(fixedSupply, adapter.Executor.State.Supply), "일시정지 중 안내 오류 복귀를 보류");
                adapter.Tick(true, false);
                Check(adapter.Progress.State == TutorialProgressState.Error && !ReferenceEquals(fixedSupply, adapter.Executor.State.Supply), "진행 가능한 안내 오류는 진단을 보존하며 일반 공급 복귀");
                owner = new GameObject("ComposerGuidanceExitFixture");
                GameScreen.PuzzleGameSession session = owner.AddComponent<GameScreen.PuzzleGameSession>();
                void Set(string field, object value) => typeof(GameScreen.PuzzleGameSession).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, value);
                Set("started", true); Set("ready", true); Set("executor", adapter.Executor); Set("tutorial", adapter);
                int completed = 0;
                Set("tutorialContext", TutorialExecutionContext.CreateEditor(TutorialRunMode.Always, _ => false, _ => completed++));
                typeof(GameScreen.PuzzleGameSession).GetMethod("TickTutorial", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(session, null);
                Check(session.CanAcceptInput && session.TutorialState.State == TutorialProgressState.Error && completed == 0, "게임 세션은 오류 안내만 해제하고 일반 입력 허용·완료 미기록");
                Check(session.CanPreviewSwap(new BoardCoordinate(0, 0), new BoardCoordinate(0, 1)), "안내 밖 칸에 남은 튜토리얼 입력 제한 없음");
                LevelRuntimeState remainingOne = StartingBoardBuilder.Build(level, level.Tutorial.seed).State;
                typeof(LevelRuntimeState).GetProperty("MovesRemaining").SetValue(remainingOne, 1);
                using TutorialBoardAdapter exhausted = TutorialBoardAdapter.Prepare(level, remainingOne);
                Check(exhausted.TryBegin(TutorialInput.Swap(step.first, step.second)), "이동 소진 시험의 실제 교환 승인");
                BoardActionResult action = exhausted.Executor.Swap(step.first, step.second); exhausted.ReportAction(action.IsApplied);
                int rounds = 100;
                while (exhausted.Executor.HasPendingCascade && rounds-- > 0) exhausted.ObserveCascade(exhausted.Executor.AdvanceCascade());
                exhausted.Tick(false, false);
                Check(action.IsApplied && exhausted.Executor.State.MovesRemaining == 0 && exhausted.Executor.Outcome == null &&
                    exhausted.Progress.State == TutorialProgressState.AwaitPresentation, "마지막 이동 후에도 연출 전에 패배나 안내 종료를 확정하지 않음");
                exhausted.Tick(true, false);
                Check(exhausted.Progress.State == TutorialProgressState.Cancelled && exhausted.Executor.Outcome?.Kind == BoardOutcomeKind.MovesExhausted,
                    "조건 미충족 이동 소진은 안내 취소 후 정상 실패 흐름으로 복귀");
                Set("executor", exhausted.Executor); Set("tutorial", exhausted);
                typeof(GameScreen.PuzzleGameSession).GetMethod("TickTutorial", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(session, null);
                Check(!session.HasFailed && session.Outcome?.Kind == BoardOutcomeKind.MovesExhausted && session.TutorialState.State == TutorialProgressState.Cancelled && completed == 0,
                    "이동 소진은 게임 처리 오류가 아니며 튜토리얼 완료를 기록하지 않음");
                using TutorialBoardAdapter retry = TutorialBoardAdapter.Prepare(level, StartingBoardBuilder.Build(level, level.Tutorial.seed).State);
                Check(retry.Progress.StepIndex == 0 && retry.Progress.Snapshot.ConditionCounts[0] == 0 && retry.Executor.State.MovesRemaining == level.MoveCount,
                    "재시도 준비는 첫 단계·집계0·원래 이동 수로 복원");
                RuntimeSupply stoppedSupply = retry.Executor.State.Supply;
                retry.Progress.Fail("시험: 퍼즐 실행 중단");
                typeof(BoardActionExecutor).GetProperty("Phase").SetValue(retry.Executor, BoardActionPhase.Stopped);
                retry.Tick(true, false);
                Check(!retry.IsReleased && ReferenceEquals(stoppedSupply, retry.Executor.State.Supply), "실행기가 중단된 경우 안내 복귀로 정상 보드를 가장하지 않음");
                Set("ready", true); Set("executor", retry.Executor); Set("tutorial", retry);
                typeof(GameScreen.PuzzleGameSession).GetMethod("TickTutorial", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(session, null);
                Check(session.HasFailed && !session.CanAcceptInput && completed == 0, "실제 퍼즐 중단은 기존 오류 화면 경로와 입력 차단 유지");
            }
            finally { if (owner != null) UnityEngine.Object.DestroyImmediate(owner); UnityEngine.Object.DestroyImmediate(level); }
        }

        private static void VerifyAutomaticTargets()
        {
            object Invoke(string method, params object[] args) => typeof(FixedObstacleVerification).GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
            LevelDefinition level = (LevelDefinition)Invoke("Make");
            try
            {
                BoardCoordinate from = new BoardCoordinate(4, 0), to = new BoardCoordinate(5, 0);
                Invoke("Place", level, from, InitialBlockKind.Rocket, RocketDirection.Horizontal, RabbitColor.Type1);
                LevelRuntimeState state = (LevelRuntimeState)Invoke("Build", level, 12345);
                IReadOnlyList<TutorialTargetEntity> current = TutorialTargetQuery.Capture(state);
                TutorialTargetEntity rocket = current.Single(value => value.Cells.Contains(from) && value.Layer == TutorialTargetLayer.Content);
                TutorialTargetEntity moved = (TutorialTargetEntity)Activator.CreateInstance(typeof(TutorialTargetEntity), BindingFlags.Instance | BindingFlags.NonPublic, null,
                    new object[] { rocket.Occurrence, rocket.Definition, rocket.Layer, new[] { to }, rocket.Durability, rocket.RocketDirection }, null);
                TutorialStepDefinition step = TutorialSampleCatalog.CreateSwap(new BoardCoordinate(0, 0), new BoardCoordinate(0, 1), false);
                step.automaticHighlights = true;
                step.conditions = new List<TutorialConditionDefinition> { new TutorialConditionDefinition { kind = TutorialConditionKind.Activated,
                    powerDefinitionId = rocket.Definition.Id.Value, target = new TutorialTargetDefinition { kind = TutorialTargetKind.Entity, coordinate = from } } };
                var definition = new LevelTutorialDefinition { steps = new List<TutorialStepDefinition> { step } };
                using (TutorialProgress progress = new TutorialProgress(definition, readTargets: () => current))
                {
                    Check(progress.Snapshot.Highlights.Count == 3 && progress.Snapshot.Highlights.Contains(from), "자동 강조는 조작 칸과 조건 개체를 합침");
                    Check(progress.TryApprove(TutorialInput.Swap(step.first, step.second), out TutorialActionTicket ticket), "자동 강조 이동 시험 행동 승인");
                    progress.ReportCompletion(ticket, true);
                    current = current.Where(value => value.Occurrence != rocket.Occurrence && !value.Cells.Contains(to)).Append(moved).ToArray();
                    progress.ReportResultsComplete(ticket);
                    Check(progress.Snapshot.Highlights.Contains(from) && !progress.Snapshot.Highlights.Contains(to), "논리 결과만 끝나면 기존 강조를 유지하고 연출 종료 대기");
                    progress.ReportPresentationComplete(ticket);
                    Check(progress.State == TutorialProgressState.AwaitAction && progress.Snapshot.Highlights.Contains(to) && !progress.Snapshot.Highlights.Contains(from), "연출 후 동일 개체의 이동 위치로 자동 강조 갱신");
                }
                step.conditions[0].target = new TutorialTargetDefinition { kind = TutorialTargetKind.Area, cells = new List<BoardCoordinate> { from } };
                using (TutorialProgress progress = new TutorialProgress(definition, readTargets: () => current))
                    Check(progress.Snapshot.Highlights.Contains(from) && !progress.Snapshot.Highlights.Contains(to), "빈 영역도 지정한 칸을 강조하며 이동 개체를 따라가지 않음");
                step.conditions[0].target = new TutorialTargetDefinition { kind = TutorialTargetKind.Definition, definitionId = rocket.Definition.Id.Value };
                using (TutorialProgress progress = new TutorialProgress(definition, readTargets: () => current))
                    Check(progress.Snapshot.Highlights.Contains(to), "종류 대상은 현재 일치하는 개체 위치 강조");
                step.conditions[0].target = new TutorialTargetDefinition();
                using (TutorialProgress progress = new TutorialProgress(definition, readTargets: () => current))
                    Check(progress.Snapshot.Highlights.Count == 2, "보드 전체 조건은 조작 칸만 자동 강조");
                step.automaticHighlights = false; step.highlights = new List<BoardCoordinate> { from };
                using (TutorialProgress progress = new TutorialProgress(definition, readTargets: () => current))
                    Check(progress.Snapshot.Highlights.SequenceEqual(new[] { from }), "수동 강조는 조건 대상이나 조작 칸을 자동 추가하지 않음");
            }
            finally { UnityEngine.Object.DestroyImmediate(level); }
        }

        private static void VerifyGeneratedActions()
        {
            Check(typeof(TutorialStepDefinition).GetField("firstBinding") != null, "생성 결과를 조작 대상으로 참조하는 계약");
            LevelDefinition level = (LevelDefinition)typeof(TutorialRocketLevelVerification).GetMethod("Candidate", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            try
            {
                foreach (ElementPlacementDefinition cell in level.Elements) cell.color = level.Colors[(cell.coordinate.Row * 2 + cell.coordinate.Column) % level.Colors.Count];
                BoardCoordinate donor = new BoardCoordinate(4, 2), spawn = new BoardCoordinate(4, 3), fallen = new BoardCoordinate(6, 3), neighbor = new BoardCoordinate(6, 4);
                for (int row = 3; row <= 6; row++) level.Elements.Single(cell => cell.coordinate.Equals(new BoardCoordinate(row, 3))).color = level.Colors[0];
                level.Elements.Single(cell => cell.coordinate.Equals(spawn)).color = level.Colors[1];
                level.Elements.Single(cell => cell.coordinate.Equals(donor)).color = level.Colors[0];
                level.Elements.Single(cell => cell.coordinate.Equals(new BoardCoordinate(3, 2))).color = level.Colors[2];
                TutorialStepDefinition first = TutorialSampleCatalog.CreateSwap(donor, spawn, false);
                first.conditions = new List<TutorialConditionDefinition> { new TutorialConditionDefinition { kind = TutorialConditionKind.Generated, powerDefinitionId = "power.rocket", bindGeneratedAs = "만든 로켓", target = new TutorialTargetDefinition() } };
                TutorialStepDefinition next = TutorialSampleCatalog.CreateSwap(spawn, neighbor, false);
                JsonUtility.FromJsonOverwrite("{\"hasFirst\":false,\"firstBinding\":\"만든 로켓\"}", next);
                next.conditions = new List<TutorialConditionDefinition> { new TutorialConditionDefinition { kind = TutorialConditionKind.Activated, powerDefinitionId = "power.rocket", target = new TutorialTargetDefinition { kind = TutorialTargetKind.Generated, binding = "만든 로켓" } } };
                level.Tutorial.steps = new List<TutorialStepDefinition> { first, next };
                LevelBoardView board = new LevelBoardView(); board.Display(level, null);
                EditorWindow host = ScriptableObject.CreateInstance<EditorWindow>(); host.ShowUtility(); host.rootVisualElement.Add(board);
                try
                {
                    using var panel = new LevelTutorialEditorPanel(level, board, 1, _ => { }); host.rootVisualElement.Add(panel);
                    PopupField<string> choice = panel.Q<PopupField<string>>("tutorial-action-binding-first");
                    Check(choice != null && choice.index == 1 && choice.choices.Count == 2, "후속 조작에서 이전 생성 연결을 선택하는 UI 제공");
                    choice.value = choice.choices[0]; Check(string.IsNullOrEmpty(level.Tutorial.steps[1].firstBinding), "생성 조작 참조를 지정 칸 방식으로 해제");
                    choice = panel.Q<PopupField<string>>("tutorial-action-binding-first"); choice.value = choice.choices[1];
                    Check(level.Tutorial.steps[1].firstBinding == "만든 로켓", "이전 생성 이름을 조작 참조로 저장");
                }
                finally { host.Close(); UnityEngine.Object.DestroyImmediate(host); }
                LevelDefinition packed = LevelPackCodec.ReadLevel(LevelPackCodec.Snapshot(level), level.LevelNumber);
                try { Check(JsonUtility.ToJson(packed.Tutorial.steps[1]) == JsonUtility.ToJson(next), "생성 조작 참조 팩 왕복"); }
                finally { UnityEngine.Object.DestroyImmediate(packed); }
                StartingBoardSearch start = StartingBoardBuilder.Build(level, level.Tutorial.seed);
                Check(start.State != null, "실제 생성 시험 시작 보드: " + start.Message);
                using TutorialBoardAdapter adapter = TutorialBoardAdapter.Prepare(level, start.State);
                Check(adapter.TryBegin(TutorialInput.Swap(donor, spawn)), "실제 세로4매칭 생성 행동 승인");
                BoardActionResult result = adapter.Executor.Swap(donor, spawn); adapter.ReportAction(result.IsApplied);
                int budget = 100;
                while (adapter.Executor.HasPendingCascade && budget-- > 0) adapter.ObserveCascade(adapter.Executor.AdvanceCascade());
                adapter.Tick(true, false);
                Check(result.IsApplied && adapter.Progress.StepIndex == 1 && adapter.Progress.State == TutorialProgressState.AwaitAction && adapter.Progress.Snapshot.First?.Equals(fallen) == true,
                    "생성 후 두 칸 낙하한 동일 로켓의 현재 위치를 다음 조작에 사용");
                Check(!adapter.Progress.CanApprove(TutorialInput.Swap(spawn, neighbor)) && adapter.TryBegin(TutorialInput.Swap(fallen, neighbor)), "생성 당시 좌표를 대체하지 않고 낙하한 개체만 조작 허용");
                result = adapter.Executor.Swap(fallen, neighbor); adapter.ReportAction(result.IsApplied); budget = 100;
                while (adapter.Executor.HasPendingCascade && budget-- > 0) adapter.ObserveCascade(adapter.Executor.AdvanceCascade());
                adapter.Tick(true, false);
                Check(result.IsApplied && adapter.Progress.State == TutorialProgressState.Completed, "생성·낙하·동일 개체 조작·발동 조건의 실제 실행 완료");
                Check(!next.hasFirst && next.first.Equals(spawn), "생성 조작 위치 해석은 제작 좌표를 변경하지 않음");
            }
            finally { UnityEngine.Object.DestroyImmediate(level); }
        }

        private static void VerifyActionArea()
        {
            Check(typeof(TutorialStepDefinition).GetField("actionArea") != null, "조건 대상과 별개인 조작 영역 계약");
            TutorialStepDefinition step = JsonUtility.FromJson<TutorialStepDefinition>("{\"kind\":1,\"instructions\":\"영역에서 교환\",\"actionArea\":[{\"row\":2,\"column\":2},{\"row\":2,\"column\":3},{\"row\":3,\"column\":3}],\"conditions\":[{\"kind\":0,\"requiredCount\":2}]}");
            var definition = new LevelTutorialDefinition { steps = new List<TutorialStepDefinition> { step } };
            using (TutorialProgress progress = new TutorialProgress(definition))
            {
                Check(progress.State == TutorialProgressState.AwaitAction, "영역 동작은 고정 두 칸 없이 준비");
                Check(progress.CanApprove(TutorialInput.Swap(new BoardCoordinate(2, 2), new BoardCoordinate(2, 3))) &&
                    progress.CanApprove(TutorialInput.Swap(new BoardCoordinate(3, 3), new BoardCoordinate(2, 3))), "영역 안의 서로 다른 인접 교환 허용");
                Check(!progress.CanApprove(TutorialInput.Swap(new BoardCoordinate(2, 2), new BoardCoordinate(3, 3))) &&
                    !progress.CanApprove(TutorialInput.Swap(new BoardCoordinate(2, 2), new BoardCoordinate(2, 1))), "대각선과 영역 바깥 교환 거절");
            }
            LevelDefinition level = LevelPackCodec.ReadLevel(File.ReadAllBytes("Tests/Fixtures/Tutorial/legacy-v3.bytes"), 1);
            try
            {
                TutorialStepDefinition actual = level.Tutorial.steps.First(value => value.kind == TutorialStepKind.Swap);
                level.Tutorial.steps = new List<TutorialStepDefinition> { step };
                byte[] bytes = LevelPackCodec.Snapshot(level);
                LevelDefinition restored = LevelPackCodec.ReadLevel(bytes, level.LevelNumber);
                try { Check(bytes[4] == 5 && JsonUtility.ToJson(restored.Tutorial.steps[0]) == JsonUtility.ToJson(step), "조작 영역 팩5 왕복"); }
                finally { UnityEngine.Object.DestroyImmediate(restored); }
                TutorialStepDefinition invalid = JsonUtility.FromJson<TutorialStepDefinition>(JsonUtility.ToJson(step));
                JsonUtility.FromJsonOverwrite("{\"actionArea\":[{\"row\":2,\"column\":2},{\"row\":3,\"column\":3}]}", invalid);
                level.Tutorial.steps[0] = invalid;
                Check(LevelTutorialValidator.Validate(level).Any(issue => issue.PropertyPath.Contains("actionArea")), "인접 교환이 없는 영역은 제작 오류");
                invalid.kind = TutorialStepKind.Description;
                Check(LevelTutorialValidator.Validate(level).Any(issue => issue.PropertyPath.Contains("actionArea")), "설명에는 조작 영역을 지정할 수 없음");
                BoardCoordinate first = actual.first, second = actual.second;
                actual.hasFirst = false; actual.hasSecond = false; actual.actionArea = new List<BoardCoordinate> { first, second };
                actual.results.Clear(); actual.conditions = new List<TutorialConditionDefinition> { new TutorialConditionDefinition { kind = TutorialConditionKind.SuccessfulSwap } };
                level.Tutorial.steps[0] = actual;
                using TutorialBoardAdapter adapter = TutorialBoardAdapter.Prepare(level, StartingBoardBuilder.Build(level, level.Tutorial.seed).State);
                MethodInfo find = typeof(TutorialBoardAdapter).GetMethod("TryFindGuidance");
                Check(find != null, "영역 안내 후보 조회 제공");
                int draws = adapter.Executor.State.Random.DrawCount, moves = adapter.Executor.State.MovesRemaining;
                string supply = string.Join(";", adapter.Executor.State.Supply.Sources.Select(source => source.ItemIndex + ":" + source.ItemConsumed));
                string cells = string.Join(";", adapter.Executor.State.Cells.Select(cell => cell.Content + ":" + cell.Color));
                object[] query = { null };
                Check((bool)find.Invoke(adapter, query) && ((TutorialInput)query[0]).First.HasValue, "실제 교환 성공 조건에 기여하는 영역 후보 선택");
                Check(adapter.Executor.State.Random.DrawCount == draws && adapter.Executor.State.MovesRemaining == moves &&
                    cells == string.Join(";", adapter.Executor.State.Cells.Select(cell => cell.Content + ":" + cell.Color)) && adapter.Progress.Snapshot.ConditionCounts.Single() == 0,
                    "안내 조회는 실제 보드·난수·이동·조건 집계를 변경하지 않음");
                adapter.Tick(true, false);
                Check(adapter.Progress.Snapshot.First.HasValue && adapter.Progress.Snapshot.Second.HasValue, "영역 안내 후보를 화면용 두 칸으로 전달");
                int revision = (int)typeof(TutorialProgress).GetProperty("GuidanceRevision", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(adapter.Progress);
                adapter.Tick(true, false);
                Check(revision == (int)typeof(TutorialProgress).GetProperty("GuidanceRevision", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(adapter.Progress) &&
                    supply == string.Join(";", adapter.Executor.State.Supply.Sources.Select(source => source.ItemIndex + ":" + source.ItemConsumed)), "안정된 보드의 안내를 매 프레임 재계산하거나 고정 공급을 소비하지 않음");
                Check(LevelTutorialReplayValidator.Validate(level).Count == 0, "영역 단계의 재생 검사가 동적 안내 행동을 실행");
                Check(adapter.CanSelectBlock(first) && adapter.CanSelectBlock(second), "실제 보드의 영역 안 블록 선택 허용");
                Check(adapter.TryBegin(TutorialInput.Swap(first, second)), "실제 영역 교환 승인");
                BoardActionResult result = adapter.Executor.Swap(first, second); adapter.ReportAction(result.IsApplied);
                int budget = 100;
                while (adapter.Executor.HasPendingCascade && budget-- > 0) adapter.ObserveCascade(adapter.Executor.AdvanceCascade());
                adapter.Tick(true, false);
                Check(result.IsApplied && adapter.Progress.State == TutorialProgressState.Completed, "영역 교환의 실제 결과·연쇄·연출 경계 후 조건 완료");
                actual.conditions = new List<TutorialConditionDefinition> { new TutorialConditionDefinition { kind = TutorialConditionKind.Activated,
                    target = new TutorialTargetDefinition(), powerDefinitionId = level.CreateElementCatalog().Definitions.First(value => value.Supply?.Content == RuntimeContent.Rocket).Id.Value } };
                using TutorialBoardAdapter irrelevant = TutorialBoardAdapter.Prepare(level, StartingBoardBuilder.Build(level, level.Tutorial.seed).State);
                Check(!(bool)find.Invoke(irrelevant, new object[] { null }), "파워 발동 조건에 무관한 일반 매칭을 안내하지 않음");
                irrelevant.Tick(true, false);
                Check(irrelevant.Progress.State == TutorialProgressState.Error && irrelevant.Progress.Message.Contains("기여"), "조건에 기여하는 영역 행동 부재를 명시적으로 진단");
            }
            finally { UnityEngine.Object.DestroyImmediate(level); }
        }

        private static void VerifyMissionConditions()
        {
            Check(Enum.TryParse("MissionProgress", out TutorialConditionKind kind), "미션 실제 증가량 조건 등록");
            TutorialConditionDefinition condition = JsonUtility.FromJson<TutorialConditionDefinition>("{\"kind\":9,\"missionIndex\":1,\"requiredCount\":3}");
            Check(TutorialHandlerRegistry.CreateDefault().TryGetCondition(kind, out ITutorialConditionEvaluator evaluator), "미션 조건 공용 처리기 등록");
            ITutorialConditionState state = evaluator.Create(condition, new TutorialConditionContext(Array.Empty<TutorialTargetEntity>(), new Dictionary<string, long>()));
            TutorialConditionEvent Event(int index, int amount) => (TutorialConditionEvent)Activator.CreateInstance(typeof(TutorialConditionEvent), new object[] { "mission:" + index + ":" + amount, index, amount });
            Check(!state.Accept(Event(0, 3)) && !state.Accept(Event(1, 0)), "다른 미션과 증가 없는 사건 제외");
            Check(state.Accept(Event(1, 2)) && state.Count == 2 && !state.IsSatisfied, "미션 사건 횟수가 아닌 실제 증가량 집계");
            Check(state.Accept(Event(1, 1)) && state.IsSatisfied, "지정 미션 증가량 누적 완료");
            int[] remaining = { 4 };
            TutorialStepDefinition first = TutorialSampleCatalog.CreateSwap(new BoardCoordinate(1, 1), new BoardCoordinate(1, 2), false);
            first.conditions = new List<TutorialConditionDefinition> { new TutorialConditionDefinition { kind = TutorialConditionKind.MissionProgress, missionIndex = 0, requiredCount = 2 } };
            TutorialStepDefinition next = JsonUtility.FromJson<TutorialStepDefinition>(JsonUtility.ToJson(first)); next.conditions[0].requiredCount = 3;
            using (TutorialProgress progress = new TutorialProgress(new LevelTutorialDefinition { steps = new List<TutorialStepDefinition> { first, next } }, readMissionRemaining: () => remaining))
            {
                Check(progress.TryApprove(TutorialInput.Swap(first.first, first.second), out TutorialActionTicket ticket), "미션 후속 단계 시험 행동 승인");
                progress.ReportCompletion(ticket, true); progress.ReportConditionEvents(ticket, new[] { Event(0, 2) }); remaining[0] = 2;
                progress.ReportResultsComplete(ticket);
                Check(progress.State == TutorialProgressState.AwaitPresentation, "후속 단계 미션 진단도 연출 종료 전 실행하지 않음");
                progress.ReportPresentationComplete(ticket);
                Check(progress.StepIndex == 1 && progress.State == TutorialProgressState.Error && progress.Message.Contains("미션"), "후속 단계 진입은 갱신된 남은 미션 목표량으로 검사");
            }
            TutorialConditionSample sample = TutorialSampleCatalog.ConditionSamples(Elements.LegacyElementDefinitions.DefaultCatalog).Single(value => value.Title == "미션 진행 늘리기");
            TutorialConditionDefinition sampleCopy = sample.CreateConditions().Single(); sampleCopy.requiredCount = 7;
            Check(sample.CreateConditions().Single().requiredCount == 1 && sample.CreateConditions().Single().missionIndex == -1, "미션 샘플은 미선택 독립 사본으로 적용");
            LevelDefinition level = LevelPackCodec.ReadLevel(File.ReadAllBytes("Tests/Fixtures/Tutorial/legacy-v3.bytes"), 1);
            try
            {
                TutorialStepDefinition step = level.Tutorial.steps.First(value => value.kind == TutorialStepKind.Swap);
                level.Tutorial.steps = new List<TutorialStepDefinition> { step }; step.results.Clear();
                step.conditions = new List<TutorialConditionDefinition> { JsonUtility.FromJson<TutorialConditionDefinition>("{\"kind\":9,\"missionIndex\":0,\"requiredCount\":1}") };
                step.conditions[0].SelectMission(0, level.Missions[0]);
                byte[] bytes = LevelPackCodec.Snapshot(level);
                LevelDefinition copy = LevelPackCodec.ReadLevel(bytes, level.LevelNumber);
                try { Check(bytes[4] == 5 && JsonUtility.ToJson(copy.Tutorial.steps[0].conditions[0]) == JsonUtility.ToJson(step.conditions[0]), "미션 조건 팩5 왕복"); }
                finally { UnityEngine.Object.DestroyImmediate(copy); }
                JsonUtility.FromJsonOverwrite("{\"missions\":[" + string.Join(",", level.Colors.Select(color => "{\"kind\":0,\"color\":" + (int)color + ",\"count\":100}")) + "]}", level);
                step.conditions = level.Missions.Select((mission, index) => JsonUtility.FromJson<TutorialConditionDefinition>("{\"kind\":9,\"missionIndex\":" + index + ",\"requiredCount\":1}")).ToList();
                for (int index = 0; index < level.Missions.Count; index++) step.conditions[index].SelectMission(index, level.Missions[index]);
                using (TutorialBoardAdapter adapter = TutorialBoardAdapter.Prepare(level, StartingBoardBuilder.Build(level, level.Tutorial.seed).State))
                {
                    int start = adapter.Executor.State.MissionProgressRecords.Count;
                    Check(adapter.TryBegin(TutorialInput.Swap(step.first, step.second)), "실제 미션 증가 교환 승인");
                    BoardActionResult result = adapter.Executor.Swap(step.first, step.second); adapter.ReportAction(result.IsApplied);
                    int[] expected = level.Missions.Select((mission, index) => adapter.Executor.State.MissionProgressRecords.Skip(start).Where(record => record.MissionIndex == index).Sum(record => record.Amount)).ToArray();
                    Check(result.IsApplied && expected.Sum() > 0 && adapter.Progress.Snapshot.ConditionCounts.SequenceEqual(expected), "실제 직접 매칭 미션 증가량과 튜토리얼 집계 일치 · applied=" + result.IsApplied + " expected=" + string.Join(",", expected) + " actual=" + string.Join(",", adapter.Progress.Snapshot.ConditionCounts));
                    adapter.ReportAction(true);
                    Check(adapter.Progress.Snapshot.ConditionCounts.SequenceEqual(expected), "중복 행동 보고로 미션 진행량 중복 집계하지 않음");
                    int budget = 100;
                    while (adapter.Executor.HasPendingCascade && budget-- > 0) adapter.ObserveCascade(adapter.Executor.AdvanceCascade());
                    expected = level.Missions.Select((mission, index) => adapter.Executor.State.MissionProgressRecords.Skip(start).Where(record => record.MissionIndex == index).Sum(record => record.Amount)).ToArray();
                    Check(!adapter.Executor.HasPendingCascade && adapter.Progress.Snapshot.ConditionCounts.SequenceEqual(expected), "실제 연쇄까지 미션 증가량 누적");
                }
                JsonUtility.FromJsonOverwrite("{\"missionIndex\":999}", step.conditions[0]);
                Check(LevelDefinitionValidator.Validate(level).Any(issue => issue.PropertyPath.EndsWith(".missionIndex")), "없는 미션 참조는 제작 오류");
                step.conditions.Clear();
                step.conditions.Add(JsonUtility.FromJson<TutorialConditionDefinition>("{\"kind\":9,\"missionIndex\":0,\"missionKind\":0,\"missionColor\":" + (int)level.Missions[0].Color + ",\"requiredCount\":1}"));
                using (SerializedObject data = new SerializedObject(level)) { data.FindProperty("missions").MoveArrayElement(0, 1); data.ApplyModifiedPropertiesWithoutUndo(); }
                Check(LevelTutorialValidator.Validate(level).Any(issue => issue.PropertyPath.EndsWith(".missionIndex")), "미션 순서 변경으로 다른 대상을 조용히 집계하지 않음");
                using (SerializedObject data = new SerializedObject(level)) { data.FindProperty("missions").MoveArrayElement(1, 0); data.ApplyModifiedPropertiesWithoutUndo(); }
                LevelRuntimeState complete = StartingBoardBuilder.Build(level, level.Tutorial.seed).State;
                typeof(RuntimeMission).GetProperty("Progress").SetValue(complete.Missions[0], complete.Missions[0].Target);
                bool rejected = false;
                try { using TutorialBoardAdapter invalid = new TutorialBoardAdapter(level, new BoardActionExecutor(complete)); }
                catch (ArgumentException error) { rejected = error.Message.Contains("미션"); }
                Check(rejected, "이미 완료된 미션의 추가 진행 조건은 단계 진입 오류");
            }
            finally { UnityEngine.Object.DestroyImmediate(level); }
        }

        private static void VerifyTargetIdentity()
        {
            Type query = typeof(TutorialProgress).Assembly.GetType("Tutorial.TutorialTargetQuery");
            Check(query != null, "내구도/생성 대상에 공용 개체 조회 경계 제공");
            LevelDefinition level = LevelPackCodec.ReadLevel(File.ReadAllBytes("Tests/Fixtures/Tutorial/legacy-v3.bytes"), 1);
            try
            {
                LevelRuntimeState state = StartingBoardBuilder.Build(level, level.Tutorial.seed).State;
                object[] Capture() => ((IEnumerable)query.GetMethod("Capture").Invoke(null, new object[] { state })).Cast<object>().ToArray();
                T Get<T>(object value, string field) => (T)value.GetType().GetProperty(field).GetValue(value);
                BoardCoordinate from = new BoardCoordinate(3, 4), to = new BoardCoordinate(4, 4);
                object original = Capture().Single(value => Get<IEnumerable<BoardCoordinate>>(value, "Cells").Contains(from) && Get<object>(value, "Layer").ToString() == "Content");
                long id = Get<long>(original, "Occurrence");
                RuntimeCell source = state.CellAt(from), target = state.CellAt(to);
                PropertyInfo occurrence = typeof(RuntimeCell).GetProperty("ContentOccurrence", BindingFlags.Instance | BindingFlags.NonPublic);
                typeof(RuntimeCell).GetProperty("Content").SetValue(target, source.Content);
                typeof(RuntimeCell).GetProperty("Color").SetValue(target, source.Color);
                occurrence.SetValue(target, occurrence.GetValue(source));
                typeof(RuntimeCell).GetProperty("Content").SetValue(source, RuntimeContent.Normal);
                object moved = Capture().Single(value => Get<long>(value, "Occurrence") == id);
                Check(Get<IEnumerable<BoardCoordinate>>(moved, "Cells").SequenceEqual(new[] { to }), "같은 개체는 이동한 현재 위치로 조회");
                Check(Capture().Any(value => Get<IEnumerable<BoardCoordinate>>(value, "Cells").Contains(from) && Get<long>(value, "Occurrence") != id), "원래 칸의 신규 블록은 다른 개체");
                typeof(RuntimeCell).GetProperty("Content").SetValue(target, RuntimeContent.Empty);
                Check(!Capture().Any(value => Get<long>(value, "Occurrence") == id), "소실 개체를 원래 칸 신규 블록으로 대체하지 않음");
            }
            finally { UnityEngine.Object.DestroyImmediate(level); }
        }

        private static void VerifyTargetSelection()
        {
            Type definition = typeof(TutorialProgress).Assembly.GetType("Tutorial.TutorialTargetDefinition");
            Type selection = typeof(TutorialProgress).Assembly.GetType("Tutorial.TutorialTargetSelection");
            Check(definition != null && selection != null, "대상 설정과 단계 진입 시 개체 선택 분리");
            LevelDefinition level = LevelPackCodec.ReadLevel(File.ReadAllBytes("Tests/Fixtures/Tutorial/legacy-v3.bytes"), 1);
            try
            {
                LevelRuntimeState state = StartingBoardBuilder.Build(level, level.Tutorial.seed).State;
                var entities = TutorialTargetQuery.Capture(state);
                BoardCoordinate from = new BoardCoordinate(3, 4), to = new BoardCoordinate(4, 4);
                TutorialTargetEntity entity = entities.Single(value => value.Layer == TutorialTargetLayer.Content && value.Cells.Contains(from));
                object Bind(string json) => selection.GetMethod("Bind").Invoke(null, new object[] { JsonUtility.FromJson(json, definition), entities, new Dictionary<string, long>() });
                bool Matches(object bound, BoardCoordinate at) => (bool)selection.GetMethod("Matches").Invoke(bound, new object[] { entity, at });
                object specific = Bind("{\"kind\":1,\"coordinate\":{\"row\":3,\"column\":4}}");
                object area = Bind("{\"kind\":3,\"cells\":[{\"row\":3,\"column\":4}]}");
                Check(Matches(specific, to), "특정 개체 조건은 사건 좌표가 바뀌어도 동일 개체 인정");
                Check(Matches(area, from) && !Matches(area, to), "영역 조건은 단계 시작 좌표가 아니라 사건 위치로 판정");
                bool missingRejected = false;
                try { Bind("{\"kind\":4,\"binding\":\"없는 생성 결과\"}"); }
                catch (TargetInvocationException error) when (error.InnerException is InvalidOperationException) { missingRejected = true; }
                Check(missingRejected, "없는 생성 결과 연결은 다른 개체로 대체하지 않고 오류");
            }
            finally { UnityEngine.Object.DestroyImmediate(level); }
        }

        private static void VerifyLargeTarget()
        {
            object Invoke(string method, params object[] args) => typeof(FixedObstacleVerification)
                .GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
            LevelDefinition level = (LevelDefinition)Invoke("Make");
            try
            {
                BoardCoordinate at = new BoardCoordinate(4, 4);
                Invoke("Obstacle", level, ObstacleKind.Appliance, 9, at, RabbitColor.Type1);
                Invoke("SetMission", level, ObstacleKind.Appliance);
                LevelRuntimeState state = (LevelRuntimeState)Invoke("Build", level, 12345);
                var entities = TutorialTargetQuery.Capture(state);
                TutorialTargetEntity body = entities.Single(value => value.Cells.Count == 4);
                Check(body.Durability == 9, "2×2 네 칸을 내구도9 본체 하나로 조회");
                TutorialStepDefinition highlighted = TutorialSampleCatalog.CreateSwap(new BoardCoordinate(0, 0), new BoardCoordinate(0, 1), false);
                highlighted.conditions = new List<TutorialConditionDefinition> { new TutorialConditionDefinition { kind = TutorialConditionKind.RemainingDurability,
                    target = new TutorialTargetDefinition { kind = TutorialTargetKind.Entity, coordinate = at }, requiredCount = 1 } };
                using (TutorialProgress progress = new TutorialProgress(new LevelTutorialDefinition { steps = new List<TutorialStepDefinition> { highlighted } }, readTargets: () => entities))
                    Check(body.Cells.All(progress.Snapshot.Highlights.Contains), "2×2 조건 개체의 모든 점유 칸을 자동 강조");
                TutorialTargetSelection selection = TutorialTargetSelection.Bind(new TutorialTargetDefinition
                { kind = TutorialTargetKind.Area, cells = body.Cells.ToList() }, entities, new Dictionary<string, long>());
                Check(selection.InitialOccurrences.SequenceEqual(new[] { body.Occurrence }), "각 대상마다 목록에 2×2 본체 식별자를 한 번만 고정");
                var binding = new Dictionary<string, long> { { "대상", body.Occurrence } };
                TutorialTargetSelection generated = TutorialTargetSelection.Bind(new TutorialTargetDefinition
                { kind = TutorialTargetKind.Generated, binding = "대상" }, entities, binding);
                Check(generated.Matches(body, at), "생성 연결은 저장된 동일 개체 식별자 사용");
                bool lost = false;
                try { TutorialTargetSelection.Bind(new TutorialTargetDefinition { kind = TutorialTargetKind.Generated, binding = "대상" },
                    entities.Where(value => value.Occurrence != body.Occurrence).ToArray(), binding); }
                catch (InvalidOperationException) { lost = true; }
                Check(lost, "연결 이름이 있어도 이미 소실한 대상이면 오류");
            }
            finally { UnityEngine.Object.DestroyImmediate(level); }
        }
    }
}


