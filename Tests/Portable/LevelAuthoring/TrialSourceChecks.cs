using System;
using System.IO;
using System.Linq;
using LevelAuthoring.Documents;
using LevelAuthoring.Storage;
internal static partial class Program
{
    private static void TrialSourceChecks()
    {
        Type type = typeof(ContentSnapshot).Assembly.GetType("LevelAuthoring.Storage.AuthoringTrialSource");
        Check(type != null, "portable trial source format exists");
        var snapshot = new ContentSnapshotStore(File.ReadAllText("Logs/GameAuthoringStage02/latest-fixtures.txt")).Read().Snapshot;
        string id = snapshot.Documents.First(doc => doc.Kind == "level").Id;
        string encoded = (string)type.GetMethod("Encode").Invoke(null, new object[] { snapshot, id });
        object decoded = type.GetMethod("Decode").Invoke(null, new object[] { encoded });
        var restored = (ContentSnapshot)type.GetProperty("Snapshot").GetValue(decoded);
        Check((string)type.GetProperty("LevelId").GetValue(decoded) == id && restored.Documents.Length == snapshot.Documents.Length, "trial source preserves level and all referenced documents");
        Check((string)type.GetMethod("Encode").Invoke(null, new object[] { restored, id }) == encoded, "trial source deterministic roundtrip");
        string variant = AuthoringTrialSource.EncodeWithMoves(snapshot, id, 17);
        var changed = AuthoringTrialSource.Decode(variant);
        Check((int)changed.Snapshot.Get(id).Data["moveCount"] == 17, "balance source uses requested move count");
        Check(AuthoringTrialSource.Encode(snapshot, id) == encoded, "balance source keeps original snapshot immutable");
        foreach (var doc in snapshot.Documents)
        {
            var copy = changed.Snapshot.Get(doc.Id);
            if (doc.Id == id) copy.Data["moveCount"] = doc.Data["moveCount"].DeepClone();
            Check(ContentJson.Write(copy) == ContentJson.Write(doc), "balance changes only selected level moves " + doc.Id);
        }
    }
}
