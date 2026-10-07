using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    public sealed partial class LevelInitialStatePanel
    {
        private void CreateLevelSourceUI(VisualElement toolbar)
        {
            PopupField<string> source = new PopupField<string>("레벨 입력", new List<string> { "에셋", "MemoryPack" }, useMemoryPack ? 1 : 0)
                { name = "initial-level-source", tooltip = "MemoryPack은 마지막 생성 파일을 읽습니다. 에셋 변경은 갱신 후 반영됩니다." };
            source.RegisterValueChangedCallback(evt =>
            {
                if (batchSession?.CanContinue == true || balancePanel?.CanContinue == true)
                { source.SetValueWithoutNotify(useMemoryPack ? "MemoryPack" : "에셋"); return; }
                useMemoryPack = evt.newValue == "MemoryPack";
                ClearBatch();
                Invalidate("레벨 입력이 바뀌었습니다. 다시 구성하세요.");
                ReloadPackedLevel();
                UpdateManualControls();
            });
            toolbar.Add(source);
            toolbar.Add(new Button(() =>
            {
                if (batchSession?.CanContinue == true || balancePanel?.CanContinue == true) return;
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
            ReleasePackedLevel();
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
