using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    public static partial class ConnectionGraphVerification
    {
        public static void StartMerges()
        {
            Evidence = "Logs/MergeEditingVerification"; testPortals = testPaths = testMerges = true; Start();
        }
        private static IEnumerator MergeUI()
        {
            JsonUtility.FromJsonOverwrite("{\"flow\":{\"gravity\":[],\"paths\":[],\"merges\":[],\"walls\":[],\"portals\":[],\"arrivals\":[]}}", level);
            LevelFlowEditing.SetGravity(level, new[] { C(3, 2) }, GravityDirection.Right);
            LevelFlowEditing.SetGravity(level, new[] { C(3, 4) }, GravityDirection.Left);
            LevelFlowEditing.SetPortal(level, C(0, 0), C(3, 3));
            var initial = LevelFlowRules.Sources(level, C(3, 3)).ToArray();
            Check(initial.Length == 4, "합류 검증 일반/통로 유입 4개");
            LevelFlowEditing.SetMerge(level, C(3, 3), initial); Undo.ClearUndo(level); Refresh();
            typeof(LevelEditorWindow).GetMethod("SetFlowTool", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, new object[] { FlowTool.Select });
            Select(C(3, 3)); yield return null;
            Check(Flow.MergeSources.SequenceEqual(initial) && Flow.Query<Label>().ToList().Count(e => e.name?.StartsWith("merge-rank-") == true) == 4, "합류 칸 선택 시 유입 4개 순위 표시");
            Capture("merge-initial.png");
            string before = JsonUtility.ToJson(level);
            ListView list = window.rootVisualElement.Q<ListView>("merge-order");
            DropdownMenuAction reset = LevelContextMenuVerification.Action(window, "합류 우선순위 초기화", list);
            Check(JsonUtility.ToJson(level) == before, "목록 우클릭 메뉴 열기만으로 초기화되지 않음");
            reset.Execute(); yield return null;
            Check(level.Flow.Merges.Count == 0 && Flow.MergeSources.SequenceEqual(initial), "메뉴 초기화 선택 시 기본 순서 복원");
            Undo.PerformUndo(); yield return null; Check(JsonUtility.ToJson(level) == before, "합류 초기화 단일 Undo");
            var boardView = window.rootVisualElement.Q<LevelBoardView>();
            reset = LevelContextMenuVerification.Action(window, "바닥·연결/합류 우선순위 초기화", boardView, boardView.LocalToWorld(new Vector2(140, 140)));
            Check(JsonUtility.ToJson(level) == before, "보드 우클릭 메뉴 열기만으로 원본 보존");
            reset.Execute(); yield return null; Check(level.Flow.Merges.Count == 0, "보드 컨텍스트 메뉴 초기화 실행");
            Undo.PerformUndo(); yield return null;
            list = window.rootVisualElement.Q<ListView>("merge-order");
            Vector2 start = new Vector2(list.worldBound.xMin + 9, list.worldBound.yMin + 14);
            VisualElement handle = window.rootVisualElement.panel.Pick(start);
            Send(handle, EventType.MouseDown, start); yield return null;
            Send(handle, EventType.MouseDrag, start + new Vector2(0, 10)); yield return null;
            Send(handle, EventType.MouseDrag, start + new Vector2(0, 42)); yield return null; yield return null;
            Check(JsonUtility.ToJson(level) == before, "목록 드래그 중 원본 불변");
            Send(handle, EventType.MouseUp, start + new Vector2(0, 42)); yield return null; yield return null;
            Check(level.Flow.Merges[0].Sources[1].Equals(initial[0]) && level.Flow.Merges[0].Sources[0].Equals(initial[1]), "실제 핸들 드래그로 우선순위 재정렬");
            Check(Flow.MergeSources.SequenceEqual(level.Flow.Merges[0].Sources) && Flow.Q<Label>("merge-rank-0-0").text == "2", "드롭 후 보드 순위 즉시 동기화");
            Capture("merge-reordered.png"); string reordered = JsonUtility.ToJson(level);
            Undo.PerformUndo(); yield return null; Check(JsonUtility.ToJson(level) == before && Flow.MergeSources.SequenceEqual(initial), "재정렬 단일 Undo/보드 순위 복원");
            Undo.PerformRedo(); yield return null; Check(JsonUtility.ToJson(level) == reordered, "재정렬 Redo");
            list = window.rootVisualElement.Q<ListView>("merge-order");
            start = new Vector2(list.worldBound.xMin + 9, list.worldBound.yMin + 14); handle = window.rootVisualElement.panel.Pick(start);
            Send(handle, EventType.MouseDown, start); yield return null;
            Send(handle, EventType.MouseDrag, start + new Vector2(0, 42)); yield return null;
            using (KeyDownEvent evt = KeyDownEvent.GetPooled('\0', KeyCode.Escape, EventModifiers.None)) { evt.target = list; list.SendEvent(evt); }
            yield return null; Send(handle, EventType.MouseUp, start + new Vector2(0, 42)); yield return null; yield return null;
            Check(JsonUtility.ToJson(level) == reordered, "합류 목록 드래그 Esc 취소");
            list = window.rootVisualElement.Q<ListView>("merge-order");
            reset = LevelContextMenuVerification.Action(window, "합류 우선순위 초기화", list);
            Select(C(8, 8)); yield return null; reset.Execute(); yield return null;
            Check(JsonUtility.ToJson(level) == reordered && Flow.MergeSources.Count == 0, "선택 변경 시 표시 해제/이전 초기화 메뉴 거절");
            Select(C(3, 3)); yield return null;
            typeof(LevelEditorWindow).GetMethod("SetFlowTool", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, new object[] { FlowTool.Path });
            yield return null; Check(Flow.MergeSources.Count == 0, "그리기 도구에서는 합류 강조 숨김");
        }
    }
}
