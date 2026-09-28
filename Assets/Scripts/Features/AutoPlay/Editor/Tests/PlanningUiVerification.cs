using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AutoPlay;
using Board;
using Simulation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    /// <summary>계획 전략의 실제 Match 조작과 한 판 소유권을 전용 Editor에서 검사한다.</summary>
    public static class PlanningUiVerification
    {
        private const string Evidence = "Logs/BotPlanningVerification";
        private static readonly List<string> Results = new List<string>();
        private static LevelEditorWindow window;
        private static LevelInitialStatePanel panel;
        private static LevelDefinition level;
        private static IEnumerator sequence;
        private static double next;
        private static BotPlaySession Session => (BotPlaySession)typeof(LevelInitialStatePanel)
            .GetField("botSession", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel);

        public static void Start()
        {
            BoardCoordinate[] active = Enumerable.Range(0, 16).Select(i => new BoardCoordinate(i / 4, i % 4)).ToArray();
            level = (LevelDefinition)typeof(SettlementVerification).GetMethod("Make", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { active });
            JsonUtility.FromJsonOverwrite("{\"initialBlocks\":[],\"moveCount\":3,\"missions\":[{\"kind\":0,\"color\":0,\"count\":100}]}", level);
            Directory.CreateDirectory(Evidence);
            window = LevelEditorWindow.OpenWorkspace(1, level, true); panel = window.ActiveSimulationPanel;
            sequence = Run(); EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup < next) return;
            next = EditorApplication.timeSinceStartup + 0.04;
            Exception failure = null;
            try { if (sequence.MoveNext()) return; }
            catch (Exception error) { failure = error; Results.Add("FAIL " + error); }
            EditorApplication.update -= Tick;
            if (window != null) { window.SetLevel(null); window.Close(); }
            if (level != null) UnityEngine.Object.DestroyImmediate(level);
            File.WriteAllLines(Evidence + "/planning-ui-results.txt", Results);
            EditorApplication.Exit(failure == null ? 0 : 1);
        }

        /// <returns>실제 UI 이벤트와 화면 갱신 사이에 수행하는 검사 순서.</returns>
        private static IEnumerator Run()
        {
            yield return null;
            typeof(ConnectionGraphVerification).GetField("window", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, window);
            typeof(ConnectionGraphVerification).GetMethod("RaiseWindow", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            window.position = new Rect(20, 20, 760, 860);
            VisualElement root = panel.rootVisualElement;
            root.Q<Foldout>("bot-trial").value = true;
            PopupField<string> strategy = root.Q<PopupField<string>>("bot-strategy");
            Check(strategy != null && strategy.value == "기본", "전략 선택은 기본 전략으로 시작");
            Check(!root.Q<Button>("bot-repeat").enabledInHierarchy, "첫 시험 전 같은 조건 버튼 비활성");
            Click(root, "bot-new");
            for (int i = 0; Session.NeedsAdvance && i < 1000; i++) yield return null;
            Check(Session.Status == BotSessionStatus.Ready, "기본 전략의 실제 새 시험 준비");
            Check(root.Q<Label>("bot-progress").text.Contains("새 시작 조건"), "새 시험의 조건 안내 표시");
            string start = Snapshot(Session.State); int seed = Session.Seed; BotPlaySession old = Session;
            strategy.value = "계획";
            Check(!root.Q<Button>("bot-step").enabledInHierarchy && root.Q<Button>("bot-repeat").enabledInHierarchy,
                "전략 변경만으로 기존 판 전환 안 함·재시험 안내");
            Click(root, "bot-repeat");
            for (int i = 0; Session.NeedsAdvance && i < 1000; i++) yield return null;
            Check(old.Status == BotSessionStatus.Disposed && Session.Strategy == BotStrategyKind.Planning && Session.Seed == seed && Snapshot(Session.State) == start,
                "같은 조건 버튼으로 두 전략의 시작 상태·시드 완전 일치");
            Check(root.Q<Label>("bot-progress").text.Contains("직전과 같은 시작 조건"), "재시험의 같은 조건 안내 표시");

            // 무작위 시작 버튼은 위에서 검사했다. 나머지 UI 경계는 이미 재현 검증한
            // 검사 전용 시드로 고정해 가정 모델의 정상적인 실패를 UI 실패로 섞지 않는다.
            Session.Dispose();
            BotPlaySession fixedSession = new BotPlaySession(level, 771, BotStrategyKind.Planning);
            for (int i = 0; fixedSession.NeedsAdvance && i < 2000; i++) fixedSession.Advance();
            typeof(LevelInitialStatePanel).GetField("botSession", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(panel, fixedSession);
            typeof(LevelInitialStatePanel).GetField("seed", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(panel, 771);
            typeof(LevelInitialStatePanel).GetField("botReplaySeed", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(panel, (int?)771);
            ForceTick();
            EditorUtility.SetDirty(level); string original = JsonUtility.ToJson(level);
            start = Snapshot(Session.State);
            Click(root, "bot-step"); ForceTick();
            Check(Session.IsPlanning && !strategy.enabledInHierarchy && !root.Q<Button>("bot-repeat").enabledInHierarchy,
                "계획 계산 중 전략 변경·같은 조건 재시험 잠금");
            Check(root.Q<Label>("bot-progress").text.Contains("계획 계산") && Snapshot(Session.State) == start,
                "계획 진행 표시 중 실제 보드·비용 무변경");
            Click(root, "bot-stop");
            Check(Session.Status == BotSessionStatus.Stopped && !Session.IsPlanning && Snapshot(Session.State) == start,
                "계산 중 실제 중지 버튼은 탐색만 취소");
            Click(root, "bot-step");
            for (int i = 0; Session.NeedsAdvance && i < 5000; i++) yield return null;
            Check(Session.Status == BotSessionStatus.Ready && Session.Records.Count == 1 && Session.LastChoice.Reason.Contains("가정"),
                "실제 한 수 버튼으로 계획 후 한 행동 완료");
            Check(root.Q<Label>("bot-reason").text.Contains(Session.LastChoice.Reason), "화면에 실제 선택 이유·가정 한계 표시");
            yield return null;
            foreach (string name in new[] { "bot-new", "bot-step", "bot-run", "bot-stop", "bot-repeat", "bot-manual", "bot-strategy" })
            {
                Rect bounds = root.Q(name).worldBound;
                Check(bounds.width > 35 && bounds.height > 15 && bounds.xMin >= 0 && bounds.xMax <= window.rootVisualElement.worldBound.xMax + 1 &&
                    bounds.yMax <= window.rootVisualElement.worldBound.yMax + 1, "최소 창의 필수 컨트롤 접근성 " + name);
            }
            Check(root.Q<ScrollView>("initial-board-scroll").contentViewport.worldBound.height >= 150, "계획 설명 이후에도 보드 영역 150px 이상");
            Texture2D capture = new Texture2D((int)window.position.width, (int)window.position.height, TextureFormat.RGB24, false);
            capture.SetPixels(UnityEditorInternal.InternalEditorUtility.ReadScreenPixel(window.position.position, capture.width, capture.height)); capture.Apply();
            File.WriteAllBytes(Evidence + "/planning-ui.png", capture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(capture);

            Click(root, "bot-run"); ForceTick(); Click(window.rootVisualElement, "workspace-tab-0");
            for (int i = 0; Session.NeedsAdvance && i < 1000; i++) yield return null;
            Check(Session.Status == BotSessionStatus.Stopped && !Session.IsPlanning, "계산 중 탭 이탈은 탐색 폐기·중지");
            Click(window.rootVisualElement, "workspace-tab-1");
            for (int i = 0; i < 4; i++) yield return null;
            Check(Session.Status == BotSessionStatus.Stopped && Session.Records.Count == 1, "탭 복귀로 계획 자동 재개 없음");
            Click(root, "bot-run");
            for (int i = 0; Session.NeedsAdvance && i < 10000; i++) yield return null;
            Check(Session.Status == BotSessionStatus.MovesExhausted && Session.Records.Count == 3 &&
                root.Q<Label>("bot-progress").text.Contains("이동 수 소진"), "계획 한 판 실행·실제 종료 UI");
            Check(JsonUtility.ToJson(level) == original && EditorUtility.IsDirty(level), "계획 UI 전체 실행 후 원본·dirty 보존");
            seed = Session.Seed; old = Session; Click(root, "bot-new");
            for (int i = 0; Session.NeedsAdvance && i < 1000; i++) yield return null;
            Check(Session.Seed != seed && old.Status == BotSessionStatus.Disposed, "새 시험 버튼은 새 시드·이전 판 폐기");
            Click(root, "bot-run"); ForceTick(); old = Session;
            JsonUtility.FromJsonOverwrite("{\"levelNumber\":261}", level); EditorUtility.SetDirty(level);
            for (int i = 0; Session != null && i < 1000; i++) yield return null;
            Check(Session == null && old.Status == BotSessionStatus.Disposed && !old.IsPlanning && !root.Q<Button>("bot-repeat").enabledInHierarchy,
                "원본 수정은 탐색·과거 비교 조건·세션 폐기");
            Click(root, "bot-new");
            for (int i = 0; Session.NeedsAdvance && i < 1000; i++) yield return null;
            Click(root, "bot-run"); ForceTick(); old = Session; window.SetLevel(null);
            Check(old.Status == BotSessionStatus.Disposed && !old.IsPlanning && Session == null, "레벨 변경 시 계획·관찰 폐기");
            window.SetLevel(level); Click(root, "bot-new");
            for (int i = 0; Session.NeedsAdvance && i < 1000; i++) yield return null;
            Click(root, "bot-run"); ForceTick(); old = Session; window.Close(); window = null;
            Check(old.Status == BotSessionStatus.Disposed && !old.IsPlanning && old.State == null, "창 종료 시 계획·실제 상태·예약 해제");
        }

        private static void ForceTick()
        {
            typeof(LevelInitialStatePanel).GetField("nextBotTick", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(panel, 0d);
            typeof(LevelInitialStatePanel).GetMethod("BotTick", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(panel, null);
        }
        /// <param name="root">검색 루트.</param><param name="name">실제 버튼 이름.</param>
        private static void Click(VisualElement root, string name)
        {
            Button button = root.Q<Button>(name);
            if (button == null || !button.enabledInHierarchy) throw new InvalidOperationException("버튼 사용 불가 " + name);
            using NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled(); evt.target = button; button.SendEvent(evt);
        }
        /// <param name="pass">검사 조건.</param><param name="name">증거 이름.</param>
        private static void Check(bool pass, string name) { if (!pass) throw new InvalidOperationException(name); Results.Add("PASS " + name); }
        /// <param name="value">상태 값.</param><returns>기존 검증기의 결정적 표현.</returns>
        private static string Snapshot(object value) => (string)typeof(LevelInitialStateVerification)
            .GetMethod("Snapshot", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new[] { value });
    }
}
