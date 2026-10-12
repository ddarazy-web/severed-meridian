#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.IO;
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
            CheckpointOnInterruption();
            var cancellation = new CancellationTokenSource(); fileTransferCancellation = cancellation;
            busy = true; editor.SetEnabled(false); RefreshCommandStates();
            string imported = null;
            bool saved = false;
            bool transferred = false;
            try
            {
                fileTransfer ??= LevelToolFileTransfer.CreateDefault();
                if (!import)
                {
                    await UniTask.RunOnThreadPool(Workspace.Save); saved = true;
                    cancellation.Token.ThrowIfCancellationRequested();
                    Show("내부 저장 완료 · ZIP 저장 위치를 선택하세요.");
                }
                else Show("가져올 프로젝트 ZIP을 선택하세요.");
                Stream stream = import ? await fileTransfer.OpenProject(cancellation.Token) :
                    await fileTransfer.CreateProject(Path.GetFileName(Workspace.Folder) + ".zip", cancellation.Token);
                if (stream == null) { Show(saved ? "전송 취소 · 내부 저장본 유지" : "전송 취소"); return; }
                string savedFolder = Workspace.Folder, projectsRoot = InternalProjectsRoot;
                await UniTask.RunOnThreadPool(() =>
                {
                    // 호출자가 스트림을 소유한다. close 실패도 성공 안내 전에 관찰한다.
                    using (stream)
                    {
                        if (import) imported = ProjectPackageStore.Import(stream, projectsRoot, projectName, cancellation.Token);
                        else { ProjectPackageStore.Export(savedFolder, stream, cancellation.Token); stream.Flush(); }
                    }
                });
                cancellation.Token.ThrowIfCancellationRequested();
                transferred = true;
                Show(import ? "ZIP 검사·내부 프로젝트 등록 완료" : "ZIP 내보내기 완료");
            }
            catch (OperationCanceledException) { Show(saved ? "전송 취소 · 내부 저장본 유지" : "전송 취소"); }
            catch (Exception failure)
            {
                Show((import ? "가져오기 실패: " : "내보내기 실패 · 내부 저장본 유지: ") + failure.Message +
                    (import ? "" : " · 외부 파일은 불완전할 수 있습니다."));
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
                    CheckpointOnInterruption();
                    if (transferred && imported != null) WithDirty(choice => Workspace.Open(imported, choice));
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
            if (Workspace.Folder == null || !string.Equals(Path.GetDirectoryName(Workspace.Folder), InternalProjectsRoot, StringComparison.OrdinalIgnoreCase)) return;
            try
            {
                recentProjects ??= new RecentProjectStore(Path.GetDirectoryName(InternalProjectsRoot));
                recentProjects.RecordOpened(Path.GetFileName(Workspace.Folder));
            }
            catch (Exception failure) { Show("프로젝트는 열렸지만 최근 기록 갱신 실패: " + failure.Message); }
        }

        private void OpenRecentProjects()
        {
            VisualElement panel = OpenModal("최근 내부 프로젝트");
            Label notice = new Label("일반 Windows 작업 폴더는 기존 ‘작업 폴더 열기’를 사용하세요."); panel.Add(notice);
            ScrollView list = new ScrollView(); list.style.maxHeight = 280; panel.Add(list);
            try
            {
                recentProjects ??= new RecentProjectStore(Path.GetDirectoryName(InternalProjectsRoot));
                string[] names = recentProjects.Read();
                if (recentProjects.ReadWarning != null) notice.text = recentProjects.ReadWarning;
                foreach (string name in names)
                {
                    string path = LevelToolProjectAccess.Resolve(InternalProjectsRoot, name);
                    Button item = Button(list, "recent-project", name, () => { CloseModal(); WithDirty(choice => Workspace.Open(path, choice)); });
                    if (!File.Exists(Path.Combine(path, "project.json"))) { item.SetEnabled(false); item.text += " · 저장본 없음"; }
                }
                if (names.Length == 0) list.Add(new Label("최근에 연 내부 프로젝트가 없습니다."));
            }
            catch (Exception failure) { notice.text = failure.Message; }
            Button(panel, "cancel", "닫기", CloseModal);
        }
    }
}
#endif
