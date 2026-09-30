// [UNITY-SKILL:SPRITEATLAS]
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Levels.Editor
{
    // 콘텐츠가 플레이어에 복사되기 전에 빌드한다. 플레이어 완성 후 빌드하면 첫 배포에서 누락된다.
    public sealed class BoardAtlasContentBuild : IPreprocessBuildWithReport
    {
        public int callbackOrder => -900;
        public void OnPreprocessBuild(BuildReport report) => Build();
        public static void Build()
        {
            AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
            if (!string.IsNullOrEmpty(result.Error)) throw new BuildFailedException(result.Error);
        }
    }
}
