using System;
using System.Linq;
using Board;
using LevelAuthoring.Editing;

namespace Levels.Editor
{
    public sealed partial class LevelEditorWindow
    {
        private AuthoringEditSession jsonElementSelectionSession;
        private ElementSelectionEditing jsonElementSelection;

        private ElementSelectionEditing JsonElementSelection
        {
            get
            {
                // 다시 열기와 작업 폴더 전환은 새 세션이므로 이전 클립보드를 이어받지 않는다.
                if (!ReferenceEquals(jsonElementSelectionSession, jsonWorkspace.Session))
                {
                    jsonElementSelectionSession = jsonWorkspace.Session;
                    jsonElementSelection = new ElementSelectionEditing(jsonElementSelectionSession, JsonDefinitions);
                }
                return jsonElementSelection;
            }
        }

        private void AddJsonElementSelectionActions(Action<string, bool, Action> add, PlacementLayer layer,
            BoardCoordinate? cell, PlacementSelection[] placements)
        {
            bool hasSelection = placements.Length > 0;
            ElementSelectionEditing commands = JsonElementSelection;
            string layerName = layer.ToString();
            add("요소 복사", hasSelection, () => RunJsonElementSelection(command => command.Copy(layerName), "선택 요소를 복사했습니다.", false));
            add("요소 잘라내기", hasSelection, () => RunJsonElementSelection(command => command.Cut(layerName), "선택 요소를 잘라냈습니다.", true));
            BoardCoordinate? anchor = cell ?? (hasSelection ? placements[0].Coordinate : (BoardCoordinate?)null);
            add("요소 붙여넣기", commands.HasClipboard && commands.ClipboardLayer == layerName && anchor.HasValue, () =>
                RunJsonElementSelection(command => command.Paste(anchor.Value.Row, anchor.Value.Column), "복사한 요소를 붙여넣었습니다.", true));
            if (commands.HasClipboard && commands.ClipboardLayer != layerName)
                add("복사한 요소의 층으로 전환한 뒤 붙여넣으세요.", false, () => { });
            add("선택한 층의 요소 삭제", hasSelection, () => RunJsonElementSelection(command => command.Delete(layerName), "선택 요소를 삭제했습니다.", true));
        }

        private void RunJsonElementSelection(Action<ElementSelectionEditing> command, string message, bool changesDocument)
        {
            JsonAction(() =>
            {
                board.CancelStroke();
                data?.ApplyModifiedPropertiesWithoutUndo();
                jsonWorkspace.Capture("요소 메뉴 전 입력 확정");
                int[] cells = selectedPlacements.Select(item => item.Coordinate.Row * 9 + item.Coordinate.Column).ToArray();
                if (cells.Length == 0 && selected.HasValue) cells = new[] { selected.Value.Row * 9 + selected.Value.Column };
                jsonWorkspace.Session.SelectCells(cells);
                ElementSelectionEditing commands = JsonElementSelection;
                int[] footprint = commands.SelectedCells(board.Layer.ToString());
                if (footprint.Length > 0) jsonWorkspace.Session.SelectCells(footprint);
                try
                {
                    command(commands);
                    if (changesDocument) jsonWorkspace.RestoreDisplay();
                    RestoreJsonSelection(); Refresh();
                    operation.text = message;
                    RebuildJsonControls();
                }
                finally { PersistJsonState(); }
            });
        }
    }
}
