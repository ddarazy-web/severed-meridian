using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelAuthoring.Editor
{
    internal sealed class ContentImportHistoryWindow : EditorWindow
    {
        private Func<string> candidate;
        private Func<bool> operationBusy;
        private ImportHistoryPlan plan;
        private Label summary, result;
        private ScrollView entries;
        private Button delete;

        internal static void Open(Func<string> candidate, Func<bool> operationBusy)
        {
            ContentImportHistoryWindow window = GetWindow<ContentImportHistoryWindow>("제작 데이터 히스토리 정리");
            window.candidate = candidate;
            window.operationBusy = operationBusy;
            window.Show();
            window.RefreshList();
        }

        public void CreateGUI()
        {
            minSize = new Vector2(640, 480);
            VisualElement root = rootVisualElement;
            root.Clear(); root.AddToClassList("content-import");
            StyleSheet sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Scripts/Features/LevelAuthoring/Editor/Import/ContentImportWindow.uss");
            if (sheet != null && !root.styleSheets.Contains(sheet)) root.styleSheets.Add(sheet);
            VisualElement header = new VisualElement(); header.AddToClassList("import-header"); root.Add(header);
            Label title = new Label("히스토리 정리"); title.AddToClassList("import-title"); header.Add(title);
            header.Add(new Label("현재 원본과 게임 리소스는 유지하고 사용하지 않는 작업 이력만 삭제합니다."));
            summary = new Label("목록 갱신을 눌러 정리 대상을 확인하세요."); summary.AddToClassList("import-guidance"); root.Add(summary);
            entries = new ScrollView(ScrollViewMode.Vertical); entries.AddToClassList("import-page"); root.Add(entries);
            VisualElement actions = new VisualElement(); actions.AddToClassList("import-navigation"); root.Add(actions);
            actions.Add(new Button(RefreshList) { text = "목록 갱신" });
            delete = new Button(DeleteHistory) { text = "표시된 이력 정리", name = "delete-history" }; actions.Add(delete);
            delete.SetEnabled(false);
            result = new Label("목록 확인 후 정리 버튼을 눌러 주세요. 삭제한 이력은 되돌릴 수 없습니다.");
            result.AddToClassList("import-guidance"); root.Add(result);
        }

        private void RefreshList()
        {
            if (entries == null) return;
            plan = null; delete.SetEnabled(false); entries.Clear();
            try
            {
                AssertIdle();
                plan = ContentImportHistory.Preview(Path.GetFullPath("."), candidate?.Invoke());
                summary.text = "정리 대상 " + plan.Entries.Count + "개 · 파일 " + plan.FileCount + "개 · " + EditorUtility.FormatBytes(plan.Bytes);
                foreach (ImportHistoryEntry entry in plan.Entries)
                {
                    Label row = new Label(Path.GetRelativePath(plan.Root, entry.Path) + "\n파일 " + entry.Files.Length + "개 · " + EditorUtility.FormatBytes(entry.Bytes));
                    row.AddToClassList("import-section"); row.style.whiteSpace = WhiteSpace.Normal;
                    row.selection.isSelectable = true; entries.Add(row);
                }
                foreach (string note in plan.Protected) entries.Add(new HelpBox("보존 · " + note, HelpBoxMessageType.Info));
                delete.SetEnabled(plan.Entries.Count > 0);
                result.text = plan.Entries.Count == 0 ? "정리할 이력이 없습니다." : "목록에 표시된 대상만 삭제합니다. 확인 창에서 취소할 수 있습니다.";
            }
            catch (Exception error)
            {
                summary.text = "정리 대상을 확인할 수 없습니다.";
                result.text = "파일을 삭제하지 않았습니다. 진행 중인 가져오기·변환을 마친 뒤 다시 확인하세요.\n" + error.Message;
            }
        }

        private void DeleteHistory()
        {
            if (plan == null || plan.Entries.Count == 0) return;
            if (!EditorUtility.DisplayDialog("제작 데이터 히스토리 삭제",
                "표시된 " + plan.Entries.Count + "개 대상(파일 " + plan.FileCount + "개, " + EditorUtility.FormatBytes(plan.Bytes) +
                ")을 삭제합니다.\n현재 원본과 게임 리소스는 보존합니다. 삭제한 이력은 되돌릴 수 없습니다.", "이력 삭제", "취소")) return;
            try
            {
                AssertIdle();
                int count = plan.FileCount;
                ContentImportHistory.Delete(plan, candidate?.Invoke());
                AssetDatabase.Refresh();
                RefreshList();
                result.text = "히스토리 정리 완료 · 파일 " + count + "개 삭제. 현재 원본과 게임 리소스는 유지했습니다.";
            }
            catch (Exception error)
            {
                plan = null; delete.SetEnabled(false);
                result.text = "정리를 중단했습니다. 일부 파일이 이미 삭제됐을 수 있으므로 목록을 갱신해 확인하세요.\n" + error.Message;
            }
        }

        private void AssertIdle()
        {
            if (operationBusy?.Invoke() == true)
                throw new InvalidOperationException("제작 데이터 작업이 진행 중입니다.");
        }
    }
}
