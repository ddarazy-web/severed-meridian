using System.Diagnostics;

namespace GameDiagnostics
{
    public static class GameLog
    {
        /// <summary>에디터 또는 Development Build에서만 출력한다. 그 외 빌드에서는 호출과 인수 계산을 제거한다.</summary>
        /// <param name="message">디버그 메시지.</param>
        /// <param name="context">콘솔에서 연결할 Unity 오브젝트. 생략 가능.</param>
        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void Debug(object message, UnityEngine.Object context = null)
        {
            UnityEngine.Debug.Log(message, context);
        }
    }
}
