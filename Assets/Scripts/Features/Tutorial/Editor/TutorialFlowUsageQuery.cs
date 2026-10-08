using System.Collections.Generic;
using System.Linq;
using Levels;
using UnityEditor;
namespace Tutorial.Editor
{
    public static class TutorialFlowUsageQuery
    {
        public static IReadOnlyList<LevelDefinition> Find(TutorialFlowDefinition flow)
            => AssetDatabase.FindAssets("t:LevelDefinition", new[] { "Assets/Data" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<LevelDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(level => level != null && level.Tutorial.flow == flow).OrderBy(level => level.LevelNumber).ToArray();

        public static IReadOnlyList<(LevelDefinition level, LevelValidationIssue issue)> Validate(TutorialFlowDefinition flow)
        {
            var results = new List<(LevelDefinition, LevelValidationIssue)>();
            foreach (LevelDefinition level in Find(flow))
                foreach (LevelValidationIssue issue in LevelTutorialReplayValidator.Validate(level)) results.Add((level, issue));
            return results;
        }
    }
}
