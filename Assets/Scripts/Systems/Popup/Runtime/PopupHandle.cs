using System;

namespace PopupUI
{
    public readonly struct PopupHandle : IEquatable<PopupHandle>
    {
        public long Value { get; }
        internal PopupHandle(long value) { Value = value; }
        public bool Equals(PopupHandle other) => Value == other.Value;
        public override bool Equals(object obj) => obj is PopupHandle other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public static bool operator ==(PopupHandle left, PopupHandle right) => left.Equals(right);
        public static bool operator !=(PopupHandle left, PopupHandle right) => !left.Equals(right);
    }
}
