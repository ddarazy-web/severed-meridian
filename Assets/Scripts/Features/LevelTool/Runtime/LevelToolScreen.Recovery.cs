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
        private bool quitAllowed;
        private int recoveryReadRevision;
        public event Action ExitRequested;

        private void EnableRecovery()
        {
            Application.wantsToQuit -= WantsToQuit;
            Application.wantsToQuit += WantsToQuit;
            draftStore ??= new ToolDraftStore(string.IsNullOrEmpty(recoveryDirectory)
                ? Path.Combine(Application.persistentDataPath, "LevelTool") : recoveryDirectory);
            // 과거 복구본은 시작할 때 읽지 않는다. 명시적으로 요청한 경우에만 확인한다.
            TryOpenDefaultWorkspace();
            if (UsesInternalProjects) TryOpenInternalDefault();
        }

        private async UniTask ReadLegacyRecovery()
        {
            int readRevision = ++recoveryReadRevision;
            busy = true; editor.SetEnabled(false);
            try
            {
                ToolDraftStore.PreparedDraft record = await UniTask.RunOnThreadPool(draftStore.ReadPrepared);
                if (this == null || !isActiveAndEnabled || readRevision != recoveryReadRevision) return;
                if (record == null) { Show("과거 복구 기록이 없습니다."); return; }
                string warning = draftStore.ReadWarning;
                if (!File.Exists(Path.Combine(record.Folder, "project.json")))
                    warning = (warning == null ? "" : warning + "\n") + "프로젝트 저장본이 없습니다. 복구 초안을 복원하거나 다른 프로젝트를 열어 주세요.";
                VisualElement panel = OpenModal(warning == null ? "과거 복구 기록 확인" : "복구 기록을 확인해 주세요.");
                if (warning != null) panel.Add(new Label(warning));
                panel.Add(new Label("복원하면 이전 선택과 실행 취소 이력이 돌아옵니다. 복구 파일과 원본 JSON은 변경하지 않습니다."));
                Button(panel, "restore-draft", "이전 작업 복원", () =>
                { CloseModal(); RequestLeaveFor(() => RestoreRecovery(record, false), "복원"); });
                Button(panel, "discard-draft", "당시 저장본 열기", () =>
                { CloseModal(); RequestLeaveFor(() => RestoreRecovery(record, true), "복원"); });
                Button(panel, "cancel", "취소", CloseModal);
            }
            catch (Exception error)
            {
                if (this != null && isActiveAndEnabled && readRevision == recoveryReadRevision)
                {
                    VisualElement panel = OpenModal("복구 기록을 읽을 수 없습니다.");
                    panel.Add(new Label(error.Message + " · 원본 저장본과 복구 기록을 자동으로 바꾸지 않았습니다."));
                    Button(panel, "restore-previous-draft", "이전 복구 기록 확인", () => { CloseModal(); RestorePreviousRecovery().Forget(); });
                    Button(panel, "open-other-project", "다른 프로젝트 열기", () => { CloseModal(); ChooseFolder(path => WithDirty(choice => Workspace.OpenListing(path, choice))); });
                    Button(panel, "cancel", "닫기", CloseModal);
                }
            }
            finally
            {
                if (readRevision == recoveryReadRevision) busy = false;
                if (this != null && readRevision == recoveryReadRevision && isActiveAndEnabled && editor != null)
                {
                    editor.SetEnabled(modal == null);
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
                ToolDraftStore.PreparedDraft previous = await UniTask.RunOnThreadPool(draftStore.ReadPreviousPrepared);
                if (this == null || !isActiveAndEnabled || readRevision != recoveryReadRevision) return;
                if (previous == null) { Show("이전 복구 기록이 없습니다. 다른 프로젝트를 열어 주세요."); return; }
                busy = false;
                RequestLeaveFor(() => RestoreRecovery(previous, false), "복원");
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

        private void RestoreRecovery(ToolDraftStore.PreparedDraft record, bool discard)
        {
            try
            {
                JObject view = record.View;
                SharedTutorialDraft restoredShared = !discard && view["sharedTutorialDraft"]?.Type == JTokenType.String && (string)view["sharedTutorialDraft"] != null
                    ? SharedTutorialDraft.Restore((string)view["sharedTutorialDraft"]) : null;
                // 작업공간과 화면 초안 검증을 모두 마친 뒤 현재 세션의 소유권을 바꾼다.
                record.ApplyTo(Workspace, discard);
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
        }
        private void OnDisable()
        {
            suspendedInputs = editor != null && Session != null ? CaptureRecoveryInputs() : null;
            displaySession = null; displayDirectory = null; displayedLevel = null;
            displayDocuments.Clear(); displayVersions.Clear(); definitions.Clear();
            packComparisonKey = validationComparisonKey = null;
            root?.UnregisterCallback<InputEvent>(OnManualInput, TrickleDown.TrickleDown);
            recoveryReadRevision++;
            DisposeFileTransfer();
            DisposeBoardTouch();
            boardView?.Dispose(); boardView = null;
            supplyPanel?.Dispose(); supplyPanel = null;
            propertiesPanel?.Dispose(); propertiesPanel = null;
            tutorialPanel?.Dispose(); tutorialPanel = null;
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
        }

        public void RequestLeave(Action continuation) => RequestLeaveFor(continuation, "종료");

        private void RequestLeaveFor(Action continuation, string operation)
        {
            if (GuardTutorialWorkExit(() => RequestLeaveFor(continuation, operation))) return;
            if (RecordsBusy) { Show("기록 정리를 끝내거나 취소한 뒤 " + operation + "해 주세요."); return; }
            if (busy) { Show("파일 처리 완료 후 " + operation + "해 주세요."); return; }
            if (sharedTutorialDraft != null) { AskSharedDraftBeforeLeave(continuation, operation); return; }
            if (!HasUnsavedWork) { Leave("Cancel", continuation, operation).Forget(); return; }
            VisualElement panel = OpenModal(operation + " 전 현재 작업 확인");
            panel.Add(new Label("저장하지 않은 변경이 있습니다. 저장한 뒤 " + operation + "하거나 변경을 버릴 수 있습니다. 취소하면 입력 중인 내용까지 유지합니다.")
                { style = { whiteSpace = WhiteSpace.Normal } });
            Button(panel, "save-and-leave", "저장 후 " + operation, () => { CloseModal(); ContinueAfterManualInput(() => Leave("Save", continuation, operation).Forget()).Forget(); });
            Button(panel, "discard-and-leave", "버리고 " + operation, () => { CloseModal(); DiscardUncommittedInputs(); Leave("Discard", continuation, operation).Forget(); });
            Button(panel, "cancel", "취소", CloseModal);
        }
        private async UniTask Leave(string choice, Action continuation, string operation)
        {
            bool completed = false;
            busy = true; editor.SetEnabled(false);
            try
            {
                if (choice == "Save")
                {
                    BeginManualSave();
                    await UniTask.RunOnThreadPool(Workspace.Save);
                    CompleteManualSave();
                }
                else if (choice == "Discard") { Session.Discard(); manualSaveFailure = null; }
                await CloseToolBatch();
                await CloseToolMulti();
                continuation();
                completed = true;
            }
            catch (Exception error)
            {
                if (manualSaveInProgress) CompleteManualSave(error);
                Show(operation + " 취소: " + error.Message);
            }
            finally
            {
                busy = false;
                if (this != null && isActiveAndEnabled && editor != null)
                {
                    editor.SetEnabled(true);
                    // 복원은 현재 화면을 바꾸므로 갱신한다. 실제 종료 성공 뒤에는 화면을 다시 구성하지 않는다.
                    if (!completed || operation != "종료") Refresh();
                    else { RefreshCommandStates(); UpdateManualSaveStatus(); }
                }
            }
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
