using System;
using UnityEngine;

namespace PopupUI
{
    [CreateAssetMenu(menuName = "UI/Popup Catalog")]
    public sealed class PopupCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string Id;
            public PopupView Prefab;
            public bool AllowMultiple;
            public bool PauseGameplay;
            public bool CloseOnCancel = true;
            public bool Restorable;
        }
        [SerializeField] private Entry[] entries = Array.Empty<Entry>();
        public Entry[] Entries => Array.ConvertAll(entries, CopyEntry);
        public void Configure(Entry[] value)
        { if (value == null) throw new ArgumentNullException(nameof(value)); entries = Array.ConvertAll(value, CopyEntry); }
        private static Entry CopyEntry(Entry value) => value == null ? null : new Entry {
            Id = value.Id, Prefab = value.Prefab, AllowMultiple = value.AllowMultiple,
            PauseGameplay = value.PauseGameplay, CloseOnCancel = value.CloseOnCancel, Restorable = value.Restorable
        };
    }
}
