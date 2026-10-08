using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using LevelAuthoring.Documents;

namespace LevelAuthoring.Validation
{
    // v1의 기본 ID는 원본을 덧붙여 저장하지 않고 호환 참조 범위로만 사용한다.
    internal static class ContentReferenceValidator
    {
        private static readonly string[] DefaultIds = {
            "obstacle.crate.wood", "obstacle.scrap", "obstacle.recovery-capsule", "obstacle.color-lock", "obstacle.metal-rod-box",
            "obstacle.generator", "cover.web", "cover.mold", "floor.dust", "supply.normal.random", "supply.normal.fixed",
            "power.rocket", "power.bomb", "power.drone", "power.magnet", "supply.scrap", "supply.recovery", "supply.power.random" };
        private static readonly string[] DefaultVisualKeys = {
            "legacy.normal", "legacy.rocket", "legacy.bomb", "legacy.drone", "legacy.magnet", "legacy.recovery", "legacy.crate",
            "legacy.scrap", "legacy.capsule", "legacy.color-lock", "legacy.rod-box", "legacy.generator", "legacy.web", "legacy.mold", "legacy.dust" };
        private static readonly HashSet<string> ConditionFields = new HashSet<string> { "RequiredCount", "MatchSize", "Target", "PowerDefinitionId", "Color", "MissionIndex" };

        public static void Validate(Dictionary<string, ContentDocument> documents)
        {
            foreach (var document in documents.Values)
            {
                switch (document.Kind)
                {
                    case "tutorialFlow": Flow(document.Data); break;
                    case "tutorialSample": GeneratedNames(Objects(document.Data["steps"], "sample.steps")); break;
                    case "visual": Visual(document.Data); break;
                    case "catalog": Catalog(document.Data, documents); break;
                    case "level": Level(document.Data, documents); break;
                }
            }
        }

        private static JObject[] Objects(JToken value, string path)
        {
            if (!(value is JArray array) || array.Any(item => !(item is JObject))) throw new ContentFormatException(path + ": 빈 목록 항목 또는 목록이 없습니다.");
            return array.Cast<JObject>().ToArray();
        }
        private static Dictionary<string, JObject> Unique(IEnumerable<JObject> items, string field, string path)
        {
            var result = new Dictionary<string, JObject>(StringComparer.Ordinal);
            foreach (var item in items)
            {
                string key = (string)item[field];
                if (string.IsNullOrWhiteSpace(key) || result.ContainsKey(key)) throw new ContentFormatException(path + ": 누락/중복 ID " + key);
                result.Add(key, item);
            }
            return result;
        }

        private static void Flow(JObject flow)
        {
            var steps = Unique(Objects(flow["steps"], "flow.steps"), "authoringId", "flow.steps");
            foreach (var step in steps.Values) Unique(Objects(step["conditions"], "flow.conditions"), "authoringId", "flow.conditions");
            var parameters = Unique(Objects(flow["parameters"], "flow.parameters"), "key", "flow.parameters");
            var destinations = new HashSet<string>(StringComparer.Ordinal);
            foreach (var parameter in parameters.Values)
            {
                string stepId = (string)parameter["stepId"], field = (string)parameter["field"];
                if (stepId == null || !steps.TryGetValue(stepId, out var step)) throw new ContentFormatException("flow.parameter.stepId: 없는 단계");
                if (!destinations.Add(stepId + "/" + (string)parameter["conditionId"] + "/" + field)) throw new ContentFormatException("flow.parameter: 중복 목적지");
                if (ConditionFields.Contains(field) && !Objects(step["conditions"], "conditions").Any(condition => (string)condition["authoringId"] == (string)parameter["conditionId"]))
                    throw new ContentFormatException("flow.parameter.conditionId: 없는 조건");
                if ((field == "First" && !string.IsNullOrEmpty((string)step["firstBinding"])) ||
                    (field == "Second" && !string.IsNullOrEmpty((string)step["secondBinding"]))) throw new ContentFormatException("생성 연결을 좌표 파라미터로 덮어쓸 수 없습니다.");
            }
            GeneratedNames(steps.Values.ToArray(), parameters.Values.ToArray());
        }

