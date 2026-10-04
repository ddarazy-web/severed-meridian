namespace PopupUI
{
    public sealed partial class PopupService
    {
        public PopupInspection Inspect()
        {
            PopupInspectionItem[] items = new PopupInspectionItem[instances.Count];
            for (int index = 0; index < instances.Count; index++)
            {
                Instance instance = instances[index];
                PopupCatalog.Entry entry = instance.Entry;
                items[index] = new PopupInspectionItem(entry.Id, instance.Handle, index == instances.Count - 1,
                    entry.AllowMultiple, entry.PauseGameplay, entry.CloseOnCancel, entry.Restorable);
            }
            return new PopupInspection(System.Array.AsReadOnly(items), Top, pendingExit != null,
                System.Array.AsReadOnly(snapshots.Inspect()));
        }
    }
}
