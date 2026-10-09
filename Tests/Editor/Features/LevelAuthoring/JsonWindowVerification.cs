using System;
using System.IO;
using System.Linq;
using LevelAuthoring.Documents;
using LevelAuthoring.Runtime;
using Newtonsoft.Json.Linq;
using System.Reflection;
using LevelAuthoring.Storage;
using Levels.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelAuthoring.Editor
{
    public static class JsonWindowVerification
    {
        public static void Run() => Check(false);
        public static void RunFull() => Check(true);
        private static void Check(bool full)
        {
            LevelEditorWindow window = null;
            try
            {
                var template = ScriptableObject.CreateInstance<Levels.LevelDefinition>();
                ContentDocument initial;
                try { initial = UnityAuthoringCodec.Write(template, "level", "level-window", _ => null, _ => null); }
                finally { UnityEngine.Object.DestroyImmediate(template); }
                var project = new ContentDocument("project", "project-window", new JObject
                {
                    ["name"] = "window test", ["contentVersion"] = 1, ["defaultCatalogId"] = JValue.CreateNull(),
                    ["resources"] = new JArray(), ["documents"] = new JArray(), ["sourceIds"] = new JArray()
                });
                var source = full ? new ContentSnapshotStore(File.ReadAllText("Logs/GameAuthoringStage02/latest-fixtures.txt")).Read().Snapshot : new ContentSnapshot(new[] { project, initial });
                string root = Path.GetFullPath("ContentData/Trials/game-authoring-stage-03/window-" + Guid.NewGuid().ToString("N"));
                new ContentSnapshotStore(root).Publish(source, null);
                window = ScriptableObject.CreateInstance<LevelEditorWindow>(); window.CreateGUI();
                MethodInfo open = typeof(LevelEditorWindow).GetMethod("OpenJsonWorkspace", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (open == null) throw new Exception("창의 JSON 작업 모드 미구현");
                Debug.Log("Window verification: open");
                typeof(LevelEditorWindow).GetMethod("ShowShapeRecommendations", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(window, null);
                open.Invoke(window, new object[] { root });
                if (window.rootVisualElement.Q<Button>("register-shape") != null) throw new Exception("JSON 전환 후 이전 SO 모양 콜백 잔류");
                typeof(LevelEditorWindow).GetMethod("SelectWorkspaceTab", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(window, new object[] { 4 });
                if ((int)typeof(LevelEditorWindow).GetProperty("WorkspaceTab", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(window) == 4) throw new Exception("JSON 모드에서 SO 여러 레벨 시험 진입 허용");
                if (window.rootVisualElement.Query<Button>("initial-pack-rebuild").ToList().Any(button => button.enabledSelf)) throw new Exception("JSON 모드의 SO 팩 갱신 차단 미연결");
                if (window.CurrentLevel == null || EditorUtility.IsPersistent(window.CurrentLevel)) throw new Exception("JSON 표시 레벨 누락 또는 원본 에셋 사용");
                string name = window.CurrentLevel.name;
                typeof(LevelEditorWindow).GetMethod("SaveJsonWorkspace", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Invoke(window, null);
                Debug.Log("Window verification: open");
                open.Invoke(window, new object[] { root });
                if (window.CurrentLevel.name != name) throw new Exception("저장 재열기 실패");
                MethodInfo duplicate = typeof(LevelEditorWindow).GetMethod("DuplicateJsonLevel", BindingFlags.NonPublic | BindingFlags.Instance);
                if (duplicate == null) throw new Exception("JSON 레벨 복제 미구현");
                Debug.Log("Window verification: duplicate");
                duplicate.Invoke(window, new object[] { 881993 });
                if (window.CurrentLevel.LevelNumber != 881993 || EditorUtility.IsPersistent(window.CurrentLevel)) throw new Exception("JSON 복제 대상 오류");
                typeof(LevelEditorWindow).GetMethod("UndoJson", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(window, new object[] { false });
                if (window.CurrentLevel.LevelNumber == 881993) throw new Exception("복제 Undo 실패");
                typeof(LevelEditorWindow).GetMethod("UndoJson", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(window, new object[] { true });
                if (window.CurrentLevel.LevelNumber != 881993) throw new Exception("복제 Redo 실패");
                typeof(LevelEditorWindow).GetMethod("SaveJsonWorkspace", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(window, null);
                typeof(LevelEditorWindow).GetMethod("CreateJsonLevel", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(window, new object[] { "신규" });
                if (window.CurrentLevel.Elements.Count != 0 || window.CurrentLevel.SchemaVersion != 5 || window.CurrentLevel.ElementSupply.sources.Count != 9) throw new Exception("새 JSON 레벨 기본값 오류");
                typeof(LevelEditorWindow).GetMethod("ApplyStroke", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(window,
                    new object[] { LevelBrush.Fixed, Levels.RabbitColor.Type1, new[] { new Board.BoardCoordinate(2, 2) } });
                if (window.CurrentLevel.Elements.Count != 1) throw new Exception("배치 실패");
                var workspace = (JsonAuthoringWorkspace)typeof(LevelEditorWindow).GetField("jsonWorkspace", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(window);
                var beforeCapture = workspace.Session.Documents.ToDictionary(doc => doc.Id);
                workspace.Capture("변경 없는 표시 수집");
                foreach (var doc in workspace.Session.Documents)
                    if (beforeCapture.TryGetValue(doc.Id, out var beforeDoc))
                        foreach (var field in beforeDoc.Data.Properties())
                            if (!JToken.DeepEquals(field.Value, doc.Data[field.Name])) Debug.Log("Display delta " + doc.Id + "." + field.Name + ": " + field.Value + " -> " + doc.Data[field.Name]);                typeof(LevelEditorWindow).GetMethod("UndoJson", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(window, new object[] { false });
                if (window.CurrentLevel.name != "신규" || window.CurrentLevel.Elements.Count != 0) throw new Exception("배치 동작 이력 미연결");
                typeof(LevelEditorWindow).GetMethod("UndoJson", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(window, new object[] { true });
                if (window.CurrentLevel.Elements.Count != 1) throw new Exception("배치 redo 실패");
                typeof(LevelEditorWindow).GetMethod("SetPlacementValue", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(window,
                    new object[] { "elements.Array.data[0].color", 1, JsonUtility.ToJson(window.CurrentLevel) });
                if ((string)workspace.Session.Get(workspace.Session.SelectedLevelId).Data["elements"][0]["color"] != "Type2") throw new Exception("단일 요소 속성 세션 반영 누락");
                typeof(LevelEditorWindow).GetMethod("UndoJson", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(window, new object[] { false });
                if (window.CurrentLevel.Elements[0].color != Levels.RabbitColor.Type1) throw new Exception("단일 요소 속성 Undo 실패");
                var editBoard = (LevelBoardView)typeof(LevelEditorWindow).GetField("board", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(window);
                editBoard.Brush = LevelBrush.Select; editBoard.Layer = Levels.PlacementLayer.Block;
                typeof(LevelEditorWindow).GetMethod("SelectCell", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(window, new object[] { new Board.BoardCoordinate(2, 2) });
                var deleteButton = window.rootVisualElement.Q<Button>("delete-placement");
                typeof(Clickable).GetMethod("Invoke", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(deleteButton.clickable, new object[] { null });
                if (((JArray)workspace.Session.Get(workspace.Session.SelectedLevelId).Data["elements"]).Count != 0) throw new Exception("선택 요소 삭제 세션 반영 누락");
                typeof(LevelEditorWindow).GetMethod("UndoJson", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(window, new object[] { false });
                if (window.CurrentLevel.Elements.Count != 1) throw new Exception("선택 요소 삭제 Undo 실패");
                typeof(LevelEditorWindow).GetMethod("SupplyAction", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(window,
                    new object[] { (Func<string>)(() => LevelSupplyEditing.PlaceSources(window.CurrentLevel, new[] { new Board.BoardCoordinate(0, 0) }, true)) });
                if (window.CurrentLevel.ElementSupply.sources.Count != 8) throw new Exception("생성구 삭제 실패");
                typeof(LevelEditorWindow).GetMethod("UndoJson", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(window, new object[] { false });
                if (window.CurrentLevel.ElementSupply.sources.Count != 9 || window.CurrentLevel.Elements.Count != 1) throw new Exception("공급 동작 이력 미연결");
                var panel = typeof(LevelEditorWindow).GetField("tutorialPanel", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(window);
                panel.GetType().GetMethod("Edit", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(panel, new object[] { (Action<SerializedProperty>)(tutorial => tutorial.FindPropertyRelative("steps").arraySize++) });
                if (window.CurrentLevel.Tutorial.steps.Count != 1) throw new Exception("튜토리얼 단계 추가 실패");
                var createFlow = panel.GetType().GetMethod("CreateJsonFlow", BindingFlags.NonPublic | BindingFlags.Instance);
                if (createFlow == null) throw new Exception("JSON 공유 흐름 생성 UI 미연결");
                createFlow.Invoke(panel, null);
                if (window.CurrentLevel.Tutorial.flow == null || EditorUtility.IsPersistent(window.CurrentLevel.Tutorial.flow)) throw new Exception("공유 흐름이 JSON 표시 사본이 아님");
                var commitFlow = typeof(LevelEditorWindow).GetMethod("CommitJsonFlow", BindingFlags.NonPublic | BindingFlags.Instance);
                if (commitFlow == null) throw new Exception("JSON 공통 원본 적용 미연결");
                var editedFlow = window.CurrentLevel.Tutorial.flow;
                string previousText = editedFlow.steps[0].instructions;
                commitFlow.Invoke(window, new object[] { editedFlow, (Action)(() => editedFlow.steps[0].instructions = "공유 안내 수정") });
                if (editedFlow.steps[0].instructions != "공유 안내 수정") throw new Exception("공유 원본 적용 실패");
                typeof(LevelEditorWindow).GetMethod("UndoJson", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(window, new object[] { false });
                if (editedFlow.steps[0].instructions != previousText) throw new Exception("공유 원본 Undo 실패");
                typeof(LevelEditorWindow).GetMethod("UndoJson", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(window, new object[] { false });
                if (window.CurrentLevel.Tutorial.flow != null || window.CurrentLevel.Tutorial.steps.Count != 1) throw new Exception("공유 흐름 생성 단일 Undo 실패");
                typeof(LevelEditorWindow).GetMethod("UndoJson", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(window, new object[] { false });
                if (window.CurrentLevel.Tutorial.steps.Count != 0 || window.CurrentLevel.Elements.Count != 1) throw new Exception("튜토리얼 단계 이력 미연결");
                var overlay = (LevelFlowOverlay)typeof(LevelEditorWindow).GetField("flowOverlay", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(window);
                overlay.Display(window.CurrentLevel, FlowTool.Path);
                var pathDraft = (System.Collections.Generic.List<Board.BoardCoordinate>)typeof(LevelFlowOverlay).GetField("draft", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(overlay);
                pathDraft.Add(new Board.BoardCoordinate(2, 2)); pathDraft.Add(new Board.BoardCoordinate(3, 2));
                overlay.Complete();
                if (window.CurrentLevel.Flow.Paths.Count == 0) throw new Exception("흐름 경로 추가 실패");
                typeof(LevelEditorWindow).GetMethod("UndoJson", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(window, new object[] { false });
                if (window.CurrentLevel.Flow.Paths.Count != 0 || window.CurrentLevel.Elements.Count != 1) throw new Exception("흐름 경로 이력 미연결");
                typeof(LevelEditorWindow).GetMethod("ShowShapeRecommendations", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(window, null);
                if (window.rootVisualElement.Q<Button>("register-json-shape") == null) throw new Exception("JSON 모양 목록이 SO 경로를 사용함");
                // 전체 fixture에 같은 모양이 이미 있어도 새 등록의 동작을 검증한다.
                var shapeMask = workspace.Session.Get(workspace.Session.SelectedLevelId).Data;
                shapeMask["board"]["cells"][80]["isActive"] = false;
                workspace.Session.Apply("모양 시험", docs => docs[workspace.Session.SelectedLevelId].Data["board"] = shapeMask["board"].DeepClone());
                workspace.RestoreDisplay();
                string shapeId = Editing.ShapeDocumentEditing.Register(workspace.Session, workspace.Session.SelectedLevelId, "창 시험 모양", Array.Empty<string>());
                var newTemplate = (JObject)typeof(LevelEditorWindow).GetMethod("CreateJsonLevelTemplate", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(window, null);
                Editing.ShapeDocumentEditing.ApplyToNewLevel(newTemplate, workspace.Session.Get(shapeId).Data);
                if ((bool)newTemplate["board"]["cells"][80]["isActive"] || ((JArray)newTemplate["elementSupply"]["sources"]).Count != 9) throw new Exception("모양 새 레벨 마스크/공급 오류");
                workspace.Undo(); workspace.Undo();
                typeof(LevelEditorWindow).GetMethod("Refresh", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(window, null);
                if (!window.hasUnsavedChanges) throw new Exception("창 닫기 미저장 보호 미연결");
                typeof(LevelEditorWindow).GetMethod("OnDisable", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(window, null);
                if (typeof(LevelEditorWindow).GetField("jsonWorkspace", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(window) != null) throw new Exception("비활성화 시 표시 사본 누수");
                typeof(LevelEditorWindow).GetMethod("OnEnable", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(window, null);
                if (window.CurrentLevel.name != "신규") throw new Exception("재활성화 초안 유실");
                MethodInfo createRequest = typeof(LevelEditorWindow).GetMethod("CreateJsonPlayRequest", BindingFlags.NonPublic | BindingFlags.Instance);
                if (createRequest == null) throw new Exception("JSON 현재 문서 게임 입력 미연결");
                JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":0,\"color\":0,\"count\":3}]}", window.CurrentLevel);
                var request = (GameScreen.PuzzlePlayRequest)createRequest.Invoke(window, new object[] { 481 });
                var launch = GameScreen.Editor.PuzzleEditorLaunchRequest.FromJson(request, Tutorial.TutorialRunMode.Always);
                if (launch.Source != GameScreen.Editor.PuzzleEditorLevelSource.Json || launch.TutorialMode != Tutorial.TutorialRunMode.Always) throw new Exception("JSON 시험 요청 정책 불일치");
                var runtimeLevel = launch.CreateDefinition();
                try
                {
                    if (request.Seed != 481 || runtimeLevel.LevelNumber != window.CurrentLevel.LevelNumber || runtimeLevel.Elements.Count != 1)
                        throw new Exception("JSON 게임 입력 스냅샷 불일치");
                }
                finally { UnityEngine.Object.DestroyImmediate(runtimeLevel); }
                typeof(LevelEditorWindow).GetMethod("SaveJsonWorkspace", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(window, null);
                UnityEngine.Object.DestroyImmediate(window); window = null;
                File.WriteAllText(full ? "Logs/GameAuthoringStage03/window-full-results.txt" : "Logs/GameAuthoringStage03/window-results.txt", "PASS window opens JSON transient level\nPASS window saves and reopens JSON\nPASS duplicate undo redo\nPASS new level defaults\nPASS placement undo redo\nPASS disable cleanup\nPASS restore draft\nPASS window closes\n");
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                if (window != null) UnityEngine.Object.DestroyImmediate(window);
                Debug.LogException(error); EditorApplication.Exit(1);
            }
        }
    }
}
