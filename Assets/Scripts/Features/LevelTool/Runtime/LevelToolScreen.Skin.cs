#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using Board;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelTool
{
    public sealed partial class LevelToolScreen
    {
        private BoardSpriteAtlas toolSkin;

        private async UniTask LoadToolSkin()
        {
            var owner = new BoardSpriteAtlas("LevelToolUI"); toolSkin = owner;
            try
            {
                await owner.LoadAsync();
                if (this != null && isActiveAndEnabled && toolSkin == owner) ApplyToolSkin();
            }
            catch (Exception error)
            {
                if (this != null && isActiveAndEnabled && toolSkin == owner)
                    Debug.LogWarning("레벨툴 UI 이미지 로드 실패: " + error.Message);
            }
        }

        private void ApplyToolSkin()
        {
            if (toolSkin == null || !toolSkin.IsLoaded || root == null) return;
            foreach (var element in root.Query<VisualElement>().ToList())
            {
                string sprite = null;
                if (element == leftPanel || element == rightPanel || element.ClassListContains("dialog")) sprite = "panel";
                else if (element is Button && element.parent != workspaceTabs && element.parent?.name != "menu-bar")
                { sprite = "button"; element.AddToClassList("skin-button"); }
                else if (element.ClassListContains("unity-base-field__input")) sprite = "input";
                if (sprite == null) continue;
                element.style.backgroundImage = new StyleBackground(toolSkin.Get(sprite));
                element.style.backgroundColor = Color.clear;
                element.style.unitySliceLeft = element.style.unitySliceRight = 16;
                element.style.unitySliceTop = element.style.unitySliceBottom = 16;
                element.style.unitySliceScale = .5f;
            }
            string[] controls = { "menu-file", "save", "undo", "redo", "play", "command-Validate", "", "select", "", "erase", "fit-board", "command-Help" };
            for (int i = 1; i < controls.Length; i++)
            {
                if (controls[i].Length == 0) continue;
                var button = root.Q<Button>(controls[i]);
                if (button == null || button.Q("tool-icon") != null) continue;
                var icon = new Image { name = "tool-icon", sprite = toolSkin.GetFrame("icons", 4, 4, i), pickingMode = PickingMode.Ignore };
                icon.style.position = Position.Absolute; icon.style.left = 7; icon.style.top = 6;
                icon.style.width = icon.style.height = 18;
                button.style.paddingLeft = 28;
                button.style.whiteSpace = WhiteSpace.NoWrap;
                if (button.parent == toolbar) button.style.minWidth = button.text.Length * 14 + 40;
                button.Add(icon);
            }
        }
    }
}
#endif
