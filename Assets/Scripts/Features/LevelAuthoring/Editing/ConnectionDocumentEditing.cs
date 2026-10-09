#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System.Collections.Generic;
using System.Linq;
using LevelAuthoring.Documents;
using Newtonsoft.Json.Linq;
using static LevelAuthoring.Editing.DocumentBoardRules;

namespace LevelAuthoring.Editing
{
    public static class ConnectionDocumentEditing
    {
        private static JObject Body(JObject level, string id)
        {
            JObject[] bodies = List(level, "elements").Cast<JObject>().Where(item => (string)item["layer"] == "Obstacle" && (string)item["instanceId"] == id).ToArray();
            if (string.IsNullOrEmpty(id) || bodies.Length != 1) throw new ContentFormatException("연결 대상 ID가 없거나 중복되었습니다.");
            return bodies[0];
        }
        public static void Add(JObject level, IReadOnlyDictionary<string, JObject> definitions, string generatorId, string targetId)
        {
            JObject generator = Definition(definitions, (string)Body(level, generatorId)["definitionId"]), target = Definition(definitions, (string)Body(level, targetId)["definitionId"]);
            if ((string)generator["reaction"] != "GeneratorCharge" || (string)target["reaction"] != "Durability" || (bool?)target["removal"]?["enabled"] == true && (string)target["removal"]["kind"] == "Scrap") throw new ContentFormatException("발전기 또는 연결 대상 종류가 잘못되었습니다.");
            JArray list = List(level, "connections");
            if (list.Any(item => (string)item["targetId"] == targetId)) throw new ContentFormatException("하나의 대상은 한 발전기에만 연결할 수 있습니다.");
            if (list.Count(item => (string)item["generatorId"] == generatorId) >= 3) throw new ContentFormatException("발전기는 최대 3개 대상까지 연결합니다.");
            list.Add(new JObject { ["generatorId"] = generatorId, ["targetId"] = targetId, ["vertices"] = new JArray() });
        }
        public static void Remove(JObject level, int index)
        {
            JArray list = List(level, "connections");
            if (index < 0 || index >= list.Count) throw new ContentFormatException("연결을 선택하세요.");
            list.RemoveAt(index);
        }
        // 꼭짓점은 10×10 번호(행 × 10 + 열)를 사용한다. 셀 번호와 혼동하지 않는다.
        public static void SetWire(JObject level, IReadOnlyDictionary<string, JObject> definitions, int index, IReadOnlyList<int> vertices, bool allowIncomplete = false)
        {
            JArray list = List(level, "connections");
            if (index < 0 || index >= list.Count) throw new ContentFormatException("연결을 선택하세요.");
            if (vertices == null || vertices.Count < (allowIncomplete ? 1 : 2)) throw new ContentFormatException("전선 경로가 미완성입니다.");
            JObject generator = Body(level, (string)list[index]["generatorId"]), target = Body(level, (string)list[index]["targetId"]);
            if (!Terminal(generator, Size(Definition(definitions, (string)generator["definitionId"])), vertices[0]) || !allowIncomplete && !Terminal(target, Size(Definition(definitions, (string)target["definitionId"])), vertices[vertices.Count - 1])) throw new ContentFormatException("본체 위치와 단자가 맞지 않습니다.");
            if (vertices.Any(vertex => vertex < 0 || vertex >= 100) || vertices.Distinct().Count() != vertices.Count) throw new ContentFormatException("전선 꼭짓점이 보드 밖이거나 중복됩니다.");
            foreach (JObject body in List(level, "elements").Where(item => (string)item["layer"] == "Obstacle"))
            {
                int size = Size(Definition(definitions, (string)body["definitionId"]));
                int row = (int)body["coordinate"]["row"], column = (int)body["coordinate"]["column"];
                if (vertices.Any(vertex => vertex / 10 > row && vertex / 10 < row + size && vertex % 10 > column && vertex % 10 < column + size)) throw new ContentFormatException("전선이 2×2 본체 내부를 지납니다.");
            }
            for (int i = 1; i < vertices.Count; i++)
            {
                if (!Adjacent(vertices[i - 1], vertices[i], 10)) throw new ContentFormatException("전선은 인접 격자 꼭짓점으로 연결하세요.");
                if (List(level, "flow.walls").Any(w => { int[] edge = WallVertices(w); return SameEdge(vertices[i - 1], vertices[i], edge[0], edge[1]); })) throw new ContentFormatException("전선은 고철 벽을 통과할 수 없습니다.");
            }
            if (list.Where((_, i) => i != index).Any(connection => ((JArray)connection["vertices"]).Any(vertex => vertices.Contains(Index(vertex, 10))))) throw new ContentFormatException("다른 전선과 꼭짓점을 공유할 수 없습니다.");
            list[index]["vertices"] = new JArray(vertices.Select(vertex => Coordinate(vertex, 10)));
        }
        private static bool Terminal(JObject body, int size, int vertex)
        {
            int row = (int)body["coordinate"]["row"], column = (int)body["coordinate"]["column"], r = vertex / 10, c = vertex % 10;
            return r >= row && r <= row + size && c >= column && c <= column + size && (r == row || r == row + size || c == column || c == column + size);
        }
    }
}
#endif
