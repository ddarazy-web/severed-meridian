using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Board;
using LevelAuthoring.Editing;
using LevelAuthoring.Runtime;
using Levels;
using LevelTool;
using LevelTool.Presentation;
using Tutorial;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelAuthoring.Editor
{
    public static class LevelToolTutorialConditionsPanelVerification
    {
        private sealed class Host : EditorWindow { }
        public static void Run()
        {
            int exit = 0;
            IDisposable panel = null;
            Host host = null;
            try
            {
                Type tutorialPanelType = typeof(LevelToolScreen).Assembly.GetType("LevelTool.Presentation.LevelToolTutorialPanel");
                Check(tutorialPanelType != null && typeof(IDisposable).IsAssignableFrom(tutorialPanelType), "tutorial composition has its own presentation lifetime");
                Type type = typeof(LevelToolScreen).Assembly.GetType("LevelTool.Presentation.LevelToolTutorialConditionsPanel");
                Check(type != null, "tutorial conditions have an independent presentation owner");
                AuthoringToolWorkspace workspace = new AuthoringToolWorkspace();
                workspace.Create(Path.GetFullPath("Logs/LevelToolRefactorStage03/conditions-" + Guid.NewGuid().ToString("N")), LevelToolDocuments.NewProject());
                AuthoringEditSession session = workspace.Session;
                TutorialDraftEditing.EditLevel(session, "준비", data => data.Tutorial.steps.Add(new TutorialStepDefinition()));
                TutorialConditionKind kind = TutorialConditionKind.Match;
                int edits = 0;
                Action<string, Action<LevelDefinition>> edit = (label, action) => { edits++; TutorialDraftEditing.EditLevel(session, label, action); };
                panel = (IDisposable)Activator.CreateInstance(type, (Func<AuthoringEditSession>)(() => session), edit,
                    (Func<List<BoardCoordinate>>)(() => new List<BoardCoordinate>()), (Action<string, string>)((_, __) => { }),
                    (Func<TutorialConditionKind>)(() => kind), (Action<TutorialConditionKind>)(value => kind = value));
                VisualElement root = (VisualElement)type.GetProperty("Root").GetValue(panel);
                host = ScriptableObject.CreateInstance<Host>(); host.Show(); host.rootVisualElement.Add(root);
                void Draw()
                {
                    using var graph = new AuthoringObjectGraph(session.Documents);
                    type.GetMethod("Refresh").Invoke(panel, new object[] { (LevelDefinition)graph.Resolve(session.SelectedLevelId), 0 });
                }
                Draw();
                Button add = root.Q<Button>("tutorial-add-condition");
                typeof(Clickable).GetMethod("Invoke", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(add.clickable, new object[] { null });
                Check(edits == 1, "condition creation uses one tutorial command");
                Draw();
                IntegerField count = root.Q<IntegerField>("tutorial-condition-count-0");
                Check(count != null, "condition keeps its existing named count field");
                string original = session.Get(session.SelectedLevelId).Data.ToString();
                count.value = 7;
                Check(edits == 2, "count edit uses existing tutorial transaction");
                session.Undo(); Check(session.Get(session.SelectedLevelId).Data.ToString() == original, "condition Undo restores exact document");
                Draw(); host.rootVisualElement.Add(count); count.value = 9;
                Check(edits == 2, "old condition control cannot change a refreshed step");
                IntegerField current = root.Q<IntegerField>("tutorial-condition-count-0");
                panel.Dispose(); host.rootVisualElement.Add(current); current.value = 11;
                Check(edits == 2 && root.childCount == 0, "disposed condition panel cannot edit tutorial data");
                LevelToolTutorialState state = new LevelToolTutorialState { Step = 99 };
                using LevelToolTutorialPanel composition = new LevelToolTutorialPanel(new LevelToolTutorialActions
                {
                    Session = () => session, Draft = () => null, Edit = action => action(), Refresh = () => { },
                    Preview = () => false, SetPreview = _ => { }, Layer = () => "Block", Pick = (_, __, ___, ____) => { },
                    BeginSharedDraft = () => { }, ApplySharedDraft = () => { }, DiscardSharedDraft = () => { }
                }, state);
                host.rootVisualElement.Add(composition.Root); composition.RefreshView();
                Check(state.Step == 0, "tutorial composition normalizes restored step selection");
                Button addStep = composition.Root.Q<Button>("tutorial-add-step");
                typeof(Clickable).GetMethod("Invoke", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(addStep.clickable, new object[] { null });
                Check(session.Get(session.SelectedLevelId).Data["tutorial"]["steps"].Count() == 2, "step add remains usable after selection normalization");
            }
            catch (Exception error) { Debug.LogException(error); exit = 1; }
            finally { panel?.Dispose(); if (host != null) host.Close(); }
            EditorApplication.Exit(exit);
        }
        private static void Check(bool value, string message)
        {
            if (!value) throw new Exception("FAIL " + message);
            Debug.Log("PASS " + message);
        }
    }
}
