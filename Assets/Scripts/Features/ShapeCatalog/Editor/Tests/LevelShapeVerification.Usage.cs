using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    public static partial class LevelShapeVerification
    {
        /// <summary>저장 기준 집계·실제 저장 버튼·미사용 삭제와 재확인을 검사한다. 사용자 에셋은 수정하지 않는다.</summary>
        public static void Usage()
        {
            Directory.CreateDirectory(Evidence); Results.Clear();
            folder = "Assets/__ShapeUsage_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            sequence = UsageSequence(); EditorApplication.update += Tick;
        }

        /// <summary>두 저장 파일의 같은 모양과 미저장 편집을 분리하고 UI 삭제·취소를 검증한다.</summary>
        /// <returns>화면 갱신을 사이에 두고 실행할 검사 단계.</returns>
        private static IEnumerator UsageSequence()
        {
            LevelDefinition source = LevelAssetOperations.CreateAtPath(folder + "/Source.asset");
            var random = new System.Random(Guid.NewGuid().GetHashCode());
            using (SerializedObject edit = new SerializedObject(source))
            {
                for (int i = 0; i < 100; i++) edit.FindProperty("board.cells").GetArrayElementAtIndex(i).FindPropertyRelative("isActive").boolValue = i < 10 || random.Next(2) == 0;
                edit.FindProperty("obstacles").arraySize = 2;
                edit.ApplyModifiedPropertiesWithoutUndo();
            }
            AssetDatabase.SaveAssetIfDirty(source);
            string savedJson = JsonUtility.ToJson(source), disk = File.ReadAllText(AssetDatabase.GetAssetPath(source));
            Check(LevelShapeUsage.Read(Array.Empty<LevelShapePreset>()).Count == 0, "등록 모양이 없으면 빈 집계");
            Check(LevelShapeRecommendations.Register(source, out LevelShapePreset shape) == null, "사용 현황 검사 소유 모양 등록");
            string shapePath = AssetDatabase.GetAssetPath(shape); registeredPaths.Add(shapePath);
            string history = string.Join("/", shape.ObstacleHistory);
            Check(AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source), folder + "/Copy.asset"), "같은 모양의 두 번째 레벨 파일 준비");
            LevelDefinition copy = AssetDatabase.LoadAssetAtPath<LevelDefinition>(folder + "/Copy.asset");
            var uses = LevelShapeUsage.Read(new[] { shape })[shape];
            Check(uses.Count == 2 && uses.Select(entry => entry.Path).Distinct().Count() == 2, "동일 번호여도 별도 저장 파일 두 개 집계");
            Check(uses.All(entry => entry.Obstacles.Any(item => item.Contains("2개"))), "레벨별 저장 장애물 요약");
            using (SerializedObject edit = new SerializedObject(source))
            { edit.FindProperty("obstacles").arraySize = 4; edit.ApplyModifiedPropertiesWithoutUndo(); }
            string unsaved = JsonUtility.ToJson(source);
            uses = LevelShapeUsage.Read(new[] { shape })[shape];
            Check(uses.All(entry => entry.Obstacles.Any(item => item.Contains("2개"))), "미저장 장애물 변경은 집계에서 제외");
            Check(EditorUtility.IsDirty(source) && JsonUtility.ToJson(source) == unsaved && File.ReadAllText(AssetDatabase.GetAssetPath(source)) == disk,
                "디스크 조회 후 원본 dirty·미저장 내용·파일 보존");
            window = LevelEditorWindow.OpenWorkspace(0, source, true); window.ShowUtility(); window.position = new Rect(20, 20, 1160, 850);
            yield return null; Click("show-shape-recommendations");
            window.rootVisualElement.Q<PopupField<string>>("shape-choice").value = shape.name;
            yield return null;
            Check(window.rootVisualElement.Q<Label>("shape-usage-count").text.Contains("2개") && !window.rootVisualElement.Q<Button>("delete-shape").enabledSelf,
                "사용 수 표시와 사용 중 삭제 비활성");
            Click("save-level"); for (int frame = 0; frame < 5; frame++) yield return null;
            Check(window.rootVisualElement.Q("shape-used-levels").Query<Label>().ToList().Any(label => label.text.Contains("4개")), "실제 저장 후 열린 목록의 장애물 갱신");
            Click("save-level"); for (int frame = 0; frame < 3; frame++) yield return null;
            Check(window.rootVisualElement.Q<Label>("shape-usage-count").text.Contains("2개"), "반복 저장은 사용 수를 증가시키지 않음");
            using (SerializedObject edit = new SerializedObject(source))
            { var cell = edit.FindProperty("board.cells").GetArrayElementAtIndex(99).FindPropertyRelative("isActive"); cell.boolValue = !cell.boolValue; edit.ApplyModifiedPropertiesWithoutUndo(); }
            Check(LevelShapeUsage.Read(new[] { shape })[shape].Count == 2, "미저장 모양 변경은 사용 수 보존");
            Click("save-level"); for (int frame = 0; frame < 5; frame++) yield return null;
            Check(window.rootVisualElement.Q<Label>("shape-usage-count").text.Contains("1개"), "다른 모양 저장 시 기존 맵 사용 수 감소");
            Check(LevelShapeUsage.DeleteUnused(shape)?.Contains("사용 중") == true && File.Exists(shapePath), "사용 중 맵 서비스 삭제 거절");
            AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(copy), "RenamedCopy");
            uses = LevelShapeUsage.Read(new[] { shape })[shape];
            Check(uses.Count == 1 && uses[0].Path.EndsWith("RenamedCopy.asset"), "레벨 이름 변경 후 한 번만 집계");
            window.rootVisualElement.Q<Foldout>("shape-used-levels").value = true;
            for (int frame = 0; frame < 4; frame++) yield return null;
            typeof(BotAnalysisVerification).GetField("window", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, window);
            typeof(BotAnalysisVerification).GetMethod("CaptureAnalysis", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { "shape-usage.png" });
            File.Copy("Logs/BotAnalysisVerification/shape-usage.png", "Logs/ShapeUsageVerification/usage.png", true);
            Check(AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(copy)), "검사 소유 레벨 삭제");
            window.ShowShapeRecommendations(); window.rootVisualElement.Q<PopupField<string>>("shape-choice").value = shape.name;
            yield return null;
            Check(window.rootVisualElement.Q<Label>("shape-usage-count").text.Contains("0개") && window.rootVisualElement.Q<Button>("delete-shape").enabledSelf,
                "마지막 사용 레벨 삭제 시 미사용 맵 삭제 허용");
            Click("delete-shape"); Click("cancel-delete-shape");
            Check(File.Exists(shapePath) && window.rootVisualElement.Q("shape-delete-prompt").style.display == DisplayStyle.None, "등록 맵 삭제 취소 보존");
            Click("delete-shape");
            LevelDefinition late = LevelAssetOperations.CreateAtPath(folder + "/Late.asset");
            JsonUtility.FromJsonOverwrite(savedJson, late); EditorUtility.SetDirty(late); AssetDatabase.SaveAssetIfDirty(late);
            Click("confirm-delete-shape"); yield return null;
            Check(File.Exists(shapePath) && window.rootVisualElement.Q<Label>("shape-status").text.Contains("사용 중"), "확인 중 새 사용 레벨이 생기면 삭제 직전 재검사");
            Check(string.Join("/", shape.ObstacleHistory) == history, "현재 사용 현황 변경에도 등록 당시 기록 보존");
            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(late));
            window.ShowShapeRecommendations(); window.rootVisualElement.Q<PopupField<string>>("shape-choice").value = shape.name;
            Click("delete-shape"); Click("confirm-delete-shape"); yield return null;
            Check(!File.Exists(shapePath) && !File.Exists(shapePath + ".meta") && File.Exists(AssetDatabase.GetAssetPath(source)), "미사용 등록 맵과 meta만 휴지통 이동·레벨 보존");
            registeredPaths.Remove(shapePath);
        }
    }
}
