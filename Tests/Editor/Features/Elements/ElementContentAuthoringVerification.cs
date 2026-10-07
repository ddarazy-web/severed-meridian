using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;
using Cysharp.Threading.Tasks;
using Levels;
using Levels.Editor;
using MemoryPack;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.AddressableAssets;
using UnityEngine;

namespace Elements.Editor
{
    /// <summary>제작 원본과 배포 팩의 연결을 별도 배치 Editor에서 검사한다.</summary>
    [InitializeOnLoad]
    public static class ElementContentAuthoringVerification
    {
        private static readonly List<string> Results = new List<string>();
        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); Results.Add("PASS " + message); }
        private const string LoadKey = "ElementContentAuthoring.Load";
        static ElementContentAuthoringVerification()
        {
            EditorApplication.playModeStateChanged += change =>
            {
                if (change != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(LoadKey, false)) return;
                SessionState.EraseBool(LoadKey); LoadAsync().Forget(Debug.LogException);
            };
        }
        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 Editor 전용입니다.");
            Results.Clear(); int exit = 0;
            try
            {
                Type codec = typeof(ElementDefinition).Assembly.GetType("Elements.ElementContentPackCodec");
                Check(codec != null, "콘텐츠 MemoryPack 변환 경로 존재");
                MethodInfo encode = codec.GetMethod("Encode");
                byte[] bytes = (byte[])encode.Invoke(null, new object[] { LegacyElementDefinitions.DefaultCatalog, LegacyElementVisuals.Catalog });
                object decoded = codec.GetMethod("Decode").Invoke(null, new object[] { bytes });
                var catalog = (ElementCatalog)decoded.GetType().GetProperty("Definitions").GetValue(decoded);
                var visuals = (ElementVisualCatalog)decoded.GetType().GetProperty("Visuals").GetValue(decoded);
                Check(catalog.Count == 18, "확정18개 종류 왕복");
                Check(JsonUtility.ToJson(visuals.ToDto()) == JsonUtility.ToJson(LegacyElementVisuals.Catalog.ToDto()), "전체 상태와 효과 프레임 왕복");
                byte[] invalid = (byte[])bytes.Clone(); invalid[4] = 255;
                bool rejected = false;
                try { codec.GetMethod("Decode").Invoke(null, new object[] { invalid }); } catch (TargetInvocationException) { rejected = true; }
                Check(rejected, "미지원 팩 버전 거절");
                VerifyAuthored();
                VerifyEditorSources();
            }
            catch (Exception error) { Results.Add("FAIL " + error); Debug.LogException(error); exit = 1; }
            finally
            {
                Directory.CreateDirectory("Logs/ElementContentAuthoring");
                File.WriteAllLines("Logs/ElementContentAuthoring/results.txt", Results);
            }
            EditorApplication.Exit(exit);
        }

        private static void VerifyAuthored()
        {
            ElementCatalogAsset source = AssetDatabase.LoadAssetAtPath<ElementCatalogAsset>(ElementContentAuthoring.CatalogPath);
            Check(source != null && source.DefinitionAssets.Count == 18, "실제18개 제작 원본 연결");
            ElementContentAuthoring.ValidatePlanning(source);
            Check(source.VisualAsset != null, "실제 표현 제작 원본 연결");
            foreach (ElementDefinitionAsset asset in source.DefinitionAssets)
            {
                ElementDefinition definition = asset.ToDefinition();
                Check(MemoryPackSerializer.Serialize(PackedElementDefinition.FromDefinition(definition)).SequenceEqual(
                    MemoryPackSerializer.Serialize(PackedElementDefinition.FromDefinition(LegacyElementDefinitions.DefaultCatalog.Get(definition.Id)))),
                    "기획 대조 기존 설정 보존 " + definition.Id.Value);
            }
            var before = AssetDatabase.FindAssets("", new[] { ElementContentAuthoring.Folder }).Select(AssetDatabase.GUIDToAssetPath)
                .Where(File.Exists).ToDictionary(path => path, File.ReadAllBytes);
            ElementDefinitionAsset edited = source.DefinitionAssets[0]; string prior = JsonUtility.ToJson(edited);
            try
            {
                SerializedObject input = new SerializedObject(edited);
                input.FindProperty("definition").FindPropertyRelative("displayName").stringValue = "검사 중 편집값";
                input.ApplyModifiedPropertiesWithoutUndo();
                ElementContentAuthoring.Initialize();
                Check(edited.ToDefinition().DisplayName == "검사 중 편집값", "초기화 재실행이 편집값을 덮어쓰지 않음");
            }
            finally { JsonUtility.FromJsonOverwrite(prior, edited); }
            foreach (var file in before) Check(File.ReadAllBytes(file.Key).SequenceEqual(file.Value), "제작 원본 재실행 디스크 보존 " + Path.GetFileName(file.Key));
            ElementDefinitionAsset temporary = ScriptableObject.CreateInstance<ElementDefinitionAsset>();
            ElementCatalogAsset temporaryCatalog = ScriptableObject.CreateInstance<ElementCatalogAsset>();
            ElementVisualCatalogAsset temporaryVisuals = ScriptableObject.CreateInstance<ElementVisualCatalogAsset>();
            LevelDefinition level = null, restored = null;
            try
            {
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source), temporaryCatalog);
                ElementDefinitionAsset crate = source.DefinitionAssets.Single(value => value.ToDefinition().Id.Value == "obstacle.crate.wood");
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(crate), temporary);
                SerializedObject input = new SerializedObject(temporary);
                input.FindProperty("definition").FindPropertyRelative("placement").FindPropertyRelative("maxDurability").intValue = 4;
                input.FindProperty("planningDocument").stringValue = ""; input.ApplyModifiedPropertiesWithoutUndo();
                SerializedObject catalogInput = new SerializedObject(temporaryCatalog);
                SerializedProperty list = catalogInput.FindProperty("definitions");
                for (int index = 0; index < list.arraySize; index++)
                    if (list.GetArrayElementAtIndex(index).objectReferenceValue == crate) list.GetArrayElementAtIndex(index).objectReferenceValue = temporary;
                catalogInput.ApplyModifiedPropertiesWithoutUndo();
                bool rejected = false;
                try { ElementContentAuthoring.ValidatePlanning(temporaryCatalog); } catch (ArgumentException) { rejected = true; }
                Check(rejected, "기획 출처 없는 새 제작 입력 거절");
                input.FindProperty("planningDocument").stringValue = ElementContentAuthoring.RulesDocument; input.ApplyModifiedPropertiesWithoutUndo();
                rejected = false;
                try { ElementContentPackBuild.CreateBytes(new[] { source, temporaryCatalog }); } catch (ArgumentException) { rejected = true; }
                Check(rejected, "여러 출시 카탈로그의 같은 ID 다른 설정 거절");
                level = LevelPackCodec.Copy(AssetDatabase.LoadAssetAtPath<LevelDefinition>("Assets/Data/Levels/Level_01.asset"));
                SerializedObject levelInput = new SerializedObject(level);
                levelInput.FindProperty("elementCatalog").objectReferenceValue = temporaryCatalog; levelInput.ApplyModifiedPropertiesWithoutUndo();
                LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)ObstacleKind.Crate,
                    Durability = 3, ReplaceExisting = true }, new[] { new Board.BoardCoordinate(7, 7) });
                byte[] bytes = LevelPackBuild.CreatePackBytes(new[] { level }).Values.Single();
                var data = LevelPackCodec.ReadLevelWithCatalog(bytes, level.LevelNumber); restored = data.Level;
                Check(data.Catalog.Get(new ElementId("obstacle.crate.wood")).Placement.MaxDurability == 4,
                    "SO 수정값이 구형 제작 레벨의 팩 변환에도 반영됨");
                input.FindProperty("definition").FindPropertyRelative("id").stringValue = "verification.planned.crate";
                input.ApplyModifiedPropertiesWithoutUndo();
                ElementVisualCatalogDto visualInput = source.CreateVisualCatalog().ToDto();
                visualInput.bindings = visualInput.bindings.Concat(new[] { new ElementVisualBindingDto { id = "verification.planned.crate", visualKey = "legacy.crate" } }).ToArray();
                JsonUtility.FromJsonOverwrite("{\"planningDocument\":\"" + source.VisualAsset.PlanningDocument + "\",\"catalog\":" + JsonUtility.ToJson(visualInput) + "}", temporaryVisuals);
                catalogInput.FindProperty("visuals").objectReferenceValue = temporaryVisuals; catalogInput.ApplyModifiedPropertiesWithoutUndo();
                ElementContentData extended = ElementContentPackCodec.Decode(ElementContentPackBuild.CreateBytes(new[] { source, temporaryCatalog }));
                Check(extended.Definitions.Count == 19 && extended.Definitions.Get(new ElementId("verification.planned.crate")).Placement.MaxDurability == 4,
                    "기획 출처를 가진 다른 카탈로그의 신규 ID도 출시 팩에 병합");
                Check(extended.Visuals.Get(new ElementId("verification.planned.crate")).Key == "legacy.crate", "신규 ID의 명시적 이미지 공유 별칭 왕복");
                Check(File.ReadAllBytes(ElementContentPackBuild.OutputPath).SequenceEqual(ElementContentPackBuild.CreateDefaultBytes()), "제작 원본과 현재 디스크 콘텐츠 팩 동일");
                var settings = AddressableAssetSettingsDefaultObject.Settings;
                Check(settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(ElementContentPackBuild.OutputPath)).address == ElementContentPackCodec.Address,
                    "콘텐츠 bytes Addressables 등록");
                foreach (ElementDefinitionAsset asset in source.DefinitionAssets)
                    Check(settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset))) == null, "정의 SO 직접 등록 제외 " + asset.ToDefinition().Id.Value);
                Check(settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(ElementContentAuthoring.CatalogPath)) == null &&
                    settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(ElementContentAuthoring.VisualPath)) == null, "두 카탈로그 SO 직접 등록 제외");
                LevelPackBuild.ValidateExclusion(settings);
                Check(true, "씬·Resources·Addressables 의존성의 제작 원본 제외");
            }
            finally
            {
                if (restored != null) UnityEngine.Object.DestroyImmediate(restored);
                if (level != null) UnityEngine.Object.DestroyImmediate(level);
                UnityEngine.Object.DestroyImmediate(temporary); UnityEngine.Object.DestroyImmediate(temporaryCatalog); UnityEngine.Object.DestroyImmediate(temporaryVisuals);
            }
        }

        private static void VerifyEditorSources()
        {
            ElementVisualCatalogAsset visuals = AssetDatabase.LoadAssetAtPath<ElementVisualCatalogAsset>(ElementContentAuthoring.VisualPath);
            LevelDefinition level = AssetDatabase.LoadAssetAtPath<LevelDefinition>("Assets/Data/Levels/Level_01.asset");
            string before = JsonUtility.ToJson(visuals);
            try
            {
                var input = new SerializedObject(visuals);
                SerializedProperty definition = input.FindProperty("catalog").FindPropertyRelative("definitions").GetArrayElementAtIndex(0);
                string key = definition.FindPropertyRelative("key").stringValue;
                definition.FindPropertyRelative("states").GetArrayElementAtIndex(0).FindPropertyRelative("size").floatValue = 1.37f;
                input.ApplyModifiedPropertiesWithoutUndo();
                var asset = GameScreen.Editor.PuzzleEditorLaunchRequest.Capture(level, GameScreen.Editor.PuzzleEditorLevelSource.Asset, 12345);
                var packed = GameScreen.Editor.PuzzleEditorLaunchRequest.Capture(level, GameScreen.Editor.PuzzleEditorLevelSource.MemoryPack, 12345);
                float Size(ElementVisualCatalog catalog) => catalog.ToDto().definitions.Single(value => value.key == key).states[0].size;
                Check(Size(asset.CreateVisualCatalog()) == 1.37f, "에셋 게임 실행은 현재 SO 표현 편집값 사용");
                Check(Size(packed.CreateVisualCatalog()) == Size(ElementContentPackCodec.Decode(File.ReadAllBytes(ElementContentPackBuild.OutputPath)).Visuals),
                    "MemoryPack 게임 실행은 미반영 SO 편집값 대신 내보낸 표현 사용");
            }
            finally { JsonUtility.FromJsonOverwrite(before, visuals); }
        }

        public static void RunLoad()
        {
            if (!Application.isBatchMode || EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("별도 배치 Editor 전용입니다.");
            SessionState.SetBool(LoadKey, true);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }
        private static async UniTask LoadAsync()
        {
            Results.Clear(); int exit = 0; LevelDefinition level = null;
            try
            {
                ElementContentData content = await ElementContentPackLoader.LoadAsync();
                Check(content.Definitions.Count == 18, "실제 Addressables 콘텐츠18개 로드");
                level = await LevelPackLoader.LoadAsync(1);
                Check(level.ElementCatalog == null && level.PackedVisuals != null, "런타임에 제작 SO 대신 팩 표현 연결");
                Check(ReferenceEquals(ElementVisualLookup.ForLevel(level), level.PackedVisuals), "보드가 로드한 표현 팩을 사용");
                var start = new Simulation.StartingBoardSearch(level, 12345);
                while (!start.IsDone) { start.Advance(128); await UniTask.Yield(); }
                Check(start.Status == Simulation.StartingBoardStatus.Success, "팩 데이터로 실제 시작 보드 구성");
                var lookup = new ElementVisualLookup(level.PackedVisuals);
                Check(lookup.Content(start.State.CellAt(new Board.BoardCoordinate(4, 4))) != null, "팩의 실제 일반 블록 이미지 조회");
            }
            catch (Exception error) { Results.Add("FAIL " + error); Debug.LogException(error); exit = 1; }
            finally
            {
                if (level != null) UnityEngine.Object.Destroy(level);
                File.WriteAllLines("Logs/ElementContentAuthoring/load-results.txt", Results);
                EditorApplication.Exit(exit);
            }
        }
    }
}
