using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AutoPlay;
using Simulation;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    /// <summary>완료 구간 실측 추천을 검사 소유 사본에 입력·저장하고 저장된 맵 사용 수를 대조한다.</summary>
    public static class Stage31ApplyVerification
    {
        private const string Evidence = "Logs/Stage31Workflow";
        private static readonly List<string> Results = new List<string>();
        private static readonly List<LevelDefinition> clones = new List<LevelDefinition>();
        private static IEnumerator sequence;
        private static LevelEditorWindow window;
        private static string folder, ownedShape;

        public static void Start()
        {
            sequence = Run(); EditorApplication.update += Tick;
        }

        /// <returns>실제 UI 바인딩과 저장 갱신을 기다리는 검사 단계.</returns>
        private static IEnumerator Run()
        {
            string run = File.ReadAllText(Evidence + "/full-run-path.txt").Trim();
            BotMoveBalanceRecord result = new BotMoveBalanceStore(run + "/records").Load();
            Check(result.trials.Count > 0, "기존 실제 완료 구간 추천 기록 사용·전체 시험 재실행 없음");
            string sourcePath = File.ReadAllText(run + "/source-asset.txt").Trim();
            string sourceDisk = File.ReadAllText(sourcePath), sourceGuid = AssetDatabase.AssetPathToGUID(sourcePath);
            folder = "Assets/__Stage31Apply_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            string path = folder + "/선택적용.asset";
            Check(AssetDatabase.CopyAsset(sourcePath, path), "추천 적용용 별도 레벨 사본 생성");
            LevelDefinition level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(path);
            string before = JsonUtility.ToJson(level), guid = AssetDatabase.AssetPathToGUID(path);
            int originalMoves = level.MoveCount;
            string registration = LevelShapeRecommendations.Register(level, out LevelShapePreset shape);
            Check(shape != null && shape.IsValid, "검사 사본과 일치하는 등록 맵 확보");
            if (registration == null) ownedShape = AssetDatabase.GetAssetPath(shape);
            string[] usedBefore = LevelShapeUsage.Read(new[] { shape })[shape].Select(entry => entry.Path).OrderBy(value => value).ToArray();
            // 실제 밸런스 실행과 같은 비영속 사본의 경계를 검사한다. 추가 플레이 판 수는 아니다.
            for (int moves = 1; moves <= 100; moves++)
            {
                LevelDefinition clone = ScriptableObject.CreateInstance<LevelDefinition>();
                clone.hideFlags = HideFlags.HideAndDontSave; clones.Add(clone);
                JsonUtility.FromJsonOverwrite(before, clone);
                JsonUtility.FromJsonOverwrite("{\"moveCount\":" + moves + "}", clone);
                if (moves == 1 || moves == 100)
                {
                    LevelStateBuildResult boundary = LevelStateBuilder.Build(clone, result.seeds[0]);
                    Check(boundary.IsBuilt && boundary.State.InitialMoves == moves && boundary.State.MovesRemaining == moves,
                        "공통 실행기 이동 수 경계 구성 " + moves + "회 · 추가 플레이 없음");
                }
            }
            string[] withClones = LevelShapeUsage.Read(new[] { shape })[shape].Select(entry => entry.Path).OrderBy(value => value).ToArray();
            Check(usedBefore.SequenceEqual(withClones), "이동 수 1~100의 메모리 사본은 맵 사용 레벨 수에 포함되지 않음");
            int selected = result.trials.First(trial => trial.grade >= 0 && trial.moves != level.MoveCount).moves;
            window = LevelEditorWindow.OpenWorkspace(0, level, true); window.ShowUtility();
            yield return null;
            Button tab = window.rootVisualElement.Q<Button>("inspector-tab-1");
            using (NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled()) { evt.target = tab; tab.SendEvent(evt); }
            yield return null;
            IntegerField field = window.rootVisualElement.Query<IntegerField>().ToList().First(item => item.bindingPath == "moveCount");
            Check(field.enabledInHierarchy, "추천 횟수를 입력할 실제 레벨 설정 접근");
            field.value = selected;
            for (int frame = 0; frame < 5; frame++) yield return null;
            Check(level.MoveCount == selected && EditorUtility.IsDirty(level), "실측 추천 횟수 입력·dirty 표시 상태");
            using (KeyDownEvent evt = KeyDownEvent.GetPooled(new Event { type = EventType.KeyDown, keyCode = KeyCode.S, modifiers = EventModifiers.Control }))
            { evt.target = window.rootVisualElement; window.rootVisualElement.SendEvent(evt); }
            for (int frame = 0; frame < 5; frame++) yield return null;
            Check(!EditorUtility.IsDirty(level), "Ctrl+S 후 미저장 상태 해제");
            UnityEngine.Object[] savedObjects = InternalEditorUtility.LoadSerializedFileAndForget(path);
            try
            {
                LevelDefinition saved = savedObjects.OfType<LevelDefinition>().Single();
                Check(saved.MoveCount == selected, "디스크 독립 재조회에서 추천 횟수 보존");
                JsonUtility.FromJsonOverwrite("{\"moveCount\":" + originalMoves + "}", saved);
                Check(JsonUtility.ToJson(saved) == before, "저장된 이동 수 외 모든 레벨 설정 보존");
            }
            finally { foreach (UnityEngine.Object value in savedObjects) UnityEngine.Object.DestroyImmediate(value); }
            window.Close(); window = LevelEditorWindow.OpenWorkspace(0, AssetDatabase.LoadAssetAtPath<LevelDefinition>(path), true);
            yield return null;
            Check(window.CurrentLevel.MoveCount == selected && AssetDatabase.AssetPathToGUID(path) == guid, "창 다시 열기와 적용 사본 GUID 보존");
            string[] usedAfter = LevelShapeUsage.Read(new[] { shape })[shape].Select(entry => entry.Path).OrderBy(value => value).ToArray();
            Check(usedBefore.SequenceEqual(usedAfter), "추천 횟수 저장·재열기는 맵 사용 수를 증가시키지 않음");
            Check(File.ReadAllText(sourcePath) == sourceDisk && AssetDatabase.AssetPathToGUID(sourcePath) == sourceGuid,
                "전체 시험의 대표 원본 파일·GUID 보존");
            Results.Add("DATA 선택한 실측 이동 횟수: " + selected);

            // 긴 비연속 범위는 합성 UI 입력이다. 원시 실측 기록이나 시험 완료로 저장하지 않는다.
            window.Close(); window = LevelEditorWindow.OpenWorkspace(1, level, true);
            window.ShowUtility(); window.position = new Rect(20, 20, 780, 860);
            BotMoveBalancePanel balance = (BotMoveBalancePanel)typeof(LevelInitialStatePanel)
                .GetField("balancePanel", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window.ActiveSimulationPanel);
            balance.Root.value = true;
            BotMoveBalanceRecord synthetic = JsonUtility.FromJson<BotMoveBalanceRecord>(JsonUtility.ToJson(result));
            synthetic.trials = Enumerable.Range(1, 100).Select(n => new BotMoveTrial {
                moves = n, normalPerStrategy = 100, grade = n % 2 == 1 ? 0 : -1 }).ToList();
            synthetic.status = BotBatchStatus.Completed;
            typeof(BotMoveBalancePanel).GetField("previous", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(balance, synthetic);
            typeof(BotMoveBalancePanel).GetMethod("Refresh", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(balance, null);
            for (int frame = 0; frame < 5; frame++) yield return null;
            Label range = window.rootVisualElement.Q<Label>("balance-recommendations");
            Check(range.text.Contains("1회, 3회") && range.text.Contains("99회") && !range.text.Contains("1~99회"), "합성 긴 범위는 빈 횟수를 연결하지 않음");
            Check(range.text.Contains("보통 : 추천 없음"), "합성 전체 완료에서 없는 등급은 추천 없음");
            Check(range.worldBound.width > 0 && range.worldBound.xMax <= window.rootVisualElement.worldBound.xMax + 1 &&
                range.worldBound.yMax <= window.rootVisualElement.worldBound.yMax + 1, "합성 긴 범위 최소 창 줄바꿈·접근");
            synthetic.rulesVersion = "unsupported-evaluation";
            typeof(BotMoveBalancePanel).GetMethod("Refresh", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(balance, null);
            Check(range.text.Contains("다른 기록") && window.rootVisualElement.Q<Label>("balance-rows").text.Contains("등급 표시 안 함"),
                "평가 버전 불일치 시 추천·상세 등급 모두 차단");
        }

        /// <param name="pass">검사 결과.</param><param name="message">증거 설명.</param>
        private static void Check(bool pass, string message)
        {
            if (!pass) throw new InvalidOperationException(message);
            Results.Add("PASS " + message);
        }

        private static void Tick()
        {
            try { if (sequence.MoveNext()) return; }
            catch (Exception error) { Results.Add("FAIL " + error); }
            EditorApplication.update -= Tick;
            try
            {
                if (window != null) window.Close();
                foreach (LevelDefinition clone in clones) UnityEngine.Object.DestroyImmediate(clone);
                if (!string.IsNullOrEmpty(folder) && folder.StartsWith("Assets/__Stage31Apply_", StringComparison.Ordinal) && !folder.Contains(".."))
                    Check(AssetDatabase.DeleteAsset(folder), "검사 소유 적용 사본 정리");
                if (!string.IsNullOrEmpty(ownedShape)) Check(AssetDatabase.DeleteAsset(ownedShape), "이번 검사에서 등록한 모양만 정리");
            }
            catch (Exception error) { Results.Add("FAIL 검사 소유 데이터 정리: " + error); }
            File.WriteAllLines(Evidence + "/apply-results.txt", Results);
            EditorApplication.Exit(Results.Any(line => line.StartsWith("FAIL ")) ? 1 : 0);
        }
    }
}

