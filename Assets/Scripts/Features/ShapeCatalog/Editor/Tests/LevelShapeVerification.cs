using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Board;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    /// <summary>별도 검증 실행용. 이 검사에서 소유한 임시 에셋만 생성·삭제하고 종료한다.</summary>
    public static class LevelShapeVerification
    {
        private const string Evidence = "Logs/LevelShapeCatalogVerification";
        private static readonly List<string> Results = new List<string>();
        private static LevelEditorWindow window;
        private static string folder;
        private static string ownedPath;
        private static readonly List<string> registeredPaths = new List<string>();
        private static IEnumerator sequence;
        private static double next;

        public static void Start()
        {
            Directory.CreateDirectory(Evidence);
            folder = "Assets/__ShapeVerification_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            sequence = Run(); EditorApplication.update += Tick;
        }

        private static void Check(bool success, string message)
        {
            if (!success) throw new InvalidOperationException(message);
            Results.Add("PASS " + message);
        }

        private static void Click(string name)
        {
            Button button = window.rootVisualElement.Q<Button>(name);
            Check(button != null && button.enabledInHierarchy, "버튼 접근 " + name);
            using NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled(); evt.target = button; button.SendEvent(evt);
        }

        private static IEnumerator Run()
        {
            var baseline = LevelShapeRecommendations.LoadAll().Select(p => AssetDatabase.GetAssetPath(p)).ToArray();
            Check(LevelShapeRecommendations.Register(null, out _) != null, "레벨 미선택 등록 거절");
            LevelDefinition source = LevelAssetOperations.CreateAtPath(folder + "/Source_" + Guid.NewGuid().ToString("N") + ".asset");
            var random = new System.Random(Guid.NewGuid().GetHashCode());
            using (SerializedObject edit = new SerializedObject(source))
            {
                // 기존 사용자 목록과 우연히 충돌하지 않는 검증 전용 모양을 만든다.
                for (int i = 0; i < 100; i++) edit.FindProperty("board.cells").GetArrayElementAtIndex(i).FindPropertyRelative("isActive").boolValue = i < 10 || random.Next(2) == 0;
                edit.FindProperty("levelNumber").intValue = 910021;
                var obstacles = edit.FindProperty("obstacles"); obstacles.arraySize = 2;
                foreach (int i in new[] { 0, 1 }) obstacles.GetArrayElementAtIndex(i).FindPropertyRelative("kind").intValue = (int)ObstacleKind.Crate;
                var covers = edit.FindProperty("covers"); covers.arraySize = 1;
                covers.GetArrayElementAtIndex(0).FindPropertyRelative("kind").intValue = (int)CoverKind.Web;
                edit.FindProperty("dust").arraySize = 1;
                edit.ApplyModifiedPropertiesWithoutUndo();
            }
            Check(LevelShapeRecommendations.Register(source, out _)?.Contains("저장하지 않은") == true, "미저장 수정 등록 거절");
            AssetDatabase.SaveAssetIfDirty(source);
            string original = JsonUtility.ToJson(source);
            Check(LevelShapeRecommendations.Register(source, out var first) == null, "저장한 사용자 모양 등록");
            registeredPaths.Add(AssetDatabase.GetAssetPath(first));
            Check(JsonUtility.ToJson(source) == original, "등록 후 원본 내용 보존");
            Check(first.Cells.SequenceEqual(source.Board.Cells.Select(c => c.IsActive)), "등록 모양 100칸 일치");
            Check(first.ObstacleHistory.Contains("나무상자 · 2개") && first.ObstacleHistory.Contains("거미줄 · 1칸") && first.ObstacleHistory.Contains("먼지 · 1칸"), "종류별 장애물·덮개·먼지 사용 기록");
            Check(File.Exists(registeredPaths[0]) && LevelShapeRecommendations.LoadAll().Contains(first), "등록 파일 저장과 목록 조회");
            int count = LevelShapeRecommendations.LoadAll().Count;
            Check(LevelShapeRecommendations.Register(source, out var duplicate)?.Contains("같은 모양") == true && duplicate == first, "같은 모양 중복 등록 거절");
            Check(LevelShapeRecommendations.LoadAll().Count == count, "중복 파일 추가 없음");
            using (SerializedObject edit = new SerializedObject(source))
            {
                edit.FindProperty("obstacles").arraySize = 0; edit.FindProperty("covers").arraySize = 0; edit.FindProperty("dust").arraySize = 0;
                edit.ApplyModifiedPropertiesWithoutUndo();
            }
            AssetDatabase.SaveAssetIfDirty(source);
            Check(LevelShapeRecommendations.Register(source, out duplicate) != null && duplicate == first && first.ObstacleHistory.Count == 3, "장애물이 달라도 중복·기존 기록 유지");
            bool saved = first.Cells[99];
            using (SerializedObject edit = new SerializedObject(source))
            {
                edit.FindProperty("board.cells").GetArrayElementAtIndex(99).FindPropertyRelative("isActive").boolValue = !saved;
                edit.ApplyModifiedPropertiesWithoutUndo();
            }
            AssetDatabase.SaveAssetIfDirty(source);
            Check(first.Cells[99] == saved, "원본 수정과 등록 모양 독립");
            Check(LevelShapeRecommendations.Register(source, out var second) == null, "한 칸 다른 모양 별도 등록");
            registeredPaths.Add(AssetDatabase.GetAssetPath(second));
            Check(second.name != first.name && second.ObstacleHistory.Count == 0, "같은 원본의 다른 모양은 고유 이름·빈 장애물 기록");
            string firstPath = registeredPaths[0], firstName = first.name;
            Resources.UnloadAsset(first);
            first = AssetDatabase.LoadAssetAtPath<LevelShapePreset>(firstPath);
            Check(first != null && first.Cells[99] == saved && first.ObstacleHistory.Count == 3, "디스크 재로딩 후 모양과 사용 기록 유지");

            window = LevelEditorWindow.OpenWorkspace(0, source, true); window.ShowUtility(); window.position = new Rect(20,20,1160,780);
            yield return null;
            typeof(ConnectionGraphVerification).GetField("window", BindingFlags.Static|BindingFlags.NonPublic).SetValue(null, window);
            typeof(ConnectionGraphVerification).GetMethod("RaiseWindow", BindingFlags.Static|BindingFlags.NonPublic).Invoke(null, null);
            Click("show-shape-recommendations"); yield return null;
            Check(window.rootVisualElement.Q<PopupField<string>>("shape-choice").choices.Contains(firstName), "등록 모양 목록 표시");
            Click("register-shape"); yield return null;
            Check(window.rootVisualElement.Q<Label>("shape-status").text.Contains("같은 모양"), "화면에서 중복 등록 안내");
            using (SerializedObject edit = new SerializedObject(source))
            {
                var cell = edit.FindProperty("board.cells").GetArrayElementAtIndex(98).FindPropertyRelative("isActive");
                cell.boolValue = !cell.boolValue; edit.ApplyModifiedPropertiesWithoutUndo();
            }
            AssetDatabase.SaveAssetIfDirty(source);
            Click("register-shape"); yield return null;
            var added = LevelShapeRecommendations.LoadAll().Single(p => !baseline.Contains(AssetDatabase.GetAssetPath(p)) && !registeredPaths.Contains(AssetDatabase.GetAssetPath(p)));
            registeredPaths.Add(AssetDatabase.GetAssetPath(added));
            Check(window.rootVisualElement.Q<Label>("shape-status").text.StartsWith("등록했습니다"), "화면에서 새 모양 등록 성공");
            var choices = window.rootVisualElement.Q<PopupField<string>>("shape-choice"); choices.value = firstName;
            yield return null;
            Check(window.rootVisualElement.Q("shape-candidates").Query<Label>().ToList().Any(l => l.text.Contains("나무상자")), "장애물 이력 화면 표시");
            Capture("catalog.png");
            original = JsonUtility.ToJson(source);
            Click("close-shape-recommendations");
            Check(JsonUtility.ToJson(source) == original, "목록 선택·닫기는 현재 레벨 보존");
            Click("show-shape-recommendations");
            window.rootVisualElement.Q<PopupField<string>>("shape-choice").value = firstName;
            window.rootVisualElement.Q<IntegerField>("shape-level-number").value = 910021; Click("create-shape-level");
            Check(window.rootVisualElement.Q<Label>("shape-status").text.Contains("같은 레벨 번호"), "새 레벨 번호 중복 차단");
            window.rootVisualElement.Q<IntegerField>("shape-level-number").value = 910022;
            window.rootVisualElement.Q<TextField>("shape-file-name").value = "../invalid"; Click("create-shape-level");
            Check(window.CurrentLevel == source, "잘못된 이름 생성 차단");
            string unique = "ShapeTest_" + Guid.NewGuid().ToString("N"); ownedPath = LevelAssetOperations.DefaultFolder + "/" + unique + ".asset";
            window.rootVisualElement.Q<TextField>("shape-file-name").value = unique; Click("create-shape-level"); yield return null;
            var created = window.CurrentLevel;
            Check(created != source && created.Board.Cells.Select(c=>c.IsActive).SequenceEqual(first.Cells), "확정 시 새 레벨에 선택 모양 적용");
            Check(created.Obstacles.Count == 0 && created.Covers.Count == 0 && created.Dust.Count == 0 && created.Missions.Count == 0, "장애물 이력과 미션 자동 배치 없음");
            Check(JsonUtility.ToJson(source) == original, "새 레벨 생성 후 원본 보존");
            List<LevelValidationIssue> issues = new List<LevelValidationIssue>();
            LevelFlowRules.Validate(created, issues); LevelSupplyRules.Validate(created, issues);
            Check(issues.Count == 0, "적용 모양의 기본 공급 정합성");
            Check(created.Board.Cells.Where(c=>c.IsActive).Count() == created.Supply.Sources.Sum(s => Enumerable.Range(s.Coordinate.Row,10-s.Coordinate.Row).TakeWhile(r => first.Cells[r*10+s.Coordinate.Column]).Count()), "모든 활성 칸 아래 중력 공급");
            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(source));
            Check(first.Cells[99] == saved && first.SourceLevelNumber == 910021 && first.ObstacleHistory.Count == 3, "원본 삭제 후 등록 기록 유지");
            window.position = new Rect(20,20,680,480); Click("show-shape-recommendations"); yield return null;
            window.rootVisualElement.Q<PopupField<string>>("shape-choice").value = firstName; yield return null;
            Capture("catalog-narrow.png");
            Check(window.rootVisualElement.Q<Button>("register-shape").worldBound.yMax < 480 && window.rootVisualElement.Q<Button>("create-shape-level").worldBound.yMax <= 480, "좁은 창 등록·적용 버튼 접근");
            Check(baseline.All(p => File.Exists(p)), "기존 사용자 등록 파일 보존");
        }

        private static void Capture(string name)
        {
            Rect r = window.position;
            Texture2D image = new Texture2D((int)r.width,(int)r.height,TextureFormat.RGB24,false);
            image.SetPixels(UnityEditorInternal.InternalEditorUtility.ReadScreenPixel(r.position,(int)r.width,(int)r.height)); image.Apply();
            File.WriteAllBytes(Evidence+"/"+name,image.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(image);
        }

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup < next) return;
            next = EditorApplication.timeSinceStartup + .25;
            Exception failure = null;
            try { if (sequence.MoveNext()) return; }
            catch (Exception error) { failure = error; Results.Add("FAIL " + error); Debug.LogException(error); }
            EditorApplication.update -= Tick;
            if (window != null) window.Close();
            if (ownedPath != null) AssetDatabase.DeleteAsset(ownedPath);
            foreach (string path in registeredPaths) AssetDatabase.DeleteAsset(path);
            AssetDatabase.DeleteAsset(folder);
            File.WriteAllLines(Evidence+"/results.txt", Results);
            EditorApplication.Exit(failure == null ? 0 : 1);
        }
    }
}
