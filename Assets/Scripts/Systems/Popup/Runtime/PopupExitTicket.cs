namespace PopupUI
{
    public sealed class PopupExitTicket
    {
        internal PopupService Owner;
        internal PopupHost Host;
        internal readonly PopupContext Context;
        internal readonly bool Preserve;
        internal PopupSnapshotStore.Item[] Items;
        internal bool Consumed;
        public bool IsPending => !Consumed && Owner != null && Host != null && Host.isActiveAndEnabled;
        internal PopupExitTicket(PopupService owner, PopupHost host, PopupContext context, bool preserve, PopupSnapshotStore.Item[] items)
        { Owner = owner; Host = host; Context = context; Preserve = preserve; Items = items; }
        internal void Consume()
        { Consumed = true; Owner = null; Host = null; Items = System.Array.Empty<PopupSnapshotStore.Item>(); }
    }
}
