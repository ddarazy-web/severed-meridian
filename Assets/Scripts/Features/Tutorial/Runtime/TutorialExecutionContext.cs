using System;
using System.Collections.Generic;
using System.Linq;
using Levels;
using UnityEngine;

namespace Tutorial
{
    public enum TutorialRunMode { Automatic, Always, Never }

    /// <summary>실행 문맥별 완료 기록을 격리한다. 이전 레벨 기록은 명시 매핑으로만 승계한다.</summary>
    public sealed class TutorialExecutionContext
    {
        private const string Prefix = "MoonRabbit.Tutorial.Completed.";
        private const string IdentityPrefix = "MoonRabbit.Tutorial.Identity.";
        private readonly TutorialRunMode mode;
        private readonly Func<int, bool> read;
        private readonly Action<int> write;
        private readonly Func<string, bool> readIdentity;
        private readonly Action<string> writeIdentity;

        private TutorialExecutionContext(TutorialRunMode mode, Func<int, bool> read, Action<int> write, Func<string, bool> readIdentity, Action<string> writeIdentity)
        { this.mode = mode; this.read = read; this.write = write; this.readIdentity = readIdentity; this.writeIdentity = writeIdentity; }

        public static TutorialExecutionContext CreatePlayer()
            => CreateEditorWithIdentity(TutorialRunMode.Automatic,
                level => PlayerPrefs.GetInt(Prefix + level, 0) == 1,
                level => { PlayerPrefs.SetInt(Prefix + level, 1); PlayerPrefs.Save(); },
                id => PlayerPrefs.GetInt(IdentityPrefix + id, 0) == 1,
                id => { PlayerPrefs.SetInt(IdentityPrefix + id, 1); PlayerPrefs.Save(); });

        public static TutorialExecutionContext CreateTest(int mode)
        {
            HashSet<int> completed = new HashSet<int>();
            return CreateEditor((TutorialRunMode)mode, completed.Contains, level => completed.Add(level));
        }

        public static TutorialExecutionContext CreateEditor(TutorialRunMode mode, Func<int, bool> read, Action<int> write)
        {
            HashSet<string> completed = new HashSet<string>(StringComparer.Ordinal);
            return CreateEditorWithIdentity(mode, read, write, completed.Contains, id => completed.Add(id));
        }

        public static TutorialExecutionContext CreateEditorWithIdentity(TutorialRunMode mode, Func<int, bool> read, Action<int> write,
            Func<string, bool> readIdentity, Action<string> writeIdentity)
        {
            if (!Enum.IsDefined(typeof(TutorialRunMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            return new TutorialExecutionContext(mode, read ?? throw new ArgumentNullException(nameof(read)), write ?? throw new ArgumentNullException(nameof(write)),
                readIdentity ?? throw new ArgumentNullException(nameof(readIdentity)), writeIdentity ?? throw new ArgumentNullException(nameof(writeIdentity)));
        }

        public bool ShouldRun(int level, bool hasTutorial)
            => hasTutorial && mode != TutorialRunMode.Never && (mode == TutorialRunMode.Always || !read(level));

        public bool ShouldRun(LevelDefinition level)
        {
            if (level == null || !level.HasTutorial || mode == TutorialRunMode.Never) return false;
            if (mode == TutorialRunMode.Always) return true;
            LevelTutorialDefinition tutorial = level.Tutorial;
            if (string.IsNullOrEmpty(tutorial.completionId)) return ShouldRun(level.LevelNumber, true);
            if (readIdentity(tutorial.completionId)) return false;
            if (tutorial.previousLevelNumbers != null && tutorial.previousLevelNumbers.Any(read))
            { writeIdentity(tutorial.completionId); return false; }
            return true;
        }

        public void Complete(int level) { if (!read(level)) write(level); }

        public void Complete(int level, LevelTutorialDefinition tutorial)
        {
            if (string.IsNullOrEmpty(tutorial?.completionId)) { Complete(level); return; }
            if (!readIdentity(tutorial.completionId)) writeIdentity(tutorial.completionId);
        }
    }
}
