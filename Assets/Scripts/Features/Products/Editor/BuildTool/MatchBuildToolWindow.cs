using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEngine;
using UnityEngine.UIElements;

namespace Products.Editor
{
    public sealed partial class MatchBuildToolWindow : EditorWindow
    {
        [Serializable] private sealed class SelectionRecord { public int version = 1; public string profileGuid; }
        internal string SelectionPath = "UserSettings/MatchBuildTool.json";
        private List<BuildToolProfileInfo> profiles = new List<BuildToolProfileInfo>();
        private string selectedGuid, editorState, preferenceNotice;
        private ScrollView profileList, detail;
        private Label active, notice;
        private Button inspect;
        internal string WindowSizeKey => "Match.BuildTool.WindowSize." + Path.GetFullPath(SelectionPath);

        [MenuItem("Match/Build/빌드 도구")]
        public static void Open()
        {
            bool alreadyOpen = HasOpenInstances<MatchBuildToolWindow>();
            var window = GetWindow<MatchBuildToolWindow>("빌드 도구");
            if (!alreadyOpen && !window.docked) window.RestoreWindowBounds();
            window.Show();
        }

        internal void RestoreWindowBounds()
        {
            var bounds = position;
            bounds.x = EditorPrefs.GetFloat(WindowSizeKey + ".X", bounds.x);
            bounds.y = EditorPrefs.GetFloat(WindowSizeKey + ".Y", bounds.y);
            bounds.width = Mathf.Max(minSize.x, EditorPrefs.GetFloat(WindowSizeKey + ".Width", 1040));
            bounds.height = Mathf.Max(minSize.y, EditorPrefs.GetFloat(WindowSizeKey + ".Height", 760));
            position = bounds;
        }

        private void OnEnable()
        {
            minSize = new Vector2(640, 540);
            EditorApplication.projectChanged += OnProjectChanged;
            EditorApplication.update += OnEditorStateChanged;
        }

