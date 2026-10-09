using System;
using System.IO;
using System.Linq;
using LevelAuthoring.Editing;
using LevelAuthoring.Storage;
using Newtonsoft.Json.Linq;

internal static partial class Program
{
    private static void CommonChecks()
    {
        var stored = new ContentSnapshotStore(File.ReadAllText("Logs/GameAuthoringStage02/latest-fixtures.txt")).Read();
        var document = stored.Snapshot.Documents.First(doc => doc.Kind == "level" && (int)doc.Data["schemaVersion"] == 5);
        var definitions = stored.Snapshot.Documents.Where(doc => doc.Kind == "element").ToDictionary(doc => (string)doc.Data["definition"]["id"], doc => (JObject)doc.Data["definition"]);
        JObject level = document.Data;
        foreach (JToken cell in level["board"]["cells"]) cell["isActive"] = true;
        level["elements"] = new JArray(); level["connections"] = new JArray(); level["missions"] = new JArray();
        foreach (JProperty list in ((JObject)level["flow"]).Properties()) list.Value = new JArray();
        level["elementSupply"]["sources"] = new JArray();
        FlowDocumentEditing.SetPath(level, new[] { 10, 11, 12 });
        Check(level["flow"]["paths"].Count() == 3 && (bool)level["flow"]["paths"][2]["isEnd"], "common path has explicit endpoint");
        string before = level.ToString();
        Reject(() => FlowDocumentEditing.SetPath(level, new[] { 20, 22 }), "common path rejects jumps");
        Check(before == level.ToString(), "common failed path is atomic");
        JObject invalid = (JObject)level.DeepClone();
        JObject duplicatePortal = new JObject { ["entrance"] = new JObject { ["row"] = 6, ["column"] = 7 }, ["hasExit"] = false, ["exit"] = new JObject { ["row"] = 0, ["column"] = 0 } };
        ((JArray)invalid["flow"]["portals"]).Add(duplicatePortal); ((JArray)invalid["flow"]["portals"]).Add(duplicatePortal.DeepClone());
        string invalidBefore = invalid.ToString();
        Reject(() => FlowDocumentEditing.SetPath(invalid, new[] { 60, 61 }), "common path detects duplicate final portal before editing");
        Check(invalid.ToString() == invalidBefore, "common duplicate endpoint leaves no partial path");
        FlowDocumentEditing.SetGravity(level, new[] { 20, 21 }, "Left");
        Check(level["flow"]["gravity"].Count() == 2, "common gravity supports area operation");
        FlowDocumentEditing.SetWall(level, definitions, 60, 61, false);
        Reject(() => FlowDocumentEditing.SetPath(level, new[] { 60, 61 }), "common path cannot cross wall");
        FlowDocumentEditing.SetWall(level, definitions, 60, 61, true);
        Check(!level["flow"]["walls"].Any(), "common wall removal is symmetric");
        FlowDocumentEditing.SetGravity(level, new[] { 21 }, "Right");
        int[] sources = FlowDocumentEditing.Sources(level, 22);
        FlowDocumentEditing.SetMerge(level, 22, sources.Reverse().ToArray());
        Check(level["flow"]["merges"][0]["sources"].Count() == sources.Length, "common merge follows current incoming graph");
        Reject(() => FlowDocumentEditing.SetMerge(level, 22, new[] { 1, 2 }), "common merge rejects stale candidates");
        FlowDocumentEditing.SetPortal(level, 30, 40);
        Reject(() => FlowDocumentEditing.SetPortal(level, 31, 40), "common portal preserves unique roles");
        FlowDocumentEditing.SetArrival(level, 70, false);
        Reject(() => SupplyDocumentEditing.PlaceSources(level, new[] { 70 }, false), "common source rejects arrival overlap");
        SupplyDocumentEditing.PlaceSources(level, new[] { 0, 1 }, false);
        var tutorialEdit = typeof(SupplyDocumentEditing).GetMethod("EditTutorial");
        Check(tutorialEdit != null, "tutorial supply scoped editing exists");
        if (tutorialEdit != null)
        {
            level["tutorial"]["supply"]["sources"] = new JArray();
            string ordinary = level["elementSupply"].ToString();
            Action<JObject> add = data =>
            {
                SupplyDocumentEditing.PlaceSources(data, new[] { 8 }, false);
                int last = data["elementSupply"]["sources"].Count() - 1;
                SupplyDocumentEditing.SetSourceProperty(data, definitions, new[] { last }, "mode", "Fixed");
                SupplyDocumentEditing.SetItems(data, definitions, new[] { last }, new JArray(new JObject {
                    ["definitionId"] = "power.rocket", ["count"] = 2, ["color"] = "Type1",
                    ["direction"] = "Vertical", ["durability"] = 1 }), false);
            };
            tutorialEdit.Invoke(null, new object[] { level, definitions, add });
            Check(level["elementSupply"].ToString() == ordinary, "tutorial supply preserves ordinary generation");
            Check((string)level["tutorial"]["supply"]["sources"].Last["items"][0]["direction"] == "Vertical", "tutorial supply preserves ordered item values");
            string saved = level.ToString();
            Action<JObject> random = data => SupplyDocumentEditing.PlaceSources(data, new[] { 7 }, false);
            bool rejected = false;
            try { tutorialEdit.Invoke(null, new object[] { level, definitions, random }); }
            catch (System.Reflection.TargetInvocationException error) when (error.InnerException is LevelAuthoring.Documents.ContentFormatException) { rejected = true; }
            Check(rejected && saved == level.ToString(), "tutorial random source rejected atomically");
        }
        SupplyDocumentEditing.SetSourceProperty(level, definitions, new[] { 0, 1 }, "mode", "Fixed");
        JObject item = new JObject { ["definitionId"] = "power.rocket", ["count"] = 2, ["color"] = "Type1", ["direction"] = "Vertical", ["durability"] = 1 };
        SupplyDocumentEditing.SetItems(level, definitions, new[] { 0, 1 }, new JArray(item), false);
        item["count"] = 999;
        Check((int)level["elementSupply"]["sources"][0]["items"][0]["count"] == 2, "common supply list owns copied values");
        before = level.ToString();
        Reject(() => SupplyDocumentEditing.SetSourceProperty(level, definitions, new[] { 0, 1 }, "mode", "Random"), "common source cannot hide fixed list");
        Check(before == level.ToString(), "common source group rejection is atomic");
        SupplyDocumentEditing.PlaceSources(level, new[] { 2 }, false);
        SupplyDocumentEditing.SetSourceProperty(level, definitions, new[] { 2 }, "mode", "MaintainRecovery");
        before = level.ToString();
        JObject recovery = (JObject)item.DeepClone(); recovery["definitionId"] = "supply.recovery"; recovery["count"] = 1;
        Reject(() => SupplyDocumentEditing.SetItems(level, definitions, new[] { 0, 1 }, new JArray(recovery), true), "common fixed supply conflicts with maintained content");
        Check(before == level.ToString(), "common maintenance conflict leaves all sources untouched");
        MissionDocumentEditing.Set(level, 0, "Color", "Type1", 3);
        Reject(() => MissionDocumentEditing.Set(level, 1, "Color", "Type1", 4), "common mission duplicate rejected");
        MissionDocumentEditing.Set(level, 1, "Mold", "Type1", 9);
        Check((int)level["missions"][1]["count"] == 0, "common mold mission keeps implicit target");
        LevelDocumentEditing.Place(level, definitions, "obstacle.generator", 5, 0, false);
        LevelDocumentEditing.Place(level, definitions, "obstacle.crate.wood", 5, 3, false);
        string generator = (string)level["elements"][0]["instanceId"], target = (string)level["elements"][1]["instanceId"];
        ConnectionDocumentEditing.Add(level, definitions, generator, target);
        Reject(() => ConnectionDocumentEditing.Add(level, definitions, generator, target), "common target belongs to one generator");
        ConnectionDocumentEditing.SetWire(level, definitions, 0, new[] { 50, 51, 52, 53 });
        Check(level["connections"][0]["vertices"].Count() == 4, "common wire uses ten-wide vertex coordinates");
        before = level.ToString();
        Reject(() => ConnectionDocumentEditing.SetWire(level, definitions, 0, new[] { 50, 53 }), "common wire cannot jump vertices");
        Check(before == level.ToString(), "common failed wire preserves route");
        Reject(() => FlowDocumentEditing.SetWall(level, definitions, 38, 47, false), "common wall cannot cross existing wire");
        LevelDocumentEditing.Place(level, definitions, "obstacle.metal-rod-box", 6, 5, false);
        Reject(() => FlowDocumentEditing.SetWall(level, definitions, 59, 60, false), "common wall cannot split 2x2 body");
        var session = new AuthoringEditSession(stored, document.Id);
        session.Apply("공통 미션 편집", docs => { docs[document.Id].Data["missions"] = new JArray(); MissionDocumentEditing.Set(docs[document.Id].Data, 0, "Color", "Type1", 3); });
        session.Undo(); Check(JToken.DeepEquals(session.Get(document.Id).Data["missions"], stored.Snapshot.Get(document.Id).Data["missions"]), "common commands participate in one session undo");
    }
}
