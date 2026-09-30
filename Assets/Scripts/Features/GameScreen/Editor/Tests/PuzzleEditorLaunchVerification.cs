using System;
using System.IO;
using System.Reflection;
using Levels;
using Levels.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameScreen.Editor
{
    public static class PuzzleEditorLaunchVerification
    {
        public static void RunRejectedInputs()
        {
            string output = "Logs/PuzzleEditorLaunchVerification/rejected-results.txt";
            File.WriteAllText(output, "");
            LevelDefinition source = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<LevelDefinition>("Assets/Data/Levels/Level_01.asset"));
            string alternate = "Assets/Scenes/PuzzleGame.StageThreeMissing.unity";
            string guid = AssetDatabase.AssetPathToGUID(PuzzleGameAssets.ScenePath);
            bool moved = false;
            try
            {
                Reject(() => PuzzleEditorLaunchRequest.Capture(null, PuzzleEditorLevelSource.Asset, 1), "NoSelectionRejected");
                JsonUtility.FromJsonOverwrite("{\"levelNumber\":0}", source);
                Reject(() => PuzzleEditorLaunchRequest.Capture(source, PuzzleEditorLevelSource.Asset, 1), "InvalidNumberRejected");
                JsonUtility.FromJsonOverwrite("{\"levelNumber\":1}", source);
                PuzzleEditorLaunchRequest request = PuzzleEditorLaunchRequest.Capture(source, PuzzleEditorLevelSource.Asset, 1);
                if (File.Exists(alternate)) throw new Exception("임시 씬 경로가 이미 사용 중입니다.");
                string error = AssetDatabase.MoveAsset(PuzzleGameAssets.ScenePath, alternate);
                if (error != "") throw new Exception(error);
                moved = true;
                Reject(() => PuzzleEditorLauncher.Launch(request, 0), "MissingSceneRejected");
                if (EditorApplication.isPlayingOrWillChangePlaymode || PuzzleEditorLauncher.IsBusy) throw new Exception("잘못된 입력으로 Play 진입");
            }
            catch (Exception error) { File.AppendAllText(output, "FAIL " + error + "\n"); }
            finally
            {
                if (moved)
                {
                    string error = AssetDatabase.MoveAsset(alternate, PuzzleGameAssets.ScenePath);
                    if (error != "" || AssetDatabase.AssetPathToGUID(PuzzleGameAssets.ScenePath) != guid) throw new Exception("검사 씬 복원 실패: " + error);
                }
                UnityEngine.Object.DestroyImmediate(source);
            }
            void Reject(Action action, string name)
            {
                bool rejected = false;
                try { action(); } catch (InvalidOperationException) { rejected = true; }
                if (!rejected) throw new Exception(name);
                File.AppendAllText(output, "PASS " + name + "\n");
            }
        }

        public static void RunUI()
        {
            string output = "Logs/PuzzleEditorLaunchVerification/ui-results.txt";
            LevelEditorWindow window = ScriptableObject.CreateInstance<LevelEditorWindow>();
            try
            {
                window.ShowUtility(); window.CreateGUI();
                if (window.rootVisualElement.Q<Button>("play-level") == null) throw new Exception("기존 플레이 테스트 버튼 누락");
                Button launch = window.rootVisualElement.Q<Button>("game-play-level");
                if (launch == null) throw new Exception("게임 플레이 버튼 누락");
                if (window.rootVisualElement.Q<PopupField<string>>("game-level-source")?.value != "에셋") throw new Exception("기본 에셋 입력 누락");
                if (window.rootVisualElement.Q<IntegerField>("game-level-seed")?.value != 12345) throw new Exception("기본 시드 누락");
                if (launch.enabledSelf) throw new Exception("선택 없는 게임 실행이 활성화되었습니다.");
                File.WriteAllText(output, "PASS ExistingPlayTestPreserved\nPASS GameControlsAndDefaults\nPASS NoSelectionDisabled\n");
            }
            catch (Exception error) { File.WriteAllText(output, "FAIL " + error); }
            finally { window.Close(); }
        }

        [MenuItem("Tools/Match/게임 실행 입력 검증")]
        public static void RunData()
        {
            string output = "Logs/PuzzleEditorLaunchVerification/data-results.txt";
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            LevelDefinition source = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<LevelDefinition>("Assets/Data/Levels/Level_01.asset"));
            LevelDefinition copy = null;
            try
            {
                Type requestType = typeof(PuzzleEditorLaunchVerification).Assembly.GetType("GameScreen.Editor.PuzzleEditorLaunchRequest");
                if (requestType == null) throw new Exception("AssetSnapshotIncludesUnsavedChanges: 실행 요청 구현이 없습니다.");
                Type modeType = requestType.Assembly.GetType("GameScreen.Editor.PuzzleEditorLevelSource");
                JsonUtility.FromJsonOverwrite("{\"moveCount\":37}", source);
                object request = requestType.GetMethod("Capture").Invoke(null, new object[] { source, Enum.ToObject(modeType, 0), 8765 });
                JsonUtility.FromJsonOverwrite("{\"moveCount\":49}", source);
                copy = (LevelDefinition)requestType.GetMethod("CreateDefinition").Invoke(request, null);
                if (copy.MoveCount != 37) throw new Exception("AssetSnapshotIncludesUnsavedChanges: 클릭 시점 미저장 값이 보존되지 않았습니다.");
                if (ReferenceEquals(copy.Board, source.Board) || ReferenceEquals(copy.Colors, source.Colors) || ReferenceEquals(copy.InitialBlocks, source.InitialBlocks))
                    throw new Exception("SnapshotDoesNotShareCollections: 원본 참조를 공유합니다.");
                File.WriteAllText(output, "PASS AssetSnapshotIncludesUnsavedChanges\nPASS SnapshotFixedAtCapture\nPASS SnapshotDoesNotShareCollections\n");
                UnityEngine.Object.DestroyImmediate(copy); copy = null;
                // 실제 생성 팩은 읽기만 한다. 에셋 사본의 미저장 값과 다른 파일 값을 비교한다.
                object packRequest = requestType.GetMethod("Capture").Invoke(null, new object[] { source, Enum.ToObject(modeType, 1), 7654 });
                copy = (LevelDefinition)requestType.GetMethod("CreateDefinition").Invoke(packRequest, null);
                LevelDefinition disk = LevelPackCodec.ReadLevel(File.ReadAllBytes(LevelPackBuild.FilePath(source.LevelNumber)), source.LevelNumber);
                try
                {
                    if (copy.MoveCount != disk.MoveCount || copy.MoveCount == 49) throw new Exception("PackSnapshotUsesGeneratedBytes");
                }
                finally { UnityEngine.Object.DestroyImmediate(disk); }
                File.AppendAllText(output, "PASS PackSnapshotUsesGeneratedBytes\n");
                // 충돌하지 않는 검사 전용 번호 구간에만 임시 파일을 만든다.
                const int testNumber = 900001;
                string packPath = LevelPackBuild.FilePath(testNumber);
                if (File.Exists(packPath) || File.Exists(packPath + ".meta")) throw new Exception("검사용 구간에 기존 파일이 있습니다.");
                JsonUtility.FromJsonOverwrite("{\"levelNumber\":900001,\"moveCount\":31}", source);
                AssetDatabase.DisallowAutoRefresh();
                try
                {
                    Reject("MissingPackRejected");
                    File.WriteAllBytes(packPath, new byte[] { 1, 2, 3 }); Reject("CorruptPackRejected");
                    File.WriteAllBytes(packPath, LevelPackCodec.Encode(new[] { source }));
                    JsonUtility.FromJsonOverwrite("{\"levelNumber\":900002}", source); Reject("MissingNumberRejected");
                    JsonUtility.FromJsonOverwrite("{\"levelNumber\":900001}", source);
                    packRequest = requestType.GetMethod("Capture").Invoke(null, new object[] { source, Enum.ToObject(modeType, 1), 42 });
                    File.WriteAllBytes(packPath, new byte[] { 1 });
                    LevelDefinition fixedPack = (LevelDefinition)requestType.GetMethod("CreateDefinition").Invoke(packRequest, null);
                    try { if (fixedPack.MoveCount != 31) throw new Exception("PackSnapshotFixedAtCapture"); }
                    finally { UnityEngine.Object.DestroyImmediate(fixedPack); }
                    File.AppendAllText(output, "PASS PackSnapshotFixedAtCapture\n");
                }
                finally { if (File.Exists(packPath)) File.Delete(packPath); AssetDatabase.AllowAutoRefresh(); }
                foreach (int number in new[] { 50, 51 })
                {
                    JsonUtility.FromJsonOverwrite("{\"levelNumber\":" + number + "}", source);
                    LevelDefinition boundary = LevelPackCodec.ReadLevel(LevelPackCodec.Encode(new[] { source }), number);
                    try { if (boundary.LevelNumber != number) throw new Exception("PackBoundary"); }
                    finally { UnityEngine.Object.DestroyImmediate(boundary); }
                }
                File.AppendAllText(output, "PASS PackBoundary50And51\n");
                void Reject(string name)
                {
                    bool rejected = false;
                    try { requestType.GetMethod("Capture").Invoke(null, new object[] { source, Enum.ToObject(modeType, 1), 12345 }); }
                    catch (TargetInvocationException) { rejected = true; }
                    if (!rejected) throw new Exception(name);
                    File.AppendAllText(output, "PASS " + name + "\n");
                }
            }
            catch (Exception error) { File.WriteAllText(output, "FAIL " + error); Debug.LogException(error); }
            finally
            {
                if (copy != null) UnityEngine.Object.DestroyImmediate(copy);
                UnityEngine.Object.DestroyImmediate(source);
            }
        }
    }
}
