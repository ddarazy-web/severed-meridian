using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    public static partial class LevelCommonEditingVerification
    {
        private static LevelEditorWindow window;
        private static LevelDefinition level;
        private static string uiFolder, clipboardBefore;
        private static IEnumerator sequence;
        private static double nextTick;

        public static void Start()
        {
            try
            {
                Prepare();
                State state = JsonUtility.FromJson<State>(File.ReadAllText(Evidence + "/state.json"));
                uiFolder = state.folder;
                clipboardBefore = EditorGUIUtility.systemCopyBuffer;
                level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(uiFolder + "/Source.asset");
                Set(level, "obstacles.Array.data[1].durability", 2);
                Set(level, "obstacles.Array.data[1].color", (int)RabbitColor.Type2);
                window = ScriptableObject.CreateInstance<LevelEditorWindow>();
                window.titleContent = new GUIContent("6단계 공통 편집 검증");
                window.position = new Rect(10, 10, 1000, 780); window.ShowUtility(); window.SetLevel(level); window.Focus();
                sequence = RunUI(); EditorApplication.update += Tick;
            }
            catch (Exception exception) { Results.Add("FAIL " + exception); Debug.LogException(exception); Finish(1); }
        }

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup < nextTick) return;
            nextTick = EditorApplication.timeSinceStartup + 0.2;
            try
            {
                if (sequence.MoveNext()) return;
                string[] paths = AssetDatabase.FindAssets("t:LevelDefinition", new[] { uiFolder }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path).ToArray();
                foreach (string path in paths) AssetDatabase.SaveAssetIfDirty(AssetDatabase.LoadAssetAtPath<LevelDefinition>(path));
                File.WriteAllText(Evidence + "/state.json", JsonUtility.ToJson(new State { folder = uiFolder, process = System.Diagnostics.Process.GetCurrentProcess().Id,
                    paths = paths, values = paths.Select(path => JsonUtility.ToJson(AssetDatabase.LoadAssetAtPath<LevelDefinition>(path))).ToArray(), guids = paths.Select(AssetDatabase.AssetPathToGUID).ToArray() }, true));
                Finish(0);
            }
            catch (Exception exception) { Results.Add("FAIL " + exception); Debug.LogException(exception); Finish(1); }
        }

        private static void Finish(int code)
        {
            EditorApplication.update -= Tick;
            File.WriteAllLines(Evidence + "/ui-results.txt", Results);
            if (clipboardBefore != null) EditorGUIUtility.systemCopyBuffer = clipboardBefore;
            if (window != null) window.Close();
            if (code != 0 && uiFolder != null)
            {
                AssetDatabase.DeleteAsset(uiFolder);
                File.Delete(Evidence + "/state.json");
            }
            EditorApplication.Exit(code);
        }

        private static IEnumerator RunUI()
        {
            yield return null;
            window.rootVisualElement.Q<PopupField<string>>("placement-layer").index = (int)PlacementLayer.Obstacle; yield return null;
            Cell(4, 0); yield return null; Cell(4, 1, true); yield return null;
            Check(window.rootVisualElement.Q<LevelBoardView>().PlacementSelection.Count == 2, "실제 Ctrl 다중 선택");
            Check(window.rootVisualElement.Q<IntegerField>("common-durability").showMixedValue, "내구도 혼합 값 표시");
            Check(window.rootVisualElement.Q<PopupField<string>>("common-color").value.Contains("혼합"), "색 혼합 값 표시");
            IntegerField staleField = window.rootVisualElement.Q<IntegerField>("common-durability");
            int priorDurability = level.Obstacles[0].Durability;
            Set(level, "moveCount", 38);
            staleField.value = 2; yield return null;
            Check(level.Obstacles[0].Durability == priorDurability && window.rootVisualElement.Q<Label>("operation-status").text.Contains("변경"), "외부 변경 직후 오래된 공통 입력 거절");
            string before = JsonUtility.ToJson(level);
            window.rootVisualElement.Q<IntegerField>("common-durability").value = 1; yield return null;
            Check(level.Obstacles[0].Durability == 1 && level.Obstacles[1].Durability == 1 && level.Obstacles[1].Color == RabbitColor.Type2, "UI 공통 내구도만 변경");
            Undo.PerformUndo(); yield return null;
            Check(JsonUtility.ToJson(level) == before, "UI 공통 편집 한 번 Undo"); Undo.PerformRedo(); yield return null; yield return null;
            before = JsonUtility.ToJson(level);
            window.rootVisualElement.Q<IntegerField>("common-durability").value = 99; yield return null;
            Check(JsonUtility.ToJson(level) == before && window.rootVisualElement.Q<Label>("operation-status").text.Contains("범위"), "UI 잘못된 값 거절");
            LevelBoardView contextBoard = window.rootVisualElement.Q<LevelBoardView>();
            LevelContextMenuVerification.Action(window, "설정 붙여넣기", contextBoard, contextBoard.CellAt(C(4, 1)).worldBound.center);
            Check(contextBoard.PlacementSelection.Count == 2, "보드 우클릭 다중 선택 유지");
            LevelContextMenuVerification.Action(window, "설정 복사", contextBoard, contextBoard.CellAt(C(5, 0)).worldBound.center);
            Check(contextBoard.PlacementSelection.Count == 1, "보드 우클릭 새 대상 선택");
            Cell(4, 0); yield return null; Cell(4, 1, true); yield return null;
            Capture("common-wide.png");
            Cell(4, 0); yield return null; LevelContextMenuVerification.Action(window, "설정 복사").Execute(); yield return null;
            Cell(4, 1); yield return null; Cell(5, 0, true); yield return null;
            Check(window.rootVisualElement.Q<IntegerField>("common-durability") == null && window.rootVisualElement.Q<Button>("delete-placement") == null, "혼합 종류 공통 수정·단일 삭제 제외");
            LevelContextMenuVerification.Action(window, "설정 붙여넣기").Execute(); yield return null;
            Check(level.Obstacles[1].Color == RabbitColor.Type1 && level.Obstacles[2].Durability == 2 &&
                window.rootVisualElement.Q<Label>("operation-status").text.Contains("제외 1개"), "혼합 선택 붙여넣기 제외 수·원본 보존");
            Cell(6, 6); yield return null;
            Check(window.rootVisualElement.Q<LevelBoardView>().PlacementSelection.Count == 4, "2×2 전체 본체 강조");
            LevelContextMenuVerification.Action(window, "2×2 본체 이동").Execute();
            Check(contextBoard.Brush == LevelBrush.Move, "컨텍스트 본체 이동 도구 시작");
            contextBoard.CancelStroke();
            before = JsonUtility.ToJson(level);
            LevelContextMenuVerification.Action(window, "선택한 층의 요소 삭제").Execute(); yield return null;
            Check(JsonUtility.ToJson(level) != before, "컨텍스트 선택 요소 삭제");
            Undo.PerformUndo(); yield return null;
            Check(JsonUtility.ToJson(level) == before, "컨텍스트 삭제 Undo");
            Cell(6, 6); yield return null;
            LevelContextMenuVerification.Action(window, "바닥·연결/중력 기본값으로");
            Cell(7, 7, true); yield return null;
            Check(window.rootVisualElement.Q<LevelBoardView>().PlacementSelection.Count == 0, "2×2 다른 점유 칸으로 동일 본체 해제");
            Cell(4, 0); yield return null; Cell(4, 1, true); yield return null;
            window.position = new Rect(10, 10, 680, 480); yield return null; yield return null;
            Check(window.rootVisualElement.Q<ScrollView>("board-scroll").horizontalScroller.highValue > 0, "좁은 창 보드 스크롤");
            Capture("common-narrow.png");
            window.position = new Rect(10, 10, 1000, 780); yield return null;
            window.rootVisualElement.Q<PopupField<string>>("placement-layer").index = (int)PlacementLayer.Cover; yield return null;
            Check(window.rootVisualElement.Q<LevelBoardView>().PlacementSelection.Count == 0, "층 변경 시 선택 정리");
            Click("duplicate-level"); yield return null;
            Check(window.rootVisualElement.Q("duplicate-panel").resolvedStyle.display == DisplayStyle.Flex, "복제 번호 입력 패널 열기");
            before = JsonUtility.ToJson(level); bool dirty = EditorUtility.IsDirty(level);
            Click("cancel-duplicate"); yield return null;
            Check(window.CurrentLevel == level && JsonUtility.ToJson(level) == before && EditorUtility.IsDirty(level) == dirty, "복제 UI 취소 원본 보존");
            Click("duplicate-level"); yield return null;
            window.rootVisualElement.Q<IntegerField>("duplicate-number").value = 61010;
            yield return null;
            Capture("duplicate-panel.png");
            window.DuplicateCurrentTo(uiFolder + "/UICopy.asset"); yield return null;
            LevelDefinition copy = window.CurrentLevel;
            Check(copy != level && copy.LevelNumber == 61010 && JsonUtility.ToJson(level) == before && EditorUtility.IsDirty(level) == dirty, "복제 UI 완료 후 사본 선택·원본 보존");
            window.SetLevel(level); yield return null;
            window.rootVisualElement.Q<PopupField<string>>("placement-layer").index = (int)PlacementLayer.Obstacle; yield return null;
            Cell(4, 0); yield return null;
            DropdownMenuAction stale = LevelContextMenuVerification.Action(window, "설정 붙여넣기");
            window.SetLevel(copy); yield return null;
            before = JsonUtility.ToJson(copy); stale.Execute(); yield return null;
            Check(JsonUtility.ToJson(copy) == before, "에셋 전환 후 오래된 붙여넣기 거절");
        }

        private static void Click(string name)
        {
            Button button = window.rootVisualElement.Q<Button>(name);
            if (button == null || !button.enabledInHierarchy) throw new InvalidOperationException("버튼 사용 불가: " + name);
            Submit(button);
        }
        private static void Submit(Button button)
        { using NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled(); evt.target = button; button.SendEvent(evt); }
        private static void Cell(int row, int column, bool additive = false)
        {
            LevelBoardView board = window.rootVisualElement.Q<LevelBoardView>();
            Vector2 position = board.LocalToWorld(new Vector2(column * 40 + 20, row * 40 + 20));
            Event input = new Event { type = EventType.MouseDown, button = 0, mousePosition = position, modifiers = additive ? EventModifiers.Control : EventModifiers.None };
            using PointerDownEvent evt = PointerDownEvent.GetPooled(input); evt.target = board; board.SendEvent(evt);
        }
        private static void Capture(string name)
        {
            Rect rect = window.position;
            Texture2D texture = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGB24, false);
            texture.SetPixels(UnityEditorInternal.InternalEditorUtility.ReadScreenPixel(rect.position, (int)rect.width, (int)rect.height)); texture.Apply();
            File.WriteAllBytes(Evidence + "/" + name, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}



