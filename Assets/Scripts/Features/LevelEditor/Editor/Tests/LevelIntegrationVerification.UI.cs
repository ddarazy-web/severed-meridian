using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.UIElements;

namespace Levels.Editor
{
    public static partial class LevelIntegrationVerification
    {
        private static readonly List<string> Results = new List<string>();
        private static IEnumerator sequence;
        private static double nextTick;
        private static string originalClipboard;
        [Serializable] private sealed class SavedState { public string folder; public int process; public string[] paths, values, guids; }

        public static void Start()
        {
            Directory.CreateDirectory(Evidence);
            if (File.Exists(Evidence + "/state.json")) throw new InvalidOperationException("기존 통합 검증 Restart를 먼저 실행하세요.");
            folder = "Assets/__LevelIntegrationVerification_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            originalClipboard = EditorGUIUtility.systemCopyBuffer;
            window = ScriptableObject.CreateInstance<LevelEditorWindow>();
            window.titleContent = new GUIContent("7단계 제작 동선 검증");
            window.position = new Rect(10, 10, 1000, 780); window.ShowUtility(); window.Focus();
            sequence = RunIntegration(); EditorApplication.update += IntegrationTick;
        }

        private static void IntegrationTick()
        {
            if (EditorApplication.timeSinceStartup < nextTick) return;
            nextTick = EditorApplication.timeSinceStartup + 0.3;
            try
            {
                if (sequence.MoveNext()) return;
                string[] paths = AssetDatabase.FindAssets("t:LevelDefinition", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path).ToArray();
                foreach (string path in paths) AssetDatabase.SaveAssetIfDirty(AssetDatabase.LoadAssetAtPath<LevelDefinition>(path));
                File.WriteAllText(Evidence + "/state.json", JsonUtility.ToJson(new SavedState { folder = folder, process = System.Diagnostics.Process.GetCurrentProcess().Id,
                    paths = paths, guids = paths.Select(AssetDatabase.AssetPathToGUID).ToArray(), values = paths.Select(path => JsonUtility.ToJson(AssetDatabase.LoadAssetAtPath<LevelDefinition>(path))).ToArray() }, true));
                FinishIntegration(0);
            }
            catch (Exception exception) { Results.Add("FAIL " + exception); Debug.LogException(exception); FinishIntegration(1); }
        }

        private static void FinishIntegration(int code)
        {
            EditorApplication.update -= IntegrationTick;
            File.WriteAllLines(Evidence + "/ui-results.txt", Results);
            EditorGUIUtility.systemCopyBuffer = originalClipboard;
            if (window != null) window.Close();
            if (code != 0) { if (level != null) File.WriteAllText(Evidence + "/failed-level.json", JsonUtility.ToJson(level, true)); AssetDatabase.DeleteAsset(folder); }
            EditorApplication.Exit(code);
        }

        public static void Restart()
        {
            SavedState state = JsonUtility.FromJson<SavedState>(File.ReadAllText(Evidence + "/state.json"));
            Check(state.process != System.Diagnostics.Process.GetCurrentProcess().Id, "독립 프로세스 재시작");
            for (int i = 0; i < state.paths.Length; i++)
            {
                LevelDefinition saved = AssetDatabase.LoadAssetAtPath<LevelDefinition>(state.paths[i]);
                Check(saved != null && JsonUtility.ToJson(saved) == state.values[i] && AssetDatabase.AssetPathToGUID(state.paths[i]) == state.guids[i], "전체 값·GUID 재로딩 " + state.paths[i]);
            }
            if (!state.folder.StartsWith("Assets/__LevelIntegrationVerification_", StringComparison.Ordinal) || state.folder.Contains("..")) throw new InvalidOperationException("정리 대상 범위 오류");
            Check(AssetDatabase.DeleteAsset(state.folder), "소유한 통합 검증 에셋 정리");
            File.WriteAllLines(Evidence + "/restart-results.txt", Results);
            File.Move(Evidence + "/state.json", Evidence + "/completed-" + DateTime.UtcNow.Ticks + ".json");
        }

