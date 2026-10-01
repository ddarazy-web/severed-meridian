using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Board;
using Cysharp.Threading.Tasks;
using MemoryPack;
using Simulation;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    public static class LevelPackVerification
    {
        private static readonly List<string> results = new List<string>();
        private static readonly List<LevelDefinition> owned = new List<LevelDefinition>();
        public static void Run() => RunAsync().Forget(Debug.LogException);
        private static async UniTask RunAsync()
        {
            const string fixtureFolder = "Assets/__LevelPackExclusionVerification";
            LevelEditorWindow window = null;
            LevelInitialStatePanel panel = null;
            bool fixtureCreated = false;
            try
            {
                results.Clear();
                LevelPackBuild.Generate();
                Check(LevelPackCodec.FirstLevel(1) == 1 && LevelPackCodec.FirstLevel(50) == 1 && LevelPackCodec.FirstLevel(51) == 51 &&
                    LevelPackCodec.FirstLevel(100) == 51 && LevelPackCodec.FirstLevel(101) == 101, "50레벨 고정 구간 경계");
                foreach (string fixture in Directory.GetFiles("Assets/Scripts/Features/Levels/Editor/Tests/Fixtures", "*.json"))
                {
                    LevelDefinition original = New();
                    JsonUtility.FromJsonOverwrite(File.ReadAllText(fixture), original);
                    JsonUtility.FromJsonOverwrite("{\"schemaVersion\":4,\"levelNumber\":1}", original);
                    LevelDefinition copy = LevelPackCodec.ReadLevel(LevelPackCodec.Encode(new[] { original }), 1); owned.Add(copy);
                    Check(JsonUtility.ToJson(copy) == JsonUtility.ToJson(original), "전체 필드 왕복 " + Path.GetFileName(fixture));
                }
                LevelDefinition first = New(), last = New(), next = New();
                JsonUtility.FromJsonOverwrite("{\"levelNumber\":50}", last);
                JsonUtility.FromJsonOverwrite("{\"levelNumber\":51}", next);
                byte[] sparse = LevelPackCodec.Encode(new[] { last, first });
                Check(LevelPackCodec.Decode(sparse).Levels.Select(item => item.LevelNumber).SequenceEqual(new[] { 1, 50 }), "빈 번호 구간 보존·정렬");
                Reject(() => LevelPackCodec.Encode(new[] { first, first }), "중복 번호 거절");
                Reject(() => LevelPackCodec.Encode(new[] { last, next }), "구간 혼합 거절");
                Reject(() => LevelPackCodec.ReadLevel(sparse, 2), "누락 레벨 거절");
                Reject(() => LevelPackCodec.ReadLevel(sparse, 51), "다른 구간 요청 거절");
                Reject(() => LevelPackCodec.Decode(sparse.Take(10).ToArray()), "잘린 파일 거절");
                LevelPack wrongVersion = LevelPackCodec.Decode(sparse); wrongVersion.FormatVersion++;
                Reject(() => LevelPackCodec.Decode(MemoryPackSerializer.Serialize(wrongVersion)), "미지원 포맷 거절");
                var settings = AddressableAssetSettingsDefaultObject.Settings;
                LevelDefinition asset = AssetDatabase.LoadAssetAtPath<LevelDefinition>("Assets/Data/Levels/Level_01.asset");
                string assetJson = JsonUtility.ToJson(asset);
                string assetFile = File.ReadAllText(AssetDatabase.GetAssetPath(asset));
                string packPath = LevelPackBuild.FilePath(asset.LevelNumber);
                byte[] generated = File.ReadAllBytes(packPath);
                Check(generated.SequenceEqual(LevelPackCodec.Encode(new[] { asset })), "생성 파일과 원본 일치");
                Check(settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset))) == null, "원본 Addressables 항목 제외");
                Check(settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(packPath)).address == LevelPackCodec.Address(asset.LevelNumber), "바이너리만 Addressables 등록");
                Check(!AssetDatabase.GetDependencies(packPath, true).Contains(AssetDatabase.GetAssetPath(asset)), "바이너리의 원본 에셋 의존성 없음");
                LevelDefinition loaded = await LevelPackLoader.LoadAsync(asset.LevelNumber); owned.Add(loaded);
                Check(JsonUtility.ToJson(loaded) == assetJson, "실제 Addressables 번들 로드·전체 필드 일치");
                bool bundled = false;
                foreach (var locator in Addressables.ResourceLocators)
                    if (locator.Locate(LevelPackCodec.Address(asset.LevelNumber), typeof(TextAsset), out var locations))
                        bundled |= locations.Any(location => location.ProviderId.Contains("BundledAssetProvider"));
                Check(bundled, "BundledAssetProvider 사용");
                LevelStateBuildResult a = LevelStateBuilder.Build(asset, 17), b = LevelStateBuilder.Build(loaded, 17);
                Check(a.IsBuilt == b.IsBuilt && LevelStateBuilder.Fingerprint(asset) == LevelStateBuilder.Fingerprint(loaded), "동일 시드 입력·빌드 결과 일치");
                window = ScriptableObject.CreateInstance<LevelEditorWindow>();
                window.ShowUtility();
                panel = new LevelInitialStatePanel(); panel.Initialize(window, asset, false);
                window.rootVisualElement.Add(panel.rootVisualElement);
                var source = panel.rootVisualElement.Q<PopupField<string>>("initial-level-source");
                var inputProperty = typeof(LevelInitialStatePanel).GetProperty("level", BindingFlags.Instance | BindingFlags.NonPublic);
                Check(source.value == "에셋" && ReferenceEquals(inputProperty.GetValue(panel), asset), "에디터 에셋 입력 선택");
                source.value = "MemoryPack";
                results.Add("INFO source panel attached " + (source.panel != null) + " / mode " + source.value);
                Check(!ReferenceEquals(inputProperty.GetValue(panel), asset) && JsonUtility.ToJson(inputProperty.GetValue(panel)) == assetJson, "에디터 MemoryPack 파일 입력 선택");
                typeof(LevelInitialStatePanel).GetMethod("Build", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(panel, null);
                Check((panel.CurrentState != null) == a.IsBuilt, "MemoryPack 입력으로 초기 보드 실행");
                source.value = "에셋";
                Check(ReferenceEquals(inputProperty.GetValue(panel), asset), "에셋 입력 복귀");
                LevelDefinition edited = New();
                JsonUtility.FromJsonOverwrite(assetJson, edited);
                JsonUtility.FromJsonOverwrite("{\"moveCount\":123}", edited);
                panel.SetLevel(edited);
                source.value = "MemoryPack";
                Check(((LevelDefinition)inputProperty.GetValue(panel)).MoveCount == asset.MoveCount, "MemoryPack은 편집 사본 대신 생성 파일을 읽음");
                JsonUtility.FromJsonOverwrite("{\"levelNumber\":51}", edited);
                typeof(LevelInitialStatePanel).GetMethod("Build", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(panel, null);
                Check(inputProperty.GetValue(panel) == null && panel.CurrentState == null, "누락 팩을 에셋으로 대체하지 않음");
                using (MultiLevelTestPanel multi = new MultiLevelTestPanel(() => false, _ => { }, new MultiLevelTestStore("Logs/LevelPackVerification/Multi")))
                {
                    window.rootVisualElement.Add(multi.Root);
                    multi.Root.Q<PopupField<string>>("multi-level-source").value = "MemoryPack";
                    var choices = multi.Root.Q<ListView>("multi-level-choices");
                    int selected = -1;
                    for (int i = 0; i < choices.itemsSource.Count; i++) if (ReferenceEquals(choices.itemsSource[i], asset)) selected = i;
                    choices.SetSelection(selected);
                    typeof(MultiLevelTestPanel).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(multi, null);
                    var session = (MultiLevelTestSession)typeof(MultiLevelTestPanel).GetField("session", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(multi);
                    Check(session != null && session.Record.entries.Count == 1 && session.Record.entries[0].definitionJson == assetJson, "다중 레벨 시험도 MemoryPack 사본 사용");
                }
                Check(JsonUtility.ToJson(asset) == assetJson && File.ReadAllText(AssetDatabase.GetAssetPath(asset)) == assetFile, "테스트가 원본 에셋을 바꾸지 않음");
                if (AssetDatabase.IsValidFolder(fixtureFolder)) throw new Exception("검증 폴더가 이미 존재합니다.");
                AssetDatabase.CreateFolder("Assets", "__LevelPackExclusionVerification");
                fixtureCreated = true;
                AssetDatabase.CreateFolder(fixtureFolder, "Resources");
                LevelDefinition forbidden = New();
                AssetDatabase.CreateAsset(forbidden, fixtureFolder + "/Resources/Original.asset");
                Reject(() => LevelPackBuild.ValidateExclusion(settings), "Resources에 섞인 원본은 빌드 거절");
                AssetDatabase.DeleteAsset(fixtureFolder);
                LevelPackBuild.ValidateExclusion(settings);
                Check(true, "현재 빌드 의존성에 원본 레벨 없음");
                results.Add("INFO pack bytes " + generated.Length);
            }
            catch (Exception error) { results.Add("FAIL " + error); }
            finally
            {
                panel?.Dispose();
                if (fixtureCreated && AssetDatabase.IsValidFolder(fixtureFolder)) AssetDatabase.DeleteAsset(fixtureFolder);
                if (window != null) window.Close();
                foreach (LevelDefinition item in owned) if (item != null && !EditorUtility.IsPersistent(item)) UnityEngine.Object.DestroyImmediate(item);
                owned.Clear();
                Directory.CreateDirectory("Logs/LevelPackVerification");
                File.WriteAllLines("Logs/LevelPackVerification/results.txt", results);
                if (Application.isBatchMode) EditorApplication.Exit(results.Any(value => value.StartsWith("FAIL")) ? 1 : 0);
            }
        }
        private static LevelDefinition New() { var value = ScriptableObject.CreateInstance<LevelDefinition>(); owned.Add(value); return value; }
        private static void Check(bool condition, string message) => results.Add((condition ? "PASS " : "FAIL ") + message);
        private static void Reject(Action action, string message)
        {
            try { action(); results.Add("FAIL " + message); }
            catch { results.Add("PASS " + message); }
        }
    }
}
