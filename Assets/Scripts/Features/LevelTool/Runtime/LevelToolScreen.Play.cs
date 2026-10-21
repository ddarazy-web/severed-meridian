#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameScreen;
using LevelAuthoring.Documents;
using LevelAuthoring.Runtime;
using Tutorial;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelTool
{
    public sealed partial class LevelToolScreen
    {
        [SerializeField] private GameObject boardPrefab;
        [SerializeField] private GameObject sessionPrefab;
        [SerializeField] private GameObject screenPrefab;
        private GameObject playRoot;
        private CancellationTokenSource playLifetime;
        private Button returnButton;
        private PuzzleGameSession playSession;
        private PuzzlePopupBinding playPopups;
        private int playBackFrame = -1;
        private TutorialRunMode tutorialMode = TutorialRunMode.Automatic;
        private int playRevision;
        private FocusedInputState playReturnFocus;
        public PuzzleGameSession TestSession => playSession;

        public void Configure(Font uiFont, StyleSheet sheet, GameObject world, GameObject session, GameObject screen)
        { font = uiFont; styles = sheet; boardPrefab = world; sessionPrefab = session; screenPrefab = screen; }

        private ContentSnapshot CreateTrialSnapshot(IEnumerable<string> ids = null)
        {
            ids ??= new[] { Session.SelectedLevelId };
            return sharedTutorialDraft?.PreviewLevels(Session, ids) ?? Session.CreateTrialSnapshot(ids);
        }

        public async UniTask BeginPlay()
        {
            if (busy || RecordsBusy || Session == null || playRoot != null) return;
            if (BatchActive || MultiActive) { Show("진행 중인 시험을 중지하고 저장이 끝난 뒤 게임 시험을 시작하세요."); return; }
            DisposeToolBot();
            playReturnFocus = commandInputFocus ?? CaptureFocusedInput(); commandInputFocus = null;
            int revision = ++playRevision;
            busy = true; editor.SetEnabled(false);
            try
            {
                var snapshot = CreateTrialSnapshot();
                if (boardPrefab == null || sessionPrefab == null || screenPrefab == null)
                    throw new InvalidOperationException("게임 시험 프리팹 연결이 없습니다.");
                playLifetime = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
                playRoot = new GameObject("LevelTool Test Game");
                PuzzlePlayRequest request;
                if (packedTrial)
                {
                    int number = (int)snapshot.Get(Session.SelectedLevelId).Data["levelNumber"];
                    var trial = await PackedPuzzlePlayAdapter.CreateRequestAsync(number, seed, playLifetime.Token);
                    if (revision != playRevision) return;
                    checkedPackHash = trial.SourceHash; UpdatePackStatus(); request = trial.Request;
                }
                else request = JsonPuzzlePlayAdapter.CreateRequest(snapshot, Session.SelectedLevelId, seed);
                playLifetime.Token.ThrowIfCancellationRequested();
                Camera camera = new GameObject("Test Camera").AddComponent<Camera>();
                camera.transform.SetParent(playRoot.transform, false); camera.transform.position = new Vector3(0, 0, -10);
                camera.orthographic = true; camera.orthographicSize = 6; camera.cullingMask &= ~(1 << 5);
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.06f, 0.09f, 0.14f);
                PuzzleWorldBoard world = Instantiate(boardPrefab, playRoot.transform).GetComponent<PuzzleWorldBoard>();
                GameObject game = Instantiate(sessionPrefab, playRoot.transform);
                playSession = game.GetComponent<PuzzleGameSession>();
                PuzzleBoardInput input = game.GetComponent<PuzzleBoardInput>();
                playSession.Configure(world, camera); input.Configure(playSession, world, camera);
                PuzzleScreenView screen = Instantiate(screenPrefab, playRoot.transform).GetComponent<PuzzleScreenView>();
                screen.Configure(playSession, input);
                playPopups = screen.GetComponent<PuzzlePopupBinding>();
                if (backgroundCamera != null) backgroundCamera.enabled = false;
                editor.style.display = DisplayStyle.None;
                root.style.backgroundColor = Color.clear; root.pickingMode = PickingMode.Ignore;
                returnButton = Button(root, "return-to-editor", "← 편집으로 돌아가기", ReturnToEditor);
                returnButton.style.position = Position.Absolute; returnButton.style.right = 80; returnButton.style.top = 12;
                ApplyTrialReturnLayout();
                if (mobileViewportActive) ApplyToolSkin();
                // Start가 정식 입력을 선택하기 전에 명시적 시험 문맥을 전달한다.
                await playSession.InitializeAsync(request, PuzzlePlayContext.CreateTest(tutorialMode), playLifetime.Token);
                if (playSession != null && playSession.HasFailed) throw new InvalidOperationException(playSession.Message);
            }
            catch (OperationCanceledException) { if (revision == playRevision) ReturnToEditor(); }
            catch (Exception error) { if (revision == playRevision) { ReturnToEditor(); Show("시험 시작 실패: " + error.Message); } }
            finally { if (revision == playRevision) { busy = false; if (editor != null) editor.SetEnabled(true); } }
        }

        public void ReturnToEditor()
        {
            if (backgroundCamera != null) backgroundCamera.enabled = true;
            playRevision++; busy = false;
            playLifetime?.Cancel(); playLifetime?.Dispose(); playLifetime = null;
            if (playRoot != null) { playRoot.SetActive(false); Destroy(playRoot); playRoot = null; }
            playSession = null; playPopups = null; playBackFrame = -1;
            returnButton?.RemoveFromHierarchy(); returnButton = null;
            if (editor != null)
            {
                editor.SetEnabled(true);
                editor.style.display = DisplayStyle.Flex;
                root.style.backgroundColor = new Color(0.08f, 0.1f, 0.14f);
                root.pickingMode = PickingMode.Position;
                RestoreFocusedInput(playReturnFocus); playReturnFocus = null;
                RefreshCommandStates();
                Show("시험에서 돌아왔습니다. 미저장 내용과 실행 취소 이력을 유지했습니다.");
            }
        }

        private void HandleTrialBack()
        {
            if (playBackFrame == Time.frameCount) return;
            playBackFrame = Time.frameCount;
            if (playPopups?.Host?.TryCancelInput() == true) return;
            if (playSession != null && playSession.IsReady && !playSession.IsRestarting && !playSession.IsChangingLevel &&
                (playSession.Outcome == null || playSession.IsPresenting || playSession.HasProgressFeedback))
                playPopups?.OpenPause();
        }

        private void ApplyTrialReturnLayout()
        {
            if (returnButton == null || !mobileViewportActive) return;
            returnButton.text = "← 편집 복귀";
            returnButton.style.right = StyleKeyword.Auto;
            returnButton.style.left = mobileVisibleArea.xMin + 8;
            returnButton.style.top = mobileVisibleArea.yMin + 8;
            returnButton.style.minWidth = 48; returnButton.style.minHeight = 48;
        }

        private void OnDestroy()
        {
            boardView?.Dispose(); boardView = null;
            supplyPanel?.Dispose(); supplyPanel = null;
            propertiesPanel?.Dispose(); propertiesPanel = null;
            tutorialPanel?.Dispose(); tutorialPanel = null;
            ReturnToEditor();
        }
    }
}
#endif
