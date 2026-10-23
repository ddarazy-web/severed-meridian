#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using LevelAuthoring.Editing;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelTool
{
    public sealed partial class LevelToolScreen
    {
        private VisualElement startScreen;
        private Label startTitle, startDescription;
        private Button firstLevelButton;

        private void OpenProjectStartMenu()
        {
            if (busy || sharedTutorialDraft != null || tutorialPick != null || playRoot != null) return;
            VisualElement panel = OpenModal("프로젝트 시작");
            commandMenuOpen = true;
            panel.Add(new Label("현재 작업은 프로젝트를 바꾸기 전까지 유지됩니다.")
                { style = { whiteSpace = WhiteSpace.Normal } });
            CommandButton(panel, ToolCommandId.NewProject, "menu-new-project");
            CommandButton(panel, ToolCommandId.Open, "menu-open-project");
            CommandButton(panel, ToolCommandId.ImportProject, "menu-import-project");
            CommandButton(panel, ToolCommandId.RecentProjects, "menu-recent-projects");
            Button(panel, "cancel", "편집으로 돌아가기", CloseModal);
            RefreshCommandStates();
        }

        private void BuildStartScreen()
        {
            startScreen = new ScrollView { name = "project-start" };
            startScreen.style.flexGrow = 1;
            startScreen.style.paddingLeft = 24; startScreen.style.paddingRight = 24;
            startScreen.style.paddingTop = 24; startScreen.style.paddingBottom = 24;
            editor.Insert(editor.IndexOf(body), startScreen);
            startTitle = new Label { name = "start-title" };
            startTitle.style.fontSize = 24; startTitle.style.whiteSpace = WhiteSpace.Normal;
            startScreen.Add(startTitle);
            startDescription = new Label { name = "start-description" };
            startDescription.style.whiteSpace = WhiteSpace.Normal;
            startDescription.style.marginBottom = 20; startScreen.Add(startDescription);
            firstLevelButton = Button(startScreen, "first-level", "첫 레벨 만들기", () => AddLevel(false));
            CommandButton(startScreen, ToolCommandId.NewProject, "start-new-project");
            CommandButton(startScreen, ToolCommandId.Open, "start-open-project");
            CommandButton(startScreen, ToolCommandId.ImportProject, "start-import-project");
            CommandButton(startScreen, ToolCommandId.RecentProjects, "start-recent-projects");
            foreach (Button button in startScreen.Query<Button>().ToList())
            {
                button.style.minHeight = 44; button.style.marginBottom = 8;
                button.style.maxWidth = 480;
            }
        }

        private void RefreshStartScreen()
        {
            if (startScreen == null || !editor.Contains(startScreen)) BuildStartScreen();
            bool projectOpen = Workspace.Folder != null;
            bool empty = levelIndex.Count == 0;
            bool visible = !projectOpen || empty;
            startScreen.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            body.style.display = visible ? DisplayStyle.None : DisplayStyle.Flex;
            workspaceTabs.style.display = visible ? DisplayStyle.None : DisplayStyle.Flex;
            editor.Q("workspace-context").style.display = visible ? DisplayStyle.None : DisplayStyle.Flex;
            editor.Q("board-touch-tools").style.display = visible ? DisplayStyle.None : DisplayStyle.Flex;
            validationPanel.style.display = visible ? DisplayStyle.None : DisplayStyle.Flex;
            if (mobileNavigation != null) mobileNavigation.style.display = visible ? DisplayStyle.None : DisplayStyle.Flex;
            if (mobileToolStatus != null) mobileToolStatus.style.display = visible ? DisplayStyle.None : DisplayStyle.Flex;
            startTitle.text = projectOpen ? "아직 만든 레벨이 없습니다" : "레벨 제작을 시작하세요";
            startDescription.text = projectOpen ? "첫 레벨을 만들면 9×9 보드에서 바로 편집할 수 있어요. 미션은 나중에 설정해도 됩니다." :
                "새 프로젝트를 만들거나, 저장한 프로젝트와 ZIP 백업을 열어 주세요.";
            firstLevelButton.style.display = projectOpen ? DisplayStyle.Flex : DisplayStyle.None;
            firstLevelButton.SetEnabled(!busy);
            root.Q("new-level")?.SetEnabled(projectOpen && !busy && sharedTutorialDraft == null);
            root.Q("duplicate")?.SetEnabled(Session?.SelectedLevelId != null && !busy && sharedTutorialDraft == null);
        }

        private void NewProject()
        {
            if (GuardTutorialWorkExit(NewProject)) return;
            if (busy) return;
            VisualElement panel = OpenModal("새 프로젝트 만들기");
            panel.Add(new Label("프로젝트 이름을 정해 주세요. 레벨은 다음 화면에서 만들어요.")
                { style = { whiteSpace = WhiteSpace.Normal } });
            var name = new TextField("프로젝트 이름") { name = "project-name", value = "새 프로젝트" };
            panel.Add(name);
            var error = new Label { name = "project-name-error", style = { whiteSpace = WhiteSpace.Normal } }; panel.Add(error);
            Button(panel, "create-project", UsesInternalProjects ? "프로젝트 만들기" : "저장 위치 선택", () =>
            {
                if (string.IsNullOrWhiteSpace(name.value)) { error.text = "프로젝트 이름을 입력해 주세요."; return; }
                string projectName = name.value.Trim();
                try
                {
                    var template = LevelToolDocuments.NewProject(projectName);
                    if (UsesInternalProjects)
                    {
                        string path = LevelToolProjectAccess.Resolve(InternalProjectsRoot, projectName, true);
                        CloseModal(); WithDirtyFor("새 프로젝트 만들기", choice => Workspace.Create(path, template, choice));
                    }
                    else
                    {
                        CloseModal();
                        ChooseFolder(path => WithDirtyFor("새 프로젝트 만들기", choice => Workspace.Create(path, template, choice)), true);
                    }
                }
                catch (Exception failure) { error.text = failure.Message; }
            });
            Button(panel, "cancel", "취소", CloseModal);
        }

        private void AddLevel(bool duplicate)
        {
            if (GuardTutorialWorkExit(() => AddLevel(duplicate))) return;
            if (busy || Workspace.Folder == null || sharedTutorialDraft != null || modal != null) return;
            ContinueAfterManualInput(() => OpenNewLevelDialog(duplicate)).Forget();
        }

        private void OpenNewLevelDialog(bool duplicate)
        {
            if (busy || Workspace.Folder == null || sharedTutorialDraft != null) return;
            try { Workspace.BeginEditingProject(); }
            catch (Exception error) { Show(error.Message); return; }
            bool canDuplicate = Session.SelectedLevelId != null;
            int suggested = Session.DocumentIdsOfKind("level").Select(id => Session.GetDocumentInfo(id).LevelNumber).DefaultIfEmpty(0).Max();
            if (suggested == int.MaxValue) suggested = 0;
            do { suggested++; } while (Session.DocumentIdsOfKind("level").Any(id => Session.GetDocumentInfo(id).LevelNumber == suggested));
            VisualElement panel = OpenModal("새 레벨 만들기");
            var source = new DropdownField("시작 방법", canDuplicate ? new System.Collections.Generic.List<string> { "기본 9×9 보드", "현재 레벨 복제" } :
                new System.Collections.Generic.List<string> { "기본 9×9 보드" }, duplicate && canDuplicate ? 1 : 0) { name = "new-level-source" };
            var number = new IntegerField("레벨 번호") { name = "new-level-number", value = suggested };
            var name = new TextField("레벨 이름") { name = "new-level-name", value = "레벨 " + suggested };
            panel.Add(source); panel.Add(number); panel.Add(name);
            panel.Add(new Label("복제하면 보드·미션·이동 횟수·튜토리얼이 함께 복사됩니다. 원본은 유지됩니다.")
                { style = { whiteSpace = WhiteSpace.Normal } });
            var errorLabel = new Label { name = "new-level-error", style = { whiteSpace = WhiteSpace.Normal } }; panel.Add(errorLabel);
            AuthoringEditSession owner = Session;
            Button(panel, "create-level", "만들고 편집하기", () =>
            {
                try
                {
                    if (!ReferenceEquals(owner, Session)) return;
                    if (source.index == 1) DocumentEditing.DuplicateLevel(owner, owner.SelectedLevelId, number.value, name.value);
                    else
                    {
                        JToken catalog = owner.SelectedLevelId != null ? owner.Get(owner.SelectedLevelId).Data["catalogId"] :
                            owner.Get(owner.DocumentIdsOfKind("project").Single()).Data["defaultCatalogId"];
                        DocumentEditing.AddLevel(owner, LevelToolDocuments.NewLevel(catalog), number.value, name.value);
                    }
                    CloseModal(); forceProperties = true; SelectWorkspacePage("보드 편집");
                    Show("레벨을 만들었습니다. 저장하면 파일에 반영됩니다.");
                }
                catch (Exception failure) { errorLabel.text = failure.Message; }
            });
            Button(panel, "cancel", "취소", CloseModal);
        }
    }
}
#endif
