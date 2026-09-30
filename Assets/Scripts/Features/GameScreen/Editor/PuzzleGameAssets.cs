using System.IO;
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameScreen.Editor
{
    public static class PuzzleGameAssets
    {
        public const string Folder = "Assets/Prefabs/Game/Puzzle";
        public const string ScenePath = "Assets/Scenes/PuzzleGame.unity";

        [MenuItem("Tools/Match/게임 플레이 씬 연결")]
        public static void GenerateGameplay()
        {
            string path = Folder + "/PuzzleGameSession.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                GameObject temporary = new GameObject("PuzzleGameSession");
                PuzzleGameSession session = temporary.AddComponent<PuzzleGameSession>();
                PuzzleBoardInput input = temporary.AddComponent<PuzzleBoardInput>();
                input.Configure(session, null, null);
                temporary.AddComponent<PuzzlePlayDebugView>().Configure(session, input);
                prefab = Save(temporary, "PuzzleGameSession");
            }
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (!opened && scene.isDirty) throw new InvalidOperationException("게임 씬을 저장한 뒤 연결해 주세요.");
            if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                GameObject[] roots = scene.GetRootGameObjects();
                PuzzleWorldBoard board = roots.SelectMany(item => item.GetComponentsInChildren<PuzzleWorldBoard>(true)).Single();
                Camera camera = roots.SelectMany(item => item.GetComponentsInChildren<Camera>(true)).Single();
                PuzzleGameSession session = roots.SelectMany(item => item.GetComponentsInChildren<PuzzleGameSession>(true)).SingleOrDefault();
                if (session == null) session = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, scene)).GetComponent<PuzzleGameSession>();
                session.Configure(board, camera);
                PuzzleBoardInput input = session.GetComponent<PuzzleBoardInput>(); input.Configure(session, board, camera);
                PuzzlePlayDebugView view = session.GetComponent<PuzzlePlayDebugView>(); view.Configure(session, input);
                foreach (Component component in new Component[] { session, input, view })
                { EditorUtility.SetDirty(component); PrefabUtility.RecordPrefabInstancePropertyModifications(component); }
                foreach (PuzzleBoardPreview preview in roots.SelectMany(item => item.GetComponentsInChildren<PuzzleBoardPreview>(true)))
                { preview.enabled = false; EditorUtility.SetDirty(preview); }
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/Match/월드 보드 프리팹 생성")]
        public static void Generate()
        {
            Directory.CreateDirectory(Folder);
            Directory.CreateDirectory("Assets/Scenes");
            AssetDatabase.Refresh();
            GameObject cell = new GameObject("PuzzleCell");
            PuzzleCellView view = cell.AddComponent<PuzzleCellView>();
            view.Configure(Renderer("Floor", cell.transform, 0), Renderer("Dust", cell.transform, 2),
                Renderer("Content", cell.transform, 10), Renderer("Cover", cell.transform, 20));
            GameObject cellAsset = Save(cell, "PuzzleCell");
            GameObject obstacleAsset = Save(Renderer("PuzzleObstacle", null, 10).gameObject, "PuzzleObstacle");
            GameObject decorationAsset = Save(Renderer("PuzzleDecoration", null, 30).gameObject, "PuzzleDecoration");
            GameObject board = new GameObject("PuzzleWorldBoard");
            board.AddComponent<PuzzleWorldBoard>().Configure(cellAsset.GetComponent<PuzzleCellView>(),
                obstacleAsset.GetComponent<SpriteRenderer>(), decorationAsset.GetComponent<SpriteRenderer>());
            GameObject boardAsset = Save(board, "PuzzleWorldBoard");

            // 이미 있는 씬은 사용자의 배치와 설정을 보존한다.
            if (!File.Exists(ScenePath))
            {
                Scene previous = SceneManager.GetActiveScene();
                // 배치 프로세스의 이름 없는 초기 씬에는 보존할 사용자 편집이 없다.
                NewSceneMode mode = Application.isBatchMode && string.IsNullOrEmpty(previous.path)
                    ? NewSceneMode.Single : NewSceneMode.Additive;
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, mode);
                SceneManager.SetActiveScene(scene);
                GameObject cameraObject = new GameObject("Board Camera", typeof(Camera));
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.orthographic = true; camera.orthographicSize = 5.7f;
                camera.transform.position = new Vector3(0, 0, -10);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.32f, 0.41f, 0.45f);
                camera.nearClipPlane = 0.1f; camera.farClipPlane = 50;
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(boardAsset, scene);
                GameObject preview = new GameObject("Puzzle Board Preview");
                preview.AddComponent<PuzzleBoardPreview>().Configure(instance.GetComponent<PuzzleWorldBoard>(), camera);
                EditorSceneManager.SaveScene(scene, ScenePath);
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("월드 보드 프리팹과 씬 준비 완료: " + ScenePath);
        }

        private static SpriteRenderer Renderer(string name, Transform parent, int order)
        {
            GameObject item = new GameObject(name, typeof(SpriteRenderer));
            item.transform.SetParent(parent, false);
            SpriteRenderer renderer = item.GetComponent<SpriteRenderer>();
            renderer.sortingOrder = order;
            // URP 2D 조명 유무와 관계없이 승인된 원화 색을 표시한다.
            renderer.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            return renderer;
        }
        private static GameObject Save(GameObject temporary, string name)
        {
            try { return PrefabUtility.SaveAsPrefabAsset(temporary, Folder + "/" + name + ".prefab"); }
            finally { UnityEngine.Object.DestroyImmediate(temporary); }
        }
    }
}
