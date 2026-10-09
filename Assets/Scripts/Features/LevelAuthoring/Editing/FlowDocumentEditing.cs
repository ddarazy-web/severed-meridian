#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using LevelAuthoring.Documents;
using Newtonsoft.Json.Linq;
using static LevelAuthoring.Editing.DocumentBoardRules;

namespace LevelAuthoring.Editing
{
    public static class FlowDocumentEditing
    {
        public static void SetGravity(JObject level, IEnumerable<int> cells, string direction)
        {
            if (direction != null && !new[] { "Down", "Up", "Left", "Right" }.Contains(direction)) throw new ContentFormatException("중력 방향 오류입니다.");
            int[] targets = cells.Distinct().ToArray(); JArray list = List(level, "flow.gravity");
            foreach (int cell in targets) { RequireActive(level, cell); Unique(list, "coordinate", cell); }
            foreach (int cell in targets)
            {
                JObject old = Unique(list, "coordinate", cell);
                if (direction == null) old?.Remove();
                else ReplaceOrAdd(list, old, new JObject { ["coordinate"] = Coordinate(cell), ["direction"] = direction });
            }
        }
        public static void SetPath(JObject level, IReadOnlyList<int> cells)
        {
            if (cells == null || cells.Count == 0 || cells.Distinct().Count() != cells.Count) throw new ContentFormatException("경로 칸을 중복 없이 지정하세요.");
            JArray paths = List(level, "flow.paths"), portals = List(level, "flow.portals");
            for (int i = 0; i < cells.Count; i++)
            {
                RequireActive(level, cells[i]);
                Unique(portals, "entrance", cells[i]);
                if (i < cells.Count - 1 && (Unique(paths, "coordinate", cells[i]) != null || Unique(portals, "entrance", cells[i]) != null)) throw new ContentFormatException("기존 경로/통로 입구를 지운 뒤 수정하세요.");
                Unique(paths, "coordinate", cells[i]);
                if (i > 0 && (!Adjacent(cells[i - 1], cells[i]) || Wall(level, cells[i - 1], cells[i]))) throw new ContentFormatException("벽 없는 상하좌우 인접 칸으로 연결하세요.");
            }
            for (int i = 0; i < cells.Count; i++)
            {
                if (i == cells.Count - 1 && (Unique(paths, "coordinate", cells[i]) != null || Unique(portals, "entrance", cells[i]) != null)) break;
                paths.Add(new JObject { ["coordinate"] = Coordinate(cells[i]), ["isEnd"] = i == cells.Count - 1, ["next"] = Coordinate(i == cells.Count - 1 ? 0 : cells[i + 1]) });
            }
        }
        public static void RemovePath(JObject level, int cell) => Unique(List(level, "flow.paths"), "coordinate", cell)?.Remove();
        public static void SetPortal(JObject level, int entrance, int? exit)
        {
            RequireActive(level, entrance); if (exit.HasValue) RequireActive(level, exit.Value);
            if (entrance == exit || Unique(List(level, "flow.paths"), "coordinate", entrance) != null) throw new ContentFormatException("입구와 출구/직접 경로가 겹칩니다.");
            if (List(level, "flow.arrivals").Any(item => Index(item) == entrance || Index(item) == exit)) throw new ContentFormatException("도착 바닥과 통로가 겹칩니다.");
            JArray portals = List(level, "flow.portals"); JObject old = Unique(portals, "entrance", entrance);
            foreach (JObject portal in portals)
            {
                if (portal == old) continue;
                int a = Index(portal["entrance"]); int? b = (bool)portal["hasExit"] ? Index(portal["exit"]) : (int?)null;
                if (a == entrance || b == entrance || exit.HasValue && (a == exit || b == exit)) throw new ContentFormatException("통로의 역할/쌍이 중복됩니다.");
            }
            ReplaceOrAdd(portals, old, new JObject { ["entrance"] = Coordinate(entrance), ["hasExit"] = exit.HasValue, ["exit"] = Coordinate(exit ?? 0) });
        }
        public static void RemovePortal(JObject level, int entrance) => Unique(List(level, "flow.portals"), "entrance", entrance)?.Remove();
        public static void SetArrival(JObject level, int cell, bool erase)
        {
            JArray list = List(level, "flow.arrivals");
            if (!erase)
            {
                RequireActive(level, cell);
                if (Unique(List(level, "elementSupply.sources"), "coordinate", cell) != null || List(level, "flow.portals").Any(p => Index(p["entrance"]) == cell || (bool)p["hasExit"] && Index(p["exit"]) == cell)) throw new ContentFormatException("생성구/통로와 도착 바닥은 겹칠 수 없습니다.");
            }
            JToken[] matches = list.Where(item => Index(item) == cell).ToArray();
            if (erase) foreach (JToken item in matches) item.Remove(); else if (matches.Length == 0) list.Add(Coordinate(cell));
        }
        public static void SetWall(JObject level, IReadOnlyDictionary<string, JObject> definitions, int a, int b, bool erase)
        {
            JArray walls = List(level, "flow.walls");
            if (!erase)
            {
                RequireActive(level, a); RequireActive(level, b);
                if (!Adjacent(a, b)) throw new ContentFormatException("벽은 인접 칸 사이에 설치하세요.");
                foreach (JObject body in List(level, "elements").Where(item => (string)item["layer"] == "Obstacle"))
                {
                    int size = Size(Definition(definitions, (string)body["definitionId"]));
                    if (size > 1 && Covers(body, size, a) && Covers(body, size, b)) throw new ContentFormatException("2×2 본체 내부에는 벽을 놓을 수 없습니다.");
                }
                if (List(level, "flow.paths").Any(p => !(bool)p["isEnd"] && SameEdge(a, b, Index(p["coordinate"]), Index(p["next"])))) throw new ContentFormatException("직접 경로가 지나는 경계입니다.");
                int[] edge = WallVertices(new JObject { ["a"] = Coordinate(a), ["b"] = Coordinate(b) });
                foreach (JToken connection in List(level, "connections"))
                {
                    int[] wire = ((JArray)connection["vertices"]).Select(v => Index(v, 10)).ToArray();
                    for (int i = 1; i < wire.Length; i++) if (SameEdge(edge[0], edge[1], wire[i - 1], wire[i])) throw new ContentFormatException("전선이 지나는 경계입니다.");
                }
            }
            JToken[] matches = walls.Where(w => SameEdge(a, b, Index(w["a"]), Index(w["b"]))).ToArray();
            if (erase) foreach (JToken wall in matches) wall.Remove();
            else if (matches.Length == 0) walls.Add(new JObject { ["a"] = Coordinate(a), ["b"] = Coordinate(b) });
        }
        public static int[] Sources(JObject level, int destination) => Enumerable.Range(0, 81).Where(cell => Next(level, cell) == destination).ToArray();
        private static int? Next(JObject level, int cell)
        {
            if (!Active(level, cell)) return null;
            JObject portal = Unique(List(level, "flow.portals"), "entrance", cell);
            if (portal != null) return (bool)portal["hasExit"] && Active(level, Index(portal["exit"])) ? Index(portal["exit"]) : (int?)null;
            JObject path = Unique(List(level, "flow.paths"), "coordinate", cell);
            int next;
            if (path != null) { if ((bool)path["isEnd"]) return null; next = Index(path["next"]); }
            else
            {
                string direction = (string)Unique(List(level, "flow.gravity"), "coordinate", cell)?["direction"] ?? "Down";
                if (!new[] { "Down", "Up", "Left", "Right" }.Contains(direction)) return null;
                next = direction == "Up" ? cell - 9 : direction == "Left" ? cell - 1 : direction == "Right" ? cell + 1 : cell + 9;
            }
            return Active(level, next) && Adjacent(cell, next) && !Wall(level, cell, next) ? next : (int?)null;
        }
        public static void SetMerge(JObject level, int cell, IReadOnlyList<int> sources)
        {
            int[] candidates = Sources(level, cell);
            if (sources == null || candidates.Length < 2 || sources.Count != candidates.Length || sources.Distinct().Count() != sources.Count || !new HashSet<int>(candidates).SetEquals(sources)) throw new ContentFormatException("현재 합류 후보를 빠짐없이 한 번씩 지정하세요.");
            JArray list = List(level, "flow.merges");
            ReplaceOrAdd(list, Unique(list, "coordinate", cell), new JObject { ["coordinate"] = Coordinate(cell), ["sources"] = new JArray(sources.Select(value => Coordinate(value))) });
        }
        public static void RemoveMerge(JObject level, int cell)
        {
            foreach (JToken merge in List(level, "flow.merges").Where(item => Index(item["coordinate"]) == cell).ToArray()) merge.Remove();
        }
    }
}
#endif
