using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Board;
using Elements;
using Elements.Editor;
using Levels;
using Levels.Editor;
using Simulation;
using Tutorial;
using UnityEditor;
using UnityEngine;

public static partial class TutorialDroneLevelVerification
{
    private const string LevelPath = "Assets/Data/Levels/Level_04.asset";
    private const string Output = "Logs/Tutorial/Stage07";
    private static readonly List<string> Results = new List<string>();
    private static readonly BoardCoordinate Donor = new BoardCoordinate(2, 4);
    private static readonly BoardCoordinate Spawn = new BoardCoordinate(3, 4);
    private static readonly BoardCoordinate Settled = new BoardCoordinate(4, 4);
    private static readonly BoardCoordinate Neighbor = new BoardCoordinate(4, 5);
    [Serializable] private sealed class Placements { public List<ElementPlacementDefinition> elements; }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        Results.Add("PASS " + message);
    }
    public static void Preview() => Author(false);
    public static void Apply() => Author(true);
    public static void Run()
    {
        Directory.CreateDirectory(Output); Results.Clear();
        try
        {
            LevelDefinition level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(LevelPath);
            Check(level != null, "4레벨 출시 에셋 없음");
            VerifyData(level);
            VerifyPack(level);
            File.WriteAllLines(Output + "/run-results.txt", Results);
            StartEditorVerification();
        }
        catch (Exception error) { Results.Add("FAIL " + error); File.WriteAllLines(Output + "/run-results.txt", Results); Debug.LogException(error); EditorApplication.Exit(1); }
    }

    private static void VerifyPack(LevelDefinition level)
    {
        byte[] current = File.ReadAllBytes(LevelPackBuild.FilePath(1));
        byte[] previous = File.ReadAllBytes(Output + "/baseline-pack.bytes");
        foreach (int number in LevelPackCodec.DecodeTutorial(previous).Tutorials.Select(item => item.LevelNumber))
        {
            LevelDefinition before = LevelPackCodec.ReadLevel(previous, number), after = LevelPackCodec.ReadLevel(current, number);
            try { Check(LevelPackCodec.Snapshot(before).SequenceEqual(LevelPackCodec.Snapshot(after)), "기존 구간 레벨 논리 보존 " + number); }
            finally { UnityEngine.Object.DestroyImmediate(before); UnityEngine.Object.DestroyImmediate(after); }
        }
        LevelDefinition packed = LevelPackCodec.ReadLevel(current, 4);
        try { Check(LevelPackCodec.Snapshot(level).SequenceEqual(LevelPackCodec.Snapshot(packed)), "4레벨 Asset/팩 스냅샷 동등"); VerifyData(packed); }
        finally { UnityEngine.Object.DestroyImmediate(packed); }
    }

    private static LevelDefinition Candidate()
    {
        LevelDefinition level = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<LevelDefinition>("Assets/Data/Levels/Level_03.asset"));
        level.hideFlags = HideFlags.HideAndDontSave;
        level.Tutorial.steps.Clear(); level.Tutorial.supply.sources.Clear(); level.Tutorial.seed = 12345;
        List<ElementPlacementDefinition> cells = new List<ElementPlacementDefinition>();
        for (int row = 0; row < 9; row++) for (int column = 0; column < 9; column++)
            cells.Add(new ElementPlacementDefinition { definitionId = "supply.normal.fixed", layer = PlacementLayer.Block,
                coordinate = new BoardCoordinate(row, column), hasColor = true, color = level.Colors[(row * 2 + column) % level.Colors.Count] });
        foreach (BoardCoordinate at in new[] { new BoardCoordinate(3, 3), new BoardCoordinate(4, 3), new BoardCoordinate(4, 4) })
            cells.Single(cell => cell.coordinate.Equals(at)).color = level.Colors[0];
        cells.Single(cell => cell.coordinate.Equals(Spawn)).color = level.Colors[1];
        cells.Single(cell => cell.coordinate.Equals(Donor)).color = level.Colors[0];
        ElementLevelSupplyDefinition supply = level.ElementSupply;
        JsonUtility.FromJsonOverwrite("{\"schemaVersion\":5,\"levelNumber\":4,\"initialBlocks\":[],\"supply\":{\"sources\":[]},\"elementSupply\":" + JsonUtility.ToJson(supply) + "}", level);
        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(new Placements { elements = cells }), level);
        using (SerializedObject data = new SerializedObject(level))
        {
            data.FindProperty("elementCatalog").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ElementCatalogAsset>(ElementContentAuthoring.CatalogPath);
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        level.Tutorial.steps.Add(new TutorialStepDefinition { instructions = "같은 색 4개를 2×2로 맞추면 수거 드론이 생겨요.", highlights = new List<BoardCoordinate> { Donor, Spawn } });
        level.Tutorial.steps.Add(new TutorialStepDefinition { kind = TutorialStepKind.Swap, instructions = "표시된 두 칸을 바꿔 2×2로 수거 드론을 만들어 보세요.", hasFirst = true, first = Donor, hasSecond = true, second = Spawn,
            results = new List<TutorialResultDefinition> { new TutorialResultDefinition { kind = TutorialResultKind.Generated, definitionId = "power.drone", hasCoordinate = true, coordinate = Spawn } } });
        level.Tutorial.steps.Add(new TutorialStepDefinition { instructions = "수거 드론이 생겨 아래로 내려왔어요. 옆 블록과 바꾸면 가까운 칸을 정리하고 다른 목표로 날아가요.", highlights = new List<BoardCoordinate> { Settled, Neighbor } });
        level.Tutorial.steps.Add(new TutorialStepDefinition { kind = TutorialStepKind.PowerSwap, instructions = "수거 드론을 표시된 옆 블록과 바꿔 보세요.", actionDefinitionId = "power.drone", hasFirst = true, first = Settled, hasSecond = true, second = Neighbor,
            results = new List<TutorialResultDefinition> { new TutorialResultDefinition { kind = TutorialResultKind.Activated, definitionId = "power.drone", hasCoordinate = true, coordinate = Neighbor } } });
        level.Tutorial.steps.Add(new TutorialStepDefinition { instructions = "잘했어요! 이제 남은 달토끼를 모아 보세요." });
        StartingBoardSearch starting = StartingBoardBuilder.Build(level, level.Tutorial.seed);
        LevelRuntimeState state = starting.State;
        if (state == null)
        {
            LevelRuntimeState diagnostic = LevelStateBuilder.Build(level, level.Tutorial.seed).State;
            string details = diagnostic == null ? "" : new StartConditionReport(diagnostic).Message + " · 지정 교환 " + ActionQuery.Swap(diagnostic, Donor, Spawn).Message +
                " · 완성 매칭 " + string.Join(" / ", MatchQuery.Find(diagnostic).Select(match => match.Key));
            UnityEngine.Object.DestroyImmediate(level);
            throw new InvalidOperationException("후보 시작 보드 구성 실패: " + starting.Message + " · " + string.Join(" · ", starting.Issues) + " · " + details);
        }
        foreach (RuntimeSource source in state.Supply.Sources)
            level.Tutorial.supply.sources.Add(new ElementSupplySourceDefinition { coordinate = source.Coordinate, mode = SupplyMode.Fixed, exhaustion = SupplyExhaustion.Stop,
                items = Enumerable.Range(0, 64).Select(index => new ElementSupplyItemDefinition { definitionId = "supply.normal.fixed", color = state.Colors[(index * 2 + source.Coordinate.Column) % state.Colors.Count] }).ToList() });
        return level;
    }

    private static void Author(bool apply)
    {
        Directory.CreateDirectory(Output); Results.Clear(); LevelDefinition candidate = null;
        try
        {
            candidate = Candidate(); VerifyData(candidate);
            using (TutorialBoardAdapter adapter = Replay(candidate, true))
                foreach (RuntimeSource source in adapter.Executor.State.Supply.Sources)
                {
                    int consumed = source.Items.Take(source.ItemIndex).Sum(item => item.Count) + source.ItemConsumed;
                    candidate.Tutorial.supply.sources.Single(item => item.coordinate.Equals(source.Coordinate)).items =
                        candidate.Tutorial.supply.sources.Single(item => item.coordinate.Equals(source.Coordinate)).items.Take(Math.Max(1, consumed + 2)).ToList();
                    Results.Add("PASS 공급 소비 " + source.Coordinate + " = " + consumed);
                }
            VerifyData(candidate);
            if (apply)
            {
                if (File.Exists(LevelPath) || File.Exists(LevelPath + ".meta")) throw new InvalidOperationException("기존 4레벨/메타를 덮어쓰지 않습니다.");
                LevelDefinition[] interval = AssetDatabase.FindAssets("t:LevelDefinition", new[] { LevelAssetOperations.DefaultFolder }).Select(AssetDatabase.GUIDToAssetPath)
                    .Select(AssetDatabase.LoadAssetAtPath<LevelDefinition>).Where(level => LevelPackCodec.FirstLevel(level.LevelNumber) == 1).ToArray();
                if (interval.Any(level => level.LevelNumber == 4)) throw new InvalidOperationException("다른 경로의 기존 4레벨을 보존합니다.");
                Dictionary<string, byte[]> outputs = LevelPackBuild.CreatePackBytes(interval.Append(candidate));
                Check(outputs.Count == 1, "승인 구간만 인코딩");
                LevelDefinition asset = LevelAssetOperations.CreateElementAtPath(LevelPath);
                EditorUtility.CopySerialized(candidate, asset); asset.hideFlags = HideFlags.None; asset.name = "Level_04";
                EditorUtility.SetDirty(asset); AssetDatabase.SaveAssetIfDirty(asset);
                foreach (KeyValuePair<string, byte[]> output in outputs) { File.WriteAllBytes(output.Key, output.Value); AssetDatabase.ImportAsset(output.Key); }
                Check(AssetDatabase.LoadAssetAtPath<LevelDefinition>(LevelPath).SchemaVersion == 5, "현재 제작 스키마 저장");
            }
            File.WriteAllLines(Output + (apply ? "/apply-results.txt" : "/preview-results.txt"), Results); EditorApplication.Exit(0);
        }
        catch (Exception error) { Results.Add("FAIL " + error); File.WriteAllLines(Output + (apply ? "/apply-results.txt" : "/preview-results.txt"), Results); Debug.LogException(error); EditorApplication.Exit(1); }
        finally { if (candidate != null) UnityEngine.Object.DestroyImmediate(candidate); }
    }
    private static void VerifyData(LevelDefinition level)
    {
        Check(level.LevelNumber == 4 && level.SchemaVersion == 5 && level.Board.Rows == 9 && level.Board.Columns == 9 && level.Elements.Count == 81, "신형 전체 9x9 레벨 4");
        Check(level.Tutorial.steps.Count == 5, "출시 안내 5단계");
        List<LevelValidationIssue> issues = LevelTutorialReplayValidator.Validate(level);
        Check(issues.Count == 0, "실제 재생 오류 0 · " + string.Join(" · ", issues));
        LevelDefinition missingSupply = UnityEngine.Object.Instantiate(level);
        try
        {
            missingSupply.Tutorial.supply.sources.Clear();
            Check(LevelTutorialReplayValidator.Validate(missingSupply).Count > 0, "생성구 누락은 실제 재생에서 거절");
            missingSupply.Tutorial.supply.sources.AddRange(level.Tutorial.supply.sources.Select(source => new ElementSupplySourceDefinition
            { coordinate = source.coordinate, mode = SupplyMode.Random, exhaustion = SupplyExhaustion.Stop, items = source.items }));
            Check(LevelTutorialReplayValidator.Validate(missingSupply).Count > 0, "무작위 대체 공급은 거절");
            missingSupply.Tutorial.supply.sources = level.Tutorial.supply.sources.Select(source => new ElementSupplySourceDefinition
            { coordinate = source.coordinate, mode = SupplyMode.Fixed, exhaustion = SupplyExhaustion.Stop, items = source.items.Take(1).ToList() }).ToList();
            Check(LevelTutorialReplayValidator.Validate(missingSupply).Count > 0, "부족한 고정 공급은 무작위 대체 없이 거절");
        }
        finally { UnityEngine.Object.DestroyImmediate(missingSupply); }
        using TutorialBoardAdapter adapter = Replay(level);
        Check(adapter.Progress.State == TutorialProgressState.Completed && adapter.Executor.State.MovesRemaining == level.MoveCount - 2, "두 교환 이동 차감/전체 진행 완료");
        Check(adapter.Executor.State.Missions.Any(mission => mission.Progress < mission.Target), "남은 미션으로 자유 플레이 유지");
        Check(adapter.Executor.State.Supply.Sources.Count == 9 && adapter.Executor.State.Supply.Sources.All(source => source.Mode == SupplyMode.Random), "종료 후 일반 무작위 공급 9개 복귀");
        for (int index = 0; index < adapter.Executor.State.Missions.Count; index++)
            Check(adapter.Executor.State.Missions[index].Progress == adapter.Executor.State.MissionProgressRecords.Where(record => record.MissionIndex == index).Sum(record => record.Amount),
                "실제 미션 기록만 집계 " + index);
    }
    private static void VerifyDroneEffects(BoardActionResult action, BoardActionExecutor executor)
    {
        PowerAttackRecord direct = action.PowerTrace.Attacks.Single(attack => !attack.IsFlight && attack.Power == RuntimeContent.Drone);
        BoardCoordinate[] plus = { Neighbor, new BoardCoordinate(Neighbor.Row - 1, Neighbor.Column), new BoardCoordinate(Neighbor.Row + 1, Neighbor.Column),
            new BoardCoordinate(Neighbor.Row, Neighbor.Column - 1), new BoardCoordinate(Neighbor.Row, Neighbor.Column + 1) };
        Check(direct.Area == PowerArea.Plus && direct.Targets.OrderBy(cell => cell.Row).ThenBy(cell => cell.Column)
            .SequenceEqual(plus.OrderBy(cell => cell.Row).ThenBy(cell => cell.Column)), "직접 +5칸은 단독 발사 기록으로 분리");
        DroneFlightRecord flight = action.PowerTrace.Flights.Single();
        PowerAttackRecord landing = action.PowerTrace.Attacks.Single(attack => attack.IsFlight);
        Check(flight.LandingTarget.HasValue && landing.Area == PowerArea.Point && landing.Targets.Count == 1 &&
            landing.Targets[0].Equals(flight.LandingTarget.Value) && !plus.Contains(flight.LandingTarget.Value), "추가 비행 표적1개는 직접 +5칸과 분리");
        Check(action.Effects.Any(effect => effect.HitGroup == landing.HitGroup && effect.Target.Equals(flight.LandingTarget.Value) &&
            effect.Response == DamageResponse.Remove), "추가 표적의 실제 착탄 제거 기록");
        Check(executor.TurnEffects.Targeting.Any(record => record.Event == TargetingEvent.Reserved && record.Message.Contains("미션")) &&
            executor.TurnEffects.Targeting.Count(record => record.Event == TargetingEvent.Landed) == 1, "현재 미션 우선 선택과 착탄1회 기록");
        Results.Add("INFO 비행 " + flight.Origin + " → " + flight.InitialTarget + " → " + flight.LandingTarget);
        GameScreen.PuzzleEffectTimeline timeline = new GameScreen.PuzzleEffectTimeline(executor.State, action.Changes, action.Effects, action.PowerTrace);
        GameScreen.PuzzleEffectTimeline.Attack scheduled = timeline.Attacks.Single(attack => attack.Record.IsFlight);
        Check(scheduled.Start > 0 && timeline.Duration >= scheduled.End, "현재 떠오름/호버 후 돌진과 착탄까지 표시 대기");
        Check(timeline.Flights.Single().Phases.Select(phase => phase.Kind).SequenceEqual(new[] { GameScreen.DroneFlightPhaseKind.Rise,
            GameScreen.DroneFlightPhaseKind.Hover, GameScreen.DroneFlightPhaseKind.Dash }), "기존 상승→호버→돌진 재사용·선회 없음");
        Check(timeline.Reactions.Where(reaction => reaction.Record.HitGroup == landing.HitGroup)
            .All(reaction => reaction.Time >= scheduled.ImpactAt(reaction.Record.Target)), "비행 표적 피해는 실제 착탄 이후");
    }
    private static TutorialBoardAdapter Replay(LevelDefinition level, bool retainTutorialSupply = false)
    {
        TutorialBoardAdapter adapter = TutorialBoardAdapter.Prepare(level, StartingBoardBuilder.Build(level, level.Tutorial.seed).State);
        try
        {
            for (int index = 0; index < (retainTutorialSupply ? 4 : 5); index++)
            {
                TutorialProgressSnapshot snapshot = adapter.Progress.Snapshot;
                int movesBefore = adapter.Executor.State.MovesRemaining;
                Check(!adapter.TryBegin(TutorialInput.Swap(new BoardCoordinate(0, 0), new BoardCoordinate(0, 1))) &&
                    !adapter.TryBegin(TutorialInput.Activate(Spawn)) && adapter.Executor.State.MovesRemaining == movesBefore,
                    "임의 교환/제자리 발동 거절·이동 보존 " + index);
                TutorialInput input = snapshot.State == TutorialProgressState.AwaitDescription ? TutorialInput.Next() : TutorialInput.Swap(snapshot.First.Value, snapshot.Second.Value);
                if (index == 1)
                {
                    ActionCandidate query = ActionQuery.Swap(adapter.Executor.State, Donor, Spawn);
                    Check(query.IsAllowed && query.Matches.Count(match => match.Kind == MatchKind.Drone) == 1 &&
                        query.Matches.Single(match => match.Kind == MatchKind.Drone).Cells.SequenceEqual(new[] { new BoardCoordinate(3, 3), Spawn, new BoardCoordinate(4, 3), Settled }),
                        "실제 조회: 2×2 4매칭 하나→수거 드론");
                }
                Check(adapter.TryBegin(input), "실제 단계 승인 " + index);
                if (input.Kind != TutorialInputKind.Next)
                {
                    BoardActionResult action = adapter.Executor.Swap(input.First.Value, input.Second.Value);
                    adapter.ReportAction(action.IsApplied); Check(action.IsApplied, "실제 교환 " + index);
                    if (index == 3) VerifyDroneEffects(action, adapter.Executor);
                    int rounds = 0;
                    while (adapter.Executor.HasPendingCascade && rounds++ < adapter.Executor.CascadeLimit * 2) adapter.ObserveCascade(adapter.Executor.AdvanceCascade());
                    Check(!adapter.Executor.HasPendingCascade, "연쇄 정상 종료 " + index);
                    Check(adapter.Executor.State.MovesRemaining == movesBefore - 1, "해당 교환 이동 1회 차감 " + index);
                    if (index == 1) Check(adapter.Executor.State.CellAt(Settled).Content == RuntimeContent.Drone,
                        "생성된 수거 드론이 낙하 후 지정 위치에 유지");
                    if (index == 1) Check(action.Decisions.Count == 1 && action.Decisions[0].Selected.Kind == MatchKind.Drone && action.Decisions[0].Spawn?.Equals(Spawn) == true &&
                        adapter.Executor.State.Cells.Count(cell => cell.Content == RuntimeContent.Drone) == 1, "실제 생성 결정/보드 수거 드론 정확히 1개");
                    adapter.Tick(false, false);
                    Check(adapter.Progress.StepIndex == index && adapter.Progress.State == TutorialProgressState.AwaitPresentation,
                        "논리 완료 후 표시 완료 전 단계 유지 " + index);
                }
                adapter.Tick(true, false);
                Check(adapter.Progress.StepIndex > index || adapter.Progress.State == TutorialProgressState.Completed, "실제 결과로 다음 단계 " + index);
            }
            return adapter;
        }
        catch { adapter.Dispose(); throw; }
    }
}
