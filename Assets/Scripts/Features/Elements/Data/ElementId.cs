using System;

namespace Elements
{
    /// <summary>표시명·GUID·본체 인스턴스 ID와 독립된 불변 요소 정의 ID.</summary>
    public readonly struct ElementId : IEquatable<ElementId>
    {
        private readonly string value;
        public bool IsValid => value != null;
        public string Value => IsValid ? value : throw new InvalidOperationException("초기화되지 않은 요소 정의 ID는 조회할 수 없습니다.");

        /// <param name="value">보존할 ID 원문. 빈 값은 거절하고 정규화하지 않는다.</param>
        public ElementId(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("요소 정의 ID는 빈 값일 수 없습니다.", nameof(value));
            this.value = value;
        }

        /// <param name="other">비교할 정의 ID.</param><returns>Ordinal로 같은 원문인지 여부. default끼리의 비교도 안전하게 처리한다.</returns>
        public bool Equals(ElementId other) => StringComparer.Ordinal.Equals(value, other.value);
        public override bool Equals(object obj) => obj is ElementId other && Equals(other);
        public override int GetHashCode() => IsValid ? StringComparer.Ordinal.GetHashCode(value) : 0;
        public override string ToString() => Value;
        public static bool operator ==(ElementId left, ElementId right) => left.Equals(right);
        public static bool operator !=(ElementId left, ElementId right) => !left.Equals(right);
    }
}
