#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using LevelAuthoring.Documents;
using Newtonsoft.Json.Linq;

namespace LevelAuthoring.Editing
{
    // 저장 문서의 배치 연산. 호출자는 세션 Apply로 사용자 동작과 실행 취소를 묶는다.
    public static class LevelDocumentEditing
    {
        public static void Place(JObject level, IReadOnlyDictionary<string, JObject> definitions, string definitionId,
            int row, int column, bool replace, int durability = -1, int charge = -1, string color = "Type1", string direction = "Horizontal")
        {
            JArray items = Items(level);
            JObject definition = Definition(definitions, definitionId);
            string layer = Layer(definition);
            if (layer == null) throw new ContentFormatException("직접 배치할 수 없는 공급 정의입니다.");
            JObject existing = Find(items, definitions, layer, row, column);
            if (existing != null && !replace && ((string)existing["definitionId"] != definitionId || Size(definition) > 1))
                throw new ContentFormatException("더블클릭으로 교체하거나 속성에서 수정하세요.");
            if (existing != null && replace) { row = (int)existing["coordinate"]["row"]; column = (int)existing["coordinate"]["column"]; }
            bool colored = Enabled(definition, "color") && (bool)definition["color"]["requiresMatchingColor"] ||
                Enabled(definition, "supply") && (string)definition["supply"]["behavior"] == "FixedNormal";
            int max = Enabled(definition, "placement") ? (int)definition["placement"]["maxDurability"] : 0;
            int minimumCharge = Enabled(definition, "charge") ? (int)definition["charge"]["minRequired"] : 0;
            int actualDurability = max == 0 ? 0 : durability < 0 ? max : durability;
            int actualCharge = minimumCharge == 0 ? 0 : charge < 0 ? minimumCharge : charge;
            if (max > 0 && (actualDurability < 1 || actualDurability > max)) throw new ContentFormatException("내구도가 정의 범위 밖입니다.");
            if (minimumCharge > 0 && (actualCharge < minimumCharge || actualCharge > (int)definition["charge"]["maxRequired"])) throw new ContentFormatException("충전량이 정의 범위 밖입니다.");
            if (colored && !level["colors"].Values<string>().Contains(color)) throw new ContentFormatException("레벨 사용 색을 선택하세요.");
            if (Enabled(definition, "supply") && (string)definition["supply"]["content"] == "Rocket" && direction != "Horizontal" && direction != "Vertical")
                throw new ContentFormatException("로켓 방향이 잘못됐습니다.");
            var value = new JObject { ["definitionId"] = definitionId,
                ["instanceId"] = existing != null && (string)existing["definitionId"] == definitionId ? (string)existing["instanceId"] : Guid.NewGuid().ToString("N"),
                ["layer"] = layer, ["coordinate"] = Coordinate(row, column), ["durability"] = actualDurability,
                ["requiredCharge"] = actualCharge, ["hasColor"] = colored, ["color"] = color, ["rocketDirection"] = direction };
            CheckSpace(level, items, definitions, value, existing, replace);
            var removed = new HashSet<string>(StringComparer.Ordinal);
            int originalIndex = existing == null ? -1 : items.IndexOf(existing);
            foreach (JObject item in items.Cast<JObject>().ToArray())
            {
                bool cross = replace && (layer == "Obstacle" && ((string)item["layer"] == "Block" || (string)item["layer"] == "Cover") && Covers(value, definition, (int)item["coordinate"]["row"], (int)item["coordinate"]["column"]) ||
                    layer == "Block" && (string)item["layer"] == "Obstacle" && Covers(item, Definition(definitions, (string)item["definitionId"]), row, column));
                if (item != existing && !cross) continue;
                if ((string)item["instanceId"] != (string)value["instanceId"]) removed.Add((string)item["instanceId"]);
                item.Remove();
            }
            if (originalIndex >= 0 && originalIndex <= items.Count) items.Insert(originalIndex, value); else items.Add(value);
            Commit(level, items, removed);
        }

