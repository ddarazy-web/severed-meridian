using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Board;
using Cysharp.Threading.Tasks;
using GameScreen;
using Levels;
using Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Elements.Editor
{
    [InitializeOnLoad]
    public static class ElementSupplyVisualVerification
    {
        private const string PlayKey = "ElementFramework.Phase04.SupplyVisual";
        private static readonly List<string> Results = new List<string>();
        static ElementSupplyVisualVerification()
        {
            EditorApplication.playModeStateChanged += change =>
            {
                if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(PlayKey, false))
                { SessionState.EraseBool(PlayKey); RunAsync().Forget(Debug.LogException); }
            };
        }
        private static void Check(bool pass, string message)
        { if (!pass) throw new InvalidOperationException(message); Results.Add("PASS " + message); }
        private static object Field(object owner, string name) => owner.GetType().GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(owner);
        private static void Invoke(object owner, string name) => owner.GetType().GetMethod(name,
            BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, null);
        public static void Run()
        {
            if (!Application.isBatchMode || EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("별도 배치 Editor와 미저장 씬 보존");
            SessionState.SetBool(PlayKey, true);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }
        private static async UniTask RunAsync()
        {
            Results.Clear(); int exit = 0; LevelDefinition level = null; GameObject boardObject = null; PuzzleArtwork art = null;
            try
            {
                string[] ids = { "fixture.supply.first", "fixture.supply.second" };
                ElementDefinition[] definitions = ids.Select(id => ElementDefinition.CreateSupply(new ElementId(id), id,
                    new ElementSupplyProfile(ElementSupplyBehavior.FixedNormal, RuntimeContent.Normal))).ToArray();
                ElementCatalog rules = new ElementCatalog(LegacyElementDefinitions.DefaultCatalog.Definitions.Concat(definitions));
                ElementLevelSupplyDefinition supply = new ElementLevelSupplyDefinition
                {
                    sources = ids.Select((id, index) => new ElementSupplySourceDefinition
                    {
                        coordinate = new BoardCoordinate(0, index), mode = SupplyMode.Fixed, exhaustion = SupplyExhaustion.Stop,
                        items = new List<ElementSupplyItemDefinition> { new ElementSupplyItemDefinition { definitionId = id, count = 1 } }
                    }).ToList()
                };
                level = ScriptableObject.CreateInstance<LevelDefinition>();
                JsonUtility.FromJsonOverwrite("{\"schemaVersion\":5,\"missions\":[{\"kind\":0,\"count\":20}],\"elementSupply\":" +
                    JsonUtility.ToJson(supply) + "}", level);
                LevelStateBuildResult built = LevelStateBuilder.Build(level, 12345, rules);
                Check(built.IsBuilt, "두 신규 ID 고정 공급 입력 유효 " + string.Join(";", built.Issues));
                foreach (RuntimeCell cell in built.State.Cells)
                    typeof(RuntimeCell).GetProperty("Content").SetValue(cell, RuntimeContent.Empty);
                int random = built.State.Random.DrawCount; string input = JsonUtility.ToJson(level);
                SettlementResult settled = SettlementResolution.Resolve(built.State);
                Check(settled.IsApplied, "실제 공급/낙하 실행 " + settled.Message);
                SettlementRecord[] records = settled.Records.Where(record => record.Kind == MovementKind.Supply).ToArray();
                Check(records.Length == 2 && records.All(record => settled.State.CellAt(record.Target).Content == RuntimeContent.Empty),
                    "생성 지점의 마지막 상태가 비어도 공급 당시 점유자 그림을 보존해야 함");
                string[] paths = { "Obstacles/Crate/crate-durability-1-v1-256", "Obstacles/Scrap/scrap-durability-1-v1-256" };
                ElementVisualCatalog visuals = LegacyElementVisuals.WithOverrides(new ElementVisualCatalogDto
                {
                    definitions = ids.Select((id, index) => new ElementVisualDefinitionDto
                    {
                        key = id, states = new[] { new ElementVisualFrameDto { path = paths[index], size = 1.1f + index * .2f,
                            effectAnimations = ElementVisualVerification.FixtureEffects("legacy.normal"),
                            offsetX = .1f, offsetY = -.15f, angle = 17 + index * 9, order = 13 + index } }
                    }).ToArray(),
                    bindings = ids.Select(id => new ElementVisualBindingDto { id = id, visualKey = id }).ToArray()
                });
                art = new PuzzleArtwork(visuals); await art.PrepareAsync(built.State, CancellationToken.None);
                boardObject = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Game/Puzzle/PuzzleWorldBoard.prefab"));
                PuzzleWorldBoard board = boardObject.GetComponent<PuzzleWorldBoard>();
                MethodInfo create = typeof(PuzzleWorldBoard).GetMethod("SupplyImage", BindingFlags.Instance | BindingFlags.NonPublic);
                int capacity = 0;
                for (int repeat = 0; repeat < 20; repeat++)
                {
                    int index = repeat % 2;
                    object image = create.Invoke(board, new object[] { records[index], settled.State, art });
                    SpriteRenderer renderer = (SpriteRenderer)Field(image, "Renderer");
                    ElementVisualFrame frame = visuals.Get(new ElementId(ids[index])).States[0];
                    Check(renderer.sprite.name == paths[index].Substring(paths[index].LastIndexOf('/') + 1) + "(Clone)",
                        "공급 당시 실제 신규 ID 그림 선택 " + repeat);
                    float size = frame.Size * .92f;
                    Check(Mathf.Approximately(renderer.transform.localScale.x * renderer.sprite.bounds.size.x, size) &&
                        Quaternion.Angle(renderer.transform.localRotation, Quaternion.Euler(0, 0, frame.Angle)) < .01f &&
                        renderer.sortingOrder == frame.Order && Vector3.Distance(renderer.transform.localPosition,
                            PuzzleWorldBoard.CellPosition(records[index].Target) + new Vector3(.1f, -.15f, 0) * size) < .001f,
                        "공급의 등록 크기/회전/order/오프셋 적용 " + repeat);
                    SortingGroup group = renderer.transform.parent.GetComponent<SortingGroup>();
                    Check(group != null && group.sortingOrder == frame.Order && group.transform.localPosition == Vector3.zero &&
                        group.transform.localScale == Vector3.one && group.transform.localRotation == Quaternion.identity,
                        "공급의 실제 합성 그룹에도 등록 order와 기본 변환 적용 " + repeat);
                    SpriteMask mask = (SpriteMask)((IList)Field(board, "supplyClips"))[0];
                    Check(mask.gameObject.activeSelf && mask.sprite != null && renderer.maskInteraction == SpriteMaskInteraction.VisibleInsideMask,
                        "공급 중 전용 마스크 유지 " + repeat);
                    Invoke(image, "ReleaseClip");
                    Check(renderer.enabled && renderer.sprite != null && renderer.maskInteraction == SpriteMaskInteraction.None,
                        "공급 완료의 마스크 반환은 점유자 그림을 숨기지 않음 " + repeat);
                    renderer.color = Color.magenta; renderer.sortingOrder = 999; renderer.flipX = renderer.flipY = true;
                    renderer.transform.localRotation = Quaternion.Euler(0, 0, 91);
                    mask.isCustomRangeActive = true; mask.alphaCutoff = .95f; mask.frontSortingOrder = 500;
                    mask.transform.localRotation = Quaternion.Euler(0, 0, 31);
                    group.sortingOrder = 999; group.transform.localPosition = Vector3.one * 9;
                    group.transform.localScale = Vector3.one * 5; group.transform.localRotation = Quaternion.Euler(0, 0, 31);
                    Invoke(image, repeat % 3 == 0 ? "Hide" : "Restore");
                    Check(!renderer.gameObject.activeSelf && !renderer.enabled && renderer.sprite == null && renderer.color == Color.white &&
                        renderer.sortingOrder == 0 && !renderer.flipX && !renderer.flipY && renderer.transform.localPosition == Vector3.zero &&
                        renderer.transform.localScale == Vector3.one && renderer.transform.localRotation == Quaternion.identity &&
                        !mask.gameObject.activeSelf && mask.sprite == null && !mask.isCustomRangeActive &&
                        Mathf.Approximately(mask.alphaCutoff, .5f) && mask.frontSortingOrder == 0 &&
                        mask.transform.localPosition == Vector3.zero && mask.transform.localRotation == Quaternion.identity,
                        "공급 반환의 Sprite/마스크/변환/order 오염0 " + repeat);
                    Check(group.sortingOrder == 10 && group.transform.localPosition == Vector3.zero &&
                        group.transform.localScale == Vector3.one && group.transform.localRotation == Quaternion.identity,
                        "공급 반환의 합성 그룹 order/변환 오염0 " + repeat);
                    if (repeat == 0) capacity = boardObject.GetComponentsInChildren<Transform>(true).Length;
                    else Check(boardObject.GetComponentsInChildren<Transform>(true).Length == capacity, "공급 슬롯 준비 용량 내 추가 생성0 " + repeat);
                }
                Check(built.State.Random.DrawCount == random && JsonUtility.ToJson(level) == input &&
                    art.Get(paths[0]) != null && art.Get(paths[1]) != null, "표현/반환은 원본/난수와 아틀라스 소유권 무변경");
                board.Draw(settled.State, art);
                RuntimeCell displayed = settled.State.Cells.First(cell => cell.Content == RuntimeContent.Normal);
                SpriteRenderer occupied = board.OccupantAt(displayed.Coordinate);
                PropertyInfo contentElement = typeof(RuntimeCell).GetProperty("ContentElement", BindingFlags.Instance | BindingFlags.NonPublic);
                object selectedElement = contentElement.GetValue(displayed);
                occupied.transform.localScale = Vector3.one * 3; occupied.flipX = occupied.flipY = true;
                occupied.color = Color.magenta; occupied.sortingOrder = 999;
                typeof(RuntimeCell).GetProperty("Content").SetValue(displayed, RuntimeContent.Empty);
                board.Draw(settled.State, art);
                Check(occupied.sprite == null && !occupied.enabled && occupied.color == Color.white && occupied.sortingOrder == 0 &&
                    occupied.transform.localScale == Vector3.one && !occupied.flipX && !occupied.flipY &&
                    occupied.maskInteraction == SpriteMaskInteraction.None && occupied.transform.localRotation == Quaternion.identity,
                    "월드 점유 슬롯이 빈칸으로 반환될 때 축척/뒤집기/표시 오염0");
                typeof(RuntimeCell).GetProperty("Content").SetValue(displayed, RuntimeContent.Normal);
                contentElement.SetValue(displayed, selectedElement); board.Draw(settled.State, art);
                object snapshot = typeof(PuzzleWorldBoard).GetMethod("Capture", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(board, null);
                Type removalType = typeof(PuzzleWorldBoard).Assembly.GetType("GameScreen.PuzzleBoardRemovalPlayback");
                object removal = Activator.CreateInstance(removalType, true);
                removalType.GetMethod("Begin", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(removal,
                    new object[] { snapshot, settled.State, art, Array.Empty<MatchedBlockChange>(), .12f });
                Check(!(bool)removalType.GetProperty("IsPlaying", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(removal),
                    "제거 연출은 같은 실제 ID 그림을 구형 일반 그림으로 비교해 오인하지 않음");
                Invoke(removal, "Reset");
                SpriteMask ownedMask = (SpriteMask)((IList)Field(board, "supplyClips"))[0];
                UnityEngine.Object.Destroy(boardObject); boardObject = null; await UniTask.Yield(); await UniTask.Yield();
                Check(ownedMask == null && art.Get(paths[0]) != null, "보드 Destroy의 공급 마스크 파기와 아트 소유권 유지");
            }
            catch (Exception error) { Results.Add("FAIL " + error); Debug.LogException(error); exit = 1; }
            finally
            {
                if (boardObject != null) UnityEngine.Object.DestroyImmediate(boardObject);
                if (level != null) UnityEngine.Object.DestroyImmediate(level);
                art?.Dispose();
                Directory.CreateDirectory("Logs/ElementFramework/Phase04");
                File.WriteAllLines("Logs/ElementFramework/Phase04/supply-visual-results.txt", Results);
                EditorApplication.Exit(exit);
            }
        }
    }
}
