using System;
using System.IO;
using System.Linq;
using LevelAuthoring.Editing;
using LevelAuthoring.Storage;
using Levels;
using UnityEditor;
using UnityEngine;

namespace LevelAuthoring.Editor
{
    public static class JsonWorkspaceVerification
    {
        public static void Run()
        {
            IDisposable workspace = null;
            try
            {
                var stored = new ContentSnapshotStore(File.ReadAllText("Logs/GameAuthoringStage02/latest-fixtures.txt")).Read();
                string levelId = stored.Snapshot.Documents.First(doc => doc.Kind == "level").Id;
                var session = new AuthoringEditSession(stored, levelId);
                Type type = typeof(JsonWorkspaceVerification).Assembly.GetType("LevelAuthoring.Editor.JsonAuthoringWorkspace");
                if (type == null) throw new Exception("JSON 표시 어댑터 미구현");
                int baseline = Resources.FindObjectsOfTypeAll<LevelDefinition>().Length;
                workspace = (IDisposable)Activator.CreateInstance(type, session);
                LevelDefinition level = (LevelDefinition)type.GetProperty("Level").GetValue(workspace);
                int moves = level.MoveCount;
                if (EditorUtility.IsPersistent(level)) throw new Exception("실제 에셋을 표시 사본으로 사용함");
                Undo.RecordObject(level, "표시 수정");
                JsonUtility.FromJsonOverwrite("{\"moveCount\":" + (moves + 1) + "}", level);
                type.GetMethod("Capture").Invoke(workspace, new object[] { "속성 변경" });
                if (!session.IsDirty || (int)session.Get(levelId).Data["moveCount"] != moves + 1) throw new Exception("사본 변경 수집 실패");
                type.GetMethod("Undo").Invoke(workspace, null);
                if (level.MoveCount != moves || session.IsDirty) throw new Exception("Undo 표시/문서 복원 실패");
                type.GetMethod("Redo").Invoke(workspace, null);
                if (level.MoveCount != moves + 1) throw new Exception("Redo 표시 복원 실패");
                bool failed = false;
                try { type.GetMethod("Execute").Invoke(workspace, new object[] { "실패", (Action)(() => { JsonUtility.FromJsonOverwrite("{\"moveCount\":999}", level); throw new InvalidOperationException("injected"); }) }); }
                catch (System.Reflection.TargetInvocationException) { failed = true; }
                if (!failed || level.MoveCount != moves + 1 || (int)session.Get(levelId).Data["moveCount"] != moves + 1) throw new Exception("실패 rollback 불일치");
                var register = type.GetMethod("Register");
                if (register == null) throw new Exception("공유 문서 트랜잭션 등록 미구현");
                string flowId = null;
                type.GetMethod("Execute").Invoke(workspace, new object[] { "공유 문서 생성", (Action)(() =>
                {
                    var flow = ScriptableObject.CreateInstance<Tutorial.TutorialFlowDefinition>();
                    flowId = (string)register.Invoke(workspace, new object[] { flow, "tutorialFlow" });
                    level.Tutorial.flow = flow;
                }) });
                if ((string)session.Get(levelId).Data["tutorial"]["flowId"] != flowId) throw new Exception("등록 문서 참조 저장 실패");
                type.GetMethod("Undo").Invoke(workspace, null);
                if (session.Documents.Any(doc => doc.Id == flowId)) throw new Exception("새 문서 단일 Undo 실패");
                type.GetMethod("Redo").Invoke(workspace, null);
                if (level.Tutorial.flow == null || (string)session.Get(levelId).Data["tutorial"]["flowId"] != flowId) throw new Exception("새 문서 Redo 참조 복원 실패");
                Tutorial.TutorialFlowDefinition rejectedFlow = null;
                try
                {
                    type.GetMethod("Execute").Invoke(workspace, new object[] { "등록 후 실패", (Action)(() =>
                    {
                        rejectedFlow = ScriptableObject.CreateInstance<Tutorial.TutorialFlowDefinition>();
                        register.Invoke(workspace, new object[] { rejectedFlow, "tutorialFlow" });
                        throw new InvalidOperationException("injected registration failure");
                    }) });
                }
                catch (System.Reflection.TargetInvocationException) { }
                if (rejectedFlow != null) throw new Exception("실패한 등록 사본 누수");
                workspace.Dispose(); workspace = null;
                if (Resources.FindObjectsOfTypeAll<LevelDefinition>().Length != baseline) throw new Exception("표시 사본 해제 누락");
                File.WriteAllText("Logs/GameAuthoringStage03/workspace-results.txt", "PASS transient display\nPASS capture\nPASS undo display\nPASS redo display\nPASS failed action rollback\nPASS display cleanup\n");
                EditorApplication.Exit(0);
            }
            catch (Exception error) { workspace?.Dispose(); Debug.LogException(error); EditorApplication.Exit(1); }
        }
    }
}