        public static void Move(JObject level, IReadOnlyDictionary<string, JObject> definitions, string instanceId, int row, int column)
        {
            JArray items = Items(level);
            JObject[] found = items.Cast<JObject>().Where(item => (string)item["instanceId"] == instanceId && (string)item["layer"] == "Obstacle").ToArray();
            if (string.IsNullOrEmpty(instanceId) || found.Length != 1) throw new ContentFormatException("이동할 본체 ID가 없거나 중복입니다.");
            JObject target = (JObject)found[0].DeepClone(); target["coordinate"] = Coordinate(row, column);
            CheckSpace(level, items, definitions, target, found[0], false);
            found[0]["coordinate"] = target["coordinate"].DeepClone();
            level["elements"] = items;
        }
        public static void Erase(JObject level, IReadOnlyDictionary<string, JObject> definitions, string layer, int row, int column)
        {
            JArray items = Items(level);
            JObject target = Find(items, definitions, layer, row, column);
            if (target == null) return;
            string id = (string)target["instanceId"]; target.Remove();
            Commit(level, items, new HashSet<string>(StringComparer.Ordinal) { id });
        }
        private static JArray Items(JObject level)
        {
            if ((int?)level["schemaVersion"] != 5 || !(level["elements"] is JArray items)) throw new ContentFormatException("요소 배치 형식의 레벨이 필요합니다.");
            return (JArray)items.DeepClone();
        }
        private static JObject Definition(IReadOnlyDictionary<string, JObject> definitions, string id)
        {
            if (id == null || !definitions.TryGetValue(id, out JObject value)) throw new ContentFormatException("없는 요소 정의: " + id);
            return value;
        }
        private static bool Enabled(JObject definition, string profile) => (bool?)definition[profile]?["enabled"] == true;
        private static int Size(JObject definition) => Enabled(definition, "placement") ? (int)definition["placement"]["size"] : Enabled(definition, "charge") ? (int)definition["charge"]["size"] : 1;
        private static string Layer(JObject definition)
        {
            if ((string)definition["reaction"] != "Unspecified") return "Obstacle";
            if (Enabled(definition, "layer")) return (string)definition["layer"]["behavior"] == "NormalConsumption" ? "Dust" : "Cover";
            if (Enabled(definition, "supply") && (string)definition["supply"]["behavior"] != "Obstacle" && (string)definition["supply"]["behavior"] != "RandomPower") return "Block";
            return null;
        }
        private static JObject Coordinate(int row, int column) => new JObject { ["row"] = row, ["column"] = column };
        private static bool Covers(JObject item, JObject definition, int row, int column)
        {
            int r = (int)item["coordinate"]["row"], c = (int)item["coordinate"]["column"], size = Size(definition);
            return row >= r && row < r + size && column >= c && column < c + size;
        }
        private static JObject Find(JArray items, IReadOnlyDictionary<string, JObject> definitions, string layer, int row, int column)
        {
            JObject[] found = items.Cast<JObject>().Where(item => (string)item["layer"] == layer && Covers(item, Definition(definitions, (string)item["definitionId"]), row, column)).ToArray();
            if (found.Length > 1) throw new ContentFormatException("중복 점유 칸을 먼저 수정하세요.");
            JObject result = found.FirstOrDefault();
            string id = (string)result?["instanceId"];
            if (!string.IsNullOrEmpty(id) && items.Count(item => (string)item["instanceId"] == id) > 1)
                throw new ContentFormatException("중복 본체 ID를 먼저 수정하세요.");
            return result;
        }
        private static void CheckSpace(JObject level, JArray items, IReadOnlyDictionary<string, JObject> definitions, JObject value, JObject self, bool replace)
        {
            JObject definition = Definition(definitions, (string)value["definitionId"]);
            int row = (int)value["coordinate"]["row"], column = (int)value["coordinate"]["column"], size = Size(definition);
            string layer = (string)value["layer"];
            if (size < 1 || size > 2 || row < 0 || column < 0 || row + size > 9 || column + size > 9) throw new ContentFormatException("본체가 보드 범위 밖입니다.");
            if (layer == "Obstacle" && level["flow"]?["walls"] is JArray walls)
                foreach (JObject wall in walls)
                    if (Covers(value, definition, (int)wall["a"]["row"], (int)wall["a"]["column"]) && Covers(value, definition, (int)wall["b"]["row"], (int)wall["b"]["column"]))
                        throw new ContentFormatException("본체 내부에 벽이 있습니다.");
            for (int r = row; r < row + size; r++)
                for (int c = column; c < column + size; c++)
                {
                    if ((bool?)level["board"]?["cells"]?[r * 9 + c]?["isActive"] != true) throw new ContentFormatException("비활성 칸입니다.");
                    foreach (string otherLayer in new[] { "Block", "Obstacle", "Cover", "Dust" })
                    {
                        JObject other = Find(items, definitions, otherLayer, r, c);
                        if (other == null || other == self) continue;
                        if (layer == otherLayer) throw new ContentFormatException("다른 요소가 점유합니다.");
                        if (layer == "Dust" || otherLayer == "Dust") continue;
                        if (layer == "Obstacle" && !replace || otherLayer == "Obstacle" && (layer != "Block" || !replace)) throw new ContentFormatException("다른 층 본체가 점유합니다.");
                        JObject otherDefinition = Definition(definitions, (string)other["definitionId"]);
                        if (layer == "Block" && otherLayer == "Cover" && ((string)otherDefinition["layer"]?["behavior"] == "CoverRemoval" || (string)definition["supply"]?["behavior"] == "Recovery") ||
                            layer == "Cover" && otherLayer == "Block" && (string)otherDefinition["supply"]?["behavior"] == "Recovery") throw new ContentFormatException("이 덮개와 블록은 겹칠 수 없습니다.");
                    }
                }
        }
        private static void Commit(JObject level, JArray items, HashSet<string> removed)
        {
            JArray connections = (JArray)level["connections"].DeepClone();
            foreach (JObject connection in connections.Cast<JObject>().ToArray())
                if (removed.Contains((string)connection["generatorId"]) || removed.Contains((string)connection["targetId"])) connection.Remove();
            level["elements"] = items; level["connections"] = connections;
        }
    }
}
#endif
