using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using GameScreen;
using LevelAuthoring.Editing;
using LevelTool;
using LevelTool.Presentation;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelAuthoring.Editor
{
    public static class LevelToolBoardLifetimeVerification
    {
        private static readonly List<LevelToolBoardView> Views = new List<LevelToolBoardView>();
        private static readonly List<string> Errors = new List<string>();
        private static PuzzleArtwork[] pendingOwners;
        private static AuthoringToolWorkspace workspace;
        private static string populatedId, expected;
        private static int phase, exit, lastFrame;
        private static double deadline;
        private static bool previousEnabled, finished;
        private static EnterPlayModeOptions previousOptions;

        public static void Run()
        {
            Views.Clear(); Errors.Clear(); phase = exit = 0; finished = false;
            previousEnabled = EditorSettings.enterPlayModeOptionsEnabled; previousOptions = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            deadline = EditorApplication.timeSinceStartup + 120;
            EditorApplication.update += Tick; EditorApplication.playModeStateChanged += Changed;
            EditorApplication.EnterPlaymode();
        }

        private static void Tick()
        {
            if (finished || !EditorApplication.isPlaying) return;
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Board lifetime verification timed out");
                if (phase == 0)
                {
                    workspace = new AuthoringToolWorkspace();
                    workspace.Create(Path.GetFullPath("Logs/LevelToolRefactorStage03/board-lifetime-" + Guid.NewGuid().ToString("N")), LevelToolTestProject.Create());
                    AuthoringEditSession session = workspace.Session;
                    populatedId = session.SelectedLevelId;
                    var definitions = LevelToolDocuments.Definitions(session);
                    string normal = definitions.First(pair => (string)pair.Value["supply"]?["behavior"] == "FixedNormal").Key;
                    session.EditLevel("검사 이미지", data => LevelDocumentEditing.Place(data, definitions, normal, 4, 4, false));
                    DocumentEditing.DuplicateLevel(session, populatedId, 2, "빈 전환 대상");
                    string emptyId = session.SelectedLevelId;
                    session.EditLevel("검사 빈 보드", data => data["elements"] = new JArray());
                    session.SelectLevel(populatedId);
                    for (int i = 0; i < 3; i++)
                    {
                        LevelToolBoardView view = new LevelToolBoardView(new VisualElement(), Errors.Add, _ => { }, () => false);
                        Views.Add(view); Draw(view);
                    }
                    pendingOwners = Views.Select(Artwork).ToArray();
                    Check(pendingOwners.All(owner => Pending(owner) > 0), "all three board views have real pending image requests before transition");
                    Views[1].Dispose(); Views[2].Clear();
                    session.SelectLevel(emptyId); Draw(Views[0]);
                    expected = session.ExportState(); lastFrame = Time.frameCount; phase = 1;
                    return;
                }
                if (Time.frameCount <= lastFrame || pendingOwners.Any(owner => Pending(owner) != 0)) { EditorApplication.QueuePlayerLoopUpdate(); return; }
                if (phase == 1)
                {
                    Check(Views[0].Root.Query<Image>().ToList().Count == 0, "old level images cannot appear on the new empty level");
                    Check(Views[1].Root.childCount == 0, "disposed board stays empty after pending image requests finish");
                    Check(Views[2].Root.childCount == 0, "cleared board stays empty after pending image requests finish");
                    Check(pendingOwners[0].AtlasCount == 0 && pendingOwners[1].AtlasCount == 0, "replaced and disposed artwork release their loaded atlases");
                    Check(workspace.Session.ExportState() == expected, "async completion preserves document selection and history");
                    workspace.Session.SelectLevel(populatedId); Draw(Views[0]);
                    expected = workspace.Session.ExportState(); phase = 2; lastFrame = Time.frameCount;
                    return;
                }
                if (Pending(Artwork(Views[0])) != 0) { EditorApplication.QueuePlayerLoopUpdate(); return; }
                Check(Views[0].Root.Query<Image>().ToList().Count == 1, "revisited level receives only its current image");
                Check(workspace.Session.ExportState() == expected && Errors.Count == 0, "current render preserves editing state without cancellation errors");
                Finish();
            }
            catch (Exception error) { Debug.LogException(error); exit = 1; Finish(); }
        }

        private static void Draw(LevelToolBoardView view) => view.Refresh(workspace.Session, workspace.Folder,
            LevelToolDocuments.Definitions(workspace.Session), "Block", 52, new LevelToolBoardOverlayState());
        private static PuzzleArtwork Artwork(LevelToolBoardView view) => (PuzzleArtwork)typeof(LevelToolBoardView)
            .GetField("artwork", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
        private static int Pending(PuzzleArtwork artwork) => (int)typeof(PuzzleArtwork)
            .GetField("pending", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(artwork);
        private static void Finish()
        {
            finished = true;
            foreach (LevelToolBoardView view in Views) view.Dispose();
            EditorApplication.ExitPlaymode();
        }
        private static void Changed(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= Changed;
            EditorSettings.enterPlayModeOptionsEnabled = previousEnabled; EditorSettings.enterPlayModeOptions = previousOptions;
            EditorApplication.Exit(exit);
        }
        private static void Check(bool value, string message)
        {
            if (!value) throw new Exception("FAIL " + message);
            Debug.Log("PASS " + message);
        }
    }
}
