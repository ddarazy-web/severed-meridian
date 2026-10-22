#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Threading;
using Cysharp.Threading.Tasks;
using LevelAuthoring.Storage;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelTool
{
    public sealed partial class LevelToolScreen
    {
        private ILevelToolFileTransfer fileTransfer;
        private CancellationTokenSource fileTransferCancellation;
        private RecentProjectStore recentProjects;

        private void ExportProject()
        {
            try
            {
                var snapshot = Session.CreateSnapshot();
                VisualElement panel = OpenModal("저장 후 ZIP 내보내기");
                Label summary = new Label($"프로젝트: {Path.GetFileName(Workspace.Folder)}\nID: {snapshot.Project.Id}\n레벨 {snapshot.Documents.Count(value => value.Kind == "level")}개 · 콘텐츠 버전 {snapshot.Project.Data["contentVersion"]}")
                    { name = "export-summary" };
                summary.style.whiteSpace = WhiteSpace.Normal; panel.Add(summary);
                Label guide = new Label("현재 편집 내용을 먼저 저장하고, 참조하는 JSON 전체를 ZIP으로 내보냅니다. 이미지·프리팹·로그·과거 백업은 포함하지 않습니다.\nZIP을 USB나 공유 폴더로 옮길 수 있습니다. 클라우드 업로드·동기화 완료는 해당 서비스에서 확인하세요.");
                guide.text += "\nZIP 위치 선택을 취소해도 먼저 완료한 JSON 저장은 유지됩니다.";
                guide.style.whiteSpace = WhiteSpace.Normal; panel.Add(guide);
                Button(panel, "export-project-confirm", "저장 후 ZIP 위치 선택", () =>
                { CloseModal(); RunProjectTransfer(false).Forget(); });
                Button(panel, "cancel", "취소", CloseModal);
            }
            catch (Exception failure) { Show("내보내기 준비 실패: " + failure.Message); }
        }

        private void ImportProject()
        {
            VisualElement panel = OpenModal("프로젝트 ZIP 가져오기");
            panel.Add(new Label("ZIP을 검사한 뒤 앱 내부의 새 프로젝트로 등록합니다. 기존 프로젝트는 덮어쓰지 않습니다."));
            TextField name = new TextField("새 프로젝트 이름") { name = "import-project-name" };
            panel.Add(name); Label error = new Label(); panel.Add(error);
            Button(panel, "import-project-confirm", "이 이름으로 ZIP 선택", () =>
            {
                try
                {
                    LevelToolProjectAccess.Resolve(InternalProjectsRoot, name.value, true);
                    string projectName = name.value; CloseModal(); RunProjectTransfer(true, projectName).Forget();
                }
                catch (Exception failure) { error.text = failure.Message; }
            });
            Button(panel, "cancel", "취소", CloseModal);
        }

        private async UniTask RunProjectTransfer(bool import, string projectName = null)
        {
            if (busy || fileTransferCancellation != null) return;
            if (sharedTutorialDraft != null) { Show("공유 원본 사본을 적용하거나 버린 뒤 파일 작업을 해 주세요."); return; }
            var previous = Session;
            FocusedInputState focus = commandInputFocus ?? CaptureFocusedInput(); commandInputFocus = null;

            var cancellation = new CancellationTokenSource(); fileTransferCancellation = cancellation;
            busy = true; editor.SetEnabled(false); RefreshCommandStates();
            string imported = null;
            bool saved = false;
            bool transferred = false;
            string destination = null;
            string savedHash = null;
            try
            {
                fileTransfer ??= LevelToolFileTransfer.CreateDefault();
                if (!import)
                {
                    BeginManualSave();
                    await UniTask.RunOnThreadPool(Workspace.Save); saved = true;
                    CompleteManualSave();
                    savedHash = Session.Revision;
                    cancellation.Token.ThrowIfCancellationRequested();
                    Show("내부 저장 완료 · ZIP 저장 위치를 선택하세요.");
                }
                else Show("가져올 프로젝트 ZIP을 선택하세요.");
                Stream stream = import ? await fileTransfer.OpenProject(cancellation.Token) :
                    await fileTransfer.CreateProject(Path.GetFileName(Workspace.Folder) + "_" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture) + ".zip", cancellation.Token);
                if (stream == null) { Show(saved ? "ZIP 전송 취소 · JSON 저장 완료 · 내부 저장본 유지" : "전송 취소"); return; }
                // Windows 파일 스트림은 실제 경로를 제공한다. SAF 등은 경로를 추정하지 않는다.
                destination = stream is FileStream file ? file.Name : "시스템 파일 선택기에서 지정한 문서";
                string savedFolder = Workspace.Folder, projectsRoot = InternalProjectsRoot;
                await UniTask.RunOnThreadPool(() =>
                {
                    // 호출자가 스트림을 소유한다. close 실패도 성공 안내 전에 관찰한다.
                    using (stream)
                    {
                        if (import) imported = ProjectPackageStore.Import(stream, projectsRoot, projectName, cancellation.Token);
                        else { ProjectPackageStore.Export(savedFolder, stream, cancellation.Token, savedHash); stream.Flush(); }
                    }
                });
                cancellation.Token.ThrowIfCancellationRequested();
                transferred = true;
                Show(import ? "ZIP 검사·내부 프로젝트 등록 완료" : "ZIP 내보내기 완료 · " + destination + " · 클라우드 동기화는 해당 서비스에서 확인하세요.");
            }
            catch (OperationCanceledException) { Show((saved ? "ZIP 전송 취소 · JSON 저장 완료 · 내부 저장본 유지" : "전송 취소") +
                (!import && saved ? " · 생성된 외부 파일이 있다면 불완전할 수 있습니다." : "")); }
            catch (Exception failure)
            {
                if (manualSaveInProgress) CompleteManualSave(failure);
                Show((import ? "가져오기 실패: " : saved ? "내보내기 실패 · 내부 저장본 유지: " : "내부 저장 실패 · ZIP 내보내기 실패: ") + failure.Message +
                    (!import && saved ? " · 생성된 외부 파일이 있다면 불완전할 수 있습니다." : ""));
            }
            finally
            {
                bool current = ReferenceEquals(fileTransferCancellation, cancellation);
                if (current) fileTransferCancellation = null;
                cancellation.Dispose();
                if (current && this != null) busy = false;
                if (current && this != null && isActiveAndEnabled)
                {
                    editor.SetEnabled(modal == null); Refresh();
                    if (ReferenceEquals(previous, Session)) RestoreFocusedInput(focus);

                    if (transferred && imported != null) WithDirty(choice => Workspace.OpenListing(imported, choice));
                }
            }
        }

        private void DisposeFileTransfer()
        {
            // 실행 중 worker가 끝날 때까지 요청 소유권과 잠금을 유지한다.
            fileTransferCancellation?.Cancel();
            (fileTransfer as IDisposable)?.Dispose(); fileTransfer = null;
        }

        private void RememberInternalProject()
        {
            if (Workspace.Folder == null || UsesInternalProjects && !string.Equals(Path.GetDirectoryName(Workspace.Folder), InternalProjectsRoot, StringComparison.OrdinalIgnoreCase)) return;
            try
            {
                recentProjects ??= new RecentProjectStore(Path.GetDirectoryName(InternalProjectsRoot), !UsesInternalProjects);
                recentProjects.RecordOpened(UsesInternalProjects ? Path.GetFileName(Workspace.Folder) : Workspace.Folder);
            }
            catch (Exception failure) { Show("프로젝트는 열렸지만 최근 기록 갱신 실패: " + failure.Message); }
        }

        private void OpenRecentProjects()
        {
            VisualElement panel = OpenModal("최근 프로젝트");
            Label notice = new Label("프로젝트를 선택해서 여세요. 위치가 바뀌었다면 ‘프로젝트 열기’를 사용하세요.")
                { style = { whiteSpace = WhiteSpace.Normal } }; panel.Add(notice);
            ScrollView list = new ScrollView(); list.style.maxHeight = 280; panel.Add(list);
            try
            {
                recentProjects ??= new RecentProjectStore(Path.GetDirectoryName(InternalProjectsRoot), !UsesInternalProjects);
                string[] names = recentProjects.Read();
                if (recentProjects.ReadWarning != null) notice.text = recentProjects.ReadWarning;
                foreach (string name in names)
                {
                    string path = UsesInternalProjects ? LevelToolProjectAccess.Resolve(InternalProjectsRoot, name) : name;
                    Button item = Button(list, "recent-project", name, () => { CloseModal(); WithDirty(choice => Workspace.OpenListing(path, choice)); });
                    if (!File.Exists(Path.Combine(path, "project.json"))) { item.SetEnabled(false); item.text += " · 저장본 없음"; }
                }
                if (names.Length == 0) list.Add(new Label("최근에 연 프로젝트가 없습니다. 새로 만들거나 프로젝트를 열어 주세요."));
            }
            catch (Exception failure) { notice.text = failure.Message; }
            Button(panel, "recent-open-other", "프로젝트 열기", () => { CloseModal(); TryExecuteCommand(ToolCommandId.Open); });
            Button(panel, "cancel", "닫기", CloseModal);
        }
    }
}
#endif
