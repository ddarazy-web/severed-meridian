using System;
using System.IO;
using LevelAuthoring.Storage;
using LevelAuthoring.Documents;
using Newtonsoft.Json.Linq;
internal static partial class Program
{
    private static int passed;
    private static void Main()
    {
        string valid = "{\"kind\":\"project\",\"schemaVersion\":1,\"id\":\"project-test\",\"data\":{\"name\":\"한글 프로젝트\",\"contentVersion\":1,\"defaultCatalogId\":null,\"resources\":[]}}";
        valid = valid.Replace("\"name\"", "\"documents\":[],\"sourceIds\":[],\"name\"");
        var document = ContentJson.Read(valid);
        var text = ContentJson.Write(document);
        Check(text.EndsWith("\n") && !text.Contains("\r"), "stable LF output");
        Check(text.Contains("  \"kind\""), "two space indentation");
        Check(ContentJson.Write(ContentJson.Read(text)) == text, "canonical roundtrip");
        Reject(() => ContentJson.Read(valid.Replace("\"schemaVersion\":1", "\"schemaVersion\":2")), "future version");
        Reject(() => ContentJson.Read(valid.Replace("\"project\"", "\"alien\"")), "unknown kind");
        Reject(() => ContentJson.Read(valid.Replace("\"contentVersion\":1", "\"contentVersion\":1,\"future\":0")), "unknown nested field");
        Reject(() => ContentJson.Read(valid.Replace("\"id\":\"project-test\"", "\"id\":\"project-test\",\"id\":\"other\"")), "duplicate JSON property");
        Reject(() => ContentJson.Read(valid.Replace("\"resources\":[]", "\"resources\":null")), "required collection null");
        Reject(() => ContentJson.Read(valid.Replace("\"contentVersion\":1,", "")), "missing field");
        Reject(() => ContentJson.Read(valid + " {}"), "trailing document");
        Reject(() => ContentJson.Read(valid.Replace("\"contentVersion\":1", "\"contentVersion\":1.5")), "integer type");
        Reject(() => ContentJson.Read(valid.Replace("\"kind\"", "/* comment */\"kind\"")), "non JSON comment");
                Reject(() => ContentJson.Read(valid.Replace("\"resources\":[]", "\"resources\":[],")), "trailing comma");
        Reject(() => ContentJson.Read(valid.Replace("\"contentVersion\":1", "\"contentVersion\":0x01")), "hex number");
        Reject(() => ContentJson.Read(valid.Replace("\"contentVersion\":1", "\"contentVersion\":01")), "leading zero");
        var shape = new ContentDocument("shape", "shape-one", JObject.Parse("{\"displayName\":\"빈 모양\",\"cells\":[],\"sourceName\":\"level\",\"sourceLevelNumber\":1,\"obstacleHistory\":[]}"));
        for (int cell = 0; cell < 81; cell++) ((JArray)shape.Data["cells"]).Add(true);
        Check(ContentJson.Read(ContentJson.Write(shape)).Id == shape.Id, "shape roundtrip");
        shape.Data["unknown"] = false;
        Reject(() => ContentJson.Write(shape), "unknown shape property");
        shape.Data.Remove("unknown");
        ((JArray)shape.Data["cells"]).RemoveAt(80);
        Reject(() => ContentJson.Write(shape), "shape dimension");
        SnapshotChecks(document, shape);
        StorageChecks(document);
        ExportedChecks();
        SafetyChecks(document);
        Console.WriteLine("PASS " + passed + " portable checks");
    }
    private static void StorageChecks(ContentDocument document)
    {
        string root = Path.GetFullPath(Path.Combine("ContentData", "Trials", "game-authoring-stage-02", "portable-" + Guid.NewGuid().ToString("N")));
        var store = new JsonContentStore(root);
        var first = store.Save(document, "레벨/project.json", null);
        Check(store.Read("레벨/project.json").Hash == first.Hash, "UTF8 path and hash");
        Reject(() => store.Save(document, "레벨/project.json", null), "create cannot overwrite");
        Reject(() => store.Read("../escape.json"), "parent escape");
        Reject(() => store.Read(Path.Combine(root, "absolute.json")), "absolute path");
        Reject(() => store.Read("file.json:stream"), "alternate data stream");
        document.Data["name"] = "변경";
        var second = store.Save(document, "레벨/project.json", first.Hash);
        Check(second.Hash != first.Hash, "edited hash");
        Check(store.ReadBackup("레벨/project.json").Hash == first.Hash, "previous good backup");
        Reject(() => store.Save(document, "레벨/project.json", first.Hash), "external modification conflict");
        foreach (SavePhase phase in Enum.GetValues(typeof(SavePhase)))
        {
            var broken = new JsonContentStore(root, current => { if (current == phase) throw new IOException("Injected " + phase); });
            try { broken.Save(document, "레벨/project.json", second.Hash); throw new Exception("injection not called"); }
            catch (IOException) { }
            Check(store.Read("레벨/project.json").Hash == second.Hash, "failure preserves " + phase);
        }
        File.WriteAllText(Path.Combine(root, "레벨", "project.json.pending-interrupted"), "partial");
        Check(new JsonContentStore(root).Read("레벨/project.json").Hash == second.Hash, "interrupted write ignored on reopen");
        document.Data["future"] = true;
        Reject(() => store.Save(document, "레벨/project.json", second.Hash), "invalid save blocked");
        Check(store.Read("레벨/project.json").Hash == second.Hash, "invalid save preserves file");
        document.Data.Remove("future");
    }
    private static void Check(bool value, string name)
    {
        if (!value) throw new Exception("FAIL " + name);
        passed++; Console.WriteLine("PASS " + name);
    }
    private static void Reject(Action action, string name)
    {
        try { action(); } catch (ContentFormatException) { Check(true, name); return; }
        throw new Exception("FAIL accepted " + name);
    }
}
