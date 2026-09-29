using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    public sealed partial class LevelEditorWindow
    {
        private Label workspacePageName;

        /// <summary>상단 메뉴는 명령, 탭은 자주 오가는 작업 화면을 담당한다. 실행/삭제는 기존 진입점을 재사용한다.</summary>
        private Toolbar CreateWorkspaceMenu()
        {
            Toolbar bar = new Toolbar { name = "workspace-menubar" };
            ToolbarMenu file = new ToolbarMenu { text = "레벨", name = "workspace-menu-level" };
            file.menu.AppendAction("새 레벨…", _ => ShowLevelNamePanel(true));
            file.menu.AppendAction("저장    Ctrl+S", _ => Save(), _ => level != null ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            file.menu.AppendAction("복제…", _ => { SelectWorkspaceTab(0); ShowDuplicatePanel(); }, _ => level != null ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            file.menu.AppendAction("이름 변경…", _ => ShowLevelNamePanel(false), _ => level != null ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            file.menu.AppendSeparator();
            file.menu.AppendAction("등록한 맵 모양…", _ => ShowShapeRecommendations());
            file.menu.AppendAction("레벨 데이터 검사", _ => { SelectWorkspaceTab(0); Validate(); }, _ => level != null ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            bar.Add(file);

            ToolbarMenu test = new ToolbarMenu { text = "시험", name = "workspace-menu-test" };
            test.menu.AppendAction("플레이 테스트", _ => SelectWorkspaceTab(1));
            test.menu.AppendAction("여러 레벨 시험", _ => SelectWorkspaceTab(4));
            test.menu.AppendSeparator();
            test.menu.AppendAction("초기 보드·진단", _ => SelectWorkspaceTab(2));
            bar.Add(test);

            ToolbarMenu records = new ToolbarMenu { text = "기록", name = "workspace-menu-records" };
            records.menu.AppendAction("결과·이력", _ => SelectWorkspaceTab(3));
            records.menu.AppendAction("지난 여러 레벨 시험", _ => { SelectWorkspaceTab(4); multiPanel.Root.Q<PopupField<string>>("multi-history")?.Focus(); });
            records.menu.AppendSeparator();
            records.menu.AppendAction("기록 관리", _ => RevealRecordManagement());
            records.menu.AppendAction("시험 기록 전체 삭제…", _ => { RevealRecordManagement(); recordManagement.Request(); },
                _ => recordManagement?.CanRequest == true ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            bar.Add(records);

            ToolbarMenu help = new ToolbarMenu { text = "도움말", name = "workspace-menu-help" };
            help.menu.AppendAction("사용 설명서", _ => OpenManual());
            help.menu.AppendAction("현재 화면 사용법", _ => LevelEditorHelp.Open(workspaceTab switch {
                1 => "play.html", 2 => "play.html", 3 => "analysis.html", 4 => "difficulty.html#multi-level", _ => "index.html" }));
            bar.Add(help);
            workspacePageName = new Label { name = "workspace-page-name" };
            bar.Add(workspacePageName);
            return bar;
        }

        /// <summary>삭제 명령은 관리 영역을 보여준 뒤 기존 확인 절차로 들어간다.</summary>
        private void RevealRecordManagement()
        {
            SelectWorkspaceTab(3);
            analysisPanel.Root.Q<Foldout>("record-management").value = true;
        }
    }
}
