#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System.Collections.Generic;
using System.Linq;
using LevelAuthoring.Documents;
using Newtonsoft.Json.Linq;
using static LevelAuthoring.Editing.DocumentBoardRules;

namespace LevelAuthoring.Editing
{
    public static class SupplyDocumentEditing
    {
        public static void EditTutorial(JObject level, IReadOnlyDictionary<string, JObject> definitions, System.Action<JObject> edit)
        {
            // 일반 공급과 같은 편집 규칙을 사용하되 결과는 튜토리얼 영역에만 반영한다.
            JObject candidate = (JObject)level.DeepClone();
            candidate["elementSupply"] = level["tutorial"]["supply"].DeepClone();
            edit(candidate);
            foreach (JObject source in List(candidate, "elementSupply.sources"))
            {
                if ((string)source["mode"] != "Fixed" || (string)source["exhaustion"] != "Stop")
                    throw new ContentFormatException("튜토리얼은 고정 순서·소진 후 중단만 사용할 수 있습니다.");
                foreach (JObject item in (JArray)source["items"])
                {
                    string behavior = (string)Definition(definitions, (string)item["definitionId"])["supply"]?["behavior"];
                    if (behavior == "RandomNormal" || behavior == "RandomPower")
                        throw new ContentFormatException("튜토리얼에는 무작위 공급을 사용할 수 없습니다.");
                }
            }
            level["tutorial"]["supply"] = candidate["elementSupply"].DeepClone();
        }