        private static JObject[] ResolveSteps(JObject level, Dictionary<string, ContentDocument> documents)
        {
            var tutorial = level["tutorial"] as JObject;
            if (tutorial == null) return Array.Empty<JObject>();
            if (tutorial["flowId"].Type == JTokenType.Null) return Objects(tutorial["steps"], "tutorial.steps");
            var flow = documents[(string)tutorial["flowId"]].Data;
            Flow(flow);
            var steps = Objects((JArray)flow["steps"].DeepClone(), "flow.steps");
            var parameters = Unique(Objects(flow["parameters"], "parameters"), "key", "parameters");
            var bindings = Unique(Objects(tutorial["bindings"], "bindings"), "key", "bindings");
            if (parameters.Count != bindings.Count || bindings.Keys.Any(key => !parameters.ContainsKey(key))) throw new ContentFormatException("레벨별 연결 목록이 공통 파라미터와 다릅니다.");
            foreach (var parameter in parameters.Values)
            {
                if (!bindings.TryGetValue((string)parameter["key"], out var binding) || (string)binding["field"] != (string)parameter["field"])
                    throw new ContentFormatException("파라미터 값 누락 또는 종류 불일치");
                string field = (string)parameter["field"];
                var step = steps.Single(item => (string)item["authoringId"] == (string)parameter["stepId"]);
                var condition = ConditionFields.Contains(field) ? Objects(step["conditions"], "conditions").Single(item => (string)item["authoringId"] == (string)parameter["conditionId"]) : null;
                switch (field)
                {
                    case "RequiredCount": condition["requiredCount"] = binding["number"].DeepClone(); break;
                    case "Target": condition["target"] = binding["target"].DeepClone(); break;
                    case "PowerDefinitionId": condition["powerDefinitionId"] = binding["definitionId"].DeepClone(); break;
                    case "ActionDefinitionId": step["actionDefinitionId"] = binding["definitionId"].DeepClone(); break;
                    case "MissionIndex":
                        int index = (int)binding["number"];
                        if (!(level["missions"] is JArray missions) || index < 0 || index >= missions.Count) throw new ContentFormatException("없는 레벨 미션 번호");
                        break;
                }
            }
            return steps;
        }

