using System;
using System.IO;
using System.Linq;
using LevelAuthoring.Editing;
using Newtonsoft.Json.Linq;
using LevelAuthoring.Storage;
internal static partial class Program
{
    private static void ElementSelectionChecks()
    {
        var stored = new ContentSnapshotStore(File.ReadAllText("Logs/GameAuthoringStage02/latest-fixtures.txt")).Read();
        string levelId = stored.Snapshot.Documents.First(doc => doc.Kind == "level" && (int)doc.Data["schemaVersion"] == 5).Id;
        var session = new AuthoringEditSession(stored, levelId);
        var definitions = session.Documents.Where(doc => doc.Kind == "element").ToDictionary(doc => (string)doc.Data["definition"]["id"], doc => (JObject)doc.Data["definition"]);
        session.Apply("selection fixture", docs =>
        {
            JObject level = docs[levelId].Data;
            level["elements"] = new JArray(); level["connections"] = new JArray();
            foreach (var cell in level["board"]["cells"]) cell["isActive"] = true;
            LevelDocumentEditing.Place(level, definitions, "obstacle.metal-rod-box", 1, 1, false);
            LevelDocumentEditing.Place(level, definitions, "floor.dust", 2, 2, false);
        });
        session.SelectCells(new[] { 20 });
        var commands = new ElementSelectionEditing(session);
        Check(commands.SelectedInstanceIds("Obstacle").Length == 1 && commands.SelectedCells("Obstacle").SequenceEqual(new[] { 10, 11, 19, 20 }), "selection child resolves whole 2x2 body");
        string before = session.Get(levelId).Data.ToString();
        commands.Copy("Obstacle");
        Check(commands.ClipboardLayer == "Obstacle", "clipboard remembers copied layer");
        Check(session.Get(levelId).Data.ToString() == before && commands.HasClipboard, "copy leaves document unchanged");
        commands.Delete("Obstacle");
        Check(session.Get(levelId).Data["elements"].Count() == 1 && (string)session.Get(levelId).Data["elements"][0]["layer"] == "Dust", "selection delete preserves other layers");
        session.Undo();
        Check(session.Get(levelId).Data.ToString() == before, "selection delete one undo restores original");
        Reject(() => commands.Paste(8, 8), "selection paste board edge atomic");
        Check(session.Get(levelId).Data.ToString() == before, "failed paste preserves source");
        Reject(() => commands.Paste(1, 1), "selection paste same-definition collision rejected");
        Check(session.Get(levelId).Data.ToString() == before, "collision paste preserves document");
        session.Apply("source property", docs => docs[levelId].Data["elements"][0]["durability"] = 1);
        commands.Paste(4, 4);
        JObject copy = (JObject)session.Get(levelId).Data["elements"].Last();
        Check((int)copy["durability"] != 1 && (string)copy["instanceId"] != (string)session.Get(levelId).Data["elements"][0]["instanceId"], "clipboard deep copy and fresh identity");
        string pasted = session.Get(levelId).Data.ToString();
        session.Undo(); session.Redo();
        Check(session.Get(levelId).Data.ToString() == pasted, "paste redo preserves new identity");
        commands.Cut("Obstacle");
        Check(session.Get(levelId).Data["elements"].Count() == 2, "cut removes selected whole body");
        commands.Paste(6, 6);
        Check(session.Get(levelId).Data["elements"].Count() == 3, "cut clipboard survives source removal");
        session.Apply("connection fixture", docs =>
        {
            JObject level = docs[levelId].Data;
            level["elements"] = new JArray(); level["connections"] = new JArray();
            LevelDocumentEditing.Place(level, definitions, "obstacle.generator", 1, 1, false);
            LevelDocumentEditing.Place(level, definitions, "obstacle.crate.wood", 1, 3, false);
            ConnectionDocumentEditing.Add(level, definitions, (string)level["elements"][0]["instanceId"], (string)level["elements"][1]["instanceId"]);
            ConnectionDocumentEditing.SetWire(level, definitions, 0, new[] { 12, 13 });
        });
        session.SelectCells(new[] { 10, 12 });
        commands.Copy("Obstacle");
        JObject originalConnection = (JObject)session.Get(levelId).Data["connections"][0].DeepClone();
        commands.Paste(4, 1);
        JObject wired = session.Get(levelId).Data;
        Check(JToken.DeepEquals(wired["connections"][0], originalConnection) && wired["connections"].Count() == 2, "paste preserves original connection");
        Check((string)wired["connections"][1]["generatorId"] == (string)wired["elements"][2]["instanceId"] &&
            (string)wired["connections"][1]["targetId"] == (string)wired["elements"][3]["instanceId"] &&
            (int)wired["connections"][1]["vertices"][0]["row"] == 4, "paste remaps internal connection and shifts wire");
        session.SelectCells(new[] { 10 }); commands.Copy("Obstacle");
        before = session.Get(levelId).Data.ToString();
        Reject(() => commands.Paste(7, 1), "paste disconnected generator reference rejected");
        Check(session.Get(levelId).Data.ToString() == before, "reference paste failure is atomic");
        session.SelectCells(new[] { 10, 12 }); commands.Copy("Obstacle");
        session.Apply("late collision", docs => LevelDocumentEditing.Place(docs[levelId].Data, definitions, "obstacle.crate.wood", 7, 3, false));
        before = session.Get(levelId).Data.ToString();
        Reject(() => commands.Paste(7, 1), "paste second body collision rejects whole batch");
        Check(session.Get(levelId).Data.ToString() == before, "late collision leaves no first body");
        session.Apply("remove copied definition", docs => docs.Remove(session.Documents.First(doc => doc.Kind == "element" && (string)doc.Data["definition"]["id"] == "obstacle.generator").Id));
        before = session.Get(levelId).Data.ToString();
        Reject(() => commands.Paste(7, 5), "paste missing definition rejected");
        Check(session.Get(levelId).Data.ToString() == before, "missing definition preserves document");
        Reject(() => commands.Cut("Obstacle"), "failed cut capture rejects missing definition");
        Check(session.Get(levelId).Data.ToString() == before, "failed cut preserves original bodies");
        session = new AuthoringEditSession(stored, levelId);
        commands = new ElementSelectionEditing(session);
        string[] definitionIds = { "supply.normal.fixed", "power.rocket", "floor.dust", "cover.web" };
        string[] layers = { "Block", "Block", "Dust", "Cover" };
        session.Apply("layer fixture", docs =>
        {
            JObject level = docs[levelId].Data;
            level["elements"] = new JArray(); level["connections"] = new JArray();
            foreach (var cell in level["board"]["cells"]) cell["isActive"] = true;
            for (int i = 0; i < definitionIds.Length; i++) LevelDocumentEditing.Place(level, definitions, definitionIds[i], 0, i, false);
        });
        for (int i = 0; i < definitionIds.Length; i++)
        {
            session.SelectCells(new[] { i }); commands.Copy(layers[i]); commands.Paste(2, i);
            Check((string)session.Get(levelId).Data["elements"].Last()["definitionId"] == definitionIds[i], "layer clipboard " + definitionIds[i]);
            string current = session.Get(levelId).Data.ToString();
            commands.Delete(layers[i]); session.Undo();
            Check(session.Get(levelId).Data.ToString() == current, "layer delete undo " + definitionIds[i]);
        }
        session.Apply("unrelated incomplete draft", docs =>
        {
            docs[session.Documents.First(doc => doc.Kind == "level" && doc.Id != levelId).Id].Data["moveCount"] = -1;
            docs[session.Documents.Single(doc => doc.Kind == "project").Id].Data["contentVersion"] = 0;
        });
        session.SelectCells(new[] { 0 }); commands.Copy("Block"); commands.Paste(4, 0);
        Check((string)session.Get(levelId).Data["elements"].Last()["definitionId"] == "supply.normal.fixed", "paste allows unrelated incomplete draft documents");
        session.Apply("current incomplete tutorial", docs =>
        {
            docs[levelId].Data["moveCount"] = -1;
            docs[levelId].Data["tutorial"]["flowId"] = "unfinished-flow";
        });
        commands.Paste(4, 1);
        Check((string)session.Get(levelId).Data["elements"].Last()["definitionId"] == "supply.normal.fixed", "paste allows unrelated current level draft errors");
        session.Apply("missing destination catalog", docs => docs[levelId].Data["catalogId"] = "missing-catalog");
        before = session.Get(levelId).Data.ToString();
        Reject(() => commands.Paste(4, 2), "paste missing destination catalog rejected");
        Check(session.Get(levelId).Data.ToString() == before, "catalog reference failure leaves document unchanged");
        session.Apply("built-in only definitions", docs =>
        {
            docs[levelId].Data["catalogId"] = JValue.CreateNull();
            docs[levelId].Data["embeddedDefinitions"] = new JArray();
            foreach (string elementId in docs.Values.Where(document => document.Kind == "element").Select(document => document.Id).ToArray()) docs.Remove(elementId);
        });
        commands = new ElementSelectionEditing(session, () => definitions);
        session.SelectCells(new[] { 0 }); commands.Copy("Block"); commands.Paste(4, 2);
        Check((string)session.Get(levelId).Data["elements"].Last()["definitionId"] == "supply.normal.fixed", "paste resolves built-in definitions without element documents");
        session = new AuthoringEditSession(stored, levelId);
        commands = new ElementSelectionEditing(session, () => definitions);
        session.Apply("catalog overrides old embedded definitions", docs =>
        {
            JObject level = docs[levelId].Data;
            level["catalogId"] = stored.Snapshot.Documents.First(document => document.Kind == "catalog").Id;
            level["embeddedDefinitions"] = new JArray(definitions["floor.dust"].DeepClone());
            level["elements"] = new JArray(); level["connections"] = new JArray();
            foreach (var cell in level["board"]["cells"]) cell["isActive"] = true;
            LevelDocumentEditing.Place(level, definitions, "supply.normal.fixed", 0, 0, false);
        });
        session.SelectCells(new[] { 0 }); commands.Copy("Block"); commands.Paste(2, 0);
        Check((string)session.Get(levelId).Data["elements"].Last()["definitionId"] == "supply.normal.fixed", "paste active catalog overrides stale embedded definitions");
    }
}
