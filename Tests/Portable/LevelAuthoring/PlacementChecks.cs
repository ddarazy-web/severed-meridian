using System;
using System.IO;
using System.Linq;
using LevelAuthoring.Editing;
using LevelAuthoring.Storage;
using Newtonsoft.Json.Linq;
internal static partial class Program
{
    private static void PlacementChecks()
    {
        var stored = new ContentSnapshotStore(File.ReadAllText("Logs/GameAuthoringStage02/latest-fixtures.txt")).Read();
        var definitions = stored.Snapshot.Documents.Where(doc => doc.Kind == "element").ToDictionary(doc => (string)doc.Data["definition"]["id"], doc => (JObject)doc.Data["definition"]);
        var level = stored.Snapshot.Documents.First(doc => doc.Kind == "level" && (int)doc.Data["schemaVersion"] == 5).Data;
        level["elements"] = new JArray(); level["connections"] = new JArray();
        foreach (var cell in level["board"]["cells"]) cell["isActive"] = true;
        LevelDocumentEditing.Place(level, definitions, "supply.normal.fixed", 0, 0, false);
        Reject(() => LevelDocumentEditing.Place(level, definitions, "obstacle.crate.wood", 0, 0, false), "placement requires explicit replacement");
        LevelDocumentEditing.Place(level, definitions, "obstacle.crate.wood", 0, 0, true);
        Check(level["elements"].Count() == 1 && (string)level["elements"][0]["definitionId"] == "obstacle.crate.wood", "normal to obstacle replacement");
        string bodyId = (string)level["elements"][0]["instanceId"];
        LevelDocumentEditing.Move(level, definitions, bodyId, 2, 2);
        Check((string)level["elements"][0]["instanceId"] == bodyId && (int)level["elements"][0]["coordinate"]["row"] == 2, "move preserves body identity");
        LevelDocumentEditing.Place(level, definitions, "supply.normal.fixed", 2, 2, true);
        Check((string)level["elements"][0]["layer"] == "Block", "obstacle to normal replacement");
        LevelDocumentEditing.Place(level, definitions, "obstacle.metal-rod-box", 4, 4, false);
        string before = level.ToString();
        Reject(() => LevelDocumentEditing.Place(level, definitions, "obstacle.crate.wood", 5, 5, false), "2x2 occupied child rejected");
        Check(level.ToString() == before, "rejected placement leaves board intact");
        Reject(() => LevelDocumentEditing.Place(level, definitions, "obstacle.metal-rod-box", 8, 8, false), "2x2 board edge rejected");
        string largeId = (string)level["elements"].Last()["instanceId"];
        ((JArray)level["connections"]).Add(new JObject { ["generatorId"] = "generator", ["targetId"] = largeId, ["vertices"] = new JArray() });
        LevelDocumentEditing.Erase(level, definitions, "Obstacle", 5, 5);
        Check(level["elements"].Count() == 1 && !level["connections"].Any(), "erase child removes whole body and connection");
        LevelDocumentEditing.Place(level, definitions, "floor.dust", 2, 2, false);
        Check(level["elements"].Count() == 2, "floor coexists with normal block");
        LevelDocumentEditing.Place(level, definitions, "obstacle.crate.wood", 6, 0, false);
        JObject duplicate = (JObject)level["elements"].Last().DeepClone(); duplicate["coordinate"]["column"] = 2;
        ((JArray)level["elements"]).Add(duplicate);
        Reject(() => LevelDocumentEditing.Place(level, definitions, "obstacle.crate.wood", 6, 0, true), "duplicate body ID blocks editing");
    }
}