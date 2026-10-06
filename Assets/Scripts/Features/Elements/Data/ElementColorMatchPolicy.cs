using Levels;

namespace Elements
{
    /// <summary>상태나 실행 문맥을 보유하지 않는 불변 색 일치 조건.</summary>
    public sealed class ElementColorMatchPolicy
    {
        public bool RequiresMatchingColor { get; }

        /// <param name="requiresMatchingColor">출발색이 지정색과 일치해야 하는지 여부.</param>
        public ElementColorMatchPolicy(bool requiresMatchingColor)
        { RequiresMatchingColor = requiresMatchingColor; }

        /// <param name="sourceColor">조회에 전달된 출발색. null은 색이 없는 경우다.</param>
        /// <param name="targetColor">본체 지정색.</param><returns>색 조건의 허용 여부.</returns>
        public bool Allows(RabbitColor? sourceColor, RabbitColor targetColor) =>
            !RequiresMatchingColor || sourceColor == targetColor;
    }
}
