using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Board;
using Cysharp.Threading.Tasks;
using Levels;
using Levels.Editor;
using Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GameScreen.Editor
{
    public static partial class PuzzlePresentationAcceptanceVerification
    {
        public static void RunMotion()
        {
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("미저장 씬 보존");
            Directory.CreateDirectory(Output + "motion"); PuzzleUIRenderVerification.RememberSize(); SessionState.SetBool("Stage12.Motion", true);
            EditorSceneManager.OpenScene(PuzzleGameAssets.ScenePath); EditorApplication.EnterPlaymode();
        }
        private static async UniTask MotionAsync()
        {
            results.Clear(); int exit = 0; float previous = Time.timeScale;
            List<string> manifest = new List<string> { "file,case,seconds,frame,width,height,phase,stages,inputReady,resultReady" };
            List<string> summaries = new List<string> { "case,seconds,frames,stages,stateEqual,outcome" };
            List<string> trajectories = new List<string> { "case,seconds,frame,id,label,x,y,angle,sprite" };
            try
            {
                Time.timeScale = 0; Application.runInBackground = true;
                PuzzleUIRenderVerification.SetSize(450, 800);
                for (int frame = 0; frame < 12; frame++) await UniTask.NextFrame();
                PuzzleGameSession session = await LoadOriginal(PuzzleEditorLevelSource.Asset);
                for (int scenario = 0; scenario < 18; scenario++)
                {
                    Time.timeScale = 0;
                    LevelDefinition level;
                    if (scenario >= 14 && scenario <= 16)
                        level = await (UniTask<LevelDefinition>)typeof(PuzzleStabilityVerification).GetMethod("PrepareFixture", BindingFlags.NonPublic | BindingFlags.Static)
                            .Invoke(null, new object[] { session, scenario == 14 ? 0 : scenario == 15 ? 1 : 3 });
                    else
                    {
                        level = scenario >= 4 ? (LevelDefinition)typeof(CombinationVerification).GetMethod("Make", BindingFlags.NonPublic | BindingFlags.Static)
                            .Invoke(null, new object[] { scenario == 17 ? 1 : scenario - 4, RocketDirection.Horizontal, null }) :
                            (LevelDefinition)typeof(PowerEffectVerification).GetMethod("Make", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
                        if (scenario < 4)
                            typeof(PowerEffectVerification).GetMethod("Place", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null,
                                new object[] { level, new BoardCoordinate(4, 4), new[] { InitialBlockKind.Rocket, InitialBlockKind.Bomb, InitialBlockKind.Drone, InitialBlockKind.Magnet }[scenario], RocketDirection.Horizontal, RabbitColor.Type1 });
                        JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":0,\"color\":0,\"count\":1000}]}", level);
                        if (scenario == 17)
                        {
                            BoardCoordinate anchor = new BoardCoordinate(3, 6);
                            LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Block, Erase = true },
                                LevelPlacementRules.Footprint(anchor, LevelPlacementRules.Size(ObstacleKind.Appliance)));
                            Check(LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)ObstacleKind.Appliance,
                                Durability = 9, Color = RabbitColor.Type1 }, new[] { anchor }).Changed == 1, "실제2x2 내구도9 영상 fixture");
                        }
                        Check(LevelSupplyEditing.AddTopSources(level) == null, "영상 실제 신규 공급구 " + scenario);
                        typeof(PuzzleGameSession).GetField("initialBytes", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(session, LevelPackCodec.Encode(new[] { level }));
                        await session.RestartAsync(CancellationToken.None); Invoke(session, "TickProgress", .7f);
                    }
                    try
                    {
                        session.enabled = true; Invoke(session, "Draw");
                        PuzzleUIRenderVerification.SetSize(450, 800);
                        for (int frame = 0; frame < 12; frame++) await UniTask.NextFrame();
                        Check(session.CanAcceptInput, "영상 시작 입력 가능 " + scenario);
                        BoardActionExecutor direct = new BoardActionExecutor(session.State);
                        PuzzleWorldBoard board = UnityEngine.Object.FindFirstObjectByType<PuzzleWorldBoard>();
                        PuzzleBoardInput input = UnityEngine.Object.FindFirstObjectByType<PuzzleBoardInput>();
                        BoardCoordinate first = new BoardCoordinate(4, 4), second = new BoardCoordinate(4, 5);
                        bool swap = scenario >= 4 && scenario != 16;
                        if (scenario == 14) { first = new BoardCoordinate(2, 3); second = new BoardCoordinate(3, 3); }
                        if (scenario == 15)
                        {
                            RuntimeCell cell = session.State.Cells.First(cell => cell.Coordinate.Column < 8 && ActionQuery.Swap(session.State, cell.Coordinate,
                                new BoardCoordinate(cell.Coordinate.Row, cell.Coordinate.Column + 1)).Reason == ActionReason.NoNewMatch);
                            first = cell.Coordinate; second = new BoardCoordinate(first.Row, first.Column + 1);
                        }
                        await RecordFrame(session, scenario, 0, 0, manifest);
                        Time.timeScale = 1; float start = Time.realtimeSinceStartup;
                        Vector2 firstPoint = session.BoardCamera.WorldToScreenPoint(board.transform.TransformPoint(PuzzleWorldBoard.CellPosition(first)));
                        Vector2 secondPoint = session.BoardCamera.WorldToScreenPoint(board.transform.TransformPoint(PuzzleWorldBoard.CellPosition(second)));
                        Invoke(input, "BeginPointer", 121, firstPoint);
                        if (swap) { Invoke(input, "UpdatePointer", 121, secondPoint); direct.Swap(first, second); }
                        else direct.Activate(first);
                        Invoke(input, "EndPointer", 121, swap ? secondPoint : firstPoint);
                        Check(session.IsPresenting, "실제 포인터 경로 파워/교환/복귀 표시 시작 " + scenario);
                        int count = 0; float next = 0; HashSet<string> stages = new HashSet<string>(); bool paused = false, rotated = false, clearedDroneTarget = false;
                        while ((session.IsPresenting || session.HasProgressFeedback || ((BoardActionExecutor)Field(session, "executor")).HasPendingCascade) &&
                            !session.HasFailed && Time.realtimeSinceStartup - start < 90)
                        {
                            await UniTask.NextFrame(); float elapsed = Time.realtimeSinceStartup - start;
                            string[] active = ((IEnumerable<string>)typeof(PuzzleStabilityVerification).GetMethod("Stages", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { session })).ToArray();
                            foreach (string stage in active) stages.Add(stage);
                            foreach (SpriteRenderer renderer in board.GetComponentsInChildren<SpriteRenderer>())
                            {
                                if (!renderer.enabled || renderer.sprite == null || (renderer.name != "Effect-playback" && renderer.name != "Supply-playback")) continue;
                                Transform moving = renderer.name == "Effect-playback" ? renderer.transform.parent : renderer.transform;
                                Vector3 position = board.transform.InverseTransformPoint(moving.position);
                                trajectories.Add(string.Join(",", scenario, elapsed.ToString("F4", CultureInfo.InvariantCulture), Time.frameCount,
                                    moving.GetInstanceID(), moving.name, position.x.ToString("F4", CultureInfo.InvariantCulture), position.y.ToString("F4", CultureInfo.InvariantCulture),
                                    moving.localEulerAngles.z.ToString("F4", CultureInfo.InvariantCulture), renderer.sprite.name));
                            }
                            if (scenario == 12 && active.Contains("drone"))
                            {
                                object playback = Field(session, "powerPlayback");
                                PuzzleEffectTimeline schedule = (PuzzleEffectTimeline)Field(playback, "timeline");
                                clearedDroneTarget |= schedule.Attacks.Any(attack => attack.Record.IsFlight && session.State.CellAt(attack.Record.Center).Content == RuntimeContent.Empty);
                            }
                            if (scenario == 2 && !paused && active.Contains("drone"))
                            {
                                string state = Snapshot(session.State);
                                Click("Pause"); Check(session.IsPaused, "실제 드론 중 pause UI");
                                await RecordFrame(session, scenario, ++count, elapsed, manifest);
                                await UniTask.Delay(200, ignoreTimeScale: true);
                                Check(Snapshot(session.State) == state && !session.AudioPlayback.Play(PuzzleFeedbackCueKind.Match), "드론pause 실제 대기·음성 정지");
                                Click("PuzzlePausePopup/Panel/Primary"); paused = true;
                            }
                            if (scenario == 2 && paused && !rotated && active.Contains("drone"))
                            {
                                PuzzleUIRenderVerification.SetSize(1280, 720);
                                for (int frame = 0; frame < 8; frame++) await UniTask.NextFrame();
                                Check(Screen.width == 1280 && Screen.height == 720 && !input.Selected.HasValue, "실제 드론 중 화면 회전·선택 잔류0"); rotated = true;
                            }
                            if (elapsed >= next)
                            {
                                await RecordFrame(session, scenario, ++count, elapsed, manifest);
                                next = elapsed + (elapsed < 2 ? .035f : .12f);
                            }
                        }
                        await RecordFrame(session, scenario, ++count, Time.realtimeSinceStartup - start, manifest);
                        while (direct.HasPendingCascade) direct.AdvanceCascade();
                        Check(!session.HasFailed && !session.IsPresenting && !session.HasProgressFeedback && Snapshot(session.State) == Snapshot(direct.State) &&
                            session.Phase == direct.Phase && Snapshot(session.Outcome) == Snapshot(direct.Outcome), "실제Update 영상 전체State·Phase·Outcome 직접 동등 " + scenario);
                        Check(((BoardActionExecutor)Field(session, "executor")).Turn == direct.Turn && (session.CanAcceptInput || session.ResultReady), "포인터 한 행동·종료 입력/결과 " + scenario);
                        if (scenario == 2) Check(paused && rotated, "드론 실제pause/회전 관찰 수행");
                        if (scenario == 12) Check(clearedDroneTarget, "다수 드론 논리 표적 소멸 중 실제 비행·최종 직접 동등");
                        summaries.Add(scenario + "," + (Time.realtimeSinceStartup - start).ToString("F4", CultureInfo.InvariantCulture) + "," + count + "," + string.Join("/", stages) + ",True," + session.Outcome?.Kind);
                    }
                    finally { UnityEngine.Object.Destroy(level); }
                }
                BaselineChecks();
            }
            catch (Exception error) { results.Add("FAIL " + error); exit = 1; }
            finally
            {
                File.WriteAllLines(Output + "motion-manifest.csv", manifest); File.WriteAllLines(Output + "motion-summary.csv", summaries);
                File.WriteAllLines(Output + "motion-trajectories.csv", trajectories);
                Time.timeScale = previous; FinishGameView(exit, "motion-results.txt");
            }
        }
        private static async UniTask RecordFrame(PuzzleGameSession session, int scenario, int index, float seconds, List<string> manifest)
        {
            string path = Output + "motion/case-" + scenario.ToString("D2") + "-" + index.ToString("D3") + ".png";
            if (File.Exists(path)) File.Delete(path);
            int width = Screen.width, height = Screen.height, frame = Time.frameCount;
            string stages = string.Join("/", (IEnumerable<string>)typeof(PuzzleStabilityVerification).GetMethod("Stages", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { session }));
            string phase = session.Phase.ToString(); bool inputReady = session.CanAcceptInput, resultReady = session.ResultReady;
            ScreenCapture.CaptureScreenshot(path);
            float deadline = Time.realtimeSinceStartup + 10;
            while ((!File.Exists(path) || new FileInfo(path).Length < 1000) && Time.realtimeSinceStartup < deadline) await UniTask.NextFrame();
            ValidatePng(path, width, height);
            manifest.Add(string.Join(",", path, scenario, seconds.ToString("F4", CultureInfo.InvariantCulture), frame, width, height, phase, stages, inputReady, resultReady));
        }
    }
}
