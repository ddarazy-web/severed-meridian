using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Levels;
using Simulation;
using UnityEditor;
using UnityEngine;
namespace Tutorial.Editor
{
    public static class TutorialCompletionIdentityVerification
    {
        public static void Run()
        {
            var output = new List<string>();
            try
            {
                MethodInfo create = typeof(TutorialExecutionContext).GetMethod("CreateEditorWithIdentity");
                if (create == null) throw new Exception("튜토리얼 ID 완료 문맥 없음");
                HashSet<int> old = new HashSet<int> { 1 }; HashSet<string> completed = new HashSet<string>();
                int writes = 0;
                TutorialExecutionContext context = (TutorialExecutionContext)create.Invoke(null, new object[]
                { TutorialRunMode.Automatic, (Func<int,bool>)old.Contains, (Action<int>)(n => old.Add(n)), (Func<string,bool>)completed.Contains, (Action<string>)(id => { writes++; completed.Add(id); }) });
                LevelDefinition level = TutorialSampleBoards.All.First(value => value.Id == "swap").CreateBoard();
                try
                {
                    MethodInfo shouldRun = typeof(TutorialExecutionContext).GetMethod("ShouldRun", new[] { typeof(LevelDefinition) });
                    level.Tutorial.completionId = "learn.swap";
                    bool Run() => (bool)shouldRun.Invoke(context, new object[] { level });
                    if (!Run()) throw new Exception("명시 매핑 없이 이전 레벨 기록을 사용함");
                    level.Tutorial.previousLevelNumbers.Add(1);
                    if (Run() || writes != 1 || !completed.Contains("learn.swap")) throw new Exception("이전 기록 승계 실패");
                    if (Run() || writes != 1 || !old.Contains(1)) throw new Exception("승계 중복 또는 이전 기록 삭제");
                    level.Tutorial.completionId = "learn.new"; level.Tutorial.previousLevelNumbers.Clear();
                    if (!Run()) throw new Exception("새 학습을 이전 완료로 생략");
                    output.Add("PASS 명시 승계·중복 승계 방지·이전 키 보존·새 ID 재학습");
                }
                finally { UnityEngine.Object.DestroyImmediate(level); }
            }
            catch (Exception error) { output.Add("FAIL " + error); }
            foreach (string id in new[] { "hammer", "description" })
            {
                LevelDefinition level = TutorialSampleBoards.All.First(value => value.Id == id).CreateBoard();
                try
                {
                    using TutorialBoardAdapter adapter = TutorialBoardAdapter.Prepare(level, StartingBoardBuilder.Build(level, level.Tutorial.seed).State);
                    for (int i = 0; adapter.Executor.HasPendingCascade && i < 100; i++) adapter.ObserveCascade(adapter.Executor.AdvanceCascade());
                    typeof(LevelRuntimeState).GetProperty("MovesRemaining").SetValue(adapter.Executor.State, 0);
                    adapter.Tick(true, false);
                    if (adapter.Progress.State == TutorialProgressState.Cancelled || adapter.IsReleased) throw new Exception("이동0에서 무료 체험/설명을 취소함: " + id);
                    output.Add("PASS 이동0에서도 " + id + " 유지");
                }
                catch (Exception error) { output.Add("FAIL " + error); }
                finally { UnityEngine.Object.DestroyImmediate(level); }
            }
            Directory.CreateDirectory("Logs/Tutorial/Composer03"); File.WriteAllLines("Logs/Tutorial/Composer03/identity.txt", output);
            EditorApplication.Exit(output.Any(value => value.StartsWith("FAIL")) ? 1 : 0);
        }
    }
}
