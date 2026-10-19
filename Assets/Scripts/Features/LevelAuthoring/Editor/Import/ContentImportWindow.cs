using System;
using System.IO;
using System.Threading;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelAuthoring.Editor
{
    public sealed class ContentImportWindow : EditorWindow
    {
        private ExternalImportReport report;
        private Label status, summary;
        private ScrollView details;
        private Toggle confirm;
        private Button adopt, inspect, choose;
        private Button convert, refreshPacks;
        private Label packStatus, packDetails;
        private bool busy, adopted, sourceAvailable, packsCurrent;
        private int step;
        private VisualElement[] steps;
        private Button[] stepButtons;
        private Button previous, next, useSelected;
        private Label guidance, stepState;
        private ScrollView page;
        internal string ConfigurationPath = AuthoringSourceSelection.DefaultConfiguration;
        internal string CandidatesRoot = "ContentData/Candidates";

        [MenuItem("Match/제작 데이터/가져오기·리소스 변환")]
        public static void Open() => GetWindow<ContentImportWindow>("제작 데이터 가져오기").Show();

        public void CreateGUI()
        {
            minSize = new Vector2(640, 480);
            VisualElement root = rootVisualElement;
            root.Clear();
            root.AddToClassList("content-import");
            StyleSheet sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Scripts/Features/LevelAuthoring/Editor/Import/ContentImportWindow.uss");
            if (sheet != null && !root.styleSheets.Contains(sheet)) root.styleSheets.Add(sheet);

            VisualElement header = new VisualElement(); header.AddToClassList("import-header");
            Label title = new Label("제작 데이터"); title.AddToClassList("import-title"); header.Add(title);
            header.Add(new Label("레벨툴의 ZIP을 확인하고 게임 리소스로 연결합니다."));
            header.Add(new Button(() => ContentImportHistoryWindow.Open(
                () => this == null ? null : report?.Folder, () => this != null && busy))
                { text = "히스토리 정리", name = "cleanup-history", tooltip = "사용하지 않는 후보·백업·임시 파일을 미리 확인하고 정리합니다. 현재 원본과 게임 리소스는 보존합니다." });
            root.Add(header);
            VisualElement progress = new VisualElement { name = "import-steps" };
            progress.AddToClassList("import-steps"); root.Add(progress);
            string[] titles = { "1  ZIP 선택", "2  변경 확인·반영", "3  리소스 변환" };
            stepButtons = new Button[3];
            for (int i = 0; i < titles.Length; i++)
            {
                int target = i;
                stepButtons[i] = new Button(() => GoToStep(target)) { name = "import-step-" + i, text = titles[i] };
                progress.Add(stepButtons[i]);
            }
            stepState = new Label { name = "import-step-state" }; stepState.AddToClassList("import-step-state"); root.Add(stepState);
            guidance = new Label { name = "import-guidance" }; guidance.AddToClassList("import-guidance"); root.Add(guidance);
            page = new ScrollView(ScrollViewMode.Vertical) { name = "import-page" };
            page.AddToClassList("import-page"); root.Add(page);

            VisualElement candidate = Section(page, "01   ZIP 가져오기 · 검사", "검사만으로 현재 원본과 게임 리소스가 바뀌지는 않습니다.");
            VisualElement toolbar = new VisualElement(); toolbar.AddToClassList("import-actions");
            choose = new Button(ChooseZip) { text = "ZIP 선택·검사", name = "choose-zip", tooltip = "레벨툴에서 내보낸 프로젝트 ZIP을 별도 후보 폴더에 풀고 변경 내용을 검사합니다." };
            choose.AddToClassList("primary-action");
            inspect = new Button(() => Run(() => Display(ExternalProjectImport.Inspect(report.Folder, ConfigurationPath, CancellationToken.None, Progress))))
                { text = "후보 다시 검사", name = "inspect-candidate", tooltip = "후보나 현재 원본이 바뀌면 변경 목록을 다시 계산합니다." };
            toolbar.Add(choose); toolbar.Add(inspect); candidate.Add(toolbar);
            useSelected = new Button(() => GoToStep(2)) { name = "use-selected-source", text = "이미 반영한 원본으로 리소스 변환", tooltip = "새 ZIP을 가져오지 않고 현재 프로젝트가 선택한 JSON 원본을 변환합니다." };
            candidate.Add(useSelected);
            summary = new Label("선택한 ZIP이 없습니다. 먼저 가져올 파일을 선택하세요.") { name = "import-summary" };
            summary.AddToClassList("import-note"); candidate.Add(summary);
            details = new ScrollView(ScrollViewMode.Vertical) { name = "import-details" };
            details.AddToClassList("import-changes"); candidate.Add(details);
            details.Add(new Label("검사 후 추가·변경·제외 예정 항목이 여기에 표시됩니다."));

            VisualElement adoption = Section(page, "02   편집 원본으로 반영", "프로젝트 전체를 교체합니다. 일부 레벨만 병합하지 않으며, 이전 원본 폴더는 보존합니다.");
            adoption.Add(summary); adoption.Add(details); adoption.Add(inspect);
            confirm = new Toggle("변경 목록과 제외 예정 항목을 확인했으며 프로젝트 전체 교체에 동의합니다.")
                { name = "confirm-replacement", tooltip = "다른 프로젝트 ID 또는 제외 예정 문서를 확인하세요. 기존 원본 파일을 삭제하지는 않습니다." };
            confirm.RegisterValueChangedCallback(_ => RefreshButtons()); adoption.Add(confirm);
            adopt = new Button(AdoptCandidate) { text = "검사한 JSON을 편집 원본으로 반영", name = "adopt-candidate", tooltip = "검사를 통과한 후보를 이후 게임 리소스 변환에 사용할 원본으로 선택합니다. 팩 변환은 다음 단계에서 진행합니다." };
            adoption.Add(adopt);

            VisualElement publication = Section(page, "03   게임 리소스 변환", "현재 선택된 JSON 원본을 50레벨 단위 MemoryPack으로 묶고 Addressables에 등록합니다.");
            packStatus = new Label { name = "pack-status" }; packStatus.AddToClassList("import-pack-status"); publication.Add(packStatus);
            VisualElement packActions = new VisualElement(); packActions.AddToClassList("import-actions");
            convert = new Button(ConvertPacks) { text = "MemoryPack 변환·등록", name = "convert-packs", tooltip = "위에서 반영한 원본 또는 이전에 선택한 원본을 변환합니다. ZIP을 다시 가져올 필요는 없습니다." };
            convert.AddToClassList("primary-action");
            refreshPacks = new Button(RefreshPackStatus) { text = "생성 상태 확인", tooltip = "원본과 생성 세대 해시 및 팩 파일의 무결성을 다시 확인합니다." };
            packActions.Add(convert); packActions.Add(refreshPacks); publication.Add(packActions);
            Foldout foldout = new Foldout { text = "원본 경로 · 생성 정보", value = false };
            ScrollView packScroll = new ScrollView(ScrollViewMode.Vertical); packScroll.AddToClassList("import-pack-details");
            packDetails = new Label(); packDetails.selection.isSelectable = true;
            packScroll.Add(packDetails); foldout.Add(packScroll); publication.Add(foldout);

            steps = new[] { candidate, adoption, publication };
            for (int i = 0; i < steps.Length; i++) steps[i].name = "import-stage-" + i;
            VisualElement navigation = new VisualElement(); navigation.AddToClassList("import-navigation"); root.Add(navigation);
            previous = new Button(() => GoToStep(step == 2 && report == null && !adopted ? 0 : step - 1)) { name = "previous-step", text = "← 이전 단계", tooltip = "이전 내용을 확인합니다. 이미 반영한 원본이나 생성한 리소스는 되돌리지 않습니다." };
            next = new Button(() => GoToStep(step + 1)) { name = "next-step" }; next.AddToClassList("primary-action");
            navigation.Add(previous); navigation.Add(next);
            VisualElement result = Section(root, "최근 작업 결과", "");
            result.AddToClassList("import-result-section");
            ScrollView resultScroll = new ScrollView(ScrollViewMode.Vertical); resultScroll.AddToClassList("import-result");
            status = new Label("작업 대기 중 · ZIP을 선택하거나 현재 원본의 생성 상태를 확인하세요.") { name = "import-status" };
            status.selection.isSelectable = true; resultScroll.Add(status); result.Add(resultScroll);
            if (report != null) Display(report);
            RefreshPackStatus();
            RefreshButtons();
        }

        private static VisualElement Section(VisualElement parent, string title, string description)
        {
            VisualElement section = new VisualElement(); section.AddToClassList("import-section");
            Label heading = new Label(title); heading.AddToClassList("import-section-title"); section.Add(heading);
            Label hint = new Label(description); hint.AddToClassList("import-hint"); section.Add(hint);
            parent.Add(section);
            return section;
        }

        private void ChooseZip()
        {
            string path = EditorUtility.OpenFilePanel("제작 프로젝트 ZIP 가져오기", "", "zip");
            if (string.IsNullOrEmpty(path)) return;
            // 다음 UI 갱신에서 검사 중 안내를 표시한 뒤 Unity 객체 검증을 수행한다.
            Run(() => LoadZip(path));
        }

        internal void LoadZip(string path)
        {
            report = null; adopted = false; step = 0;
            summary.text = "ZIP 검사 중…"; details.Clear();
            confirm.SetValueWithoutNotify(false); RefreshButtons();
            using var stream = File.OpenRead(path);
            try { Display(ExternalProjectImport.Import(stream, Path.GetFullPath(CandidatesRoot), ConfigurationPath, CancellationToken.None, Progress)); }
            finally { EditorUtility.ClearProgressBar(); }
        }

        private void Display(ExternalImportReport value)
        {
            report = value; adopted = false; confirm.SetValueWithoutNotify(false); details.Clear();
            summary.text = "프로젝트 ID: " + report.ProjectId + "\n후보 폴더: " + report.Folder +
                "\n" + (report.DifferentProject ? "주의: 현재와 다른 프로젝트입니다. " : "") + "제외 예정 문서: " + report.RemovedCount;
            foreach (string error in report.Errors) details.Add(new HelpBox(error, HelpBoxMessageType.Error));
            foreach (string change in report.Changes)
            {
                var row = new Label(change); row.AddToClassList("import-change-row"); row.selection.isSelectable = true; details.Add(row);
            }
            status.text = report.CanAdopt ? "검사 통과 · 변경 목록을 확인한 뒤 반영하세요. 아직 원본 선택과 팩은 변경되지 않았습니다." :
                "검사 실패 · " + report.Errors.Count + "건. 기존 원본은 유지됩니다.\n" + string.Join("\n", report.Errors);
            step = 1;
            RefreshButtons();
        }

        internal void AdoptCandidate()
        {
            if (busy || report == null || !confirm.value) return;
            Run(() =>
            {
                string backup = ExternalProjectImport.Adopt(report, confirm.value, CancellationToken.None, Progress);

                adopted = true; report = null; step = 2; confirm.SetValueWithoutNotify(false); RefreshButtons();
                RefreshPackStatus();
                status.text = "JSON 원본 반영 완료 · " + (packsCurrent ? "생성된 리소스가 이미 최신입니다." : "리소스 갱신 필요. MemoryPack 변환·등록을 진행하세요.") +
                    "\n이전 설정: " + (backup ?? "이전 선택 없음");
            });
        }

        private void RefreshPackStatus()
        {
            try
            {
                bool pending = File.Exists("Library/AuthoringPacks/publication.json");
                string source = pending ? null : AuthoringSourceSelection.ReadFolder(ConfigurationPath);
                sourceAvailable = pending || !string.IsNullOrEmpty(source);
                ContentPackStatus current = ContentPackStatus.Read(source, Path.GetFullPath("."));
                packsCurrent = current.State == "Current";
                string label = current.State == "Current" ? "생성 파일 최신" : current.State == "Missing" ? "리소스 미생성" :
                    current.State == "Outdated" ? "리소스 갱신 필요" : current.State == "RecoveryRequired" ? "이전 작업 복구 필요" : "리소스 확인 실패";
                packStatus.text = label + (pending ? "" : " · 레벨 " + current.LevelCount + "개") + (current.Error == null ? "" : "\n" + current.Error);
                packDetails.text = "정식 원본: " + (source ?? "복구 후 확인") + "\n원본 해시: " + current.SourceHash + "\n생성 해시: " + (current.GeneratedHash ?? "없음") +
                    "\n생성 시간: " + (current.GeneratedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "없음") + "\n출력:\n" + string.Join("\n", current.OutputPaths);
            }
            catch (Exception error) { sourceAvailable = false; packsCurrent = false; packStatus.text = "원본 확인 실패: " + error.Message; packDetails.text = ""; }
            RefreshButtons();
        }

        private void ConvertPacks()
        {
            Run(() =>
            {
                try
                {
                    Progress("현재 정식 원본을 MemoryPack으로 변환합니다.", 0.1f);
                    JsonPackRegistration.PublishSelected(ConfigurationPath);
                    status.text = "MemoryPack 변환·Addressables 등록 완료.";
                }
                catch (OperationCanceledException) { status.text = "변환 취소 · 기존 리소스를 유지합니다."; }
                catch (Exception error)
                {
                    bool pending = File.Exists("Library/AuthoringPacks/publication.json");
                    status.text = (pending ? "복구 미완료 · 복구 기록을 보존했습니다. 다시 변환하면 복구를 먼저 시도합니다.\n" :
                        "변환 실패 · 팩 교체 전 중단했거나 이전 팩·등록 상태로 복구했습니다.\n") + error.Message;
                }
                finally { RefreshPackStatus(); }
            });
        }

        private void Run(Action action)
        {
            if (busy) return;
            busy = true; RefreshButtons(); status.text = "작업 진행 중 · 완료될 때까지 기다려 주세요…";
            EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                try { action(); }
                catch (OperationCanceledException)
                {
                    report = null; confirm.SetValueWithoutNotify(false); status.text = "검사 취소 · 기존 원본 선택과 리소스를 유지합니다. 후보 파일은 보존됩니다.";
                }
                catch (Exception error)
                {
                    report = null; confirm.SetValueWithoutNotify(false);
                    status.text = "작업 실패 · 기존 선택 유지: " + error.Message;
                }
                finally { EditorUtility.ClearProgressBar(); busy = false; RefreshButtons(); }
            };
        }

        private static void Progress(string message, float value)
        {
            if (EditorUtility.DisplayCancelableProgressBar("제작 데이터 검사", message, value)) throw new OperationCanceledException();
        }

        private void GoToStep(int target)
        {
            if (busy || target < 0 || target > 2) return;
            if (target == 1 && report == null && !adopted) return;
            if (target == 2 && (!sourceAvailable || report != null && !adopted)) return;
            step = target;
            page.scrollOffset = Vector2.zero;
            RefreshButtons();
        }

        private void RefreshSteps()
        {
            if (steps == null || stepButtons == null) return;
            string[] titles = { "1  ZIP 선택", "2  변경 확인·반영", "3  리소스 변환" };
            bool[] completed = { report != null && report.CanAdopt || adopted, adopted, packsCurrent && sourceAvailable && report == null };
            for (int i = 0; i < steps.Length; i++)
            {
                steps[i].style.display = i == step ? DisplayStyle.Flex : DisplayStyle.None;
                stepButtons[i].text = titles[i] + (completed[i] ? " · 완료" : "");
                stepButtons[i].EnableInClassList("current-step", i == step);
                stepButtons[i].SetEnabled(!busy && (i == 0 || i == 1 && (report != null || adopted) ||
                    i == 2 && sourceAvailable && report == null));
            }
            stepState.text = busy ? "작업 진행 중 · 잠시 기다려 주세요." :
                "현재 " + (step + 1) + " / 3 단계" + (completed[step] ? " · 완료" : " · 확인 필요");
            guidance.text = step == 0 ? "지금 할 일: 가져올 ZIP을 선택하세요. 이미 반영한 원본은 ZIP 없이 3단계로 이동할 수 있습니다." :
                step == 1 ? adopted ? "원본 반영 완료. 변경 내역을 다시 확인하거나 3단계에서 게임 리소스를 생성하세요." :
                report != null && report.CanAdopt ? "지금 할 일: 아래 변경 목록을 확인하고 교체 동의 후 원본 반영 버튼을 누르세요. 성공하면 3단계로 이동합니다." :
                "검사를 통과하지 못했습니다. 오류를 확인하고 이전 단계에서 ZIP을 다시 선택하거나 후보를 다시 검사하세요." :
                packsCurrent ? "완료: 현재 원본과 생성된 게임 리소스가 일치합니다. 새 ZIP을 가져오려면 1단계로 돌아가세요." :
                "지금 할 일: 현재 원본을 확인한 뒤 MemoryPack 변환·등록을 누르세요. 완료되면 이 화면에서 최신 상태를 확인할 수 있습니다.";
            previous.SetEnabled(!busy && step > 0);
            // 기존 원본만 사용하는 경로에서는 ZIP 확인 단계가 없으므로 첫 화면으로 돌아간다.
            previous.text = step == 2 && report == null && !adopted ? "← ZIP 선택으로" : "← 이전 단계";
            next.text = step == 0 ? "변경 내용 확인 →" : "리소스 변환으로 →";
            next.style.display = step == 2 ? DisplayStyle.None : DisplayStyle.Flex;
            next.SetEnabled(!busy && (step == 0 ? report != null || adopted : adopted));
            useSelected.SetEnabled(!busy && sourceAvailable && report == null);
            confirm.style.display = adopt.style.display = adopted ? DisplayStyle.None : DisplayStyle.Flex;
        }
        private void RefreshButtons()
        {
            choose?.SetEnabled(!busy);
            convert?.SetEnabled(!busy && sourceAvailable && (report == null || adopted));
            refreshPacks?.SetEnabled(!busy);
            inspect?.SetEnabled(!busy && report != null);
            confirm?.SetEnabled(!busy && report != null && report.CanAdopt);
            adopt?.SetEnabled(!busy && report != null && report.CanAdopt && confirm.value);
            RefreshSteps();
        }
    }
}
