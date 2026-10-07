using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Tutorial;
using GameScreen.Editor;
using System.Collections.Generic;
using Levels;
using Levels.Editor;
using Tutorial.Editor;

public static partial class TutorialGuidanceVerification
{
    public static void Run()
    {
        Directory.CreateDirectory("Logs/Tutorial/Stage04");
        try
        {
            Type type = typeof(Tutorial.TutorialProgress).Assembly.GetType("Tutorial.TutorialExecutionContext");
            if (type == null) throw new InvalidOperationException("완료 기록과 시험 실행 문맥이 없습니다.");
            object context = type.GetMethod("CreateTest").Invoke(null, new object[] { 0 });
            MethodInfo shouldRun = type.GetMethod("ShouldRun"), complete = type.GetMethod("Complete");
            if (!(bool)shouldRun.Invoke(context, new object[] { 1, true })) throw new Exception("미완료 자동 안내가 생략됨");
            complete.Invoke(context, new object[] { 1 });
            if ((bool)shouldRun.Invoke(context, new object[] { 1, true })) throw new Exception("완료 자동 안내가 반복됨");
            object independent = type.GetMethod("CreateTest").Invoke(null, new object[] { 0 });
            if (!(bool)shouldRun.Invoke(independent, new object[] { 1, true })) throw new Exception("시험 문맥이 실제/다른 시험 기록을 변경함");
            foreach (int mode in new[] { 1, 2 })
            {
                object trial = type.GetMethod("CreateTest").Invoke(null, new object[] { mode });
                complete.Invoke(trial, new object[] { 1 });
                if ((bool)shouldRun.Invoke(trial, new object[] { 1, true }) != (mode == 1)) throw new Exception("항상/실행 안 함 정책 오류");
                if ((bool)shouldRun.Invoke(trial, new object[] { 1, false })) throw new Exception("튜토리얼 없는 레벨 실행 오류");
            }
            Type view = typeof(Tutorial.TutorialProgress).Assembly.GetType("Tutorial.TutorialOverlayView");
            if (view == null) throw new Exception("실제 안내 표시 View가 없습니다.");
            LevelDefinition representative = AssetDatabase.LoadAssetAtPath<LevelDefinition>("Assets/Data/Levels/Level_01.asset");
            VerifyEditorModes(representative);
            if (!representative.HasTutorial || representative.Tutorial.steps.Count != 3 || LevelTutorialReplayValidator.Validate(representative).Count != 0)
                throw new Exception("대표 1레벨 기본 매칭 안내·재생 누락");
            LevelDefinition packed = LevelPackCodec.ReadLevel(File.ReadAllBytes(LevelPackBuild.FilePath(1)), 1);
            try
            {
                if (JsonUtility.ToJson(representative.Tutorial) != JsonUtility.ToJson(packed.Tutorial) || LevelTutorialReplayValidator.Validate(packed).Count != 0)
                    throw new Exception("대표 Asset/팩3 안내/재생 불일치");
                foreach (TutorialRunMode mode in Enum.GetValues(typeof(TutorialRunMode)))
                {
                    PuzzleEditorLaunchRequest request = PuzzleEditorLaunchRequest.Capture(representative, PuzzleEditorLevelSource.MemoryPack, 4321, mode);
                    if (request.TutorialMode != mode) throw new Exception("에디터 시험 모드 전달 누락");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(packed); }
            int writes = 0;
            HashSet<int> records = new HashSet<int>();
            TutorialExecutionContext counted = TutorialExecutionContext.CreateEditor(TutorialRunMode.Automatic, records.Contains, level => { writes++; records.Add(level); });
            counted.Complete(4); counted.Complete(4);
            if (writes != 1 || counted.ShouldRun(4, true) || !counted.ShouldRun(5, true)) throw new Exception("레벨별 완료 1회 기록 오류");
            string path = "Assets/Prefabs/UI/Puzzle/__TutorialStage04Verification.prefab";
            if (System.IO.File.Exists(path)) throw new Exception("검사 소유권 없는 임시 프리팹 존재");
            try
            {
                GameObject prefab = PuzzleTutorialAssets.GenerateOverlay(path);
                TutorialOverlayView overlay = prefab.GetComponent<TutorialOverlayView>();
                if (overlay.Finger.GetComponent<CanvasRenderer>() == null) throw new Exception("손가락 그래픽 렌더러가 없어 화면에 그려지지 않음");
                if (overlay == null || overlay.Next == null || prefab.GetComponent<GameScreen.PuzzleTutorialBinding>() == null) throw new Exception("프리팹 안내 연결 누락");
                foreach (UnityEngine.UI.Graphic graphic in prefab.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                    if (graphic.raycastTarget != (graphic.gameObject == overlay.Next.gameObject)) throw new Exception("장식이 보드 터치를 가로챔: " + graphic.name);
                if (overlay.GetComponentsInChildren<UnityEngine.UI.Image>(true).Length < 81) throw new Exception("보드 강조/어둠 칸 누락");
                GameObject owned = UnityEngine.Object.Instantiate(prefab);
                try
                {
                    TutorialOverlayView actual = owned.GetComponent<TutorialOverlayView>();
                    LevelTutorialDefinition data = new LevelTutorialDefinition(); data.steps.Add(new TutorialStepDefinition { instructions = "첫 설명" });
                    using (TutorialProgress progress = new TutorialProgress(data))
                    {
                        actual.Display(progress.Snapshot, true, true);
                        if (!actual.Content.gameObject.activeSelf || !actual.Next.gameObject.activeSelf || !actual.Next.interactable) throw new Exception("설명·다음 표시 실패");
                        actual.Display(null, false, false);
                        if (actual.Content.gameObject.activeSelf) throw new Exception("안내 해제 실패");
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(owned); }
            }
            finally { AssetDatabase.DeleteAsset(path); }
            File.WriteAllText("Logs/Tutorial/Stage04/guidance-results.txt", "PASS 자동 실행·완료 생략·시험 문맥 격리\nPASS 항상/실행 안 함·튜토리얼 없는 레벨\nPASS 레벨별 완료 기록 1회\nPASS 실제 안내 프리팹 연결·설명/다음 표시·해제\nPASS 장식 raycast 차단 없음\n");
            StartPlayVerification();
        }
        catch (Exception error)
        {
            File.WriteAllText("Logs/Tutorial/Stage04/guidance-results.txt", "FAIL " + error + "\n");
            Debug.LogException(error); EditorApplication.Exit(1);
        }
    }

    public static void ApplyAssets()
    {
        try { PuzzleTutorialAssets.Apply(); EditorApplication.Exit(0); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    private static void VerifyEditorModes(LevelDefinition representative)
    {
        LevelDefinition owned = UnityEngine.Object.Instantiate(representative);
        LevelEditorWindow window = ScriptableObject.CreateInstance<LevelEditorWindow>();
        try
        {
            window.ShowUtility(); window.CreateGUI(); window.SetLevel(owned);
            UnityEngine.UIElements.PopupField<string> field = window.rootVisualElement.Q<UnityEngine.UIElements.PopupField<string>>("game-tutorial-mode");
            if (field == null) throw new Exception("에디터 실제 튜토리얼 옵션 UI 누락");
            foreach (TutorialRunMode mode in Enum.GetValues(typeof(TutorialRunMode)))
            {
                field.value = field.choices[(int)mode];
                object selected = typeof(LevelEditorWindow).GetField("gameTutorialMode", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window);
                if ((TutorialRunMode)selected != mode) throw new Exception("에디터 옵션 변경 전달 실패 " + mode);
            }
        }
        finally { window.Close(); UnityEngine.Object.DestroyImmediate(owned); }
    }
}
