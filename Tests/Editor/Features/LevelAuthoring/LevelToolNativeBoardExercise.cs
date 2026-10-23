using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using LevelAuthoring.Storage;
using LevelTool;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelAuthoring.Editor
{
    internal static class LevelToolNativeBoardExercise
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        internal static async UniTask Run(LevelToolScreen screen, RenderTexture texture, PanelSettings panel, string isolation, bool baseline)
        {
            string output = Path.GetFullPath("Logs/NativeBoardChecks"); Directory.CreateDirectory(output);
            string mode = baseline ? "baseline" : "final";
            VisualElement root = screen.GetComponent<UIDocument>().rootVisualElement;
            void Call(string method, params object[] args) => typeof(LevelToolScreen).GetMethod(method, Flags).Invoke(screen, args);
            object Get(string field) => typeof(LevelToolScreen).GetField(field, Flags).GetValue(screen);
            void Check(bool ok, string message) { if (!ok) throw new Exception("native-board " + message); Debug.Log("PASS native-board " + message); }
            void Click(string name)
            {
                var button = root.Q<Button>(name); Check(button != null && button.enabledInHierarchy, "enabled " + name);
                typeof(Clickable).GetMethod("Invoke", Flags).Invoke(button.clickable, new object[] { null });
            }
            var fixture = new ContentSnapshotStore(File.ReadAllText("Logs/GameAuthoringStage02/latest-fixtures.txt").Trim()).Read().Snapshot;
            screen.Workspace.Create(Path.Combine(isolation, "BoardProject"), fixture);
            Call("Refresh"); await UniTask.DelayFrame(20);
            var session = screen.Workspace.Session;
            string before = session.ExportState();
            int reads = (int)Get("displayDocumentReadCount");
            long allocation = GC.GetAllocatedBytesForCurrentThread();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 100; i++) Call("Refresh");
            watch.Stop(); allocation = GC.GetAllocatedBytesForCurrentThread() - allocation;
            File.WriteAllText(Path.Combine(output, mode + "-refresh.txt"),
                $"100 unchanged Refresh calls: {watch.Elapsed.TotalMilliseconds:F3} ms, {allocation} bytes, additional document reads={(int)Get("displayDocumentReadCount") - reads}\n");
            Check(session.ExportState() == before && (int)Get("displayDocumentReadCount") == reads, "unchanged refresh preserves state without document reads");
            int[] selected = session.SelectedCells;
            watch.Restart();
            for (int i = 0; i < 100; i++) { session.SelectCells(new[] { i % 81 }); Call("Refresh"); }
            watch.Stop();
            double selectionMs = watch.Elapsed.TotalMilliseconds;
            session.SelectCells(selected); Call("Refresh");
            bool initialMaterials = (bool)Get("materialsVisible");
            watch.Restart();
            for (int i = 0; i < 100; i++)
            {
                typeof(LevelToolScreen).GetField("materialsVisible", Flags).SetValue(screen, i % 2 == 0);
                Call("UpdatePanelVisibility");
            }
            watch.Stop();
            typeof(LevelToolScreen).GetField("materialsVisible", Flags).SetValue(screen, initialMaterials);
            Call("UpdatePanelVisibility");
            File.WriteAllText(Path.Combine(output, mode + "-interaction.txt"),
                $"100 selection/Refresh calls: {selectionMs:F3} ms\n100 list/material panel changes: {watch.Elapsed.TotalMilliseconds:F3} ms\nAdditional display document reads: {(int)Get("displayDocumentReadCount") - reads}\nAllocation counter unsupported/unverified; zero is not an allocation claim.\n");
            Check(session.ExportState() == before, "selection and panel changes preserve documents/history after restoring selection");
            Capture(texture, Path.Combine(output, mode + "-1280x800.png"));
            if (baseline) return;
            Check(root.Q("board-scroll").worldBound.yMin <= 180, "compact top reserves board height");
            Click("sidebar-materials");
            Check(root.Q<TextField>("material-search") != null && root.Q<DropdownField>("material-category") != null, "material search and categories");
            var category = root.Q<DropdownField>("material-category");
            var search = root.Q<TextField>("material-search");
            category.value = "장애물";
            await UniTask.DelayFrame(30);
            var materials = root.Q<ScrollView>("palette").Query<Button>().ToList();
            float previewDeadline = Time.realtimeSinceStartup + 15;
            while (Time.realtimeSinceStartup < previewDeadline && materials.Any(item => item.Q<Image>("material-image")?.sprite == null)) await UniTask.DelayFrame(1);
            Capture(texture, Path.Combine(output, "material-diagnostic.png"));
            Check(materials.Count > 0 && materials.All(item => item.Q<Image>("material-image")?.sprite != null), "obstacle materials use actual sprites; missing=" + string.Join(",", materials.Where(item => item.Q<Image>("material-image")?.sprite == null).Select(item => item.name + ":" + item.tooltip)));
            string materialId = materials[0].name;
            search.value = materialId;
            Check(root.Q<ScrollView>("palette").Query<Button>().ToList().Any(item => item.name == materialId), "material ID search");
            Click(materialId);
            Check((string)Get("layer") == "Obstacle" && (string)Get("brush") == materialId, "material chooses editing layer");
            Check(root.Q<Toggle>("board-touch-replace").value, "material selection enables same-layer replacement");
            search.value = ""; category.value = "최근 사용";
            Check(root.Q<ScrollView>("palette").Query<Button>().ToList().First().name == materialId, "recent material selection");
            Check(session.ExportState() == before, "material selection and search do not edit data");
            category.value = "전체";
            materials = root.Q<ScrollView>("palette").Query<Button>().ToList();
            previewDeadline = Time.realtimeSinceStartup + 15;
            while (Time.realtimeSinceStartup < previewDeadline && materials.Any(item => item.Q<Image>("material-image")?.sprite == null)) await UniTask.DelayFrame(1);
            Check(materials.All(item => item.Q<Image>("material-image")?.sprite != null), "all material categories have previews");
            await UniTask.DelayFrame(2);
            Capture(texture, Path.Combine(output, "final-materials-1280x800.png"));
            string dataBefore = session.Get(session.SelectedLevelId).Data.ToString();
            Call("SelectMaterial", "cover.web");
            Call("ExecuteBoardCell", 40, false, true, false);
            string covered = session.Get(session.SelectedLevelId).Data.ToString();
            Check(covered != dataBefore, "cover placement changes selected layer");
            Call("OpenBoardActions");
            Check(root.Q("board-clear-layer-Block") != null && root.Q("board-clear-layer-Cover") != null, "context menu identifies occupied layers");
            Click("board-clear-layer-Cover");
            Check(session.Get(session.SelectedLevelId).Data.ToString() == dataBefore, "context layer clear preserves underlying block");
            Click("undo");
            Check(session.Get(session.SelectedLevelId).Data.ToString() == covered, "context layer clear undo restores cover");
            Click("board-touch-erase"); Call("ExecuteBoardCell", 40, false, false, false);
            Check(session.Get(session.SelectedLevelId).Data.ToString() == dataBefore, "eraser preserves underlying block");
            Click("undo");
            Check(session.Get(session.SelectedLevelId).Data.ToString() == covered, "eraser undo restores cover");
            Call("ConfirmClearBoardCells"); Click("cancel");
            Check(session.Get(session.SelectedLevelId).Data.ToString() == covered, "all-layer clear cancellation preserves data");
            Call("ConfirmClearBoardCells"); Click("confirm-clear-all-layers");
            Check(!session.Get(session.SelectedLevelId).Data["elements"].Any(item => (int)item["coordinate"]["row"] == 4 && (int)item["coordinate"]["column"] == 4), "confirmed all-layer clear removes block and cover");
            Click("undo");
            Check(session.Get(session.SelectedLevelId).Data.ToString() == covered, "all-layer clear is one undo");
            Click("undo");
            Check(session.Get(session.SelectedLevelId).Data.ToString() == dataBefore, "placement undo restores original data");
            Click("inspector-level");
            var nameInput = root.Q<TextField>("level-name");
            nameInput.Focus(); await UniTask.DelayFrame(2);
            var textInput = nameInput.Q<TextElement>("unity-text-input") ?? nameInput.Query<TextElement>().ToList().Last();
            ((INotifyValueChanged<string>)textInput).SetValueWithoutNotify("미확정 패널 입력");
            nameInput.SelectRange(1, 4);
            string pendingState = session.ExportState();
            foreach (int toggle in new[] { 0, 1 })
            {
                var panelButton = root.Q<Button>("toggle-right-panel");
                Event pointer = new Event { type = EventType.MouseDown, button = 0, mousePosition = panelButton.worldBound.center };
                using (PointerDownEvent evt = PointerDownEvent.GetPooled(pointer)) { evt.target = panelButton; panelButton.SendEvent(evt); }
                pointer.type = EventType.MouseUp;
                using (PointerUpEvent evt = PointerUpEvent.GetPooled(pointer)) { evt.target = panelButton; panelButton.SendEvent(evt); }
                await UniTask.DelayFrame(3);
                Check(session.ExportState() == pendingState && nameInput.text == "미확정 패널 입력", "desktop panel pointer preserves delayed draft " + toggle);
            }
            nameInput.SetValueWithoutNotify((string)session.Get(session.SelectedLevelId).Data["displayName"]); nameInput.Blur();
            Click("toggle-left-panel"); Check(root.Q("left-panel").resolvedStyle.display == DisplayStyle.None, "left panel can hide");
            Click("toggle-left-panel");
            Click("board-focus-toggle"); Check(root.Q("undo").worldBound.height > 0 && root.Q("undo").visible, "focus retains undo");
            Click("board-focus-toggle");
            Call("ZoomBoard", 100f, root.Q<ScrollView>("board-scroll").contentViewport.worldBound.center, null);
            Click("board-touch-pan");
            await UniTask.DelayFrame(4);
            var viewport = root.Q<ScrollView>("board-scroll");
            viewport.scrollOffset = new Vector2(50, 50);
            await UniTask.DelayFrame(2);
            Vector2 dragStart = viewport.contentViewport.worldBound.center;
            Vector2 scrollBefore = viewport.scrollOffset;
            int[] selectionBefore = session.SelectedCells.ToArray();
            Event source = new Event { type = EventType.MouseDown, button = 0, mousePosition = dragStart };
            using (PointerDownEvent evt = PointerDownEvent.GetPooled(source)) { evt.target = root.Q("board"); root.Q("board").SendEvent(evt); }
            source.type = EventType.MouseDrag; source.mousePosition = dragStart - new Vector2(40, 40);
            using (PointerMoveEvent evt = PointerMoveEvent.GetPooled(source)) { evt.target = root; root.SendEvent(evt); }
            source.type = EventType.MouseUp;
            using (PointerUpEvent evt = PointerUpEvent.GetPooled(source)) { evt.target = root; root.SendEvent(evt); }
            Check(viewport.scrollOffset.y > scrollBefore.y + 20 && session.SelectedCells.SequenceEqual(selectionBefore), "mouse pan moves viewport without editing selection");
            Click("board-touch-select"); Click("board-fit");
            await UniTask.DelayFrame(3);
            Vector2 headerSize = root.Q("header").worldBound.size;
            float wheelSize = (float)Get("cellSize");
            using (WheelEvent evt = WheelEvent.GetPooled(new Event { type = EventType.ScrollWheel,
                delta = new Vector2(0, -3), mousePosition = viewport.contentViewport.worldBound.center, modifiers = EventModifiers.Control }))
            { evt.target = root.Q("board"); root.Q("board").SendEvent(evt); }
            await UniTask.DelayFrame(3);
            Check((float)Get("cellSize") > wheelSize && Vector2.Distance(headerSize, root.Q("header").worldBound.size) < .1f,
                "Ctrl wheel enlarges only board and preserves header geometry");
            wheelSize = (float)Get("cellSize");
            VisualElement outsideBoard = root.Q<ScrollView>("palette").contentViewport;
            using (WheelEvent evt = WheelEvent.GetPooled(new Event { type = EventType.ScrollWheel,
                delta = new Vector2(0, -3), mousePosition = outsideBoard.worldBound.center, modifiers = EventModifiers.Control }))
            { evt.target = outsideBoard; outsideBoard.SendEvent(evt); }
            Check((float)Get("cellSize") == wheelSize, "wheel outside board never changes board zoom");
            Click("board-fit");
            Click("board-focus-toggle"); Click("board-touch-materials");
            Check(root.Q("left-panel").resolvedStyle.display != DisplayStyle.None, "placement opens material panel from focus mode");
            Click("toggle-right-panel"); Call("OpenBoardActions"); Click("board-properties");
            Check(root.Q("right-panel").resolvedStyle.display != DisplayStyle.None, "context properties opens hidden inspector");
            Click("board-focus-toggle"); Click("toggle-right-panel");
            Check(root.Q("right-panel").resolvedStyle.display != DisplayStyle.None, "panel toggle opens inspector from focus mode");
            Click("board-focus-toggle");
            await UniTask.DelayFrame(2);
            VisualElement title = root.Q("level-title");
            Event titlePointer = new Event { type = EventType.MouseDown, button = 0, mousePosition = title.worldBound.center };
            using (PointerDownEvent evt = PointerDownEvent.GetPooled(titlePointer)) { evt.target = title; title.SendEvent(evt); }
            titlePointer.type = EventType.MouseUp;
            using (PointerUpEvent evt = PointerUpEvent.GetPooled(titlePointer)) { evt.target = title; title.SendEvent(evt); }
            await UniTask.DelayFrame(2);
            Check(root.Q("left-panel").resolvedStyle.display != DisplayStyle.None && !(bool)Get("materialsVisible"), "level title opens list from focus mode");
            LevelAuthoring.Runtime.TutorialDraftEditing.CreateFlow(session, "격리 공유 검사");
            var shared = new LevelAuthoring.Runtime.SharedTutorialDraft(session);
            typeof(LevelToolScreen).GetField("sharedTutorialDraft", Flags).SetValue(screen, shared);
            Call("SetBoardTouchTool", "erase");
            string sharedBaseline = session.ExportState();
            Call("ExecuteBoardCell", 40, false, false, false);
            Check(session.ExportState() == sharedBaseline, "shared draft rejects erase on parent level");
            typeof(LevelToolScreen).GetField("sharedTutorialDraft", Flags).SetValue(screen, null);
            Call("Refresh"); Click("inspector-level");
            nameInput = root.Q<TextField>("level-name"); nameInput.Focus(); await UniTask.DelayFrame(2);
            textInput = nameInput.Q<TextElement>("unity-text-input") ?? nameInput.Query<TextElement>().ToList().Last();
            ((INotifyValueChanged<string>)textInput).SetValueWithoutNotify("숨긴 입력 저장 검사");
            Call("ToggleEditorPanel", false); await UniTask.DelayFrame(2);
            Click("save"); await UniTask.DelayFrame(10);
            float saveDeadline = Time.realtimeSinceStartup + 15;
            while (Time.realtimeSinceStartup < saveDeadline && ((bool)Get("busy") || (bool)Get("committingCommandInput"))) await UniTask.DelayFrame(1);
            Check((string)session.Get(session.SelectedLevelId).Data["displayName"] == "숨긴 입력 저장 검사" && !session.IsDirty, "save commits hidden desktop draft");
            Call("SetBoardTouchTool", "tap");
            root.Q("board").Focus(); await UniTask.DelayFrame(2);
            foreach (Vector2Int size in new[] { new Vector2Int(1440,900), new Vector2Int(390,844), new Vector2Int(844,390), new Vector2Int(820,1180) })
            {
                panel.targetTexture = null; texture.Release(); texture.width = size.x; texture.height = size.y; texture.Create(); panel.targetTexture = texture;
                await UniTask.DelayFrame(4);
                if (size.x < 1000) Call("ApplyMobileViewport", (Vector2)size, new Rect(0,0,size.x,size.y), new Rect());
                await UniTask.DelayFrame(6);
                if (size.x < 1000 && Get("mobilePanel") is string openPanel)
                {
                    Call("SelectMobilePanel", openPanel);
                    await UniTask.DelayFrame(3);
                }
                Capture(texture, Path.Combine(output, "final-" + size.x + "x" + size.y + ".png"));
                Check(root.Q("board-scroll").worldBound.height > (size.y < 500 ? 150 : 350), "board usable " + size + " bounds=" + root.Q("board-scroll").worldBound);
            }
        }
        private static void Capture(RenderTexture target, string path)
        {
            RenderTexture old = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            try { image.ReadPixels(new Rect(0,0,target.width,target.height),0,0); image.Apply(); File.WriteAllBytes(path,image.EncodeToPNG()); }
            finally { RenderTexture.active=old; UnityEngine.Object.Destroy(image); }
        }
    }
}