        private void OnDisable()
        {
            // 도킹 배치는 Unity 레이아웃이 관리하므로 별도 창의 위치와 크기만 기억한다.
            if (!docked)
            {
                EditorPrefs.SetFloat(WindowSizeKey + ".Width", position.width);
                EditorPrefs.SetFloat(WindowSizeKey + ".Height", position.height);
                EditorPrefs.SetFloat(WindowSizeKey + ".X", position.x);
                EditorPrefs.SetFloat(WindowSizeKey + ".Y", position.y);
            }
            EditorApplication.projectChanged -= OnProjectChanged;
            EditorApplication.update -= OnEditorStateChanged;
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            ApplyPresentation();
            var viewport = new ScrollView { name = "build-tool-viewport", style = { flexGrow = 1 } };
            rootVisualElement.Add(viewport);
            var root = viewport.contentContainer;
            root.style.flexGrow = 1; root.style.minHeight = Length.Percent(100);
            var title = new Label("제품 빌드 도구"); title.AddToClassList("window-title"); root.Add(title);
            var subtitle = new Label("프로필 선택 → 설정 확인 → 옵션 선택 → 빌드 실행"); subtitle.AddToClassList("caption"); root.Add(subtitle);
            active = new Label { name = "active-profile", tooltip = "Unity가 현재 사용 중인 설정입니다. 아래 목록에서 고른 빌드 대상과는 별개입니다." }; active.AddToClassList("active-context"); root.Add(active);
            notice = new Label { name = "build-tool-notice" }; notice.AddToClassList("notice"); root.Add(notice);
            var actions = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } }; root.Add(actions);
            actions.AddToClassList("toolbar");
            actions.Add(new Button(ReloadProfiles) { name = "refresh-profiles", text = "목록 새로고침", tooltip = "프로필을 다시 읽습니다. 활성 제품이나 플랫폼을 전환하지 않습니다." });
            inspect = new Button(RunPreflight) { name = "inspect-profile", text = "설정 다시 검사", tooltip = "선택 프로필의 설정과 준비 상태를 읽기 전용으로 확인합니다. 빌드나 플랫폼 전환은 하지 않습니다." }; actions.Add(inspect);
            buildButton = new Button(RequestBuild) { name = "build-product", text = "선택 프로필 빌드…", tooltip = "대상과 실제 빌드 실행을 확인한 후에만 프로필을 전환하고 빌드합니다." };
            actions.Add(buildButton);
            buildButton.AddToClassList("primary");
            cancelButton = new Button(CancelBuild) { name = "cancel-build", text = "준비·대기 취소", tooltip = "빌드 호출 전의 준비·전환 대기를 취소하고 임시 상태를 정리합니다. 실행 중인 동기 빌드를 강제 종료하지 않습니다." }; actions.Add(cancelButton);
            resolveButton = new Button(ResolveBuild) { name = "resolve-build", text = "미완료 요청 정리…", tooltip = "중단된 요청을 다시 빌드하지 않고 임시 설정만 정리합니다. 외부 변경과 충돌하면 이유를 표시합니다." }; actions.Add(resolveButton);
            var body = new VisualElement { name = "build-tool-body", style = { flexDirection = FlexDirection.Row, flexGrow = 1, flexShrink = 0, flexBasis = 0, minHeight = 260, marginTop = 12 } }; root.Add(body);
            var listPanel = Section(body, "profiles-section", "01  빌드할 제품", "목록 선택은 조회만 합니다. 실제 전환은 빌드 확인 후 시작합니다."); listPanel.AddToClassList("profile-sidebar");
            profileList = new ScrollView { name = "profile-list", style = { flexGrow = 1 } }; listPanel.Add(profileList);
            detail = new ScrollView { name = "profile-detail", style = { flexGrow = 1, minWidth = 0, minHeight = 0 } }; body.Add(detail);
            CreateExecutionPanel(root);
            if (selectedGuid == null)
            {
                try
                {
                    if (File.Exists(SelectionPath))
                    {
                        var record = JsonUtility.FromJson<SelectionRecord>(File.ReadAllText(SelectionPath));
                        if (record == null || record.version != 1 || string.IsNullOrEmpty(record.profileGuid)) throw new InvalidDataException();
                        selectedGuid = record.profileGuid;
                    }
                }
                catch (Exception) { preferenceNotice = "이전 선택 기록을 읽을 수 없어 기본 목록으로 열었습니다. 제품 설정은 변경하지 않았습니다."; }
            }
            ReloadProfiles();
        }

        internal void ReloadProfiles()
        {
            if (profileList == null) return;
            bool busy = EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode;
            var current = BuildProfile.GetActiveBuildProfile();
            active.text = "현재 활성 프로필: " + (current == null ? "기본 프로필" : current.name) + " · 플랫폼: " + EditorUserBuildSettings.activeBuildTarget;
            notice.text = busy ? "컴파일·갱신 또는 Play Mode 중입니다. 사전 검사를 보류합니다." :
                "목록 선택은 편집 대상 선택입니다. 활성 프로필·플랫폼을 전환하지 않습니다. " + preferenceNotice;
            inspect.SetEnabled(!busy);
            if (busy) { detail.Clear(); detail.Add(new Label(notice.text) { style = { whiteSpace = WhiteSpace.Normal } }); RefreshExecution(); return; }
            try { profiles = BuildToolProfiles.Discover(); }
            catch (Exception error)
            {
                profiles.Clear(); preferenceNotice = "프로필 목록 읽기 실패: " + error.Message; notice.text = preferenceNotice;
            }
            if (!profiles.Any(item => item.Guid == selectedGuid))
            {
                if (!string.IsNullOrEmpty(selectedGuid)) preferenceNotice = "이전에 선택한 프로필이 없어 현재 목록에서 다시 선택했습니다.";
                selectedGuid = profiles.FirstOrDefault(item => item.Profile == current)?.Guid ?? profiles.FirstOrDefault()?.Guid;
            }
            DrawProfiles();
            DrawDetail();
            RefreshExecution();
        }

        internal void SelectProfile(string guid)
        {
            if (!profiles.Any(item => item.Guid == guid)) { ReloadProfiles(); return; }
            selectedGuid = guid;
            try
            {
                BuildToolPreferences.SaveSelection(SelectionPath, selectedGuid);
            }
            catch (Exception error) { preferenceNotice = "선택 기억 저장 실패: " + error.Message; }
            DrawProfiles(); DrawDetail(); RefreshExecution();
        }

        internal void RunPreflight() => ReloadProfiles();

        private void DrawProfiles()
        {
            profileList.Clear();
            foreach (var item in profiles)
            {
                var button = new Button(() => SelectProfile(item.Guid)) { name = "profile-" + item.Guid,
                    text = (item.Guid == selectedGuid ? "선택  ·  " : "") + item.Name + "\n" + item.Target + (item.ReadIssues.Count > 0 ? " · 오류" : item.IsCopy ? " · 복제" : ""), tooltip = "선택해도 Unity 활성 프로필은 바뀌지 않습니다.\n" + item.Path };
                button.AddToClassList("profile-row"); button.EnableInClassList("selected", item.Guid == selectedGuid); profileList.Add(button);
            }
            if (profiles.Count == 0) profileList.Add(new Label("Build Profile이 없습니다. Assets/Settings/BuildProfiles를 확인하세요.") { style = { whiteSpace = WhiteSpace.Normal } });
        }

        private void DrawDetail()
        {
            detail.Clear();
            var item = profiles.FirstOrDefault(value => value.Guid == selectedGuid);
            inspect.SetEnabled(item != null && !EditorApplication.isCompiling && !EditorApplication.isUpdating && !EditorApplication.isPlayingOrWillChangePlaymode);
            executionPreflight = null;
            if (item == null) { detail.Add(new Label("조회할 프로필을 선택하세요.")); return; }
            if (BuildToolPlatforms.Default.TryGet(item.Target, out var platform, out _) && platform is IBuildToolPlatformSettings settings)
                settings.DrawSettings(detail, item, ReloadProfiles);
            var overview = Section(detail, "product-section", "02  선택한 제품 정보", "이번에 빌드할 대상입니다. 현재 활성 프로필과 달라도 정상입니다.");
            var productTitle = new Label(item.Name) { name = "selected-profile-name" }; productTitle.AddToClassList("selected-title"); overview.Add(productTitle);
            var grid = new VisualElement(); grid.AddToClassList("info-grid"); overview.Add(grid);
            InfoField(grid, "대상 플랫폼", item.Target.ToString(), "선택한 제품이 실행될 플랫폼입니다. 게임과 레벨툴은 별도 제품입니다.");
            InfoField(grid, "앱 이름", item.ProductName ?? "미확인", "기기에 표시될 제품 이름입니다.");
            InfoField(grid, "앱 식별자", item.ApplicationId ?? "미확인", "스토어와 기기에서 앱을 구분하는 고유 식별자입니다.");
            InfoField(grid, "고정 출력 위치", item.Product?.Output ?? "미등록", "완성된 빌드가 저장될 위치입니다. 이 도구에서는 제품별 고정 경로를 사용합니다.");
            string info = "제품: " + (item.Product?.Name ?? "미등록 / 읽기 오류") + "\n플랫폼: " + (item.Target == BuildTarget.NoTarget ? "미확인" : item.Target.ToString()) +
                "\n앱 이름: " + (item.ProductName ?? "미확인") + "\n앱 식별자: " + (item.ApplicationId ?? "미확인") +
                "\n시작 씬: " + string.Join(", ", item.Scenes) + "\n제품/기능 심볼: " + string.Join(", ", item.Symbols) +
                "\n기본 출력: " + (item.Product?.Output ?? "미등록") + "\n에셋: " + item.Path + "\nGUID: " + item.Guid +
                "\n활성 여부: " + (item.Profile != null && BuildProfile.GetActiveBuildProfile() == item.Profile ? "현재 활성 프로필" : "활성 아님 · 선택만으로 전환하지 않음");
            var technical = new Foldout { text = "기술 정보 · 씬 / 심볼 / 에셋 경로", value = false, tooltip = "문제 확인에 필요한 내부 식별 정보입니다. 펼쳐서 내용을 선택·복사할 수 있습니다." }; overview.Add(technical);
            var information = new Label(info) { name = "profile-information", selection = { isSelectable = true } }; information.AddToClassList("technical"); technical.Add(information);
            DrawOptions(item.Product);
            var report = BuildToolPreflight.Inspect(item);
            executionPreflight = report;
            var checks = Section(detail, "checks-section", "04  사전 검사", "설정 점검 결과입니다. ‘미확인’은 실제 SDK·빌드·기기 검증을 하지 않았다는 뜻입니다.");
            var summary = new Label(report.Summary) { name = "preflight-summary" }; summary.AddToClassList("badge"); summary.AddToClassList(report.HasErrors ? "error" : "unknown"); checks.Add(summary);
            foreach (var issue in report.Issues)
            {
                string level = issue.Level == BuildToolIssueLevel.Error ? "차단 오류" : issue.Level == BuildToolIssueLevel.Warning ? "주의" : issue.Level == BuildToolIssueLevel.Unknown ? "미확인" : "확인";
                var message = new VisualElement { tooltip = issue.Action }; message.AddToClassList("issue");
                string tone = issue.Level == BuildToolIssueLevel.Error ? "error" : issue.Level == BuildToolIssueLevel.Warning ? "warning" : issue.Level == BuildToolIssueLevel.Pass ? "good" : "unknown";
                message.AddToClassList(tone); checks.Add(message);
                var heading = new Label(level + "  ·  " + issue.Location); heading.AddToClassList(tone); message.Add(heading);
                message.Add(new Label(issue.Message));
                if (issue.Level != BuildToolIssueLevel.Pass) { var action = new Label(issue.Action); action.AddToClassList("caption"); message.Add(action); }
            }
            notice.text = "목록 선택·사전 검사는 설정을 바꾸지 않습니다. 빌드는 별도 확인 후 실행합니다. " + preferenceNotice;
        }

        private void OnProjectChanged() => ReloadProfiles();

        private void OnEditorStateChanged()
        {
            RefreshExecution();
            var current = BuildProfile.GetActiveBuildProfile();
            string state = EditorApplication.isCompiling + ":" + EditorApplication.isUpdating + ":" + EditorApplication.isPlayingOrWillChangePlaymode +
                ":" + EditorUserBuildSettings.activeBuildTarget + ":" + (current == null ? 0 : current.GetInstanceID()) + ":" + BuildToolController.Runner.IsLocked;
            if (state == editorState) return;
            editorState = state; ReloadProfiles();
        }
    }
}
