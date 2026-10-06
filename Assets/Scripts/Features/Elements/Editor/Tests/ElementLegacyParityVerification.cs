using System;
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

namespace Elements.Editor
{
    /// <summary>구형 배치 전체를 선택 변환한 후 실제 실행 값·공급·난수 원문을 비교한다.</summary>
    public static class ElementLegacyParityVerification
    {
        private static readonly List<string> Results = new List<string>();
        private static readonly List<string> States = new List<string>();
        private static readonly List<LevelDefinition> Loaded = new List<LevelDefinition>();
        private static bool packed;
        private static BoardCoordinate C(int row, int column) => new BoardCoordinate(row, column);
        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); Results.Add("PASS " + message); }
        private static string Snapshot(object value) => (string)typeof(LevelInitialStateVerification)
            .GetMethod("Snapshot", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new[] { value });
        private static string State(LevelRuntimeState state) => string.Join("|", typeof(LevelRuntimeState).GetProperties()
            .Where(property => property.GetIndexParameters().Length == 0 && property.Name != "SchemaVersion" && property.Name != "DefinitionFingerprint")
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .Select(property => property.Name + "=" + Snapshot(property.GetValue(state))));
        private static void Equal(LevelRuntimeState old, LevelRuntimeState converted, string label)
        {
            string before = State(old), after = State(converted);
            States.Add(label + " v4=" + before); States.Add(label + " v5=" + after);
            Check(before == after, label + "의 전체 공개 실행 값 동일(스키마/원본 지문만 제외)");
            Check(old.Random.DrawCount == converted.Random.DrawCount, label + " 난수 소비 동일");
            MethodInfo copy = typeof(SimulationRandom).GetMethod("Copy", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo next = typeof(SimulationRandom).GetMethod("Next", BindingFlags.Instance | BindingFlags.NonPublic);
            object oldRandom = copy.Invoke(old.Random, null), newRandom = copy.Invoke(converted.Random, null);
            Check(new[] { 2, 5, 123, 4, 1000 }.All(bound => (int)next.Invoke(oldRandom, new object[] { bound }) ==
                (int)next.Invoke(newRandom, new object[] { bound })), label + " 후속 난수 값 동일(사본 조회)");
        }
        private static LevelStateBuildResult BuildInput(LevelDefinition level, int seed)
        {
            LevelStateBuildResult asset = LevelStateBuilder.Build(level, seed);
            if (!packed) return asset;
            byte[] bytes = LevelPackCodec.Snapshot(level);
            LevelDefinition copy = LevelPackCodec.ReadLevel(bytes, level.LevelNumber); Loaded.Add(copy);
            LevelStateBuildResult result = LevelStateBuilder.Build(copy, seed);
            Check(asset.IsBuilt && result.IsBuilt, "Asset/팩" + (level.SchemaVersion == 4 ? "1" : "2") + " 실제 구성 성공 " + string.Join(";", result.Issues));
            Equal(asset.State, result.State, "Asset/팩" + (level.SchemaVersion == 4 ? "1" : "2") + " 구성");
            Check(asset.State.DefinitionFingerprint == result.State.DefinitionFingerprint, "동일 버전 Asset/팩의 원본 지문 동일");
            return result;
        }

        public static void Run() => RunInternal(false);
        public static void RunPack() => RunInternal(true);
        private static void RunInternal(bool usePack)
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 Editor에서만 실행한다.");
            Results.Clear(); States.Clear(); Loaded.Clear(); packed = usePack; int exit = 0; LevelDefinition level = null;
            try
            {
                level = (LevelDefinition)typeof(PowerEffectVerification).GetMethod("Make", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                HashSet<BoardCoordinate> occupied = new HashSet<BoardCoordinate>();
                List<string> bodies = new List<string>();
                int[] rows = { 0, 0, 0, 0, 3, 3 }, columns = { 0, 2, 4, 6, 0, 3 };
                for (int kind = 0; kind < 6; kind++)
                {
                    foreach (BoardCoordinate cell in LevelPlacementRules.Footprint(C(rows[kind], columns[kind]), LevelPlacementRules.Size((ObstacleKind)kind))) occupied.Add(cell);
                    bodies.Add("{\"id\":\"body-" + kind + "\",\"coordinate\":{\"row\":" + rows[kind] + ",\"column\":" + columns[kind] +
                        "},\"kind\":" + kind + ",\"durability\":1,\"color\":0,\"requiredCharge\":3}");
                }
                List<string> blocks = new List<string>();
                for (int row = 0; row < 9; row++) for (int column = 0; column < 9; column++)
                {
                    if (occupied.Contains(C(row, column)) || row == 8 && column == 7) continue;
                    int kind = row == 7 && column < 4 ? column + 2 : 1;
                    blocks.Add("{\"coordinate\":{\"row\":" + row + ",\"column\":" + column + "},\"kind\":" + kind +
                        ",\"fixedColor\":" + (row * 2 + column) % 5 + ",\"rocketDirection\":1}");
                }
                List<string> items = new List<string>();
                foreach (SupplyKind kind in Enum.GetValues(typeof(SupplyKind))) items.Add(JsonUtility.ToJson(new SupplyItem(kind, 1)));
                JsonUtility.FromJsonOverwrite("{\"initialBlocks\":[" + string.Join(",", blocks) + "],\"obstacles\":[" + string.Join(",", bodies) +
                    "],\"covers\":[{\"coordinate\":{\"row\":6,\"column\":0},\"kind\":0,\"durability\":2},{\"coordinate\":{\"row\":6,\"column\":2},\"kind\":1,\"durability\":1}]," +
                    "\"dust\":[{\"coordinate\":{\"row\":6,\"column\":4},\"durability\":2}],\"recoveryParts\":[{\"row\":8,\"column\":7}]," +
                    "\"flow\":{\"arrivals\":[{\"row\":8,\"column\":8}]}," +
                    "\"connections\":[{\"generatorId\":\"body-5\",\"targetId\":\"body-0\",\"vertices\":[{\"row\":3,\"column\":3},{\"row\":2,\"column\":3},{\"row\":1,\"column\":3},{\"row\":0,\"column\":3},{\"row\":0,\"column\":2},{\"row\":0,\"column\":1},{\"row\":0,\"column\":0}]}]," +
                    "\"supply\":{\"sources\":[{\"coordinate\":{\"row\":0,\"column\":8},\"mode\":1,\"exhaustion\":0,\"items\":[" + string.Join(",", items) +
                    "]}]},\"missions\":[{\"kind\":0,\"color\":0,\"count\":100},{\"kind\":9,\"count\":1}]}", level);
                string original = JsonUtility.ToJson(level);
                LevelStateBuildResult oldBuild = BuildInput(level, 61392);
                Check(oldBuild.IsBuilt, "구형 전체 종류·층·연결·고정 공급 입력 유효 " + string.Join(";", oldBuild.Issues));
                Check(oldBuild.State.Cells.Where(cell => cell.Content == RuntimeContent.Normal).Select(cell => cell.Color).Distinct().Count() == 5,
                    "구형 비교 입력은 실제 다섯 색 포함");
                ElementPlacementDefinition[] preview = LevelElementMigration.Preview(level);
                ElementLevelSupplyDefinition supplyPreview = LevelElementMigration.PreviewSupply(level);
                Check(JsonUtility.ToJson(level) == original, "전체 변환 미리보기 원본 불변");
                LevelElementMigration.Apply(level);
                string selectedSource = JsonUtility.ToJson(level);
                Check(level.ElementSupply.sources[0].items.Count == 9 &&
                    level.ElementSupply.sources[0].items.Select(item => item.definitionId)
                        .SequenceEqual(supplyPreview.sources[0].items.Select(item => item.definitionId)), "선택 적용은 고정 공급9종의 정의 ID도 복사");
                Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
                Check(JsonUtility.ToJson(level) == original, "공급 선택 변환 Undo 전체 원문 복원");
                Undo.PerformRedo(); Check(JsonUtility.ToJson(level) == selectedSource, "공급 선택 변환 Redo 전체 원문 복원");
                Undo.PerformUndo(); Undo.ClearUndo(level);
                JsonUtility.FromJsonOverwrite("{\"schemaVersion\":5,\"elementSupply\":" + JsonUtility.ToJson(supplyPreview) +
                    ",\"elements\":[" + string.Join(",", preview.Select(JsonUtility.ToJson)) + "]}", level);
                LevelStateBuildResult convertedBuild = BuildInput(level, 61392);
                string convertedSource = JsonUtility.ToJson(level);
                Check(convertedBuild.IsBuilt, "구형 전체 종류의 ID 구성 유효 " + string.Join(";", convertedBuild.Issues));
                Check(oldBuild.State.SchemaVersion == 4 && convertedBuild.State.SchemaVersion == 5, "스키마 변경은 메타데이터로 별도 확인");
                Equal(oldBuild.State, convertedBuild.State, "초기 구성");
                LevelRuntimeState old = oldBuild.State, converted = convertedBuild.State;
                for (int step = 0; step < items.Count; step++)
                {
                    typeof(RuntimeCell).GetProperty("Content").SetValue(old.CellAt(C(0, 8)), RuntimeContent.Empty);
                    typeof(RuntimeCell).GetProperty("Content").SetValue(converted.CellAt(C(0, 8)), RuntimeContent.Empty);
                    SettlementResult before = SettlementResolution.Resolve(old), after = SettlementResolution.Resolve(converted);
                    Check(before.IsApplied && after.IsApplied, "고정 공급 " + step + " 실행 성공");
                    Check(Snapshot(before.Records) == Snapshot(after.Records), "고정 공급 " + step + " 기록 원문 동일");
                    old = before.State; converted = after.State; Equal(old, converted, "고정 공급 " + step);
                }
                Check(JsonUtility.ToJson(level) == convertedSource, "공급 실행 중 제작 입력 불변");
                foreach (SupplyMode mode in new[] { SupplyMode.MaintainScrap, SupplyMode.MaintainRecovery })
                {
                    JsonUtility.FromJsonOverwrite(original, level);
                    string settings = mode == SupplyMode.MaintainScrap
                        ? "\"scrapTarget\":1,\"scrapLimit\":3,\"scrapDurability\":2,\"recoveryTarget\":0"
                        : "\"scrapTarget\":0,\"scrapLimit\":0,\"scrapDurability\":1,\"recoveryTarget\":1";
                    int mission = mode == SupplyMode.MaintainScrap ? (int)MissionKind.Scrap : (int)MissionKind.Recovery;
                    JsonUtility.FromJsonOverwrite("{\"recoveryParts\":[],\"supply\":{\"sources\":[{\"coordinate\":{\"row\":0,\"column\":8},\"mode\":" +
                        (int)mode + ",\"exhaustion\":0,\"items\":[]}]," + settings + "},\"missions\":[{\"kind\":0,\"color\":0,\"count\":100},{\"kind\":" + mission + ",\"count\":3}]}", level);
                    oldBuild = BuildInput(level, 61392);
                    preview = LevelElementMigration.Preview(level);
                    supplyPreview = LevelElementMigration.PreviewSupply(level);
                    JsonUtility.FromJsonOverwrite("{\"schemaVersion\":5,\"elementSupply\":" + JsonUtility.ToJson(supplyPreview) +
                        ",\"elements\":[" + string.Join(",", preview.Select(JsonUtility.ToJson)) + "]}", level);
                    convertedBuild = BuildInput(level, 61392);
                    Check(oldBuild.IsBuilt && convertedBuild.IsBuilt, mode + " 구형/신형 구성 유효 " + string.Join(";", oldBuild.Issues.Concat(convertedBuild.Issues)));
                    old = oldBuild.State; converted = convertedBuild.State;
                    if (mode == SupplyMode.MaintainScrap)
                    {
                        typeof(RuntimeObstacle).GetProperty("Durability").SetValue(old.Obstacles[1], 0);
                        typeof(RuntimeObstacle).GetProperty("Durability").SetValue(converted.Obstacles[1], 0);
                        typeof(RuntimeCell).GetProperty("Content").SetValue(old.CellAt(C(0, 2)), RuntimeContent.Empty);
                        typeof(RuntimeCell).GetProperty("Content").SetValue(converted.CellAt(C(0, 2)), RuntimeContent.Empty);
                    }
                    for (int step = 0; step < 3; step++)
                    {
                        foreach (LevelRuntimeState work in new[] { old, converted })
                        {
                            RuntimeCell cell = work.CellAt(C(0, 8));
                            if (cell.Content == RuntimeContent.Obstacle)
                                typeof(RuntimeObstacle).GetProperty("Durability").SetValue(work.Obstacles[cell.ObstacleIndex.Value], 0);
                            typeof(RuntimeCell).GetProperty("Content").SetValue(cell, RuntimeContent.Empty);
                        }
                        SettlementResult before = SettlementResolution.Resolve(old), after = SettlementResolution.Resolve(converted);
                        Check(before.IsApplied && after.IsApplied && Snapshot(before.Records) == Snapshot(after.Records), mode + " " + step + " 생성 기록 동일");
                        old = before.State; converted = after.State; Equal(old, converted, mode + " " + step);
                    }
                }
            }
            catch (Exception error) { Results.Add("FAIL " + error); Debug.LogException(error); exit = 1; }
            finally
            {
                if (level != null) UnityEngine.Object.DestroyImmediate(level);
                foreach (LevelDefinition copy in Loaded) UnityEngine.Object.DestroyImmediate(copy);
                string prefix = packed ? "pack-legacy-parity" : "legacy-parity";
                File.WriteAllLines("Logs/ElementFramework/Phase03/" + prefix + "-results.txt", Results);
                File.WriteAllLines("Logs/ElementFramework/Phase03/" + prefix + "-states.txt", States);
            }
            EditorApplication.Exit(exit);
        }
    }
}