        private static IEnumerator RunIntegration()
        {
            yield return null;
            Check(window.rootVisualElement.Q<LevelBoardView>().panel != null, "빈 Editor 열기");
            string[] existing = AssetDatabase.FindAssets("t:LevelDefinition");
            Click("new-level"); yield return null;
            Click("confirm-level-name"); yield return null;
            level = window.CurrentLevel;
            string newPath = AssetDatabase.GetAssetPath(level);
            Check(!existing.Contains(AssetDatabase.AssetPathToGUID(newPath)), "새 레벨 UI로 독립 에셋 생성");
            string moveError = AssetDatabase.MoveAsset(newPath, folder + "/Created.asset");
            Check(string.IsNullOrEmpty(moveError), "방금 생성한 검증 에셋을 소유 폴더로 이동"); yield return null;
            Click("inspector-tab-1"); yield return null;
            window.rootVisualElement.Query<PropertyField>().ToList().First(field => field.bindingPath == "levelNumber").Q<IntegerField>().value = 71011;
            yield return null;
            window.rootVisualElement.Query<PropertyField>().ToList().First(field => field.bindingPath == "moveCount").Q<IntegerField>().value = 24;
            yield return null;
            Check(level.LevelNumber == 71011 && level.MoveCount == 24, "새 레벨 기본 속성 UI 편집");
            Click("add-mission"); yield return null;
            window.rootVisualElement.Q<IntegerField>("mission-count-0").value = 12; yield return null;
            Check(level.Missions.Count == 1 && level.Missions[0].Count == 12, "기본 설정에서 미션 작성");
            Tool("tool-Deactivate"); Paint(8, 8); yield return null;
            Tool("tool-Fixed-0"); Paint(2, 2); yield return null;
            Tool("place-Block-" + (int)InitialBlockKind.Rocket); Paint(2, 3); yield return null;
            Tool("place-Obstacle-" + (int)ObstacleKind.Crate); Paint(4, 2); Paint(4, 3); yield return null;
            Tool("place-Cover-" + (int)CoverKind.Web); Paint(2, 2); yield return null;
            Tool("place-Dust-0"); Paint(2, 2); yield return null;
            Check(level.InitialBlocks.Count == 2 && level.Obstacles.Count == 2 && level.Covers.Count == 1 && level.Dust.Count == 1, "도구 연속 전환으로 층별 배치 작성");
            Menu("menu-flow", "중력 →"); FlowClick(6, 1); yield return null;
            Menu("menu-flow", "직접 경로"); FlowClick(6, 1); FlowClick(6, 2); FlowClick(7, 2); Click("complete-flow"); yield return null;
            Check(level.Flow.Paths.Count == 3 && level.Flow.Paths.Last().IsEnd, "배치 이후 흐름 경로와 끝칸 구성");
            Menu("menu-supply", "생성구 선택"); Paint(0, 0); yield return null;
            window.rootVisualElement.Q<PopupField<string>>("source-mode").value = "고정 목록"; yield return null;
            LevelContextMenuVerification.Action(window, "공급 항목/추가").Execute(); yield return null;
            window.rootVisualElement.Q<IntegerField>("supply-item-count").value = 3; yield return null;
            Check(level.Supply.Sources[0].Items.Count == 1 && level.Supply.Sources[0].Items[0].Count == 3, "흐름 작성 후 고정 공급 목록 작성");
            Click("validate-level"); yield return null;
            Check(LevelDefinitionValidator.Validate(level).Any(issue => issue.Code == LevelValidationCode.InvalidMerge), "작성 중 합류 우선순위 누락 검출");
            Button mergeIssue = window.rootVisualElement.Q<ScrollView>("validation-issues").Query<Button>().ToList().First(button => button.text.StartsWith("InvalidMerge:"));
            using (NavigationSubmitEvent navigate = NavigationSubmitEvent.GetPooled()) { navigate.target = mergeIssue; mergeIssue.SendEvent(navigate); }
            yield return null;
            Check(window.rootVisualElement.Q<LevelFlowOverlay>().Tool == FlowTool.Select && window.rootVisualElement.Q<Button>("confirm-merge") != null, "공급 편집에서 오류 클릭으로 흐름 합류 수정 위치 이동");
            Click("confirm-merge"); yield return null;
            Check(window.rootVisualElement.Q<Label>("validation-status").text.Contains("다시 검사"), "오류 수정 후 이전 검사 결과 무효화");
            Click("validate-level"); yield return null;
            Check(LevelDefinitionValidator.Validate(level).Count == 0, "합류 순서 수정 후 구조 검사 오류 없음: " + string.Join(" | ", LevelDefinitionValidator.Validate(level)));
            Check(window.rootVisualElement.Q<Label>("validation-status").text.Contains("플레이 가능 판정 아님"), "구조 검사와 게임 실행 가능성 구분");
            Click("save-level"); yield return null;
            Check(!EditorUtility.IsDirty(level), "제작 결과 명시적 저장");
            CaptureIntegration("created-wide.png");
            window.position = new Rect(10, 10, 680, 480); yield return null; yield return null;
            Check(window.rootVisualElement.Q<ScrollView>("board-scroll").horizontalScroller.highValue > 0, "좁은 창에서 보드 스크롤 접근");
            CaptureIntegration("created-narrow.png");
            window.position = new Rect(10, 10, 1000, 780); yield return null;
            IEnumerator remaining = RunRemaining();
            while (remaining.MoveNext()) yield return remaining.Current;
        }

