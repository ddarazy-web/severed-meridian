using System;
using System.Collections;
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
using UnityEngine.SceneManagement;

namespace GameScreen.Editor
{
    [InitializeOnLoad]
    public static partial class PoolBaselineVerification
    {
        private const string PlayKey = "EF11.PoolBaseline.Play";
        static PoolBaselineVerification()
        {
            EditorApplication.playModeStateChanged += change =>
            {
                if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(PlayKey, false))
                { SessionState.SetBool(PlayKey, false); PlayAsync().Forget(error => { Debug.LogException(error); EditorApplication.Exit(1); }); }
            };
        }
        public static void RunPlay()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 Editor에서만 실행한다.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(PlayKey, true); EditorApplication.EnterPlaymode();
        }
        private static async UniTask PlayAsync()
        {
            int exit = 0; Results.Clear(); Rows.Clear(); Directory.CreateDirectory(Evidence);
            File.WriteAllText(Evidence + "/scene-lifetime.csv", "observation,boardDestroyed,remainingRenderers,maskSpriteDestroyed" + Environment.NewLine);
            PuzzleWorldBoard board = null; PuzzleArtwork art = new PuzzleArtwork(); LevelDefinition level = null;
            try
            {
                level = (LevelDefinition)typeof(Levels.Editor.PowerEffectVerification).GetMethod("Make", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
                LevelRuntimeState state = Build(level); await art.PrepareAsync(state, CancellationToken.None);
                await (UniTask)typeof(PuzzleArtwork).GetMethod("PrepareEffectsAsync", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(art, new object[] { new[] { "Effects/Match/Animations/match-pink-frame-01-v1-256" }, CancellationToken.None });
                board = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<PuzzleWorldBoard>("Assets/Prefabs/Game/Puzzle/PuzzleWorldBoard.prefab"));
                board.Draw(state, art);
                object first = Call(board, "EffectAt", 0); object second = Call(board, "EffectAt", 1);
                Sprite rotor = art.Get("PowerBlocks/collection-drone-rotor-4frames-v1");
                for (int frame = 0; frame < 4; frame++)
                {
                    Call(first, "Paint", rotor, new Vector3(frame, frame / 2f, 0), 1f, 30f * frame, true, frame, "Drone-flight");
                    Record("rotor-frame-" + frame, board, level);
                    SpriteRenderer image = (SpriteRenderer)Field(first, "image"); SpriteMask mask = (SpriteMask)Field(first, "mask");
                    Check(image.maskInteraction == SpriteMaskInteraction.VisibleInsideMask && mask.gameObject.activeSelf && image.transform.localPosition == new Vector3(.5f - frame % 2, frame / 2 - .5f, 0), "드론 실제 시트 마스크/프레임 " + frame);
                }
                SpriteRenderer effectImage = (SpriteRenderer)Field(first, "image");
                effectImage.color = Color.red; effectImage.enabled = false;
                Call(first, "Hide"); Check(!((Transform)Field(first, "root")).gameObject.activeSelf, "효과 숨김 반환");
                Call(first, "Paint", art.Get("Effects/Match/Animations/match-pink-frame-01-v1-256"), Vector3.zero, .8f, 0f, false, 0, "Match");
                Check(effectImage.color == Color.white && effectImage.enabled && effectImage.transform.localPosition == Vector3.zero && effectImage.maskInteraction == SpriteMaskInteraction.None && !((SpriteMask)Field(first, "mask")).gameObject.activeSelf, "시트→단일 효과 상태 초기화");
                Check(ReferenceEquals(first, Call(board, "EffectAt", 0)) && ReferenceEquals(second, Call(board, "EffectAt", 1)), "효과 슬롯 객체 재사용");
                Call(board, "HideEffects"); Record("effects-hidden", board, level);
                Type powerType = typeof(PuzzleWorldBoard).Assembly.GetType("GameScreen.PuzzlePowerPlayback");
                Type timelineType = typeof(PuzzleWorldBoard).Assembly.GetType("GameScreen.PuzzleEffectTimeline");
                object power = Activator.CreateInstance(powerType, true);
                foreach (InitialBlockKind kind in new[] { InitialBlockKind.Rocket, InitialBlockKind.Bomb, InitialBlockKind.Drone, InitialBlockKind.Magnet })
                {
                    int[] warm = null;
                    typeof(PowerEffectVerification).GetMethod("Place", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null,
                        new object[] { level, new BoardCoordinate(4, 4), kind, RocketDirection.Horizontal, RabbitColor.Type1 });
                    foreach (bool cancel in new[] { false, true })
                    {
                        LevelRuntimeState powerBefore = Build(level);
                        BoardActionExecutor executor = new BoardActionExecutor(Build(level));
                        BoardActionResult action = executor.Activate(new BoardCoordinate(4, 4));
                        Check(action.IsApplied, "파워 풀 fixture " + kind + "/" + cancel);
                        object timeline = Activator.CreateInstance(timelineType, new object[] { powerBefore, action.Changes, action.Effects, action.PowerTrace });
                        await art.PrepareAsync(executor.State, CancellationToken.None);
                        await (UniTask)Call(power, "PrepareAsync", powerBefore, executor.State, art, timeline, CancellationToken.None);
                        Call(power, "Begin", board); Call(power, "Tick", .1f);
                        Record("power-begin-" + kind + "-" + cancel, board, level);
                        if (!cancel) Check((bool)Call(power, "Tick", 100f), "파워 정상 종료 " + kind);
                        Call(power, "Reset");
                        Check(((IEnumerable)Field(board, "effects")).Cast<object>().All(effect => !((Transform)Field(effect, "root")).gameObject.activeSelf), "파워 종료/취소 효과 반환 " + kind + "/" + cancel);
                        Check(ReferenceEquals(first, Call(board, "EffectAt", 0)), "파워 간 효과 인스턴스 재사용 " + kind + "/" + cancel);
                        int[] current = ((IEnumerable)Field(board, "effects")).Cast<object>().Select(effect => ((Transform)Field(effect, "root")).GetInstanceID()).ToArray();
                        if (cancel) Check(current.SequenceEqual(warm), "동일 파워 두번째 사용 효과 풀 추가 생성0 " + kind);
                        else warm = current;
                        Record("power-returned-" + kind + "-" + cancel, board, level);
                    }
                }
                LevelSupplyEditing.PlaceSources(level, new[] { new BoardCoordinate(0, 4) });
                LevelRuntimeState before = Build(level);
                RuntimeCell vacancy = before.CellAt(new BoardCoordinate(8, 4));
                typeof(RuntimeCell).GetProperty("Content").GetSetMethod(true).Invoke(vacancy, new object[] { RuntimeContent.Empty });
                typeof(RuntimeCell).GetProperty("Color").GetSetMethod(true).Invoke(vacancy, new object[] { null });
                SettlementResult settlement = SettlementResolution.Resolve(before);
                Check(settlement.IsApplied && settlement.Records.Any(record => record.Kind == MovementKind.Supply), "실제 공급 정착 fixture");
                await art.PrepareAsync(settlement.State, CancellationToken.None); board.Draw(before, art);
                SettlementRecord supply = settlement.Records.First(record => record.Kind == MovementKind.Supply);
                object temporary = Call(board, "SupplyImage", supply, settlement.State, art);
                SpriteRenderer supplied = (SpriteRenderer)temporary.GetType().GetField("Renderer", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(temporary);
                int suppliedId = supplied.GetInstanceID(); Record("supply-first", board, level);
                supplied.color = Color.blue; supplied.sortingOrder = 700; supplied.transform.localScale *= 3;
                Call(temporary, "Restore"); Check(!supplied.gameObject.activeSelf && supplied.maskInteraction == SpriteMaskInteraction.None, " 공급 Restore 반환/마스크 해제");
                temporary = Call(board, "SupplyImage", supply, settlement.State, art);
                Check(supplied.GetInstanceID() == suppliedId && supplied.color == Color.white && supplied.sortingOrder == 10 && supplied.maskInteraction == SpriteMaskInteraction.VisibleInsideMask, "공급 재사용 인스턴스/색/order/마스크 초기화");
                Call(temporary, "Hide");
                Type playbackType = typeof(PuzzleWorldBoard).Assembly.GetType("GameScreen.PuzzleBoardSettlementPlayback");
                object playback = Activator.CreateInstance(playbackType, true);
                foreach (bool cancel in new[] { false, true })
                {
                    board.Draw(before, art); object snapshot = Call(board, "Capture");
                    Call(playback, "Begin", snapshot, board, settlement, art, 0, .1f, .2f, .05f);
                    Record("settlement-begin-" + cancel, board, level);
                    if (!cancel)
                    {
                        Check((bool)Call(playback, "Tick", 100f), "정착 재생 종료 신호");
                        Record("settlement-tick-ended-before-session-reset", board, level);
                    }
                    Call(playback, "Reset");
                    Check(((IEnumerable)Field(board, "supplyImages")).Cast<SpriteRenderer>().All(image => !image.gameObject.activeSelf) && ((IEnumerable)Field(board, "supplyClips")).Cast<SpriteMask>().All(mask => !mask.gameObject.activeSelf), "공급 종료/취소 반환 " + cancel);
                    Record("settlement-returned-" + cancel, board, level);
                }
                SpriteRenderer[] children = board.GetComponentsInChildren<SpriteRenderer>(true);
                Sprite clipSprite = (Sprite)Field(board, "supplyClipSprite");
                UnityEngine.Object.Destroy(board.gameObject); board = null; await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate); await UniTask.Yield();
                Check(children.All(image => image == null) && clipSprite == null, "Play Mode 보드 파괴 렌더러/공용 마스크 Sprite 해제");
                Scene ownedScene = SceneManager.CreateScene("EF11-owned-memory-scene");
                board = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<PuzzleWorldBoard>("Assets/Prefabs/Game/Puzzle/PuzzleWorldBoard.prefab"));
                SceneManager.MoveGameObjectToScene(board.gameObject, ownedScene); board.Draw(Build(level), art);
                Call(board, "EffectAt", 0); children = board.GetComponentsInChildren<SpriteRenderer>(true);
                clipSprite = (Sprite)Field(board, "supplyClipSprite");
                Record("scene-before-unload", board, level);
                await SceneManager.UnloadSceneAsync(ownedScene).ToUniTask();
                for (int frame = 0; frame < 5; frame++)
                {
                    File.AppendAllText(Evidence + "/scene-lifetime.csv", frame + "," + (board == null) + "," + children.Count(image => image != null) + "," + (clipSprite == null) + Environment.NewLine);
                    if (board == null && children.All(image => image == null) && clipSprite == null) break;
                    await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate); await UniTask.Yield();
                }
                Check(board == null && children.All(image => image == null) && clipSprite == null, "메모리 씬 Unload 풀 객체/마스크 Sprite 해제");
                Check(art.AtlasCount > 0 && art.Get(PuzzleArtworkPaths.Floor) != null, "씬 객체 해제와 외부 artwork 소유권 분리");
                board = null;
            }
            catch (Exception error) { Results.Add("FAIL " + error); Debug.LogException(error); exit = 1; }
            finally
            {
                if (board != null) UnityEngine.Object.Destroy(board.gameObject);
                if (level != null) UnityEngine.Object.Destroy(level);
                art.Dispose(); File.WriteAllLines(Evidence + "/play-results.txt", Results); File.WriteAllLines(Evidence + "/play-observations.jsonl", Rows);
            }
            EditorApplication.Exit(exit);
        }
    }
}
