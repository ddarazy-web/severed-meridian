using System;
using System.Collections.Generic;
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
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameScreen.Editor
{
    public static partial class PuzzlePresentationAcceptanceVerification
    {
        public static void RunInteraction()
        {
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("미저장 씬 보존");
            Directory.CreateDirectory(Output + "motion"); PuzzleUIRenderVerification.RememberSize();
            SessionState.SetBool("Stage12.Interaction", true);
            EditorSceneManager.OpenScene(PuzzleGameAssets.ScenePath); EditorApplication.EnterPlaymode();
        }

        private static async UniTask InteractionAsync()
        {
            results.Clear(); int exit = 0; float previous = Time.timeScale; LevelDefinition owned = null;
            List<string> manifest = new List<string> { "file,case,seconds,frame,width,height,phase,stages,inputReady,resultReady" };
            try
            {
                Time.timeScale = 0; Application.runInBackground = true;
                PuzzleUIRenderVerification.SetSize(450, 800);
                for (int frame = 0; frame < 12; frame++) await UniTask.NextFrame();
                PuzzleGameSession session = await LoadOriginal(PuzzleEditorLevelSource.Asset);
                owned = await (UniTask<LevelDefinition>)typeof(PuzzleStabilityVerification).GetMethod("PrepareFixture", BindingFlags.NonPublic | BindingFlags.Static)
                    .Invoke(null, new object[] { session, 0 });
                session.enabled = true; Invoke(session, "Draw");
                for (int frame = 0; frame < 12; frame++) await UniTask.NextFrame();
                PuzzleWorldBoard board = UnityEngine.Object.FindFirstObjectByType<PuzzleWorldBoard>();
                PuzzleBoardInput input = UnityEngine.Object.FindFirstObjectByType<PuzzleBoardInput>();
                PuzzleScreenView screen = UnityEngine.Object.FindFirstObjectByType<PuzzleScreenView>();
                PuzzleHudView hud = UnityEngine.Object.FindFirstObjectByType<PuzzleHudView>();
                BoardCoordinate first = new BoardCoordinate(2, 3), second = new BoardCoordinate(3, 3);
                Vector2 Point(BoardCoordinate at) => session.BoardCamera.WorldToScreenPoint(board.transform.TransformPoint(PuzzleWorldBoard.CellPosition(at)));
                string initial = Snapshot(session.State);
                SpriteRenderer selected = board.OccupantAt(first), neighbor = board.OccupantAt(second);
                int order = selected.sortingOrder;
                Vector3 origin = selected.transform.localPosition;
                Invoke(input, "BeginPointer", 122, Point(first));
                Vector2 shortDrag = session.BoardCamera.WorldToScreenPoint(board.transform.TransformPoint(PuzzleWorldBoard.CellPosition(first) + Vector3.down * .2f));
                Invoke(input, "UpdatePointer", 122, shortDrag);
                Check(selected.sortingOrder > neighbor.sortingOrder && selected.transform.localPosition != origin && Snapshot(session.State) == initial,
                    "실제 선택 이동·주변보다 높은 order·임계 미만 규칙 불변");
                await RecordFrame(session, 18, 0, 0, manifest);
                Invoke(input, "EndPointer", 122, shortDrag);
                Check(selected.sortingOrder == order && selected.transform.localPosition == origin && input.Selected.HasValue && input.Selected.Value.Equals(first),
                    "손 놓기 order·위치 복원·선택 표시");
                await RecordFrame(session, 18, 1, 0, manifest);

                Transform pause = screen.transform.Find("SafeArea/Pause");
                Vector2 uiPoint = RectTransformUtility.WorldToScreenPoint(null, pause.position);
                List<RaycastResult> hits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = uiPoint }, hits);
                Check(hits.Count > 0, "실제 Pause UI Raycast 준비");
                Invoke(input, "BeginPointer", 122, uiPoint); Invoke(input, "EndPointer", 122, Point(second));
                Check(Snapshot(session.State) == initial && !input.Selected.HasValue, "UI에서 시작한 입력 보드 행동0");
                Invoke(input, "BeginPointer", 122, Point(first)); Invoke(input, "EndPointer", 122, uiPoint);
                Check(Snapshot(session.State) == initial && !input.Selected.HasValue && selected.sortingOrder == order, "UI에서 끝난 입력 행동0·order 복원");
                Invoke(input, "BeginPointer", 122, Point(first)); Click("Pause");
                Check(session.IsPaused && !input.Selected.HasValue && selected.sortingOrder == order, "실제 Pause 클릭으로 진행 제스처 취소");
                Invoke(input, "EndPointer", 122, Point(second));
                Check(Snapshot(session.State) == initial, "pause 중 남은 손 놓기 행동0");
                await RecordFrame(session, 18, 2, 0, manifest);
                Click("PuzzlePausePopup/Panel/Primary");

                BoardActionExecutor direct = new BoardActionExecutor(session.State); direct.Swap(first, second);
                Invoke(input, "BeginPointer", 123, Point(first)); Invoke(input, "UpdatePointer", 123, Point(second));
                Invoke(input, "EndPointer", 123, Point(second)); Invoke(input, "EndPointer", 123, Point(second));
                Time.timeScale = 1; float started = Time.realtimeSinceStartup;
                while (session.ProgressFeedback.Flights.Count == 0 && !session.HasFailed && Time.realtimeSinceStartup - started < 10) await UniTask.NextFrame();
                Check(session.ProgressFeedback.Flights.Count > 0, "실제 Update 수집 비행 시작");
                Time.timeScale = 0;
                PuzzleProgressFeedback.Flight flight = session.ProgressFeedback.Flights[0];
                Check(flight.Source.HasValue && session.State.CellAt(flight.Source.Value).Content != RuntimeContent.Normal,
                    "수집 원본 블록 소멸 후 원점 보존");
                await RecordFrame(session, 18, 3, Time.realtimeSinceStartup - started, manifest);
                PuzzleUIRenderVerification.SetSize(1280, 720);
                for (int frame = 0; frame < 12; frame++) await UniTask.NextFrame();
                hud.Frame(session);
                Image icon = screen.GetComponentsInChildren<Image>().Single(image => image.name == "MissionCollection" + flight.Slot);
                Vector2 source = session.BoardCamera.WorldToScreenPoint(session.CollectionWorldPosition(flight.Source.Value));
                PuzzleMissionView destination = hud.GetComponentsInChildren<PuzzleMissionView>()[flight.MissionIndex];
                Vector2 target = RectTransformUtility.WorldToScreenPoint(null, destination.Icon.position);
                Vector2 actual = RectTransformUtility.WorldToScreenPoint(null, icon.rectTransform.position);
                float t = Mathf.SmoothStep(0, 1, flight.Progress);
                Vector2 expected = Vector2.Lerp(source, target, t) + Vector2.up * Mathf.Sin(t * Mathf.PI) * Mathf.Min(80, Vector2.Distance(source, target) * .12f);
                Check(Screen.width == 1280 && Screen.height == 720 && Vector2.Distance(actual, expected) < 2 && !icon.raycastTarget,
                    "수집 중 실제 회전·현재 카메라/HUD 위치 재계산·입력 비차단");
                await RecordFrame(session, 18, 4, Time.realtimeSinceStartup - started, manifest);
                Click("Pause"); string frozen = Snapshot(session.State); float progress = flight.Progress;
                Time.timeScale = 1; await UniTask.Delay(200, ignoreTimeScale: true);
                Check(session.IsPaused && Snapshot(session.State) == frozen && flight.Progress == progress, "수집 중 실제 pause 대기 불변");
                Click("PuzzlePausePopup/Panel/Primary");
                while ((session.IsPresenting || session.HasProgressFeedback || ((BoardActionExecutor)Field(session, "executor")).HasPendingCascade) &&
                    !session.HasFailed && Time.realtimeSinceStartup - started < 30) await UniTask.NextFrame();
                while (direct.HasPendingCascade) direct.AdvanceCascade();
                Check(!session.HasFailed && Snapshot(session.State) == Snapshot(direct.State) && session.Phase == direct.Phase && Snapshot(session.Outcome) == Snapshot(direct.Outcome),
                    "수집 소멸·회전·pause 후 전체 상태 직접 동등");
                Check(((BoardActionExecutor)Field(session, "executor")).Turn == direct.Turn && session.CanAcceptInput && !session.HasProgressFeedback &&
                    !screen.GetComponentsInChildren<Image>().Any(image => image.name.StartsWith("MissionCollection")), "중복 손 놓기 행동1·수집 잔상0");
                await RecordFrame(session, 18, 5, Time.realtimeSinceStartup - started, manifest);

                Click("Pause"); Click("PuzzlePausePopup/Panel/Retry");
                float deadline = Time.realtimeSinceStartup + 20;
                while ((!session.CanAcceptInput || session.IsRestarting) && !session.HasFailed && Time.realtimeSinceStartup < deadline) await UniTask.NextFrame();
                Check(session.CanAcceptInput && !session.IsPaused && Snapshot(session.State) == initial && !input.Selected.HasValue &&
                    !session.HasProgressFeedback && !session.IsPresenting, "실제 다시하기 초기 상태·선택/연출/수집 잔류0");
                await RecordFrame(session, 18, 6, 0, manifest);
                UnityEngine.Object.Destroy(owned);
                owned = (LevelDefinition)typeof(PowerEffectVerification).GetMethod("Make", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
                Time.timeScale = 0;
                ObstacleKind[] kinds = { ObstacleKind.Crate, ObstacleKind.Scrap, ObstacleKind.Safe, ObstacleKind.ColorLock,
                    ObstacleKind.Appliance, ObstacleKind.Generator, ObstacleKind.Safe, ObstacleKind.Appliance };
                BoardCoordinate[] anchors = { new BoardCoordinate(1, 0), new BoardCoordinate(1, 2), new BoardCoordinate(1, 4), new BoardCoordinate(1, 6),
                    new BoardCoordinate(4, 0), new BoardCoordinate(4, 3), new BoardCoordinate(2, 8), new BoardCoordinate(4, 6) };
                for (int index = 0; index < kinds.Length; index++)
                {
                    LevelObstacleEditing.Apply(owned, new PlacementBrush { Layer = PlacementLayer.Block, Erase = true },
                        LevelPlacementRules.Footprint(anchors[index], LevelPlacementRules.Size(kinds[index])));
                    Check(LevelObstacleEditing.Apply(owned, new PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)kinds[index],
                        Durability = index < 6 ? Math.Max(1, LevelPlacementRules.MaxDurability(kinds[index])) : 1, RequiredCharge = 3,
                        Color = RabbitColor.Type1 }, new[] { anchors[index] }).Changed == 1, "실제 장애물 갤러리 fixture " + index);
                }
                Check(LevelObstacleEditing.Apply(owned, new PlacementBrush { Layer = PlacementLayer.Cover, Kind = (int)CoverKind.Web, Durability = 3 },
                    new[] { new BoardCoordinate(7, 1) }).Changed == 1, "거미줄 실제 fixture");
                Check(LevelObstacleEditing.Apply(owned, new PlacementBrush { Layer = PlacementLayer.Cover, Kind = (int)CoverKind.Mold, Durability = 1 },
                    new[] { new BoardCoordinate(7, 3) }).Changed == 1, "곰팡이 실제 fixture");
                Check(LevelObstacleEditing.Apply(owned, new PlacementBrush { Layer = PlacementLayer.Dust, Durability = 3 },
                    new[] { new BoardCoordinate(7, 5) }).Changed == 1, "먼지 실제 fixture");
                Check(LevelConnectionEditing.Add(owned, owned.Obstacles[5].Id, owned.Obstacles[4].Id) == null, "발전기 갤러리 실제 연결");
                string wireError = LevelConnectionEditing.SetWire(owned, 0, new[] { new BoardCoordinate(4, 3), new BoardCoordinate(4, 2) });
                Check(wireError == null, "발전기 갤러리 실제 전선: " + wireError);
                foreach (BoardCoordinate cell in new[] { new BoardCoordinate(3, 2), new BoardCoordinate(3, 4), new BoardCoordinate(3, 5), new BoardCoordinate(2, 3) })
                    typeof(PowerEffectVerification).GetMethod("Place", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null,
                        new object[] { owned, cell, InitialBlockKind.FixedNormal, RocketDirection.Horizontal, RabbitColor.Type1 });
                typeof(PuzzleGameSession).GetField("initialBytes", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(session, LevelPackCodec.Encode(new[] { owned }));
                LevelStateBuildResult galleryBuild = LevelStateBuilder.Build(owned, 12345);
                Check(galleryBuild.IsBuilt, "갤러리 실제 정의 검증: " + string.Join(" | ", galleryBuild.Issues));
                Check(new StartConditionReport(galleryBuild.State).IsSatisfied, "갤러리 무매칭·유효 교환 시작 조건");
                await session.RestartAsync(CancellationToken.None); Invoke(session, "TickProgress", .7f);
                Check(session.IsReady && !session.HasFailed, "갤러리 세션 준비: " + session.Message);
                PuzzleUIRenderVerification.SetSize(450, 800);
                for (int frame = 0; frame < 12; frame++) await UniTask.NextFrame();
                PuzzleArtwork artwork = (PuzzleArtwork)Field(session, "artwork");
                foreach (RuntimeObstacle body in session.State.Obstacles)
                {
                    Sprite expectedImage = artwork.Get(PuzzleArtworkPaths.Obstacle(body));
                    Check(expectedImage != null && board.GetComponentsInChildren<SpriteRenderer>().Any(renderer => renderer.enabled && renderer.sprite == expectedImage),
                        "실제 갤러리 이미지 로드·표시 " + body.Definition.Kind + "/" + body.Durability);
                }
                foreach (string path in new[] { "Obstacles/Web/web-durability-3-v2-256", "Obstacles/Mold/mold-base-v1-256", "Obstacles/Dust/dust-durability-3-v1-256" })
                    Check(artwork.Get(path) != null && board.GetComponentsInChildren<SpriteRenderer>().Any(renderer => renderer.enabled && renderer.sprite == artwork.Get(path)),
                        "실제 덮개·먼지 이미지 표시 " + path);
                await RecordFrame(session, 19, 0, 0, manifest);
                BaselineChecks();
            }
            catch (Exception error) { results.Add("FAIL " + error); exit = 1; }
            finally
            {
                if (owned != null) UnityEngine.Object.Destroy(owned);
                File.WriteAllLines(Output + "interaction-manifest.csv", manifest);
                Time.timeScale = previous; FinishGameView(exit, "interaction-results.txt");
            }
        }
    }
}
