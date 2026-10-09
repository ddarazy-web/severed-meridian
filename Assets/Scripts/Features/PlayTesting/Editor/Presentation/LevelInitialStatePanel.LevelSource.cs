using System;
using System.Collections.Generic;
using System.IO;
using Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    public sealed partial class LevelInitialStatePanel
    {
        private string jsonSourceFingerprint;
        private bool JsonInput => Owner != null && Owner.IsJsonMode;
        private bool JsonDraftInput => Owner != null && Owner.IsJsonFlowDraft;
        private void UpdateSourceControls()
        {
            if (rootVisualElement == null) return;
            if (JsonDraftInput) useMemoryPack = false;
            var source = rootVisualElement.Q<PopupField<string>>("initial-level-source");
            if (source != null)
            {
                source.choices = JsonDraftInput ? new List<string> { "현재 편집 사본" } : JsonInput ? new List<string> { "JSON 편집값" } : new List<string> { "에셋", "MemoryPack" };
                source.SetValueWithoutNotify(JsonDraftInput ? "현재 편집 사본" : JsonInput ? "JSON 편집값" : useMemoryPack ? "MemoryPack" : "에셋");
                source.SetEnabled(!JsonInput && !JsonDraftInput);
            }
            var rebuild = rootVisualElement.Q<Button>("initial-pack-rebuild");
            if (rebuild != null)
            {
                rebuild.SetEnabled(!JsonInput && !JsonDraftInput);
                rebuild.tooltip = JsonDraftInput ? LevelEditorWindow.JsonFlowDraftRestriction : JsonInput ? "JSON 편집 중에는 기존 SO 기반 배포 팩을 갱신하지 않습니다. 시험은 현재 편집 스냅샷을 사용합니다." : "기존 SO 레벨의 MemoryPack을 갱신합니다.";
            }
            rootVisualElement.Q<UnityEditor.UIElements.ObjectField>("initial-level")?.SetEnabled(!JsonInput && !JsonDraftInput);
        }
        private void CreateLevelSourceUI(VisualElement toolbar)
        {
            PopupField<string> source = new PopupField<string>("레벨 입력", new List<string> { "에셋", "MemoryPack" }, useMemoryPack ? 1 : 0)
                { name = "initial-level-source", tooltip = "MemoryPack은 마지막 생성 파일을 읽습니다. 에셋 변경은 갱신 후 반영됩니다." };
            source.RegisterValueChangedCallback(evt =>
            {
                if (JsonInput || JsonDraftInput || batchSession?.CanContinue == true || balancePanel?.CanContinue == true)
                { UpdateSourceControls(); return; }
                useMemoryPack = evt.newValue == "MemoryPack";
                ClearBatch();
                Invalidate("레벨 입력이 바뀌었습니다. 다시 구성하세요.");
                ReloadPackedLevel();
                UpdateManualControls();
            });
            toolbar.Add(source);
            toolbar.Add(new Button(() =>
            {
                if (JsonInput || JsonDraftInput || batchSession?.CanContinue == true || balancePanel?.CanContinue == true) return;
                try
                {
                    LevelPackBuild.Generate();
                    Invalidate("MemoryPack 갱신 완료. 다시 구성하세요.");
                    ReloadPackedLevel();
                    UpdateManualControls();
                }
                catch (Exception error) { status.text = "MemoryPack 갱신 실패: " + error.Message; }
            }) { name = "initial-pack-rebuild", text = "MemoryPack 갱신" });
        }

        private bool ReloadPackedLevel()
        {
            if (JsonDraftInput && !Owner.TryPrepareJsonDraftTest(out string connectionError))
            { if (status != null) status.text = connectionError; return false; }
            ReleasePackedLevel();
            UpdateSourceControls();
            if (JsonInput)
            {
                if (sourceLevel == null) return false;
                try
                {
                    packedLevel = Owner.CreateJsonPlayRequest(seed).CreateDefinition();
                    jsonSourceFingerprint = LevelStateBuilder.Fingerprint(sourceLevel);
                    return true;
                }
                catch (Exception error) { if (status != null) status.text = "JSON 시험 입력 오류: " + error.Message; return false; }
            }
            if (!useMemoryPack) return true;
            if (sourceLevel == null) return false;
            try
            {
                string path = LevelPackBuild.FilePath(sourceLevel.LevelNumber);
                if (!File.Exists(path)) throw new FileNotFoundException("생성 파일이 없습니다. MemoryPack 갱신을 누르세요.", path);
                var content = Elements.ElementContentPackCodec.Decode(File.ReadAllBytes(Elements.Editor.ElementContentPackBuild.OutputPath));
                packedLevel = LevelPackCodec.ReadLevel(File.ReadAllBytes(path), sourceLevel.LevelNumber, content);
                return true;
            }
            catch (Exception error)
            {
                if (status != null) status.text = "MemoryPack 로드 실패: " + error.Message;
                return false;
            }
        }

        private void ReleasePackedLevel()
        {
            if (packedLevel != null) UnityEngine.Object.DestroyImmediate(packedLevel);
            packedLevel = null;
        }
    }
}
