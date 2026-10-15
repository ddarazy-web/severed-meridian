using System.IO;
using Products.Editor;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
using UnityEngine;

namespace LevelTool.Editor
{
    public sealed class LevelToolManualBuild : BuildPlayerProcessor
    {
        public override int callbackOrder => -1000;

        public override void PrepareForBuild(BuildPlayerContext context)
        {
            if (!ProductProfiles.ValidateProfile(BuildProfile.GetActiveBuildProfile()).IsTool) return;
            string source = Path.GetFullPath(Path.Combine(Application.dataPath, "../Docs/MoonRabbitJunkyard/Manual"));
            if (!File.Exists(Path.Combine(source, "index.html"))) throw new BuildFailedException("레벨툴 사용 설명서 index.html이 없습니다.");
            // 원본 문서를 복제·수정하지 않고 레벨툴 플레이어에만 동봉한다.
            context.AddAdditionalPathToStreamingAssets(source, "LevelToolManual");
        }
    }
}
