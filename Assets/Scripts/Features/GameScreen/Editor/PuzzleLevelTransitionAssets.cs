using System;
using UnityEditor;
using UnityEngine;

namespace GameScreen.Editor
{
    public static class PuzzleLevelTransitionAssets
    {
        public static void Apply()
        {
            const string path = "Assets/Prefabs/UI/Puzzle/PuzzleResultPopup.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                PuzzleResultView view = root.GetComponent<PuzzleResultView>();
                Transform panel = root.transform.Find("Panel");
                if (view == null || panel == null) throw new InvalidOperationException("기존 결과 팝업 구조를 찾을 수 없습니다.");
                UnityEngine.UI.Button retry = panel.Find("Primary").GetComponent<UnityEngine.UI.Button>();
                Transform existing = panel.Find("NextLevel");
                UnityEngine.UI.Button next = existing != null ? existing.GetComponent<UnityEngine.UI.Button>() :
                    UnityEngine.Object.Instantiate(retry, panel);
                next.name = "NextLevel"; next.onClick.RemoveAllListeners();
                next.GetComponentInChildren<UnityEngine.UI.Text>().text = "다음 레벨";
                (retry.transform as RectTransform).anchoredPosition = new Vector2(30, -230);
                (next.transform as RectTransform).anchoredPosition = new Vector2(30, -290);
                panel.Find("Detail").GetComponent<RectTransform>().sizeDelta = new Vector2(340, 100);
                view.ConfigureNextButton(next);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log("Stage13 기존 결과 팝업 다음 레벨 버튼 연결 완료");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            EditorApplication.Exit(0);
        }
    }
}
