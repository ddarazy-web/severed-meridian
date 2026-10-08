using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameScreen;
using LevelAuthoring.Documents;
using LevelAuthoring.Runtime;
using LevelAuthoring.Storage;
using Levels;
using Tutorial;
using Tutorial.Editor;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
namespace LevelAuthoring.Editor
{
    public static class JsonSharedFixtureVerification
    {
        public static void Run()
        {
            var owned = new List<ScriptableObject>();
            var results = new List<string>();
            try
            {
                var sources = LegacyContentExporter.Discover().ToList();
                var first = TutorialSampleBoards.All.First(value => value.Id == "swap").CreateBoard(); owned.Add(first);
                var second = Object.Instantiate(first); owned.Add(second);
                var third = Object.Instantiate(first); owned.Add(third);
                var flow = ScriptableObject.CreateInstance<TutorialFlowDefinition>(); owned.Add(flow);
                flow.name = "공유 교환"; flow.steps = first.Tutorial.steps;
                for (int i = 0; i < flow.steps.Count; i++)
                {
                    flow.steps[i].authoringId = "step-" + i;
                    for (int j = 0; j < flow.steps[i].conditions.Count; j++) flow.steps[i].conditions[j].authoringId = "condition-" + j;
                }
                flow.parameters.Add(new TutorialFlowParameter { key = "swap-count", label = "교환 횟수", help = "레벨별 값",
                    stepId = flow.steps[0].authoringId, conditionId = flow.steps[0].conditions[0].authoringId, field = TutorialFlowField.RequiredCount });
                var independent = Object.Instantiate(flow); owned.Add(independent); independent.name = "독립 복사";
                var sample = ScriptableObject.CreateInstance<TutorialUserSampleDefinition>(); owned.Add(sample);
                sample.name = "교환 샘플"; sample.description = "샘플 선택 후 수치를 지정"; sample.steps = flow.steps.Select(step => step.Copy()).ToList();
                var levels = new[] { first, second, third };
                for (int i = 0; i < levels.Length; i++)
                {
                    var level = levels[i];
                    JsonUtility.FromJsonOverwrite("{\"levelNumber\":" + (991801 + i) + "}", level);
                    level.name = "JSON fixture " + i;
                    level.Tutorial.flow = i == 2 ? independent : flow;
                    level.Tutorial.steps = new List<TutorialStepDefinition> { new TutorialStepDefinition { instructions = "보존할 비활성 로컬 단계 " + i } };
                    level.Tutorial.bindings = new List<TutorialFlowBinding> { new TutorialFlowBinding { key = "swap-count", field = TutorialFlowField.RequiredCount, number = i + 1 } };
                    level.Tutorial.completionId = "stage02-fixture-" + i;
                    level.Tutorial.previousLevelNumbers = new List<int> { 91 + i };
                    sources.Add(new LegacyContentSource((8001 + i).ToString("x32"), "level", level));
                }
                sources.Add(new LegacyContentSource(8010.ToString("x32"), "tutorialFlow", flow));
                sources.Add(new LegacyContentSource(8011.ToString("x32"), "tutorialFlow", independent));
                sources.Add(new LegacyContentSource(8012.ToString("x32"), "tutorialSample", sample));
                string root = Path.GetFullPath("ContentData/Trials/game-authoring-stage-02/fixtures-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
                var published = LegacyContentExporter.Export(root, sources, null);
                var snapshot = new ContentSnapshotStore(root).Read().Snapshot;
                if (snapshot.Documents.Select(doc => doc.Kind).Distinct().Count() != 8) throw new Exception("8종류 누락");
                results.Add("PASS all eight kinds published/read");
                string Id(int number) => snapshot.Documents.Single(doc => doc.Kind == "level" && (int)doc.Data["levelNumber"] == number).Id;
                var a = snapshot.Get(Id(991801)); var b = snapshot.Get(Id(991802)); var c = snapshot.Get(Id(991803));
                if ((string)a.Data["tutorial"]["flowId"] != (string)b.Data["tutorial"]["flowId"] ||
                    (string)a.Data["tutorial"]["flowId"] == (string)c.Data["tutorial"]["flowId"]) throw new Exception("공유/복사 참조 소실");
                results.Add("PASS shared vs independent flow IDs");
                for (int i = 0; i < levels.Length; i++)
                {
                    var document = snapshot.Get(Id(991801 + i));
                    if ((int)document.Data["tutorial"]["bindings"][0]["number"] != i + 1 ||
                        (string)document.Data["tutorial"]["completionId"] != levels[i].Tutorial.completionId ||
                        (int)document.Data["tutorial"]["previousLevelNumbers"][0] != 91 + i ||
                        (string)document.Data["tutorial"]["steps"][0]["instructions"] != levels[i].Tutorial.steps[0].instructions)
                        throw new Exception("제작 정보 소실");
                    var request = JsonPuzzlePlayAdapter.CreateRequest(snapshot, document.Id, 73);
                    var restored = request.CreateDefinition(); owned.Add(restored);
                    if (restored.Tutorial.steps[0].conditions[0].requiredCount != i + 1 ||
                        restored.Tutorial.completionId != levels[i].Tutorial.completionId) throw new Exception("레벨별 해석 정보 소실");
                    results.Add("PASS bindings/local steps/identity/resolution " + i);
                }
                var copy = snapshot.Get((string)a.Data["tutorial"]["flowId"]);
                copy.Data["steps"][0]["instructions"] = "수정된 공통 단계";
                var changed = new ContentSnapshot(snapshot.Documents.Select(doc => doc.Id == copy.Id ? copy : doc));
                foreach (int number in new[] { 991801, 991802, 991803 })
                {
                    var restored = JsonPuzzlePlayAdapter.CreateRequest(changed, Id(number), 73).CreateDefinition(); owned.Add(restored);
                    if ((restored.Tutorial.steps[0].instructions == "수정된 공통 단계") != (number != 991803)) throw new Exception("공유 수정 범위 오류");
                }
                results.Add("PASS shared update changes two levels, independent copy unchanged");
                var again = LegacyContentExporter.Export(root, sources, null);
                if (again.Snapshot.Project.Data["sourceIds"].ToString() != published.Snapshot.Project.Data["sourceIds"].ToString()) throw new Exception("fixture IDs changed");
                results.Add("PASS fixture stable re-export IDs");
                File.WriteAllText("Logs/GameAuthoringStage02/latest-fixtures.txt", root);
                File.WriteAllLines("Logs/GameAuthoringStage02/shared-fixture-results.txt", results);
                foreach (var value in owned) if (value != null) Object.DestroyImmediate(value);
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                foreach (var value in owned) if (value != null) Object.DestroyImmediate(value);
                Debug.LogException(error); EditorApplication.Exit(1);
            }
        }
    }
}
