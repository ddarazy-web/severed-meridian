using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    public static class LevelNamingVerification
    {
        private const string Evidence = "Logs/LevelNamingManualVerification";
        private static readonly List<string> Results = new List<string>();
        private static LevelEditorWindow window;
        private static LevelDefinition created;
        private static string folder;
        private static IEnumerator sequence;
        private static double next;

        public static void Start()
        {
            Directory.CreateDirectory(Evidence);
            folder = "Assets/__LevelNamingVerification_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            window = LevelEditorWindow.OpenWorkspace(0);
            window.position = new Rect(20, 20, 1160, 780);
            sequence = Run(); EditorApplication.update += Tick;
        }

        private static void Check(bool pass, string label)
        {
            if (!pass) throw new InvalidOperationException(label);
            Results.Add("PASS " + label);
        }

        private static void Click(string name)
        {
            Button button = window.rootVisualElement.Q<Button>(name);
            Check(button != null && button.enabledInHierarchy, "버튼 접근 " + name);
            using NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled(); evt.target = button; button.SendEvent(evt);
        }

        private static IEnumerator Run()
        {
            yield return null;
            typeof(ConnectionGraphVerification).GetField("window", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, window);
            typeof(ConnectionGraphVerification).GetMethod("RaiseWindow", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null); yield return null;
            foreach (string name in new[] { "", " ", "../나쁜이름", "a/b", "a:b", "a?b", "CON", "COM1.test", "이름.", "이름.asset" })
                Check(LevelAssetOperations.FileNameError(name) != null, "잘못된 이름 거절 " + name);
            Click("new-level"); yield return null;
            window.rootVisualElement.Q<TextField>("level-file-name").value = "01_처음 만난 달토끼";
            yield return null;
            Capture("name-form.png");
            Click("cancel-level-name");
            string unique = "검증_" + Guid.NewGuid().ToString("N");
            Check(string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(LevelAssetOperations.DefaultFolder + "/" + unique + ".asset")), "신규 파일 경로 비어 있음");
            Click("new-level"); window.rootVisualElement.Q<TextField>("level-file-name").value = unique;
            Click("confirm-level-name"); yield return null;
            created = window.CurrentLevel;
            Check(created != null && created.name == unique && created.Board.Cells.Count == 81, "지정한 이름으로 새 레벨 생성");
            string path = AssetDatabase.GetAssetPath(created);
            Check(string.IsNullOrEmpty(AssetDatabase.MoveAsset(path, folder + "/Before.asset")), "소유 검증 폴더로 이동");
            path = AssetDatabase.GetAssetPath(created);
            string guid = AssetDatabase.AssetPathToGUID(path);
            JsonUtility.FromJsonOverwrite("{\"levelNumber\":81001,\"moveCount\":37}", created); EditorUtility.SetDirty(created);
            Click("rename-level"); window.rootVisualElement.Q<TextField>("level-file-name").value = "01_처음 만난 달토끼";
            Check(window.rootVisualElement.Q<Label>("level-name-message").text.Contains("함께 저장"), "이름 변경 전 저장 안내 표시");
            yield return null; Capture("rename-form.png");
            Click("confirm-level-name"); yield return null;
            path = AssetDatabase.GetAssetPath(created);
            Check(Path.GetFileName(path) == "01_처음 만난 달토끼.asset" && AssetDatabase.AssetPathToGUID(path) == guid, "한글 이름 변경·GUID 보존");
            File.WriteAllText(Evidence + "/rename-state.txt", "number=" + created.LevelNumber + " moves=" + created.MoveCount + " dirty=" + EditorUtility.IsDirty(created) + "\n" + File.ReadAllText(path));
            Check(created.LevelNumber == 81001 && created.MoveCount == 37 && !EditorUtility.IsDirty(created), "이름 변경·저장 후 내용·번호 보존");
            Check(File.ReadAllText(path).Contains("moveCount: 37"), "이름 변경·저장은 현재 내용도 파일에 반영");
            LevelDefinition other = LevelAssetOperations.CreateAtPath(folder + "/Existing.asset");
            Check(LevelAssetOperations.Rename(created, "Existing") != null && AssetDatabase.GetAssetPath(created) == path, "중복 이름 거절·원본 경로 보존");
            Check(LevelAssetOperations.Rename(created, "../Escape") != null && AssetDatabase.GetAssetPath(created) == path, "경로 탈출 입력 거절");
            Click("rename-level"); window.rootVisualElement.Q<TextField>("level-file-name").value = "취소한 이름"; Click("cancel-level-name");
            Check(AssetDatabase.GetAssetPath(created) == path, "이름 변경 취소는 원본 유지");
            AssetDatabase.SaveAssetIfDirty(created);
            Check(AssetDatabase.LoadAssetAtPath<LevelDefinition>(path).MoveCount == 37, "명시적 저장 후 내용 유지");
            LevelDefinition fixture = (LevelDefinition)typeof(ItemBoosterVerification).GetMethod("PlayFixture", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(fixture), created); UnityEngine.Object.DestroyImmediate(fixture);
            window.SetLevel(other); window.SetLevel(created); yield return null;
            typeof(ConnectionGraphVerification).GetField("window", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, window);
            typeof(ConnectionGraphVerification).GetMethod("RaiseWindow", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null); yield return null;
            Capture("editor-overview.png");
            Click("tool-tab-1"); yield return null; Capture("flow-menu.png");
            Click("tool-tab-2"); yield return null; Capture("used-list.png");
            Click("inspector-tab-1"); yield return null; Capture("level-settings.png");
            Click("play-level");
            for (int i = 0; window.ActiveSimulationPanel.IsSearching && i < 600; i++) yield return null;
            Check(window.ActiveSimulationPanel.CurrentState != null, "새 이름의 레벨로 플레이 준비");
            Click("manual-start"); yield return null; Capture("play-test.png");
            Click("workspace-tab-2"); yield return null; Capture("diagnostics.png");
            window.position = new Rect(20, 20, 680, 480); yield return null;
            Check(window.rootVisualElement.Q<Button>("open-level-manual").worldBound.xMax <= 680, "좁은 창에서 이름 변경·설명서 버튼 접근");
            Check(File.Exists("Docs/MoonRabbitJunkyard/Manual/index.html"), "설명서 시작 파일 존재");
        }

        private static void Capture(string name)
        {
            Rect rect = window.position;
            Texture2D image = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGB24, false);
            image.SetPixels(UnityEditorInternal.InternalEditorUtility.ReadScreenPixel(rect.position, (int)rect.width, (int)rect.height)); image.Apply();
            File.WriteAllBytes(Evidence + "/" + name, image.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(image);
        }

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup < next) return;
            next = EditorApplication.timeSinceStartup + 0.3;
            Exception failure = null;
            try { if (sequence.MoveNext()) return; }
            catch (Exception error) { failure = error; Results.Add("FAIL " + error); Debug.LogException(error); }
            EditorApplication.update -= Tick;
            if (window != null) window.Close();
            if (created != null && AssetDatabase.GetAssetPath(created).StartsWith(LevelAssetOperations.DefaultFolder + "/검증_", StringComparison.Ordinal)) AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(created));
            AssetDatabase.DeleteAsset(folder);
            File.WriteAllLines(Evidence + "/results.txt", Results);
            EditorApplication.Exit(failure == null ? 0 : 1);
        }
    }
}
