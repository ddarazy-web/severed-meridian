using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace GameScreen.Editor
{
    public static partial class PuzzlePopupVerification
    {
        public static void RunDesign()
        {
            const string sourceRoot = "Assets/Prefabs/UI/Puzzle/";
            const string ownedRoot = "Assets/Prefabs/UI/PopupStage03OwnedDesign";
            const string baseline = "89431f4e1cadc33347b037a894be19c729de1ac9";
            results.Clear(); int exit = 0;
            List<GameObject> loaded = new List<GameObject>();
            Dictionary<string, byte[]> originals = new Dictionary<string, byte[]>();
            try
            {
                Check(!Directory.Exists(ownedRoot), "시각 보존 검사 소유 경로 충돌 없음");
                Directory.CreateDirectory(ownedRoot);
                foreach (string name in new[] { "PuzzleScreen", "PuzzlePausePopup", "PuzzleResultPopup" })
                {
                    string originalPath = sourceRoot + name + ".prefab";
                    originals.Add(originalPath, File.ReadAllBytes(originalPath));
                    ProcessStartInfo start = new ProcessStartInfo("git", "show " + baseline + ":" + originalPath)
                        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                    using Process process = Process.Start(start);
                    string yaml = process.StandardOutput.ReadToEnd(); string error = process.StandardError.ReadToEnd(); process.WaitForExit();
                    Check(process.ExitCode == 0 && yaml.StartsWith("%YAML"), "기준 프리팹 읽기 " + name + error);
                    string copy = ownedRoot + "/" + name + ".prefab";
                    File.WriteAllText(copy, yaml); AssetDatabase.ImportAsset(copy, ImportAssetOptions.ForceSynchronousImport);
                    GameObject before = PrefabUtility.LoadPrefabContents(copy); loaded.Add(before);
                    GameObject after = PrefabUtility.LoadPrefabContents(originalPath); loaded.Add(after);
                    if (name == "PuzzleScreen")
                    {
                        Check(VisualSignature(before.transform, true) == VisualSignature(after.transform, true),
                            "게임 화면 기존 보드 HUD 아이템 배경 Rect Image Text Button 디자인 보존");
                        GameObject description = PrefabUtility.LoadPrefabContents(sourceRoot + "PuzzleDescriptionPopup.prefab"); loaded.Add(description);
                        Check(VisualSignature(before.transform.Find("SafeArea/MissionDescription"), false) == VisualSignature(description.transform, false),
                            "설명 프리팹 분리 기존 Rect Image Text Button 디자인 보존");
                    }
                    else Check(VisualSignature(before.transform, false) == VisualSignature(after.transform, false),
                        "기존 팝업 Rect Image Text Button 디자인 보존 " + name);
                }
                foreach (KeyValuePair<string, byte[]> item in originals)
                    Check(File.ReadAllBytes(item.Key).SequenceEqual(item.Value), "시각 검사 원본 프리팹 쓰기0 " + item.Key);
            }
            catch (Exception error) { exit = 1; results.Add("FAIL " + error); }
            finally
            {
                foreach (GameObject root in loaded) PrefabUtility.UnloadPrefabContents(root);
                AssetDatabase.DeleteAsset(ownedRoot);
                Directory.CreateDirectory(Output); File.WriteAllLines(Output + "stage03-design-results.txt", results);
                EditorApplication.Exit(exit);
            }
        }
        private static string VisualSignature(Transform root, bool screen)
        {
            StringBuilder value = new StringBuilder();
            foreach (RectTransform rect in root.GetComponentsInChildren<RectTransform>(true))
            {
                string path = AnimationUtility.CalculateTransformPath(rect, root);
                if (screen && new[] { "SafeArea/MissionDescription", "SafeArea/PuzzlePausePopup", "SafeArea/PuzzleResultPopup", "SafeArea/PopupHost" }
                    .Any(excluded => path == excluded || path.StartsWith(excluded + "/"))) continue;
                value.AppendLine(path + ":" + rect.anchorMin.ToString("R") + rect.anchorMax.ToString("R") + rect.anchoredPosition.ToString("R") +
                    rect.sizeDelta.ToString("R") + rect.pivot.ToString("R") + rect.localScale.ToString("R") + rect.localRotation.ToString("R"));
                foreach (Image image in rect.GetComponents<Image>())
                    value.AppendLine("Image:" + AssetDatabase.GetAssetPath(image.sprite) + image.color.ToString("R") + image.type + image.preserveAspect + image.raycastTarget);
                foreach (Text text in rect.GetComponents<Text>())
                    value.AppendLine("Text:" + text.text + AssetDatabase.GetAssetPath(text.font) + text.fontSize + text.fontStyle + text.alignment +
                        text.color.ToString("R") + text.lineSpacing + text.horizontalOverflow + text.verticalOverflow + text.supportRichText +
                        text.resizeTextForBestFit + text.resizeTextMinSize + text.resizeTextMaxSize);
                foreach (Button button in rect.GetComponents<Button>())
                {
                    ColorBlock colors = button.colors;
                    value.AppendLine("Button:" + button.transition + colors.normalColor.ToString("R") + colors.highlightedColor.ToString("R") +
                        colors.pressedColor.ToString("R") + colors.selectedColor.ToString("R") + colors.disabledColor.ToString("R") + colors.colorMultiplier + colors.fadeDuration);
                }
            }
            return value.ToString();
        }
    }
}
