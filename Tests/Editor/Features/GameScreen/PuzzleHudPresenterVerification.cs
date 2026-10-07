using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Board;
using Levels;
using Levels.Editor;
using Simulation;
using UnityEditor;
using UnityEngine;

namespace GameScreen.Editor
{
    public static class PuzzleHudPresenterVerification
    {
        private static readonly List<string> Results = new List<string>();
        private static void Check(bool pass, string message)
        { if (!pass) throw new InvalidOperationException(message); Results.Add("PASS " + message); }
        private static T Read<T>(object owner, string property) => (T)owner.GetType().GetProperty(property).GetValue(owner);
        private static void Set(object owner, string field, object value) => owner.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(owner, value);
        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 Editor에서만 실행한다.");
            Results.Clear(); int exit = 0;
            LevelDefinition level = null; GameObject root = null, hudRoot = null; IDisposable presenter = null;
            try
            {
                Type presenterType = typeof(PuzzleGameSession).Assembly.GetType("GameScreen.PuzzleHudPresenter");
                Type stateType = typeof(PuzzleGameSession).Assembly.GetType("GameScreen.PuzzleHudState");
                Check(presenterType != null && stateType != null, "HUD Presenter와 읽기 전용 표시 상태 존재");
                level = (LevelDefinition)typeof(PowerEffectVerification).GetMethod("Make", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
                JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":0,\"color\":0,\"count\":1}]}", level);
                BoardCoordinate at = new BoardCoordinate(4, 4);
                typeof(PowerEffectVerification).GetMethod("Place", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null,
                    new object[] { level, at, InitialBlockKind.Rocket, RocketDirection.Horizontal, RabbitColor.Type1 });
                LevelStateBuildResult built = LevelStateBuilder.Build(level, 12345);
                Check(built.IsBuilt, "HUD 실제 실행 입력 유효 " + string.Join(";", built.Issues));
                BoardActionExecutor executor = new BoardActionExecutor(built.State);
                root = new GameObject("Phase04 HUD session"); PuzzleGameSession session = root.AddComponent<PuzzleGameSession>();
                Set(session, "executor", executor); Set(session, "ready", true);
                session.ProgressFeedback.Initialize(session.State);
                string prefabPath = "Assets/Prefabs/UI/Puzzle/PuzzleHUD.prefab";
                byte[] original = File.ReadAllBytes(prefabPath), metadata = File.ReadAllBytes(prefabPath + ".meta");
                hudRoot = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
                PuzzleHudView hud = hudRoot.GetComponent<PuzzleHudView>();
                presenter = (IDisposable)Activator.CreateInstance(presenterType, session, hud);
                object display = presenterType.GetProperty("State").GetValue(presenter);
                IList missions = Read<IList>(display, "Missions");
                Check(missions.IsReadOnly && missions.Count == 1 && Read<int>(missions[0], "Progress") == 0, "표시 미션 목록은 읽기 전용 값");
                Check(stateType.GetProperties().All(property => property.SetMethod == null || !property.SetMethod.IsPublic) &&
                    !stateType.GetProperties().Any(property => property.PropertyType == typeof(PuzzleGameSession) || property.PropertyType == typeof(LevelRuntimeState)), "View 표시 상태에 세션/실행 원본과 공개 쓰기 없음");
                int randomBefore = session.State.Random.DrawCount; string sourceBefore = JsonUtility.ToJson(level);
                presenterType.GetMethod("Frame").Invoke(presenter, null);
                Check(session.State.Random.DrawCount == randomBefore && JsonUtility.ToJson(level) == sourceBefore, "HUD 수집은 원본/난수 무변경");
                BoardActionResult action = executor.Activate(at);
                Check(action.IsApplied && session.State.Missions[0].Progress == 1, "실제 로켓 실행은 미션 완료");
                session.ProgressFeedback.Schedule(session.State, record => 0f);
                session.ProgressFeedback.Tick(.01f);
                presenterType.GetMethod("Refresh").Invoke(presenter, null);
                Check(Read<int>(missions[0], "Progress") == 0 && hudRoot.GetComponentsInChildren<UnityEngine.UI.Text>().Any(label => label.text == "0/1"), "수집 도착 전 HUD는 최종 진행값을 선반영하지 않음");
                session.ProgressFeedback.Tick(.33f);
                presenterType.GetMethod("Refresh").Invoke(presenter, null);
                Check(Read<int>(missions[0], "Progress") == 1 && hudRoot.GetComponentsInChildren<UnityEngine.UI.Text>().Any(label => label.text == "✓ 1/1"), "수집 도착 후 HUD 숫자 갱신");
                Check(original.SequenceEqual(File.ReadAllBytes(prefabPath)) && metadata.SequenceEqual(File.ReadAllBytes(prefabPath + ".meta")), "기존 uGUI 프리팹/메타 유지");
                int slots = hudRoot.GetComponentsInChildren<PuzzleMissionView>(true).Length;
                for (int repeat = 0; repeat < 20; repeat++) presenterType.GetMethod("Refresh").Invoke(presenter, null);
                Check(hudRoot.GetComponentsInChildren<PuzzleMissionView>(true).Length == slots, "HUD 미션 슬롯 반복 추가 생성0");
                presenter.Dispose(); presenter.Dispose();
                Delegate changed = (Delegate)typeof(PuzzleGameSession).GetField("Changed", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(session);
                Check(changed == null || changed.GetInvocationList().All(callback => !ReferenceEquals(callback.Target, presenter)), "Presenter Dispose 구독 해제와 반복 반환");
                Check(missions.Count == 0 && hudRoot.GetComponentsInChildren<PuzzleMissionView>().Length == 0, "Presenter 반환 후 표시 미션/잔상 제거");
            }
            catch (Exception error) { Results.Add("FAIL " + error); Debug.LogException(error); exit = 1; }
            finally
            {
                presenter?.Dispose();
                if (hudRoot != null) UnityEngine.Object.DestroyImmediate(hudRoot);
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                if (level != null) UnityEngine.Object.DestroyImmediate(level);
            }
            Directory.CreateDirectory("Logs/ElementFramework/Phase04");
            File.WriteAllLines("Logs/ElementFramework/Phase04/hud-presenter-results.txt", Results);
            EditorApplication.Exit(exit);
        }
    }
}
