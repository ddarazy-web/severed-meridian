using System.Collections;
using System.Linq;
using System.Reflection;
using Board;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    public static partial class ConnectionGraphVerification
    {
        private static LevelFlowOverlay Flow => window.rootVisualElement.Q<LevelFlowOverlay>();
        public static void StartPaths()
        {
            Evidence = "Logs/PathDragVerification"; testPortals = testPaths = true; Start();
        }
        private static void PathInput(EventType type, int row, int column) => Send(Flow, type, Flow.LocalToWorld(new Vector2(column * 40 + 20, row * 40 + 20)));
        private static void PathReset(string data)
        {
            Flow.CancelInput(); JsonUtility.FromJsonOverwrite(data, level); Undo.ClearUndo(level); Refresh();
            typeof(LevelEditorWindow).GetMethod("SetFlowTool", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, new object[] { FlowTool.Path });
        }
        private static IEnumerator PathUI()
        {
            JsonUtility.FromJsonOverwrite("{\"flow\":{\"gravity\":[],\"paths\":[],\"merges\":[],\"walls\":[],\"portals\":[],\"arrivals\":[]}}", level);
            string baseline = JsonUtility.ToJson(level); PathReset(baseline); yield return null;
            Check(Flow.panel.Pick(Flow.LocalToWorld(new Vector2(60, 60))) == Flow, "경로 도구 실제 포인터 적중");
            PathInput(EventType.MouseDown, 1, 1); yield return null;
            Check(Flow.IsDragging && Flow.PathCandidates.Count == 4 && JsonUtility.ToJson(level) == baseline, "경로 시작/인접 후보/원본 보존");
            PathInput(EventType.MouseDrag, 1, 4); yield return null;
            Check(Flow.DraftCount == 4 && !Flow.PathCandidates.Contains(C(1, 3)), "빠른 드래그 중간 칸 보간/중복 후보 제외");
            PathInput(EventType.MouseDrag, 2, 4); yield return null; Capture("path-preview.png");
            Check(Flow.DraftCount == 5 && level.Flow.Paths.Count == 0, "꺾인 경로 미리보기/확정 전 무변경");
            PathInput(EventType.MouseUp, 2, 4); yield return null;
            Check(level.Flow.Paths.Count == 5 && level.Flow.Paths.Last().IsEnd && level.Flow.Paths[0].Next.Equals(C(1, 2)) && !Flow.IsDragging && Flow.PathCandidates.Count == 0, "놓아서 경로 확정/끝 칸/후보 해제");
            Capture("path-completed.png"); string completed = JsonUtility.ToJson(level);
            Undo.PerformUndo(); yield return null; Check(JsonUtility.ToJson(level) == baseline, "경로 드래그 전체 단일 Undo");
            Undo.PerformRedo(); yield return null; Check(JsonUtility.ToJson(level) == completed, "경로 드래그 Redo");
            PathReset(baseline); yield return null;
            PathInput(EventType.MouseDown, 1, 1); PathInput(EventType.MouseUp, 1, 1); yield return null;
            PathInput(EventType.MouseDown, 1, 2); PathInput(EventType.MouseUp, 1, 2); yield return null;
            Check(Flow.DraftCount == 2 && level.Flow.Paths.Count == 0, "기존 클릭 경로 입력 유지");
            Flow.Complete(); yield return null; Check(level.Flow.Paths.Count == 2, "기존 경로 완료 유지");
            PathReset(baseline); yield return null;
            LevelFlowEditing.SetWalls(level, new[] { new BoardEdge(C(1, 1), C(1, 2)) }, false); Refresh(); yield return null;
            string wall = JsonUtility.ToJson(level);
            PathInput(EventType.MouseDown, 1, 1); yield return null;
            Check(!Flow.PathCandidates.Contains(C(1, 2)), "벽 뒤 칸 후보 제외");
            PathInput(EventType.MouseDrag, 1, 4); PathInput(EventType.MouseUp, 1, 4); yield return null;
            Check(JsonUtility.ToJson(level) == wall && !Flow.IsDragging, "벽 건너뛰기 거절/전체 취소");
            PathReset(baseline); yield return null;
            PathInput(EventType.MouseDown, 1, 1); PathInput(EventType.MouseDrag, 2, 2); PathInput(EventType.MouseUp, 2, 2); yield return null;
            Check(JsonUtility.ToJson(level) == baseline, "모서리 대각선 자동 경로 생성 금지");
            PathInput(EventType.MouseDown, 1, 1); PathInput(EventType.MouseDrag, 1, 3); yield return null;
            using (KeyDownEvent evt = KeyDownEvent.GetPooled('\0', KeyCode.Escape, EventModifiers.None)) { evt.target = Flow; Flow.SendEvent(evt); }
            yield return null; Check(!Flow.IsDragging && Flow.DraftCount == 0 && Flow.PathCandidates.Count == 0 && JsonUtility.ToJson(level) == baseline, "경로 Esc 취소/후보 정리");
            PathInput(EventType.MouseDown, 1, 1);
            Send(Flow, EventType.MouseDrag, Flow.LocalToWorld(new Vector2(-20, 60)));
            PathInput(EventType.MouseDrag, 1, 4); PathInput(EventType.MouseUp, 1, 4); yield return null;
            Check(JsonUtility.ToJson(level) == baseline, "보드 밖 재진입 시 건너뛰기 금지");
            LevelFlowEditing.SetPath(level, new[] { C(1, 4), C(2, 4) }); Refresh(); yield return null;
            var existing = level.Flow.Paths.ToArray();
            PathInput(EventType.MouseDown, 1, 1); PathInput(EventType.MouseDrag, 1, 4); yield return null;
            Check(Flow.PathCandidates.Count == 0, "기존 경로 합류 후 추가 연결 후보 없음");
            PathInput(EventType.MouseUp, 1, 4); yield return null;
            Check(level.Flow.Paths.Count == 5 && level.Flow.Paths[0].Next.Equals(existing[0].Next) && level.Flow.Paths[1].IsEnd, "기존 경로 합류/기존 흐름 보존");
            PathReset(baseline); yield return null;
            LevelFlowEditing.SetPortal(level, C(1, 4), C(6, 6)); Refresh(); yield return null;
            PathInput(EventType.MouseDown, 1, 1); PathInput(EventType.MouseDrag, 1, 4); yield return null;
            Check(Flow.PathCandidates.Count == 0, "통로 입구 합류 후 후보 없음");
            PathInput(EventType.MouseUp, 1, 4); yield return null;
            Check(level.Flow.Paths.Count == 3 && level.Flow.Paths.Last().Next.Equals(C(1, 4)) && level.Flow.Portals[0].HasExit, "통로 입구 합류/통로 보존");
            PathReset(baseline); yield return null;
            PathInput(EventType.MouseDown, 1, 1); PathInput(EventType.MouseDrag, 1, 2);
            JsonUtility.FromJsonOverwrite("{\"moveCount\":22}", level); string changed = JsonUtility.ToJson(level);
            PathInput(EventType.MouseUp, 1, 3); yield return null;
            Check(JsonUtility.ToJson(level) == changed && !Flow.IsDragging && Flow.DraftCount == 0, "원본 변경 시 경로 드래그 취소");
            PathReset(baseline); yield return null;
            PathInput(EventType.MouseDown, 1, 1); PathInput(EventType.MouseDrag, 1, 2);
            typeof(LevelEditorWindow).GetMethod("SetFlowTool", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, new object[] { FlowTool.Select });
            yield return null; Check(!Flow.IsDragging && Flow.DraftCount == 0 && JsonUtility.ToJson(level) == baseline, "도구 전환 시 경로 취소");
        }
    }
}
