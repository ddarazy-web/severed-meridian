using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Board;
using Cysharp.Threading.Tasks;
using Levels;
using Simulation;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace GameScreen.Editor
{
    public static partial class PuzzleLevelTransitionVerification
    {
        private sealed class FixtureLocator : IResourceLocator
        {
            public string LocatorId => "Stage13-owned-fixture";
            public IEnumerable<object> Keys => original.SelectMany(locator => locator.Keys).Concat(map.Keys).Distinct();
            public IEnumerable<IResourceLocation> AllLocations => original.SelectMany(locator => locator.AllLocations).Concat(map.AllLocations).Distinct();
            private readonly IResourceLocator[] original;
            private readonly ResourceLocationMap map;
            public FixtureLocator(IResourceLocator[] source, ResourceLocationMap replacements)
            { original = source; map = replacements; }
            public bool Locate(object key, Type type, out IList<IResourceLocation> locations)
            {
                if (map.Locate(key, type, out locations)) return true;
                foreach (IResourceLocator locator in original)
                    if (locator.Locate(key, type, out locations)) return true;
                locations = null; return false;
            }
        }

        private static async UniTask SuccessChecks(PuzzleGameSession session)
        {
            string path = "Assets/Stage13-owned-" + Guid.NewGuid().ToString("N") + ".bytes";
            IResourceLocator[] original = Addressables.ResourceLocators.ToArray();
            FixtureLocator locator = null;
            AssetDatabaseProvider provider = Addressables.ResourceManager.ResourceProviders.OfType<AssetDatabaseProvider>().FirstOrDefault();
            bool ownsProvider = provider == null;
            if (ownsProvider) { provider = new AssetDatabaseProvider(); Addressables.ResourceManager.ResourceProviders.Add(provider); }
            LevelDefinition level = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<LevelDefinition>("Assets/Data/Levels/Level_01.asset"));
            List<PuzzleFeedbackCueKind> cues = new List<PuzzleFeedbackCueKind>();
            void Played(PuzzleFeedbackCueKind cue) { cues.Add(cue); }
            try
            {
                JsonUtility.FromJsonOverwrite("{\"levelNumber\":2}", level);
                BoardCoordinate anchor = new BoardCoordinate(6, 0);
                Levels.Editor.LevelObstacleEditing.Apply(level, new Levels.Editor.PlacementBrush { Layer = Levels.PlacementLayer.Block, Erase = true }, Levels.LevelPlacementRules.Footprint(anchor, 2));
                Levels.Editor.LevelObstacleEditing.Apply(level, new Levels.Editor.PlacementBrush { Layer = Levels.PlacementLayer.Obstacle, Kind = (int)ObstacleKind.Appliance, Durability = 9 }, new[] { anchor });
                File.WriteAllBytes(path, LevelPackCodec.Encode(new[] { level }));
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                ResourceLocationMap map = new ResourceLocationMap("Stage13-test-pack");
                map.Add(LevelPackCodec.Address(2), new ResourceLocationBase("Stage13-test-pack", path, provider.ProviderId, typeof(TextAsset)));
                locator = new FixtureLocator(original, map);
                foreach (IResourceLocator old in original) Addressables.RemoveResourceLocator(old);
                Addressables.AddResourceLocator(locator);
                FieldInfo artworkField = typeof(PuzzleGameSession).GetField("artwork", BindingFlags.NonPublic | BindingFlags.Instance);
                PuzzleArtwork previous = (PuzzleArtwork)artworkField.GetValue(session);
                PuzzleWorldBoard board = UnityEngine.Object.FindFirstObjectByType<PuzzleWorldBoard>();
                int[] cellsBefore = ((System.Collections.IEnumerable)typeof(PuzzleWorldBoard).GetField("cells", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(board))
                    .Cast<PuzzleCellView>().Select(cell => cell.GetInstanceID()).ToArray();
                session.AudioPlayback.Played += Played;
                int seed = (int)typeof(PuzzleGameSession).GetField("seed", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(session);
                StartingBoardSearch expected = new StartingBoardSearch(level, seed);
                while (!expected.IsDone) expected.Advance(128);
                PuzzleResultView popup = UnityEngine.Object.FindFirstObjectByType<PuzzleResultView>();
                string previousLogicalSession = session.LogicalSessionId;
                UnityEngine.UI.Button next = (UnityEngine.UI.Button)typeof(PuzzleResultView).GetField("nextLevel", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(popup);
                PointerEventData pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
                ExecuteEvents.Execute(next.gameObject, pointer, ExecuteEvents.pointerClickHandler);
                Check(session.IsChangingLevel, "실제 EventSystem 다음 버튼 전환 시작");
                ExecuteEvents.Execute(next.gameObject, pointer, ExecuteEvents.pointerClickHandler);
                MethodInfo background = typeof(PuzzleGameSession).GetMethod("OnApplicationPause", BindingFlags.NonPublic | BindingFlags.Instance);
                background.Invoke(session, new object[] { true });
                try
                {
                    await CapturePopups("loading");
                    Check(session.IsChangingLevel && session.ResultReady && session.State.LevelNumber == 1, "준비 중 background·네 해상도 회전 기존 승리 보존");
                }
                finally { background.Invoke(session, new object[] { false }); }
                float transitionDeadline = Time.realtimeSinceStartup + 45;
                while (session.IsChangingLevel && Time.realtimeSinceStartup < transitionDeadline) await UniTask.Yield();
                Check(session.State.LevelNumber == 2 && !session.HasFailed, "검사 소유 팩 실제 버튼 Addressables 성공·중복 클릭 전환1회");
                Check(session.LogicalSessionId != previousLogicalSession && !string.IsNullOrEmpty(session.LogicalSessionId),
                    "Next 성공에만 새 논리 문맥 확정");
                Check(session.State.LevelNumber == 2 && session.Outcome == null && session.IsStartingFeedback && !session.IsChangingLevel,
                    "새 번호·승리 해제·시작 피드백 전환");
                session.AudioPlayback.Played -= Played;
                Check(cues.Count == 1 && cues[0] == PuzzleFeedbackCueKind.Start && ((PuzzleFeedbackSchedule)Private(session, "audioSchedule")).PendingCount == 0,
                    "실제 중복 클릭·background 복귀 전환 시작음1·이전 결과음/예약0");
                Check(cellsBefore.SequenceEqual(((System.Collections.IEnumerable)typeof(PuzzleWorldBoard).GetField("cells", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(board)).Cast<PuzzleCellView>().Select(cell => cell.GetInstanceID())),
                    "레벨 전환 81칸 기존 풀 인스턴스 재사용");
                PuzzleHudView hud = UnityEngine.Object.FindFirstObjectByType<PuzzleHudView>();
                UnityEngine.UI.Text moves = (UnityEngine.UI.Text)typeof(PuzzleHudView).GetField("moves", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hud);
                PuzzleMissionView[] missions = hud.GetComponentsInChildren<PuzzleMissionView>();
                Check(moves.text == session.State.MovesRemaining.ToString() && missions.Length == session.State.Missions.Count && missions.Select((mission, index) =>
                    ((UnityEngine.UI.Text)typeof(PuzzleMissionView).GetField("count", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(mission)).text == "0/" + session.State.Missions[index].Target).All(value => value),
                    "새 판 실제 HUD 이동·미션 초기 수치 갱신");
                string initial = (string)typeof(Levels.Editor.LevelInitialStateVerification).GetMethod("Snapshot", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { session.State });
                string expectedState = (string)typeof(Levels.Editor.LevelInitialStateVerification).GetMethod("Snapshot", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { expected.State });
                Check(initial == expectedState, "새 레벨 전체 시작 상태·동일 시드 일치");
                Check(previous.AtlasCount == 0 && ((PuzzleArtwork)artworkField.GetValue(session)).AtlasCount > 0, "이전 아틀라스 반환·새 아틀라스 유지");
                System.Collections.IDictionary loaded = (System.Collections.IDictionary)typeof(PuzzleArtwork).GetField("atlases", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(artworkField.GetValue(session));
                Check(session.State.Obstacles.Any(body => body.Definition.Kind == ObstacleKind.Appliance) && new System.Collections.Generic.HashSet<string>(loaded.Keys.Cast<string>()).SetEquals(new[]
                    { BoardSpriteAtlas.AddressFor("Blocks/"), BoardSpriteAtlas.AddressFor("PowerBlocks/"), BoardSpriteAtlas.AddressFor(PuzzleArtworkPaths.Floor), BoardSpriteAtlas.AddressFor("Obstacles/MetalRodBox/"),
                        BoardSpriteAtlas.AddressFor("Effects/Match/"), BoardSpriteAtlas.AddressFor("Effects/PowerCreation/"),
                        BoardSpriteAtlas.AddressFor("Effects/Rocket/"), BoardSpriteAtlas.AddressFor("Effects/BombExplosion/"),
                        BoardSpriteAtlas.AddressFor("Effects/Drone/"), BoardSpriteAtlas.AddressFor("Effects/Magnet/"), BoardSpriteAtlas.AddressFor("Effects/MetalBreak/") }),
                    "새 맵 장애물과 도달 가능한 매칭/생성/파워/피해 효과의 필요 아틀라스만 준비: " + string.Join(",", loaded.Keys.Cast<string>().OrderBy(value => value)));
                float deadline = Time.realtimeSinceStartup + 45;
                while (!session.CanAcceptInput && !session.HasFailed && Time.realtimeSinceStartup < deadline) await UniTask.Yield();
                Check(session.CanAcceptInput, "새 판 시작 후 입력 허용");
                SpriteRenderer[] temporary = board.GetComponentsInChildren<SpriteRenderer>(true)
                    .Where(image => image.name == "Supply-playback" || image.name == "Effect-playback").ToArray();
                Check(temporary.All(image => !image.enabled && image.sprite == null && image.color == Color.white &&
                    !image.flipX && !image.flipY && image.sortingOrder == 0 && image.maskInteraction == SpriteMaskInteraction.None &&
                    image.transform.localPosition == Vector3.zero && image.transform.localScale == Vector3.one && image.transform.localRotation == Quaternion.identity),
                    "실제 다음 레벨 전환의 이전 공급/효과 표시 참조/변환/order 잔류0");
                Check(board.GetComponentsInChildren<SpriteMask>(true).All(mask => !mask.gameObject.activeSelf && mask.sprite == null),
                    "실제 다음 레벨 전환의 이전 공급/효과 마스크 잔류0");
                Check(Private(session, "effectLoad") == null && Private(session, "powerPlayback") == null && !session.IsPresenting,
                    "실제 다음 레벨 전환의 이전 준비 콜백/파워 소유자 잔류0");
                int[] capacity = board.GetComponentsInChildren<Transform>(true).Select(value => value.GetInstanceID()).ToArray();
                for (int repeat = 0; repeat < 3; repeat++)
                {
                    await session.RestartAsync(CancellationToken.None); await ReadyOrResult(session, false);
                    Check(capacity.SequenceEqual(board.GetComponentsInChildren<Transform>(true).Select(value => value.GetInstanceID())),
                        "전환 후 같은 레벨 3회 재시작의 준비 용량 안 추가 생성0 " + repeat);
                    Check(initial == (string)typeof(Levels.Editor.LevelInitialStateVerification).GetMethod("Snapshot", BindingFlags.Static | BindingFlags.NonPublic)
                        .Invoke(null, new object[] { session.State }), "전환 후 재시작 전체 논리 상태 보존 " + repeat);
                }
                string imagePath = Output + "next-level-gameplay.png";
                if (File.Exists(imagePath)) File.Delete(imagePath);
                ScreenCapture.CaptureScreenshot(imagePath);
                deadline = Time.realtimeSinceStartup + 10;
                while ((!File.Exists(imagePath) || new FileInfo(imagePath).Length < 1000) && Time.realtimeSinceStartup < deadline) await UniTask.Yield();
                Texture2D image = new Texture2D(2, 2);
                try
                {
                    Check(File.Exists(imagePath) && image.LoadImage(File.ReadAllBytes(imagePath)) && image.width == Screen.width && image.height == Screen.height,
                        "새 레벨 실제 게임 보드·HUD PNG 저장·디코딩");
                }
                finally { UnityEngine.Object.Destroy(image); }
                BoardCoordinate target = session.State.Cells.First(cell => cell.Content == Simulation.RuntimeContent.Normal).Coordinate;
                Check(session.TryUseItem(BoardItem.Hammer, target), "새 판 실제 한 행동");
                deadline = Time.realtimeSinceStartup + 45;
                while (!session.CanAcceptInput && session.Outcome == null && !session.HasFailed && Time.realtimeSinceStartup < deadline) await UniTask.Yield();
                await session.RestartAsync(CancellationToken.None);
                string retried = (string)typeof(Levels.Editor.LevelInitialStateVerification).GetMethod("Snapshot", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { session.State });
                Check(session.State.LevelNumber == 2 && retried == initial, "새 레벨 Retry 번호·전체 초기 상태 보존");
            }
            finally
            {
                session.AudioPlayback.Played -= Played;
                if (locator != null)
                {
                    Addressables.RemoveResourceLocator(locator);
                    foreach (IResourceLocator old in original) Addressables.AddResourceLocator(old);
                }
                if (ownsProvider) Addressables.ResourceManager.ResourceProviders.Remove(provider);
                AssetDatabase.DeleteAsset(path); UnityEngine.Object.Destroy(level);
                Check(!File.Exists(path) && !File.Exists(path + ".meta") && Addressables.ResourceLocators.SequenceEqual(original),
                    "검사 소유 팩·locator cleanup 원복");
            }
        }
    }
}
