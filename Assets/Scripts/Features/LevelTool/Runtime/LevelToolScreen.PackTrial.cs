#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using LevelAuthoring.Documents;
using Levels;
using UnityEngine.UIElements;

namespace LevelTool
{
    public sealed partial class LevelToolScreen
    {
        private bool packedTrial;
        private string checkedPackHash;
        private Label packStatus;
        private int packCheckRevision;

        private void DrawTrialSource()
        {
            var controls = new VisualElement(); controls.AddToClassList("toolbar"); editor.Add(controls);
            var source = new DropdownField("게임 시험 입력", new List<string> { "현재 편집본 JSON", "생성된 MemoryPack" }, packedTrial ? 1 : 0) { name = "trial-source" };
            source.tooltip = "편집본은 미저장 내용까지 시험합니다. MemoryPack은 마지막으로 생성·등록한 팩을 시험하며 현재 편집 내용을 저장하거나 반영하지 않습니다.";
            source.RegisterValueChangedCallback(change => { packedTrial = source.index == 1; UpdatePackStatus(); }); controls.Add(source);
            Button(controls, "check-pack", "팩 상태 확인", () => CheckPackStatus().Forget());
            packStatus = new Label { name = "trial-pack-status" }; editor.Add(packStatus); UpdatePackStatus();
        }

        public async UniTask CheckPackStatus()
        {
            int revision = ++packCheckRevision;
            try
            {
                packStatus.text = "등록된 팩 확인 중…";
                byte[] bytes = await ContentPackAssetLoader.LoadAsync(ContentPackGenerationCodec.Address, this.GetCancellationTokenOnDestroy());
                if (revision != packCheckRevision) return;
                checkedPackHash = ContentPackGenerationCodec.Decode(bytes).SourceHash;
                UpdatePackStatus();
            }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                if (revision != packCheckRevision || this == null) return;
                checkedPackHash = null; packStatus.text = "팩 확인 실패: " + error.Message;
            }
        }

        private void UpdatePackStatus()
        {
            if (packStatus == null) return;
            string input = packedTrial ? "시험 입력: 생성된 팩. " : "시험 입력: 현재 편집본(미저장 포함). ";
            if (Session == null) { packStatus.text = input + "JSON 작업 폴더를 먼저 여세요."; return; }
            if (checkedPackHash == null) { packStatus.text = input + "팩 상태 확인을 눌러 최신 여부를 확인하세요."; return; }
            try
            {
                string hash = ContentSnapshotFingerprint.Compute(sharedTutorialDraft?.Preview(Session) ?? Session.CreateSnapshot());
                packStatus.text = input + (hash == checkedPackHash ? "팩과 현재 편집본이 일치합니다." : "팩과 현재 편집본이 다릅니다. 팩 시험에는 현재 수정이 반영되지 않습니다.");
            }
            catch (Exception error) { packStatus.text = input + "편집본 비교 불가: " + error.Message; }
        }
    }
}
#endif
