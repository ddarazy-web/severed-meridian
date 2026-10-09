#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using LevelAuthoring.Documents;
using Newtonsoft.Json.Linq;

namespace LevelAuthoring.Editing
{
    // 셀 번호는 9열, 전선 꼭짓점 번호는 10열이다. 공개 편집 API는 변경 전 전체 입력을 검사한다.
    internal static class DocumentBoardRules
    {
        internal static JObject Coordinate(int cell, int width = 9) => new JObject { ["row"] = cell / width, ["column"] = cell % width };
        internal static int Index(JToken cell, int width = 9) => (int)cell["row"] * width + (int)cell["column"];
        internal static bool Adjacent(int a, int b, int width = 9) => Math.Abs(a / width - b / width) + Math.Abs(a % width - b % width) == 1;
        internal static bool Active(JObject level, int cell) => cell >= 0 && cell < 81 && (bool?)level["board"]?["cells"]?[cell]?["isActive"] == true;
        internal static void RequireActive(JObject level, int cell) { if (!Active(level, cell)) throw new ContentFormatException("활성 칸을 선택하세요."); }
        internal static JArray List(JObject level, string path) => level.SelectToken(path) as JArray ?? throw new ContentFormatException("편집 목록이 없습니다: " + path);
        internal static JObject Definition(IReadOnlyDictionary<string, JObject> definitions, string id) => id != null && definitions.TryGetValue(id, out JObject value) ? value : throw new ContentFormatException("없는 요소 정의: " + id);
        internal static int Size(JObject definition) => (bool?)definition["charge"]?["enabled"] == true ? (int)definition["charge"]["size"] : (bool?)definition["placement"]?["enabled"] == true ? (int)definition["placement"]["size"] : 1;
        internal static bool Covers(JObject body, int size, int cell)
        {
            int r = (int)body["coordinate"]["row"], c = (int)body["coordinate"]["column"];
            return cell / 9 >= r && cell / 9 < r + size && cell % 9 >= c && cell % 9 < c + size;
        }
        internal static JObject Unique(JArray list, string key, int cell)
        {
            JObject[] matches = list.Cast<JObject>().Where(item => Index(item[key]) == cell).ToArray();
            if (matches.Length > 1) throw new ContentFormatException("중복 좌표를 먼저 수정하세요.");
            return matches.FirstOrDefault();
        }
        internal static bool SameEdge(int a, int b, int c, int d) => a == c && b == d || a == d && b == c;
        internal static bool Wall(JObject level, int a, int b) => List(level, "flow.walls").Any(w => SameEdge(a, b, Index(w["a"]), Index(w["b"])));
        internal static int[] WallVertices(JToken wall)
        {
            int a = Index(wall["a"]), b = Index(wall["b"]);
            int start = a / 9 == b / 9 ? a / 9 * 10 + Math.Max(a % 9, b % 9) : Math.Max(a / 9, b / 9) * 10 + a % 9;
            return new[] { start, start + (a / 9 == b / 9 ? 10 : 1) };
        }
        internal static void ReplaceOrAdd(JArray list, JObject old, JObject value) { if (old == null) list.Add(value); else old.Replace(value); }
    }
}
#endif