        private static void Tool(string name)
        { if (!LevelEditorVerification.ChooseMenuTool(window, name)) throw new InvalidOperationException("도구 없음: " + name); }
        private static void Menu(string name, string value)
        { Click(name == "menu-flow" || name == "menu-terrain" ? "tool-tab-1" : "tool-tab-0"); window.rootVisualElement.Q<PopupField<string>>(name).value = value; }
        private static void Click(string name)
        {
            Button button = window.rootVisualElement.Q<Button>(name);
            if (button == null || !button.enabledInHierarchy) throw new InvalidOperationException("버튼 사용 불가: " + name);
            using NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled(); evt.target = button; button.SendEvent(evt);
        }
        private static void Paint(int row, int column) => PointerCell(window.rootVisualElement.Q<LevelBoardView>(), row, column);
        private static void FlowClick(int row, int column) => PointerCell(window.rootVisualElement.Q<LevelFlowOverlay>(), row, column);
        private static void PointerCell(VisualElement target, int row, int column)
        {
            Vector2 position = target.LocalToWorld(new Vector2(column * LevelBoardView.CellSize + LevelBoardView.CellSize / 2f, row * LevelBoardView.CellSize + LevelBoardView.CellSize / 2f));
            Event input = new Event { type = EventType.MouseDown, button = 0, mousePosition = position };
            using (PointerDownEvent down = PointerDownEvent.GetPooled(input)) { down.target = target; target.SendEvent(down); }
            input.type = EventType.MouseUp;
            using PointerUpEvent up = PointerUpEvent.GetPooled(input); up.target = target; target.SendEvent(up);
        }
        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); Results.Add("PASS " + message); Debug.Log("[LevelIntegrationVerification] " + message); }
        private static void CaptureIntegration(string name)
        {
            Rect rect = window.position;
            Texture2D texture = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGB24, false);
            texture.SetPixels(UnityEditorInternal.InternalEditorUtility.ReadScreenPixel(rect.position, (int)rect.width, (int)rect.height)); texture.Apply();
            File.WriteAllBytes(Evidence + "/" + name, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}




