using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Levels;
using MemoryPack;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Elements.Editor
{
    public static partial class ElementScaleVerification
    {
        private const string ScaleFolder = "Assets/__ElementScaleVerification";
        private static EditorWindow scaleWindow;
        private static ElementCatalogView scaleView;
        private static ElementCatalogViewModel scaleModel;
        private static IEnumerator scaleSequence;
        private static bool ownsScaleFolder;
        private static double scaleDeadline, scaleNextTick;
        private static readonly List<string> ScaleMeasurements = new List<string>();
        private static bool allocationCounterAvailable;
        private static PackedElementDefinition[] scalePacked;
        private static bool measuringFrames;

        public static void RunFrameAllocationProbe()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 Editor에서만 실행한다.");
            bool enabled = UnityEngine.Profiling.Profiler.enabled;
            bool editor = UnityEditorInternal.ProfilerDriver.profileEditor;
            UnityEditorInternal.ProfilerDriver.profileEditor = true;
            UnityEngine.Profiling.Profiler.enabled = true;
            ProfilerRecorder recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 16);
            int frame = 0; long baseline = 0;
            double deadline = EditorApplication.timeSinceStartup + 10;
            EditorApplication.CallbackFunction tick = null;
            tick = () =>
            {
                int exit = 0; string result = null;
                try
                {
                    if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("할당 프레임 교정 시간 초과");
                    frame++;
                    if (frame < 4) return;
                    if (frame == 4)
                    {
                        baseline = recorder.LastValue;
                        byte[] allocation = new byte[65536]; GC.KeepAlive(allocation);
                        return;
                    }
                    result = "Unity=" + Application.unityVersion + "\tmode=Editor\tmetric=GC Allocated In Frame\tunit=" + recorder.UnitType +
                        "\tvalid=" + recorder.Valid + "\tsamples=" + recorder.Count + "\tbaselineFrameBytes=" + baseline +
                        "\tallocatedFrameBytes=" + recorder.LastValue + "\tknownAllocation=65536\tcalibrated=" +
                        (recorder.Valid && recorder.UnitType.ToString() == "Bytes" && recorder.Count > 0 && recorder.LastValue >= 65536);
                }
                catch (Exception error) { exit = 1; result = "FAIL " + error; }
                EditorApplication.update -= tick; recorder.Dispose();
                UnityEngine.Profiling.Profiler.enabled = enabled; UnityEditorInternal.ProfilerDriver.profileEditor = editor;
                File.WriteAllText("Logs/ElementFramework/Phase05/frame-allocation-probe.txt", result);
                EditorApplication.Exit(exit);
            };
            EditorApplication.update += tick;
        }

        public static void RunAllocationProbe()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 Editor에서만 실행한다.");
            int exit = 0;
            bool profilerEnabled = UnityEngine.Profiling.Profiler.enabled;
            List<string> probe = new List<string> { "Unity=" + Application.unityVersion + "\tmode=Editor\tmetric=GC.Alloc\tthread=current\tframeSummation=false" };
            try
            {
                UnityEngine.Profiling.Profiler.enabled = true;
                using ProfilerRecorder recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc", 4096,
                    ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
                byte[] allocation = new byte[65536]; GC.KeepAlive(allocation); recorder.Stop();
                ProfilerRecorderSample[] samples = recorder.ToArray();
                long bytes = samples.Sum(sample => sample.Value);
                probe.Add("valid=" + recorder.Valid + "\tsamples=" + samples.Length + "\twrapped=" + recorder.WrappedAround +
                    "\trawSum=" + bytes + "\tunit=" + recorder.UnitType + "\tknownAllocation=65536\tcalibrated=" +
                    (recorder.Valid && !recorder.WrappedAround && recorder.UnitType.ToString() == "Bytes" && bytes >= allocation.Length));
                List<Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle> handles = new List<Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle>();
                Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle.GetAvailable(handles);
                foreach (var handle in handles)
                {
                    var description = Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle.GetDescription(handle);
                    if (description.Name.Contains("GC")) probe.Add("availableMetric=" + description.Name + "\tunit=" + description.UnitType);
                }
            }
            catch (Exception error) { probe.Add("FAIL " + error); exit = 1; }
            finally { UnityEngine.Profiling.Profiler.enabled = profilerEnabled; }
            File.WriteAllLines("Logs/ElementFramework/Phase05/allocation-probe.txt", probe);
            EditorApplication.Exit(exit);
        }

        public static void RunCatalog()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 Editor에서만 실행한다.");
            Results.Clear(); ScaleMeasurements.Clear(); ownsScaleFolder = false;
            long calibrationStart = GC.GetAllocatedBytesForCurrentThread();
            byte[] calibration = new byte[65536]; GC.KeepAlive(calibration);
            allocationCounterAvailable = GC.GetAllocatedBytesForCurrentThread() - calibrationStart >= calibration.Length;
            ScaleMeasurements.Add("allocationCalibration\tknownBytes=65536\tavailable=" + allocationCounterAvailable);
            try
            {
                if (Directory.Exists(ScaleFolder) || AssetDatabase.IsValidFolder(ScaleFolder)) throw new InvalidOperationException("검사 폴더가 이미 존재합니다.");
                AssetDatabase.CreateFolder("Assets", "__ElementScaleVerification"); ownsScaleFolder = true;
                string path = ScaleFolder + "/Catalog.asset";
                ElementCatalogAsset source = ScriptableObject.CreateInstance<ElementCatalogAsset>();
                AssetDatabase.CreateAsset(source, path);
                ElementDefinition[] templates = { LegacyElementDefinitions.Get(ObstacleKind.Crate),
                    LegacyElementDefinitions.Get(CoverKind.Web), LegacyElementDefinitions.GetSupply(SupplyKind.RandomPower),
                    LegacyElementDefinitions.GetSupply(SupplyKind.Scrap), LegacyElementDefinitions.GetSupply(SupplyKind.Rocket) };
                ElementVisualCatalogDto builtin = LegacyElementVisuals.Catalog.ToDto();
                List<ElementVisualBindingDto> aliases = new List<ElementVisualBindingDto>();
                MeasureScale("author500", () =>
                {
                    using SerializedObject input = new SerializedObject(source);
                    SerializedProperty entries = input.FindProperty("definitions"); entries.arraySize = 500;
                    for (int i = 0; i < 500; i++)
                    {
                        ElementDefinition template = templates[i % 5];
                        if (i % 5 == 2) template = ElementDefinition.CreateSupply(template.Id, template.DisplayName,
                            ElementSupplyProfile.ForRandomPower(new[] { new ElementId("phase05.scale." + (i + 2).ToString("D3")) }));
                        if (i % 5 == 3) template = ElementDefinition.CreateSupply(template.Id, template.DisplayName,
                            ElementSupplyProfile.ForObstacle(new ElementId("phase05.scale." + (i - 3).ToString("D3")), "scale-body-"));
                        PackedElementDefinition value = PackedElementDefinition.FromDefinition(template);
                        value.id = "phase05.scale." + i.ToString("D3"); value.displayName = "규모 정의 " + i.ToString("D3");
                        ElementDefinitionAsset definition = ScriptableObject.CreateInstance<ElementDefinitionAsset>();
                        definition.name = value.id;
                        JsonUtility.FromJsonOverwrite("{\"definition\":" + JsonUtility.ToJson(value) + "}", definition);
                        AssetDatabase.AddObjectToAsset(definition, source);
                        entries.GetArrayElementAtIndex(i).objectReferenceValue = definition;
                        if (i % 5 == 0 || i % 5 == 1 || i % 5 == 4)
                            aliases.Add(new ElementVisualBindingDto { id = value.id,
                                visualKey = builtin.bindings.Single(binding => binding.id == template.Id.Value).visualKey });
                    }
                    ElementVisualCatalogAsset visual = ScriptableObject.CreateInstance<ElementVisualCatalogAsset>();
                    JsonUtility.FromJsonOverwrite("{\"catalog\":" + JsonUtility.ToJson(new ElementVisualCatalogDto { bindings = aliases.ToArray() }) + "}", visual);
                    AssetDatabase.AddObjectToAsset(visual, source); input.FindProperty("visuals").objectReferenceValue = visual;
                    input.ApplyModifiedPropertiesWithoutUndo();
                });
                MeasureScale("save500", () => AssetDatabase.SaveAssetIfDirty(source));
                Check(File.Exists(path) && AssetDatabase.LoadAllAssetsAtPath(path).OfType<ElementDefinitionAsset>().Count() == 500,
                    "실제 제작 에셋500개를 카탈로그 하위 에셋으로 저장");
                string disk = File.ReadAllText(path);
                Check(disk.Contains("phase05.scale.000") && disk.Contains("phase05.scale.499"), "제작 원문 디스크에 양 끝 ID 저장");
                string guid = AssetDatabase.AssetPathToGUID(path);
                // 이전 객체를 직접 내리고 저장된 카탈로그와 하위 원본을 다시 읽는다.
                foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path)) Resources.UnloadAsset(asset);
                source = null;
                ElementCatalog catalog = null;
                MeasureScale("reload500", () =>
                {
                    source = AssetDatabase.LoadAssetAtPath<ElementCatalogAsset>(path);
                    catalog = source.CreateCatalog();
                });
                Check(catalog.Count == 500 && AssetDatabase.AssetPathToGUID(path) == guid, "제작500개 실제 저장/재로드와 GUID 유지");
                Check(catalog.Definitions.Count(item => item.Supply?.ObstacleDefinitionId.HasValue == true) == 100 &&
                    catalog.Definitions.Count(item => item.Supply?.ChoiceDefinitionIds.Count > 0) == 100,
                    "제작 본체 공급100개/파워 선택100개의 새 ID 참조 재로드");
                Check(source.CreateVisualCatalog().ToDto().bindings.Count(item => item.id.StartsWith("phase05.scale.")) == 300,
                    "실제 표시 정의300개의 명시적 공유 별칭 저장/재로드; 생성 공급은 참조 대상 표시 사용");
                MeasureScale("lookup500forwardreverse", () =>
                {
                    for (int i = 0; i < 500; i++) Check(catalog.Get(new ElementId("phase05.scale." + i.ToString("D3"))).Id.Value.EndsWith(i.ToString("D3")), "정순 ID " + i);
                    for (int i = 499; i >= 0; i--) Check(catalog.Get(new ElementId("phase05.scale." + i.ToString("D3"))).DisplayName.EndsWith(i.ToString("D3")), "역순 ID " + i);
                });
                PackedElementDefinition[] packed = catalog.Definitions.Select(PackedElementDefinition.FromDefinition).ToArray();
                scalePacked = packed;
                byte[] bytes = null;
                MeasureScale("memorypack500", () => bytes = MemoryPackSerializer.Serialize(packed));
                ElementCatalog restored = null;
                MeasureScale("restorepack500", () => restored = new ElementCatalog(MemoryPackSerializer.Deserialize<PackedElementDefinition[]>(bytes).Select(item => item.ToDefinition())));
                Check(restored.Count == 500 && restored.Definitions.Select(item => JsonUtility.ToJson(PackedElementDefinition.FromDefinition(item)))
                    .SequenceEqual(catalog.Definitions.Select(item => JsonUtility.ToJson(PackedElementDefinition.FromDefinition(item)))), "제작500개 MemoryPack 프로필 전체 왕복");
                ElementDefinitionAsset first = AssetDatabase.LoadAllAssetsAtPath(path).OfType<ElementDefinitionAsset>().Single(item => item.name.EndsWith("000"));
                string original = JsonUtility.ToJson(first);
                using (SerializedObject edit = new SerializedObject(first))
                {
                    edit.FindProperty("definition.displayName").stringValue = "Undo 검증 이름";
                    edit.ApplyModifiedProperties();
                }
                Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
                Check(JsonUtility.ToJson(first) == original, "제작 원본 이름 편집 Undo");
                Undo.PerformRedo(); Check(first.ToDefinition().DisplayName == "Undo 검증 이름", "제작 원본 이름 편집 Redo");
                Undo.PerformUndo(); AssetDatabase.SaveAssetIfDirty(first);
                Check(File.ReadAllText(path) == disk, "Undo 후 디스크 제작 원문 복원");
                scaleModel = new ElementCatalogViewModel(catalog);
                MeasureScale("search500", () => scaleModel.SetSearch("SCALE.499"));
                Check(scaleModel.Visible.Single().Id.Value == "phase05.scale.499", "500개 카탈로그 ID 검색");
                scaleModel.SetSearch(""); scaleModel.SetLayer(PlacementLayer.Cover);
                Check(scaleModel.Visible.Count == 100, "제작 층 프로필 필터100개"); scaleModel.SetLayer(null);
                scaleWindow = ScriptableObject.CreateInstance<EditorWindow>();
                scaleWindow.position = new Rect(30, 30, 450, 400);
                MeasureScale("view500construct", () => scaleView = new ElementCatalogView(scaleModel,
                    definition => Check(definition.Id == scaleModel.SelectedDefinition.Id, "실제 선택 배치 명령의 ID")));
                scaleView.style.height = 350;
                scaleWindow.rootVisualElement.Add(scaleView); scaleWindow.ShowUtility(); scaleWindow.Focus();
                scaleSequence = VerifyScalePanel(); scaleDeadline = EditorApplication.timeSinceStartup + 60;
                EditorApplication.update += TickScale;
            }
            catch (Exception error) { Results.Add("FAIL " + error); UnityEngine.Debug.LogException(error); FinishScale(1); }
        }

        private static void MeasureScale(string name, Action action)
        {
            long before = GC.GetAllocatedBytesForCurrentThread(); Stopwatch timer = Stopwatch.StartNew();
            action(); timer.Stop();
            ScaleMeasurements.Add(name + "\tms=" + timer.Elapsed.TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) +
                "\tthreadAllocatedBytes=" + (allocationCounterAvailable ? (GC.GetAllocatedBytesForCurrentThread() - before).ToString() : "unavailable"));
        }
        private static void TickScale()
        {
            if (EditorApplication.timeSinceStartup < scaleNextTick) return;
            scaleNextTick = EditorApplication.timeSinceStartup + (measuringFrames ? 0 : .15);
            try
            {
                if (EditorApplication.timeSinceStartup > scaleDeadline) throw new TimeoutException("실제500개 패널 대기 시간 초과");
                scaleWindow.Repaint();
                if (!scaleSequence.MoveNext()) FinishScale(0);
            }
            catch (Exception error) { Results.Add("FAIL " + error); UnityEngine.Debug.LogException(error); FinishScale(1); }
        }
        private static IEnumerator VerifyScalePanel()
        {
            yield return null; yield return null; yield return null;
            Check(scaleView.panel != null && scaleView.worldBound.height > 0 && scaleModel.Visible.Count == 500, "실제 열린500개 카탈로그 패널");
            int rows = scaleView.Query<Button>().ToList().Count(button => button.name.StartsWith("definition-"));
            ScaleMeasurements.Add("livePanel\tvisibleDefinitions=500\tcreatedRows=" + rows);
            Check(rows > 0 && rows <= 40, "보이는 행만 생성:500개 전체 버튼 선생성0 (실제 " + rows + ")");
            ListView list = scaleView.Q<ListView>("element-catalog-entries");
            list.ScrollToItem(499);
            yield return null; yield return null;
            Button last = scaleView.Q<Button>("definition-phase05.scale.499");
            Check(last != null, "실제 목록 끝으로 스크롤하여 재활용 행499 표시");
            using (NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled()) { evt.target = last; last.SendEvent(evt); }
            Check(scaleModel.SelectedDefinition.Id.Value == "phase05.scale.499", "스크롤로 재활용된 행의 현재 ID 선택");
            yield return null; yield return null;
            int recycledRows = scaleView.Query<Button>().ToList().Count(button => button.name.StartsWith("definition-"));
            Check(recycledRows > 0 && recycledRows <= 40, "스크롤/선택 후에도 생성 행 수 제한 유지");
            scaleView.Q<ToolbarSearchField>("element-catalog-search").value = "scale.499";
            yield return null; yield return null;
            Button row = scaleView.Q<Button>("definition-phase05.scale.499");
            Check(row != null && scaleModel.Visible.Count == 1, "실제 검색 이벤트와 재활용 행499");
            using (NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled()) { evt.target = row; row.SendEvent(evt); }
            Check(scaleModel.SelectedDefinition.Id.Value == "phase05.scale.499" && scaleModel.CanPlace, "실제 행 선택 이벤트와 활성 배치");
            Button place = scaleView.Q<Button>("element-catalog-place");
            using (NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled()) { evt.target = place; place.SendEvent(evt); }
            IEnumerator measurements = MeasureScaleFrames();
            try { while (measurements.MoveNext()) yield return measurements.Current; }
            finally { (measurements as IDisposable)?.Dispose(); }
            scaleView.Dispose(); scaleModel.SetSearch("scale.001");
            Check(scaleModel.Visible.Single().Id.Value == "phase05.scale.001" && list.itemsSource.Count == 1 &&
                ((ElementDefinition)list.itemsSource[0]).Id.Value == "phase05.scale.499", "View Dispose 후 모델만 변경되고 표시 목록 구독 해제");
            scaleModel.Dispose(); Check(scaleModel.Visible.Count == 0 && !scaleModel.CanPlace, "모델 Dispose 상태/배치 정리");
        }
        private static IEnumerator MeasureScaleFrames()
        {
            bool enabled = UnityEngine.Profiling.Profiler.enabled, editor = UnityEditorInternal.ProfilerDriver.profileEditor;
            UnityEditorInternal.ProfilerDriver.profileEditor = true; UnityEngine.Profiling.Profiler.enabled = true;
            measuringFrames = true;
            using ProfilerRecorder recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 16);
            using ElementCatalogViewModel model = new ElementCatalogViewModel(new ElementCatalog(scalePacked.Select(item => item.ToDefinition())));
            try
            {
                yield return null; yield return null; yield return null;
                byte[] calibration = new byte[65536]; GC.KeepAlive(calibration); yield return null;
                Check(recorder.Valid && recorder.UnitType.ToString() == "Bytes" && recorder.LastValue >= calibration.Length,
                    "프레임 바이트 카운터64KB 실제 할당 교정");
                byte[] bytes = MemoryPackSerializer.Serialize(scalePacked);
                ElementCatalog catalog = new ElementCatalog(scalePacked.Select(item => item.ToDefinition()));
                Action[] actions = { () => model.SetSearch("scale.499"),
                    () => { foreach (PackedElementDefinition item in scalePacked) item.ToDefinition(); },
                    () => MemoryPackSerializer.Serialize(scalePacked),
                    () => new ElementCatalog(MemoryPackSerializer.Deserialize<PackedElementDefinition[]>(bytes).Select(item => item.ToDefinition())),
                    () => { for (int i = 0; i < 500; i++) catalog.Get(new ElementId("phase05.scale." + i.ToString("D3"))); } };
                string[] names = { "search500", "validate500", "memorypack500", "restore500", "lookup500" };
                for (int operation = 0; operation < actions.Length; operation++)
                {
                    actions[operation](); yield return null;
                    for (int sample = 0; sample < 5; sample++)
                    {
                        Stopwatch timer = Stopwatch.StartNew(); actions[operation](); timer.Stop();
                        double milliseconds = timer.Elapsed.TotalMilliseconds; yield return null;
                        ScaleMeasurements.Add(names[operation] + "\tsample=" + sample + "\twarmup=1\tms=" +
                            milliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + "\twholeEditorFrameBytes=" + recorder.LastValue);
                    }
                }
                IEnumerator authoring = MeasureAuthoringFrames(recorder);
                try { while (authoring.MoveNext()) yield return authoring.Current; }
                finally { (authoring as IDisposable)?.Dispose(); }
                Check(model.Visible.Single().Id.Value == "phase05.scale.499", "측정용 검색 상태는 실제500개 정의에서499 선택");
            }
            finally { measuringFrames = false; UnityEngine.Profiling.Profiler.enabled = enabled; UnityEditorInternal.ProfilerDriver.profileEditor = editor; }
        }
        private static IEnumerator MeasureAuthoringFrames(ProfilerRecorder recorder)
        {
            // 각 표본은 소유한 임시 카탈로그에 실제 제작 입력500개를 저장하고 내린 뒤 다시 읽는다.
            for (int sample = -1; sample < 5; sample++)
            {
                string path = ScaleFolder + "/AuthoringSample.asset";
                ElementCatalogAsset source = ScriptableObject.CreateInstance<ElementCatalogAsset>();
                Stopwatch timer = Stopwatch.StartNew();
                AssetDatabase.CreateAsset(source, path);
                using (SerializedObject input = new SerializedObject(source))
                {
                    SerializedProperty entries = input.FindProperty("definitions");
                    entries.arraySize = scalePacked.Length;
                    for (int i = 0; i < scalePacked.Length; i++)
                    {
                        ElementDefinitionAsset definition = ScriptableObject.CreateInstance<ElementDefinitionAsset>();
                        definition.name = scalePacked[i].id;
                        JsonUtility.FromJsonOverwrite("{\"definition\":" + JsonUtility.ToJson(scalePacked[i]) + "}", definition);
                        AssetDatabase.AddObjectToAsset(definition, source);
                        entries.GetArrayElementAtIndex(i).objectReferenceValue = definition;
                    }
                    input.ApplyModifiedPropertiesWithoutUndo();
                }
                timer.Stop();
                double milliseconds = timer.Elapsed.TotalMilliseconds;
                yield return null;
                if (sample >= 0)
                    ScaleMeasurements.Add("author500\tsample=" + sample + "\twarmup=1\tms=" +
                        milliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) +
                        "\twholeEditorFrameBytes=" + recorder.LastValue);

                timer.Restart();
                AssetDatabase.SaveAssetIfDirty(source);
                timer.Stop(); milliseconds = timer.Elapsed.TotalMilliseconds;
                yield return null;
                if (sample >= 0)
                    ScaleMeasurements.Add("save500\tsample=" + sample + "\twarmup=1\tms=" +
                        milliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) +
                        "\twholeEditorFrameBytes=" + recorder.LastValue);
                Check(File.Exists(path) && AssetDatabase.LoadAllAssetsAtPath(path).OfType<ElementDefinitionAsset>().Count() == 500,
                    "반복 제작 표본 " + sample + " 실제500개 하위 에셋 저장");
                string disk = File.ReadAllText(path), guid = AssetDatabase.AssetPathToGUID(path);
                foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path)) Resources.UnloadAsset(asset);
                source = null;
                yield return null;

                timer.Restart();
                source = AssetDatabase.LoadAssetAtPath<ElementCatalogAsset>(path);
                ElementCatalog restored = source.CreateCatalog();
                timer.Stop(); milliseconds = timer.Elapsed.TotalMilliseconds;
                yield return null;
                if (sample >= 0)
                    ScaleMeasurements.Add("reload500\tsample=" + sample + "\twarmup=1\tms=" +
                        milliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) +
                        "\twholeEditorFrameBytes=" + recorder.LastValue);
                Check(restored.Count == 500 && restored.Definitions.Select(item => JsonUtility.ToJson(PackedElementDefinition.FromDefinition(item)))
                    .SequenceEqual(scalePacked.Select(item => JsonUtility.ToJson(item))),
                    "반복 제작 표본 " + sample + " 실제 재로드500개 전체 값/참조 일치");
                Check(AssetDatabase.AssetPathToGUID(path) == guid && File.ReadAllText(path) == disk,
                    "반복 제작 표본 " + sample + " 저장/재로드 GUID와 원문 유지");
                Check(AssetDatabase.DeleteAsset(path), "반복 제작 표본 " + sample + " 소유한 임시 카탈로그 정리");
                yield return null;
            }
        }

        private static void FinishScale(int exit)
        {
            EditorApplication.update -= TickScale;
            (scaleSequence as IDisposable)?.Dispose(); scaleSequence = null;
            scaleView?.Dispose(); scaleModel?.Dispose();
            if (scaleWindow != null) scaleWindow.Close();
            if (ownsScaleFolder) { AssetDatabase.DeleteAsset(ScaleFolder); ownsScaleFolder = false; }
            Directory.CreateDirectory("Logs/ElementFramework/Phase05");
            ScaleMeasurements.Insert(0, "Unity=" + Application.unityVersion + "\tmode=Editor\tdefinitions=500\tboard=9x9\tinitialSamples=1\tframeSamples=5\tframeWarmup=1\tallocation=calibrated whole Editor frame bytes; thread counter unavailable is not zero; assertions/logs included");
            File.WriteAllLines("Logs/ElementFramework/Phase05/catalog-scale-measurements.txt", ScaleMeasurements);
            File.WriteAllLines("Logs/ElementFramework/Phase05/catalog-scale-results.txt", Results);
            EditorApplication.Exit(exit);
        }
    }
}
