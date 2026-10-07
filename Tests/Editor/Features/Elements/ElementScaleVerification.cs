using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Board;
using Cysharp.Threading.Tasks;
using Levels;
using Levels.Editor;
using Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Elements.Editor
{
    /// <summary>제작 입력을 통해 기존 행동의 콘텐츠 확장과 실제 실행 경계를 검사한다.</summary>
    [InitializeOnLoad]
    public static partial class ElementScaleVerification
    {
        private static readonly List<string> Results = new List<string>();
        private static readonly List<UnityEngine.Object> Owned = new List<UnityEngine.Object>();
        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); Results.Add("PASS " + message); }

        private const string BoardsKey = "ElementFramework.Phase05.ContentBoards";
        static ElementScaleVerification()
        {
            EditorApplication.playModeStateChanged += change =>
            {
                if (change != PlayModeStateChange.EnteredPlayMode) return;
                if (SessionState.GetBool(ResourcesKey, false))
                { SessionState.EraseBool(ResourcesKey); RunResourcesAsync().Forget(Debug.LogException); return; }
                if (!SessionState.GetBool(BoardsKey, false)) return;
                SessionState.EraseBool(BoardsKey);
                RunContentAsync(true).Forget(Debug.LogException);
            };
        }
        public static void RunContent() => RunContentAsync(false).Forget(Debug.LogException);
        public static void RunBoards()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 Editor에서만 실행한다.");
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("미저장 씬 보존");
            SessionState.SetBool(BoardsKey, true);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }
        private static async UniTask RunContentAsync(bool nativeBoards)
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 Editor에서만 실행한다.");
            Results.Clear(); Owned.Clear(); int exit = 0;
            try
            {
                string[] ids = { "phase05.obstacle.rod-box", "phase05.cover.mesh", "phase05.power.rocket" };
                ElementDefinition[] templates = {
                    LegacyElementDefinitions.Get(ObstacleKind.Appliance),
                    LegacyElementDefinitions.Get(CoverKind.Web),
                    LegacyElementDefinitions.GetSupply(SupplyKind.Rocket) };
                ElementCatalogAsset source = ScriptableObject.CreateInstance<ElementCatalogAsset>(); Owned.Add(source);
                ElementVisualCatalogAsset visualSource = ScriptableObject.CreateInstance<ElementVisualCatalogAsset>(); Owned.Add(visualSource);
                SerializedObject catalogInput = new SerializedObject(source);
                SerializedProperty entries = catalogInput.FindProperty("definitions"); entries.arraySize = ids.Length;
                ElementVisualCatalogDto builtin = LegacyElementVisuals.Catalog.ToDto();
                ElementVisualBindingDto[] aliases = new ElementVisualBindingDto[ids.Length];
                for (int i = 0; i < ids.Length; i++)
                {
                    PackedElementDefinition packed = PackedElementDefinition.FromDefinition(templates[i]);
                    packed.id = ids[i]; packed.displayName = "확장 검증 " + i;
                    ElementDefinitionAsset authored = ScriptableObject.CreateInstance<ElementDefinitionAsset>(); Owned.Add(authored);
                    JsonUtility.FromJsonOverwrite("{\"definition\":" + JsonUtility.ToJson(packed) + "}", authored);
                    entries.GetArrayElementAtIndex(i).objectReferenceValue = authored;
                    aliases[i] = new ElementVisualBindingDto { id = ids[i],
                        visualKey = builtin.bindings.Single(binding => binding.id == templates[i].Id.Value).visualKey };
                    Check(authored.ToDefinition().Id.Value == ids[i], "기존 행동의 별도 제작 ID " + ids[i]);
                }
                JsonUtility.FromJsonOverwrite("{\"catalog\":" + JsonUtility.ToJson(new ElementVisualCatalogDto {
                    definitions = Array.Empty<ElementVisualDefinitionDto>(), bindings = aliases }) + "}", visualSource);
                catalogInput.FindProperty("visuals").objectReferenceValue = visualSource;
                catalogInput.ApplyModifiedPropertiesWithoutUndo();
                ElementCatalog catalog = source.CreateCatalog();
                ElementVisualCatalog visuals = source.CreateVisualCatalog();
                Check(catalog.Count == 3 && catalog.Get(new ElementId(ids[0])).RequirePlacement().Size == 2,
                    "제작 카탈로그3개와2×2 본체 프로필");

                LevelDefinition level = (LevelDefinition)typeof(PowerEffectVerification).GetMethod("Make", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                Owned.Add(level);
                SerializedObject levelInput = new SerializedObject(level);
                levelInput.FindProperty("elementCatalog").objectReferenceValue = source; levelInput.ApplyModifiedPropertiesWithoutUndo();
                LevelElementMigration.Apply(level);
                BoardCoordinate[] positions = { new BoardCoordinate(3, 3), new BoardCoordinate(4, 7), new BoardCoordinate(4, 0) };
                Type editing = typeof(ElementScaleVerification).Assembly.GetType("Elements.Editor.ElementPlacementEditing");
                for (int i = 0; i < ids.Length; i++)
                {
                    PlacementBrush brush = (PlacementBrush)editing.GetMethod("ForDefinition").Invoke(null, new object[] { level, new ElementId(ids[i]) });
                    brush.ReplaceExisting = true; brush.Durability = i == 0 ? 2 : 1; brush.Direction = RocketDirection.Horizontal;
                    Check(LevelObstacleEditing.Apply(level, brush, new[] { positions[i] }).Changed == 1,
                        "실제 편집 경계의 신규 정의 배치 " + ids[i]);
                }
                JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":" + (int)MissionKind.Appliance +
                    ",\"count\":1},{\"kind\":" + (int)MissionKind.Web + ",\"count\":1}]}", level);
                JsonUtility.FromJsonOverwrite("{\"elementSupply\":{\"sources\":[{\"coordinate\":{\"row\":0,\"column\":0}," +
                    "\"mode\":1,\"exhaustion\":0,\"items\":[{\"definitionId\":\"" + ids[2] + "\",\"count\":1}]}]}}", level);
                string original = JsonUtility.ToJson(level), fingerprint = LevelStateBuilder.Fingerprint(level);
                LevelStateBuildResult built = LevelStateBuilder.Build(level, 12345);
                Check(built.IsBuilt, "제작3종/공급 입력 실제 구성 " + string.Join(";", built.Issues));
                Check(built.State.Cells.Count(cell => cell.ObstacleIndex == 0) == 4 && built.State.Obstacles[0].Element.Id.Value == ids[0],
                    "2×2 실제 점유와 신규 본체 ID");
                Check(built.State.CellAt(positions[1]).Cover == CoverKind.Web && built.State.CellAt(positions[2]).Content == RuntimeContent.Rocket,
                    "덮개와 파워는 기존 행동으로 배치");
                ElementVisualLookup lookup = new ElementVisualLookup(visuals);
                Check(lookup.Obstacle(built.State.Obstacles[0]).Path.Contains("MetalRodBox") &&
                    lookup.Cover(built.State.CellAt(positions[1])).Path.Contains("Web") &&
                    lookup.Content(built.State.CellAt(positions[2])).Path.Contains("cleaning-rocket"), "세 제작 ID의 명시적 공유 아트 조회");

                byte[] bytes = LevelPackCodec.Snapshot(level);
                LevelDefinition restored = LevelPackCodec.ReadLevel(bytes, level.LevelNumber); Owned.Add(restored);
                LevelStateBuildResult roundTrip = LevelStateBuilder.Build(restored, 12345);
                Check(roundTrip.IsBuilt && ids.All(id => restored.Elements.Any(item => item.definitionId == id)) &&
                    restored.ElementSupply.sources.Single().items.Single().definitionId == ids[2], "실제 팩2 메모리 왕복에서 제작/공급 ID 유지");
                Check(LevelPackCodec.Snapshot(restored).SequenceEqual(bytes), "메모리 팩 전체 바이트 왕복 동일");
                BoardActionExecutor executor = new BoardActionExecutor(built.State);
                BoardActionResult action = executor.Activate(positions[2]);
                Check(action.IsApplied, "신규 파워 ID 실제 발동 " + action.Message);
                Check(executor.State.Obstacles[0].Durability == 0 && !executor.State.CellAt(positions[1]).Cover.HasValue,
                    "로켓 범위의2×2 두 칸 피해와 덮개 제거");
                Check(executor.State.Missions.All(mission => mission.Progress == 1), "신규 본체/층 제거의 실제 미션 완료");
                BoardActionExecutor restoredExecutor = new BoardActionExecutor(roundTrip.State);
                restoredExecutor.Activate(positions[2]);
                Check(executor.State.Random.DrawCount == restoredExecutor.State.Random.DrawCount &&
                    executor.State.Obstacles[0].Durability == restoredExecutor.State.Obstacles[0].Durability &&
                    executor.State.Missions.Select(mission => mission.Progress).SequenceEqual(restoredExecutor.State.Missions.Select(mission => mission.Progress)),
                    "Asset/메모리 입력 실행의 피해/미션/난수 동등");
                LevelRuntimeState supplyState = LevelStateBuilder.Build(level, 12345).State;
                typeof(RuntimeCell).GetProperty("Content").SetValue(supplyState.CellAt(new BoardCoordinate(0, 0)), RuntimeContent.Empty);
                SettlementResult supplied = SettlementResolution.Resolve(supplyState);
                PropertyInfo selected = typeof(SettlementRecord).GetProperty("ContentElement", BindingFlags.NonPublic | BindingFlags.Instance);
                Check(supplied.IsApplied && supplied.Records.Any(record => ((ElementDefinition)selected.GetValue(record))?.Id.Value == ids[2]),
                    "실제 고정 공급/낙하 기록의 신규 파워 ID 유지");
                Check(JsonUtility.ToJson(level) == original && LevelStateBuilder.Fingerprint(level) == fingerprint,
                    "편집 이후 실행/조회/공급은 제작 원본과 지문 무변경");
                if (nativeBoards) await VerifyBoards(level, visuals, built.State);
                Results.Add("INFO 신규 ID3개: 기존 제작/표현 데이터 사용, 생산 C# 변경0.");
            }
            catch (Exception error) { Results.Add("FAIL " + error); Debug.LogException(error); exit = 1; }
            finally
            {
                foreach (UnityEngine.Object owned in Owned) if (owned != null) UnityEngine.Object.DestroyImmediate(owned);
                Owned.Clear(); Directory.CreateDirectory("Logs/ElementFramework/Phase05");
                if (nativeBoards) LevelBoardArtwork.ReleaseAll();
                File.WriteAllLines("Logs/ElementFramework/Phase05/" + (nativeBoards ? "content-boards-results.txt" : "content-results.txt"), Results);
            }
            EditorApplication.Exit(exit);
        }
    }
}
