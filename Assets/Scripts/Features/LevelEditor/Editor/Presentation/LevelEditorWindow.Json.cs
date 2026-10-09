using System;
using System.IO;
using System.Linq;
using LevelAuthoring.Documents;
using LevelAuthoring.Editing;
using LevelAuthoring.Editor;
using LevelAuthoring.Storage;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    public sealed partial class LevelEditorWindow
    {
        [SerializeField] private string jsonFolder;
        [SerializeField] private string jsonEditState;
        private JsonAuthoringWorkspace jsonWorkspace;
        private bool jsonSwitching;
        private bool jsonCapturing;
        private VisualElement jsonControls;
        internal bool IsJsonMode => jsonWorkspace != null;

        internal void OpenJsonWorkspace(string folder)
        {
            if (IsJsonFlowDraft) throw new InvalidOperationException(JsonFlowDraftRestriction);
            if (!ConfirmJsonLeave()) return;
            string fullPath = Path.GetFullPath(folder);
            StoredContentSnapshot stored = new ContentSnapshotStore(fullPath).Read();
            string first = stored.Snapshot.Documents.Where(doc => doc.Kind == "level").OrderBy(doc => (int)doc.Data["levelNumber"]).FirstOrDefault()?.Id;
            if (first == null) throw new ContentFormatException("작업 폴더에 레벨이 없습니다.");
            var next = new JsonAuthoringWorkspace(new AuthoringEditSession(stored, first));
            try { _ = next.Level; } catch { next.Dispose(); throw; }
            JsonAuthoringWorkspace previous = jsonWorkspace;
            jsonWorkspace = next; jsonFolder = fullPath; jsonBackupMode = false;
            jsonSwitching = true;
            try { SetLevel(next.Level); PersistJsonState(); RebuildJsonControls(); }
            finally { jsonSwitching = false; previous?.Dispose(); }
        }
        internal void SaveJsonWorkspace()
        {
            if (jsonWorkspace == null) return;
            data?.ApplyModifiedPropertiesWithoutUndo();
            if (jsonBackupMode) throw new ContentFormatException("복구본은 원본에 덮어쓰지 않습니다. 다른 폴더에 사본 저장을 사용하세요.");
            jsonWorkspace.Capture("입력 확정");
            jsonWorkspace.Session.Save(new ContentSnapshotStore(jsonFolder));
            PersistJsonState(); UpdateSaveState();
        }
        private void PersistJsonState()
        {
            if (jsonWorkspace != null)
            {
                jsonEditState = jsonWorkspace.Session.ExportState();
                hasUnsavedChanges = jsonWorkspace.Session.IsDirty;
                saveChangesMessage = "JSON 작업 폴더의 변경 내용을 저장하시겠습니까? 저장 실패 시 창을 유지합니다.";
            }
        }
        private void RestoreJsonWorkspace()
        {
            if (jsonWorkspace != null || string.IsNullOrEmpty(jsonEditState)) return;
            try { jsonWorkspace = new JsonAuthoringWorkspace(AuthoringEditSession.Restore(jsonEditState)); level = jsonWorkspace.Level; jsonRestoreViewPending = true; }
            catch (Exception error) { Debug.LogError("JSON 편집 복구 실패: " + error.Message); }
        }
        public override void SaveChanges()
        {
            if (IsJsonMode) SaveJsonWorkspace();
            base.SaveChanges();
        }
        public override void DiscardChanges()
        {
            if (recommendationPanel != null) { recommendationPanel.Clear(); recommendationPanel.style.display = DisplayStyle.None; }
            refreshShapeUsage = null;
            if (IsJsonMode)
            {
                properties?.Unbind(); data?.Dispose(); data = null;
                jsonWorkspace.Session.Discard(); jsonWorkspace.RestoreDisplay();
                SetLevel(jsonWorkspace.Level); PersistJsonState(); RebuildJsonControls();
            }
            base.DiscardChanges();
        }
        private void ReleaseJsonWorkspace()
        {
            PersistJsonState(); jsonWorkspace?.Dispose(); jsonWorkspace = null;
        }
        private bool ConfirmJsonLeave()
        {
            if (jsonWorkspace == null) return true;
            rootVisualElement.focusController?.focusedElement?.Blur();
            data?.ApplyModifiedPropertiesWithoutUndo();
            jsonWorkspace.Capture("전환 전 입력 확정"); PersistJsonState();
            if (!jsonWorkspace.Session.IsDirty) return true;
            int choice = EditorUtility.DisplayDialogComplex("JSON 변경 내용", "현재 작업 폴더의 저장하지 않은 변경을 처리하세요.", "저장", "취소", "버리기");
            if (choice == 1) return false;
            if (choice == 0) SaveJsonWorkspace();
            return true;
        }
        private bool LeaveJsonFor(LevelDefinition target)
        {
            if (jsonSwitching || jsonWorkspace == null || jsonWorkspace.Owns(target)) return true;
            if (!ConfirmJsonLeave()) { workspaceLevel?.SetValueWithoutNotify(level); return false; }
            properties?.Unbind(); data?.Dispose(); data = null;
            jsonWorkspace.Dispose(); jsonWorkspace = null; jsonFolder = null; jsonEditState = null; jsonBackupMode = false; hasUnsavedChanges = false;
            RebuildJsonControls(); return true;
        }
        private void BuildJsonControls(VisualElement parent)
        {
            jsonControls = new VisualElement { name = "json-workspace-controls" };
            jsonControls.style.flexDirection = FlexDirection.Row;
            jsonControls.style.flexWrap = Wrap.Wrap;
            parent.Add(jsonControls); RebuildJsonControls();
        }
        private void RebuildJsonControls()
        {
            if (jsonControls == null) return;
            jsonControls.Clear();
            if (IsJsonFlowDraft)
            {
                workspaceLevel?.SetEnabled(false);
                jsonControls.Add(new HelpBox(JsonFlowDraftRestriction, HelpBoxMessageType.Info));
                return;
            }
            jsonControls.Add(new Label(IsJsonMode ? "JSON 작업 모드" : "SO 작업 모드"));
            jsonControls.Add(new Button(() => JsonAction(() =>
            {
                string folder = EditorUtility.OpenFolderPanel("project.json이 있는 작업 폴더", jsonFolder ?? "", "");
                if (!string.IsNullOrEmpty(folder)) OpenJsonWorkspace(folder);
            })) { name = "open-json-folder", text = "JSON 폴더 열기", tooltip = "project.json이 있는 제작 폴더를 엽니다. 기존 SO와 자동 동기화하지 않습니다." });
            AddJsonRecoveryControl();
            workspaceLevel?.SetEnabled(!IsJsonMode);
            if (workspaceTabs != null && workspaceTabs.Length > 4)
            {
                workspaceTabs[4].SetEnabled(!IsJsonMode);
                workspaceTabs[4].tooltip = IsJsonMode ? "JSON 일괄 시험은 아직 지원하지 않습니다. 선택한 레벨의 플레이 테스트·진단을 사용하세요." : "여러 레벨 시험";
                if (IsJsonMode && workspaceTab == 4) SelectWorkspaceTab(0);
            }
            if (!IsJsonMode) return;
            var documents = jsonWorkspace.Session.Documents.Where(doc => doc.Kind == "level").OrderBy(doc => (int)doc.Data["levelNumber"]).ToArray();
            var labels = documents.Select(doc => doc.Data["levelNumber"] + " · " + doc.Data["displayName"]).ToList();
            int index = Array.FindIndex(documents, doc => doc.Id == jsonWorkspace.Session.SelectedLevelId);
            var list = new PopupField<string>("JSON 레벨", labels, Math.Max(index, 0)) { name = "json-level-list" };
            list.RegisterValueChangedCallback(evt => JsonAction(() =>
            {
                string id = documents[list.index].Id;
                if (id == jsonWorkspace.Session.SelectedLevelId) return;
                if (!ConfirmJsonLeave()) { list.SetValueWithoutNotify(labels[index]); return; }
                // 버리면 마지막 저장 기준으로 돌아간다. 새 레벨이 사라졌다면 저장된 레벨 선택을 유지한다.
                if (jsonWorkspace.Session.IsDirty)
                {
                    properties?.Unbind(); data?.Dispose(); data = null;
                    jsonWorkspace.Session.Discard(); jsonWorkspace.RestoreDisplay();
                    if (jsonWorkspace.Session.Documents.Any(doc => doc.Kind == "level" && doc.Id == id)) jsonWorkspace.Session.SelectLevel(id);
                }
                else jsonWorkspace.Session.SelectLevel(id);
                SetLevel(jsonWorkspace.Level); PersistJsonState(); RebuildJsonControls();
            }));
            jsonControls.Add(list);
            jsonControls.Add(new Button(() => JsonAction(() =>
            {
                string folder = EditorUtility.OpenFolderPanel("JSON 사본을 저장할 빈 폴더", jsonFolder, "");
                if (string.IsNullOrEmpty(folder)) return;
                jsonWorkspace.Capture("사본 저장 입력 확정");
                jsonWorkspace.Session.SaveAs(new ContentSnapshotStore(Path.GetFullPath(folder)));
                jsonFolder = Path.GetFullPath(folder); jsonBackupMode = false; PersistJsonState(); UpdateSaveState(); RebuildJsonControls();
            })) { name = "save-json-copy", text = "다른 폴더에 사본 저장", tooltip = "기존 문서 ID와 변경 이력을 유지합니다. 대상 폴더에 project.json이 있으면 덮어쓰지 않습니다." });
            jsonControls.Add(new Button(() => JsonAction(() => OpenJsonWorkspace(jsonFolder))) { name = "reload-json", text = "다시 읽기" });
            jsonControls.Add(new Button(() => JsonAction(() => UndoJson(false))) { name = "json-undo", text = "실행 취소" });
            jsonControls.Add(new Button(() => JsonAction(() => UndoJson(true))) { name = "json-redo", text = "다시 실행" });
            jsonControls.Add(new Button(() => JsonAction(() => SetLevel(null))) { name = "leave-json", text = "SO 모드" });
        }
        private void CommitJsonFlow(Tutorial.TutorialFlowDefinition flow, Action apply)
        {
            if (!IsJsonMode || !jsonWorkspace.Owns(flow)) throw new InvalidOperationException("JSON 작업 폴더가 바뀌었습니다. 원본 편집을 다시 여세요.");
            jsonCapturing = true;
            try { jsonWorkspace.Execute("공통 튜토리얼 적용", apply); }
            finally { jsonCapturing = false; PersistJsonState(); }
            Refresh();
        }
        private System.Collections.Generic.IReadOnlyList<LevelDefinition> JsonFlowUsers(Tutorial.TutorialFlowDefinition flow)
        {
            if (!IsJsonMode || !jsonWorkspace.Owns(flow)) return Array.Empty<LevelDefinition>();
            string id = jsonWorkspace.DocumentId(flow);
            return jsonWorkspace.Session.Documents.Where(doc => doc.Kind == "level" && (string)doc.Data["tutorial"]?["flowId"] == id)
                .OrderBy(doc => (int)doc.Data["levelNumber"]).Select(doc => (LevelDefinition)jsonWorkspace.Resolve(doc.Id)).ToArray();
        }
        private void OpenJsonUsageLevel(LevelDefinition target)
        {
            if (!IsJsonMode || !jsonWorkspace.Owns(target)) return;
            string id = jsonWorkspace.DocumentId(target);
            if (!ConfirmJsonLeave()) return;
            if (jsonWorkspace.Session.IsDirty) DiscardChanges();
            if (!jsonWorkspace.Session.Documents.Any(doc => doc.Kind == "level" && doc.Id == id)) id = jsonWorkspace.Session.SelectedLevelId;
            SetLevel((LevelDefinition)jsonWorkspace.Resolve(id)); PersistJsonState(); RebuildJsonControls(); Focus();
        }
        internal GameScreen.PuzzlePlayRequest CreateJsonPlayRequest(int seed)
        {
            if (!IsJsonMode) throw new InvalidOperationException("JSON 작업 폴더를 먼저 여세요.");
            data?.ApplyModifiedPropertiesWithoutUndo();
            jsonWorkspace.Capture("시험 입력 확정"); PersistJsonState();
            return LevelAuthoring.Runtime.JsonPuzzlePlayAdapter.CreateRequest(jsonWorkspace.Session.CreateSnapshot(), jsonWorkspace.Session.SelectedLevelId, seed);
        }
        private void DuplicateJsonLevel(int number)
        {
            jsonWorkspace.Capture("복제 전 입력 확정");
            DocumentEditing.DuplicateLevel(jsonWorkspace.Session, jsonWorkspace.Session.SelectedLevelId, number, "Level_" + number);
            SetLevel(jsonWorkspace.Level); PersistJsonState(); RebuildJsonControls();
        }
        private void UndoJson(bool redo)
        {
            if (recommendationPanel != null) recommendationPanel.style.display = DisplayStyle.None;
            refreshShapeUsage = null;
            if (redo) jsonWorkspace.Redo(); else jsonWorkspace.Undo();
            SetLevel(jsonWorkspace.Level); RestoreJsonSelection(); Refresh(); PersistJsonState(); RebuildJsonControls();
        }
        private void CreateJsonLevel(string name)
        {
            int number = jsonWorkspace.Session.Documents.Where(doc => doc.Kind == "level").Max(doc => (int)doc.Data["levelNumber"]) + 1;
            AddJsonLevel(CreateJsonLevelTemplate(), number, name);
        }
        private Newtonsoft.Json.Linq.JObject CreateJsonLevelTemplate()
        {
            LevelDefinition template = CreateInstance<LevelDefinition>();
            try
            {
                LevelSupplyEditing.AddTopSources(template);
                ElementLevelSupplyDefinition supply = ElementLevelSupplyDefinition.FromLegacy(template.Supply);
                JsonUtility.FromJsonOverwrite("{\"schemaVersion\":5,\"supply\":{\"sources\":[]},\"elementSupply\":" + JsonUtility.ToJson(supply) + "}", template);
                var document = LevelAuthoring.Runtime.UnityAuthoringCodec.WriteDraft(template, "level", "template", _ => null, _ => null);
                document.Data["catalogId"] = jsonWorkspace.Session.Get(jsonWorkspace.Session.SelectedLevelId).Data["catalogId"]?.DeepClone();
                return document.Data;
            }
            finally { Undo.ClearUndo(template); DestroyImmediate(template); }
        }
        private void AddJsonLevel(Newtonsoft.Json.Linq.JObject template, int number, string name)
        {
            DocumentEditing.AddLevel(jsonWorkspace.Session, template, number, name);
            SetLevel(jsonWorkspace.Level); PersistJsonState(); RebuildJsonControls();
        }
        private string EditFlow(string label, Func<string> action)
        {
            if (!IsJsonMode) return action();
            bool previous = jsonCapturing; jsonCapturing = true;
            string message = null;
            try { jsonWorkspace.Execute(label, () => message = action()); return message; }
            catch (Exception error) { data?.Update(); return "편집 취소: " + error.Message; }
            finally { jsonCapturing = previous; PersistJsonState(); }
        }
        private void EditLevel(string label, Action edit)
        {
            if (!IsJsonMode) { edit(); return; }
            bool previous = jsonCapturing;
            jsonCapturing = true;
            try { jsonWorkspace.Execute(label, edit); }
            catch (Exception error)
            {
                data?.Update();
                if (operation != null) operation.text = "편집 취소: " + error.Message;
            }
            finally { jsonCapturing = previous; PersistJsonState(); }
        }
        private void JsonAction(Action action)
        {
            try { action(); }
            catch (Exception error) { if (operation != null) operation.text = "JSON 작업 실패: " + error.Message; else Debug.LogError(error.Message); }
        }
        private void OnJsonPropertyChanged(SerializedPropertyChangeEvent evt)
        {
            if (jsonWorkspace == null || jsonCapturing) return;
            jsonCapturing = true;
            try { jsonWorkspace.Capture("속성 변경"); PersistJsonState(); UpdateSaveState(); }
            catch (Exception error) { if (operation != null) operation.text = error.Message; }
            finally { jsonCapturing = false; }
        }
        private bool UpdateJsonSaveState()
        {
            if (!IsJsonMode) return false;
            bool dirty = jsonWorkspace.Session.IsDirty;
            hasUnsavedChanges = dirty;
            titleContent = new GUIContent(dirty ? "Match JSON *" : "Match JSON");
            if (state != null) state.text = jsonFolder + (dirty ? "  ● 저장 안 됨" : "  • 저장됨");
            return true;
        }
    }
}
