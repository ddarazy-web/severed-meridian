using System;
using System.Collections.Generic;
namespace PopupUI
{
    public enum PopupRestoreStatus { None, Restored, ContextMismatch, Failed }
    public sealed class PopupRestoreResult
    {
        public PopupRestoreStatus Status { get; internal set; }
        public IReadOnlyList<PopupHandle> Handles { get; internal set; } = Array.Empty<PopupHandle>();
        public string Error { get; internal set; }
    }
}
