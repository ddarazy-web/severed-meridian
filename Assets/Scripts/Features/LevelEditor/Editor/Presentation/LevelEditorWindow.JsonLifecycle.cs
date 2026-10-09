using System;
using System.IO;
using System.Linq;
using Board;
using LevelAuthoring.Documents;
using LevelAuthoring.Editing;
using LevelAuthoring.Editor;
using LevelAuthoring.Storage;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    public sealed partial class LevelEditorWindow
    {
        [SerializeField] private bool jsonBackupMode;
        [SerializeField] private string jsonViewLevelId;
        [SerializeField] private PlacementLayer jsonViewLayer;
        [SerializeField] private LevelBrush jsonViewBrush;
        [SerializeField] private RabbitColor jsonViewColor;
        [SerializeField] private int jsonViewSelectedCell = -1;
        [SerializeField] private int[] jsonViewPlacements = Array.Empty<int>();
        [SerializeField] private int[] jsonViewSources = Array.Empty<int>();
        [SerializeField] private int jsonViewToolPage;
        [SerializeField] private int jsonViewInspectorPage;
        [SerializeField] private int jsonViewTutorialStep;
        [SerializeField] private Vector2 jsonViewBoardScroll;
        [SerializeField] private Vector2 jsonViewToolScroll;
        [SerializeField] private Vector2 jsonViewInspectorScroll;
        [SerializeField] private Vector2 jsonViewWorkspaceScroll;
        private bool jsonRestoreViewPending;

        private void AddJsonRecoveryControl()
        {
            jsonControls.Add(new Button(() => JsonAction(() =>
            {
                string folder = EditorUtility.OpenFolderPanel("이전 정상본을 복구할 JSON 작업 폴더", jsonFolder ?? "", "");
                if (!string.IsNullOrEmpty(folder)) OpenJsonBackup(folder);
            })) { name = "recover-json-backup", text = "이전 정상본 열기", tooltip = "마지막 저장 직전의 정상본을 엽니다. 현재 원본은 보존하며, 복구한 내용은 다른 빈 폴더에 사본으로 저장하세요." });
            if (jsonBackupMode && IsJsonMode) jsonControls.Add(new Label("복구본 · 다른 폴더에 사본 저장 필요"));
        }

        private void OpenJsonBackup(string folder)
        {
            if (IsJsonFlowDraft) throw new InvalidOperationException(JsonFlowDraftRestriction);
            if (!ConfirmJsonLeave()) return;
            string path = Path.GetFullPath(folder);
            StoredContentSnapshot backup = new ContentSnapshotStore(path).ReadBackup();
            string first = backup.Snapshot.Documents.Where(doc => doc.Kind == "level")
                .OrderBy(doc => (int)doc.Data["levelNumber"]).FirstOrDefault()?.Id;
            if (first == null) throw new ContentFormatException("이전 정상본에 레벨이 없습니다.");
            JsonAuthoringWorkspace next = new JsonAuthoringWorkspace(new AuthoringEditSession(backup, first));
            try { _ = next.Level; }
            catch { next.Dispose(); throw; }
            JsonAuthoringWorkspace previous = jsonWorkspace;
            jsonWorkspace = next; jsonFolder = path; jsonBackupMode = true;
            jsonSwitching = true;
            try { SetLevel(next.Level); PersistJsonState(); RebuildJsonControls(); }
            finally { jsonSwitching = false; previous?.Dispose(); }
        }

        private void CaptureJsonViewState()
        {
            if (!IsJsonMode || board == null || !jsonWorkspace.Owns(level) || jsonRestoreViewPending) return;
            jsonViewLevelId = jsonWorkspace.Session.SelectedLevelId;
            jsonViewLayer = board.Layer; jsonViewBrush = board.Brush; jsonViewColor = board.Color;
            jsonViewSelectedCell = selected.HasValue ? selected.Value.Row * 9 + selected.Value.Column : -1;
            jsonViewPlacements = selectedPlacements.Select(item => item.Coordinate.Row * 9 + item.Coordinate.Column).ToArray();
            jsonViewSources = selectedSources.Select(cell => cell.Row * 9 + cell.Column).ToArray();
            int[] cells = jsonViewBrush == LevelBrush.SourceSelect ? jsonViewSources : jsonViewPlacements;
            jsonWorkspace.Session.SelectCells(cells.Length > 0 ? cells : jsonViewSelectedCell < 0 ? Array.Empty<int>() : new[] { jsonViewSelectedCell });
            jsonViewToolPage = toolPage; jsonViewInspectorPage = inspectorPage; jsonViewTutorialStep = selectedTutorialStep;
            jsonViewBoardScroll = boardScroll.scrollOffset;
            jsonViewToolScroll = editorRoot.Q<ScrollView>("tool-scroll")?.scrollOffset ?? Vector2.zero;
            jsonViewInspectorScroll = editorRoot.Q<ScrollView>("inspector-scroll")?.scrollOffset ?? Vector2.zero;
            jsonViewWorkspaceScroll = workspaceViewport?.scrollOffset ?? Vector2.zero;
            PersistJsonState();
        }

        private void RestoreJsonViewState()
        {
            if (!jsonRestoreViewPending || !IsJsonMode || board == null) return;
            jsonRestoreViewPending = false;
            if (jsonViewLevelId != jsonWorkspace.Session.SelectedLevelId) return;
            board.Layer = jsonViewLayer; board.Color = jsonViewColor;
            // 배치 드래그·연결 초안은 재개하지 않고 선택 도구로 복귀한다.
            board.Brush = jsonViewBrush == LevelBrush.SourceSelect ? LevelBrush.SourceSelect : LevelBrush.Select;
            selected = jsonViewSelectedCell < 0 ? (BoardCoordinate?)null : new BoardCoordinate(jsonViewSelectedCell / 9, jsonViewSelectedCell % 9);
            selectedPlacements.Clear();
            foreach (int cell in jsonViewPlacements)
                if (LevelCommonEditing.TrySelect(level, board.Layer, new BoardCoordinate(cell / 9, cell % 9), out PlacementSelection target)) selectedPlacements.Add(target);
            selectedSources.Clear();
            foreach (int cell in jsonViewSources) selectedSources.Add(new BoardCoordinate(cell / 9, cell % 9));
            toolPage = jsonViewToolPage; inspectorPage = jsonViewInspectorPage; selectedTutorialStep = jsonViewTutorialStep;
            Refresh();
            // 레이아웃이 계산되기 전에 스크롤 값을 넣으면 ScrollView가 0으로 제한한다.
            editorRoot.schedule.Execute(() =>
            {
                if (!IsJsonMode || jsonViewLevelId != jsonWorkspace.Session.SelectedLevelId) return;
                boardScroll.scrollOffset = jsonViewBoardScroll;
                editorRoot.Q<ScrollView>("tool-scroll").scrollOffset = jsonViewToolScroll;
                editorRoot.Q<ScrollView>("inspector-scroll").scrollOffset = jsonViewInspectorScroll;
                if (workspaceViewport != null) workspaceViewport.scrollOffset = jsonViewWorkspaceScroll;
            });
        }

        private void RestoreJsonSelection()
        {
            if (!IsJsonMode || board == null) return;
            int[] cells = jsonWorkspace.Session.SelectedCells;
            selected = cells.Length == 0 ? (BoardCoordinate?)null : new BoardCoordinate(cells[0] / 9, cells[0] % 9);
            selectedPlacements.Clear(); selectedSources.Clear();
            foreach (int cell in cells)
            {
                BoardCoordinate coordinate = new BoardCoordinate(cell / 9, cell % 9);
                if (board.Brush == LevelBrush.SourceSelect) selectedSources.Add(coordinate);
                else if (LevelCommonEditing.TrySelect(level, board.Layer, coordinate, out PlacementSelection target)) selectedPlacements.Add(target);
            }
        }

        private bool PrepareJsonPlayMode(bool ownTest, Func<int> choose)
        {
            if (!IsJsonMode) return true;
            try
            {
                rootVisualElement.focusController?.focusedElement?.Blur();
                data?.ApplyModifiedPropertiesWithoutUndo();
                jsonWorkspace.Capture("Play Mode 전 입력 확정");
                CaptureJsonViewState(); PersistJsonState();
                if (ownTest || !jsonWorkspace.Session.IsDirty) return true;
                int choice = choose();
                if (choice == 1) return false;
                if (choice == 0) SaveJsonWorkspace();
                else if (choice == 2) DiscardChanges();
                else return false;
                return true;
            }
            catch (Exception error)
            {
                if (operation != null) operation.text = "Play Mode 진입 취소: " + error.Message;
                PersistJsonState(); return false;
            }
        }

        private void OnJsonPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.ExitingEditMode || !IsJsonMode) return;
            bool allow = PrepareJsonPlayMode(GameScreen.Editor.PuzzleEditorLauncher.IsBusy, () =>
                EditorUtility.DisplayDialogComplex("JSON 변경 내용", "일반 Play 버튼은 JSON 편집값을 게임에 전달하지 않습니다. 현재 변경을 저장하거나 버린 뒤 진입할 수 있습니다. 편집값 시험은 이 창의 게임 플레이를 사용하세요.", "저장 후 진입", "취소", "버리고 진입"));
            if (!allow) EditorApplication.isPlaying = false;
        }
    }
}
