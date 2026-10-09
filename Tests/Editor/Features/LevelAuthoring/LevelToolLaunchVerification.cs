using System;
using System.IO;
using System.Reflection;
using LevelAuthoring.Editing;
using LevelTool;
using LevelTool.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace LevelAuthoring.Editor
{
    public static class LevelToolLaunchVerification
    {
        private static bool previousEnabled;
        private static EnterPlayModeOptions previousOptions;
        private static SceneAsset previousStart;
        private static double deadline;
        private static bool defaultEntry;
        private static string incoming, expected;
        private static bool waitingForHandoff;
        public static void RunIncoming()
        {
            var workspace = new AuthoringToolWorkspace(); workspace.Open(File.ReadAllText("Logs/GameAuthoringStage02/latest-fixtures.txt"), "Cancel");
            string id = workspace.Session.SelectedLevelId;
            workspace.Session.Apply("기존 창 인계", docs => docs[id].Data["displayName"] = "씬 진입 인계 초안");
            incoming = workspace.ExportState(); expected = workspace.Session.ExportState(); Run();
        }
        public static void RunDefault()
        {
            defaultEntry = true;
            Run();
        }
        public static void Run()
        {
            previousEnabled = EditorSettings.enterPlayModeOptionsEnabled; previousOptions = EditorSettings.enterPlayModeOptions;
            previousStart = EditorSceneManager.playModeStartScene;
            EditorSettings.enterPlayModeOptionsEnabled = true; EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("UnsavedLevelToolLaunchMarker").transform.position = new Vector3(12, 34, 56);
            EditorSceneManager.MarkSceneDirty(scene);
            deadline = EditorApplication.timeSinceStartup + 90;
            EditorApplication.playModeStateChanged += Changed; EditorApplication.update += Watch;
            try
            {
                if (incoming != null) LevelToolLauncher.LaunchWorkspace(incoming);
                else if (defaultEntry)
                {
                    if (!EditorApplication.ExecuteMenuItem("Match/레벨 에디터") || !EditorApplication.isPlayingOrWillChangePlaymode)
                        throw new Exception("기본 레벨 편집 메뉴가 공통 씬으로 진입하지 않습니다.");
                }
                else LevelToolLauncher.Launch();
            }
            catch (Exception error) { Debug.LogException(error); Finish(1); }
        }
        private static void Changed(PlayModeStateChange state)
        {
            try
            {
                if (state == PlayModeStateChange.EnteredPlayMode)
                {
                    if (UnityEngine.Object.FindFirstObjectByType<LevelToolScreen>() == null) throw new Exception("레벨툴 시작 씬 진입 실패");
                    Debug.Log("PASS existing menu starts runtime tool scene");
                    if (incoming != null) { waitingForHandoff = true; return; }
                    EditorApplication.ExitPlaymode();
                }
                else if (state == PlayModeStateChange.EnteredEditMode)
                {
                    GameObject marker = GameObject.Find("UnsavedLevelToolLaunchMarker");
                    if (marker == null || marker.transform.position != new Vector3(12, 34, 56) || !SceneManager.GetActiveScene().isDirty)
                        throw new Exception("메뉴 왕복에서 미저장 씬 내용 소실");
                    if (EditorSceneManager.playModeStartScene != previousStart) throw new Exception("이전 시작 씬 설정 미복원");
                    Debug.Log("PASS unsaved editor scene and launch setting preserved"); Finish(0);
                }
            }
            catch (Exception error) { Debug.LogException(error); Finish(1); }
        }
        private static void Watch()
        {
            try
            {
                if (waitingForHandoff)
                {
                    var screen = UnityEngine.Object.FindFirstObjectByType<LevelToolScreen>();
                    if (screen != null && screen.GetComponent<UIDocument>().rootVisualElement.Q<Button>("open-incoming-workspace") != null)
                    {
                        typeof(LevelToolScreen).GetMethod("AcceptIncomingWorkspace", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(screen, new object[] { "Discard" });
                        if (screen.Workspace.Session.ExportState() != expected) throw new Exception("씬 진입에서 인계 초안 소실");
                        Debug.Log("PASS launcher delivers full unsaved workspace through scene transition");
                        waitingForHandoff = false; EditorApplication.ExitPlaymode();
                    }
                }
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("메뉴 진입 검사 시간 초과");
            }
            catch (Exception error) { Debug.LogException(error); Finish(1); }
        }
        private static void Finish(int code)
        {
            EditorApplication.playModeStateChanged -= Changed; EditorApplication.update -= Watch;
            EditorSettings.enterPlayModeOptionsEnabled = previousEnabled; EditorSettings.enterPlayModeOptions = previousOptions;
            EditorSceneManager.playModeStartScene = previousStart;
            EditorApplication.Exit(code);
        }
    }
}
