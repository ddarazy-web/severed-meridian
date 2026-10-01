using System;
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
    [InitializeOnLoad]
    public static partial class PuzzlePowerAnimationVerification
    {
        private const string PlayKey = "Stage08.Render";
        static PuzzlePowerAnimationVerification()
        {
            EditorApplication.playModeStateChanged += change =>
            {
                if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(PlayKey, false))
                { SessionState.SetBool(PlayKey, false); RenderAsync().Forget(Debug.LogException); }
                if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(PlayKey + ".Scene", false))
                { SessionState.SetBool(PlayKey + ".Scene", false); PowerSceneAsync().Forget(Debug.LogException); }
            };
        }
        public static void Run()
        {
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("미저장 씬 보존");
            Directory.CreateDirectory(Output); SessionState.SetBool(PlayKey, true);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); EditorApplication.EnterPlaymode();
        }
        private static async UniTask RenderAsync()
        {
            results.Clear(); int exit = 0; GameObject root = null; LevelDefinition level = null; PuzzleArtwork art = null;
            try
            {
                Type playbackType = typeof(PuzzleGameSession).Assembly.GetType("GameScreen.PuzzlePowerPlayback");
                Check(playbackType != null, "파워 공격·타격 표시 재생기 존재");
                level = (LevelDefinition)typeof(PowerEffectVerification).GetMethod("Make", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
                BoardCoordinate origin = new BoardCoordinate(4, 4), target = new BoardCoordinate(4, 7);
                typeof(PowerEffectVerification).GetMethod("Place", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null,
                    new object[] { level, origin, InitialBlockKind.Rocket, RocketDirection.Horizontal, RabbitColor.Type1 });
                LevelRuntimeState before = LevelStateBuilder.Build(level, 12345).State;
                BoardActionExecutor executor = new BoardActionExecutor(before); BoardActionResult result = executor.Activate(origin);
                Check(result.IsApplied, "실제 로켓 발동 fixture");
                root = new GameObject("Power verification");
                PuzzleWorldBoard board = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<PuzzleWorldBoard>(PuzzleGameAssets.Folder + "/PuzzleWorldBoard.prefab"), root.transform);
                art = new PuzzleArtwork(); await art.PrepareAsync(before, CancellationToken.None);
                PuzzleEffectTimeline timeline = new PuzzleEffectTimeline(before, result.Changes, result.Effects, result.PowerTrace);
                object playback = Activator.CreateInstance(playbackType, true);
                await (UniTask)playbackType.GetMethod("PrepareAsync", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(playback, new object[] { before, executor.State, art, timeline, CancellationToken.None });
                board.Draw(before, art); Sprite original = board.OccupantAt(target).sprite;
                playbackType.GetMethod("Begin", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(playback, new object[] { board });
                Check(board.OccupantAt(target).sprite == original, "공격 시작 전 대상 이미지 유지");
                playbackType.GetMethod("Tick", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(playback, new object[] { .1f });
                Check(board.OccupantAt(target).enabled && board.OccupantAt(target).sprite == original, "로켓 도착 전 대상 조기 제거 없음");
                playbackType.GetMethod("Tick", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(playback, new object[] { 1f });
                Check(board.OccupantAt(target) == null, "로켓 도착 후 대상 제거 표시");
                playbackType.GetMethod("Reset", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(playback, null);
                for (int fixture = 0; fixture < 14; fixture++)
                {
                    art.Dispose(); UnityEngine.Object.Destroy(level);
                    bool combination = fixture >= 4;
                    level = combination ? (LevelDefinition)typeof(CombinationVerification).GetMethod("Make", BindingFlags.NonPublic | BindingFlags.Static)
                        .Invoke(null, new object[] { fixture - 4, RocketDirection.Horizontal, null }) :
                        (LevelDefinition)typeof(PowerEffectVerification).GetMethod("Make", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
                    if (!combination)
                        typeof(PowerEffectVerification).GetMethod("Place", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null,
                            new object[] { level, origin, new[] { InitialBlockKind.Rocket, InitialBlockKind.Bomb, InitialBlockKind.Drone, InitialBlockKind.Magnet }[fixture], RocketDirection.Horizontal, RabbitColor.Type1 });
                    before = LevelStateBuilder.Build(level, 12345).State;
                    executor = new BoardActionExecutor(before);
                    result = combination ? executor.Swap(origin, new BoardCoordinate(4, 5)) : executor.Activate(origin);
                    Check(result.IsApplied, "파워 표시 fixture " + fixture);
                    art = new PuzzleArtwork(); await art.PrepareAsync(before, CancellationToken.None);
                    timeline = new PuzzleEffectTimeline(before, result.Changes, result.Effects, result.PowerTrace);
                    await (UniTask)playbackType.GetMethod("PrepareAsync", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(playback, new object[] { before, executor.State, art, timeline, CancellationToken.None });
                    playbackType.GetMethod("Begin", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(playback, new object[] { board });
                    if (fixture == 2)
                        Check(board.GetComponentsInChildren<SpriteRenderer>().Any(renderer => renderer.sprite != null && renderer.sprite.name.StartsWith("collection-drone-rotor")),
                            "드론은 선행 공격 대기 중에도 프로펠러 본체 표시");
                    int droneFrame = 0;
                    bool checkedTransformBefore = false, checkedTransformAfter = false;
                    bool checkedDroneOrbit = false, checkedDroneSeparation = false;
                    var flight = timeline.Attacks.FirstOrDefault(attack => attack.Record.IsFlight);
                    var hoveringClips = ((System.Collections.IEnumerable)playbackType.GetField("clips", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(playback))
                        .Cast<object>().Where(clip => (string)clip.GetType().GetField("Label", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(clip) == "Drone-hover").ToArray();
                    Check(!((System.Collections.IEnumerable)playbackType.GetField("clips", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(playback)).Cast<object>()
                        .Any(clip => ((string[])clip.GetType().GetField("Paths", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(clip)).Any(path => path.Contains("drone-target"))),
                        "드론 표적 예고 이미지 없음 " + fixture);
                    foreach (object clip in hoveringClips)
                    {
                        Type clipType = clip.GetType();
                        Check(((Vector3)clipType.GetField("LaneOffset", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(clip)).magnitude >= 1.2f,
                            "드론은 출발점에서 충분히 떨어진 공중 위치로 이동 " + fixture);
                        Check((float)clipType.GetField("End", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(clip) -
                            (float)clipType.GetField("Start", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(clip) >= .719f,
                            "드론은 공중 이동 후 선회할 시간을 확보 " + fixture);
                    }
                    var droneClips = ((System.Collections.IEnumerable)playbackType.GetField("clips", BindingFlags.Instance | BindingFlags.NonPublic)
                        .GetValue(playback)).Cast<object>().Where(clip => (string)clip.GetType().GetField("Label", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(clip) == "Drone-flight").ToArray();
                    foreach (object clip in droneClips)
                    {
                        Type clipType = clip.GetType();
                        Vector3 departure = (Vector3)clipType.GetField("From", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(clip);
                        Vector3 destination = (Vector3)clipType.GetField("To", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(clip);
                        Vector3 control = (Vector3)clipType.GetField("Control1", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(clip);
                        float duration = (float)clipType.GetField("End", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(clip) -
                            (float)clipType.GetField("Start", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(clip);
                        Check(Vector3.Cross(destination - departure, control - departure).magnitude > .01f, "드론 돌진 곡선 제어점 " + fixture);
                        Check(Vector3.Distance(departure, destination) / duration > .12f * Mathf.PI * 2 / .7f, "드론 돌진 평균 속도는 선회보다 빠름 " + fixture);
                    }
                    for (float time = 0; time < timeline.Duration + .02f; time += .02f)
                    {
                        playbackType.GetMethod("Tick", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(playback, new object[] { .02f });
                        if (fixture == 2 && !checkedDroneOrbit && time + .02f >= .08f)
                        {
                            Transform hover = board.GetComponentsInChildren<Transform>().First(item => item.name == "Drone-hover");
                            Check(Vector3.Distance(hover.localPosition, PuzzleWorldBoard.CellPosition(flight.Record.Origin)) > .01f,
                                "드론은 표적 비행 대기 중 공중에서 선회");
                            checkedDroneOrbit = true;
                        }
                        if ((fixture == 9 || fixture == 12) && !checkedDroneSeparation && time + .02f >= (timeline.Combination?.IsTransformation == true ? 1.03f : .68f))
                        {
                            Transform[] hovering = board.GetComponentsInChildren<Transform>().Where(item => item.name == "Drone-hover").ToArray();
                            Check(hovering.Length > 1, "다중 드론 선회 fixture " + fixture);
                            for (int a = 0; a < hovering.Length; a++)
                                for (int b = a + 1; b < hovering.Length; b++)
                                    Check(Vector3.Distance(hovering[a].localPosition, hovering[b].localPosition) >= .95f, "드론 선회 궤도 분리 " + fixture + ":" + a + ":" + b);
                            checkedDroneSeparation = true;
                        }
                        if (timeline.Combination?.IsTransformation == true)
                        {
                            LevelRuntimeState displayed = (LevelRuntimeState)playbackType.GetField("visual", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(playback);
                            if (!checkedTransformBefore && time + .02f >= .2f)
                            {
                                Check(timeline.Combination.Transformations.All(item => displayed.CellAt(item.Coordinate).Content == before.CellAt(item.Coordinate).Content),
                                    "변환 중 원본 블록 유지 " + fixture);
                                checkedTransformBefore = true;
                            }
                            if (!checkedTransformAfter && time + .02f >= .38f)
                            {
                                Check(timeline.Reactions.Where(reaction => reaction.Time <= .36f && reaction.Record.Response == DamageResponse.Activate)
                                    .All(reaction => displayed.CellAt(reaction.Record.Target).Content == RuntimeContent.Empty),
                                    "발동한 변환 파워 재등장 없음 " + fixture);
                                CapturePower(board, "transform-active-" + fixture, fixture % 2 == 0);
                                checkedTransformAfter = true;
                            }
                        }
                        if (time < .14f && time + .02f >= .14f) CapturePower(board, "fixture-" + fixture, fixture % 2 == 0);
                        if (fixture == 2 && flight != null && droneFrame < 4 && time >= flight.Start + droneFrame * .06f)
                            CapturePower(board, "drone-frame-" + droneFrame++, false);
                    }
                    Check(!(bool)playbackType.GetProperty("IsPlaying", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(playback), "파워 표시 정상 종료 " + fixture);
                    Check(board.GetComponentsInChildren<SpriteRenderer>().All(renderer => renderer.name != "Effect-playback"), "파워 종료 잔상 없음 " + fixture);
                    playbackType.GetMethod("Reset", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(playback, null);
                }
                art.Dispose(); art = null;
                await VerifyObstacleFramesAsync(board, playback, playbackType);
                await VerifyLayerFramesAsync(board, playback, playbackType);
                await VerifyCreationFramesAsync(board, playback, playbackType);
                UnityEngine.Object.Destroy(level);
                level = (LevelDefinition)typeof(BoardActionVerification).GetMethod("RocketBoard", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
                before = LevelStateBuilder.Build(level, 12345).State;
                RuntimeCell webCell = before.CellAt(new BoardCoordinate(3, 2)), dustCell = before.CellAt(new BoardCoordinate(3, 4));
                typeof(RuntimeCell).GetProperty("Cover").SetValue(webCell, CoverKind.Web);
                typeof(RuntimeCell).GetProperty("CoverDurability").SetValue(webCell, 1);
                typeof(RuntimeCell).GetProperty("DustDurability").SetValue(dustCell, 1);
                executor = new BoardActionExecutor(before); result = executor.Swap(new BoardCoordinate(2, 3), new BoardCoordinate(3, 3));
                Check(result.IsApplied && result.Changes.Any(change => change.CoverBefore > change.CoverAfter) && executor.State.CellAt(dustCell.Coordinate).DustDurability == 0,
                    "직접 매칭의 거미줄·먼지 변화 fixture");
                timeline = new PuzzleEffectTimeline(before, result.Changes, result.Effects, result.PowerTrace);
                art = new PuzzleArtwork(); await art.PrepareAsync(before, CancellationToken.None);
                await (UniTask)playbackType.GetMethod("PrepareAsync", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(playback, new object[] { before, executor.State, art, timeline, CancellationToken.None });
                playbackType.GetMethod("Begin", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(playback, new object[] { board });
                playbackType.GetMethod("Tick", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(playback, new object[] { .13f });
                Check(board.GetComponentsInChildren<SpriteRenderer>().Any(renderer => renderer.sprite != null && renderer.sprite.name.StartsWith("web-break")), "직접 매칭 거미줄 효과 표시");
                Check(board.GetComponentsInChildren<SpriteRenderer>().Any(renderer => renderer.sprite != null && renderer.sprite.name.StartsWith("dust-clear")), "직접 매칭 먼지 효과 표시");
                playbackType.GetMethod("Reset", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(playback, null);
                art.Dispose(); art = null;
                foreach (bool charge in new[] { true, false })
                {
                    UnityEngine.Object.Destroy(level);
                    level = (LevelDefinition)typeof(GeneratorVerification).GetMethod("Make", BindingFlags.NonPublic | BindingFlags.Static)
                        .Invoke(null, new object[] { ObstacleKind.Appliance, 3 });
                    BoardCoordinate rocket = new BoardCoordinate(4, charge ? 1 : 6);
                    typeof(PowerEffectVerification).GetMethod("Place", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null,
                        new object[] { level, rocket, InitialBlockKind.Rocket, RocketDirection.Horizontal, RabbitColor.Type1 });
                    before = LevelStateBuilder.Build(level, 12345).State;
                    typeof(RuntimeObstacle).GetProperty(charge ? "Charge" : "Durability").SetValue(before.Obstacles[charge ? 0 : 1], charge ? 2 : 1);
                    executor = new BoardActionExecutor(before); result = executor.Activate(rocket);
                    Check(result.IsApplied, "발전기 화면 fixture " + charge);
                    timeline = new PuzzleEffectTimeline(before, result.Changes, result.Effects, result.PowerTrace);
                    float removal = timeline.Reactions.First(reaction => reaction.Record.RemovedObstacleIndices.Contains(0)).Time;
                    art = new PuzzleArtwork(); await art.PrepareAsync(before, CancellationToken.None);
                    await (UniTask)playbackType.GetMethod("PrepareAsync", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(playback, new object[] { before, executor.State, art, timeline, CancellationToken.None });
                    playbackType.GetMethod("Begin", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(playback, new object[] { board });
                    playbackType.GetMethod("Tick", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(playback, new object[] { removal - .01f });
                    Check(board.GetComponentsInChildren<SpriteRenderer>().Count(renderer => renderer.name.StartsWith("Obstacle-")) == 2, "발전기 간접 제거 직전 두 본체 유지 " + charge);
                    playbackType.GetMethod("Tick", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(playback, new object[] { .02f });
                    Check(board.GetComponentsInChildren<SpriteRenderer>().All(renderer => !renderer.name.StartsWith("Obstacle-")), "발전기 타격 시 연결 본체 동시 제거 " + charge);
                    CapturePower(board, "generator-" + charge, false);
                    playbackType.GetMethod("Reset", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(playback, null);
                    art.Dispose(); art = null;
                }
                LevelDefinition sessionLevel = (LevelDefinition)typeof(PowerEffectVerification).GetMethod("Make", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
                typeof(PowerEffectVerification).GetMethod("Place", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null,
                    new object[] { sessionLevel, origin, InitialBlockKind.Rocket, RocketDirection.Horizontal, RabbitColor.Type1 });
                Camera sessionCamera = new GameObject("Session camera", typeof(Camera)).GetComponent<Camera>();
                sessionCamera.transform.SetParent(root.transform); sessionCamera.enabled = false;
                PuzzleGameSession session = root.AddComponent<PuzzleGameSession>(); session.Configure(board, sessionCamera);
                await session.InitializeAsync(UnityEngine.Object.Instantiate(sessionLevel), 12345, CancellationToken.None);
                Check(session.IsReady && session.TryActivate(origin), "게임 세션 파워 발동");
                Check(session.IsPresenting && !session.CanAcceptInput && !session.CanUseItems, "효과 준비부터 입력·아이템 잠금");
                session.SetPaused(true);
                float deadline = Time.realtimeSinceStartup + 20;
                FieldInfo preparing = typeof(PuzzleGameSession).GetField("preparingEffects", BindingFlags.Instance | BindingFlags.NonPublic);
                while ((bool)preparing.GetValue(session) && Time.realtimeSinceStartup < deadline) await UniTask.Yield();
                Check(!(bool)preparing.GetValue(session) && !session.HasFailed, "게임 세션 효과 아틀라스 준비 완료");
                session.SetPaused(false);
                typeof(PuzzleGameSession).GetMethod("AdvancePresentation", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(session, new object[] { .14f });
                Check(board.OccupantAt(target) != null && board.OccupantAt(target).enabled, "게임 세션도 로켓 도착까지 대상 유지");
                Check(board.GetComponentsInChildren<SpriteRenderer>().Any(renderer => renderer.name == "Effect-playback"), "게임 세션 실제 효과 표시");
                typeof(PuzzleGameSession).GetMethod("AdvancePresentation", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(session, new object[] { 10f });
                Check(!session.IsPresenting && board.OccupantAt(target) == null, "게임 세션 효과 종료 후 제거 확정");
                UnityEngine.Object.DestroyImmediate(session);
                foreach (bool missingSprite in new[] { true, false })
                {
                    GameObject owner = new GameObject("Effect lifetime verification"); owner.transform.SetParent(root.transform);
                    PuzzleGameSession lifetimeSession = owner.AddComponent<PuzzleGameSession>(); lifetimeSession.Configure(board, sessionCamera);
                    await lifetimeSession.InitializeAsync(UnityEngine.Object.Instantiate(sessionLevel), 12345, CancellationToken.None);
                    PuzzleArtwork lifetimeArt = (PuzzleArtwork)typeof(PuzzleGameSession).GetField("artwork", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lifetimeSession);
                    if (missingSprite)
                    {
                        // 정상 로드된 다른 아틀라스를 넣어 필요한 효과 프레임 누락을 재현한다. 에셋은 수정하지 않는다.
                        var atlases = (System.Collections.Generic.Dictionary<string, BoardSpriteAtlas>)typeof(PuzzleArtwork).GetField("atlases", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lifetimeArt);
                        BoardSpriteAtlas wrongAtlas = new BoardSpriteAtlas(BoardSpriteAtlas.AddressFor("Blocks/"));
                        await wrongAtlas.LoadAsync(); atlases.Add(BoardSpriteAtlas.AddressFor("Effects/Rocket/"), wrongAtlas);
                    }
                    Check(lifetimeSession.TryActivate(origin), "자원 실패/종료 파워 시작 " + missingSprite);
                    if (!missingSprite)
                    {
                        Check((bool)preparing.GetValue(lifetimeSession), "씬 객체 제거 전 효과 준비 진행 중");
                        UnityEngine.Object.DestroyImmediate(owner);
                    }
                    deadline = Time.realtimeSinceStartup + 20;
                    FieldInfo pendingLoads = typeof(PuzzleArtwork).GetField("pending", BindingFlags.Instance | BindingFlags.NonPublic);
                    while ((int)pendingLoads.GetValue(lifetimeArt) > 0 && Time.realtimeSinceStartup < deadline) await UniTask.Yield();
                    await UniTask.Yield();
                    Check(lifetimeArt.AtlasCount == 0, "실패/씬 종료 후 아틀라스 소유권 반환 " + missingSprite);
                    Check(board.GetComponentsInChildren<SpriteRenderer>().All(image => image.name != "Effect-playback"), "실패/씬 종료 효과 잔상 없음 " + missingSprite);
                    if (missingSprite)
                    {
                        Check(lifetimeSession.HasFailed && !lifetimeSession.IsPresenting && lifetimeSession.Message.Contains("이미지가 준비되지"),
                            "누락 효과 프레임은 명시적 실패로 종료하고 준비 잠금 해제");
                        UnityEngine.Object.DestroyImmediate(owner);
                    }
                }
                UnityEngine.Object.Destroy(sessionLevel);
            }
            catch (Exception error) { results.Add("FAIL " + error); exit = 1; }
            finally
            {
                art?.Dispose(); if (root != null) UnityEngine.Object.Destroy(root); if (level != null) UnityEngine.Object.Destroy(level);
                File.WriteAllLines(Output + "render-results.txt", results); EditorApplication.Exit(exit);
            }
        }

        private static void CapturePower(PuzzleWorldBoard board, string name, bool portrait)
        {
            int width = portrait ? 450 : 1280, height = portrait ? 800 : 720;
            Camera camera = new GameObject("Power capture", typeof(Camera)).GetComponent<Camera>();
            RenderTexture target = RenderTexture.GetTemporary(width, height, 24);
            RenderTexture previous = RenderTexture.active;
            Texture2D pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                camera.transform.position = board.transform.position + Vector3.back * 10;
                camera.orthographic = true; camera.aspect = (float)width / height;
                camera.orthographicSize = 5 / Mathf.Min(1, camera.aspect);
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.06f, .08f, .12f);
                // 수동 Tick을 같은 프레임에 여러 번 호출하므로 렌더 직전 그룹 순서도 갱신한다.
                UnityEngine.Rendering.SortingGroup.UpdateAllSortingGroups();
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
                File.WriteAllBytes(Output + name + ".png", pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.Destroy(pixels); UnityEngine.Object.Destroy(camera.gameObject);
            }
        }
    }
}
