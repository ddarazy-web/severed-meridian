using System;
using System.IO;
using System.Linq;
using LevelAuthoring.Documents;
using LevelAuthoring.Storage;
internal static partial class Program
{
    private static void SafetyChecks(ContentDocument document)
    {
        Check(!typeof(ContentDocument).Assembly.GetReferencedAssemblies().Any(name => name.Name.StartsWith("Unity", StringComparison.Ordinal)), "no Unity assembly reference");
        string root = Path.GetFullPath(Path.Combine("ContentData", "Trials", "game-authoring-stage-02", "safety-" + Guid.NewGuid().ToString("N")));
        var store = new JsonContentStore(root);
        var saved = store.Save(document, "safe.json", null);
        var corruptTemporary = new JsonContentStore(root, phase =>
        {
            if (phase == SavePhase.BeforeValidation)
                File.WriteAllText(Directory.GetFiles(root, "*.pending-*").Single(), "{");
        });
        Reject(() => corruptTemporary.Save(document, "safe.json", saved.Hash), "actual temporary validation failure");
        Check(store.Read("safe.json").Hash == saved.Hash, "corrupt temporary preserves original");
        var race = new JsonContentStore(root, phase =>
        {
            if (phase == SavePhase.BeforeReplace)
            {
                var changed = new ContentDocument(document.Kind, document.Id, document.Data);
                changed.Data["name"] = "외부 프로세스";
                File.WriteAllText(Path.Combine(root, "safe.json"), ContentJson.Write(changed));
            }
        });
        Reject(() => race.Save(document, "safe.json", saved.Hash), "recheck hash immediately before replace");
        Check((string)store.Read("safe.json").Document.Data["name"] == "외부 프로세스", "preserve externally edited content");
        var lockedHash = store.Read("safe.json").Hash;
        using (var locked = new FileStream(Path.Combine(root, "safe.json"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            try { store.Save(document, "safe.json", lockedHash); throw new Exception("locked file accepted"); }
            catch (IOException) { Check(true, "OS locked target failure"); }
        }
        Check(store.Read("safe.json").Hash == lockedHash, "locked target unchanged");
        FileStream replacementLock = null;
        var replacement = new JsonContentStore(root, phase =>
        {
            if (phase == SavePhase.BeforeReplace) replacementLock = new FileStream(Path.Combine(root, "safe.json"), FileMode.Open, FileAccess.Read, FileShare.Read);
        });
        try
        {
            try { replacement.Save(document, "safe.json", lockedHash); throw new Exception("replacement lock accepted"); }
            catch (IOException) { Check(true, "actual OS replace failure"); }
        }
        finally { replacementLock?.Dispose(); }
        Check(store.Read("safe.json").Hash == lockedHash, "replacement failure preserves original");
        var links = new JsonContentStore(File.ReadAllText("Logs/GameAuthoringStage02/link-root.txt"));
        Reject(() => links.Read("link/escape.json"), "junction root escape");
        Reject(() => new JsonContentStore(Path.Combine(File.ReadAllText("Logs/GameAuthoringStage02/link-root.txt"), "link")), "linked workspace root");
        File.WriteAllBytes(Path.Combine(root, "invalid-utf8.json"), new byte[] { 0xff, 0xfe });
        Reject(() => store.Read("invalid-utf8.json"), "invalid UTF8 rejected");
        File.WriteAllText(Path.Combine(root, "future.json"), ContentJson.Write(document).Replace("\"schemaVersion\": 1", "\"schemaVersion\": 99"));
        Reject(() => store.Save(document, "future.json", "incorrect"), "future document overwrite blocked");
    }
}
