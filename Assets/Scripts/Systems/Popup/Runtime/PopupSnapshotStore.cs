using System.Collections.Generic;
namespace PopupUI
{
    internal sealed class PopupSnapshotStore
    {
        internal sealed class Item
        {
            internal string Id;
            internal long OriginalHandle;
            internal PopupState State;
            internal string FocusKey;
        }
        internal sealed class Snapshot
        {
            internal PopupContext Context;
            internal Item[] Items;
        }
        private readonly Dictionary<string, Snapshot> snapshots = new Dictionary<string, Snapshot>(System.StringComparer.Ordinal);
        internal void Set(PopupContext context, Item[] items)
        {
            if (items.Length == 0) { snapshots.Remove(context.SceneKey); return; }
            snapshots[context.SceneKey] = new Snapshot { Context = context, Items = items };
        }
        internal bool TryGet(string scene, out Snapshot snapshot) => snapshots.TryGetValue(scene, out snapshot);
        internal void RemoveScene(string scene) { snapshots.Remove(scene); }
        internal void Discard(PopupContext context)
        { if (TryGet(context.SceneKey, out Snapshot snapshot) && snapshot.Context.Equals(context)) snapshots.Remove(context.SceneKey); }
    }
}
