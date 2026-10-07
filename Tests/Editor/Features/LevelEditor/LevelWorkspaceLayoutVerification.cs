using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    /// <summary>탭별 최소 크기와 도움말의 실제 배치 영역을 전용 Unity 프로세스에서 검사한다.</summary>
    public static class LevelWorkspaceLayoutVerification
    {
        private static readonly List<string> Results = new List<string>();
        private static LevelEditorWindow window;
        private static LevelDefinition level;
        private static IEnumerator sequence;
        private static double next;
        private const string Evidence = "Logs/LevelWorkspaceLayoutVerification";

        /// <summary>임시 레벨로 UI 검증을 시작하며, 종료 시 검증 프로세스도 닫는다.</summary>
        public static void Start()
        {
            Directory.CreateDirectory(Evidence);
            File.WriteAllText(Evidence + "/geometry.txt", "");
            window = LevelEditorWindow.OpenWorkspace(0);
            level = (LevelDefinition)typeof(ItemBoosterVerification).GetMethod("PlayFixture", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            window.SetLevel(level);
            sequence = Run();
            EditorApplication.update += Tick;
        }

        /// <summary>관찰 결과를 기록한다.</summary>
        /// <param name="pass">성공 여부.</param><param name="label">검사 항목.</param>
        private static void Check(bool pass, string label)
        {
            Results.Add((pass ? "PASS " : "FAIL ") + label);
        }

        /// <summary>실제 버튼 이벤트로 탭이나 도구를 선택한다.</summary>
        /// <param name="name">버튼의 UI 이름.</param>
        private static void Click(string name)
        {
            Button button = window.rootVisualElement.Q<Button>(name);
            using NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled();
            evt.target = button;
            button.SendEvent(evt);
        }

        /// <summary>레이아웃 갱신 뒤 탭별 치수·아이콘·보드 영역을 확인한다.</summary>
        /// <returns>프레임을 넘기는 검증 단계.</returns>
        private static IEnumerator Run()
        {
            yield return null;
            typeof(ConnectionGraphVerification).GetField("window", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, window);
            typeof(ConnectionGraphVerification).GetMethod("RaiseWindow", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            for (int tab = 0; tab < 3; tab++)
            {
                window.minSize = new Vector2(400, 300);
                window.position = new Rect(20, 20, 680, 480);
                window.SelectWorkspaceTab(tab);
                if (tab == 0) Click("inspector-tab-1");
                else
                {
                    VisualElement panel = window.ActiveSimulationPanel.rootVisualElement;
                    Button build = panel.Q<Button>("initial-build");
                    using NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled();
                    evt.target = build; build.SendEvent(evt);
                }
                for (int frame = 0; frame < 6; frame++) yield return null;
                if (tab == 0) window.rootVisualElement.Q<Foldout>("supply-maintenance").value = true;
                yield return null;
                Check(window.position.width >= window.minSize.x && window.position.height >= window.minSize.y, "탭 " + tab + " 최소 크기로 자동 확대");
                ScrollView outer = window.rootVisualElement.Q<ScrollView>("workspace-viewport");
                Check(outer.horizontalScroller.highValue <= 1 && outer.verticalScroller.highValue <= 1, "탭 " + tab + " 최소 크기에서 바깥 스크롤 불필요");
                VisualElement panelRoot = tab == 0 ? window.rootVisualElement.Q("editor-panel") : window.ActiveSimulationPanel.rootVisualElement;
                ScrollView board = panelRoot.Q<ScrollView>(tab == 0 ? "board-scroll" : "initial-board-scroll");
                File.AppendAllText(Evidence + "/geometry.txt", $"tab={tab} window={window.position} viewport={board.contentViewport.worldBound}\n");
                File.AppendAllText(Evidence + "/geometry.txt", $"root={window.rootVisualElement.layout} outer={outer.layout} vp={outer.contentViewport.layout} content={window.rootVisualElement.Q("workspace-content").layout} minHeight={window.rootVisualElement.Q("workspace-content").resolvedStyle.minHeight} scroll={outer.horizontalScroller.highValue},{outer.verticalScroller.highValue}\n");
                Check(board.contentViewport.worldBound.width >= (tab == 0 ? 454 : 466), "탭 " + tab + " 보드 가로 영역 확보");
                Check(board.contentViewport.worldBound.height >= (tab == 0 ? 446 : 466), "탭 " + tab + " 보드 세로 영역 확보");
                foreach (Button icon in window.rootVisualElement.Query<Button>(className: "manual-help-icon").ToList())
                {
                    bool visible = true;
                    for (VisualElement ancestor = icon; ancestor != null; ancestor = ancestor.parent)
                        if (ancestor.resolvedStyle.display == DisplayStyle.None) visible = false;
                    if (!visible) continue;
                    Rect bounds = icon.worldBound;
                    // 접힌 Foldout 내부는 Unity가 지연 배치하므로 크기가 0이다. 화면에 나온 아이콘만 검사한다.
                    if (bounds.width == 0 && bounds.height == 0) continue;
                    Check(bounds.width >= 19 && bounds.height >= 19, "도움말 클릭 영역 " + icon.name + " " + bounds);
                    for (VisualElement ancestor = icon.parent; ancestor != null; ancestor = ancestor.parent)
                    {
                        Rect clip = ancestor.worldBound;
                        // 세로 스크롤 아래에 있는 항목은 정상이다. 가로 방향으로 잘리는지 확인한다.
                        if (bounds.yMax <= clip.yMin || bounds.yMin >= clip.yMax) continue;
                        Check(bounds.xMin >= clip.xMin - 1 && bounds.xMax <= clip.xMax + 1,
                            "도움말 가로 잘림 없음 " + icon.name + " / " + ancestor.name);
                    }
                }
                Rect rect = window.position;
                Texture2D image = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGB24, false);
                image.SetPixels(UnityEditorInternal.InternalEditorUtility.ReadScreenPixel(rect.position, image.width, image.height)); image.Apply();
                File.WriteAllBytes(Evidence + "/tab-" + tab + ".png", image.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(image);
            }
            window.position = new Rect(20, 20, 1200, 900);
            window.SelectWorkspaceTab(1);
            yield return null;
            Check(window.position.width >= 1200 && window.position.height >= 900, "사용자가 넓힌 창 유지");
            Check(window.minSize.x < 1040 && window.minSize.y < 900, "작은 탭의 최소 크기는 별도 적용");
            // 도킹 시처럼 컨테이너만 좁아져도 전체 콘텐츠는 최소 크기와 스크롤 접근을 유지한다.
            ScrollView viewport = window.rootVisualElement.Q<ScrollView>("workspace-viewport");
            viewport.style.width = 600; viewport.style.height = 400; viewport.style.flexGrow = 0;
            yield return null;
            Check(viewport.horizontalScroller.highValue > 0 && viewport.verticalScroller.highValue > 0, "작은 도킹 영역에서 양방향 스크롤 제공");
        }

        /// <summary>레이아웃을 기다리며 진행하고 성공·실패 모두 임시 객체를 정리한다.</summary>
        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup < next) return;
            next = EditorApplication.timeSinceStartup + 0.3;
            Exception failure = null;
            try { if (sequence.MoveNext()) return; }
            catch (Exception error) { failure = error; Results.Add("FAIL " + error); Debug.LogException(error); }
            EditorApplication.update -= Tick;
            if (window != null) { window.SetLevel(null); window.Close(); }
            if (level != null) UnityEngine.Object.DestroyImmediate(level);
            File.WriteAllLines(Evidence + "/results.txt", Results);
            EditorApplication.Exit(failure == null && !Results.Exists(line => line.StartsWith("FAIL")) ? 0 : 1);
        }
    }
}
