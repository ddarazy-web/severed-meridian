#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using LevelAuthoring.Editing;
using LevelAuthoring.Storage;
using LevelAuthoring.Runtime;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelTool
{
    public sealed partial class LevelToolScreen
    {
        [SerializeField] private string recoveryDirectory;
        private ToolDraftStore draftStore;
        private long checkpointSequence;
        private bool checkpointPending, quitAllowed;
        private float nextCheckpoint;
        private int recoveryReadRevision;
        public event Action ExitRequested;

        private async UniTask EnableRecovery()
        {
            Application.wantsToQuit -= WantsToQuit;
            Application.wantsToQuit += WantsToQuit;
            draftStore ??= new ToolDraftStore(string.IsNullOrEmpty(recoveryDirectory)
                ? Path.Combine(Application.persistentDataPath, "LevelTool") : recoveryDirectory);
            // 같은 컴포넌트를 다시 켤 때 이미 메모리에 있는 편집본을 복구 파일로 덮지 않는다.
            if (Session != null) return;
            int readRevision = ++recoveryReadRevision;
            busy = true; editor.SetEnabled(false);
            try
            {
                JObject record = await UniTask.RunOnThreadPool(draftStore.Read);
                if (this == null || !isActiveAndEnabled || readRevision != recoveryReadRevision || record == null) return;
                AuthoringToolWorkspace previous = new AuthoringToolWorkspace();
                previous.RestoreState((string)record["workspace"]);
                ToolDraftStore.ValidateView(record["view"] as JObject);
                string warning = draftStore.ReadWarning;
                if (!File.Exists(Path.Combine(previous.Folder, "project.json")))
                    warning = (warning == null ? "" : warning + "\n") + "프로젝트 저장본이 없습니다. 복구 초안을 복원하거나 다른 프로젝트를 열어 주세요.";
                bool hasPendingInputs = record["view"]?["pendingInputs"] is JArray pending && pending.Count > 0;
                if (warning == null && !previous.Session.IsDirty && record["view"]?["sharedTutorialDraft"]?.Type != JTokenType.String &&
                    !hasPendingInputs)
                    RestoreRecovery(record, false);
                else
                {
                    recoveryBlocksDefault = true;
                    VisualElement panel = OpenModal(warning == null ? "이전 작업을 어떻게 다시 열까요?" : "복구 기록을 확인해 주세요.");
                    if (warning != null) panel.Add(new Label(warning));
                    panel.Add(new Label("복원하면 선택한 칸과 실행 취소 이력도 돌아옵니다. 원본 JSON은 변경하지 않습니다."));
                    Button(panel, "restore-draft", "이전 작업 복원", () => { CloseModal(); RestoreRecovery(record, false); });
                    Button(panel, "discard-draft", "저장본으로 돌아가기", () => { CloseModal(); RestoreRecovery(record, true); });
                    Button(panel, "open-other-project", "다른 프로젝트 열기", () => { CloseModal(); ChooseFolder(path => WithDirty(choice => Workspace.Open(path, choice))); });
                    // 결정을 미룬 동안 기존 복구본 위로 새 자동 기록을 쓰지 않는다.
                }
            }
            catch (Exception error)
            {
                if (this != null && isActiveAndEnabled && readRevision == recoveryReadRevision)
                {
                    recoveryBlocksDefault = true;
                    VisualElement panel = OpenModal("복구 기록을 읽을 수 없습니다.");
                    panel.Add(new Label(error.Message + " · 원본 저장본과 복구 기록을 자동으로 바꾸지 않았습니다."));
                    Button(panel, "restore-previous-draft", "이전 복구 기록 확인", () => { CloseModal(); RestorePreviousRecovery().Forget(); });
                    Button(panel, "open-other-project", "다른 프로젝트 열기", () => { CloseModal(); ChooseFolder(path => WithDirty(choice => Workspace.Open(path, choice))); });
                    Button(panel, "cancel", "닫기", CloseModal);
                }
            }
            finally
            {
                if (readRevision == recoveryReadRevision) busy = false;
                if (this != null && readRevision == recoveryReadRevision && isActiveAndEnabled && editor != null)
                {
                    editor.SetEnabled(modal == null); TryOpenDefaultWorkspace();
                    if (UsesInternalProjects) TryOpenInternalDefault();
                }
            }
        }

        private async UniTask RestorePreviousRecovery()
        {
            if (busy) return;
            int readRevision = ++recoveryReadRevision;
            busy = true; editor.SetEnabled(false);
            try
            {
                JObject previous = await UniTask.RunOnThreadPool(draftStore.ReadPrevious);
                if (this == null || !isActiveAndEnabled || readRevision != recoveryReadRevision) return;
                if (previous == null) { Show("이전 복구 기록이 없습니다. 다른 프로젝트를 열어 주세요."); return; }
                RestoreRecovery(previous, false);
            }
            catch (Exception error)
            {
                if (this != null && isActiveAndEnabled && readRevision == recoveryReadRevision) Show("이전 복구 기록 확인 실패: " + error.Message);
            }
            finally
            {
                if (readRevision == recoveryReadRevision) busy = false;
                if (this != null && isActiveAndEnabled && readRevision == recoveryReadRevision) editor.SetEnabled(modal == null);
            }
        }

        private void RestoreRecovery(JObject record, bool discard)
        {
            try
            {
                AuthoringToolWorkspace restored = new AuthoringToolWorkspace();
                restored.RestoreState((string)record["workspace"]);
                if (discard) restored.Open(restored.Folder, "Discard");
                JObject view = record["view"] as JObject;
                ToolDraftStore.ValidateView(view);
                SharedTutorialDraft restoredShared = !discard && view["sharedTutorialDraft"]?.Type == JTokenType.String && (string)view["sharedTutorialDraft"] != null
                    ? SharedTutorialDraft.Restore((string)view["sharedTutorialDraft"]) : null;
                // 작업공간과 화면 초안 검증을 모두 마친 뒤 현재 세션의 소유권을 바꾼다.
                Workspace.RestoreState(restored.ExportState());
                recoveryBlocksDefault = false;
                layer = (string)view["layer"] ?? "Block"; brush = (string)view["brush"];
                color = (string)view["color"] ?? "Type1"; direction = (string)view["direction"] ?? "Horizontal";
                anchor = (int?)view["anchor"] ?? -1; seed = (int?)view["seed"] ?? 12345;
                cellSize = (float?)view["cellSize"] ?? 52;
                boardTouchTool = (string)view["boardTouchTool"] ?? "tap";
                boardTouchAdd = (bool?)view["boardTouchAdd"] ?? false;
                boardTouchReplace = (bool?)view["boardTouchReplace"] ?? false;
                mobilePanel = (string)view["mobilePanel"];
                autoFitBoard = (bool?)view["autoFitBoard"] ?? true;
                materialsVisible = (bool?)view["materialsVisible"] ?? false;
                levelSettingsVisible = (bool?)view["levelSettingsVisible"] ?? false;
                boardFocus = (bool?)view["boardFocus"] ?? false;
                inspectorPage = (string)view["inspectorPage"] ?? "기본";
                batchCount = (int?)view["batchCount"] ?? 100;
                toolBotSeed = (int?)view["toolBotSeed"] ?? 12345;
                if (Enum.TryParse((string)view["toolBotStrategy"], out AutoPlay.BotStrategyKind restoredStrategy)) toolBotStrategy = restoredStrategy;
                multiSamples = (int?)view["multiSamples"] ?? 100;
                if (Enum.TryParse((string)view["multiMode"], out AutoPlay.MultiLevelTestMode restoredMode)) multiMode = restoredMode;
                multiSelection.Clear(); multiSelection.UnionWith(view["multiSelection"]?.Values<string>() ?? Enumerable.Empty<string>());
                tutorialStep = (int?)view["tutorialStep"] ?? 0;
                tutorialPreview = (bool?)view["tutorialPreview"] ?? false;
                tutorialPick = discard ? null : view["tutorialPick"] as JObject;
                if (Enum.TryParse((string)view["newTutorialCondition"], out Tutorial.TutorialConditionKind restoredCondition)) newTutorialCondition = restoredCondition;
                sharedTutorialDraft = restoredShared;
                if (sharedTutorialDraft != null) { inspectorPage = "튜토리얼"; brush = null; }
                flowTool = (string)view["flowTool"]; flowFirst = (int?)view["flowFirst"] ?? -1;
                flowRoute.Clear(); flowRoute.AddRange(view["flowRoute"]?.Values<int>() ?? Enumerable.Empty<int>());
                connectionGenerator = (string)view["connectionGenerator"]; connectionTarget = (string)view["connectionTarget"];
                selectedConnection = (int?)view["selectedConnection"] ?? 0;
                wireDrawing = (bool?)view["wireDrawing"] ?? false;
                wireRoute.Clear(); wireRoute.AddRange(view["wireRoute"]?.Values<int>() ?? Enumerable.Empty<int>());
                selectedShape = (string)view["selectedShape"];
                if (discard) CancelPendingInput();
                root.Q<DropdownField>("edit-layer").SetValueWithoutNotify(layer);
                Refresh();
                AuthoringEditSession restoredSession = Session;
                string restoredLevelId = restoredSession.SelectedLevelId;
                if (!discard) root.schedule.Execute(() =>
                {
                    if (isActiveAndEnabled && ReferenceEquals(Session, restoredSession) && Session.SelectedLevelId == restoredLevelId)
                        RestoreRecoveryInputs(view["pendingInputs"] as JArray);
                }).ExecuteLater(50);
                root.schedule.Execute(() =>
                {
                    if (!isActiveAndEnabled || !ReferenceEquals(Session, restoredSession) || Session.SelectedLevelId != restoredLevelId) return;
                    boardScroll.scrollOffset = new Vector2((float?)view["boardX"] ?? 0, (float?)view["boardY"] ?? 0);
                    properties.scrollOffset = new Vector2(0, (float?)view["propertiesY"] ?? 0);
                    levelList.scrollOffset = new Vector2(0, (float?)view["levelsY"] ?? 0);
                    palette.scrollOffset = new Vector2(0, (float?)view["paletteY"] ?? 0);
                }).ExecuteLater(50);
                Show(discard ? "미저장 변경을 버렸습니다." : "이전 작업과 실행 취소 이력을 복원했습니다.");
                RememberInternalProject();
            }
            catch (Exception error) { recoveryBlocksDefault = true; Show("복원 실패: " + error.Message + " · 기존 작업과 복구 기록을 유지했습니다."); }
        }

        private JObject CaptureView() => new JObject
        {
            ["pendingInputs"] = CaptureRecoveryInputs(),
            ["boardTouchTool"] = boardTouchTool, ["boardTouchAdd"] = boardTouchAdd, ["boardTouchReplace"] = boardTouchReplace,
            ["mobilePanel"] = mobilePanel,
            ["layer"] = layer, ["brush"] = brush, ["color"] = color, ["direction"] = direction,
            ["anchor"] = anchor, ["seed"] = seed, ["cellSize"] = cellSize,
            ["autoFitBoard"] = autoFitBoard, ["materialsVisible"] = materialsVisible,
            ["levelSettingsVisible"] = levelSettingsVisible, ["boardFocus"] = boardFocus,
            ["inspectorPage"] = inspectorPage,
            ["batchCount"] = batchCount, ["toolBotSeed"] = toolBotSeed, ["toolBotStrategy"] = toolBotStrategy.ToString(),
            ["multiSamples"] = multiSamples, ["multiMode"] = multiMode.ToString(), ["multiSelection"] = new JArray(multiSelection.OrderBy(id => id, StringComparer.Ordinal)),
            ["tutorialStep"] = tutorialStep, ["newTutorialCondition"] = newTutorialCondition.ToString(),
            ["tutorialPreview"] = tutorialPreview,
            ["tutorialPick"] = tutorialPick?.DeepClone(),
            ["sharedTutorialDraft"] = sharedTutorialDraft?.ExportState(),
            ["flowTool"] = flowTool, ["flowFirst"] = flowFirst, ["flowRoute"] = new JArray(flowRoute),
            ["connectionGenerator"] = connectionGenerator, ["connectionTarget"] = connectionTarget,
            ["selectedConnection"] = selectedConnection, ["wireDrawing"] = wireDrawing, ["wireRoute"] = new JArray(wireRoute),
            ["selectedShape"] = selectedShape,
            ["boardX"] = boardScroll.scrollOffset.x, ["boardY"] = boardScroll.scrollOffset.y,
            ["propertiesY"] = properties.scrollOffset.y, ["levelsY"] = levelList.scrollOffset.y,
            ["paletteY"] = palette.scrollOffset.y
        };

        private void Update()
        {
            UpdateMobileViewport();
            UpdateBoardTouch(Time.unscaledTime);
            windowController?.Tick();
            AdvanceToolRecords();
            AdvanceToolBot();
            AdvanceToolBatch();
            AdvanceToolMulti();
            AdvanceToolHistory();
            if (busy || modal != null || !checkpointPending || Time.unscaledTime < nextCheckpoint || Session == null) return;
            checkpointPending = false; nextCheckpoint = Time.unscaledTime + 1;
            string state = Workspace.ExportState(); JObject view = CaptureView(); long sequence = ++checkpointSequence;
            SaveCheckpoint(state, view, sequence).Forget();
        }
        private async UniTask SaveCheckpoint(string state, JObject view, long sequence)
        {
            try { await UniTask.RunOnThreadPool(() => draftStore.Save(state, view, sequence)); }
            catch (Exception error) { if (this != null) Show("자동 복구 기록 실패: " + error.Message + " · 원본 저장을 해 주세요."); }
        }

        private void FlushRecovery()
        {
            if (Session == null || draftStore == null || modal?.Q<Button>("restore-draft") != null) return;
            // 종료 콜백은 await할 수 없어 마지막 기록만 동기적으로 보장한다.
            draftStore.Save(Workspace.ExportState(), CaptureView(), ++checkpointSequence);
        }
        private void OnDisable()
        {
            bool storageWasBusy = busy;
            recoveryReadRevision++;
            DisposeFileTransfer();
            DisposeBoardTouch();
            DisposeMobileLayout();
            DisposeMobileViewport();
            windowController?.Dispose(); windowController = null;
            toolSkin?.Dispose(); toolSkin = null;
            UnregisterShortcuts();
            inputCommitRevision++; committingCommandInput = false; commandInputRefreshPending = false; commandAwaitingInput = false;
            EndToolRecords();
            botBoard?.Dispose(); botBoard = null; batchBoard?.Dispose(); batchBoard = null;
            DisposeToolBot();
            DisposeToolHistory();
            // 스크립트 재로드와 강제 씬 해제에서는 다음 프레임의 비동기 재개를 보장하지 않는다.
            DrainTrialRecords();
            CloseToolBatch().Forget();
            CloseToolMulti().Forget();
            Application.wantsToQuit -= WantsToQuit;
            try { if (!storageWasBusy) FlushRecovery(); }
            catch (Exception error) { Debug.LogError("레벨툴 복구 기록 실패: " + error.Message); }
        }

        public void RequestLeave(Action continuation)
        {
            if (RecordsBusy) { Show("기록 정리를 끝내거나 취소한 뒤 종료해 주세요."); return; }
            if (busy) { Show("파일 처리 완료 후 종료해 주세요."); return; }
            if (!CommitCommandInput()) return;
            if (committingCommandInput)
            {
                if (commandAwaitingInput) return;
                commandAwaitingInput = true;
                LeaveAfterInputCommit(continuation).Forget();
                return;
            }
            if (sharedTutorialDraft != null) { AskSharedDraftBeforeLeave(continuation); return; }
            if (Session?.IsDirty != true) { Leave("Cancel", continuation).Forget(); return; }
            VisualElement panel = OpenModal("저장하지 않은 변경이 있습니다.");
            panel.Add(new Label("저장 후 종료하거나 변경을 버릴 수 있습니다. 취소하면 계속 편집합니다."));
            Button(panel, "save-and-leave", "저장 후 종료", () => { CloseModal(); Leave("Save", continuation).Forget(); });
            Button(panel, "discard-and-leave", "버리고 종료", () => { CloseModal(); Leave("Discard", continuation).Forget(); });
            Button(panel, "cancel", "취소", CloseModal);
        }
        private async UniTask LeaveAfterInputCommit(Action continuation)
        {
            int revision = inputCommitRevision;
            await UniTask.WaitUntil(() => !committingCommandInput || this == null || !isActiveAndEnabled);
            if (this == null || !isActiveAndEnabled || revision != inputCommitRevision) return;
            commandAwaitingInput = false;
            RequestLeave(continuation);
        }
        private async UniTask Leave(string choice, Action continuation)
        {
            busy = true; editor.SetEnabled(false);
            try
            {
                if (choice == "Save") await UniTask.RunOnThreadPool(Workspace.Save);
                else if (choice == "Discard") Session.Discard();
                await CloseToolBatch();
                await CloseToolMulti();
                FlushRecovery(); continuation();
            }
            catch (Exception error) { Show("종료 취소: " + error.Message); }
            finally { busy = false; if (this != null && editor != null) { editor.SetEnabled(true); Refresh(); } }
        }
        private bool WantsToQuit()
        {
            if (quitAllowed) return true;
            RequestLeave(() => { quitAllowed = true; Application.Quit(); }); return false;
        }
        private void ExitTool() => RequestLeave(() =>
        {
            if (Application.isEditor) ExitRequested?.Invoke();
            else { quitAllowed = true; Application.Quit(); }
        });
    }
}
#endif
