using System;
using System.IO;
using System.Reflection;
using System.Linq;
using LevelAuthoring.Documents;
using LevelAuthoring.Runtime;
using LevelAuthoring.Storage;
using Levels.Editor;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace LevelAuthoring.Editor
{
    public static class JsonLifecycleVerification
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        public static void Run()
        {
            LevelEditorWindow window = null;
            try
            {
                MethodInfo backup = typeof(LevelEditorWindow).GetMethod("OpenJsonBackup", Flags);
                if (backup == null) throw new Exception("JSON 이전 정상본 복구 UI 미구현");
                var template = ScriptableObject.CreateInstance<Levels.LevelDefinition>();
                ContentDocument level;
                try { level = UnityAuthoringCodec.Write(template, "level", "recovery-level", _ => null, _ => null); }
                finally { UnityEngine.Object.DestroyImmediate(template); }
                var project = new ContentDocument("project", "recovery-project", new JObject
                {
                    ["name"] = "recovery test", ["contentVersion"] = 1, ["defaultCatalogId"] = JValue.CreateNull(),
                    ["resources"] = new JArray(), ["documents"] = new JArray(), ["sourceIds"] = new JArray()
                });
                string root = Path.GetFullPath("ContentData/Trials/game-authoring-stage-03/recovery-" + Guid.NewGuid().ToString("N"));
                var store = new ContentSnapshotStore(root);
                var first = store.Publish(new ContentSnapshot(new[] { project, level }), null);
                level.Data["displayName"] = "second generation";
                store.Publish(new ContentSnapshot(new[] { project, level }), first.Hash);
                byte[] original = File.ReadAllBytes(Path.Combine(root, "project.json"));
                window = ScriptableObject.CreateInstance<LevelEditorWindow>(); window.CreateGUI();
                backup.Invoke(window, new object[] { root });
                if (window.CurrentLevel.name == "second generation") throw new Exception("백업 대신 현재본 로드");
                bool refused = false;
                try { typeof(LevelEditorWindow).GetMethod("SaveJsonWorkspace", Flags).Invoke(window, null); }
                catch (TargetInvocationException error) when (error.InnerException is ContentFormatException) { refused = true; }
                if (!refused) throw new Exception("백업이 현재 원본을 덮어쓸 수 있음");
                if (!Convert.ToBase64String(original).Equals(Convert.ToBase64String(File.ReadAllBytes(Path.Combine(root, "project.json"))))) throw new Exception("복구 원본 변경");
                var recovered = (JsonAuthoringWorkspace)typeof(LevelEditorWindow).GetField("jsonWorkspace", Flags).GetValue(window);
                recovered.Execute("미저장 시험", () => window.CurrentLevel.name = "pending");
                MethodInfo prepare = typeof(LevelEditorWindow).GetMethod("PrepareJsonPlayMode", Flags);
                if ((bool)prepare.Invoke(window, new object[] { false, (Func<int>)(() => 1) }) || !recovered.Session.IsDirty) throw new Exception("진입 취소의 변경 보존 실패");
                if (!(bool)prepare.Invoke(window, new object[] { true, (Func<int>)(() => throw new Exception("자체 시험 확인창 호출")) }) || !recovered.Session.IsDirty) throw new Exception("자체 시험의 미저장 보존 실패");
                if ((bool)prepare.Invoke(window, new object[] { false, (Func<int>)(() => 0) }) || !recovered.Session.IsDirty) throw new Exception("저장 실패 시 Play 진입 또는 변경 유실");
                if (!(bool)prepare.Invoke(window, new object[] { false, (Func<int>)(() => 2) }) || recovered.Session.IsDirty || window.CurrentLevel.name == "pending") throw new Exception("버리기 복원 실패");
                typeof(LevelEditorWindow).GetField("jsonBackupMode", Flags).SetValue(window, false);
                typeof(LevelEditorWindow).GetMethod("SelectCell", Flags).Invoke(window, new object[] { new Board.BoardCoordinate(3, 4) });
                typeof(LevelEditorWindow).GetMethod("CaptureJsonViewState", Flags).Invoke(window, null);
                typeof(LevelEditorWindow).GetMethod("OnDisable", Flags).Invoke(window, null);
                typeof(LevelEditorWindow).GetMethod("OnEnable", Flags).Invoke(window, null);
                window.CreateGUI();
                var workspace = (JsonAuthoringWorkspace)typeof(LevelEditorWindow).GetField("jsonWorkspace", Flags).GetValue(window);
                if (workspace.Session.SelectedCells.Length != 1 || workspace.Session.SelectedCells[0] != 31) throw new Exception("리로드 선택 셀 보존 실패");
                var selected = (Board.BoardCoordinate?)typeof(LevelEditorWindow).GetField("selected", Flags).GetValue(window);
                if (!selected.HasValue || selected.Value.Row != 3 || selected.Value.Column != 4) throw new Exception("리로드 선택 표시 실패");
                typeof(LevelEditorWindow).GetMethod("OpenJsonWorkspace", Flags).Invoke(window, new object[] { root });
                workspace = (JsonAuthoringWorkspace)typeof(LevelEditorWindow).GetField("jsonWorkspace", Flags).GetValue(window);
                workspace.Execute("저장 선택", () => window.CurrentLevel.name = "saved choice");
                if (!(bool)prepare.Invoke(window, new object[] { false, (Func<int>)(() => 0) }) || workspace.Session.IsDirty || (string)store.Read().Snapshot.Documents.Single(doc => doc.Kind == "level").Data["displayName"] != "saved choice") throw new Exception("저장 후 진입 실패");
                typeof(LevelEditorWindow).GetMethod("DuplicateJsonLevel", Flags).Invoke(window, new object[] { 919191 });
                if (!(bool)prepare.Invoke(window, new object[] { false, (Func<int>)(() => 2) }) || window.CurrentLevel.LevelNumber == 919191 || workspace.Session.IsDirty) throw new Exception("새 레벨 버리기 선택 복귀 실패");
                File.WriteAllText("Logs/GameAuthoringStage03/lifecycle-results.txt", "PASS backup recovery\nPASS backup overwrite blocked\nPASS source preserved\nPASS reload selection\nPASS cancel preserves draft\nPASS own test preserves draft\nPASS failed save blocks play\nPASS discard restores saved\nPASS save before play\nPASS discard new level selection\n");
                UnityEngine.Object.DestroyImmediate(window); window = null;
                EditorApplication.Exit(0);
            }
            catch (Exception error) { if (window != null) UnityEngine.Object.DestroyImmediate(window); Debug.LogException(error); EditorApplication.Exit(1); }
        }
    }
}
