using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tutorial
{
    public enum TutorialRunMode { Automatic, Always, Never }

    /// <summary>실행 문맥별로 완료 기록을 분리하고 보드 준비 전에 실행 여부를 결정한다.</summary>
    public sealed class TutorialExecutionContext
    {
        private const string Prefix = "MoonRabbit.Tutorial.Completed.";
        private readonly TutorialRunMode mode;
        private readonly Func<int, bool> read;
        private readonly Action<int> write;

        private TutorialExecutionContext(TutorialRunMode mode, Func<int, bool> read, Action<int> write)
        { this.mode = mode; this.read = read; this.write = write; }

        public static TutorialExecutionContext CreatePlayer()
            => new TutorialExecutionContext(TutorialRunMode.Automatic,
                level => PlayerPrefs.GetInt(Prefix + level, 0) == 1,
                level => { PlayerPrefs.SetInt(Prefix + level, 1); PlayerPrefs.Save(); });

        public static TutorialExecutionContext CreateTest(int mode)
        {
            HashSet<int> completed = new HashSet<int>();
            return CreateEditor((TutorialRunMode)mode, completed.Contains, level => completed.Add(level));
        }

        public static TutorialExecutionContext CreateEditor(TutorialRunMode mode, Func<int, bool> read, Action<int> write)
        {
            if (!Enum.IsDefined(typeof(TutorialRunMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            return new TutorialExecutionContext(mode, read ?? throw new ArgumentNullException(nameof(read)), write ?? throw new ArgumentNullException(nameof(write)));
        }

        public bool ShouldRun(int level, bool hasTutorial)
            => hasTutorial && mode != TutorialRunMode.Never && (mode == TutorialRunMode.Always || !read(level));

        public void Complete(int level)
        { if (!read(level)) write(level); }
    }
}
