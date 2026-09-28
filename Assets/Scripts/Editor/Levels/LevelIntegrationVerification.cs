using System;
using System.IO;
using Board;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    // 네이티브 메뉴 표시를 확인하는 동안 사용자 에셋과 분리한 창을 유지한다.
    public static partial class LevelIntegrationVerification
    {
        private const string Evidence = "Logs/LevelIntegrationVerification";
        private static LevelEditorWindow window;
        private static LevelDefinition level;
        private static string folder;
        private static double readyAt;
        private static bool opened;
        private static string nativeView = "single";

        public static void NativeStart()
        {
            Directory.CreateDirectory(Evidence);
            if (File.Exists(Evidence + "/native-state.json")) throw new InvalidOperationException("기존 네이티브 검증 상태를 먼저 정리하세요.");
            folder = "Assets/__LevelIntegrationVerification_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            File.WriteAllText(Evidence + "/native-state.json", JsonUtility.ToJson(new NativeState { folder = folder }));
            level = LevelAssetOperations.CreateAtPath(folder + "/Native.asset");
            using (SerializedObject edit = new SerializedObject(level))
            {
                edit.FindProperty("levelNumber").intValue = 71001;
                edit.ApplyModifiedPropertiesWithoutUndo();
            }
            LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)ObstacleKind.Crate, Durability = 2 }, new[] { new BoardCoordinate(3, 2), new BoardCoordinate(3, 3) });
            window = ScriptableObject.CreateInstance<LevelEditorWindow>();
            window.titleContent = new GUIContent("7단계 네이티브 메뉴 검수");
            window.position = new Rect(10, 10, 1000, 780);
            window.ShowUtility(); window.SetLevel(level); window.Focus();
            readyAt = EditorApplication.timeSinceStartup + 3;
            EditorApplication.update += NativeTick;
        }

        public static void NativeMultiStart() { NativeStart(); nativeView = "multi"; }
        public static void NativeSupplyStart()
        {
            NativeStart(); nativeView = "supply";
            LevelSupplyEditing.SetSourceProperty(level, new[] { 0 }, "mode", (int)SupplyMode.Fixed);
            LevelSupplyEditing.SetItems(level, 0, new[] { new SupplyItem(SupplyKind.RandomNormal), new SupplyItem(SupplyKind.Bomb) });
        }

        private static void NativeTick()
        {
            if (File.Exists(Evidence + "/native-stop"))
            {
                File.Delete(Evidence + "/native-stop");
                EditorApplication.update -= NativeTick;
                window.Close();
                AssetDatabase.DeleteAsset(folder);
                File.Delete(Evidence + "/native-state.json");
                EditorApplication.Exit(0);
                return;
            }
            if (opened || EditorApplication.timeSinceStartup < readyAt) return;
            opened = true;
            if (nativeView == "supply")
            {
                Menu("menu-supply", "생성구 선택"); Paint(0, 0);
                window.rootVisualElement.RegisterCallback<ContextualMenuPopulateEvent>(evt =>
                {
                    ListView current = window.rootVisualElement.Q<ListView>("supply-items");
                    string trace = "menu pos=" + evt.mousePosition + " selected=" + current.selectedIndex + " list=" + current.worldBound;
                    foreach (Label row in current.Query<Label>().ToList()) trace += "\nrow=" + row.text + " data=" + row.userData + " rect=" + row.worldBound;
                    File.AppendAllText(Evidence + "/native-events.txt", trace + "\n");
                }, TrickleDown.TrickleDown);
                window.rootVisualElement.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button == 1) File.AppendAllText(Evidence + "/native-events.txt", "pointer pos=" + evt.position + " target=" + (evt.target as VisualElement)?.name + "\n");
                }, TrickleDown.TrickleDown);
                window.rootVisualElement.Q<ListView>("supply-items").SetSelection(1);
                window.rootVisualElement.schedule.Execute(() =>
                {
                    ListView list = window.rootVisualElement.Q<ListView>("supply-items");
                    string text = "window " + window.position + "\nlist " + list.worldBound;
                    foreach (Label row in list.Query<Label>().ToList())
                        text += "\n" + row.text + " data=" + row.userData + " bounds=" + row.worldBound;
                    File.WriteAllText(Evidence + "/native-rows.txt", text);
                }).StartingIn(500);
            }
            else
            {
                window.rootVisualElement.Q<PopupField<string>>("placement-layer").index = (int)PlacementLayer.Obstacle;
                Paint(3, 2);
                if (nativeView == "multi")
                {
                    LevelBoardView board = window.rootVisualElement.Q<LevelBoardView>();
                    Event input = new Event { type = EventType.MouseDown, button = 0, mousePosition = board.CellAt(new BoardCoordinate(3, 3)).worldBound.center, modifiers = EventModifiers.Control };
                    using PointerDownEvent pointer = PointerDownEvent.GetPooled(input); pointer.target = board; board.SendEvent(pointer);
                }
            }
            File.WriteAllText(Evidence + "/native-ready.txt", nativeView + " ready, window " + window.position);
        }
        [Serializable] private sealed class NativeState { public string folder; }
    }
}




