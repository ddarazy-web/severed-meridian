using System;

namespace Elements
{
    /// <summary>조회에 필요한 표시 상태만 전달한다. 난수·실행 상태 참조는 없다.</summary>
    public readonly struct ElementVisualState
    {
        public int Color { get; }
        public int Durability { get; }
        public int Charge { get; }
        public int RequiredCharge { get; }
        public int Direction { get; }
        public int Frame { get; }
        public int LogicalSize { get; }

        public ElementVisualState(int color, int durability, int charge, int requiredCharge, int direction, int frame, int logicalSize)
        {
            if (color < -1 || durability < 0 || charge < 0 || requiredCharge < 0 || direction < 0 || frame < 0 || logicalSize < 1)
                throw new ArgumentException("시각 상태에 음수 또는 유효하지 않은 크기가 있습니다.");
            Color = color; Durability = durability; Charge = charge; RequiredCharge = requiredCharge;
            Direction = direction; Frame = frame; LogicalSize = logicalSize;
        }
        public override string ToString() => $"색={Color}, 내구도={Durability}, 충전={Charge}/{RequiredCharge}, 방향={Direction}, 프레임={Frame}, 크기={LogicalSize}";
    }
}
