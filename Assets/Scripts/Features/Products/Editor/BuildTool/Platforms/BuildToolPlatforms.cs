using System.Collections.Generic;
using UnityEditor;

namespace Products.Editor
{
    public interface IBuildToolPlatform
    {
        BuildTarget Target { get; }
        string PlayerSettingsKey { get; }
        bool OutputIsDirectory { get; }
        void Inspect(BuildToolProfileInfo profile, List<BuildToolIssue> issues);
    }

    public sealed class BuildToolPlatforms
    {
        public static readonly BuildToolPlatforms Default = new BuildToolPlatforms(new AndroidBuildPlatform(), new IosBuildPlatform(), new WindowsBuildPlatform());
        private readonly Dictionary<BuildTarget, List<IBuildToolPlatform>> handlers = new Dictionary<BuildTarget, List<IBuildToolPlatform>>();

        public BuildToolPlatforms(params IBuildToolPlatform[] registrations)
        {
            foreach (var handler in registrations)
            {
                if (!handlers.TryGetValue(handler.Target, out var matches)) handlers.Add(handler.Target, matches = new List<IBuildToolPlatform>());
                matches.Add(handler);
            }
        }

        public bool TryGet(BuildTarget target, out IBuildToolPlatform handler, out string reason)
        {
            handler = null;
            if (!handlers.TryGetValue(target, out var matches)) { reason = "등록되지 않은 플랫폼: " + target; return false; }
            if (matches.Count != 1) { reason = "플랫폼 처리기가 중복 등록됨: " + target; return false; }
            handler = matches[0]; reason = null; return true;
        }
    }
}
