using System;
using System.Collections;
using System.IO;
using System.Linq;
using Levels;
using Levels.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Elements.Editor
{
    public static partial class ElementEditorVerification
    {
        private static LevelEditorWindow window;
        private static LevelDefinition uiLevel;
        private static IEnumerator sequence;
        private static double nextTick;
        private static double deadline;
        private const string UIBodyId = "body.editor.ui.first";
        private const string UIFixture = "Assets/__ElementEditorUIVerification";
        private static bool ownsUIFixture;
        public static void StartUI()
        {
            Results.Clear(); Owned.Clear();
            ownsUIFixture = false;
            if (AssetDatabase.IsValidFolder(UIFixture) || Directory.Exists(UIFixture)) throw new InvalidOperationException("UI 검사 경로가 이미 있습니다.");
            AssetDatabase.CreateFolder("Assets", "__ElementEditorUIVerification"); ownsUIFixture = true;
            uiLevel = ScriptableObject.CreateInstance<LevelDefinition>(); Owned.Add(uiLevel);
            JsonUtility.FromJsonOverwrite("{\"schemaVersion\":5,\"missions\":[{\"kind\":1,\"count\":1}]}", uiLevel);
            ElementDefinitionAsset bodyAsset = ScriptableObject.CreateInstance<ElementDefinitionAsset>(); Owned.Add(bodyAsset);
            typeof(ElementDefinitionAsset).GetField("definition", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(bodyAsset, PackedElementDefinition.FromDefinition(Body(UIBodyId, 2, 9)));
            ElementCatalogAsset catalogAsset = ScriptableObject.CreateInstance<ElementCatalogAsset>(); Owned.Add(catalogAsset);
            ElementDefinitionAsset supplyBodyAsset = ScriptableObject.CreateInstance<ElementDefinitionAsset>(); Owned.Add(supplyBodyAsset);
            typeof(ElementDefinitionAsset).GetField("definition", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(supplyBodyAsset, PackedElementDefinition.FromDefinition(Body("body.editor.ui.supply", 1, 13)));
            ElementDefinitionAsset supplyAsset = ScriptableObject.CreateInstance<ElementDefinitionAsset>(); Owned.Add(supplyAsset);
            typeof(ElementDefinitionAsset).GetField("definition", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(supplyAsset, PackedElementDefinition.FromDefinition(ElementDefinition.CreateSupply(new ElementId("supply.editor.ui.custom"), "custom UI supply",
                    ElementSupplyProfile.ForObstacle(new ElementId("body.editor.ui.supply"), "ui-supply-"))));
            typeof(ElementCatalogAsset).GetField("definitions", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(catalogAsset, new System.Collections.Generic.List<ElementDefinitionAsset> { bodyAsset, supplyBodyAsset, supplyAsset });
            using (SerializedObject source = new SerializedObject(uiLevel))
            { source.FindProperty("elementCatalog").objectReferenceValue = catalogAsset; source.ApplyModifiedPropertiesWithoutUndo(); }
            AssetDatabase.CreateAsset(bodyAsset, UIFixture + "/Body.asset");
            AssetDatabase.CreateAsset(supplyBodyAsset, UIFixture + "/SupplyBody.asset");
            AssetDatabase.CreateAsset(supplyAsset, UIFixture + "/Supply.asset");
            AssetDatabase.CreateAsset(catalogAsset, UIFixture + "/Catalog.asset");
            AssetDatabase.CreateAsset(uiLevel, UIFixture + "/Level.asset");
            window = ScriptableObject.CreateInstance<LevelEditorWindow>(); window.position = new Rect(20, 20, 1100, 800);
            window.ShowUtility(); window.SetLevel(uiLevel); window.Focus(); sequence = UI(); deadline = EditorApplication.timeSinceStartup + 45;
            EditorApplication.update += UITick;
        }
        private static void UITick()
        {
            if (EditorApplication.timeSinceStartup < nextTick) return;
            nextTick = EditorApplication.timeSinceStartup + 0.15;
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("실제 편집 창의 UI 검사 대기 시간 초과");
                if (window != null)
                {
                    window.Repaint();
                    LevelBoardView rendered = window.rootVisualElement.Q<LevelBoardView>();
                    if (rendered?.panel != null)
                    {
                        System.Reflection.MethodInfo layout = rendered.panel.GetType().GetMethod("ValidateLayout",
                            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                        if (layout == null) throw new InvalidOperationException("Unity 패널의 레이아웃 검사 경계가 없습니다.");
                        layout.Invoke(rendered.panel, null);
                    }
                    // Refresh 직후에는 레이아웃 좌표가 무효하다. 실제 패널 배치를 기다린 뒤 입력을 보낸다.
                    if (rendered == null || float.IsNaN(rendered.worldBound.width)) return;
                }
                if (!sequence.MoveNext()) FinishUI(0);
            }
            catch (Exception error) { Results.Add("FAIL " + error); Debug.LogException(error); FinishUI(1); }
        }
        private static void FinishUI(int exit)
        {
            EditorApplication.update -= UITick;
            if (window != null) window.Close();
            foreach (ScriptableObject item in Owned) { Undo.ClearUndo(item); if (!EditorUtility.IsPersistent(item)) UnityEngine.Object.DestroyImmediate(item); }
            if (ownsUIFixture) { AssetDatabase.DeleteAsset(UIFixture); ownsUIFixture = false; }
            File.WriteAllLines("Logs/ElementFramework/Phase03/editor-ui-results.txt", Results); EditorApplication.Exit(exit);
        }
        private static void Click(string name)
        {
            Button button = window.rootVisualElement.Q<Button>(name);
            if (button == null || !button.enabledInHierarchy) throw new InvalidOperationException("활성 버튼 없음: " + name);
            using NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled(); evt.target = button; button.SendEvent(evt);
        }
        private static void Pointer(EventType type, int row, int column, int count = 1)
        {
            LevelBoardView board = window.rootVisualElement.Q<LevelBoardView>();
            Event input = new Event { type = type, button = 0, clickCount = count,
                mousePosition = board.LocalToWorld(new Vector2(column * LevelBoardView.CellSize + 22, row * LevelBoardView.CellSize + 22)) };
            if (type == EventType.MouseDown)
            {
                using PointerDownEvent evt = PointerDownEvent.GetPooled(input);
                evt.target = board; board.SendEvent(evt);
            }
            else { using PointerUpEvent evt = PointerUpEvent.GetPooled(input); evt.target = board; board.SendEvent(evt); }
        }
        private static void GraphPointer(VisualElement target, EventType type, Vector2 world)
        {
            Event input = new Event { type = type, button = 0, mousePosition = world };
            if (type == EventType.MouseDown) { using PointerDownEvent evt = PointerDownEvent.GetPooled(input); evt.target = target; target.SendEvent(evt); }
            else if (type == EventType.MouseUp) { using PointerUpEvent evt = PointerUpEvent.GetPooled(input); evt.target = target; target.SendEvent(evt); }
            else { using PointerMoveEvent evt = PointerMoveEvent.GetPooled(input); evt.target = target; target.SendEvent(evt); }
        }
        private static IEnumerator UI()
        {
            yield return null; yield return null;
            VisualElement root = window.rootVisualElement; LevelBoardView board = root.Q<LevelBoardView>();
            Check(board.panel != null && root.Q<ElementCatalogView>() != null, "실제 편집 창의 신형 보드와 카탈로그 연결");
            string original = JsonUtility.ToJson(uiLevel); bool dirty = EditorUtility.IsDirty(uiLevel);
            root.Q<ToolbarSearchField>("element-catalog-search").value = "fixed";
            Click("definition-" + LegacyElementMap.Get(SupplyKind.FixedNormal).Value);
            Check(JsonUtility.ToJson(uiLevel) == original && EditorUtility.IsDirty(uiLevel) == dirty, "실제 검색·선택은 원본 JSON과 dirty 유지");
            Click("element-catalog-place"); yield return null;
            Check(board.Brush == LevelBrush.Placement && board.Placement.DefinitionId == LegacyElementMap.Get(SupplyKind.FixedNormal).Value,
                "카탈로그 배치 명령은 실제 보드 브러시 선택");
            Pointer(EventType.MouseDown, 2, 2); yield return null;
            Check(board.IsDragging && board.HasPointerCapture(PointerId.mousePointerId), "실제 포인터 눌림의 드래그와 캡처");
            Pointer(EventType.MouseUp, 2, 2); yield return null;
            Check(uiLevel.Elements.Count == 1 && uiLevel.Elements[0].definitionId == LegacyElementMap.Get(SupplyKind.FixedNormal).Value && uiLevel.InitialBlocks.Count == 0,
                "실제 포인터 배치는 새 ID 목록만 변경 / " + root.Q<Label>("operation-status")?.text);
            string placed = JsonUtility.ToJson(uiLevel);
            Undo.FlushUndoRecordObjects(); Undo.PerformUndo(); yield return null;
            Check(JsonUtility.ToJson(uiLevel) == original, "실제 창 배치 Undo 복원");
            Undo.PerformRedo(); yield return null;
            Check(JsonUtility.ToJson(uiLevel) == placed, "실제 창 배치 Redo 복원");
            root.Q<PopupField<string>>("placement-layer").index = (int)PlacementLayer.Obstacle; yield return null;
            root.Q<ToolbarSearchField>("element-catalog-search").value = "";
            string bodyId = UIBodyId;
            Click("definition-" + bodyId); Click("element-catalog-place"); yield return null;
            Pointer(EventType.MouseDown, 2, 2, 2); yield return null;
            Pointer(EventType.MouseUp, 2, 2, 2); yield return null;
            Check(uiLevel.Elements.Single().definitionId == bodyId, "실제 더블클릭 일반→장애물 교체");
            Click("tool-Select"); yield return null;
            Pointer(EventType.MouseDown, 2, 2); Pointer(EventType.MouseUp, 2, 2); yield return null;
            IntegerField durability = root.Q<IntegerField>("selected-durability");
            Check(durability != null && durability.label.Contains("9"), "선택 속성은 새 ID 실제 최대9 표시");
            durability.value = 2; yield return null;
            Check(uiLevel.Elements.Single().durability == 2, "실제 속성 입력은 새 원본과 Undo 연결");
            Check(root.Q<Label>("element-body-0").worldBound.width > LevelBoardView.CellSize && root.Q<Label>("used-count").text.StartsWith("1개 ID"),
                "실제 보드는 새 ID 크기2 표시와 사용 목록 유지");
            string generatorId = LegacyElementMap.Get(ObstacleKind.Generator).Value;
            root.Q<ToolbarSearchField>("element-catalog-search").value = "";
            Click("definition-" + generatorId); Click("element-catalog-place"); yield return null;
            Pointer(EventType.MouseDown, 0, 0); yield return null;
            Pointer(EventType.MouseUp, 0, 0); yield return null;
            Check(uiLevel.Elements.Count == 2, "실제 카탈로그의 발전기 다칸 배치");
            Click("tool-Select"); yield return null;
            Pointer(EventType.MouseDown, 0, 0); yield return null;
            LevelConnectionGraph graph = root.Q<LevelConnectionGraph>(); VisualElement port = root.Q("connection-port-1-0");
            Check(port != null, "실제 새 목록 인덱스와 발전기 연결점 표시");
            GraphPointer(port, EventType.MouseDown, port.worldBound.center); yield return null;
            VisualElement targetPort = root.Q("connection-port-0-0");
            Check(graph.IsDragging && targetPort != null, "실제 드래그 중 새 ID 대상 연결점 표시");
            Vector2 endpoint = targetPort.worldBound.center;
            GraphPointer(graph, EventType.MouseDrag, endpoint); yield return null;
            GraphPointer(graph, EventType.MouseUp, endpoint); yield return null;
            Check(uiLevel.Connections.Count == 1 && uiLevel.Connections[0].TargetId == uiLevel.Elements[0].instanceId,
                "실제 연결점 드래그가 새 ID 본체와 전선 저장");
            Pointer(EventType.MouseDown, 3, 3); yield return null;
            Click("delete-placement"); yield return null;
            Check(uiLevel.Elements.Count == 1 && uiLevel.Connections.Count == 0, "실제 다칸 내부 선택 삭제와 연결 동시 제거");
            Pointer(EventType.MouseDown, 0, 0); yield return null;
            Click("delete-placement"); yield return null;
            Check(uiLevel.Elements.Count == 0, "실제 발전기 삭제");
            Click("add-top-sources"); yield return null;
            Check(uiLevel.ElementSupply.sources.Count == 9 && uiLevel.Supply.Sources.Count == 0 && root.Q<Label>("supply-mark-0") != null,
                "실제 생성구 추가와 보드 표시는 새 공급 원본 사용");
            Click("source-list-0-0"); yield return null;
            root.Q<PopupField<string>>("source-mode").index = (int)SupplyMode.Fixed; yield return null;
            Click("add-supply-item"); yield return null;
            PopupField<string> supplyKind = root.Q<PopupField<string>>("supply-item-kind");
            Check(supplyKind != null, "실제 고정 공급 항목 상세는 정의 ID 선택 제공");
            supplyKind.value = supplyKind.choices.Single(value => value.Contains("[supply.editor.ui.custom]")); yield return null;
            IntegerField supplyDurability = root.Q<IntegerField>("supply-item-durability");
            Check(supplyDurability != null && supplyDurability.label.Contains("13"), "실제 신규 공급 ID의 본체 최대13 표시");
            supplyDurability.value = 12; yield return null;
            Check(uiLevel.ElementSupply.sources[0].items.Single().definitionId == "supply.editor.ui.custom" && uiLevel.ElementSupply.sources[0].items[0].durability == 12,
                "실제 공급 ID 선택과 내구도 입력은 새 목록만 변경");
            string validSupply = JsonUtility.ToJson(uiLevel);
            root.Q<IntegerField>("supply-item-durability").value = 14; yield return null;
            Check(JsonUtility.ToJson(uiLevel) == validSupply, "실제 공급 범위 밖 입력은 원본 변경 없이 거절");
            Click("duplicate-supply-item"); yield return null;
            Check(uiLevel.ElementSupply.sources[0].items.Count == 2, "실제 고정 목록 선택 항목 복제");
            Click("delete-supply-item"); yield return null;
            Check(uiLevel.ElementSupply.sources[0].items.Count == 1, "실제 고정 목록 선택 항목 삭제");
            Click("delete-sources"); yield return null;
            Check(uiLevel.ElementSupply.sources.Count == 8, "실제 선택 생성구 삭제");
            Undo.FlushUndoRecordObjects(); Undo.PerformUndo(); yield return null;
            Check(uiLevel.ElementSupply.sources.Count == 9 && uiLevel.ElementSupply.sources[0].items.Single().durability == 12, "실제 생성구 삭제 Undo는 항목까지 복원");
            string beforeSave = JsonUtility.ToJson(uiLevel); string savedGuid = AssetDatabase.AssetPathToGUID(UIFixture + "/Level.asset");
            Click("save-level"); yield return null;
            Check(!EditorUtility.IsDirty(uiLevel), "실제 저장 버튼은 선택 레벨만 저장");
            window.SetLevel(null);
            AssetDatabase.ImportAsset(UIFixture + "/Level.asset", ImportAssetOptions.ForceUpdate);
            uiLevel = AssetDatabase.LoadAssetAtPath<LevelDefinition>(UIFixture + "/Level.asset");
            window.SetLevel(uiLevel); yield return null;
            Check(JsonUtility.ToJson(uiLevel) == beforeSave && AssetDatabase.AssetPathToGUID(UIFixture + "/Level.asset") == savedGuid && uiLevel.ElementCatalog.CreateCatalog().Get(new ElementId("supply.editor.ui.custom")) != null,
                "실제 저장·재로드는 공급 ID·본체 프로필·카탈로그 참조와 GUID 보존");
            LevelDefinition malformed = ScriptableObject.CreateInstance<LevelDefinition>(); Owned.Add(malformed);
            JsonUtility.FromJsonOverwrite("{\"schemaVersion\":5,\"elements\":[{\"definitionId\":\"missing.ui.body\",\"instanceId\":\"invalid-body\",\"layer\":1,\"coordinate\":{\"row\":0,\"column\":0},\"durability\":1}],\"flow\":{\"walls\":[{\"a\":{\"row\":0,\"column\":0},\"b\":{\"row\":0,\"column\":1}}]}}", malformed);
            string invalidOriginal = JsonUtility.ToJson(malformed);
            window.SetLevel(malformed); yield return null;
            Check(JsonUtility.ToJson(malformed) == invalidOriginal && root.Q<Label>("board-art-badge").text.Contains("!"),
                "실제 누락 ID와 벽이 있는 레벨은 원본 보존과 오류 보드 표시");
            ElementCatalogViewModel model = (ElementCatalogViewModel)typeof(LevelEditorWindow).GetField("elementCatalogModel", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(window);
            window.Close(); window = null; model.SetSearch("ignored");
            Check(!model.CanPlace && model.Visible.Count == 0, "실제 창 닫기는 카탈로그 모델·구독 수명 종료");
        }
    }
}
