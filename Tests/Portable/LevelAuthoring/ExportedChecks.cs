using System;
using System.IO;
using System.Linq;
using LevelAuthoring.Documents;
using LevelAuthoring.Storage;
using Newtonsoft.Json.Linq;
internal static partial class Program
{
    private static void ExportedChecks()
    {
        string marker = "Logs/GameAuthoringStage02/latest-fixtures.txt";
        if (!File.Exists(marker)) throw new Exception("먼저 JsonSharedFixtureVerification.Run으로 대표 스냅샷을 생성하세요.");
        var snapshot = new ContentSnapshotStore(File.ReadAllText(marker)).Read().Snapshot;
        Check(snapshot.Documents.Select(document => document.Kind).Distinct().Count() == 8, "portable all eight kinds");
        foreach (var document in snapshot.Documents)
        {
            string json = ContentJson.Write(document);
            Check(ContentJson.Write(ContentJson.Read(json)) == json, "portable roundtrip " + document.Kind + " " + document.Id);
        }
        void Corrupt(string kind, Action<JObject> change, string name, Func<ContentDocument, bool> filter = null)
        {
            var documents = snapshot.Documents;
            var target = documents.First(document => document.Kind == kind && (filter == null || filter(document)));
            change(target.Data);
            Reject(() => new ContentSnapshot(documents), name);
        }
        foreach (string field in new[] { "RequiredCount", "Target" })
        {
            var documents = snapshot.Documents;
            var flow = documents.First(doc => doc.Kind == "tutorialFlow");
            var condition = flow.Data["steps"][0]["conditions"][0];
            if (field == "RequiredCount")
            {
                condition["kind"] = "Generated"; condition["bindGeneratedAs"] = "spawn"; condition["requiredCount"] = 2;
            }
            else { condition["target"]["kind"] = "Generated"; condition["target"]["binding"] = "overridden"; }
            flow.Data["parameters"][0]["field"] = field;
            foreach (var level in documents.Where(doc => doc.Kind == "level" && (string)doc.Data["tutorial"]?["flowId"] == flow.Id))
            {
                var binding = level.Data["tutorial"]["bindings"][0];
                binding["field"] = field; binding["number"] = 1;
                binding["target"] = condition["target"].DeepClone(); binding["target"]["kind"] = "Board";
                binding["target"]["binding"] = "";
            }
            Check(new ContentSnapshot(documents).Get(flow.Id) != null, "flow validates resolved " + field);
        }
        Corrupt("level", data => data["schemaVersion"] = 6, "future level payload version");
        Corrupt("catalog", data => data["definitionIds"] = JValue.CreateNull(), "null catalog members");
        Corrupt("catalog", data => ((JArray)data["definitionIds"]).Add(JValue.CreateNull()), "null catalog member");
        Corrupt("tutorialFlow", data => data["steps"][0]["kind"] = "Unsupported", "unsupported enum key");
        {
            var documents = snapshot.Documents;
            var levels = documents.Where(doc => doc.Kind == "level").Take(2).ToArray();
            levels[1].Data["levelNumber"] = levels[0].Data["levelNumber"].DeepClone();
            Reject(() => new ContentSnapshot(documents), "duplicate level number");
        }
        Corrupt("catalog", data => ((JArray)data["definitionIds"]).Add(data["definitionIds"][0].DeepClone()), "duplicate catalog member");
        {
            var documents = snapshot.Documents;
            var supply = documents.First(doc => doc.Kind == "element" && (string)doc.Data["definition"]["id"] == "supply.scrap");
            var obstacle = documents.First(doc => doc.Kind == "element" && (string)doc.Data["definition"]["id"] == "obstacle.scrap");
            supply.Data["definition"]["supply"]["obstacleDefinitionId"] = "obstacle.scrap";
            foreach (var catalog in documents.Where(doc => doc.Kind == "catalog"))
                foreach (var id in ((JArray)catalog.Data["definitionIds"]).Where(id => (string)id == obstacle.Id).ToArray()) id.Remove();
            Reject(() => new ContentSnapshot(documents), "catalog supply requires own definition");
        }
        Corrupt("tutorialFlow", data => data["parameters"][0]["stepId"] = "missing-step", "flow missing step");
        Corrupt("tutorialFlow", data => data["parameters"][0]["conditionId"] = "missing-condition", "flow missing condition");
        Corrupt("tutorialFlow", data => ((JArray)data["steps"]).Add(data["steps"][0].DeepClone()), "flow duplicate step ID");
        Corrupt("tutorialFlow", data => ((JArray)data["parameters"]).Add(data["parameters"][0].DeepClone()), "flow duplicate parameter key");
        Corrupt("level", data => data["tutorial"]["bindings"][0]["key"] = "missing-parameter", "unknown level binding",
            doc => doc.Data["tutorial"]["flowId"].Type == JTokenType.String);
        Corrupt("level", data => data["tutorial"]["bindings"][0]["field"] = "First", "binding field mismatch",
            doc => doc.Data["tutorial"]["flowId"].Type == JTokenType.String);
        Corrupt("level", data => ((JArray)data["tutorial"]["bindings"]).Clear(), "missing level binding",
            doc => doc.Data["tutorial"]["flowId"].Type == JTokenType.String);
        Corrupt("tutorialFlow", data => data["steps"][0]["firstBinding"] = "future-generated", "unknown generated name");
        Corrupt("level", data => ((JArray)data["connections"]).Add(new JObject { ["generatorId"] = "missing-generator", ["targetId"] = "missing-target", ["vertices"] = new JArray() }), "unknown connection body");
        Corrupt("visual", data => ((JArray)data["catalog"]["bindings"]).Add(new JObject { ["id"] = "new-binding", ["visualKey"] = "missing-visual" }), "unknown visual key");
        Corrupt("level", data => data["elementSupply"]["sources"][0]["randomDefinitionId"] = "not-registered", "unknown active supply ID",
            doc => (int)doc.Data["schemaVersion"] == 5 && doc.Data["elementSupply"]["sources"].Any());
    }
}
