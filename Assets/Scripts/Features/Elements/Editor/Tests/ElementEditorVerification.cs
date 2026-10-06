using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Board;
using Levels;
using Levels.Editor;
using UnityEditor;
using UnityEngine;

namespace Elements.Editor
{
    public static partial class ElementEditorVerification
    {
        private static readonly List<string> Results = new List<string>();
        private static readonly List<ScriptableObject> Owned = new List<ScriptableObject>();
        private static void Check(bool condition, string label)
        { if (!condition) throw new InvalidOperationException(label); Results.Add("PASS " + label); }
        private static BoardCoordinate C(int row, int column) => new BoardCoordinate(row, column);
        private static ElementDefinition Body(string id, int size, int maximum) => new ElementDefinition(new ElementId(id), id,
            new ElementPlacementProfile(size, maximum), null, new ElementDamageSourcePolicy(true, true, false, true), null,
            new ElementDamageAggregationPolicy(true), new ElementRemovalMissionProfile(MissionKind.Crate), ElementReactionBehavior.Durability);

        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 Editor에서만 실행한다.");
            Results.Clear(); Owned.Clear(); int exit = 0;
            try
            {
                ElementDefinition first = Body("body.editor.first", 2, 9), second = Body("body.editor.second", 1, 13);
                ElementCatalog catalog = new ElementCatalog(new[] { first, second });
                Type modelType = typeof(ElementEditorVerification).Assembly.GetType("Elements.Editor.ElementCatalogViewModel");
                Check(modelType != null, "카탈로그 화면 상태 ViewModel 계약 존재");
                object model = Activator.CreateInstance(modelType, catalog); int events = 0;
                modelType.GetEvent("Changed").AddEventHandler(model, new Action(() => events++));
                Func<string, object> property = name => modelType.GetProperty(name).GetValue(model);
                Action<string, object> command = (name, value) => modelType.GetMethod(name).Invoke(model, new[] { value });
                command("Select", first.Id); command("SetSearch", "SECOND");
                Check(((IEnumerable<ElementDefinition>)property("Visible")).Single().Id.Equals(second.Id), "ID 검색은 대소문자와 무관");
                Check(((ElementDefinition)property("SelectedDefinition")).Id.Equals(first.Id) && !(bool)property("CanPlace"), "검색으로 숨긴 선택은 보존하고 배치 명령은 비활성");
                command("SetSearch", "");
                Check((bool)property("CanPlace"), "검색 초기화 후 기존 선택 복원");
                command("SetLayer", PlacementLayer.Block);
                Check(!((IEnumerable<ElementDefinition>)property("Visible")).Any() && !(bool)property("CanPlace"), "층 필터는 선택 정의의 프로필로 판정");
                command("SetLayer", null);
                Check((bool)property("CanPlace") && events >= 5, "필터 해제와 화면 상태 변경 통지");
                ElementDefinition invalid = new ElementDefinition(new ElementId("body.editor.invalid"), "invalid", new ElementPlacementProfile(1, 2),
                    null, null, null, null, null, ElementReactionBehavior.Durability);
                modelType.GetMethod("SetCatalog").Invoke(model, new object[] { new ElementCatalog(new[] { first, second, invalid }) });
                command("Select", invalid.Id);
                Check(!(bool)property("CanPlace") && ((string)property("SelectionError")).Contains(invalid.Id.Value), "잘못된 정의 프로필은 ID를 포함한 검사 결과와 배치 비활성");
                int beforeClose = events; ((IDisposable)model).Dispose(); command("SetSearch", "first");
                Check(events == beforeClose && !(bool)property("CanPlace"), "종료한 ViewModel은 구독과 배치 명령 해제");

                LevelDefinition level = ScriptableObject.CreateInstance<LevelDefinition>(); Owned.Add(level);
                JsonUtility.FromJsonOverwrite("{\"schemaVersion\":5,\"missions\":[{\"kind\":1,\"count\":1}]}", level);
                ElementCatalogAsset sourceCatalog = ScriptableObject.CreateInstance<ElementCatalogAsset>(); Owned.Add(sourceCatalog);
                List<ElementDefinitionAsset> assets = new List<ElementDefinitionAsset>();
                foreach (ElementDefinition definition in catalog.Definitions)
                {
                    ElementDefinitionAsset asset = ScriptableObject.CreateInstance<ElementDefinitionAsset>(); Owned.Add(asset);
                    typeof(ElementDefinitionAsset).GetField("definition", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(asset, PackedElementDefinition.FromDefinition(definition)); assets.Add(asset);
                }
                typeof(ElementCatalogAsset).GetField("definitions", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(sourceCatalog, assets);
                using (SerializedObject data = new SerializedObject(level))
                { data.FindProperty("elementCatalog").objectReferenceValue = sourceCatalog; data.ApplyModifiedPropertiesWithoutUndo(); }
                string original = JsonUtility.ToJson(level);
                Type editing = typeof(ElementEditorVerification).Assembly.GetType("Elements.Editor.ElementPlacementEditing");
                Check(editing != null, "정의 ID의 실제 편집 응용 경계 존재");
                MethodInfo brushFactory = editing.GetMethod("ForDefinition");
                PlacementBrush brush = (PlacementBrush)brushFactory.Invoke(null, new object[] { level, first.Id }); brush.Durability = 7;
                Check(LevelObstacleEditing.Apply(level, brush, new[] { C(2, 2) }).Changed == 1 && level.Elements.Count == 1 &&
                    level.Elements[0].definitionId == first.Id.Value && level.Elements[0].durability == 7, "새 ID와 실제 최대9의 내구도7을 원본 새 목록에 배치");
                string placed = JsonUtility.ToJson(level); string instance = level.Elements[0].instanceId;
                Undo.FlushUndoRecordObjects(); Undo.PerformUndo(); Check(JsonUtility.ToJson(level) == original, "ID 배치 Undo 원문 복원");
                Undo.PerformRedo(); Check(JsonUtility.ToJson(level) == placed, "ID 배치 Redo 원문 복원");
                Check(LevelObstacleEditing.Move(level, 0, C(4, 4), out string move) && level.Elements[0].coordinate.Equals(C(4, 4)) &&
                    level.Elements[0].instanceId == instance, "실제 정의 크기2의 이동은 본체 ID 유지 " + move);
                PlacementBrush replacement = (PlacementBrush)brushFactory.Invoke(null, new object[] { level, second.Id }); replacement.ReplaceExisting = true; replacement.Durability = 11;
                Check(LevelObstacleEditing.Apply(level, replacement, new[] { C(5, 5) }).Changed == 1 && level.Elements.Count == 1 &&
                    level.Elements[0].definitionId == second.Id.Value && level.Elements[0].coordinate.Equals(C(4, 4)) &&
                    level.Elements[0].instanceId != instance, "2×2 내부 클릭의 새 ID 교체는 기준 칸 유지와 새 본체 ID");
                PlacementBrush normal = (PlacementBrush)brushFactory.Invoke(null, new object[] { level, LegacyElementMap.Get(SupplyKind.FixedNormal) });
                normal.Color = RabbitColor.Type2; normal.ReplaceExisting = true;
                Check(LevelObstacleEditing.Apply(level, normal, new[] { C(4, 4) }).Changed == 1 && level.Elements.Single().layer == PlacementLayer.Block,
                    "장애물→일반 블록 교체는 같은 점유 영역으로 처리");
                brush.ReplaceExisting = true;
                Check(LevelObstacleEditing.Apply(level, brush, new[] { C(4, 4) }).Changed == 1 && level.Elements.Single().layer == PlacementLayer.Obstacle,
                    "일반 블록→장애물 교체는 ID 원본에 처리");
                string beforeInvalid = JsonUtility.ToJson(level); brush.Durability = 10;
                Check(LevelObstacleEditing.Apply(level, brush, new[] { C(4, 4) }).Changed == 0 && JsonUtility.ToJson(level) == beforeInvalid,
                    "정의 최대값 밖 입력은 원본 변경 없이 거절");
                Check(LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Obstacle, Erase = true }, new[] { C(5, 5) }).Changed == 1 &&
                    level.Elements.Count == 0, "2×2 내부 칸 지우기로 본체 전체 삭제");
                brush.Durability = 7; brush.ReplaceExisting = false;
                Check(LevelObstacleEditing.Apply(level, brush, new[] { C(2, 2) }).Changed == 1 &&
                    LevelObstacleEditing.Apply(level, replacement, new[] { C(6, 6) }).Changed == 1, "공통 설정 검사 대상 새 ID 두 종류 배치");
                bool firstFound = LevelCommonEditing.TrySelect(level, PlacementLayer.Obstacle, C(3, 3), out PlacementSelection firstSelection);
                bool secondFound = LevelCommonEditing.TrySelect(level, PlacementLayer.Obstacle, C(6, 6), out PlacementSelection secondSelection);
                Check(firstFound && secondFound,
                    "새 ID의 본체 내부 선택과 공통 설정 대상 조회");
                string settings = LevelCommonEditing.Copy(level, firstSelection), beforeSettings = JsonUtility.ToJson(level);
                Check(settings != null && settings.Contains(first.Id.Value), "설정 복사는 종류 enum 대신 영구 정의 ID 포함");
                Check(LevelCommonEditing.Paste(level, new[] { secondSelection }, settings, out int pasted, out int excluded) != null &&
                    pasted == 0 && excluded == 1 && JsonUtility.ToJson(level) == beforeSettings, "같은 행동이지만 다른 ID에는 설정 붙여넣기 제외");
                Check(LevelCommonEditing.Set(level, new[] { secondSelection }, "durability", 12, out int edited) == null && edited == 1 &&
                    level.Elements[1].durability == 12, "새 ID 실제 최대13 범위의 공통 속성 변경");
                Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
                Check(JsonUtility.ToJson(level) == beforeSettings, "새 ID 공통 설정도 Undo 한 번으로 원문 복원");
                Check(typeof(LevelAssetOperations).GetMethod("CreateElementAtPath") != null, "새 제작 레벨의 명시적 스키마5 생성 경계");
                string fixture = "Assets/__ElementEditorVerification_" + Guid.NewGuid().ToString("N");
                AssetDatabase.CreateFolder("Assets", System.IO.Path.GetFileName(fixture));
                try
                {
                    LevelDefinition created = LevelAssetOperations.CreateElementAtPath(fixture + "/New.asset");
                    string createdJson = JsonUtility.ToJson(created), guid = AssetDatabase.AssetPathToGUID(fixture + "/New.asset");
                    Check(created.SchemaVersion == 5 && created.ElementSupply.sources.Count == 9 && created.Supply.Sources.Count == 0 &&
                        !EditorUtility.IsDirty(created), "실제 새 에셋은 스키마5와 신형 상단 생성구9개만 저장");
                    AssetDatabase.ImportAsset(fixture + "/New.asset", ImportAssetOptions.ForceUpdate);
                    Check(JsonUtility.ToJson(AssetDatabase.LoadAssetAtPath<LevelDefinition>(fixture + "/New.asset")) == createdJson &&
                        AssetDatabase.AssetPathToGUID(fixture + "/New.asset") == guid, "새 에셋 저장·재로드의 JSON과 GUID 보존");
                    Undo.ClearUndo(created);
                }
                finally { AssetDatabase.DeleteAsset(fixture); }
                PlacementBrush generator = ElementPlacementEditing.ForDefinition(level, LegacyElementMap.Get(ObstacleKind.Generator));
                Check(LevelObstacleEditing.Apply(level, generator, new[] { C(0, 0) }).Changed == 1, "실제 발전기 ID 배치");
                string generatorInstance = level.Elements.Single(item => item.definitionId == generator.DefinitionId).instanceId;
                string targetInstance = level.Elements.Single(item => item.definitionId == first.Id.Value).instanceId;
                string connectionError = LevelConnectionEditing.ConnectAuto(level, generatorInstance, targetInstance, C(0, 1));
                Check(connectionError == null && level.Connections.Single().TargetId == targetInstance,
                    "새 정의 본체 ID를 대상으로 실제 전선 자동 연결 / " + connectionError);
                Check(LevelConnectionRules.WireError(level, generatorInstance, targetInstance, level.Connections[0].Vertices, 0) == null,
                    "새 정의 크기2의 단자와 내부 점유로 실제 전선 검사");
                string connected = JsonUtility.ToJson(level);
                Check(ElementPlacementEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Obstacle, Erase = true }, new[] { C(3, 3) }).Changed == 1 &&
                    level.Connections.Count == 0, "본체 내부 칸 삭제는 해당 인스턴스의 연결도 함께 제거");
                Undo.FlushUndoRecordObjects(); Undo.PerformUndo(); Check(JsonUtility.ToJson(level) == connected, "본체·연결 삭제는 하나의 Undo로 원문 복원");
                Check(LevelSupplyEditing.PlaceSources(level, new[] { C(0, 4) }) == null && level.ElementSupply.sources.Count == 1 && level.Supply.Sources.Count == 0,
                    "신형 생성구 배치는 새 공급 목록만 수정");
                ElementDefinition supplyDefinition = ElementDefinition.CreateSupply(new ElementId("source.editor.second"), "second supply",
                    ElementSupplyProfile.ForObstacle(second.Id, "editor-second-"));
                ElementDefinitionAsset supplyAsset = ScriptableObject.CreateInstance<ElementDefinitionAsset>(); Owned.Add(supplyAsset);
                typeof(ElementDefinitionAsset).GetField("definition", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(supplyAsset, PackedElementDefinition.FromDefinition(supplyDefinition));
                assets.Add(supplyAsset);
                typeof(ElementCatalogAsset).GetField("definitions", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(sourceCatalog, assets);
                Check(LevelSupplyEditing.SetSourceProperty(level, new[] { 0 }, "mode", (int)SupplyMode.Fixed) == null,
                    "신형 생성구의 고정 공급 방식 변경");
                ElementSupplyItemDefinition supplyItem = new ElementSupplyItemDefinition { definitionId = supplyDefinition.Id.Value, count = 3, durability = 12 };
                Check(ElementSupplyEditing.SetItems(level, 0, new[] { supplyItem }) == null && level.ElementSupply.sources[0].items.Single().definitionId == supplyDefinition.Id.Value &&
                    level.ElementSupply.sources[0].items[0].durability == 12, "새 공급 ID의 실제 본체 최대13 범위로 고정 목록 편집");
                string supplied = JsonUtility.ToJson(level), supplyClipboard = LevelSupplyEditing.Copy(level, 0);
                supplyItem.durability = 14;
                Check(ElementSupplyEditing.SetItems(level, 0, new[] { supplyItem }) != null && JsonUtility.ToJson(level) == supplied,
                    "새 공급 ID의 범위 밖 내구도는 원본 변경 없이 거절");
                Check(supplyClipboard.Contains(supplyDefinition.Id.Value) && ElementSupplyEditing.SetItems(level, 0, Array.Empty<ElementSupplyItemDefinition>()) == null &&
                    LevelSupplyEditing.Paste(level, new[] { 0 }, supplyClipboard, false) == null && JsonUtility.ToJson(level) == supplied,
                    "공급 클립보드는 영구 ID로 수량·내구도 원문 복원");
                LevelDefinition foreign = ScriptableObject.CreateInstance<LevelDefinition>(); Owned.Add(foreign);
                JsonUtility.FromJsonOverwrite("{\"schemaVersion\":5}", foreign);
                LevelSupplyEditing.PlaceSources(foreign, new[] { C(0, 4) }); LevelSupplyEditing.SetSourceProperty(foreign, new[] { 0 }, "mode", (int)SupplyMode.Fixed);
                string foreignBefore = JsonUtility.ToJson(foreign);
                Check(LevelSupplyEditing.Paste(foreign, new[] { 0 }, supplyClipboard, false) != null && JsonUtility.ToJson(foreign) == foreignBefore,
                    "공급 정의가 없는 대상 레벨에는 대체 블록 없이 붙여넣기 거절");
                Check(LevelSupplyEditing.PlaceSources(level, new[] { C(0, 4) }, true) == null && level.ElementSupply.sources.Count == 0,
                    "신형 생성구 삭제는 새 공급 목록에 적용");
                Undo.FlushUndoRecordObjects(); Undo.PerformUndo(); Check(JsonUtility.ToJson(level) == supplied, "생성구·고정 목록 삭제도 하나의 Undo로 원문 복원");
                Check(LevelSupplyEditing.PlaceRecovery(foreign, new[] { C(4, 4) }, false) == null && foreign.Elements.Single().definitionId == foreign.ElementSupply.recoveryDefinitionId,
                    "회수 배치 도구는 신형 유지 공급 ID를 사용");
                string recoveryPlaced = JsonUtility.ToJson(foreign);
                Check(LevelSupplyEditing.PlaceRecovery(foreign, new[] { C(4, 4) }, true) == null && foreign.Elements.Count == 0, "신형 회수 삭제 도구는 회수 본체 제거");
                Undo.FlushUndoRecordObjects(); Undo.PerformUndo(); Check(JsonUtility.ToJson(foreign) == recoveryPlaced, "회수 삭제는 Undo로 새 ID 복원");
                string maintenanceBefore = JsonUtility.ToJson(level);
                Check(ElementSupplyEditing.SetMaintenanceProperty(level, "scrapDurability", 6) != null && JsonUtility.ToJson(level) == maintenanceBefore,
                    "유지 공급 내구도는 실제 고철 프로필 범위로 거절");
                Check(ElementSupplyEditing.SetMaintenanceDefinition(level, "scrapDefinitionId", supplyDefinition.Id) != null && JsonUtility.ToJson(level) == maintenanceBefore,
                    "고철 유지 정의에 다른 미션 본체를 지정하면 원본 보존");
                Check(ElementSupplyEditing.SetMaintenanceProperty(level, "scrapLimit", 4) == null && level.ElementSupply.scrapLimit == 4 && level.Supply.ScrapLimit == 0,
                    "유지 공급 설정은 신형 원본만 변경");
                Undo.FlushUndoRecordObjects(); Undo.PerformUndo(); Check(JsonUtility.ToJson(level) == maintenanceBefore, "유지 공급 설정도 Undo로 원문 복원");
                Simulation.LevelStateBuildResult observed = Simulation.LevelStateBuilder.Build(level, 12345);
                Check(observed.IsBuilt, "신규 편집 ID의 실행 상태는 정의 관찰 기록 가능");
                string definitionObservation = (string)typeof(LevelInitialStateVerification).GetMethod("SnapshotWithDefinitions", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { observed.State });
                File.WriteAllText("Logs/ElementFramework/Phase03/editor-runtime-definitions.txt", definitionObservation);
                Check(definitionObservation.Contains(first.Id.Value) && definitionObservation.Contains("MaxDurability=Int32:9"),
                    "기존 논리 스냅샷과 별도로 선택한 실제 ID·정의 수치 원문 기록");
                LevelDefinition metadataLevel = ScriptableObject.CreateInstance<LevelDefinition>(); Owned.Add(metadataLevel);
                string metadataJson = JsonUtility.ToJson(metadataLevel);
                string oldJson = metadataJson.Substring(0, metadataJson.IndexOf(",\"elements\":", StringComparison.Ordinal)) + "}";
                for (int depth = 0; depth <= 2; depth++)
                {
                    Check(RecordedLogicComparison.WithoutDefaultAuthoringMetadata(metadataJson) == oldJson,
                        "구형 빈 제작 필드 분리는 JSON 중첩 깊이" + depth + "에서 원래 필드 보존");
                    metadataJson = metadataJson.Replace("\\", "\\\\").Replace("\"", "\\\"");
                    oldJson = oldJson.Replace("\\", "\\\\").Replace("\"", "\\\"");
                }
                JsonUtility.FromJsonOverwrite("{\"elementSupply\":{\"scrapLimit\":1}}", metadataLevel);
                metadataJson = JsonUtility.ToJson(metadataLevel);
                Check(RecordedLogicComparison.WithoutDefaultAuthoringMetadata(metadataJson) == metadataJson,
                    "신규 제작 필드 값이 기본값과 다르면 비교에서 제거하지 않음");
            }
            catch (Exception error) { Results.Add("FAIL " + error); exit = 1; }
            finally
            {
                foreach (ScriptableObject item in Owned) { Undo.ClearUndo(item); UnityEngine.Object.DestroyImmediate(item); }
                File.WriteAllLines("Logs/ElementFramework/Phase03/editor-results.txt", Results); EditorApplication.Exit(exit);
            }
        }
    }
}
