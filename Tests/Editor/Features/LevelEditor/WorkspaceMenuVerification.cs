using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    /// <summary>제품 창의 메뉴·탭·최소 배치를 확인한다. 명령 검사는 화면 이동만 하고 원본 저장/삭제는 호출하지 않는다.</summary>
    public static class WorkspaceMenuVerification
    {
        private static LevelEditorWindow window;
        private static IEnumerator steps;
        private static readonly List<string> results = new List<string>();
        private static double deadline;
        public static void Start()
        { steps = Run(); deadline = EditorApplication.timeSinceStartup + 60; EditorApplication.update += Tick; }

        private static IEnumerator Run()
        {
            window = ScriptableObject.CreateInstance<LevelEditorWindow>(); window.titleContent = new GUIContent("Match");
            window.ShowUtility(); window.CreateGUI(); window.SelectWorkspaceTab(4); window.position = new Rect(20, 20, window.minSize.x, window.minSize.y);
            for (int i = 0; i < 12; i++) yield return null;
            VisualElement root = window.rootVisualElement;
            Check(root.Q("workspace-menubar").Children().OfType<ToolbarMenu>().Select(m => m.text).SequenceEqual(new[] { "레벨", "시험", "기록", "도움말" }), "상단 네 카테고리 메뉴");
            Check(root.Q("workspace-tabs").Children().OfType<Button>().Select(b => b.name).SequenceEqual(new[] { "workspace-tab-0", "workspace-tab-1", "workspace-tab-4", "workspace-tab-3" }), "빈번한 네 화면만 작업 순서대로 탭에 표시");
            Check(Action("workspace-menu-level", "저장    Ctrl+S").status == DropdownMenuAction.Status.Disabled, "레벨 없을 때 저장 메뉴 비활성화");
            Action("workspace-menu-test", "초기 보드·진단").Execute();
            Check(window.WorkspaceTab == 2 && root.Q<Label>("workspace-page-name").text == "초기 보드·진단", "진단 메뉴로 보조 화면 이동 및 현재 위치 표시");
            Action("workspace-menu-records", "기록 관리").Execute();
            Check(window.WorkspaceTab == 3 && root.Q("analysis-panel").Q<Foldout>("record-management").value, "기록 메뉴로 기존 관리 영역 열기");
            Check(Action("workspace-menu-records", "시험 기록 전체 삭제…") != null, "삭제 명령은 기존 확인 기능에 연결");
            for (int i = 0; i < 8; i++) yield return null;
            Capture("workspace-records-layout.png");
            Action("workspace-menu-test", "여러 레벨 시험").Execute();
            window.position = new Rect(20, 20, window.minSize.x, window.minSize.y);
            for (int i = 0; i < 8; i++) yield return null;
            Rect setup = root.Q("multi-setup-card").worldBound, review = root.Q("multi-review-card").worldBound;
            Button guideHelp = root.Q<Button>("manual-help-difficulty.html-multi-level");
            Check(guideHelp != null && guideHelp.worldBound.width >= 18 && guideHelp.worldBound.height > 0, "안내문 도움말 아이콘 유지");
            Check(setup.xMax < review.x && setup.width >= 330 && review.width >= 400, "시험 준비와 결과 조회를 좌우 카드로 분리");
            foreach (string name in new[] { "multi-selection-menu", "multi-mode", "multi-start", "multi-history", "multi-read", "multi-detail" })
            {
                Rect bounds = root.Q(name).worldBound, area = root.worldBound;
                Check(bounds.width > 0 && bounds.height > 0 && bounds.xMin >= area.xMin && bounds.xMax <= area.xMax + 1 && bounds.yMax <= area.yMax + 1,
                    "최소 창에서 접근 " + name);
            }
            ToolbarMenu select = root.Q<ToolbarMenu>("multi-selection-menu");
            select.menu.MenuItems().OfType<DropdownMenuAction>().First(a => a.name == "전체 선택").Execute();
            ListView choices = root.Q<ListView>("multi-level-choices");
            Check(choices.selectedIndices.Count() == choices.itemsSource.Count, "선택 도구 메뉴 전체 선택");
            select.menu.MenuItems().OfType<DropdownMenuAction>().First(a => a.name == "선택 해제").Execute();
            Check(!choices.selectedIndices.Any(), "선택 도구 메뉴 선택 해제");
            Capture("workspace-menu-layout.png");
        }
        private static DropdownMenuAction Action(string menuName, string name)
        {
            ToolbarMenu menu = window.rootVisualElement.Q<ToolbarMenu>(menuName);
            using MouseUpEvent evt = MouseUpEvent.GetPooled(new Event { type = EventType.MouseUp, button = 0 });
            menu.menu.PrepareForDisplay(evt);
            return menu.menu.MenuItems().OfType<DropdownMenuAction>().First(a => a.name == name);
        }
        private static void Capture(string name)
        {
            typeof(BotAnalysisVerification).GetMethod("CaptureAnalysis", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { name });
            File.Copy("Logs/BotAnalysisVerification/" + name, "Logs/WorkspaceUxVerification/" + name, true);
        }
        private static void Check(bool value, string message)
        { results.Add((value ? "PASS " : "FAIL ") + message); if (!value) throw new InvalidOperationException(message); }
        private static void Tick()
        {
            try { if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("메뉴 검사 시간 초과"); if (steps.MoveNext()) return; }
            catch (Exception error) { results.Add("FAIL " + error); }
            EditorApplication.update -= Tick;
            File.WriteAllLines("Logs/WorkspaceUxVerification/menu-results.txt", results); if (window != null) window.Close();
            if (results.Any(r => r.StartsWith("FAIL"))) EditorApplication.Exit(1);
            else TestRecordUiVerification.Start(); // 기존 결과 조회·잠금 회귀도 같은 프로세스에서 임시 기록으로 확인한다.
        }
    }
}
