using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.DataBuilders;

namespace Levels.Editor
{
    // Addressables만 별도로 빌드해도 변환을 거친다.
    public sealed class LevelPackAddressablesBuilder : BuildScriptPackedMode
    {
        public override string Name => "Level MemoryPack + AssetBundles";
        protected override TResult BuildDataImplementation<TResult>(AddressablesDataBuilderInput builderInput)
        {
            Products.Editor.ProductProfiles.PrepareContentBuild();
            LevelPackBuild.Generate();
            return base.BuildDataImplementation<TResult>(builderInput);
        }
    }
}
