using System.Collections;
using System.Reflection;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    public static partial class ConnectionGraphVerification
    {
        public static void StartPortals()
        {
            Evidence = "Logs/PortalGraphVerification"; testPortals = true; Start();
        }
        private static void PortalDown(string name)
        {
            VisualElement port = Graph.Q(name);
            Check(port != null && Graph.panel.Pick(port.worldBound.center) == port, "통로 연결점 실제 적중 " + name);
            Send(port, EventType.MouseDown, port.worldBound.center);
        }
        private static Vector2 PortalPoint(int row, int column) => Graph.LocalToWorld(new Vector2(column * 40 + 20, row * 40 + 20));
        private static IEnumerator PortalUI()
        {
            Check(LevelFlowEditing.SetPortal(level, C(1, 1), null) == null, "기존 버튼/API 미연결 입구 생성");
            Refresh(); Select(C(1, 1)); yield return null;
            typeof(LevelEditorWindow).GetMethod("SetFlowTool", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, new object[] { FlowTool.Select });
            yield return null;
            Check(window.rootVisualElement.Q<LevelFlowOverlay>().Tool == FlowTool.Select && Graph.Q("portal-entrance-1-1") != null, "흐름 선택 도구와 연결점 함께 표시");
            string before = JsonUtility.ToJson(level);
            Check(LevelFlowEditing.PortalError(level, C(1, 1), C(2, 7)) == null && JsonUtility.ToJson(level) == before, "통로 미리보기 검증 원본 불변");
            PortalDown("portal-entrance-1-1"); yield return null;
            Check(Graph.Query<VisualElement>().ToList().Count(e => e.name?.StartsWith("portal-candidate-") == true) == 99 && Graph.Q("portal-candidate-1-1") == null,
                "입구 드래그 시작 시 연결 가능 칸 전체 강조/자기 칸 제외");
            Send(Graph, EventType.MouseDrag, PortalPoint(2, 7)); yield return null; Capture("portal-forward-preview.png");
            Check(Graph.Q("portal-candidate-2-7").resolvedStyle.borderTopColor == Color.yellow && Graph.Q("portal-candidate-2-7").pickingMode == PickingMode.Ignore,
                "가리킨 후보 노란색 강조/입력 가로채지 않음");
            Check(Graph.Q("portal-exit-2-7") != null && JsonUtility.ToJson(level) == before, "출구 후보 표시/드래그 중 원본 불변");
            Send(Graph, EventType.MouseUp, PortalPoint(2, 7)); yield return null;
            Check(level.Flow.Portals[0].HasExit && level.Flow.Portals[0].Exit.Equals(C(2, 7)), "통로 입구→출구 드래그 저장");
            Check(Graph.Q("portal-candidate-2-7") == null, "연결 확정 후 후보 강조 해제");
            Check(Graph.Q("portal-entrance-1-1").resolvedStyle.backgroundColor.g > 0.8f && Graph.Q("portal-exit-2-7").resolvedStyle.backgroundColor.g > 0.8f, "통로 양쪽 점등");
            Capture("portal-connected.png"); string connected = JsonUtility.ToJson(level);
            Undo.PerformUndo(); yield return null; Refresh(); Check(JsonUtility.ToJson(level) == before, "통로 연결 한 번 Undo");
            Undo.PerformRedo(); yield return null; Refresh(); Check(JsonUtility.ToJson(level) == connected, "통로 연결 Redo");
            DropdownMenuAction stale = LevelContextMenuVerification.Action(window, "연결 해제", Graph.Q("portal-exit-2-7"));
            LevelContextMenuVerification.Action(window, "연결 해제", Graph.Q("portal-line-1-1")).Execute(); yield return null;
            Check(level.Flow.Portals.Count == 1 && !level.Flow.Portals[0].HasExit, "통로 선 우클릭 해제/입구 보존");
            Undo.PerformUndo(); yield return null; Refresh(); Check(JsonUtility.ToJson(level) == connected, "통로 해제 Undo");
            LevelContextMenuVerification.Action(window, "연결 해제", Graph.Q("portal-exit-2-7")).Execute(); yield return null;
            Check(!level.Flow.Portals[0].HasExit, "통로 출구 점 우클릭 해제");
            string unconnected = JsonUtility.ToJson(level); stale.Execute(); yield return null;
            Check(JsonUtility.ToJson(level) == unconnected, "통로 이전 메뉴 재실행 원본 보존");
            Check(LevelFlowEditing.SetPortal(level, C(1, 1), C(2, 6)) == null && level.Flow.Portals[0].Exit.Equals(C(2, 6)), "기존 수동 출구 지정 유지");
            LevelFlowEditing.SetPortal(level, C(1, 1), null); Refresh(); yield return null;
            Select(C(2, 7)); yield return null; PortalDown("portal-exit-2-7"); yield return null;
            Check(Graph.Query<VisualElement>().ToList().Count(e => e.name?.StartsWith("portal-candidate-") == true) == 1 && Graph.Q("portal-candidate-1-1") != null,
                "출구 드래그 시 연결 가능한 입구만 강조");
            Send(Graph, EventType.MouseDrag, PortalPoint(1, 1)); yield return null; Capture("portal-reverse-preview.png");
            Send(Graph, EventType.MouseUp, PortalPoint(1, 1)); yield return null;
            Check(JsonUtility.ToJson(level) == connected, "출구 후보→입구 드래그/실제 이동 방향 보존");
            PortalDown("portal-exit-2-7"); yield return null;
            Check(!Graph.IsDragging && JsonUtility.ToJson(level) == connected, "점유된 통로 드래그 거절");
            LevelContextMenuVerification.Action(window, "연결 해제", Graph.Q("portal-exit-2-7")).Execute(); yield return null;
            Select(C(1, 1)); yield return null;
            foreach (var destination in new[] { C(1, 1), C(3, 3), C(7, 7) })
            {
                if (destination.Equals(C(3, 3))) LevelFlowEditing.SetArrival(level, destination, false);
                if (destination.Equals(C(7, 7))) LevelFlowEditing.SetPortal(level, destination, null);
                Refresh(); yield return null; string invalidBefore = JsonUtility.ToJson(level);
                PortalDown("portal-entrance-1-1"); yield return null;
                Check(Graph.Q("portal-candidate-" + destination.Row + "-" + destination.Column) == null, "연결 불가 칸 강조 제외 " + destination);
                Send(Graph, EventType.MouseUp, PortalPoint(destination.Row, destination.Column)); yield return null;
                Check(JsonUtility.ToJson(level) == invalidBefore, "자기 칸/도착 바닥/다른 입구 거절 " + destination);
            }
            before = JsonUtility.ToJson(level); PortalDown("portal-entrance-1-1"); yield return null;
            Send(Graph, EventType.MouseUp, Graph.LocalToWorld(new Vector2(-20, 20))); yield return null;
            Check(!Graph.IsDragging && JsonUtility.ToJson(level) == before, "통로 보드 밖 드롭 취소");
            PortalDown("portal-entrance-1-1"); yield return null;
            using (KeyDownEvent evt = KeyDownEvent.GetPooled('\0', KeyCode.Escape, EventModifiers.None)) { evt.target = Graph; Graph.SendEvent(evt); }
            yield return null; Check(!Graph.IsDragging && JsonUtility.ToJson(level) == before, "통로 Esc 취소");
            Check(!Graph.Query<VisualElement>().ToList().Any(e => e.name?.StartsWith("portal-candidate-") == true), "취소 시 후보 강조 모두 해제");
            PortalDown("portal-entrance-1-1"); yield return null; Select(C(0, 0)); yield return null;
            Check(!Graph.IsDragging && JsonUtility.ToJson(level) == before, "통로 선택 변경 취소");
            Select(C(1, 1)); yield return null;
            PortalDown("portal-entrance-1-1"); yield return null;
            JsonUtility.FromJsonOverwrite("{\"moveCount\":21}", level); string externallyChanged = JsonUtility.ToJson(level);
            Send(Graph, EventType.MouseUp, PortalPoint(2, 7)); yield return null;
            Check(!Graph.IsDragging && JsonUtility.ToJson(level) == externallyChanged, "통로 드래그 중 원본 변경 취소");
            PortalDown("portal-entrance-1-1"); yield return null; Graph.Display(level, C(1, 1), false); yield return null;
            Check(!Graph.IsDragging && Graph.childCount == 0, "통로 칠하기 전환 시 입력/연결점 정리");
            Refresh(); yield return null; PortalDown("portal-entrance-1-1"); yield return null;
            window.SetLevel(null); yield return null; Check(!Graph.IsDragging && Graph.childCount == 0, "통로 레벨 변경 시 임시 상태 해제");
        }
    }
}
