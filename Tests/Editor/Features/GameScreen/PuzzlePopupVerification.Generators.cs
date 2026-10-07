using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PopupUI;
using UnityEditor;
using UnityEngine;

namespace GameScreen.Editor
{
    public static partial class PuzzlePopupVerification
    {
        public static void RunGenerators()
        {
            const string folder = "Assets/Prefabs/UI/PopupStage03OwnedGameGenerators";
            results.Clear(); int exit = 0; bool owned = false;
            try
            {
                Check(!Directory.Exists(folder) && !File.Exists(folder + ".meta"), "생성 도구 시험 전용 경로 미사용");
                Dictionary<string, byte[]> originals = Directory.GetFiles(PuzzleUIAssets.Folder, "*.prefab").Append(PuzzleGameAssets.ScenePath).Append(PuzzlePopupAssets.CatalogPath).ToDictionary(path => path, File.ReadAllBytes);
                AssetDatabase.CreateFolder("Assets/Prefabs/UI", "PopupStage03OwnedGameGenerators"); owned = true;
                foreach (string path in Directory.GetFiles(PuzzleUIAssets.Folder, "*.prefab"))
                    Check(AssetDatabase.CopyAsset(path, folder + "/" + Path.GetFileName(path)), "원본 UI 임시 복사 " + Path.GetFileName(path));
                Check(AssetDatabase.CopyAsset(PuzzlePopupAssets.CatalogPath, folder + "/PopupCatalog.asset"), "원본 catalog 임시 복사");
                PopupCatalog copiedCatalog = AssetDatabase.LoadAssetAtPath<PopupCatalog>(folder + "/PopupCatalog.asset");
                PopupView customPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Popup/PopupTemplate.prefab").GetComponent<PopupView>();
                copiedCatalog.Configure(copiedCatalog.Entries.Concat(new[] { new PopupCatalog.Entry { Id = "owned.extra", Prefab = customPrefab,
                    AllowMultiple = true, Restorable = true, PauseGameplay = true, CloseOnCancel = false } }).ToArray());
                EditorUtility.SetDirty(copiedCatalog); AssetDatabase.SaveAssetIfDirty(copiedCatalog);
                string screenGuid = AssetDatabase.AssetPathToGUID(folder + "/PuzzleScreen.prefab");
                string resultGuid = AssetDatabase.AssetPathToGUID(folder + "/PuzzleResultPopup.prefab");
                for (int repeat = 0; repeat < 2; repeat++)
                {
                    PuzzleUIAssets.GeneratePrefabs(folder);
                    PuzzleLevelTransitionAssets.ApplyToPrefab(folder + "/PuzzleResultPopup.prefab");
                    PuzzlePopupAssets.ApplyToPrefabs(folder, folder + "/PopupCatalog.asset");
                    GameObject screen = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "/PuzzleScreen.prefab");
                    GameObject result = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "/PuzzleResultPopup.prefab");
                    PuzzlePopupBinding binding = screen.GetComponent<PuzzlePopupBinding>();
                    PopupCatalog.Entry extra = binding.Catalog.Entries.SingleOrDefault(entry => entry.Id == "owned.extra");
                    Check(extra != null && extra.Prefab == customPrefab && extra.AllowMultiple && extra.Restorable && extra.PauseGameplay && !extra.CloseOnCancel,
                        "세 연결/생성 경로 재실행 추가 등록 프리팹 정책 보존 " + repeat);
                    Check(binding != null && binding.Host != null && binding.Catalog != null && AssetDatabase.GetAssetPath(binding.Catalog) == folder + "/PopupCatalog.asset" && binding.Catalog.Entries.Length == 4 && screen.transform.Find("SafeArea/MissionDescription") == null && screen.GetComponentsInChildren<PuzzlePauseView>(true).Length == 0, "두 생성 도구 재실행 공통 Host Binding catalog 설명 분리 보존 " + repeat);
                    SerializedObject resultData = new SerializedObject(result.GetComponent<PuzzleResultView>());
                    Check(result.transform.Find("Panel/NextLevel") != null && resultData.FindProperty("nextLevel").objectReferenceValue != null && result.GetComponent<PopupView>().DefaultSelection != null, "두 생성 도구 재실행 Next와 기본 선택 보존 " + repeat);
                    Check(AssetDatabase.AssetPathToGUID(folder + "/PuzzleScreen.prefab") == screenGuid && AssetDatabase.AssetPathToGUID(folder + "/PuzzleResultPopup.prefab") == resultGuid, "재생성 기존 복사본 GUID 보존 " + repeat);
                }
                Check(originals.All(pair => File.ReadAllBytes(pair.Key).SequenceEqual(pair.Value)), "임시 생성 검사 원본 UI catalog 씬 바이트 보존");
            }
            catch (Exception error) { exit = 1; results.Add("FAIL " + error); }
            finally
            {
                if (owned)
                {
                    if (!Path.GetFullPath(folder).StartsWith(Path.GetFullPath("Assets/Prefabs/UI") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("시험 생성 경로 이탈");
                    AssetDatabase.DeleteAsset(folder);
                }
                Directory.CreateDirectory(Output); results.Add("UTC " + DateTime.UtcNow.ToString("O"));
                File.WriteAllLines(Output + "stage03-generators-results.txt", results); EditorApplication.Exit(exit);
            }
        }
    }
}
