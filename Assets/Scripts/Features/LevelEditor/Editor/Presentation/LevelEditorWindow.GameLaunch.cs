using System;
using System.Collections.Generic;
using GameScreen.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    public sealed partial class LevelEditorWindow
    {
        [SerializeField] private PuzzleEditorLevelSource gameLevelSource;
        [SerializeField] private int gameLevelSeed = 12345;
        [SerializeField] private Tutorial.TutorialRunMode gameTutorialMode;
        [SerializeField] private string gameLaunchMessage = "";
        private Label gameLaunchInfo;

        private void CreateGameLaunchControls(VisualElement parent)
        {
            ToolbarButton launch = new ToolbarButton(LaunchSelectedGame) { text = "게임 플레이", name = "game-play-level" };
            parent.Add(launch);
            PopupField<string> source = new PopupField<string>("게임 입력", new List<string> { "에셋", "MemoryPack" }, (int)gameLevelSource) { name = "game-level-source" };
            source.style.width = 190; source.labelElement.style.minWidth = 60;
            source.labelElement.style.width = 60;
            source.RegisterValueChangedCallback(evt =>
            {
                if (IsJsonFlowDraft) { source.SetValueWithoutNotify("현재 편집 사본"); return; }
                gameLevelSource = evt.newValue == "에셋" ? PuzzleEditorLevelSource.Asset : PuzzleEditorLevelSource.MemoryPack;
                gameLaunchMessage = "";
            });
            if (IsJsonFlowDraft) { source.choices = new List<string> { "현재 편집 사본" }; source.SetValueWithoutNotify("현재 편집 사본"); source.tooltip = JsonFlowDraftRestriction; }
            parent.Add(source);
            IntegerField seedField = new IntegerField("게임 시드") { value = gameLevelSeed, name = "game-level-seed", isDelayed = false };
            seedField.style.width = 170; seedField.labelElement.style.minWidth = 60; seedField.labelElement.style.width = 60;
            seedField.RegisterValueChangedCallback(evt => { gameLevelSeed = evt.newValue; gameLaunchMessage = ""; });
            parent.Add(seedField);
            PopupField<string> tutorialMode = new PopupField<string>("튜토리얼", new List<string> { "자동", "항상 실행", "실행 안 함" }, (int)gameTutorialMode) { name = "game-tutorial-mode" };
            tutorialMode.style.width = 190;
            tutorialMode.RegisterValueChangedCallback(evt => gameTutorialMode = (Tutorial.TutorialRunMode)tutorialMode.choices.IndexOf(evt.newValue));
            parent.Add(tutorialMode);
            gameLaunchInfo = new Label { name = "game-launch-info" };
            gameLaunchInfo.style.whiteSpace = WhiteSpace.Normal;
            gameLaunchInfo.style.marginLeft = 6;
            editorRoot.Add(gameLaunchInfo);
            Refresh();
            parent.schedule.Execute(Refresh).Every(200);

            void Refresh()
            {
                bool available = !PuzzleEditorLauncher.IsBusy && !EditorApplication.isCompiling && !EditorApplication.isPlayingOrWillChangePlaymode;
                launch.SetEnabled(level != null && available);
                source.SetEnabled(available && !IsJsonMode && !IsJsonFlowDraft); seedField.SetEnabled(available); tutorialMode.SetEnabled(available);
                string input = IsJsonFlowDraft ? "공통 원본 편집 사본의 현재 값 사용 · 부모 적용과 무관한 시험" : IsJsonMode ? "JSON 작업 폴더의 현재 편집값 사용 (시험용 스냅샷)" : gameLevelSource == PuzzleEditorLevelSource.Asset ? "에셋의 현재 편집값 사용 (미저장 값 포함)" : "마지막 생성 MemoryPack 사용 · 갱신: 플레이 테스트 → MemoryPack 갱신";
                gameLaunchInfo.text = gameLaunchMessage != "" ? gameLaunchMessage : $"게임 실행: 레벨 {(level != null ? level.LevelNumber.ToString() : "미선택")} · 시드 {gameLevelSeed} · {input}";
            }
        }

        private void LaunchSelectedGame()
        {
            try
            {
                if (!TryPrepareJsonDraftTest(out string connectionError)) throw new InvalidOperationException(connectionError);
                board?.CancelStroke();
                data?.ApplyModifiedProperties();
                PuzzleEditorLaunchRequest request = IsJsonMode
                    ? PuzzleEditorLaunchRequest.FromJson(CreateJsonPlayRequest(gameLevelSeed), gameTutorialMode)
                    : PuzzleEditorLaunchRequest.Capture(level, IsJsonFlowDraft ? PuzzleEditorLevelSource.Asset : gameLevelSource, gameLevelSeed, gameTutorialMode);
                CaptureJsonViewState();
                PuzzleEditorLauncher.Launch(request, GetInstanceID());
                gameLaunchMessage = $"레벨 {request.LevelNumber} · {request.Source} · 시드 {request.Seed} 게임 실행 중";
            }
            catch (Exception error)
            {
                gameLaunchMessage = "게임 실행 실패: " + error.Message;
                if (gameLaunchInfo != null) gameLaunchInfo.text = gameLaunchMessage;
            }
        }

        private void OnGameLaunchFinished(int owner, string message)
        {
            if (owner != GetInstanceID()) return;
            gameLaunchMessage = message;
            SelectWorkspaceTab(0);
            Focus(); Repaint();
        }
    }
}
