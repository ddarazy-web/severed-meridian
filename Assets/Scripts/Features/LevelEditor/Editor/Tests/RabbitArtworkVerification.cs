using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Board;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    /// <summary>토끼 그림 표시와 덮개 은폐, 기존 레벨 데이터 보존을 작은 메모리 레벨로 검사한다.</summary>
    public static class RabbitArtworkVerification
    {
        private static readonly List<string> results = new List<string>();
        private static readonly string[] names = { "pink", "yellow", "blue", "green", "purple" };
        private static IEnumerator visualSteps;
        private static LevelEditorWindow visualWindow;
        private static LevelDefinition visualLevel;
        private static double visualDeadline;

        /// <summary>검사 전용 창만 열어 편집 보드·테스트 보드의 실제 렌더링을 캡처한다.</summary>
        public static void Capture()
        {
            visualDeadline = EditorApplication.timeSinceStartup + 60;
            visualSteps = CaptureSteps();
            EditorApplication.update += CaptureTick;
        }

        private static IEnumerator CaptureSteps()
        {
            visualLevel = ScriptableObject.CreateInstance<LevelDefinition>();
            for (int row = 0; row < BoardDefinition.DefaultRows; row++)
                for (int column = 0; column < BoardDefinition.DefaultColumns; column++)
                    LevelBoardEditing.Apply(visualLevel, LevelBrush.Fixed, (RabbitColor)((row * 2 + column) % 5), new[] { new BoardCoordinate(row, column) });
            LevelMissionEditing.Add(visualLevel);
            visualWindow = ScriptableObject.CreateInstance<LevelEditorWindow>();
            visualWindow.ShowUtility(); visualWindow.position = new Rect(30, 30, 1180, 820); visualWindow.SetLevel(visualLevel);
            for (int i = 0; i < 15; i++) yield return null;
            Button palette = visualWindow.rootVisualElement.Q<Button>("normal-rabbit-2");
            using (NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled()) { evt.target = palette; palette.SendEvent(evt); }
            if (visualWindow.rootVisualElement.Q<LevelBoardView>().Color != RabbitColor.Type3)
                throw new InvalidOperationException("그림 버튼의 실제 브러시 선택 실패");
            while (LevelBoardArtwork.Rabbit(RabbitColor.Type1) == null) yield return null;
            if (visualWindow.rootVisualElement.Q<LevelBoardView>().CellAt(new BoardCoordinate(0, 0))
                .Q("board-content-art")?.style.backgroundImage.value.sprite == null)
                throw new InvalidOperationException("캡처 대상 편집 보드 이미지가 아직 표시되지 않았습니다.");
            for (int i = 0; i < 10; i++) yield return null;
            Directory.CreateDirectory("Logs/BotAnalysisVerification");
            visualWindow.titleContent = new GUIContent("Artwork Verification 9x9");
            BotAnalysisVerification.CaptureWindow("rabbit-editor.png", "Artwork Verification 9x9");
            File.Copy("Logs/BotAnalysisVerification/rabbit-editor.png", "Logs/RabbitArtworkVerification/editor.png", true);
            visualWindow.SelectWorkspaceTab(2);
            LevelInitialStatePanel panel = visualWindow.ActiveSimulationPanel;
            typeof(LevelInitialStatePanel).GetMethod("Build", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(panel, null);
            for (int i = 0; i < 15; i++) yield return null;
            visualWindow.titleContent = new GUIContent("Artwork Verification 9x9");
            BotAnalysisVerification.CaptureWindow("rabbit-play.png", "Artwork Verification 9x9");
            File.Copy("Logs/BotAnalysisVerification/rabbit-play.png", "Logs/RabbitArtworkVerification/play.png", true);
        }

        private static void CaptureTick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > visualDeadline) throw new TimeoutException("토끼 화면 캡처 시간 초과");
                if (visualSteps.MoveNext()) return;
                File.WriteAllText("Logs/RabbitArtworkVerification/visual.txt", "PASS 실제 그림 버튼 선택 및 편집·테스트 창 캡처");
            }
            catch (Exception error)
            {
                File.WriteAllText("Logs/RabbitArtworkVerification/visual.txt", "FAIL " + error);
                Debug.LogException(error);
            }
            EditorApplication.update -= CaptureTick;
            if (visualWindow != null) visualWindow.Close();
            if (visualLevel != null) UnityEngine.Object.DestroyImmediate(visualLevel);
            EditorApplication.Exit(File.ReadAllText("Logs/RabbitArtworkVerification/visual.txt").StartsWith("PASS") ? 0 : 1);
        }

        /// <summary>이번에 추가한 다섯 PNG의 에디터 표시용 임포트 설정을 적용하고 검사를 실행한다. 다른 에셋은 변경하지 않는다.</summary>
        public static void ConfigureAndRun()
        {
            for (int i = 0; i < names.Length; i++)
            {
                TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(PathFor(i));
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = 256;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            Run();
        }

        /// <summary>배치 실행 진입점. 원본 레벨·시험 기록은 수정하지 않으며 검사 결과만 Logs에 쓴다.</summary>
        public static void Run() => RunAsync().Forget(Debug.LogException);

        private static async UniTask RunAsync()
        {
            LevelDefinition level = ScriptableObject.CreateInstance<LevelDefinition>();
            LevelEditorWindow window = null;
            try
            {
                results.Clear();
                await LevelBoardArtwork.Warmup();
                for (int i = 0; i < 5; i++)
                    LevelBoardEditing.Apply(level, LevelBrush.Fixed, (RabbitColor)i, new[] { new BoardCoordinate(0, i) });
                LevelBoardEditing.Apply(level, LevelBrush.Random, RabbitColor.Type1, new[] { new BoardCoordinate(0, 5) });
                LevelMissionEditing.Add(level);
                string before = JsonUtility.ToJson(level);
                LevelBoardView board = new LevelBoardView();
                board.Display(level, new BoardCoordinate(0, 0));
                for (int i = 0; i < 5; i++)
                {
                    Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(PathFor(i));
                    Check(texture != null && texture.width == 256 && texture.height == 256, "256px 텍스처 로드 " + names[i]);
                    Check(board.CellAt(new BoardCoordinate(0, i)).Q("board-content-art").style.backgroundImage.value.sprite == LevelBoardArtwork.Rabbit((RabbitColor)i) && texture != null, "편집 보드 색 매핑 " + names[i]);
                }
                Check(board.CellAt(new BoardCoordinate(0, 5)).Q<Label>("board-art-badge").text == "?" &&
                    board.CellAt(new BoardCoordinate(0, 5)).style.backgroundImage.value.texture == null, "무작위 물음표 유지");
                Check(JsonUtility.ToJson(level) == before, "보드 표시가 원본 데이터를 변경하지 않음");

                window = ScriptableObject.CreateInstance<LevelEditorWindow>();
                window.ShowUtility(); window.SetLevel(level);
                Check(window.rootVisualElement.Query<Button>().ToList().Count(b => b.name != null && b.name.StartsWith("normal-rabbit-")) == 5, "선택 목록에 토끼 5종 표시");

                LevelInitialStatePanel panel = new LevelInitialStatePanel();
                panel.Initialize(window, level, false);
                typeof(LevelInitialStatePanel).GetMethod("Build", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(panel, null);
                Check(panel.CurrentState != null, "테스트 표시용 초기 보드 구성");
                if (panel.CurrentState != null)
                {
                    for (int i = 0; i < 5; i++)
                        Check(panel.rootVisualElement.Q<Button>($"initial-cell-0-{i}").Q("runtime-content").style.backgroundImage.value.sprite ==
                            LevelBoardArtwork.Rabbit((RabbitColor)i), "플레이 테스트 색 매핑 " + names[i]);
                    object cell = panel.CurrentState.CellAt(new BoardCoordinate(0, 0));
                    cell.GetType().GetProperty("Cover").SetValue(cell, (CoverKind?)CoverKind.Mold);
                    typeof(LevelInitialStatePanel).GetMethod("DisplayState", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(panel, new object[] { null });
                    Button covered = panel.rootVisualElement.Q<Button>("initial-cell-0-0");
                    Check(covered.Q("runtime-content").style.backgroundImage.value.sprite == null && covered.text.Contains("곰팡이"), "곰팡이 아래 토끼 이미지 은폐");
                }
                Check(JsonUtility.ToJson(level) == before, "메뉴·초기 보드 표시 후 원본 보존");
                LevelBoardEditing.Apply(level, LevelBrush.Erase, RabbitColor.Type1, new[] { new BoardCoordinate(0, 0) });
                board.Display(level, null);
                Check(board.CellAt(new BoardCoordinate(0, 0)).style.backgroundImage.value.texture == null, "지운 칸의 이전 토끼 이미지 제거");
            }
            catch (Exception error) { results.Add("FAIL " + error); }
            finally
            {
                if (window != null) window.Close();
                UnityEngine.Object.DestroyImmediate(level);
                Directory.CreateDirectory("Logs/RabbitArtworkVerification");
                File.WriteAllLines("Logs/RabbitArtworkVerification/results.txt", results);
                Debug.Log(string.Join("\n", results));
                EditorApplication.Exit(results.Any(r => r.StartsWith("FAIL")) ? 1 : 0);
            }
        }

        private static string PathFor(int index) => "Assets/Textures/Blocks/rabbit-" + names[index] + "-v1-256.png";
        private static void Check(bool success, string label) => results.Add((success ? "PASS " : "FAIL ") + label);
    }
}
