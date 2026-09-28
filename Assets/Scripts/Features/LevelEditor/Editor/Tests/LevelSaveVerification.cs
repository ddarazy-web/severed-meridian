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
    /// <summary>전용 Unity 프로세스에서 저장 키, 지연 입력, dirty 표시와 저장 범위를 검증한다.</summary>
    public static class LevelSaveVerification
    {
        private static readonly List<string> Results = new List<string>();
        private static LevelEditorWindow window;
        private static string folder;
        private static IEnumerator sequence;
        private static double next;

        /// <summary>임시 에셋만 사용하는 UI 검증을 시작한다. 완료 후 검증용 에디터를 종료한다.</summary>
        public static void Start()
        {
            folder = "Assets/__LevelSaveVerification_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            window = LevelEditorWindow.OpenWorkspace(0);
            window.position = new Rect(20, 20, 1160, 780);
            window.Focus();
            sequence = Run();
            EditorApplication.update += Tick;
        }

        /// <summary>관찰 결과를 기록하고 실패하면 해당 검증을 중단한다.</summary>
        /// <param name="pass">성공 여부.</param><param name="label">검증 항목.</param>
        private static void Check(bool pass, string label)
        {
            if (!pass) throw new InvalidOperationException(label);
            Results.Add("PASS " + label);
        }

        /// <summary>실제 UI 이벤트 전달 경로로 저장 키를 보낸다.</summary>
        /// <param name="target">키를 받는 화면 요소.</param><param name="modifiers">보조 키.</param>
        private static void SendSave(VisualElement target, EventModifiers modifiers = EventModifiers.Control)
        {
            using KeyDownEvent evt = KeyDownEvent.GetPooled(new Event { type = EventType.KeyDown, keyCode = KeyCode.S, modifiers = modifiers });
            evt.target = target;
            target.SendEvent(evt);
        }

        /// <summary>프레임을 넘겨 바인딩과 dirty 표시 갱신까지 확인한다.</summary>
        /// <returns>Unity 갱신마다 진행할 검증 단계.</returns>
        private static IEnumerator Run()
        {
            yield return null;
            typeof(ConnectionGraphVerification).GetField("window", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, window);
            typeof(ConnectionGraphVerification).GetMethod("RaiseWindow", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            yield return null;
            LevelDefinition level = LevelAssetOperations.CreateAtPath(folder + "/Current.asset");
            LevelDefinition other = LevelAssetOperations.CreateAtPath(folder + "/Other.asset");
            window.SetLevel(level);
            yield return null;
            Check(window.titleContent.text == "Match", "저장된 레벨의 제목");
            Undo.RecordObject(level, "저장 검증");
            JsonUtility.FromJsonOverwrite("{\"moveCount\":37}", level);
            EditorUtility.SetDirty(level);
            Undo.FlushUndoRecordObjects();
            EditorUtility.SetDirty(other);
            yield return null;
            Check(window.titleContent.text == "Match *" && window.rootVisualElement.Q<Label>("asset-state").text.Contains("저장 안 됨"), "변경된 레벨 dirty 표시");
            SendSave(window.rootVisualElement);
            yield return null;
            Check(!EditorUtility.IsDirty(level) && File.ReadAllText(AssetDatabase.GetAssetPath(level)).Contains("moveCount: 37"), "Ctrl+S 디스크 저장");
            Check(EditorUtility.IsDirty(other), "다른 에셋은 저장하지 않음");
            Check(window.titleContent.text == "Match", "저장 직후 dirty 해제");
            Undo.PerformUndo();
            yield return null;
            Check(window.titleContent.text == "Match *", "Undo 변경도 dirty 표시");
            AssetDatabase.SaveAssetIfDirty(level);
            for (int i = 0; i < 6 && window.titleContent.text != "Match"; i++) yield return null;
            Check(window.titleContent.text == "Match", "외부 저장 후 표시 갱신 (dirty=" + EditorUtility.IsDirty(level) + ", title=" + window.titleContent.text + ")");
            // 실제 바인딩된 지연 입력의 텍스트를 수정하되 value는 아직 확정하지 않는다.
            using (NavigationSubmitEvent click = NavigationSubmitEvent.GetPooled())
            {
                Button tab = window.rootVisualElement.Q<Button>("inspector-tab-1");
                click.target = tab;
                tab.SendEvent(click);
            }
            yield return null;
            typeof(ConnectionGraphVerification).GetField("window", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, window);
            typeof(ConnectionGraphVerification).GetMethod("RaiseWindow", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            yield return null;
            IntegerField moves = window.rootVisualElement.Query<IntegerField>().ToList().First(field => field.bindingPath == "moveCount");
            Check(moves.enabledInHierarchy && moves.resolvedStyle.visibility == Visibility.Visible && moves.worldBound.height > 0, "숫자 입력란 표시·활성");
            moves.isDelayed = true;
            moves.Focus();
            yield return null;
            moves.SelectAll();
            Check(window.rootVisualElement.focusController.focusedElement != null, "숫자 입력에 포커스");
            VisualElement input = window.rootVisualElement.focusController.focusedElement as VisualElement;
            // 합성 문자 키는 운영체제 텍스트 입력을 재현하지 못하므로 텍스트 버퍼만 설정한다.
            // value와 에셋은 건드리지 않고 실제 포커스 해제·저장 키 경로를 검증한다.
            typeof(TextInputBaseField<int>).GetProperty("text").GetSetMethod(true).Invoke(moves, new object[] { "43" });
            Check(moves.text == "43" && level.MoveCount != 43, "지연 입력은 아직 데이터에 반영되지 않음 (text=" + moves.text + ", value=" + level.MoveCount + ")");
            SendSave(input);
            yield return null;
            Check(level.MoveCount == 43 && File.ReadAllText(AssetDatabase.GetAssetPath(level)).Contains("moveCount: 43"), "숫자 입력 중 Ctrl+S는 입력값도 저장");
            EditorUtility.SetDirty(level);
            SendSave(window.rootVisualElement, EventModifiers.Control | EventModifiers.Shift);
            Check(EditorUtility.IsDirty(level), "Ctrl+Shift+S는 가로채지 않음");
            window.SelectWorkspaceTab(1);
            SendSave(window.rootVisualElement);
            Check(EditorUtility.IsDirty(level), "플레이 테스트 탭에서 편집 저장 처리 제외");
            window.SelectWorkspaceTab(0);
            yield return null;
            using (NavigationSubmitEvent click = NavigationSubmitEvent.GetPooled())
            {
                Button save = window.rootVisualElement.Q<Button>("save-level");
                click.target = save;
                save.SendEvent(click);
            }
            yield return null;
            Check(!EditorUtility.IsDirty(level), "기존 저장 버튼 유지");
            window.SetLevel(null);
            SendSave(window.rootVisualElement);
            Check(window.titleContent.text == "Match", "레벨 미선택 시 안전한 처리");
        }

        /// <summary>UI 갱신을 기다리며 검증하고, 성공·실패 모두 소유 임시 데이터만 정리한다.</summary>
        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup < next) return;
            next = EditorApplication.timeSinceStartup + 0.5;
            Exception failure = null;
            try { if (sequence.MoveNext()) return; }
            catch (Exception error) { failure = error; Results.Add("FAIL " + error); Debug.LogException(error); }
            EditorApplication.update -= Tick;
            if (window != null) window.Close();
            AssetDatabase.DeleteAsset(folder);
            Directory.CreateDirectory("Logs/LevelSaveVerification");
            File.WriteAllLines("Logs/LevelSaveVerification/results.txt", Results);
            EditorApplication.Exit(failure == null ? 0 : 1);
        }
    }
}
