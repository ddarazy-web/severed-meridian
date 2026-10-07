using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Board;
using Cysharp.Threading.Tasks;
using GameScreen;
using Levels;
using Levels.Editor;
using Simulation;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Elements.Editor
{
    public static partial class ElementScaleVerification
    {
        private const string ResourcesKey = "ElementFramework.Phase05.Resources500";
        private static readonly List<string> ResourceMeasurements = new List<string>();
        public static void RunResources()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 Editor에서만 실행한다.");
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("미저장 씬 보존");
            SessionState.SetBool(ResourcesKey, true);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); EditorApplication.EnterPlaymode();
        }

        private static async UniTask RunResourcesAsync()
        {
            Results.Clear(); Owned.Clear(); ResourceMeasurements.Clear(); int exit = 0;
            bool profiler = UnityEngine.Profiling.Profiler.enabled, editor = UnityEditorInternal.ProfilerDriver.profileEditor;
            try
            {
                UnityEditorInternal.ProfilerDriver.profileEditor = true; UnityEngine.Profiling.Profiler.enabled = true;
                ElementCatalogAsset catalog = ScriptableObject.CreateInstance<ElementCatalogAsset>(); Owned.Add(catalog);
                ElementVisualCatalogAsset visualSource = ScriptableObject.CreateInstance<ElementVisualCatalogAsset>(); Owned.Add(visualSource);
                ElementDefinition[] templates = { LegacyElementDefinitions.Get(ObstacleKind.Crate),
                    LegacyElementDefinitions.Get(CoverKind.Web), LegacyElementDefinitions.GetSupply(SupplyKind.Rocket) };
                ElementVisualCatalogDto builtin = LegacyElementVisuals.Catalog.ToDto();
                List<ElementVisualBindingDto> aliases = new List<ElementVisualBindingDto>();
                using (SerializedObject input = new SerializedObject(catalog))
                {
                    SerializedProperty entries = input.FindProperty("definitions"); entries.arraySize = 500;
                    for (int i = 0; i < 500; i++)
                    {
                        PackedElementDefinition packed = PackedElementDefinition.FromDefinition(templates[i % 3]);
                        packed.id = "phase05.native." + i.ToString("D3"); packed.displayName = "native 제작 " + i.ToString("D3");
                        ElementDefinitionAsset asset = ScriptableObject.CreateInstance<ElementDefinitionAsset>(); Owned.Add(asset);
                        JsonUtility.FromJsonOverwrite("{\"definition\":" + JsonUtility.ToJson(packed) + "}", asset);
                        entries.GetArrayElementAtIndex(i).objectReferenceValue = asset;
                        aliases.Add(new ElementVisualBindingDto { id = packed.id,
                            visualKey = builtin.bindings.Single(binding => binding.id == templates[i % 3].Id.Value).visualKey });
                    }
                    JsonUtility.FromJsonOverwrite("{\"catalog\":" + JsonUtility.ToJson(new ElementVisualCatalogDto { bindings = aliases.ToArray() }) + "}", visualSource);
                    input.FindProperty("visuals").objectReferenceValue = visualSource; input.ApplyModifiedPropertiesWithoutUndo();
                }
                ElementVisualCatalog visuals = catalog.CreateVisualCatalog();
                Check(catalog.CreateCatalog().Count == 500, "native 측정도 실제 제작 입력500개와 시각 별칭 사용");
                GameObject root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Game/Puzzle/PuzzleWorldBoard.prefab")); Owned.Add(root);
                PuzzleWorldBoard world = root.GetComponent<PuzzleWorldBoard>();
                foreach (bool supplied in new[] { false, true })
                {
                    LevelDefinition level = (LevelDefinition)typeof(PowerEffectVerification).GetMethod("Make", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null); Owned.Add(level);
                    using (SerializedObject input = new SerializedObject(level))
                    { input.FindProperty("elementCatalog").objectReferenceValue = catalog; input.ApplyModifiedPropertiesWithoutUndo(); }
                    LevelElementMigration.Apply(level);
                    BoardCoordinate target = new BoardCoordinate(4, 4);
                    if (supplied)
                    {
                        Type editing = typeof(ElementScaleVerification).Assembly.GetType("Elements.Editor.ElementPlacementEditing");
                        PlacementBrush brush = (PlacementBrush)editing.GetMethod("ForDefinition").Invoke(null, new object[] { level, new ElementId("phase05.native.000") });
                        brush.ReplaceExisting = true; brush.Durability = 1;
                        Check(LevelObstacleEditing.Apply(level, brush, new[] { target }).Changed == 1, "native 공급 판의 제작 본체 배치");
                        JsonUtility.FromJsonOverwrite("{\"elementSupply\":{\"sources\":[{\"coordinate\":{\"row\":0,\"column\":0},\"mode\":1,\"exhaustion\":0," +
                            "\"items\":[{\"definitionId\":\"phase05.native.002\",\"count\":1}]}]}}", level);
                    }
                    LevelStateBuildResult built = LevelStateBuilder.Build(level, 12345);
                    Check(built.IsBuilt, "native500 실제 레벨 유효 " + supplied + " " + string.Join(";", built.Issues));
                    ElementResourcePlan plan = ElementResourcePlan.Create(built.State, visuals);
                    Check(plan.Definitions.Count(id => id.Value.StartsWith("phase05.native.")) == (supplied ? 2 : 0),
                        "실제 사용/공급 ID만 준비 집합에 포함 " + supplied);
                    string original = JsonUtility.ToJson(level); int draws = built.State.Random.DrawCount;
                    for (int sample = -1; sample < 5; sample++)
                    {
                        using PuzzleArtwork artwork = new PuzzleArtwork(visuals);
                        using ProfilerRecorder allocations = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 4096);
                        Stopwatch timer = Stopwatch.StartNew();
                        await artwork.PrepareAsync(built.State, CancellationToken.None).Timeout(TimeSpan.FromSeconds(20)); timer.Stop();
                        double prepare = timer.Elapsed.TotalMilliseconds;
                        await UniTask.DelayFrame(1);
                        long prepareFrameBytes = allocations.ToArray().Sum(item => item.Value); int prepareFrames = allocations.Count;
                        Check(allocations.Valid && allocations.UnitType.ToString() == "Bytes" && prepareFrames > 0 && !allocations.WrappedAround,
                            "native 준비 측정의 바이트 단위/유효 표본/버퍼 손실0 " + supplied + "/" + sample);
                        Dictionary<string, BoardSpriteAtlas> atlases = (Dictionary<string, BoardSpriteAtlas>)typeof(PuzzleArtwork)
                            .GetField("atlases", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(artwork);
                        Check(atlases.Keys.OrderBy(value => value).SequenceEqual(plan.Addresses.OrderBy(value => value)) && atlases.Values.All(value => value.IsLoaded),
                            "실제 native 필요한 주소 누락0/미사용 별도 요청0 " + supplied + "/" + sample);
                        Check(!atlases.ContainsKey(BoardSpriteAtlas.Prefix + "Obstacles-Web") &&
                            !atlases.ContainsKey(BoardSpriteAtlas.Prefix + "Obstacles-Generator"), "미사용500개 정의의 별도 장애물 주소 준비0 " + supplied + "/" + sample);
                        timer.Restart(); world.Draw(built.State, artwork); timer.Stop();
                        double draw = timer.Elapsed.TotalMilliseconds;
                        int[] capacity = world.GetComponentsInChildren<Transform>(true).Select(item => item.GetInstanceID()).OrderBy(value => value).ToArray();
                        for (int repeat = 0; repeat < 5; repeat++) world.Draw(built.State, artwork);
                        Check(world.GetComponentsInChildren<Transform>(true).Select(item => item.GetInstanceID()).OrderBy(value => value).SequenceEqual(capacity),
                            "native 반복 표시의 슬롯 추가 생성0 " + supplied + "/" + sample);
                        allocations.Reset(); allocations.Start();
                        timer.Restart(); BoardActionExecutor executor = new BoardActionExecutor(built.State);
                        ItemUseResult execution = executor.UseItem(BoardItem.Hammer, target); timer.Stop();
                        double execute = timer.Elapsed.TotalMilliseconds; await UniTask.DelayFrame(1);
                        Check(execution.IsApplied && execution.Effects.Count > 0, "측정도 실제 공통 아이템/피해 실행 사용 " + supplied + "/" + sample);
                        long executionFrameBytes = allocations.ToArray().Sum(item => item.Value);
                        if (sample >= 0)
                            ResourceMeasurements.Add("supply=" + supplied + "\tsample=" + sample + "\twarmup=1\tprepareMs=" + prepare.ToString("F3") +
                                "\tdrawMs=" + draw.ToString("F3") + "\texecuteMs=" + execute.ToString("F3") + "\twholeEditorExecutionFramesBytes=" + executionFrameBytes +
                                "\tprepareFrames=" + prepareFrames + "\twholeEditorPrepareFramesBytes=" + prepareFrameBytes +
                                "\taddresses=" + atlases.Count + "\treachableDefinitions=" + plan.Definitions.Count + "\ttransformSlots=" + capacity.Length +
                                "\taddressKeys=" + string.Join(",", atlases.Keys.OrderBy(value => value)));
                        Sprite clone = artwork.Get("Blocks/rabbit-pink-v1-256");
                        artwork.Dispose(); await UniTask.DelayFrame(1);
                        Check(artwork.AtlasCount == 0 && clone == null, "native 소유자 반환의 핸들/클론 잔류0 " + supplied + "/" + sample);
                    }
                    Check(JsonUtility.ToJson(level) == original && built.State.Random.DrawCount == draws,
                        "native 준비/표시/반환은 원본과 규칙 난수 무변경 " + supplied);
                }
            }
            catch (Exception error) { Results.Add("FAIL " + error); UnityEngine.Debug.LogException(error); exit = 1; }
            finally
            {
                foreach (UnityEngine.Object item in Owned) if (item != null) UnityEngine.Object.DestroyImmediate(item); Owned.Clear();
                UnityEngine.Profiling.Profiler.enabled = profiler; UnityEditorInternal.ProfilerDriver.profileEditor = editor;
                ResourceMeasurements.Insert(0, "Unity=" + Application.unityVersion + "\tmode=Editor PlayMode\tboard=9x9\tauthoredDefinitions=500\tsamples=5\twarmup=1\tallocation=whole Editor preparation frames; not exclusive function allocation\tno bundle/device build");
                File.WriteAllLines("Logs/ElementFramework/Phase05/native-scale-results.txt", Results);
                File.WriteAllLines("Logs/ElementFramework/Phase05/native-scale-measurements.txt", ResourceMeasurements);
            }
            EditorApplication.Exit(exit);
        }
    }
}
