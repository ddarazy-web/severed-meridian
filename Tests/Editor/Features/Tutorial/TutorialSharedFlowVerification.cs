using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;
using Levels;
using UnityEditor;
using UnityEngine;
namespace Tutorial.Editor
{
    public static class TutorialSharedFlowVerification
    {
        public static void Run()
        {
            List<string> results = new List<string>();
            LevelDefinition first = ScriptableObject.CreateInstance<LevelDefinition>();
            LevelDefinition second = ScriptableObject.CreateInstance<LevelDefinition>();
            ScriptableObject flow = null;
            try
            {
                Type resolver = typeof(LevelDefinition).Assembly.GetType("Tutorial.TutorialFlowResolver");
                if (resolver == null) throw new Exception("공유 흐름 해석 계약이 없음");
                flow = ScriptableObject.CreateInstance(typeof(LevelDefinition).Assembly.GetType("Tutorial.TutorialFlowDefinition"));
                JsonUtility.FromJsonOverwrite("{\"steps\":[{\"authoringId\":\"step-a\",\"kind\":1,\"conditions\":[{\"authoringId\":\"condition-a\",\"requiredCount\":1}]}],\"parameters\":[{\"key\":\"count\",\"label\":\"교환 횟수\",\"stepId\":\"step-a\",\"conditionId\":\"condition-a\",\"field\":5}]}", flow);
                foreach (LevelDefinition level in new[] { first, second })
                {
                    JsonUtility.FromJsonOverwrite("{\"bindings\":[{\"key\":\"count\",\"field\":5,\"number\":2}]}", level.Tutorial);
                    level.Tutorial.GetType().GetField("flow").SetValue(level.Tutorial, flow);
                }
                string before = JsonUtility.ToJson(flow);
                MethodInfo resolve = resolver.GetMethod("Resolve", new[] { typeof(LevelDefinition) });
                LevelTutorialDefinition a = (LevelTutorialDefinition)resolve.Invoke(null, new object[] { first });
                if (a.steps[0].conditions[0].requiredCount != 2) throw new Exception("레벨별 횟수 미적용");
                a.steps[0].conditions[0].requiredCount = 9;
                LevelTutorialDefinition b = (LevelTutorialDefinition)resolve.Invoke(null, new object[] { second });
                if (b.steps[0].conditions[0].requiredCount != 2 || JsonUtility.ToJson(flow) != before) throw new Exception("해석 사본 변경이 다른 레벨/원본에 전파됨");
                results.Add("PASS 레벨별 값 해석 및 원본/다른 레벨 격리");
                JsonUtility.FromJsonOverwrite("{\"bindings\":[]}", first.Tutorial);
                try { resolve.Invoke(null, new object[] { first }); throw new Exception("누락 연결 허용됨"); }
                catch (TargetInvocationException e) { if (!(e.InnerException is ArgumentException)) throw; }
                results.Add("PASS 누락 연결 거절");
                TutorialFlowDefinition definition = (TutorialFlowDefinition)flow;
                TutorialFlowBinding binding = second.Tutorial.bindings[0];
                void Reject(Action change, Action restore, string label)
                {
                    change(); bool rejected = false;
                    try { TutorialFlowResolver.Resolve(second); }
                    catch (ArgumentException) { rejected = true; }
                    finally { restore(); }
                    if (!rejected) throw new Exception(label + " 허용됨"); results.Add("PASS " + label);
                }
                Reject(() => second.Tutorial.bindings.Add(binding), () => second.Tutorial.bindings.RemoveAt(1), "중복 값 거절");
                Reject(() => binding.field = TutorialFlowField.First, () => binding.field = TutorialFlowField.RequiredCount, "타입 불일치 거절");
                Reject(() => definition.parameters[0].stepId = "deleted", () => definition.parameters[0].stepId = "step-a", "삭제 단계 거절");
                Reject(() => definition.parameters[0].conditionId = "deleted", () => definition.parameters[0].conditionId = "condition-a", "삭제 조건 거절");
                Reject(() => definition.parameters.Add(definition.parameters[0]), () => definition.parameters.RemoveAt(1), "중복 선언 거절");
                definition.steps.Insert(0, new TutorialStepDefinition { authoringId = "new-step" });
                if (TutorialFlowResolver.Resolve(second).steps[1].conditions[0].requiredCount != 2) throw new Exception("순서 변경으로 연결 유실");
                results.Add("PASS 순서 변경 후 이름 기반 연결 유지");
                LevelDefinition sample = TutorialSampleBoards.All.First(value => value.Id == "swap").CreateBoard();
                try
                {
                    TutorialFlowDefinition shared = (TutorialFlowDefinition)flow;
                    shared.steps = sample.Tutorial.steps; shared.parameters.Clear();
                    foreach (TutorialStepDefinition step in shared.steps)
                    { step.authoringId = Guid.NewGuid().ToString("N"); foreach (TutorialConditionDefinition condition in step.conditions) condition.authoringId = Guid.NewGuid().ToString("N"); }
                    sample.Tutorial.steps = new List<TutorialStepDefinition>(); sample.Tutorial.flow = shared;
                    sample.Tutorial.completionId = "verification.swap"; sample.Tutorial.previousLevelNumbers.Add(1);
                    if (!sample.HasTutorial) throw new Exception("공유 흐름이 있는 레벨을 튜토리얼 없음으로 판정");
                    if (LevelTutorialReplayValidator.Validate(sample).Count != 0) throw new Exception("공유 입력 재생 실패");
                    LevelDefinition packed = LevelPackCodec.ReadLevel(LevelPackCodec.Snapshot(sample), sample.LevelNumber);
                    try
                    {
                        if (packed.Tutorial.flow != null || packed.Tutorial.steps.Count != shared.steps.Count || packed.Tutorial.completionId != "verification.swap" || packed.Tutorial.previousLevelNumbers.Single() != 1)
                            throw new Exception("팩에 해석값·완료 ID·이전 번호 미보존");
                        if (LevelTutorialReplayValidator.Validate(packed).Count != 0) throw new Exception("팩 재생 실패");
                    }
                    finally { UnityEngine.Object.DestroyImmediate(packed); }
                    results.Add("PASS 공유 흐름 Asset/팩 재생 동등성 및 완료 메타 보존");
                }
                finally { UnityEngine.Object.DestroyImmediate(sample); }
            }
            catch (Exception error) { results.Add("FAIL " + error); }
            finally { UnityEngine.Object.DestroyImmediate(first); UnityEngine.Object.DestroyImmediate(second); if (flow != null) UnityEngine.Object.DestroyImmediate(flow); }
            Directory.CreateDirectory("Logs/Tutorial/Composer03");
            File.WriteAllLines("Logs/Tutorial/Composer03/shared-flow.txt", results);
            EditorApplication.Exit(results.Exists(value => value.StartsWith("FAIL")) ? 1 : 0);
        }
    }
}
