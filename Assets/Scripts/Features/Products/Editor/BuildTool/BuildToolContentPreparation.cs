using System.IO;
using UnityEditor;
using UnityEditor.Build.Profile;

namespace Products.Editor
{
    public static class BuildToolContentPreparation
    {
        private static BuildToolRequest Current()
        {
            if (!BuildPipeline.isBuildingPlayer) return null;
            var request = new BuildToolRunStore(BuildToolRunStore.DefaultRoot).Load();
            var profile = BuildProfile.GetActiveBuildProfile();
            if (request == null || request.isMock || !request.userConfirmed || request.session != BuildToolController.Session ||
                request.phase != BuildToolRunPhase.Building || !request.callStarted || profile == null ||
                AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(profile)) != request.profileGuid || EditorUserBuildSettings.activeBuildTarget != request.target)
                return null;
            return request;
        }
        public static bool PacksPrepared => Current()?.packsPrepared == true;
        public static string ExpectedOutput(ProductDefinition product)
        {
            var request = Current();
            return request != null && request.product == product.Name ? BuildToolPlatforms.Default.Output(product, request.options) : product.Output;
        }
        public static void MarkPacksPrepared(string output)
        {
            var request = Current();
            if (request == null || Path.GetFullPath(output) != request.output) return;
            request.packsPrepared = true;
            new BuildToolRunStore(BuildToolRunStore.DefaultRoot).Save(request);
        }
    }
}
