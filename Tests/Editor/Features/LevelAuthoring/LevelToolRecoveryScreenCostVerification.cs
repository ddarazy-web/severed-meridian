using UnityEngine;

namespace LevelAuthoring.Editor
{
    // 자동 저장 비용의 과거 결과는 Docs/Verification과 기존 측정 로그에 보존한다.
    // 제거된 자동 기록/Flush를 다시 호출하지 않고 새 저장 정책을 검사한다.
    public static class LevelToolRecoveryScreenCostVerification
    {
        public static void Run()
        {
            Debug.Log("자동 복구 비용 검사는 수동 저장 정책 검사로 대체되었습니다. 이 실행은 반복 성능 측정이 아닙니다. 반복 측정은 수동 저장 전환 3단계에서 수행합니다.");
            LevelToolManualSaveVerification.Run();
        }
    }
}