        public static void PlaceSources(JObject level, IEnumerable<int> cells, bool erase)
        {
            JArray list = List(level, "elementSupply.sources"); int[] targets = cells.Distinct().ToArray();
            foreach (int cell in targets)
            {
                Unique(list, "coordinate", cell);
                if (erase) continue;
                RequireActive(level, cell);
                if (List(level, "flow.arrivals").Any(item => Index(item) == cell)) throw new ContentFormatException("생성구와 도착 바닥은 겹칠 수 없습니다.");
            }
            foreach (int cell in targets)
            {
                JObject old = Unique(list, "coordinate", cell);
                if (erase) old?.Remove();
                else if (old == null) list.Add(new JObject { ["coordinate"] = Coordinate(cell), ["mode"] = "Random", ["exhaustion"] = "Stop", ["randomDefinitionId"] = "supply.normal.random", ["items"] = new JArray() });
            }
        }
        private static JObject[] Targets(JObject level, IEnumerable<int> indices)
        {
            JArray list = List(level, "elementSupply.sources"); int[] targets = indices.Distinct().ToArray();
            if (targets.Length == 0 || targets.Any(index => index < 0 || index >= list.Count || !(list[index] is JObject))) throw new ContentFormatException("유효한 생성구를 선택하세요.");
            return targets.Select(index => (JObject)list[index]).ToArray();
        }
        public static void SetSourceProperty(JObject level, IReadOnlyDictionary<string, JObject> definitions, IEnumerable<int> indices, string field, string value)
        {
            JObject[] targets = Targets(level, indices);
            if (field == "mode")
            {
                if (!new[] { "Random", "Fixed", "MaintainScrap", "MaintainRecovery" }.Contains(value)) throw new ContentFormatException("지원하지 않는 공급 방식입니다.");
                if (value != "Fixed" && targets.Any(source => source["items"].Any())) throw new ContentFormatException("고정 목록을 먼저 비우세요.");
            }
            else if (field == "exhaustion")
            {
                if (!new[] { "Stop", "Random" }.Contains(value) || targets.Any(source => (string)source["mode"] != "Fixed")) throw new ContentFormatException("고정 생성구의 소진 방식을 선택하세요.");
            }
            else throw new ContentFormatException("편집 가능한 생성구 설정이 아닙니다.");
            JObject candidate = (JObject)level.DeepClone();
            foreach (JObject source in Targets(candidate, indices)) source[field] = value;
            CheckMaintenanceConflict(candidate, definitions);
            foreach (JObject source in targets) source[field] = value;
        }
        public static void SetRandomDefinition(JObject level, IReadOnlyDictionary<string, JObject> definitions, IEnumerable<int> indices, string id)
        {
            JObject[] targets = Targets(level, indices); JObject definition = Definition(definitions, id);
            if ((bool?)definition["supply"]?["enabled"] != true || (string)definition["supply"]["behavior"] != "RandomNormal") throw new ContentFormatException("무작위 일반 공급 정의를 선택하세요.");
            foreach (JObject source in targets) source["randomDefinitionId"] = id;
        }
        public static void SetItems(JObject level, IReadOnlyDictionary<string, JObject> definitions, IEnumerable<int> indices, JArray items, bool append)
        {
            if (items == null || items.Any(item => !(item is JObject))) throw new ContentFormatException("공급 목록을 지정하세요.");
            JObject[] targets = Targets(level, indices);
            if (items.Count > 0 && targets.Any(source => (string)source["mode"] != "Fixed")) throw new ContentFormatException("고정 생성구만 선택하세요.");
            foreach (JObject item in items)
            {
                JObject definition = Definition(definitions, (string)item["definitionId"]); JToken supply = definition["supply"];
                if ((bool?)supply?["enabled"] != true || (int?)item["count"] < 1 || item["count"]?.Type != JTokenType.Integer) throw new ContentFormatException("공급 정의와 양수 수량이 필요합니다.");
                if ((string)supply["behavior"] == "FixedNormal" && !List(level, "colors").Values<string>().Contains((string)item["color"])) throw new ContentFormatException("레벨 사용 색을 선택하세요.");
                if ((string)supply["content"] == "Rocket" && !new[] { "Horizontal", "Vertical" }.Contains((string)item["direction"])) throw new ContentFormatException("로켓 방향이 잘못됐습니다.");
                if ((string)supply["behavior"] == "Obstacle")
                {
                    int maximum = MaximumDurability(definitions, (string)item["definitionId"]);
                    if ((int?)item["durability"] < 1 || (int?)item["durability"] > maximum || item["durability"]?.Type != JTokenType.Integer) throw new ContentFormatException("공급 본체의 내구도 범위를 벗어났습니다.");
                }
            }
            JObject candidate = (JObject)level.DeepClone();
            foreach (JObject source in Targets(candidate, indices))
            {
                JArray result = append ? (JArray)source["items"].DeepClone() : new JArray();
                foreach (JToken item in items) result.Add(item.DeepClone());
                source["items"] = result;
            }
            CheckMaintenanceConflict(candidate, definitions);
            JObject[] replacements = Targets(candidate, indices);
            for (int i = 0; i < targets.Length; i++) targets[i]["items"] = replacements[i]["items"].DeepClone();
        }
        public static int MaximumDurability(IReadOnlyDictionary<string, JObject> definitions, string supplyId)
        {
            JToken supply = Definition(definitions, supplyId)["supply"];
            if ((bool?)supply?["enabled"] != true || (string)supply["behavior"] != "Obstacle") throw new ContentFormatException("내구도 공급 정의가 아닙니다.");
            string id = (string)supply["obstacleDefinitionId"];
            if (string.IsNullOrEmpty(id))
            {
                switch ((string)supply["obstacle"])
                {
                    case "Crate": id = "obstacle.crate.wood"; break;
                    case "Scrap": id = "obstacle.scrap"; break;
                    case "Safe": id = "obstacle.recovery-capsule"; break;
                    case "ColorLock": id = "obstacle.color-lock"; break;
                    case "Appliance": id = "obstacle.metal-rod-box"; break;
                    case "Generator": id = "obstacle.generator"; break;
                }
            }
            JObject body = Definition(definitions, id);
            if ((string)body["reaction"] != "Durability" || Size(body) != 1) throw new ContentFormatException("한 칸 내구도 본체만 공급할 수 있습니다.");
            return (int)body["placement"]["maxDurability"];
        }
        public static void SetMaintenanceProperty(JObject level, IReadOnlyDictionary<string, JObject> definitions, string field, int value)
        {
            if (!new[] { "scrapTarget", "scrapLimit", "scrapDurability", "recoveryTarget" }.Contains(field) || value < 0) throw new ContentFormatException("유지 수량/한도는 0 이상이어야 합니다.");
            if (field == "scrapDurability" && (value < 1 || value > MaximumDurability(definitions, (string)level["elementSupply"]["scrapDefinitionId"]))) throw new ContentFormatException("유지 고철 내구도 범위를 벗어났습니다.");
            level["elementSupply"][field] = value;
        }
        public static void SetMaintenanceDefinition(JObject level, IReadOnlyDictionary<string, JObject> definitions, string field, string id)
        {
            JToken supply = Definition(definitions, id)["supply"];
            if ((bool?)supply?["enabled"] != true) throw new ContentFormatException("공급 정의를 선택하세요.");
            if (field == "recoveryDefinitionId")
            {
                if ((string)supply["behavior"] != "Recovery") throw new ContentFormatException("회수 공급 정의를 선택하세요.");
            }
            else if (field == "scrapDefinitionId")
            {
                MaximumDurability(definitions, id);
                string bodyId = (string)supply["obstacleDefinitionId"];
                if (string.IsNullOrEmpty(bodyId) && (string)supply["obstacle"] == "Scrap") bodyId = "obstacle.scrap";
                JObject body = Definition(definitions, bodyId);
                if ((bool?)body["removal"]?["enabled"] != true || (string)body["removal"]["kind"] != "Scrap") throw new ContentFormatException("고철 미션 공급 정의를 선택하세요.");
                if ((int)level["elementSupply"]["scrapDurability"] > MaximumDurability(definitions, id)) throw new ContentFormatException("현재 유지 내구도가 새 공급 정의의 범위를 벗어납니다.");
            }
            else throw new ContentFormatException("유지 공급 정의 필드가 아닙니다.");
            level["elementSupply"][field] = id;
        }
        private static void CheckMaintenanceConflict(JObject level, IReadOnlyDictionary<string, JObject> definitions)
        {
            JArray sources = List(level, "elementSupply.sources");
            bool scrap = sources.Any(source => (string)source["mode"] == "MaintainScrap"), recovery = sources.Any(source => (string)source["mode"] == "MaintainRecovery");
            if (!scrap && !recovery) return;
            foreach (JToken item in sources.Where(source => (string)source["mode"] == "Fixed").SelectMany(source => (JArray)source["items"]))
            {
                JToken supply = Definition(definitions, (string)item["definitionId"])["supply"];
                bool isScrap = (string)supply["behavior"] == "Obstacle" && ((string)supply["obstacle"] == "Scrap" || (string)supply["obstacleDefinitionId"] == "obstacle.scrap");
                if ((string)supply["behavior"] == "Obstacle" && !string.IsNullOrEmpty((string)supply["obstacleDefinitionId"]))
                {
                    JObject body = Definition(definitions, (string)supply["obstacleDefinitionId"]);
                    isScrap = (bool?)body["removal"]?["enabled"] == true && (string)body["removal"]["kind"] == "Scrap";
                }
                if (scrap && isScrap || recovery && (string)supply["behavior"] == "Recovery") throw new ContentFormatException("같은 대상의 고정 공급과 개수 유지는 함께 사용할 수 없습니다.");
            }
        }
    }
}
#endif
