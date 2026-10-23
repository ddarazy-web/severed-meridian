using System;
using System.IO;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using LevelAuthoring.Storage;
using LevelTool;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelAuthoring.Editor
{
    internal static class LevelToolNativeDeliveryExercise
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        internal static async UniTask Run(LevelToolScreen screen, RenderTexture texture, PanelSettings panel, string isolation)
        {
            var root = screen.GetComponent<UIDocument>().rootVisualElement;
            void Check(bool value, string message) { if (!value) throw new Exception("FAIL native-delivery " + message); Debug.Log("PASS native-delivery " + message); }
            void Click(string name)
            {
                var button = root.Q<Button>(name); Check(button != null && button.enabledInHierarchy, "enabled " + name);
                typeof(Clickable).GetMethod("Invoke", Flags).Invoke(button.clickable, new object[] { null });
            }
            var fixture = new ContentSnapshotStore(File.ReadAllText("Logs/GameAuthoringStage02/latest-fixtures.txt").Trim()).Read().Snapshot;
            screen.Workspace.Create(Path.Combine(isolation, "DeliveryProject"), fixture);
            var session = screen.Workspace.Session;
            session.EditLevel("미저장 시험", level => level["displayName"] = "되돌아올 편집본");
            session.SelectCells(new[] { 40 });
            typeof(LevelToolScreen).GetMethod("Refresh", Flags).Invoke(screen, null);
            await UniTask.DelayFrame(5);
            float zoomBefore = (float)typeof(LevelToolScreen).GetField("cellSize", Flags).GetValue(screen);
            object leftBefore = typeof(LevelToolScreen).GetField("leftPanelHidden", Flags).GetValue(screen);
            object rightBefore = typeof(LevelToolScreen).GetField("rightPanelHidden", Flags).GetValue(screen);
            string before = session.ExportState();
            var prefabField = typeof(LevelToolScreen).GetField("boardPrefab", Flags);
            object prefab = prefabField.GetValue(screen);
            try
            {
                prefabField.SetValue(screen, null);
                await screen.BeginPlay();
                Check(root.Q<Label>("trial-failure-message") != null, "trial failure has an actionable visible dialog");
                Check(root.Q<Label>("trial-failure-message").text.Contains("프리팹"), "failure dialog retains actual cause");
                Check(root.Q<Label>("trial-source-label").text.Contains("JSON"), "failure identifies actual trial input");
                Check(session.ExportState() == before && screen.TestSession == null, "failure preserves unsaved authoring and history");
                Click("trial-failure-edit");
                Check(root.Q("modal") == null && session.ExportState() == before, "edit recovery closes failure without changing work");
                await screen.BeginPlay();
                prefabField.SetValue(screen, prefab);
                Click("trial-failure-retry");
                float deadline = Time.realtimeSinceStartup + 60;
                while (Time.realtimeSinceStartup < deadline && (screen.TestSession == null || !screen.TestSession.IsReady))
                {
                    if (root.Q("trial-failure-message") != null) throw new Exception("retry failed: " + root.Q<Label>("trial-failure-message").text);
                    await UniTask.DelayFrame(1);
                }
                Check(screen.TestSession != null && screen.TestSession.IsReady && screen.TestSession.PlayContext.IsTest && !screen.TestSession.PlayContext.Tutorial.UsesPlayerProgress,
                    "retry after resource repair enters isolated test context");
                Check(root.Q<Button>("return-to-editor").text.Contains("JSON"), "running game keeps actual input source visible");
                screen.ReturnToEditor(); await UniTask.DelayFrame(2);
                Check(session.ExportState() == before, "retry and return preserve dirty document selection and history");
                Check((float)typeof(LevelToolScreen).GetField("cellSize", Flags).GetValue(screen) == zoomBefore &&
                    Equals(leftBefore, typeof(LevelToolScreen).GetField("leftPanelHidden", Flags).GetValue(screen)) &&
                    Equals(rightBefore, typeof(LevelToolScreen).GetField("rightPanelHidden", Flags).GetValue(screen)), "trial return preserves board zoom and panel visibility");
                var loading = screen.BeginPlay();
                Check(root.Q<Button>("trial-loading-cancel") != null, "loading has a visible cancellation action before preparation");
                Click("trial-loading-cancel"); await loading; await UniTask.DelayFrame(2);
                Check(screen.TestSession == null && root.Q("modal") == null && session.ExportState() == before,
                    "loading cancellation restores editor without modifying data or showing failure");
                loading = screen.BeginPlay();
                typeof(LevelToolScreen).GetMethod("HandleAndroidBack", Flags).Invoke(screen, null); await loading;
                Check(screen.TestSession == null && root.Q("modal") == null && session.ExportState() == before,
                    "Android back cancels loading instead of hiding its dialog");
                loading = screen.BeginPlay();
                var commands = typeof(LevelToolScreen).GetNestedType("ToolCommandId", BindingFlags.NonPublic);
                typeof(LevelToolScreen).GetMethod("TryExecuteCommand", Flags).Invoke(screen, new[] { Enum.Parse(commands, "Cancel") }); await loading;
                Check(screen.TestSession == null && root.Q("modal") == null && session.ExportState() == before,
                    "Escape command cancels loading and preserves work");
            }
            finally { prefabField.SetValue(screen, prefab); screen.ReturnToEditor(); }
            Click("save");
            await UniTask.WaitUntil(() => !(bool)typeof(LevelToolScreen).GetField("busy", Flags).GetValue(screen));
            Check(!session.IsDirty && new ContentSnapshotStore(screen.Workspace.Folder).Read().Snapshot.Get(session.SelectedLevelId).Data["displayName"].ToString() == "되돌아올 편집본",
                "explicit save writes current JSON and clears dirty state");
            string saveResult = root.Q<Label>("status").text;
            Check(saveResult.Contains("JSON 저장 완료") && saveResult.Contains(screen.Workspace.Folder),
                "save outcome names JSON and its actual project location");
            Click("save-status-details");
            Check(root.Q<Label>("save-details-policy").text.Contains("저장되어 있습니다"), "saved project is not described as memory-only");
            Check(root.Q<Label>("save-details-outcome")?.text == saveResult, "last file outcome remains accessible in save details");
            Click("close-save-details");
            string output = Path.GetFullPath("Logs/NativeLevelToolStage04/screens"); Directory.CreateDirectory(output);
            foreach (Vector2Int size in new[] { new Vector2Int(1280,800), new Vector2Int(1440,900), new Vector2Int(390,844), new Vector2Int(844,390), new Vector2Int(820,1180) })
            {
                panel.targetTexture = null; texture.Release(); texture.width = size.x; texture.height = size.y; texture.Create(); panel.targetTexture = texture;
                await UniTask.DelayFrame(5);
                if (size.x < 1000) typeof(LevelToolScreen).GetMethod("ApplyMobileViewport", Flags).Invoke(screen, new object[] { (Vector2)size, new Rect(0,0,size.x,size.y), new Rect() });
                await UniTask.DelayFrame(5);
                try { prefabField.SetValue(screen, null); await screen.BeginPlay(); }
                finally { prefabField.SetValue(screen, prefab); }
                var retry = root.Q<Button>("trial-failure-retry");
                retry.GetFirstAncestorOfType<ScrollView>()?.ScrollTo(retry); await UniTask.DelayFrame(3);
                Check(retry.worldBound.Overlaps(new Rect(0,0,size.x,size.y)) && retry.worldBound.height >= 28, "failure retry accessible " + size);
                Capture(texture, Path.Combine(output, "trial-failure-" + size.x + "x" + size.y + ".png"));
                Click("trial-failure-edit"); Click("save-status-details");
                var close = root.Q<Button>("close-save-details");
                close.GetFirstAncestorOfType<ScrollView>()?.ScrollTo(close); await UniTask.DelayFrame(3);
                Check(close.worldBound.Overlaps(new Rect(0,0,size.x,size.y)) && close.worldBound.height >= 28, "save details close accessible " + size);
                Capture(texture, Path.Combine(output, "save-details-" + size.x + "x" + size.y + ".png"));
                if (size.x == 390)
                {
                    typeof(LevelToolScreen).GetMethod("ApplyMobileViewport", Flags).Invoke(screen, new object[] { (Vector2)size, new Rect(0,0,size.x,size.y), new Rect(0,0,size.x,300) });
                    await UniTask.DelayFrame(3); close.GetFirstAncestorOfType<ScrollView>()?.ScrollTo(close); await UniTask.DelayFrame(3);
                    Check(close.worldBound.yMax <= size.y - 300 + 2, "keyboard safe area keeps save dialog close accessible");
                    panel.targetTexture = null; texture.Release(); texture.width = 844; texture.height = 390; texture.Create(); panel.targetTexture = texture;
                    typeof(LevelToolScreen).GetMethod("ApplyMobileViewport", Flags).Invoke(screen, new object[] { new Vector2(844,390), new Rect(0,0,844,390), new Rect() });
                    await UniTask.DelayFrame(5); close.GetFirstAncestorOfType<ScrollView>()?.ScrollTo(close); await UniTask.DelayFrame(3);
                    Check(root.Q<Label>("save-details-outcome")?.text == saveResult && close.worldBound.Overlaps(new Rect(0,0,844,390)),
                        "rotation keeps open dialog result and close action");
                }
                if (size.x == 1280)
                {
                    close.Focus(); await UniTask.DelayFrame(2);
                    var key = new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape };
                    using (var down = KeyDownEvent.GetPooled(key)) { down.target = close; close.SendEvent(down); }
                    await UniTask.DelayFrame(2);
                    Check(root.Q("modal") == null, "keyboard Escape closes save details");
                    continue;
                }
                Click("close-save-details");
            }
            panel.targetTexture = null; texture.Release(); texture.width = 1280; texture.height = 800; texture.Create(); panel.targetTexture = texture;
            await UniTask.DelayFrame(5);
            void Call(string method, params object[] args) => typeof(LevelToolScreen).GetMethod(method, Flags).Invoke(screen, args);
            async UniTask Idle()
            {
                await UniTask.WaitUntil(() => !(bool)typeof(LevelToolScreen).GetField("busy", Flags).GetValue(screen));
                await UniTask.DelayFrame(3);
            }
            Call("NewProject"); root.Q<TextField>("project-name").value = "제작 흐름 검사"; Click("create-project");
            string journeyFolder = Path.Combine(isolation, "JourneyProject");
            root.Q<TextField>("folder-path").value = journeyFolder; Click("choose-folder"); await Idle();
            Click("first-level"); Click("create-level"); await Idle();
            Check(screen.Workspace.Session.SelectedLevelId != null && screen.Workspace.Session.IsDirty, "journey creates project and first level through UI");
            Call("SelectWorkspacePage", "튜토리얼"); Click("tutorial-sample-board-menu");
            root.Q("modal").Q<DropdownField>().index = 2; Click("tutorial-create-sample-board");
            Call("SelectWorkspacePage", "튜토리얼"); Click("tutorial-begin-work");
            root.Q<Foldout>("tutorial-composition-details").value = true;
            const string instruction = "두 칸을 바꾸어 같은 토끼 세 마리를 맞춰요.";
            root.Q<TextField>("tutorial-instructions").value = instruction;
            string journeyId = screen.Workspace.Session.SelectedLevelId;
            string journeyBefore = screen.Workspace.Session.ExportState();
            await screen.BeginPlay();
            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(35)))
            {
                await UniTask.WaitUntil(() => screen.TestSession != null && screen.TestSession.IsReady && !screen.TestSession.IsPresenting && !screen.TestSession.HasProgressFeedback,
                    cancellationToken: timeout.Token);
                var game = screen.TestSession;
                Check(game.TutorialState.Instructions == instruction, "journey tests edited tutorial instructions");
                var guidance = game.TutorialState;
                Check(guidance.First.HasValue && guidance.Second.HasValue && game.TrySwap(guidance.First.Value, guidance.Second.Value), "journey performs instructed matching action");
                await UniTask.WaitUntil(() => game.TutorialState.State == Tutorial.TutorialProgressState.Completed && !game.IsPresenting && !game.HasProgressFeedback,
                    cancellationToken: timeout.Token);
            }
            screen.ReturnToEditor(); await UniTask.DelayFrame(3);
            Check(screen.Workspace.Session.ExportState() == journeyBefore, "journey actual matching preserves draft on return");
            Click("tutorial-work-complete");
            Check(typeof(LevelToolScreen).GetField("tutorialWork", Flags).GetValue(screen) == null && screen.Workspace.Session.IsDirty, "journey completion remains unsaved until explicit save");
            Click("save"); await Idle();
            Check(!screen.Workspace.Session.IsDirty, "journey saves completed tutorial explicitly");
            var transfer = new JourneyTransfer(); typeof(LevelToolScreen).GetField("fileTransfer", Flags).SetValue(screen, transfer);
            Call("ExportProject"); Click("export-project-confirm"); await Idle();
            Check(transfer.Bytes.Length > 0 && root.Q<Label>("status").text.Contains("ZIP 내보내기 완료"), "journey exports saved project ZIP");
            Call("ImportProject"); root.Q<TextField>("import-project-name").value = "제작 흐름 가져오기"; Click("import-project-confirm"); await Idle();
            Check(screen.Workspace.Session == null && screen.Workspace.Directory != null, "journey import opens listing without eager level loading");
            Call("RequestOpenLevel", journeyId, null); await Idle();
            Check((string)screen.Workspace.Session.Get(journeyId).Data["tutorial"]["steps"][0]["instructions"] == instruction && !screen.Workspace.Session.IsDirty,
                "journey imported selected level retains edited tutorial and saved state");
            Check(new ContentSnapshotStore(journeyFolder).Read().Snapshot.Get(journeyId).Data["tutorial"]["steps"][0]["instructions"].ToString() == instruction,
                "journey transfer leaves original saved project intact");
        }
        private sealed class JourneyTransfer : ILevelToolFileTransfer
        {
            private MemoryStream output;
            public byte[] Bytes => output.ToArray();
            public UniTask<Stream> OpenProject(CancellationToken token) => UniTask.FromResult<Stream>(new MemoryStream(Bytes));
            public UniTask<Stream> CreateProject(string suggestedName, CancellationToken token)
            { output = new MemoryStream(); return UniTask.FromResult<Stream>(output); }
        }
        private static void Capture(RenderTexture target, string path)
        {
            RenderTexture old = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            try { image.ReadPixels(new Rect(0,0,target.width,target.height),0,0); image.Apply(); File.WriteAllBytes(path,image.EncodeToPNG()); }
            finally { RenderTexture.active = old; UnityEngine.Object.Destroy(image); }
        }
    }
}
