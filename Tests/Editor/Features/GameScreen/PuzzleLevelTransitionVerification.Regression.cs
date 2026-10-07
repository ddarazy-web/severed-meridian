using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;

namespace GameScreen.Editor
{
    public static partial class PuzzleLevelTransitionVerification
    {
        private static async UniTask RegressionChecks()
        {
            // 원본 기준과 다른 의도된 결과 팝업을 제외한 보존 감사는 별도 해시 gate에서 수행한다.
            foreach (string method in new[] { "SourceAndActionChecks", "PoolReuseChecks", "InterruptionBoundaryChecks", "BackgroundResultChecks", "LifetimeChecks" })
            {
                List<string> reused = (List<string>)typeof(PuzzleStabilityVerification).GetField("results", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                reused.Clear();
                await (UniTask)typeof(PuzzleStabilityVerification).GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                results.AddRange(reused.Select(line => line.Replace("PASS ", "PASS 기존 " + method + " ")));
            }
        }
    }
}
