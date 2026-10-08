using System;
using System.Collections.Generic;
using System.Linq;
using Levels;
using UnityEditor;
using UnityEngine;
namespace Tutorial.Editor
{
    public static class TutorialUserSampleStore
    {
        public const string Folder = "Assets/Data/Tutorial/Samples";
        public static TutorialUserSampleDefinition Save(string path, List<TutorialStepDefinition> steps, string description)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            if (!path.StartsWith(Folder + "/", StringComparison.Ordinal) || !path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) || path.Contains(".."))
                throw new ArgumentException("내 샘플은 " + Folder + " 아래에 저장하세요.");
            if (AssetDatabase.LoadMainAssetAtPath(path) != null) throw new ArgumentException("같은 이름의 샘플이 있습니다. 다른 이름을 사용하세요.");
            TutorialUserSampleDefinition sample = ScriptableObject.CreateInstance<TutorialUserSampleDefinition>();
            sample.description = description; sample.steps = TutorialFlowAuthoring.CopySteps(steps);
            TutorialFlowAuthoring.EnsureIds(sample.steps);
            AssetDatabase.CreateAsset(sample, path); AssetDatabase.SaveAssetIfDirty(sample); return sample;
        }
        public static void Apply(LevelDefinition level, TutorialUserSampleDefinition sample)
        {
            if (level.Tutorial.flow != null) throw new InvalidOperationException("공유 중에는 단계를 추가할 수 없습니다. 독립 복사를 먼저 선택하세요.");
            List<TutorialStepDefinition> copy = TutorialFlowAuthoring.CopySteps(sample.steps);
            HashSet<string> used = new HashSet<string>(level.Tutorial.steps.SelectMany(step => step.conditions)
                .Select(condition => condition.bindGeneratedAs).Where(value => !string.IsNullOrWhiteSpace(value)), StringComparer.Ordinal);
            Dictionary<string, string> renamed = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (TutorialConditionDefinition condition in copy.SelectMany(step => step.conditions).Where(value => !string.IsNullOrWhiteSpace(value.bindGeneratedAs)))
            {
                string original = condition.bindGeneratedAs;
                if (renamed.ContainsKey(original)) throw new ArgumentException("샘플 내부의 생성 연결 이름이 중복입니다: " + original);
                string name = original; int suffix = 2;
                while (!used.Add(name)) name = original + "_" + suffix++;
                renamed.Add(original, name); condition.bindGeneratedAs = name;
            }
            foreach (TutorialStepDefinition step in copy)
            {
                if (renamed.TryGetValue(step.firstBinding, out string first)) step.firstBinding = first;
                if (renamed.TryGetValue(step.secondBinding, out string second)) step.secondBinding = second;
                foreach (TutorialConditionDefinition condition in step.conditions)
                    if (condition.target?.kind == TutorialTargetKind.Generated && renamed.TryGetValue(condition.target.binding, out string target)) condition.target.binding = target;
            }
            foreach (TutorialStepDefinition step in copy)
            { step.authoringId = Guid.NewGuid().ToString("N"); foreach (TutorialConditionDefinition condition in step.conditions) condition.authoringId = Guid.NewGuid().ToString("N"); }
            Undo.RecordObject(level, "내 샘플 복사 적용"); level.Tutorial.steps.AddRange(copy); EditorUtility.SetDirty(level);
        }
        public static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Data/Tutorial")) AssetDatabase.CreateFolder("Assets/Data", "Tutorial");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Data/Tutorial", "Samples");
        }
    }
}
