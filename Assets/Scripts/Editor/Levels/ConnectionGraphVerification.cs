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
    public static partial class ConnectionGraphVerification
    {
        private static string Evidence = "Logs/ConnectionGraphVerification";
        private static bool testPortals;
        private static bool testPaths;
        private static bool testMerges;
        private static readonly List<string> Results = new List<string>();
        private static LevelDefinition level;
        private static LevelEditorWindow window;
        private static IEnumerator sequence;
        private static double next;
        private static BoardCoordinate C(int r, int c) => new BoardCoordinate(r, c);
        private static void Check(bool pass, string name) { if (!pass) throw new InvalidOperationException(name); Results.Add("PASS " + name); }
        private static LevelConnectionGraph Graph => window.rootVisualElement.Q<LevelConnectionGraph>();
        private static void Select(BoardCoordinate cell) => typeof(LevelEditorWindow).GetMethod("SelectCell", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, new object[] { cell });
        private static void Refresh() => typeof(LevelEditorWindow).GetMethod("Refresh", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, null);
        private static void ClickPort(int body, int slot)
        {
            VisualElement port = Graph.Q("connection-port-" + body + "-" + slot);
            if (port == null) throw new InvalidOperationException("연결점 없음");
            if (Graph.panel.Pick(port.worldBound.center) != port) throw new InvalidOperationException("연결점 포인터 적중 실패");
            Send(port, EventType.MouseDown, port.worldBound.center);
        }
        private static void Send(VisualElement element, EventType type, Vector2 world)
        {
            Event input = new Event { type = type, button = 0, mousePosition = world };
            if (type == EventType.MouseDown) { using PointerDownEvent evt = PointerDownEvent.GetPooled(input); evt.target = element; element.SendEvent(evt); }
            else if (type == EventType.MouseUp) { using PointerUpEvent evt = PointerUpEvent.GetPooled(input); evt.target = element; element.SendEvent(evt); }
            else { using PointerMoveEvent evt = PointerMoveEvent.GetPooled(input); evt.target = element; element.SendEvent(evt); }
        }
        public static void Start()
        {
            Directory.CreateDirectory(Evidence);
            try
            {
                level = (LevelDefinition)typeof(GeneratorVerification).GetMethod("Make", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { ObstacleKind.Appliance, 3 });
                LevelConnectionEditing.Remove(level, 0);
                Data();
                window = ScriptableObject.CreateInstance<LevelEditorWindow>(); window.position = new Rect(20, 20, 1160, 780); window.ShowUtility(); window.SetLevel(level); window.Focus();
                sequence = UI(); EditorApplication.update += Tick;
            }
            catch (Exception error) { Finish(error); }
        }
        private static void Data()
        {
            string before = JsonUtility.ToJson(level), generator = level.Obstacles[0].Id, target = level.Obstacles[1].Id;
            Check(LevelConnectionEditing.FindWire(level, generator, target, C(5, 6), out List<BoardCoordinate> path) == null && path.Count > 1 &&
                LevelConnectionRules.WireError(level, generator, target, path) == null && JsonUtility.ToJson(level) == before, "유효 경로 조회/원본 무변경");
            LevelFlowEditing.SetWalls(level, new[] { new BoardEdge(C(4, 6), C(5, 6)) }, false);
            Check(LevelConnectionEditing.FindWire(level, generator, target, C(5, 6), out path) == null &&
                LevelConnectionRules.WireError(level, generator, target, path) == null && !LevelFlowRules.Segments(path).Contains(new BoardEdge(C(5, 6), C(5, 7))), "벽 선분 우회");
            JsonUtility.FromJsonOverwrite(before, level);
            Check(LevelConnectionEditing.ConnectAuto(level, generator, target, C(5, 6)) == null && level.Connections.Count == 1, "연결/전선 원자적 생성");
            string connected = JsonUtility.ToJson(level);
            Check(LevelConnectionEditing.ConnectAuto(level, generator, target, C(4, 5)) != null && JsonUtility.ToJson(level) == connected, "중복 대상 거절/변경 없음");
            Undo.PerformUndo(); Check(level.Connections.Count == 0, "단일 Undo 연결과 전선 동시 취소");
            Undo.PerformRedo(); Check(JsonUtility.ToJson(level) == connected, "Redo 경로 복원");
            LevelConnectionEditing.Remove(level, 0);
            LevelFlowEditing.SetWalls(level, new[] { new BoardEdge(C(4, 5), C(4, 6)), new BoardEdge(C(5, 5), C(5, 6)), new BoardEdge(C(4, 6), C(5, 6)) }, false);
            string blocked = JsonUtility.ToJson(level);
            Check(LevelConnectionEditing.ConnectAuto(level, generator, target, C(5, 6)) != null && JsonUtility.ToJson(level) == blocked, "진입로 없는 연결점 거절/반쪽 연결 없음");
            JsonUtility.FromJsonOverwrite(before, level); Undo.ClearUndo(level);
            Check(LevelConnectionEditing.Add(level, generator, target) == null && level.Connections[0].Vertices.Count == 0, "기존 수동 대상 추가 유지");
            var manual = new[] { C(4, 6), C(4, 7), C(4, 8) };
            Check(LevelConnectionEditing.SetWire(level, 0, manual) == null && level.Connections[0].Vertices.SequenceEqual(manual), "기존 수동 전선 지정 유지");
            Check(LevelConnectionEditing.Remove(level, 0) == null && level.Connections.Count == 0, "기존 수동 연결 삭제 유지");
            Undo.PerformUndo(); Check(level.Connections.Count == 1 && level.Connections[0].Vertices.SequenceEqual(manual), "수동 연결 Undo 복원");
            JsonUtility.FromJsonOverwrite(before, level); Undo.ClearUndo(level);
            foreach (BoardCoordinate cell in new[] { C(0, 0), C(8, 0), C(8, 8) })
            {
                LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Block, Erase = true }, new[] { cell });
                LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)ObstacleKind.Crate, Durability = 1 }, new[] { cell });
            }
            Check(LevelConnectionEditing.ConnectAuto(level, generator, target, C(5, 6)) == null &&
                LevelConnectionEditing.ConnectAuto(level, generator, level.Obstacles[2].Id, C(4, 5)) == null &&
                LevelConnectionEditing.ConnectAuto(level, generator, level.Obstacles[3].Id, C(6, 5)) == null, "세 연결점 독립 자동 경로");
            Check(level.Connections.SelectMany(c => c.Vertices).Distinct().Count() == level.Connections.Sum(c => c.Vertices.Count), "기존 전선의 꼭짓점/단자/선분 공유 없음");
            string full = JsonUtility.ToJson(level);
            Check(LevelConnectionEditing.ConnectAuto(level, generator, level.Obstacles[4].Id, C(4, 5)) != null && JsonUtility.ToJson(level) == full, "최대3개 초과 거절/원본 보존");
            JsonUtility.FromJsonOverwrite(before, level); Undo.ClearUndo(level);
        }
        private static IEnumerator UI()
        {
            yield return null; RaiseWindow(); yield return null; Select(C(4, 4)); yield return null;
            Check(Graph.Query<VisualElement>().ToList().Count(e => e.name?.StartsWith("connection-port-0-") == true) == 3, "발전기 선택 연결점3개");
            string initial = JsonUtility.ToJson(level);
            LevelFlowEditing.SetWalls(level, new[] { new BoardEdge(C(4, 5), C(4, 6)), new BoardEdge(C(5, 5), C(5, 6)), new BoardEdge(C(4, 6), C(5, 6)) }, false);
            Refresh(); yield return null; ClickPort(0, 1); yield return null;
            Check(Graph.Q("connection-candidate-1") == null, "전선 경로 없는 장애물 후보 강조 제외");
            Graph.Cancel(); JsonUtility.FromJsonOverwrite(initial, level); Undo.ClearUndo(level); Refresh(); yield return null;
            ClickPort(0, 1); yield return null;
            Check(Graph.IsDragging && Graph.Q("connection-port-1-0") != null, "드래그 시작/상대 연결점 표시");
            Check(Graph.Q("connection-candidate-1") != null && Graph.Q("connection-candidate-0") == null && Graph.Q("connection-candidate-1").resolvedStyle.width == 76,
                "발전기 드래그 시 유효 장애물 본체 전체 강조");
            Vector2 end = Graph.Q("connection-port-1-0").worldBound.center;
            Send(Graph, EventType.MouseDrag, end); yield return null; Capture("forward-preview.png");
            Check(Graph.Q("connection-candidate-1").resolvedStyle.borderTopColor == Color.yellow && Graph.Q("connection-candidate-1").pickingMode == PickingMode.Ignore,
                "발전기 대상 노란색 강조/포인터 입력 보존");
            Send(Graph, EventType.MouseUp, end); yield return null;
            Check(level.Connections.Count == 1 && !Graph.IsDragging && level.Connections[0].Vertices[0].Equals(C(5, 6)), "실제 포인터 발전기→장애물/선택 슬롯 보존");
            Check(level.Connections[0].Vertices.Last().Equals(C(6, 10)), "선택한 장애물 연결점에 전선 끝점 일치");
            Check(Graph.Q("connection-port-0-1").resolvedStyle.backgroundColor.g > 0.8f, "연결점 점등"); Capture("connected.png");
            Check(Graph.Q("connection-candidate-1") == null, "발전기 연결 완료 시 후보 강조 해제");
            ClickPort(0, 0); yield return null;
            Check(Graph.Q("connection-candidate-1") == null, "이미 연결된 장애물 후보 강조 제외");
            Graph.Cancel(); yield return null;
            ClickPort(0, 1); yield return null; Check(!Graph.IsDragging && level.Connections.Count == 1, "이미 연결된 점 드래그 거절");
            DropdownMenuAction stale = LevelContextMenuVerification.Action(window, "연결 해제", Graph.Q("connection-port-0-1"));
            LevelContextMenuVerification.Action(window, "연결 해제", Graph.Q("connection-line-0")).Execute(); yield return null;
            Check(level.Connections.Count == 0, "선 우클릭 연결 해제");
            Undo.PerformUndo(); yield return null; Refresh(); Check(level.Connections.Count == 1, "연결 해제 Undo 복원");
            JsonUtility.FromJsonOverwrite("{\"moveCount\":21}", level); Refresh(); string changed = JsonUtility.ToJson(level);
            stale.Execute(); yield return null; Check(JsonUtility.ToJson(level) == changed, "이전 컨텍스트 메뉴의 잘못된 연결 삭제 방지");
            JsonUtility.FromJsonOverwrite("{\"moveCount\":20}", level); Refresh();
            LevelContextMenuVerification.Action(window, "연결 해제", Graph.Q("connection-port-0-1")).Execute(); yield return null;
            Check(level.Connections.Count == 0, "연결점 우클릭 해제"); Undo.PerformUndo(); yield return null; Refresh();
            Undo.PerformUndo(); yield return null; Refresh(); Select(C(4, 8)); yield return null;
            ClickPort(1, 0); yield return null; end = Graph.Q("connection-port-0-2").worldBound.center;
            Check(Graph.Q("connection-candidate-0") != null && Graph.Q("connection-candidate-1") == null, "장애물에서 드래그 시 연결 가능 발전기 강조");
            Send(Graph, EventType.MouseDrag, end); yield return null; Capture("reverse-preview.png");
            Send(Graph, EventType.MouseUp, end); yield return null;
            Check(level.Connections.Count == 1 && level.Connections[0].Vertices[0].Equals(C(6, 5)), "실제 포인터 장애물→발전기");
            Check(level.Connections[0].Vertices.Last().Equals(C(6, 10)), "역방향 드래그 장애물 연결점 유지");
            Undo.PerformUndo(); yield return null; Refresh(); Select(C(4, 4)); yield return null;
            string before = JsonUtility.ToJson(level); ClickPort(0, 0); yield return null;
            Send(Graph, EventType.MouseUp, Graph.LocalToWorld(new Vector2(20, 20))); yield return null;
            Check(!Graph.IsDragging && JsonUtility.ToJson(level) == before, "빈 공간 드롭 취소");
            ClickPort(0, 0); yield return null;
            using (KeyDownEvent evt = KeyDownEvent.GetPooled('\0', KeyCode.Escape, EventModifiers.None)) { evt.target = Graph; Graph.SendEvent(evt); }
            Check(!Graph.IsDragging && JsonUtility.ToJson(level) == before, "Esc 취소/원본 보존");
            Check(!Graph.Query<VisualElement>().ToList().Any(e => e.name?.StartsWith("connection-candidate-") == true), "발전기 취소 시 후보 강조 해제");
            yield return null;
            ClickPort(0, 0); yield return null; Graph.Display(level, C(4, 4), false); yield return null;
            Check(!Graph.IsDragging && Graph.childCount == 0 && JsonUtility.ToJson(level) == before, "칠하기 모드 연결점 숨김/드래그 취소");
            Refresh(); yield return null;
            ClickPort(0, 0); yield return null; window.SetLevel(null); yield return null;
            Check(!Graph.IsDragging && Graph.childCount == 0, "레벨 변경 캡처/임시 선 해제");
            if (testPortals)
            {
                window.SetLevel(level); yield return null;
                IEnumerator portals = PortalUI();
                while (portals.MoveNext()) yield return portals.Current;
            }
            if (testPaths)
            {
                window.SetLevel(level); yield return null;
                IEnumerator paths = PathUI();
                while (paths.MoveNext()) yield return paths.Current;
            }
            if (testMerges)
            {
                IEnumerator merges = MergeUI();
                while (merges.MoveNext()) yield return merges.Current;
            }
        }
        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup < next) return; next = EditorApplication.timeSinceStartup + 0.3;
            try { if (!sequence.MoveNext()) Finish(null); } catch (Exception error) { Finish(error); }
        }
        private delegate bool WindowVisitor(IntPtr handle, IntPtr value);
        [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool EnumWindows(WindowVisitor visitor, IntPtr value);
        [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint process);
        [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr handle);
        [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr handle, IntPtr after, int x, int y, int width, int height, uint flags);
        private static void RaiseWindow()
        {
            // 다른 앱 대신 이 검증 프로세스의 Editor 창만 캡처한다.
#if UNITY_EDITOR_WIN
            uint process = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
            EnumWindows((handle, value) => { GetWindowThreadProcessId(handle, out uint owner); if (owner == process && IsWindowVisible(handle)) SetWindowPos(handle, new IntPtr(-1), 0, 0, 0, 0, 0x53); return true; }, IntPtr.Zero);
#endif
            window.Focus(); window.Repaint();
        }
        private static void Capture(string name)
        {
            Rect rect = window.position; Texture2D image = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGB24, false);
            image.SetPixels(UnityEditorInternal.InternalEditorUtility.ReadScreenPixel(rect.position, (int)rect.width, (int)rect.height)); image.Apply();
            File.WriteAllBytes(Evidence + "/" + name, image.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(image);
        }
        private static void Finish(Exception error)
        {
            EditorApplication.update -= Tick;
            if (error != null) { Results.Add("FAIL " + error); Debug.LogException(error); }
            File.WriteAllLines(Evidence + "/results.txt", Results);
            if (window != null) window.Close(); if (level != null) UnityEngine.Object.DestroyImmediate(level);
            EditorApplication.Exit(error == null ? 0 : 1);
        }
    }
}
