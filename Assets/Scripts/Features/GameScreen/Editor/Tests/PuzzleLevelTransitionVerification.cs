using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using Levels;
using UnityEditor;
using UnityEngine;

namespace GameScreen.Editor
{
    public static partial class PuzzleLevelTransitionVerification
    {
        private const string Output = "Logs/Stage13/";
        private static readonly List<string> results = new List<string>();
        private static void Check(bool value, string name)
        { if (!value) throw new InvalidOperationException(name); results.Add("PASS " + name); }

        public static void Data()
        {
            results.Clear(); int exit = 0;
            GameObject owner = null;
            try
            {
                Type type = typeof(PuzzleGameSession);
                PropertyInfo changing = type.GetProperty("IsChangingLevel"), allowed = type.GetProperty("CanAdvanceLevel");
                Check(changing != null && changing.PropertyType == typeof(bool), "전환 진행 상태 계약");
                Check(allowed != null && allowed.PropertyType == typeof(bool), "전환 허용 상태 계약");
                MethodInfo advance = type.GetMethod("AdvanceLevelAsync", new[] { typeof(CancellationToken) });
                Check(advance != null && advance.ReturnType == typeof(UniTask<bool>), "비동기 전환 결과 계약");
                MethodInfo source = type.GetMethod("SetLevelAdvanceEnabled", new[] { typeof(bool) });
                Check(source != null, "에디터 소스 허용 계약");
                owner = new GameObject("Stage13-data-session");
                PuzzleGameSession session = owner.AddComponent<PuzzleGameSession>();
                Check(!(bool)allowed.GetValue(session) && !(bool)changing.GetValue(session), "미시작 세션 전환 불가");
                source.Invoke(session, new object[] { false });
                Check(!(bool)allowed.GetValue(session), "Asset 모드 전환 불가");
                Check(LevelPackCodec.Address(50) != LevelPackCodec.Address(51) && LevelPackCodec.LevelsPerPack == 50, "50→51 팩 주소 경계");
                LevelDefinition original = AssetDatabase.LoadAssetAtPath<LevelDefinition>("Assets/Data/Levels/Level_01.asset");
                string before = JsonUtility.ToJson(original);
                LevelDefinition owned = UnityEngine.Object.Instantiate(original);
                try
                {
                    JsonUtility.FromJsonOverwrite("{\"levelNumber\":50}", owned);
                    byte[] pack = LevelPackCodec.Encode(new[] { owned });
                    bool rejected = false;
                    try { UnityEngine.Object.DestroyImmediate(LevelPackCodec.ReadLevel(pack, 51)); }
                    catch (InvalidOperationException) { rejected = true; }
                    Check(rejected, "50 팩에서 51 읽기 오류 거부");
                    JsonUtility.FromJsonOverwrite("{\"levelNumber\":51}", owned);
                    byte[] nextPack = LevelPackCodec.Encode(new[] { owned });
                    LevelDefinition decoded = LevelPackCodec.ReadLevel(nextPack, 51);
                    try
                    {
                        Check(decoded.LevelNumber == 51, "51 팩 번호 정확성");
                        Simulation.StartingBoardSearch search = new Simulation.StartingBoardSearch(decoded, 12345);
                        while (!search.IsDone) search.Advance(128);
                        Check(search.Status == Simulation.StartingBoardStatus.Success && search.State.LevelNumber == 51,
                            "다음 팩 시작 보드 검색 유효");
                    }
                    finally { UnityEngine.Object.DestroyImmediate(decoded); }
                    Check(JsonUtility.ToJson(original) == before, "MemoryPack 사본 검색 원본 Asset 불변");
                }
                finally { UnityEngine.Object.DestroyImmediate(owned); }
                FieldInfo next = typeof(PuzzleResultView).GetField("nextLevel", BindingFlags.Instance | BindingFlags.NonPublic);
                Check(next != null && next.FieldType == typeof(UnityEngine.UI.Button), "결과 팝업 다음 레벨 버튼 계약");
                GameObject popup = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Puzzle/PuzzleResultPopup.prefab");
                Check(next.GetValue(popup.GetComponent<PuzzleResultView>()) != null, "결과 프리팹 다음 버튼 직렬화 연결");
                PropertyInfo modeProperty = typeof(PuzzleEditorLaunchRequest).GetProperty("Source");
                Check(modeProperty != null && modeProperty.PropertyType == typeof(PuzzleEditorLevelSource), "실행 요청 입력 소스 계약");
                LevelDefinition unsaved = UnityEngine.Object.Instantiate(original);
                try
                {
                    JsonUtility.FromJsonOverwrite("{\"moveCount\":37}", unsaved);
                    PuzzleEditorLaunchRequest asset = PuzzleEditorLaunchRequest.Capture(unsaved, PuzzleEditorLevelSource.Asset, 8765);
                    PuzzleEditorLaunchRequest packed = PuzzleEditorLaunchRequest.Capture(unsaved, PuzzleEditorLevelSource.MemoryPack, 7654);
                    LevelDefinition selected = asset.CreateDefinition();
                    try
                    {
                        Check((PuzzleEditorLevelSource)modeProperty.GetValue(asset) == PuzzleEditorLevelSource.Asset && selected.MoveCount == 37 && asset.Seed == 8765,
                            "Asset 입력 소스·미저장 사본·시드 전달");
                        Check((PuzzleEditorLevelSource)modeProperty.GetValue(packed) == PuzzleEditorLevelSource.MemoryPack && packed.LevelNumber == 1 && packed.Seed == 7654,
                            "MemoryPack 입력 소스·선택 번호·시드 전달");
                    }
                    finally { UnityEngine.Object.DestroyImmediate(selected); }
                }
                finally { UnityEngine.Object.DestroyImmediate(unsaved); }
            }
            catch (Exception error) { results.Add("FAIL " + error); exit = 1; }
            finally
            {
                if (owner != null) UnityEngine.Object.DestroyImmediate(owner);
                Directory.CreateDirectory(Output); File.WriteAllLines(Output + "data-results.txt", results);
                EditorApplication.Exit(exit);
            }
        }
    }
}
