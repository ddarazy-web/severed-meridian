using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace LevelTool.Editor
{
    public static class LevelToolAssets
    {
        public const string ScenePath = "Assets/Scenes/LevelTool.unity";
        private const string UI = "Assets/UI/LevelTool/";
        public static void GenerateBatch()
        {
            try { Generate(); EditorApplication.Exit(0); }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }

        [MenuItem("MATCH/제작 도구/레벨툴 기본 자산 생성")]
        public static void Generate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("편집 모드에서 생성하세요.");
            Directory.CreateDirectory("Assets/Prefabs/UI/LevelTool"); AssetDatabase.Refresh();
            PanelSettings settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(UI + "LevelToolPanel.asset");
            if (settings == null)
            {
                ThemeStyleSheet theme = ScriptableObject.CreateInstance<ThemeStyleSheet>();
                AssetDatabase.CreateAsset(theme, UI + "LevelToolTheme.tss");
                settings = ScriptableObject.CreateInstance<PanelSettings>();
                settings.themeStyleSheet = theme; settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                settings.referenceResolution = new Vector2Int(1280, 800); settings.match = 0.5f;
                settings.sortingOrder = 100;
                AssetDatabase.CreateAsset(settings, UI + "LevelToolPanel.asset");
            }
            const string prefabPath = "Assets/Prefabs/UI/LevelTool/LevelToolScreen.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            {
                GameObject root = new GameObject("LevelToolScreen"); root.SetActive(false);
                try
                {
                    root.AddComponent<UIDocument>().panelSettings = settings;
                    LevelToolScreen screen = root.AddComponent<LevelToolScreen>();
                    screen.Configure(AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/NotoSansKR-Mockup.ttf"),
                        AssetDatabase.LoadAssetAtPath<StyleSheet>(UI + "LevelTool.uss"),
                        AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Game/Puzzle/PuzzleWorldBoard.prefab"),
                        AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Game/Puzzle/PuzzleGameSession.prefab"),
                        AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Puzzle/PuzzleScreen.prefab"));
                    root.SetActive(true); PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                Scene previous = SceneManager.GetActiveScene();
                // 배치 검사는 저장 전 빈 씬으로 시작한다. 사용자의 열린 씬은 이 분기로 교체하지 않는다.
                bool emptyBatch = Application.isBatchMode && string.IsNullOrEmpty(previous.path) && !previous.isDirty;
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, emptyBatch ? NewSceneMode.Single : NewSceneMode.Additive);
                try
                {
                    PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath), scene);
                    GameObject events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                    SceneManager.MoveGameObjectToScene(events, scene);
                    if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("레벨툴 씬 저장 실패");
                }
                finally
                {
                    EditorSceneManager.CloseScene(scene, true);
                    if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                }
            }
            UnityEngine.Object profile = AssetDatabase.LoadMainAssetAtPath("Assets/Settings/BuildProfiles/Windows Level Editor.asset");
            if (profile != null)
            {
                using SerializedObject data = new SerializedObject(profile);
                SerializedProperty scenes = data.FindProperty("m_Scenes");
                scenes.arraySize = 1;
                scenes.GetArrayElementAtIndex(0).FindPropertyRelative("m_enabled").boolValue = true;
                scenes.GetArrayElementAtIndex(0).FindPropertyRelative("m_path").stringValue = ScenePath;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            AssetDatabase.SaveAssets();
            Debug.Log("레벨툴 씬과 UI 프리팹 생성 완료. Player 빌드는 실행하지 않았습니다.");
        }
    }

    [InitializeOnLoad]
    public static class LevelToolLauncher
    {
        private const string Key = "LevelTool.Launch.";
        static LevelToolLauncher()
        {
            EditorApplication.playModeStateChanged += OnState;
            EditorApplication.delayCall += ConnectExit;
        }
        private static void ConnectExit()
        {
            if (!EditorApplication.isPlaying) return;
            LevelToolScreen screen = UnityEngine.Object.FindFirstObjectByType<LevelToolScreen>();
            if (screen != null)
            {
                screen.ExitRequested -= EditorApplication.ExitPlaymode; screen.ExitRequested += EditorApplication.ExitPlaymode;
                string incoming = SessionState.GetString(Key + "workspace", "");
                if (!string.IsNullOrEmpty(incoming))
                {
                    if (SessionState.GetBool(Key + "default", false)) screen.OfferDefaultWorkspace(incoming);
                    else screen.OfferWorkspace(incoming);
                    SessionState.EraseString(Key + "workspace"); SessionState.EraseBool(Key + "default");
                }
            }
        }

        public static void LaunchWorkspace(string state)
        {
            var candidate = new LevelAuthoring.Editing.AuthoringToolWorkspace(); candidate.RestoreState(state);
            SessionState.EraseBool(Key + "default");
            SessionState.SetString(Key + "workspace", state);
            try { Launch(); }
            catch { SessionState.EraseString(Key + "workspace"); throw; }
        }

        [MenuItem("MATCH/레벨툴 씬 실행")]
        public static void Launch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("실행·컴파일 완료 후 다시 시도하세요.");
            SceneAsset scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(LevelToolAssets.ScenePath);
            if (scene == null) throw new InvalidOperationException("레벨툴 씬을 먼저 생성하세요.");
            if (string.IsNullOrEmpty(SessionState.GetString(Key + "workspace", "")))
            {
                SessionState.SetString(Key + "workspace", CreateDefaultWorkspace());
                SessionState.SetBool(Key + "default", true);
            }
            SessionState.SetString(Key + "previous", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            SessionState.SetBool(Key + "active", true);
            EditorSceneManager.playModeStartScene = scene;
            try { EditorApplication.EnterPlaymode(); }
            catch { Restore(); SessionState.EraseString(Key + "workspace"); SessionState.EraseBool(Key + "default"); throw; }
        }

        internal static string CreateDefaultWorkspace()
        {
            var workspace = new LevelAuthoring.Editing.AuthoringToolWorkspace();
            workspace.Open(LevelAuthoring.Editor.AuthoringSourceSelection.ReadFolder(LevelAuthoring.Editor.AuthoringSourceSelection.DefaultConfiguration), "Cancel");
            return workspace.ExportState();
        }
        private static void OnState(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode) Restore();
            else if (state == PlayModeStateChange.EnteredPlayMode) ConnectExit();
        }
        private static void Restore()
        {
            if (!SessionState.GetBool(Key + "active", false)) return;
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(Key + "previous", ""));
            SessionState.EraseBool(Key + "active"); SessionState.EraseString(Key + "previous");
        }
    }
}
