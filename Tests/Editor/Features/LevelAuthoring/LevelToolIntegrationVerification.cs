using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using LevelAuthoring.Documents;
using LevelAuthoring.Editing;
using LevelAuthoring.Runtime;
using LevelAuthoring.Storage;
using LevelTool;
using Levels;
using Newtonsoft.Json.Linq;
using Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelAuthoring.Editor
{
    public static class LevelToolIntegrationVerification
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private static LevelToolScreen screen;
        private static bool priorEnabled, started;
        private static EnterPlayModeOptions priorOptions;
        private static string directory;
        private static int exit;
        private static double deadline;
        private static VisualElement Root => screen.GetComponent<UIDocument>().rootVisualElement;

        public static void Run()
        {
            directory = Path.GetFullPath("Logs/AndroidLevelToolStage05/integration-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            priorEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            priorOptions = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorSceneManager.OpenScene(LevelTool.Editor.LevelToolAssets.ScenePath);
            LevelToolRecoveryIsolation.Configure(Path.Combine(directory, "Recovery"));
            started = false; exit = 0; deadline = EditorApplication.timeSinceStartup + 240;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += State;
            EditorApplication.EnterPlaymode();
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying || started) return;
            if (EditorApplication.timeSinceStartup > deadline) { Finish(new Exception("Integration startup timeout")); return; }
            screen = UnityEngine.Object.FindFirstObjectByType<LevelToolScreen>();
            if (screen == null || (bool)typeof(LevelToolScreen).GetField("busy", Flags).GetValue(screen)) return;
            started = true;
            Exercise().Forget(Finish);
        }

        private static async UniTask Exercise()
        {
            string fixtureFolder = File.ReadAllText("Logs/GameAuthoringStage02/latest-fixtures.txt");
            var fixtureStore = new ContentSnapshotStore(fixtureFolder);
            var fixture = fixtureStore.Read();
            Check(screen.Workspace.Create(Path.Combine(directory, "Project"), fixture.Snapshot), "isolated fixture clone");
            var session = screen.Workspace.Session;
            string levelId = TutorialDraftEditing.AddSampleBoard(session, "two");
            session.SelectLevel(levelId);
            var definitions = LevelToolDocuments.Definitions(session);
            string generator = definitions.First(pair => (string)pair.Value["reaction"] == "GeneratorCharge").Key;
            string body = definitions.First(pair => (string)pair.Value["reaction"] == "Durability" &&
                LevelToolDocuments.Size(pair.Value) == 2 && (string)pair.Value["removal"]?["kind"] != "Scrap").Key;
            string floor = definitions.First(pair => LevelToolDocuments.Layer(pair.Value) == "Dust").Key;
            string cover = definitions.First(pair => LevelToolDocuments.Layer(pair.Value) == "Cover" &&
                (string)pair.Value["layer"]?["behavior"] == "CoverDurability").Key;
            // 복합 시험 입력은 공통 편집 명령으로 만든다. 이 준비를 실제 터치 입력 검사로 보고하지 않는다.
            session.Apply("통합 검사 복합 배치", docs =>
            {
                JObject level = docs[levelId].Data;
                LevelDocumentEditing.Place(level, definitions, generator, 2, 0, true);
                LevelDocumentEditing.Place(level, definitions, body, 2, 6, true);
                LevelDocumentEditing.Place(level, definitions, floor, 7, 0, false);
                LevelDocumentEditing.Place(level, definitions, cover, 7, 0, false);
                string generatorId = (string)level["elements"].Single(item => (string)item["definitionId"] == generator)["instanceId"];
                string targetId = (string)level["elements"].Single(item => (string)item["definitionId"] == body)["instanceId"];
                ConnectionDocumentEditing.Add(level, definitions, generatorId, targetId);
                ConnectionDocumentEditing.SetWire(level, definitions, 0, new[] { 22, 23, 24, 25, 26 });
            });
            session.SelectCells(new[] { 40 }); Refresh(); await UniTask.Yield();
            Click("board-touch-select");
            JObject touchLevel = session.Get(levelId).Data;
            var originalBlock = touchLevel["elements"].Single(item => (string)item["layer"] == "Block" &&
                (int)item["coordinate"]["row"] == 1 && (int)item["coordinate"]["column"] == 1);
            string normal = (string)originalBlock["definitionId"];
            string touchColor = touchLevel["colors"].Values<string>().First(value => value != (string)originalBlock["color"]);
            typeof(LevelToolScreen).GetField("layer", Flags).SetValue(screen, "Block");
            typeof(LevelToolScreen).GetField("brush", Flags).SetValue(screen, normal);
            typeof(LevelToolScreen).GetField("color", Flags).SetValue(screen, touchColor);
            Refresh();
            Root.Q<Toggle>("board-touch-replace").value = true;
            await UniTask.Yield(); await UniTask.Yield();
            var boardScroll = Root.Q<ScrollView>("board-scroll");
            var touchCell = Root.Q("cell-10");
            boardScroll.ScrollTo(touchCell);
            await UniTask.Yield(); await UniTask.Yield();
            Vector2 touchPoint = touchCell.worldBound.center;
            Check(touchCell.visible && touchCell.worldBound.width > 0 && boardScroll.contentViewport.worldBound.Contains(touchPoint), "continuous integration touch target is visible inside board viewport");
            JObject beforeTouch = session.Get(levelId).Data;
            JObject expectedTouch = (JObject)beforeTouch.DeepClone();
            LevelDocumentEditing.Place(expectedTouch, definitions, normal, 1, 1, true, color: touchColor);
            Check(!JToken.DeepEquals(beforeTouch, expectedTouch), "continuous integration touch changes a safe normal block");
            // 풀링한 터치 이벤트가 제품 보드 콜백을 통과한다. 물리 기기 입력 백엔드는 이 검사 범위 밖이다.
            using (PointerDownEvent down = PointerDownEvent.GetPooled(new Event { type = EventType.MouseDown, button = 0, mousePosition = touchPoint }))
            {
                down.GetType().GetProperty("pointerType").SetValue(down, UnityEngine.UIElements.PointerType.touch);
                down.GetType().GetProperty("pointerId").SetValue(down, 1);
                down.GetType().GetProperty("isPrimary").SetValue(down, true);
                down.target = touchCell; touchCell.SendEvent(down);
            }
            Check(JToken.DeepEquals(beforeTouch, session.Get(levelId).Data) && Root.HasPointerCapture(1), "touch down captures pointer without publishing an edit");
            using (PointerUpEvent up = PointerUpEvent.GetPooled(new Event { type = EventType.MouseUp, button = 0, mousePosition = touchPoint }))
            {
                up.GetType().GetProperty("pointerType").SetValue(up, UnityEngine.UIElements.PointerType.touch);
                up.GetType().GetProperty("pointerId").SetValue(up, 1);
                up.GetType().GetProperty("isPrimary").SetValue(up, true);
                up.target = touchCell; touchCell.SendEvent(up);
            }
            Check(JToken.DeepEquals(expectedTouch, session.Get(levelId).Data) && !Root.HasPointerCapture(1), "touch release produces exactly the common placement command JSON");
            Click("undo"); Check(JToken.DeepEquals(beforeTouch, session.Get(levelId).Data), "continuous touch edit has one shared UI undo");
            Click("redo"); Check(JToken.DeepEquals(expectedTouch, session.Get(levelId).Data), "continuous touch edit has one shared UI redo");
            typeof(LevelToolScreen).GetField("brush", Flags).SetValue(screen, null);
            Root.Q<Toggle>("board-touch-replace").value = false;
            // 제품 UI 콜백으로 편집·이력·저장 연결을 검사한다.
            typeof(LevelToolScreen).GetField("levelSettingsVisible", Flags).SetValue(screen, true);
            Refresh();
            Root.Q<TextField>("level-name").value = "5단계 복합 통합 검사";
            string current = session.Get(levelId).Data.ToString();
            Click("undo"); Check(session.Get(levelId).Data.ToString() != current, "UI undo changes authored name");
            Click("redo"); Check(session.Get(levelId).Data.ToString() == current, "UI redo restores authored name");
            Click("save"); await Idle();
            Check(!session.IsDirty, "UI save publishes JSON");
            var saved = new ContentSnapshotStore(screen.Workspace.Folder).Read();
            Check(JToken.DeepEquals(saved.Snapshot.Get(levelId).Data, session.Get(levelId).Data), "saved selected level JSON meaning");
            CompareExecution(saved.Snapshot, levelId);
            string before = session.ExportState(), folder = screen.Workspace.Folder;
            await screen.BeginPlay();
            var game = screen.TestSession;
            Check(game != null, "real game starts from saved composite level");
            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45)))
                await UniTask.WaitUntil(() => game.HasFailed || game.IsReady && !game.IsPresenting && !game.HasProgressFeedback &&
                    (game.Phase == BoardActionPhase.Ready || game.Phase == BoardActionPhase.Stopped), cancellationToken: timeout.Token);
            Check(!game.HasFailed && game.State != null && game.PlayContext.IsTest, "real game board and isolated test context");
            Check(game.State.Connections.Count == 1 && game.State.Obstacles.Any(item => LevelPlacementRules.Size(item.Definition.Kind) == 2) &&
                game.State.Cells.Any(cell => cell.DustDurability > 0) && game.State.Cells.Any(cell => cell.Cover.HasValue), "real game retains 2x2 floor cover and connection");
            screen.ReturnToEditor(); await UniTask.Yield(); await UniTask.Yield();
            Check(ReferenceEquals(session, screen.Workspace.Session) && session.ExportState() == before && screen.Workspace.Folder == folder,
                "game return preserves project level selection and edit history");
            session.Undo(); Check(session.IsDirty, "undo survives game return");
            session.Redo(); Check(session.ExportState() == before, "redo survives game return");
            var transfer = new MemoryTransfer();
            typeof(LevelToolScreen).GetField("fileTransfer", Flags).SetValue(screen, transfer);
            session.Apply("통합 검사 잘못된 입력", docs => docs[levelId].Data["moveCount"] = -1);
            string invalid = session.ExportState(); Refresh();
            Click("save"); await Idle();
            Check(session.ExportState() == invalid && session.IsDirty && !string.IsNullOrEmpty(Root.Q<Label>("status").text), "invalid save preserves draft and reports failure");
            await screen.BeginPlay();
            Check(screen.TestSession == null && session.ExportState() == invalid, "invalid trial preserves draft");
            Execute("ExportProject"); await Idle();
            Check(transfer.CreateCalls == 0 && session.ExportState() == invalid && new ContentSnapshotStore(folder).Read().Hash == saved.Hash,
                "invalid export never opens output or overwrites saved JSON");
            session.Undo(); Refresh();
            Execute("ExportProject"); await Idle();
            Check(transfer.Output != null && !transfer.Output.CanWrite && Root.Q<Label>("status").text.Contains("ZIP 내보내기 완료"), "UI ZIP export closes output and completes");
            var exportedSnapshot = new ContentSnapshotStore(folder).Read().Snapshot;
            Check(saved.Snapshot.Documents.Select(document => document.Id).OrderBy(id => id)
                .SequenceEqual(exportedSnapshot.Documents.Select(document => document.Id).OrderBy(id => id)), "export preserves complete document ID set");
            foreach (var document in saved.Snapshot.Documents)
            {
                var exportedDocument = exportedSnapshot.Get(document.Id);
                if (document.Kind != "project")
                    Check(ContentJson.Write(document) == ContentJson.Write(exportedDocument), "export save preserves authored document " + document.Id);
                else
                {
                    // 내보내기의 내부 저장은 새 세대 경로를 만든다. ID 목록과 나머지 프로젝트 필드는 그대로 비교한다.
                    Check(document.Data["documents"].Select(entry => (string)entry["id"])
                        .SequenceEqual(exportedDocument.Data["documents"].Select(entry => (string)entry["id"])), "export save preserves project manifest IDs and order");
                    JObject expectedProject = (JObject)document.Data.DeepClone();
                    var expectedEntries = (JArray)expectedProject["documents"];
                    var exportedEntries = (JArray)exportedDocument.Data["documents"];
                    for (int index = 0; index < expectedEntries.Count; index++)
                        expectedEntries[index]["path"] = exportedEntries[index]["path"].DeepClone();
                    Check(JToken.DeepEquals(expectedProject, exportedDocument.Data), "export save changes only manifest generation paths");
                }
            }
            transfer.Bytes = transfer.Output.ToArray();
            Execute("ImportProject");
            Root.Q<TextField>("import-project-name").value = "통합 왕복";
            Click("import-project-confirm"); await Idle();
            Check(transfer.Input != null && !transfer.Input.CanRead && !ReferenceEquals(session, screen.Workspace.Session), "UI import opens a new internal project and closes input");
            string imported = screen.Workspace.Folder;
            Check(imported != folder && Path.GetFileName(imported) == "통합 왕복", "new internal project identity");
            screen.Workspace.Session.SelectLevel(levelId); Refresh();
            Execute("Reload"); await Idle();
            var reopened = screen.Workspace.Session.CreateSnapshot();
            foreach (var document in exportedSnapshot.Documents)
                Check(ContentJson.Write(document) == ContentJson.Write(reopened.Get(document.Id)), "ZIP and reopen preserve document ID references and meaning " + document.Id);
            CompareExecution(reopened, levelId);
            Check(fixtureStore.Read().Hash == fixture.Hash, "original fixture remains unchanged");
            Debug.Log("PASS LevelToolIntegrationVerification: common fixture preparation, actual touch event edit and UI undo redo save, real game return, simulated stream ZIP import and reopen; physical transfer and device touch remain unverified");
            Finish(null);
        }

        private static void CompareExecution(ContentSnapshot snapshot, string levelId)
        {
            string source = AuthoringTrialSource.Encode(snapshot, levelId);
            var restoredSource = AuthoringTrialSource.Decode(source);
            Check(AuthoringTrialSource.Encode(restoredSource.Snapshot, restoredSource.LevelId) == source, "authoring trial source preserves JSON IDs references and selected level");
            using var graph = new AuthoringObjectGraph(snapshot.Documents);
            var direct = (LevelDefinition)graph.Resolve(levelId);
            var packed = LevelPackCodec.ReadLevel(LevelPackCodec.Snapshot(direct), direct.LevelNumber);
            try
            {
                const int seed = 12345;
                var a = LevelStateBuilder.Build(direct, seed); var b = LevelStateBuilder.Build(packed, seed);
                Check(a.IsBuilt && b.IsBuilt, "JSON graph and MemoryPack build actual initial state");
                Check(Snapshot(a.State) == Snapshot(b.State), "same seed JSON and MemoryPack complete initial state meaning");
                Check(a.State.Cells.Any(cell => cell.Content == RuntimeContent.Normal) && a.State.Cells.Any(cell => cell.Content >= RuntimeContent.Rocket && cell.Content <= RuntimeContent.Magnet), "representative normal and power blocks");
                Check(direct.HasTutorial && packed.HasTutorial, "tutorial survives MemoryPack boundary");
                var left = new BoardActionExecutor(a.State); var right = new BoardActionExecutor(b.State);
                var action = ActionQuery.Find(left.State).First(candidate => candidate.Second.HasValue);
                var x = left.Swap(action.First, action.Second.Value); var y = right.Swap(action.First, action.Second.Value);
                Check(x.IsApplied && y.IsApplied && Snapshot(x) == Snapshot(y), "identical accepted action result across JSON MemoryPack");
                int rounds = 0;
                while (left.HasPendingCascade || right.HasPendingCascade)
                {
                    Check(left.HasPendingCascade == right.HasPendingCascade && ++rounds <= left.CascadeLimit + 2, "cascade phase and bounded progress");
                    Check(Snapshot(left.AdvanceCascade()) == Snapshot(right.AdvanceCascade()), "same cascade result " + rounds);
                }
                Check(left.Phase == right.Phase && Snapshot(left.State) == Snapshot(right.State), "same settled board moves missions supply random and result meaning");
            }
            finally { UnityEngine.Object.Destroy(packed); }
        }

        private static string Snapshot(object value) => (string)typeof(Levels.Editor.LevelInitialStateVerification)
            .GetMethod("SnapshotWithDefinitions", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new[] { value });
        private static void Refresh() => typeof(LevelToolScreen).GetMethod("Refresh", Flags).Invoke(screen, null);
        private static async UniTask Idle()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(40));
            await UniTask.WaitUntil(() => !(bool)typeof(LevelToolScreen).GetField("busy", Flags).GetValue(screen), cancellationToken: timeout.Token);
            await UniTask.Yield(); await UniTask.Yield();
        }
        private static void Execute(string name)
        {
            Click("menu-file"); Click("command-" + name);
        }
        private static void Click(string name)
        {
            var button = Root.Q<Button>(name) ?? throw new Exception("Missing integration UI button " + name);
            Check(button.enabledInHierarchy, "enabled UI control " + name);
            typeof(Clickable).GetMethod("Invoke", Flags).Invoke(button.clickable, new object[] { null });
        }
        private static void Check(bool value, string message) { if (!value) throw new Exception("FAIL " + message); Debug.Log("PASS " + message); }
        private static void Finish(Exception error)
        {
            if (error != null) { exit = 1; Debug.LogException(error); }
            EditorApplication.update -= Tick;
            if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode(); else State(PlayModeStateChange.EnteredEditMode);
        }
        private static void State(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= State;
            EditorSettings.enterPlayModeOptionsEnabled = priorEnabled; EditorSettings.enterPlayModeOptions = priorOptions;
            EditorApplication.Exit(exit);
        }
        private sealed class MemoryTransfer : ILevelToolFileTransfer
        {
            public MemoryStream Output, Input;
            public byte[] Bytes;
            public int CreateCalls;
            public UniTask<Stream> CreateProject(string name, CancellationToken token) { token.ThrowIfCancellationRequested(); CreateCalls++; Output = new MemoryStream(); return UniTask.FromResult<Stream>(Output); }
            public UniTask<Stream> OpenProject(CancellationToken token) { token.ThrowIfCancellationRequested(); Input = new MemoryStream(Bytes, false); return UniTask.FromResult<Stream>(Input); }
        }
    }
}
