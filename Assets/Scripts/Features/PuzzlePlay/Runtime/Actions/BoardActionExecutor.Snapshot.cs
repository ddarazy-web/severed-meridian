using System;

namespace Simulation
{
    public sealed partial class BoardActionExecutor
    {
        /// <summary>
        /// 초기 배치가 아닌 안정된 현재 상태를 이어서 실행한다. 새 시작 판 검사/부스터를
        /// 적용하지 않는다. 호출자는 안정 경계와 소유권을 검증해야 하며 공개 API로 노출하지 않는다.
        /// 가정 상태의 분기도 이 경로로 같은 공통 규칙을 실행한다.
        /// </summary>
        /// <param name="stableState">독립 구성한 안정 상태.</param><param name="turn">완료한 턴 수.</param>
        /// <param name="effects">분기의 기존 효과 이력. 없으면 새 독립 이력.</param>
        internal BoardActionExecutor(LevelRuntimeState stableState, int turn, TurnEffectContext effects)
        {
            if (turn < 1) throw new ArgumentOutOfRangeException(nameof(turn));
            State = new LevelRuntimeState(stableState); Turn = turn;
            TurnEffects = effects?.Copy() ?? new TurnEffectContext(turn, Array.Empty<MatchedBlockChange>());
            Phase = BoardActionPhase.Ready;
            InitializeBoosters(Array.Empty<StartBooster>());
        }
    }
}
