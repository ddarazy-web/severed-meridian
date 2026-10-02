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
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.U2D;

namespace GameScreen.Editor
{
    public static partial class PuzzleLevelTransitionVerification
    {
        public static void RunReviewDisable() { SessionState.SetInt("Stage13.Review", 0); RunScene(); }
        public static void RunReviewNotification() { SessionState.SetInt("Stage13.Review", 1); RunScene(); }
        public static void RunReviewMission() { SessionState.SetInt("Stage13.Review", 2); RunScene(); }

        private static async UniTask ReviewChecks(PuzzleGameSession session, int mode)
        {
            string path = "Assets/Stage13-owned-" + Guid.NewGuid().ToString("N") + ".bytes";
            string atlasPath = path.Replace(".bytes", ".spriteatlasv2");
            IResourceLocator[] original = Addressables.ResourceLocators.ToArray();
            AssetDatabaseProvider provider = Addressables.ResourceManager.ResourceProviders.OfType<AssetDatabaseProvider>().First();
            float oldDelay = provider.GetLoadDelay();
            ResourceLocationMap map = new ResourceLocationMap("Stage13-review");
            FixtureLocator locator = new FixtureLocator(original, map);
            LevelDefinition level = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<LevelDefinition>("Assets/Data/Levels/Level_01.asset"));
            List<PuzzleFeedbackCueKind> cues = new List<PuzzleFeedbackCueKind>();
            void Played(PuzzleFeedbackCueKind cue) { cues.Add(cue); }
            bool faulted = false;
            void Fault()
            {
                if (session.State.LevelNumber == 2 && !faulted)
                { faulted = true; throw new InvalidOperationException("Stage13 owned one-shot notification failure"); }
            }
            try
            {
                JsonUtility.FromJsonOverwrite("{\"levelNumber\":2}", level);
                if (mode == 2)
                {
                    JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":1,\"count\":1}]}", level);
                    BoardCoordinate at = new BoardCoordinate(5, 5);
                    LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Block, Erase = true }, new[] { at });
                    LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)ObstacleKind.Crate, Durability = 2 }, new[] { at });
                    // 원본 아틀라스 대신 HUD 아이콘만 빠진 검사 소유 V2 아틀라스를 사용한다.
                    SpriteAtlasAsset asset = new SpriteAtlasAsset();
                    asset.Add(new UnityEngine.Object[] { AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/Obstacles/Crate/crate-durability-2-v1-256.png") });
                    SpriteAtlasAsset.Save(asset, atlasPath); AssetDatabase.ImportAsset(atlasPath, ImportAssetOptions.ForceSynchronousImport);
                    SpriteAtlas atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);
                    SpriteAtlasUtility.PackAtlases(new[] { atlas }, EditorUserBuildSettings.activeBuildTarget, false);
                    Sprite boardSprite = atlas.GetSprite("crate-durability-2-v1-256");
                    Check(boardSprite != null && atlas.GetSprite("crate-durability-1-v1-256") == null, "검사 소유 아틀라스 보드 그림 유효·미션 그림만 누락");
                    UnityEngine.Object.Destroy(boardSprite);
                    map.Add(BoardSpriteAtlas.AddressFor("Obstacles/Crate/"), new ResourceLocationBase("Stage13-review-atlas", atlasPath, provider.ProviderId, typeof(SpriteAtlas)));
                }
                File.WriteAllBytes(path, LevelPackCodec.Encode(new[] { level })); AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                map.Add(LevelPackCodec.Address(2), new ResourceLocationBase("Stage13-review-pack", path, provider.ProviderId, typeof(TextAsset)));
                foreach (IResourceLocator old in original) Addressables.RemoveResourceLocator(old);
                Addressables.AddResourceLocator(locator);
                await WinAt(session, 1);
                object state = session.State, outcome = session.Outcome, bytes = Private(session, "initialBytes");
                PuzzleArtwork art = (PuzzleArtwork)Private(session, "artwork");
                int atlases = art.AtlasCount;
                session.AudioPlayback.Played += Played;
                if (mode == 0)
                {
                    provider.SetLoadDelay(.5f);
                    FieldInfo nextField = typeof(PuzzleResultView).GetField("nextLevel", BindingFlags.Instance | BindingFlags.NonPublic);
                    FieldInfo retryField = typeof(PuzzleResultView).GetField("retry", BindingFlags.Instance | BindingFlags.NonPublic);
                    for (int iteration = 0; iteration < 2; iteration++)
                    {
                        cues.Clear();
                        UniTask<bool> pending = session.AdvanceLevelAsync(CancellationToken.None);
                        Check(session.IsChangingLevel, "실제 로드 중 비활성 경계 " + iteration);
                        if (iteration == 0) session.enabled = false; else session.gameObject.SetActive(false);
                        bool advanced;
                        try { advanced = await pending; }
                        finally { session.gameObject.SetActive(true); session.enabled = true; }
                        Check(!advanced && ReferenceEquals(state, session.State) && ReferenceEquals(outcome, session.Outcome) && ReferenceEquals(bytes, Private(session, "initialBytes")) && art.AtlasCount == atlases && !session.IsChangingLevel && cues.Count == 0,
                            "컴포넌트/오브젝트 비활성 준비취소·승리/Retry/자원 보존·시작음0 " + iteration);
                        await UniTask.Yield();
                        PuzzleResultView popup = UnityEngine.Object.FindFirstObjectByType<PuzzleResultView>();
                        Check(((UnityEngine.UI.Button)nextField.GetValue(popup)).interactable && ((UnityEngine.UI.Button)retryField.GetValue(popup)).interactable,
                            "취소 후 재활성화 실제 Next/Retry 버튼 잠금 해제 " + iteration);
                    }
                    PuzzleResultView result = UnityEngine.Object.FindFirstObjectByType<PuzzleResultView>();
                    UnityEngine.UI.Button retry = (UnityEngine.UI.Button)retryField.GetValue(result);
                    UnityEngine.EventSystems.ExecuteEvents.Execute(retry.gameObject,
                        new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { button = UnityEngine.EventSystems.PointerEventData.InputButton.Left },
                        UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
                    await ReadyOrResult(session, false);
                    Check(session.State.LevelNumber == 1 && !session.HasFailed, "비활성 취소 후 실제 Retry 클릭 기존 판 복구");
                }
                else if (mode == 1)
                {
                    session.Changed += Fault;
                    bool advanced = await session.AdvanceLevelAsync(CancellationToken.None);
                    Check(faulted && advanced && session.State.LevelNumber == 2 && session.Outcome == null && !session.IsChangingLevel && !session.Message.Contains("불러올 수 없습니다"),
                        "교체 후 단발 알림 오류는 성공 상태/반환과 일치·로드 실패로 오표시0");
                    Check(cues.Count == 1 && cues[0] == PuzzleFeedbackCueKind.Start, "알림 오류에도 새 시작음1");
                    session.Changed -= Fault;
                    await ReadyOrResult(session, false);
                    await session.RestartAsync(CancellationToken.None);
                    Check(session.State.LevelNumber == 2 && !session.HasFailed, "알림 오류 후 새 판 Retry 기준 유지");
                }
                else
                {
                    bool advanced = false;
                    try { advanced = await session.AdvanceLevelAsync(CancellationToken.None); }
                    catch (Exception) { }
                    Check(!advanced && ReferenceEquals(state, session.State) && ReferenceEquals(outcome, session.Outcome) && ReferenceEquals(bytes, Private(session, "initialBytes")) && art.AtlasCount == atlases && session.ResultReady && cues.Count == 0,
                        "미션 전용 그림 누락 준비실패·이전 승리/전체Retry/아틀라스 보존·시작음0");
                    await session.RestartAsync(CancellationToken.None);
                    Check(session.State.LevelNumber == 1 && !session.HasFailed, "미션 그림 오류 후 기존 판 Retry 가능");
                }
            }
            finally
            {
                if (session != null)
                { session.gameObject.SetActive(true); session.enabled = true; session.Changed -= Fault; session.AudioPlayback.Played -= Played; }
                provider.SetLoadDelay(oldDelay);
                Addressables.RemoveResourceLocator(locator);
                foreach (IResourceLocator old in original) Addressables.AddResourceLocator(old);
                AssetDatabase.DeleteAsset(path); AssetDatabase.DeleteAsset(atlasPath); UnityEngine.Object.Destroy(level);
                Check(!File.Exists(path) && !File.Exists(path + ".meta") && !File.Exists(atlasPath) && !File.Exists(atlasPath + ".meta") && Addressables.ResourceLocators.SequenceEqual(original), "리뷰 검사 임시팩/아틀라스/locator/delay cleanup 원복");
            }
        }
    }
}
