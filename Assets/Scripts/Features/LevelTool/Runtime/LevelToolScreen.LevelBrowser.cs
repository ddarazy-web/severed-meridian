#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using LevelAuthoring.Editing;
using LevelAuthoring.Storage;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelTool
{
    public sealed partial class LevelToolScreen
    {
        private LevelBrowserIndex levelIndex = new LevelBrowserIndex();
        private ListView levelBrowser;
        private TextField levelSearch;
        private Label levelCount;
        private DropdownField recentLevelChoice;
        private List<LevelBrowserIndex.Entry> levelRows = new List<LevelBrowserIndex.Entry>();
        private string browserFolder, browserProjectId, browsedLevelId, lastOpenedLevelId;
        private long browserRevision = -1;
        private RecentLevelStore recentLevels;
        private string[] recentLevelIds = Array.Empty<string>();
        private readonly List<string> recentVisibleIds = new List<string>();
        private IVisualElementScheduledItem pendingLevelSearch;
        private const string RecentLevelPlaceholder = "최근 편집 · 최대 20개";

        private void RequestListingRebuild()
        {
            if (busy || Workspace.Directory == null || Workspace.IsRecovery || sharedTutorialDraft != null) return;
            VisualElement panel = OpenModal("목록 정보 갱신");
            panel.Add(new Label("저장된 파일을 한 번씩 읽어 번호·이름 목록을 만듭니다. 파일이 많으면 시간이 걸릴 수 있으며 진행 중 취소할 수 있습니다. 현재 편집 내용은 저장하거나 버리지 않습니다.")
                { style = { whiteSpace = WhiteSpace.Normal } });
            Button(panel, "confirm-listing-rebuild", "목록 갱신 시작", () => { CloseModal(); RunListingRebuild().Forget(); });
            Button(panel, "cancel", "취소", CloseModal);
        }

        private async UniTask RunListingRebuild()
        {
            if (busy || Workspace.Directory == null || Workspace.IsRecovery || sharedTutorialDraft != null) return;
            var pendingInputs = CaptureRecoveryInputs();
            FocusedInputState focus = commandInputFocus ?? CaptureFocusedInput(); commandInputFocus = null;
            var cancellation = new CancellationTokenSource(); fileTransferCancellation = cancellation;
            VisualElement panel = OpenModal("목록 정보 만드는 중");
            Label progress = new Label("준비 중…") { name = "listing-progress" }; panel.Add(progress);
            Button(panel, "cancel-listing-rebuild", "취소", () => { cancellation.Cancel(); progress.text = "취소하는 중…"; });
            int completed = 0, total = 0;
            IVisualElementScheduledItem tick = progress.schedule.Execute(() =>
            {
                if (!cancellation.IsCancellationRequested)
                    progress.text = "파일 " + Volatile.Read(ref completed) + " / " + Volatile.Read(ref total) + " · 목록 정보만 수집 중";
            }).Every(100);
            busy = true; editor.SetEnabled(false); RefreshCommandStates();
            string message;
            try
            {
                await UniTask.RunOnThreadPool(() => Workspace.RebuildListing(cancellation.Token, (done, count) =>
                { Volatile.Write(ref total, count); Volatile.Write(ref completed, done); }));
                displayMetadataVersion = -1; browserRevision = -1;
                if (Session == null) displayDirectory = null;
                message = "목록 정보를 갱신했습니다. 현재 편집 내용은 유지했습니다.";
            }
            catch (OperationCanceledException) { message = "목록 갱신을 취소했습니다. 현재 편집 내용은 유지했습니다."; }
            catch (Exception error) { message = "목록 갱신 실패 · 현재 편집 유지: " + error.Message; }
            finally
            {
                tick.Pause();
                if (ReferenceEquals(fileTransferCancellation, cancellation)) fileTransferCancellation = null;
                cancellation.Dispose();
                if (this != null) busy = false;
            }
            if (this == null || !isActiveAndEnabled) return;
            CloseModal(); Refresh(); RestoreMobilePanelInputs();
            RestoreRecoveryInputs(pendingInputs); RestoreFocusedInput(focus); Show(message);
        }

        private void BuildLevelBrowser()
        {
            levelSearch = new TextField("검색") { name = "level-search", tooltip = "번호: 125 / 범위: 1200~1299 / 이름 일부. Enter는 검색만 실행합니다." };
            levelSearch.style.flexShrink = 0;
            levelPanel.Add(levelSearch);
            levelSearch.RegisterValueChangedCallback(_ =>
            {
                pendingLevelSearch?.Pause();
                pendingLevelSearch = levelSearch.schedule.Execute(ApplyLevelSearch);
                pendingLevelSearch.ExecuteLater(180);
            });
            levelSearch.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter) return;
                pendingLevelSearch?.Pause(); ApplyLevelSearch(); evt.StopPropagation(); evt.PreventDefault();
            });
            recentLevelChoice = new DropdownField(new List<string> { RecentLevelPlaceholder }, 0) { name = "recent-levels", tooltip = "최근 연 레벨을 선택하면 편집합니다. 미저장 변경이 있으면 먼저 확인합니다." };
            recentLevelChoice.RegisterCallback<PointerDownEvent>(PrepareCommandPointer, TrickleDown.TrickleDown);
            recentLevelChoice.RegisterValueChangedCallback(evt =>
            {
                int index = recentLevelChoice.choices.IndexOf(evt.newValue) - 1;
                recentLevelChoice.SetValueWithoutNotify(RecentLevelPlaceholder);
                if (index >= 0 && index < recentVisibleIds.Count) RequestOpenLevel(recentVisibleIds[index]);
            });
            var shortcuts = new VisualElement { name = "level-shortcuts" };
            shortcuts.style.flexShrink = 0;
            levelPanel.Add(shortcuts);
            shortcuts.Add(recentLevelChoice);
            Button(shortcuts, "find-current-level", "현재 레벨 찾기", () =>
            {
                pendingLevelSearch?.Pause(); levelSearch.SetValueWithoutNotify(""); ApplyLevelSearch();
                int index = levelRows.FindIndex(row => row.Id == Session?.SelectedLevelId);
                if (index < 0) return;
                levelBrowser.SetSelection(index); levelBrowser.ScrollToItem(index);
            }).tooltip = "검색을 해제하고 현재 편집 중인 레벨 위치를 표시합니다.";
            levelCount = new Label { name = "level-result-count" };
            levelCount.style.whiteSpace = WhiteSpace.Normal;
            levelCount.style.flexShrink = 0;
            levelPanel.Add(levelCount);
            Button(levelPanel, "rebuild-level-list", "목록 정보 갱신", RequestListingRebuild).tooltip =
                "요청할 때만 저장된 파일을 한 번씩 읽어 번호·이름 목록을 만듭니다. 현재 편집과 실행 취소 기록은 유지합니다.";
            levelBrowser = new ListView { name = "levels", fixedItemHeight = 48,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight, selectionType = SelectionType.Single };
            levelBrowser.style.flexGrow = 1; levelBrowser.style.minHeight = 0;
            levelBrowser.makeItem = () =>
            {
                var row = new VisualElement(); row.AddToClassList("level-browser-row");
                var label = new Label(); label.AddToClassList("level-browser-label"); row.Add(label);
                var edit = new Button(); edit.text = "편집"; edit.AddToClassList("level-edit-button"); row.Add(edit);
                edit.RegisterCallback<PointerDownEvent>(PrepareCommandPointer, TrickleDown.TrickleDown);
                edit.clicked += () => RequestOpenLevel((string)edit.userData);
                return row;
            };
            levelBrowser.bindItem = (row, index) =>
            {
                LevelBrowserIndex.Entry item = levelRows[index];
                row.userData = item.Id;
                Label label = row.Q<Label>();
                label.text = (item.Number > 0 ? item.Number.ToString() : "미확인") + " · " + item.Name;
                label.tooltip = label.text + "\n행 선택은 탐색만 합니다. 편집 버튼으로 여세요.";
                row.EnableInClassList("editing-level", item.Id == Session?.SelectedLevelId);
                Button button = row.Q<Button>(); button.userData = item.Id;
                button.tooltip = item.Number > 0 ? "레벨 " + item.Number + " 편집" : "이 파일만 읽어 레벨인지 확인하고 편집합니다.";
            };
            levelBrowser.selectionChanged += rows => browsedLevelId = rows.Cast<LevelBrowserIndex.Entry>().FirstOrDefault()?.Id;
            levelPanel.Add(levelBrowser);
            levelList = levelBrowser.Q<ScrollView>();
            levelList.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        }

        private void ApplyLevelSearch()
        {
            if (levelBrowser == null) return;
            levelRows = levelIndex.Search(levelSearch.value).ToList();
            levelBrowser.itemsSource = levelRows;
            levelBrowser.SetSelectionWithoutNotify(Array.Empty<int>());
            int index = levelRows.FindIndex(row => row.Id == browsedLevelId);
            if (index >= 0) levelBrowser.SetSelectionWithoutNotify(new[] { index });
            levelCount.text = levelIndex.SearchError ?? ("전체 " + levelIndex.Count + " · 검색 " + levelRows.Count +
                (levelRows.Count == 0 ? " · 결과 없음" : ""));
            browserRevision = levelIndex.Revision;
            if (Workspace.Directory?.MetadataWarning != null) levelCount.text += "\n" + Workspace.Directory.MetadataWarning;
        }

        private void RefreshLevelBrowser()
        {
            bool projectChanged = browserFolder != Workspace.Folder || browserProjectId != displayDocuments.Values.FirstOrDefault(doc => doc.Kind == "project")?.Id;
            if (projectChanged)
            {
                browserFolder = Workspace.Folder;
                browserProjectId = displayDocuments.Values.FirstOrDefault(doc => doc.Kind == "project")?.Id;
                lastOpenedLevelId = null;
                pendingLevelSearch?.Pause(); levelSearch.SetValueWithoutNotify(""); browsedLevelId = null;
                recentLevels ??= new RecentLevelStore(Path.Combine(string.IsNullOrEmpty(recoveryDirectory) ?
                    Path.Combine(Application.persistentDataPath, "LevelTool") : recoveryDirectory, "RecentLevels"));
                recentLevelIds = Array.Empty<string>();
                if (browserFolder != null && browserProjectId != null)
                {
                    try
                    {
                        recentLevelIds = recentLevels.Read(browserFolder, browserProjectId);
                        if (recentLevels.ReadWarning != null) Show(recentLevels.ReadWarning);
                    }
                    catch (Exception error) { Show("최근 편집 기록 읽기 실패: " + error.Message); }
                }
                ApplyLevelSearch();
            }
            if (browserRevision != levelIndex.Revision) ApplyLevelSearch();
            if (Session?.SelectedLevelId != null && lastOpenedLevelId != Session.SelectedLevelId) RecordOpenedLevel(Session.SelectedLevelId);
            foreach (VisualElement row in levelBrowser.Query<VisualElement>(className: "level-browser-row").ToList())
                row.EnableInClassList("editing-level", (string)row.userData == Session?.SelectedLevelId);
            RefreshRecentLevelChoices();
            bool enabled = (Session != null || Workspace.Directory != null) && !busy && sharedTutorialDraft == null && tutorialPick == null;
            recentLevelChoice.SetEnabled(enabled);
            levelBrowser.SetEnabled(enabled);
            levelSearch.SetEnabled(enabled);
            root.Q("find-current-level")?.SetEnabled(Session != null && !busy);
            root.Q("rebuild-level-list")?.SetEnabled(enabled && Workspace.Directory != null && !Workspace.IsRecovery);
        }

        private void RefreshRecentLevelChoices()
        {
            recentVisibleIds.Clear();
            var labels = new List<string> { RecentLevelPlaceholder };
            foreach (string id in recentLevelIds)
            {
                LevelBrowserIndex.Entry item = levelIndex.Get(id);
                if (item == null) continue;
                recentVisibleIds.Add(id);
                labels.Add(item.Number + " · " + item.Name + "  [" + recentVisibleIds.Count + "]");
            }
            recentLevelChoice.choices = labels;
            recentLevelChoice.SetValueWithoutNotify(RecentLevelPlaceholder);
        }

        private void RecordOpenedLevel(string id)
        {
            lastOpenedLevelId = id;
            if (browserFolder == null || browserProjectId == null || recentLevels == null) return;
            recentLevelIds = new[] { id }.Concat(recentLevelIds.Where(previous => previous != id)).Take(20).ToArray();
            try { recentLevels.RecordOpened(browserFolder, browserProjectId, id); }
            catch (Exception error) { Show("레벨은 열었지만 최근 편집 기록을 저장하지 못했습니다: " + error.Message); }
        }

        private void RequestOpenLevel(string id, Action afterOpen = null)
        {
            if (Session == null && Workspace.Directory == null || busy || modal != null || sharedTutorialDraft != null || tutorialPick != null ||
                levelIndex.Get(id) == null) return;
            if (Session?.SelectedLevelId == null) { OpenSelectedLevel(id, afterOpen); return; }
            if (id == Session.SelectedLevelId) { afterOpen?.Invoke(); return; }
            if (GuardTutorialWorkExit(() => RequestOpenLevel(id, afterOpen))) return;
            if (!Session.HasLevelChanges(Session.SelectedLevelId) && !HasPendingAuthoringInput()) { OpenSelectedLevel(id, afterOpen); return; }
            VisualElement panel = OpenModal("레벨 " + levelIndex.Get(id).Number + "를 편집할까요?");
            panel.Add(new Label("현재 편집 중인 레벨 " + levelIndex.Get(Session.SelectedLevelId)?.Number + "에 저장하지 않은 변경 사항이 있습니다.")
                { style = { whiteSpace = WhiteSpace.Normal } });
            panel.Add(new Label("저장 시 다른 레벨과 공유 데이터의 미저장 변경도 함께 저장됩니다. 변경 버리기는 현재 레벨에만 적용됩니다.")
                { style = { whiteSpace = WhiteSpace.Normal } });
            Button(panel, "save-and-open-level", "저장하고 열기", () =>
            { CloseModal(); ContinueAfterManualInput(() => SaveAndOpenLevel(id, afterOpen).Forget()).Forget(); });
            Button(panel, "discard-and-open-level", "현재 레벨 변경 버리고 열기", () =>
            {
                try
                {
                    Workspace.PrepareLevel(id);
                    Session.DiscardLevelChanges(Session.SelectedLevelId);
                    CloseModal(); DiscardUncommittedInputs(); OpenSelectedLevel(id, afterOpen);
                }
                catch (Exception error) { Show("변경을 버리지 못했습니다: " + error.Message); }
            });
            Button(panel, "cancel-open-level", "계속 편집", CloseModal);
        }

        private void OpenSelectedLevel(string id, Action afterOpen = null)
        {
            try
            {
                Workspace.OpenLevel(id); CancelPendingInput();
                RecordOpenedLevel(id); Refresh();
                afterOpen?.Invoke();
            }
            catch (Exception error) { Show("레벨을 열지 못했습니다: " + error.Message); }
        }

        private async UniTask SaveAndOpenLevel(string id, Action afterOpen)
        {
            busy = true; editor.SetEnabled(false);
            try
            {
                BeginManualSave();
                await UniTask.RunOnThreadPool(Workspace.Save);
                CompleteManualSave();
                OpenSelectedLevel(id, afterOpen);
            }
            catch (Exception error) { CompleteManualSave(error); Show("레벨을 열지 못했습니다: " + error.Message); }
            finally { busy = false; if (this != null && isActiveAndEnabled) { editor.SetEnabled(true); Refresh(); } }
        }
    }
}
#endif
