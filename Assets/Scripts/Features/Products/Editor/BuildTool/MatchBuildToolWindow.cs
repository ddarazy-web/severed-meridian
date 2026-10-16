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

        [MenuItem("Match/Build/빌드 도구")]
        public static void Open() => GetWindow<MatchBuildToolWindow>("빌드 도구").Show();

        private void OnEnable()
        {
            minSize = new Vector2(640, 540);
            EditorApplication.projectChanged += OnProjectChanged;
            EditorApplication.update += OnEditorStateChanged;
        }

        private void OnDisable()
        {
            EditorApplication.projectChanged -= OnProjectChanged;
            EditorApplication.update -= OnEditorStateChanged;
        }

        public void CreateGUI()
        {
            var root = rootVisualElement; root.Clear();
            root.style.paddingLeft = root.style.paddingRight = 12;
            root.style.paddingTop = root.style.paddingBottom = 12;
            root.Add(new Label("제품 빌드 도구") { style = { fontSize = 20, unityFontStyleAndWeight = FontStyle.Bold } });
            active = new Label { name = "active-profile", style = { whiteSpace = WhiteSpace.Normal, marginTop = 6 } }; root.Add(active);
            notice = new Label { name = "build-tool-notice", style = { whiteSpace = WhiteSpace.Normal, marginTop = 6, marginBottom = 8 } }; root.Add(notice);
            var actions = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } }; root.Add(actions);
            actions.Add(new Button(ReloadProfiles) { name = "refresh-profiles", text = "목록 새로고침", tooltip = "프로필을 다시 읽습니다. 활성 제품이나 플랫폼을 전환하지 않습니다." });
            inspect = new Button(RunPreflight) { name = "inspect-profile", text = "선택 프로필 사전 검사" }; actions.Add(inspect);
            buildButton = new Button(RequestBuild) { name = "build-product", text = "선택 프로필 빌드…", tooltip = "대상과 실제 빌드 실행을 확인한 후에만 프로필을 전환하고 빌드합니다." };
            actions.Add(buildButton);
            cancelButton = new Button(CancelBuild) { name = "cancel-build", text = "준비·대기 취소" }; actions.Add(cancelButton);
            resolveButton = new Button(ResolveBuild) { name = "resolve-build", text = "미완료 요청 정리…" }; actions.Add(resolveButton);
            var body = new VisualElement { name = "build-tool-body", style = { flexDirection = FlexDirection.Row, flexGrow = 1, marginTop = 12 } }; root.Add(body);
            var listPanel = new VisualElement { style = { width = 240, marginRight = 12 } }; body.Add(listPanel);
            listPanel.Add(new Label("Build Profile 목록") { style = { unityFontStyleAndWeight = FontStyle.Bold } });
            profileList = new ScrollView { name = "profile-list", style = { flexGrow = 1 } }; listPanel.Add(profileList);
            detail = new ScrollView { name = "profile-detail", style = { flexGrow = 1, minWidth = 0 } }; body.Add(detail);
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
                    text = (item.Guid == selectedGuid ? "● " : "") + item.Name + (item.ReadIssues.Count > 0 ? " · 오류" : item.IsCopy ? " · 복제" : ""), tooltip = item.Path };
                button.style.whiteSpace = WhiteSpace.Normal; button.style.minHeight = 42; profileList.Add(button);
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
            detail.Add(new Label(item.Name) { name = "selected-profile-name", style = { fontSize = 18, unityFontStyleAndWeight = FontStyle.Bold, whiteSpace = WhiteSpace.Normal } });
            string info = "제품: " + (item.Product?.Name ?? "미등록 / 읽기 오류") + "\n플랫폼: " + (item.Target == BuildTarget.NoTarget ? "미확인" : item.Target.ToString()) +
                "\n앱 이름: " + (item.ProductName ?? "미확인") + "\n앱 식별자: " + (item.ApplicationId ?? "미확인") +
                "\n시작 씬: " + string.Join(", ", item.Scenes) + "\n제품/기능 심볼: " + string.Join(", ", item.Symbols) +
                "\n기본 출력: " + (item.Product?.Output ?? "미등록") + "\n에셋: " + item.Path + "\nGUID: " + item.Guid +
                "\n활성 여부: " + (item.Profile != null && BuildProfile.GetActiveBuildProfile() == item.Profile ? "현재 활성 프로필" : "활성 아님 · 선택만으로 전환하지 않음");
            detail.Add(new Label(info) { name = "profile-information", style = { whiteSpace = WhiteSpace.Normal, marginTop = 8 } });
            DrawOptions(item.Product);
            var report = BuildToolPreflight.Inspect(item);
            executionPreflight = report;
            detail.Add(new Label(report.Summary) { name = "preflight-summary", style = { unityFontStyleAndWeight = FontStyle.Bold, whiteSpace = WhiteSpace.Normal, marginTop = 14 } });
            foreach (var issue in report.Issues)
            {
                string level = issue.Level == BuildToolIssueLevel.Error ? "차단 오류" : issue.Level == BuildToolIssueLevel.Warning ? "주의" : issue.Level == BuildToolIssueLevel.Unknown ? "미확인" : "확인";
                var message = new HelpBox("[" + level + "] " + issue.Location + "\n" + issue.Message + "\n" + issue.Action,
                    issue.Level == BuildToolIssueLevel.Error ? HelpBoxMessageType.Error : issue.Level == BuildToolIssueLevel.Warning ? HelpBoxMessageType.Warning : HelpBoxMessageType.Info);
                message.style.marginTop = 6; detail.Add(message);
            }
            notice.text = "목록 선택·사전 검사는 설정을 바꾸지 않습니다. 빌드는 별도 확인 후 실행합니다. " + preferenceNotice;
        }

        private void OnProjectChanged() => ReloadProfiles();

        private void OnEditorStateChanged()
        {
            RefreshExecution();
            var current = BuildProfile.GetActiveBuildProfile();
            string state = EditorApplication.isCompiling + ":" + EditorApplication.isUpdating + ":" + EditorApplication.isPlayingOrWillChangePlaymode +
                ":" + EditorUserBuildSettings.activeBuildTarget + ":" + (current == null ? 0 : current.GetInstanceID());
            if (state == editorState) return;
            editorState = state; ReloadProfiles();
        }
    }
}