        private static void GeneratedNames(JObject[] steps, JObject[] parameters = null)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var step in steps)
            {
                foreach (string field in new[] { "firstBinding", "secondBinding" })
                {
                    string name = (string)step[field];
                    if (!string.IsNullOrEmpty(name) && !names.Contains(name)) throw new ContentFormatException(field + ": 이전 단계에 없는 생성 연결 이름");
                }
                var added = new HashSet<string>(StringComparer.Ordinal);
                foreach (var condition in Objects(step["conditions"], "conditions"))
                {
                    // 파라미터로 지정한 값은 레벨별 값을 적용한 뒤 의미를 검증한다.
                    bool Bound(string field) => parameters != null && parameters.Any(parameter =>
                        (string)parameter["stepId"] == (string)step["authoringId"] &&
                        (string)parameter["conditionId"] == (string)condition["authoringId"] && (string)parameter["field"] == field);
                    if (!Bound("Target") && condition["target"] is JObject target && (string)target["kind"] == "Generated" &&
                        (string.IsNullOrWhiteSpace((string)target["binding"]) || !names.Contains((string)target["binding"]))) throw new ContentFormatException("target.binding: 이전 단계에 없는 생성 연결 이름");
                    string name = (string)condition["bindGeneratedAs"];
                    if (string.IsNullOrEmpty(name)) continue;
                    if (string.IsNullOrWhiteSpace(name) || (string)condition["kind"] != "Generated" || (!Bound("RequiredCount") && (int)condition["requiredCount"] != 1) ||
                        names.Contains(name) || !added.Add(name)) throw new ContentFormatException("bindGeneratedAs: 생성 1개 또는 유일 이름 조건 위반");
                }
                names.UnionWith(added);
            }
        }

        private static void RequireId(string id, HashSet<string> available, string path)
        {
            if (string.IsNullOrWhiteSpace(id) || !available.Contains(id)) throw new ContentFormatException(path + ": 없는 요소 ID " + id);
        }
        private static void Supply(JToken token, HashSet<string> available)
        {
            if (!(token is JObject supply)) return;
            foreach (var source in Objects(supply["sources"], "supply.sources"))
            {
                foreach (var item in Objects(source["items"], "supply.items")) RequireId((string)item["definitionId"], available, "supply.item");
                string mode = (string)source["mode"];
                if (mode != "Fixed" || (string)source["exhaustion"] == "Random") RequireId((string)source["randomDefinitionId"], available, "supply.randomDefinitionId");
                if (mode == "MaintainScrap") RequireId((string)supply["scrapDefinitionId"], available, "supply.scrapDefinitionId");
                if (mode == "MaintainRecovery") RequireId((string)supply["recoveryDefinitionId"], available, "supply.recoveryDefinitionId");
            }
        }
        private static void Catalog(JObject catalog, Dictionary<string, ContentDocument> documents)
        {
            if (!(catalog["definitionIds"] is JArray ids)) throw new ContentFormatException("catalog.definitionIds 목록이 없습니다.");
            var definitions = Unique(ids.Select(id => documents[(string)id].Data["definition"] as JObject), "id", "catalog.definitions");
            ValidateDefinitionSupply(definitions);
        }
        private static void ValidateDefinitionSupply(Dictionary<string, JObject> definitions)
        {
            var available = new HashSet<string>(definitions.Keys, StringComparer.Ordinal);
            foreach (var definition in definitions.Values)
                if (definition["supply"] is JObject supply && (bool)supply["enabled"])
                {
                    if (!string.IsNullOrEmpty((string)supply["obstacleDefinitionId"])) RequireId((string)supply["obstacleDefinitionId"], available, "definition.supply.obstacleDefinitionId");
                    foreach (var id in supply["choiceDefinitionIds"] as JArray ?? new JArray()) RequireId((string)id, available, "definition.supply.choiceDefinitionIds");
                }
        }
        private static void Level(JObject level, Dictionary<string, ContentDocument> documents)
        {
            var definitions = new Dictionary<string, JObject>(StringComparer.Ordinal);
            var available = new HashSet<string>(DefaultIds, StringComparer.Ordinal);
            if (level["catalogId"].Type != JTokenType.Null)
                foreach (var id in (JArray)documents[(string)level["catalogId"]].Data["definitionIds"])
                {
                    var definition = (JObject)documents[(string)id].Data["definition"];
                    if (definitions.ContainsKey((string)definition["id"])) throw new ContentFormatException("카탈로그에 중복 요소 ID");
                    definitions.Add((string)definition["id"], definition); available.Add((string)definition["id"]);
                }
            else if (level["embeddedDefinitions"] is JArray embedded && embedded.Count > 0)
            {
                definitions = Unique(Objects(embedded, "embeddedDefinitions"), "id", "embeddedDefinitions");
                available = new HashSet<string>(definitions.Keys, StringComparer.Ordinal);
            }
            ValidateDefinitionSupply(definitions);
            if ((int)level["schemaVersion"] == 5)
            {
                foreach (var placement in Objects(level["elements"], "elements")) RequireId((string)placement["definitionId"], available, "elements.definitionId");
                Supply(level["elementSupply"], available);
            }
            var steps = ResolveSteps(level, documents);
            GeneratedNames(steps);
            foreach (var step in steps)
            {
                if ((string)step["kind"] == "PowerSwap") RequireId((string)step["actionDefinitionId"], available, "step.actionDefinitionId");
                foreach (var condition in Objects(step["conditions"], "conditions"))
                {
                    if (condition["target"] is JObject target && (string)target["kind"] == "Definition") RequireId((string)target["definitionId"], available, "condition.target");
                    if (!string.IsNullOrWhiteSpace((string)condition["powerDefinitionId"])) RequireId((string)condition["powerDefinitionId"], available, "condition.powerDefinitionId");
                }
                foreach (var result in Objects(step["results"], "results")) RequireId((string)result["definitionId"], available, "result.definitionId");
            }
            Supply(level["tutorial"]?["supply"], available);
            Connections(level, definitions);
        }

        private static void Connections(JObject level, Dictionary<string, JObject> definitions)
        {
            bool modern = (int)level["schemaVersion"] == 5;
            var placements = modern ? Objects(level["elements"], "elements").Where(item => (string)item["layer"] == "Obstacle") : Objects(level["obstacles"], "obstacles");
            var bodies = Unique(placements, modern ? "instanceId" : "id", "board bodies");
            string Role(JObject body)
            {
                if (!modern) return (string)body["kind"] == "Generator" ? "generator" : (string)body["kind"] == "Scrap" ? "scrap" : "durability";
                string id = (string)body["definitionId"];
                if (definitions.TryGetValue(id, out var definition))
                {
                    if ((string)definition["reaction"] == "GeneratorCharge") return "generator";
                    if ((string)definition["reaction"] != "Durability") return "other";
                    return (bool?)definition["removal"]?["enabled"] == true && (string)definition["removal"]["kind"] == "Scrap" ? "scrap" : "durability";
                }
                return id == "obstacle.generator" ? "generator" : id == "obstacle.scrap" ? "scrap" : "durability";
            }
            var targets = new HashSet<string>(StringComparer.Ordinal);
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var connection in Objects(level["connections"], "connections"))
            {
                string generator = (string)connection["generatorId"], target = (string)connection["targetId"];
                if (generator == null || target == null || !bodies.TryGetValue(generator, out var source) || !bodies.TryGetValue(target, out var destination) ||
                    Role(source) != "generator" || Role(destination) != "durability" || !targets.Add(target)) throw new ContentFormatException("연결 본체 ID/종류/중복 대상 오류");
                counts.TryGetValue(generator, out int count); counts[generator] = count + 1;
                if (count >= 3) throw new ContentFormatException("발전기당 연결은 최대 3개입니다.");
            }
            if (bodies.Any(pair => Role(pair.Value) == "generator" && !counts.ContainsKey(pair.Key))) throw new ContentFormatException("연결 대상이 없는 발전기");
        }

        private static void Visual(JObject data)
        {
            if (!(data["catalog"] is JObject catalog)) throw new ContentFormatException("visual.catalog가 없습니다.");
            var definitions = Unique(Objects(catalog["definitions"], "visual.definitions"), "key", "visual.definitions");
            var bindings = Unique(Objects(catalog["bindings"], "visual.bindings"), "id", "visual.bindings");
            var keys = new HashSet<string>(DefaultVisualKeys, StringComparer.Ordinal); keys.UnionWith(definitions.Keys);
            var ids = new HashSet<string>(DefaultIds.Where(id => id != "supply.power.random"), StringComparer.Ordinal); ids.UnionWith(bindings.Keys);
            foreach (var binding in bindings.Values)
                if (!keys.Contains((string)binding["visualKey"] ?? "")) throw new ContentFormatException("없는 visualKey");
            foreach (var definition in definitions.Values)
                foreach (var generated in definition["generates"] as JArray ?? new JArray())
                    if (!ids.Contains((string)generated ?? "")) throw new ContentFormatException("없는 시각 생성 참조");
        }
    }
}
