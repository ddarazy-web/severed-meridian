using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AutoPlay;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    /// <summary>실제 Match 창의 버튼·배치·수명주기를 전용 Editor 프로세스에서 검증한다.</summary>
    public static partial class BotUiVerification
    {
        private const string Evidence = "Logs/BotBasicVerification";
        private static readonly List<string> Results = new List<string>();
        private static LevelEditorWindow window;
        private static LevelInitialStatePanel panel;
        private static LevelDefinition level;
        private static IEnumerator sequence;
        private static double next;
        private static BotPlaySession Session => (BotPlaySession)typeof(LevelInitialStatePanel)
            .GetField("botSession", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel);

        /// <summary>메모리 레벨로 창을 열고 UI 이벤트를 순서대로 실행한다.</summary>
        public static void Start()
        {
            Directory.CreateDirectory(Evidence);
            level = (LevelDefinition)typeof(ItemBoosterVerification).GetMethod("PlayFixture", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            window = LevelEditorWindow.OpenWorkspace(1, level, true);
            panel = window.ActiveSimulationPanel;
            sequence = Run(); EditorApplication.update += Tick;
        }

        /// <summary>코루틴을 진행하고 모든 경로에서 소유 창·메모리 레벨을 정리한다.</summary>
        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup < next) return;
            next = EditorApplication.timeSinceStartup + 0.1;
            Exception failure = null;
            try { if (sequence.MoveNext()) return; }
            catch (Exception error) { failure = error; Results.Add("FAIL " + error); }
            EditorApplication.update -= Tick;
            if (window != null) { window.SetLevel(null); window.Close(); }
            if (level != null) UnityEngine.Object.DestroyImmediate(level);
            File.WriteAllLines(Evidence + "/ui-results.txt", Results);
            EditorApplication.Exit(failure == null ? 0 : 1);
        }

        /// <returns>프레임 경계를 넘기는 UI 검사 순서.</returns>
        private static IEnumerator Run()
        {
            yield return null;
            typeof(ConnectionGraphVerification).GetField("window", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, window);
            typeof(ConnectionGraphVerification).GetMethod("RaiseWindow", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            window.position = new Rect(20, 20, 760, 860);
            EditorUtility.SetDirty(level);
            string original = JsonUtility.ToJson(level);
            VisualElement root = panel.rootVisualElement;
            root.Q<Foldout>("bot-trial").value = true;
            Click(root, "bot-new");
            for (int i = 0; Session.NeedsAdvance && i < 400; i++) yield return null;
            Check(Session.Status == BotSessionStatus.Ready, "실제 새 봇 시험 버튼으로 사본 준비");
            Check(root.Q<Button>("manual-help-play.html-bot") != null && root.Q<Foldout>("bot-trial").tooltip.Contains("공개 정보"), "봇 제목 도움말 아이콘·툴팁 연결");
            int firstSeed = Session.Seed;
            UseRepeatableSession();
            Check(panel.Execution == null && !panel.IsSearching, "수동 실행·검색과 봇 실행 동시 소유 차단");
            Check(!root.Q<Button>("manual-start").enabledInHierarchy && !root.Q<Button>("item-Hammer").enabledInHierarchy,
                "봇 시험 중 수동 시작·아이템 입력 잠금");
            Click(root, "bot-step");
            for (int i = 0; Session.NeedsAdvance && i < 1000; i++) yield return null;
            Check(Session.Records.Count == 1 && Session.Status == BotSessionStatus.Ready,
                $"실제 한 수 버튼은 연쇄 후 정확히 한 수만 진행: {Session.Status}, {Session.Message}, 시드 {Session.Seed}, 행동 {Session.Records.Count}");
            Check(root.Q<Label>("bot-reason").text.Contains(Session.LastChoice.Reason), "선택 이유가 실제 점수·한계와 일치");
            Check(root.Q<Label>("bot-progress").text.Contains("완료한 행동 1회"), "실제 진행 횟수 표시");
            Check(root.Q<Button>("bot-stop").enabledInHierarchy == false, "대기 상태 중지 버튼 비활성");

            // 실행 예약 직후 한 번만 갱신해 행동 처리 중의 중지 버튼을 검사한다.
            Click(root, "bot-run"); ForceTick(); Click(root, "bot-stop");
            for (int i = 0; Session.NeedsAdvance && i < 1000; i++) yield return null;
            Check(Session.Status == BotSessionStatus.Stopped && Session.Records.Count == 2, "실제 중지 버튼은 현재 행동 처리 후 멈춤");
            string stopped = Snapshot(Session.State);
            for (int i = 0; i < 4; i++) yield return null;
            Check(Snapshot(Session.State) == stopped, "중지 뒤 Editor 갱신으로 다음 수가 실행되지 않음");
            yield return null;
            foreach (string name in new[] { "bot-new", "bot-step", "bot-run", "bot-stop", "bot-manual" })
            {
                Button button = root.Q<Button>(name);
                Rect bounds = button.worldBound;
                Check(bounds.width > 40 && bounds.height > 15 && bounds.xMin >= 0 && bounds.xMax <= window.rootVisualElement.worldBound.xMax + 1,
                    "최소 창에서 버튼 폭과 가로 접근성 " + name);
            }
            Check(root.Q<ScrollView>("initial-board-scroll").contentViewport.worldBound.height >= 150, "봇 정보가 보드 영역을 모두 밀어내지 않음");
            Texture2D capture = new Texture2D((int)window.position.width, (int)window.position.height, TextureFormat.RGB24, false);
            capture.SetPixels(UnityEditorInternal.InternalEditorUtility.ReadScreenPixel(window.position.position, capture.width, capture.height)); capture.Apply();
            File.WriteAllBytes(Evidence + "/bot-ui.png", capture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(capture);

            Click(root, "bot-run"); ForceTick();
            Click(window.rootVisualElement, "workspace-tab-0");
            for (int i = 0; Session.NeedsAdvance && i < 1000; i++) yield return null;
            Check(Session.Status == BotSessionStatus.Stopped, "탭 이탈도 같은 안전 중지 경계 사용");
            stopped = Snapshot(Session.State);
            Click(window.rootVisualElement, "workspace-tab-1");
            for (int i = 0; i < 4; i++) yield return null;
            Check(Snapshot(Session.State) == stopped && Session.Status == BotSessionStatus.Stopped, "탭 복귀 후 자동 재개 금지");
            Check(JsonUtility.ToJson(level) == original && EditorUtility.IsDirty(level), "실제 UI 시험 후 원본 JSON·미저장 dirty 보존");
            BotPlaySession old = Session;
            Click(root, "bot-new");
            for (int i = 0; Session.NeedsAdvance && i < 400; i++) yield return null;
            Check(old.Status == BotSessionStatus.Disposed && Session.Seed != firstSeed, "새 시험은 이전 세션 폐기·새 시드 사용");
            old = Session;
            JsonUtility.FromJsonOverwrite("{\"levelNumber\":251}", level); EditorUtility.SetDirty(level);
            for (int i = 0; Session != null && i < 30; i++) yield return null;
            Check(Session == null && old.Status == BotSessionStatus.Disposed && panel.CurrentState == null, "원본 변경 감지 후 세션·관찰·보드 폐기");
            Check(root.Q<Label>("initial-status").text.Contains("원본 내용"), "원본 변경 폐기 이유 표시");
            JsonUtility.FromJsonOverwrite("{\"moveCount\":2}", level);
            Click(root, "bot-new");
            for (int i = 0; Session.NeedsAdvance && i < 400; i++) yield return null;
            UseRepeatableSession();
            Click(root, "bot-run");
            for (int i = 0; Session.NeedsAdvance && i < 1000; i++) yield return null;
            Check(Session.Status == BotSessionStatus.MovesExhausted && Session.Records.Count == 2 &&
                root.Q<Label>("bot-progress").text.Contains("이동 수 소진"), "실제 한 판 버튼으로 끝까지 실행·종료 이유 표시");
            Check(!root.Q<Button>("bot-step").enabledInHierarchy && !root.Q<Button>("bot-run").enabledInHierarchy,
                "실제 종료 UI에서 추가 조작 차단");
            // 성공 기록과 후속 처리 오류가 동시에 존재하는 경우도 화면에서 구분해야 한다.
            // 검사 소유 레벨의 목표만 낮추고, 공통 실행기의 실제 라스트팡 한도 분기를 사용한다.
            JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":0,\"color\":0,\"count\":1}]}", level);
            Click(root, "bot-new");
            for (int i = 0; Session.NeedsAdvance && i < 400; i++) yield return null;
            UseRepeatableSession();
            Click(root, "bot-run");
            for (int i = 0; Session.Outcome == null && Session.NeedsAdvance && i < 1000; i++) ForceTick();
            Check(Session.Outcome?.Kind == Simulation.BoardOutcomeKind.Won && Session.NeedsAdvance, "실제 봇 UI 성공 후 라스트팡 경계 도달");
            Simulation.BoardActionExecutor ending = (Simulation.BoardActionExecutor)typeof(BotPlaySession)
                .GetField("executor", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Session);
            typeof(Simulation.BoardActionExecutor).GetProperty("LastPangWaves").SetValue(ending, ending.CascadeLimit);
            for (int i = 0; Session.NeedsAdvance && i < 1000; i++) ForceTick();
            Check(Session.Status == BotSessionStatus.Error && Session.Outcome.Kind == Simulation.BoardOutcomeKind.Won &&
                root.Q<Label>("bot-progress").text.Contains("실행 오류") && root.Q<Label>("bot-progress").text.Contains("라스트팡"),
                "실제 UI에서 성공 기록 보존·라스트팡 오류 안내");
            Check(!root.Q<Button>("bot-step").enabledInHierarchy && !root.Q<Button>("bot-run").enabledInHierarchy,
                "라스트팡 오류 후 실제 UI 추가 조작 차단");
            JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":0,\"color\":0,\"count\":100}]}", level);
            Click(root, "bot-manual");
            for (int i = 0; panel.IsSearching && i < 400; i++) yield return null;
            Check(Session == null && panel.CurrentState != null && root.Q<Button>("manual-start").enabledInHierarchy, "수동 시험 새로 버튼으로 조작 복구");
            Click(root, "manual-start");
            Check(panel.Execution != null && root.Q<Button>("item-Hammer").enabledInHierarchy, "봇 시험 뒤 기존 수동 실행·아이템 사용 가능");
            Click(root, "bot-new");
            for (int i = 0; Session.NeedsAdvance && i < 400; i++) yield return null;
            old = Session;
            window.SetLevel(null);
            Check(old.Status == BotSessionStatus.Disposed && Session == null, "레벨 변경은 이전 봇 세션 폐기");
            window.SetLevel(level); Click(root, "bot-new");
            for (int i = 0; Session.NeedsAdvance && i < 400; i++) yield return null;
            old = Session;
            window.Close(); window = null;
            Check(old.Status == BotSessionStatus.Disposed && old.State == null, "창 종료는 실행기와 예약 폐기");
        }

        /// <summary>한 실행 단계만 만들어 후속 처리 중 UI 중지·탭 이탈을 재현한다.</summary>
        private static void ForceTick()
        {
            typeof(LevelInitialStatePanel).GetField("nextBotTick", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(panel, 0d);
            typeof(LevelInitialStatePanel).GetMethod("BotTick", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(panel, null);
        }

        /// <summary>
        /// 새 시험 버튼의 새 시드 생성은 별도로 검사한다. 행동·종료 화면은 독립 재현에 쓰는
        /// 시드로 고정해야 무작위 재배치의 정상 오류를 UI 회귀로 오인하지 않는다.
        /// 검사 소유 패널에만 실제 세션을 교체하며, 제품에 시드 옵션이나 시험용 분기를 추가하지 않는다.
        /// </summary>
        private static void UseRepeatableSession()
        {
            Session.Dispose();
            BotPlaySession repeatable = new BotPlaySession(level, 771);
            for (int i = 0; repeatable.NeedsAdvance && i < 2000; i++) repeatable.Advance();
            if (repeatable.Status != BotSessionStatus.Ready) throw new InvalidOperationException("UI 검증용 시작 상태 준비 실패");
            typeof(LevelInitialStatePanel).GetField("botSession", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(panel, repeatable);
            typeof(LevelInitialStatePanel).GetField("seed", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(panel, 771);
            ForceTick();
        }

        /// <param name="root">조회 루트.</param><param name="name">실제 버튼 이름.</param>
        private static void Click(VisualElement root, string name)
        {
            Button button = root.Q<Button>(name);
            if (button == null || !button.enabledInHierarchy) throw new InvalidOperationException("버튼 사용 불가 " + name);
            using NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled(); evt.target = button; button.SendEvent(evt);
        }
        /// <param name="pass">검사 결과.</param><param name="name">검사명.</param>
        private static void Check(bool pass, string name)
        { if (!pass) throw new InvalidOperationException(name); Results.Add("PASS " + name); }
        /// <param name="value">비교 값.</param><returns>기존 검사기의 상태 표현.</returns>
        private static string Snapshot(object value) => (string)typeof(LevelInitialStateVerification)
            .GetMethod("Snapshot", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new[] { value });
    }
}
