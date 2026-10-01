// [UNITY-SKILL:SPRITEATLAS]
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build;

namespace Levels.Editor
{
    // 플레이어 빌드 시작 전 준비 단계에서 번들을 만든다. 전처리 콜백에서는 중첩 번들 빌드가 금지된다.
    public sealed class BoardAtlasContentBuild : BuildPlayerProcessor
    {
        public override int callbackOrder => -900;
        public override void PrepareForBuild(BuildPlayerContext context) => Build();
        public static void Build()
        {
            AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
            if (!string.IsNullOrEmpty(result.Error)) throw new BuildFailedException(result.Error);
        }
    }
}
