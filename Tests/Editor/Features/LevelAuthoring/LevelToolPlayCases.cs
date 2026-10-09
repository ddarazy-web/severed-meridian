using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameScreen;
using LevelAuthoring.Runtime;
using LevelAuthoring.Storage;
using LevelTool;
using Levels;
using Newtonsoft.Json.Linq;
using Tutorial;
using Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelAuthoring.Editor
{
    internal static class LevelToolPlayCases
    {
        internal static async UniTask Run(LevelToolScreen screen)
        {
            var session = screen.Workspace.Session;
            string folder = screen.Workspace.Folder;
            string diskHash = new ContentSnapshotStore(folder).Read().Hash;
            var originals = Directory.GetFiles("Assets/Data", "*.asset", SearchOption.AllDirectories).ToDictionary(path => path, File.ReadAllText);
            int baseline = Resources.FindObjectsOfTypeAll<LevelDefinition>().Length;
            foreach (var document in session.Documents.Where(doc => doc.Kind == "level" && (int)doc.Data["levelNumber"] <= 4).OrderBy(doc => (int)doc.Data["levelNumber"]))
            {
                session.SelectLevel(document.Id); session.SelectCells(new[] { 40 }); Refresh(screen);
                await UniTask.Yield(); await UniTask.Yield();
                VisualElement root = screen.GetComponent<UIDocument>().rootVisualElement;
                ScrollView properties = root.Q<ScrollView>("properties"); properties.scrollOffset = new Vector2(0, 130);
                await UniTask.Yield(); Vector2 scroll = properties.scrollOffset;
                string before = session.ExportState();
                string key = "MoonRabbit.Tutorial.Completed." + document.Data["levelNumber"];
                string identity = "MoonRabbit.Tutorial.Identity." + document.Data["tutorial"]["completionId"];
                bool had = PlayerPrefs.HasKey(key), hadIdentity = PlayerPrefs.HasKey(identity);
                int value = PlayerPrefs.GetInt(key), identityValue = PlayerPrefs.GetInt(identity);
                await screen.BeginPlay(); Check(screen.TestSession != null, "representative start " + document.Data["levelNumber"]);
                PuzzleGameSession game = screen.TestSession;
                await Stable(game);
                Check(game.State.MovesRemaining == (int)document.Data["moveCount"], "current edited moves passed");
                Check(game.PlayContext.IsTest && !game.PlayContext.Tutorial.UsesPlayerProgress, "isolated test context");
                var request = JsonPuzzlePlayAdapter.CreateRequest(session.CreateSnapshot(), document.Id, 12345);
                LevelDefinition definition = request.CreateDefinition();
                try
                {
                    foreach (var step in TutorialFlowResolver.Resolve(definition).steps)
                    {
                        if (game.TutorialState == null || game.TutorialState.State == TutorialProgressState.Completed) break;
                        bool accepted = step.kind == TutorialStepKind.Description ? game.TryAdvanceTutorial() :
                            step.kind == TutorialStepKind.Item ? game.TryUseItem(step.item, step.first) : game.TrySwap(step.first, step.second);
                        Check(accepted, "tutorial action accepted " + step.kind); await Stable(game);
                    }
                }
                finally { UnityEngine.Object.Destroy(definition); }
                screen.ReturnToEditor(); await UniTask.Yield(); await UniTask.Yield();
                Check(session.ExportState() == before && screen.Workspace.Folder == folder, "representative return preserves draft/history/folder");
                Check(Vector2.Distance(scroll, properties.scrollOffset) < 1, "return preserves property scroll");
                Check(PlayerPrefs.HasKey(key) == had && PlayerPrefs.GetInt(key) == value && PlayerPrefs.HasKey(identity) == hadIdentity && PlayerPrefs.GetInt(identity) == identityValue,
                    "official tutorial records unchanged");
                Check(UnityEngine.Object.FindObjectsByType<PuzzleGameSession>(FindObjectsSortMode.None).Length == 0, "test game released");
            }
            string levelId = session.SelectedLevelId;
            string projectId = session.Documents.Single(doc => doc.Kind == "project").Id;
            session.Apply("누락 이미지 검사", docs =>
            {
                foreach (JToken resource in docs[projectId].Data["resources"])
                    if (((string)resource["path"]).StartsWith("Blocks/", StringComparison.Ordinal)) resource["path"] = (string)resource["path"] + "-missing-test";
            });
            Refresh(screen); await UniTask.Delay(500);
            Check(screen.GetComponent<UIDocument>().rootVisualElement.Q("board").Query<Label>().ToList().Any(label => label.text == "!" && label.tooltip.Contains("이미지")),
                "missing image shows fallback and cause");
            string missing = session.ExportState(); await screen.BeginPlay();
            Check(screen.TestSession == null && session.ExportState() == missing, "missing resources return to editable draft");
            session.Undo(); Refresh(screen);
            session.Apply("잘못된 시험 입력", docs => docs[levelId].Data["moveCount"] = -1);
            string invalid = session.ExportState(); await screen.BeginPlay();
            Check(screen.TestSession == null && session.ExportState() == invalid, "invalid input keeps editable draft"); session.Undo();
            UniTask old = screen.BeginPlay(); screen.ReturnToEditor(); UniTask fresh = screen.BeginPlay();
            await old; await fresh;
            Check(screen.TestSession != null && screen.TestSession.IsReady, "immediate cancel and retry survives old completion");
            screen.ReturnToEditor(); await UniTask.Yield(); await UniTask.Yield();
            await SharedDraftGame(screen);
            // 공유 편집의 마지막 Refresh에서 만든 조회 사본도 Unity의 프레임 끝 파괴를 기다린다.
            await UniTask.Yield(); await UniTask.Yield();
            Check(new ContentSnapshotStore(folder).Read().Hash == diskHash, "JSON source not written by play");
            foreach (var original in originals) Check(File.ReadAllText(original.Key) == original.Value, "SO source unchanged " + Path.GetFileName(original.Key));
            Check(Resources.FindObjectsOfTypeAll<LevelDefinition>().Length == baseline,
                "temporary level definitions released (expected " + baseline + ", actual " + Resources.FindObjectsOfTypeAll<LevelDefinition>().Length + ")");
        }
        private static async UniTask SharedDraftGame(LevelToolScreen screen)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var parent = screen.Workspace.Session;
            string previousPage = (string)typeof(LevelToolScreen).GetField("inspectorPage", flags).GetValue(screen);
            TutorialDraftEditing.AddSampleBoard(parent, "two");
            string flowId = TutorialDraftEditing.CreateFlow(parent, "두 행동 공유 게임 검사");
            var draft = new SharedTutorialDraft(parent);
            TutorialDraftEditing.EditFlow(draft.Session, flowId, "draft instructions", flow => flow.steps[0].instructions = "시험 사본의 두 번 교환");
            TutorialDraftEditing.EditFlow(draft.Session, flowId, "draft redo", flow => flow.steps[0].instructions = "다시 실행 안내");
            draft.Session.Undo();
            typeof(LevelToolScreen).GetField("sharedTutorialDraft", flags).SetValue(screen, draft);
            typeof(LevelToolScreen).GetField("inspectorPage", flags).SetValue(screen, "튜토리얼");
            Refresh(screen);
            string parentBefore = parent.ExportState(), draftBefore = draft.ExportState();
            string key = "MoonRabbit.Tutorial.Completed." + parent.Get(parent.SelectedLevelId).Data["levelNumber"];
            string identity = "MoonRabbit.Tutorial.Identity." + parent.Get(parent.SelectedLevelId).Data["tutorial"]["completionId"];
            bool had = PlayerPrefs.HasKey(key), hadIdentity = PlayerPrefs.HasKey(identity);
            int value = PlayerPrefs.GetInt(key), identityValue = PlayerPrefs.GetInt(identity);
            await screen.BeginPlay(); var game = screen.TestSession;
            Check(game != null, "shared draft real game starts"); await Stable(game);
            Check(game.TutorialState?.Instructions == "시험 사본의 두 번 교환", "real game uses unapplied shared draft instructions");
            int actions = 0;
            while (game.TutorialState.State != TutorialProgressState.Completed && actions < 4)
            {
                var guidance = game.TutorialState;
                Check(guidance.First.HasValue && guidance.Second.HasValue && game.TrySwap(guidance.First.Value, guidance.Second.Value), "shared two-action guidance accepted");
                actions++; await Stable(game);
            }
            Check(actions == 2 && game.TutorialState.State == TutorialProgressState.Completed, "shared draft fixed supply supports two real actions and damage condition");
            screen.ReturnToEditor(); await UniTask.Yield(); await UniTask.Yield();
            Check(parent.ExportState() == parentBefore && draft.ExportState() == draftBefore, "shared real game return preserves parent and isolated draft undo/redo");
            Check(screen.GetComponent<UIDocument>().rootVisualElement.Q<TextField>("tutorial-instructions")?.value == "시험 사본의 두 번 교환", "shared editor resumes unchanged after game");
            Check(PlayerPrefs.HasKey(key) == had && PlayerPrefs.GetInt(key) == value && PlayerPrefs.HasKey(identity) == hadIdentity && PlayerPrefs.GetInt(identity) == identityValue,
                "shared draft game leaves official tutorial progress unchanged");
            draft.Session.Redo(); Check((string)draft.Session.Get(flowId).Data["steps"][0]["instructions"] == "다시 실행 안내", "shared draft redo survives actual game");
            draft.Session.Undo();
            Check(parent.ExportState() == parentBefore, "draft undo/redo never applies shared source implicitly");
            typeof(LevelToolScreen).GetField("sharedTutorialDraft", flags).SetValue(screen, null);
            typeof(LevelToolScreen).GetField("inspectorPage", flags).SetValue(screen, previousPage); Refresh(screen);
        }
        private static async UniTask Stable(PuzzleGameSession game)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(35));
            await UniTask.Yield();
            await UniTask.WaitUntil(() => game.HasFailed || game.IsReady && !game.IsPresenting && !game.HasProgressFeedback &&
                (game.Phase == BoardActionPhase.Ready || game.Phase == BoardActionPhase.Stopped), cancellationToken: timeout.Token);
            Check(!game.HasFailed, game.Message);
        }
        private static void Refresh(LevelToolScreen screen) => typeof(LevelToolScreen).GetMethod("Refresh", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(screen, null);
        private static void Check(bool value, string message) { if (!value) throw new Exception("FAIL " + message); Debug.Log("PASS " + message); }
    }
}
