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
        private bool busy;
        internal string ConfigurationPath = AuthoringSourceSelection.DefaultConfiguration;
        internal string CandidatesRoot = "ContentData/Candidates";

        [MenuItem("Match/제작 데이터/가져오기·리소스 변환")]
        public static void Open() => GetWindow<ContentImportWindow>("제작 데이터 가져오기").Show();

        public void CreateGUI()
        {
            minSize = new Vector2(640, 480);
            var root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = root.style.paddingRight = 16;
            root.style.paddingTop = root.style.paddingBottom = 12;
            var title = new Label("외부 제작 프로젝트 가져오기");
            title.style.fontSize = 20; title.style.unityFontStyleAndWeight = FontStyle.Bold;
            root.Add(title);
            root.Add(new HelpBox("1. ZIP 선택 → 2. 검사·변경 비교 → 3. 정식 원본 반영 → 4. 리소스 변환\n프로젝트 전체를 교체합니다. 이전 원본 폴더는 보존하며 일부 레벨만 병합하지 않습니다.", HelpBoxMessageType.Info));
            var toolbar = new VisualElement(); toolbar.style.flexDirection = FlexDirection.Row;
            choose = new Button(ChooseZip) { text = "1. ZIP 선택·검사", name = "choose-zip", tooltip = "레벨툴에서 내보낸 프로젝트 ZIP을 별도 후보 폴더로 가져옵니다." };
            inspect = new Button(() => Run(() => Display(ExternalProjectImport.Inspect(report.Folder, ConfigurationPath, CancellationToken.None, Progress))))
                { text = "후보 다시 검사", name = "inspect-candidate", tooltip = "후보나 현재 원본이 바뀌면 변경 목록을 다시 계산합니다." };
            toolbar.Add(choose); toolbar.Add(inspect); root.Add(toolbar);
            summary = new Label { name = "import-summary" }; summary.style.whiteSpace = WhiteSpace.Normal;
            summary.style.marginTop = summary.style.marginBottom = 8; root.Add(summary);
            details = new ScrollView { name = "import-details" }; details.style.flexGrow = 1; details.style.minHeight = 80; root.Add(details);
            confirm = new Toggle("변경 목록과 제외 예정 항목을 확인했으며 프로젝트 전체 교체에 동의합니다.")
                { name = "confirm-replacement", tooltip = "다른 프로젝트 ID 또는 제외 예정 문서를 확인하세요. 기존 원본 파일을 삭제하지는 않습니다." };
            confirm.style.whiteSpace = WhiteSpace.Normal;
            confirm.RegisterValueChangedCallback(_ => RefreshButtons()); root.Add(confirm);
            adopt = new Button(AdoptCandidate) { text = "3. 정식 JSON 원본으로 반영", name = "adopt-candidate" }; root.Add(adopt);
            status = new Label("ZIP을 선택하세요. 검사만으로 기존 원본이나 게임 리소스는 변경되지 않습니다.") { name = "import-status" };
            status.style.whiteSpace = WhiteSpace.Normal; status.style.marginTop = 8; root.Add(status);
            var packActions = new VisualElement(); packActions.style.flexDirection = FlexDirection.Row;
            convert = new Button(ConvertPacks) { text = "4. MemoryPack 변환·등록", name = "convert-packs", tooltip = "현재 정식 JSON 원본을 50레벨 단위 팩과 요소 팩으로 변환하고 Addressables에 등록합니다." };
            refreshPacks = new Button(RefreshPackStatus) { text = "생성 상태 확인", tooltip = "원본과 생성 세대 해시 및 팩 파일의 무결성을 다시 확인합니다." };
            packActions.Add(convert); packActions.Add(refreshPacks); root.Add(packActions);
            packStatus = new Label { name = "pack-status" }; packStatus.style.whiteSpace = WhiteSpace.Normal; root.Add(packStatus);
            var foldout = new Foldout { text = "원본·생성 정보", value = false };
            var packScroll = new ScrollView(); packScroll.style.maxHeight = 160;
            packDetails = new Label(); packDetails.style.whiteSpace = WhiteSpace.Normal;
            packScroll.Add(packDetails); foldout.Add(packScroll); root.Add(foldout);
            RefreshPackStatus();
            RefreshButtons();
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
            report = null; confirm.SetValueWithoutNotify(false); RefreshButtons();
            using var stream = File.OpenRead(path);
            try { Display(ExternalProjectImport.Import(stream, Path.GetFullPath(CandidatesRoot), ConfigurationPath, CancellationToken.None, Progress)); }
            finally { EditorUtility.ClearProgressBar(); }
        }

        private void Display(ExternalImportReport value)
        {
            report = value; confirm.SetValueWithoutNotify(false); details.Clear();
            summary.text = "프로젝트 ID: " + report.ProjectId + "\n후보 폴더: " + report.Folder +
                "\n" + (report.DifferentProject ? "주의: 현재와 다른 프로젝트입니다. " : "") + "제외 예정 문서: " + report.RemovedCount;
            foreach (string error in report.Errors) details.Add(new HelpBox(error, HelpBoxMessageType.Error));
            foreach (string change in report.Changes)
            {
                var row = new Label(change); row.style.whiteSpace = WhiteSpace.Normal; row.style.marginBottom = 4; details.Add(row);
            }
            status.text = report.CanAdopt ? "검사 통과 · 변경 목록을 확인한 뒤 반영하세요. 아직 원본 선택과 팩은 변경되지 않았습니다." :
                "검사 실패 · " + report.Errors.Count + "건. 후보를 수정하고 다시 검사하세요. 기존 원본은 유지됩니다.";
            RefreshButtons();
        }

        internal void AdoptCandidate()
        {
            if (busy || report == null || !confirm.value) return;
            Run(() =>
            {
                string backup = ExternalProjectImport.Adopt(report, confirm.value, CancellationToken.None, Progress);
                status.text = "정식 JSON 원본 반영 완료 · 리소스 갱신 필요. 아래 변환 버튼으로 생성하세요.\n이전 설정: " + (backup ?? "이전 선택 없음");
                report = null; confirm.SetValueWithoutNotify(false); RefreshButtons();
                RefreshPackStatus();
            });
        }

        private void RefreshPackStatus()
        {
            try
            {
                bool pending = File.Exists("Library/AuthoringPacks/publication.json");
                string source = pending ? null : AuthoringSourceSelection.ReadFolder(ConfigurationPath);
                ContentPackStatus current = ContentPackStatus.Read(source, Path.GetFullPath("."));
                string label = current.State == "Current" ? "생성 파일 최신" : current.State == "Missing" ? "리소스 미생성" :
                    current.State == "Outdated" ? "리소스 갱신 필요" : current.State == "RecoveryRequired" ? "이전 작업 복구 필요" : "리소스 확인 실패";
                packStatus.text = label + (pending ? "" : " · 레벨 " + current.LevelCount + "개") + (current.Error == null ? "" : "\n" + current.Error);
                packDetails.text = "정식 원본: " + (source ?? "복구 후 확인") + "\n원본 해시: " + current.SourceHash + "\n생성 해시: " + (current.GeneratedHash ?? "없음") +
                    "\n생성 시간: " + (current.GeneratedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "없음") + "\n출력:\n" + string.Join("\n", current.OutputPaths);
            }
            catch (Exception error) { packStatus.text = "원본 확인 실패: " + error.Message; packDetails.text = ""; }
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
            busy = true; RefreshButtons(); status.text = "검사 중 · 레벨과 튜토리얼을 확인하고 있습니다…";
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

        private void RefreshButtons()
        {
            choose?.SetEnabled(!busy);
            convert?.SetEnabled(!busy);
            refreshPacks?.SetEnabled(!busy);
            inspect?.SetEnabled(!busy && report != null);
            confirm?.SetEnabled(!busy && report != null && report.CanAdopt);
            adopt?.SetEnabled(!busy && report != null && report.CanAdopt && confirm.value);
        }
    }
}
