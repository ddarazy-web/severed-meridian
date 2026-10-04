using System.Collections.Generic;

namespace PopupUI
{
    public sealed class PopupInspection
    {
        public IReadOnlyList<PopupInspectionItem> Items { get; }
        public PopupHandle? Top { get; }
        public int Count => Items.Count;
        public bool HasPendingExit { get; }
        public IReadOnlyList<PopupStoredInfo> Stored { get; }
        internal PopupInspection(IReadOnlyList<PopupInspectionItem> items, PopupHandle? top, bool pending, IReadOnlyList<PopupStoredInfo> stored)
        { Items = items; Top = top; HasPendingExit = pending; Stored = stored; }
    }
    public sealed class PopupInspectionItem
    {
        public string Id { get; }
        public PopupHandle Handle { get; }
        public bool IsTop { get; }
        public bool AllowMultiple { get; }
        public bool PauseGameplay { get; }
        public bool CloseOnCancel { get; }
        public bool Restorable { get; }
        internal PopupInspectionItem(string id, PopupHandle handle, bool top, bool multiple, bool pause, bool cancel, bool restorable)
        { Id = id; Handle = handle; IsTop = top; AllowMultiple = multiple; PauseGameplay = pause; CloseOnCancel = cancel; Restorable = restorable; }
    }
    public sealed class PopupStoredInfo
    {
        public PopupContext Context { get; }
        public int Count { get; }
        internal PopupStoredInfo(PopupContext context, int count) { Context = context; Count = count; }
    }
}
