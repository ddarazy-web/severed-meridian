using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Board;
using LevelAuthoring.Documents;
using LevelAuthoring.Editing;
using LevelAuthoring.Runtime;
using LevelAuthoring.Storage;
using LevelTool;
using Newtonsoft.Json.Linq;
using Tutorial;
using UnityEditor;
using UnityEngine;

namespace LevelAuthoring.Editor
{
    public static class LevelToolDataFlowVerification
    {
        internal static AuthoringToolWorkspace CreateFixture(string folder)
        {
            var workspace = new AuthoringToolWorkspace();
            workspace.Create(folder, LevelToolDocuments.NewProject());
            var session = workspace.Session;
            var definitions = LevelToolDocuments.Definitions(session);
            string normal = definitions.First(pair => (string)pair.Value["supply"]?["behavior"] == "FixedNormal").Key;
            session.EditLevel("기본·공급", level =>
            {
                LevelDocumentEditing.SetName(level, "독립 튜토리얼"); LevelDocumentEditing.SetMoveCount(level, 31);
                MissionDocumentEditing.Set(level, 0, "Color", "Type1", 30);
                SupplyDocumentEditing.SetSourceProperty(level, definitions, new[] { 0 }, "mode", "Fixed");
                SupplyDocumentEditing.AppendItem(level, definitions, new[] { 0 }, normal, 3, "Type2", "Horizontal", 1);
                LevelDocumentEditing.Place(level, definitions, normal, 0, 0, true, color: "Type1");
                LevelDocumentEditing.Place(level, definitions, normal, 0, 1, true, color: "Type2");
                LevelDocumentEditing.Place(level, definitions, normal, 1, 0, true, color: "Type2");
                LevelDocumentEditing.Place(level, definitions, normal, 2, 0, true, color: "Type2");
            });
            TutorialDraftEditing.EditLevel(session, "독립 튜토리얼", level =>
            {
                level.Tutorial.steps.Add(new TutorialStepDefinition
                {
                    authoringId = "step-swap", kind = TutorialStepKind.Swap, instructions = "두 칸을 바꿔보세요",
                    hasFirst = true, first = new BoardCoordinate(0, 0), hasSecond = true, second = new BoardCoordinate(0, 1),
                    conditions = new List<TutorialConditionDefinition> { new TutorialConditionDefinition
                    { authoringId = "condition-swap", kind = TutorialConditionKind.SuccessfulSwap, requiredCount = 1 } }
                });
            });
            string sharedLevel = DocumentEditing.DuplicateLevel(session, session.SelectedLevelId, 2, "공유 튜토리얼");
            session.SelectLevel(sharedLevel);
            string flowId = TutorialDraftEditing.CreateFlow(session, "공통 교환");
            TutorialDraftEditing.EditFlow(session, flowId, "조건 횟수 공개", flow => flow.parameters.Add(new TutorialFlowParameter
            { key = "swap-count", field = TutorialFlowField.RequiredCount, stepId = "step-swap", conditionId = "condition-swap" }));
            TutorialDraftEditing.Synchronize(session);
            TutorialDraftEditing.EditLevel(session, "레벨별 횟수", level => level.Tutorial.bindings.Single(value => value.key == "swap-count").number = 2);
            session.SelectCells(new[] { 0, 1 });
            return workspace;
        }

        public static void Run()
        {
            string root = Path.GetFullPath("Logs/LevelToolRefactorStage04/data-flow-" + Guid.NewGuid().ToString("N"));
            try
            {
                var workspace = CreateFixture(Path.Combine(root, "project"));
                var session = workspace.Session;
                string selected = session.SelectedLevelId;
                JObject authored = session.Get(selected).Data;
                Check((int)authored["moveCount"] == 31 && (int)authored["elementSupply"]["sources"][0]["items"][0]["count"] == 3 &&
                    (string)authored["elementSupply"]["sources"][0]["items"][0]["color"] == "Type2", "requested property and supply values authored");
                Check((int)authored["tutorial"]["bindings"][0]["number"] == 2 &&
                    session.Documents.Single(doc => doc.Kind == "level" && (int)doc.Data["levelNumber"] == 1).Data["tutorial"]["flowId"].Type == JTokenType.Null,
                    "shared level override and independent level remain distinct");
                string expected = ContentSnapshotFingerprint.Compute(session.CreateSnapshot());
                session.Undo(); Check(ContentSnapshotFingerprint.Compute(session.CreateSnapshot()) != expected, "binding Undo changes only prior transaction");
                session.Redo(); Check(ContentSnapshotFingerprint.Compute(session.CreateSnapshot()) == expected, "binding Redo restores full snapshot");
                string state = workspace.ExportState();
                var restored = new AuthoringToolWorkspace(); restored.RestoreState(state);
                Check(restored.ExportState() == state, "all document selection and history state roundtrip");
                // 저장 전후 세대 경로를 제외한 내용과 실제 Undo/Redo 결과를 비교한다.
                workspace.Save(); Check(ContentSnapshotFingerprint.Compute(session.CreateSnapshot()) == expected, "save preserves content fingerprint");
                session.Undo(); session.Redo(); Check(ContentSnapshotFingerprint.Compute(session.CreateSnapshot()) == expected, "save retains usable Undo/Redo");
                workspace.Reload("Discard");
                Check(workspace.Session.SelectedLevelId == selected && ContentSnapshotFingerprint.Compute(workspace.Session.CreateSnapshot()) == expected, "reload preserves selected level and all authored values");
                using (var zip = new MemoryStream())
                {
                    ProjectPackageStore.Export(workspace.Folder, zip, CancellationToken.None); zip.Position = 0;
                    string imported = ProjectPackageStore.Import(zip, Path.Combine(root, "import"), "검사 사본", CancellationToken.None);
                    Check(ContentSnapshotFingerprint.Compute(new ContentSnapshotStore(imported).Read().Snapshot) == expected, "ZIP retains IDs references independent/shared settings and supply");
                    workspace.Open(imported, "Discard");
                    Check(workspace.Session.SelectedLevelId == selected, "ZIP replacement keeps existing selected level when its ID exists");
                }
                Debug.Log("PASS stage04 data flow " + root); EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
        private static void Check(bool value, string label)
        { if (!value) throw new Exception("FAIL " + label); Debug.Log("PASS " + label); }
    }
}
