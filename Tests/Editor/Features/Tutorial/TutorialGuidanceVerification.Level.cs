using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Board;
using Levels;
using Levels.Editor;
using Simulation;
using Tutorial;
using UnityEditor;
using UnityEngine;

public static partial class TutorialGuidanceVerification
{
    public static void PreviewFirstLevel() => FirstLevel(false);
    public static void ApplyFirstLevel() => FirstLevel(true);

    private static void FirstLevel(bool apply)
    {
        LevelDefinition owned = null;
        try
        {
            LevelDefinition asset = AssetDatabase.LoadAssetAtPath<LevelDefinition>("Assets/Data/Levels/Level_01.asset");
            string original = JsonUtility.ToJson(asset);
            owned = UnityEngine.Object.Instantiate(asset); owned.Tutorial.steps.Clear(); owned.Tutorial.supply.sources.Clear(); owned.Tutorial.seed = 12345;
            StartingBoardSearch search = StartingBoardBuilder.Build(owned, owned.Tutorial.seed);
            if (search.Status != StartingBoardStatus.Success) throw new Exception(search.Message);
            HashSet<BoardCoordinate> fixedCells = new HashSet<BoardCoordinate>(asset.InitialBlocks.Where(block => block.Kind == InitialBlockKind.FixedNormal).Select(block => block.Coordinate));
            foreach (RuntimeSource source in search.State.Supply.Sources)
                owned.Tutorial.supply.sources.Add(new ElementSupplySourceDefinition { coordinate = source.Coordinate, mode = SupplyMode.Fixed, exhaustion = SupplyExhaustion.Stop,
                    items = Enumerable.Range(0, 64).Select(index => new ElementSupplyItemDefinition { definitionId = "supply.normal.fixed", color = search.State.Colors[(index * 2 + source.Coordinate.Column) % search.State.Colors.Count] }).ToList() });
            ActionCandidate selected = null;
            foreach (BoardCoordinate first in fixedCells.OrderBy(cell => cell.Row).ThenBy(cell => cell.Column))
            {
                foreach (BoardCoordinate second in fixedCells.Where(cell => new BoardEdge(first, cell).IsAdjacent).OrderBy(cell => cell.Row).ThenBy(cell => cell.Column))
                {
                    ActionCandidate action = ActionQuery.Swap(search.State, first, second);
                    if (!action.IsAllowed || action.Matches.Count != 1 || action.Matches[0].Cells.Count != 3 || !action.Matches[0].Cells.All(fixedCells.Contains)) continue;
                    owned.Tutorial.steps.Clear();
                    owned.Tutorial.steps.Add(new TutorialStepDefinition { instructions = "같은 색 토끼 3개를 맞추면 고물을 정리할 수 있어요.", highlights = new List<BoardCoordinate> { first, second } });
                    owned.Tutorial.steps.Add(new TutorialStepDefinition { kind = TutorialStepKind.Swap, instructions = "손가락 방향으로 두 칸을 바꿔 보세요.", hasFirst = true, first = first, hasSecond = true, second = second,
                        results = new List<TutorialResultDefinition> { new TutorialResultDefinition { kind = TutorialResultKind.Removed, definitionId = "supply.normal.fixed", count = 3 } } });
                    owned.Tutorial.steps.Add(new TutorialStepDefinition { instructions = "잘했어요! 남은 이동 안에 미션 토끼를 모두 모아 보세요." });
                    if (LevelTutorialReplayValidator.Validate(owned).Count == 0) { selected = action; break; }
                }
                if (selected != null) break;
            }
            if (selected == null) throw new Exception("기존 고정 배치에서 실제 재생 가능한 기본 3매칭을 찾지 못했습니다.");
            using (TutorialBoardAdapter adapter = TutorialBoardAdapter.Prepare(owned, search.State))
            {
                adapter.TryBegin(TutorialInput.Next()); adapter.Tick(true, false);
                if (!adapter.TryBegin(TutorialInput.Swap(selected.First, selected.Second.Value))) throw new Exception("공급 소비량 검사의 행동 승인 실패");
                adapter.ReportAction(adapter.Executor.Swap(selected.First, selected.Second.Value).IsApplied);
                int rounds = 0;
                while (adapter.Executor.HasPendingCascade && rounds++ < adapter.Executor.CascadeLimit * 2) adapter.ObserveCascade(adapter.Executor.AdvanceCascade());
                adapter.Tick(true, false);
                if (adapter.Progress.StepIndex != 2) throw new Exception("공급 소비량 검사에서 실제 행동 미완료");
                foreach (RuntimeSource source in adapter.Executor.State.Supply.Sources)
                {
                    ElementSupplySourceDefinition written = owned.Tutorial.supply.sources.Single(value => value.coordinate.Equals(source.Coordinate));
                    int consumed = source.Items.Take(source.ItemIndex).Sum(item => item.Count) + source.ItemConsumed;
                    written.items = written.items.Take(Math.Max(1, consumed)).ToList();
                }
            }
            if (LevelTutorialReplayValidator.Validate(owned).Count != 0) throw new Exception("실제 소비량으로 줄인 공급 재생 실패");
            if (JsonUtility.ToJson(asset) != original) throw new Exception("대표 안내 준비 중 원본 변경");
            string output = "시드 " + owned.Tutorial.seed + " · " + selected.First + " → " + selected.Second + " · 실제 고정 3매칭 재생 성공\n";
            File.WriteAllText("Logs/Tutorial/Stage04/first-level-preview.txt", output);
            if (apply)
            {
                Dictionary<string, byte[]> outputs;
                LevelDefinition[] interval = AssetDatabase.FindAssets("t:LevelDefinition", new[] { LevelAssetOperations.DefaultFolder }).Select(AssetDatabase.GUIDToAssetPath)
                    .Select(AssetDatabase.LoadAssetAtPath<LevelDefinition>).Where(level => LevelPackCodec.FirstLevel(level.LevelNumber) == 1).ToArray();
                outputs = LevelPackBuild.CreatePackBytes(interval.Select(level => level.LevelNumber == 1 ? owned : level));
                if (outputs.Count != 1) throw new Exception("1레벨 구간 외 팩 출력");
                asset.Tutorial.seed = owned.Tutorial.seed; asset.Tutorial.steps.Clear(); asset.Tutorial.steps.AddRange(owned.Tutorial.steps);
                asset.Tutorial.supply.sources.Clear(); asset.Tutorial.supply.sources.AddRange(owned.Tutorial.supply.sources);
                EditorUtility.SetDirty(asset); AssetDatabase.SaveAssetIfDirty(asset);
                foreach (KeyValuePair<string, byte[]> pack in outputs) { File.WriteAllBytes(pack.Key, pack.Value); AssetDatabase.ImportAsset(pack.Key); }
                File.AppendAllText("Logs/Tutorial/Stage04/first-level-preview.txt", "기존 구간 갱신 도구의 메모리 인코딩으로 1~50 구간만 갱신. 주소/등록/다른 리소스 변경 없음.\n");
            }
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        finally { if (owned != null) UnityEngine.Object.DestroyImmediate(owned); }
    }
}
