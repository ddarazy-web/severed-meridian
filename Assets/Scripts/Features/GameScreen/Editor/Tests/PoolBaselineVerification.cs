using System;
using System.Collections;
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
using UnityEngine.Rendering;

namespace GameScreen.Editor
{
    public static partial class PoolBaselineVerification
    {
        private const string Evidence = "Logs/ElementFramework/Stage11";
        private static readonly List<string> Results = new List<string>();
        private static readonly List<string> Rows = new List<string>();
        private static object Field(object owner, string name) => owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
        private static object Call(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, args);
        private static void Check(bool value, string name) { if (!value) throw new InvalidOperationException(name); Results.Add("PASS " + name); }
        private static int[] Ids(PuzzleWorldBoard board, string name) => ((IEnumerable)Field(board, name)).Cast<UnityEngine.Object>().Select(value => value.GetInstanceID()).ToArray();
        private static LevelRuntimeState Build(LevelDefinition level)
        {
            LevelStateBuildResult result = LevelStateBuilder.Build(level, 12345);
            if (!result.IsBuilt) throw new InvalidOperationException(string.Join(" | ", result.Issues));
            return result.State;
        }
        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 Editor에서만 실행한다.");
            RunAsync().Forget(error => { Debug.LogException(error); EditorApplication.Exit(1); });
        }
        private static async UniTask RunAsync()
        {
            Directory.CreateDirectory(Evidence); int exit = 0;
            PuzzleWorldBoard board = null; LevelDefinition small = null, large = null, tiny = null;
            PuzzleArtwork art = new PuzzleArtwork();
            try
            {
                small = (LevelDefinition)typeof(Levels.Editor.PowerEffectVerification).GetMethod("Make", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
                large = (LevelDefinition)typeof(Levels.Editor.GeneratorVerification).GetMethod("Make", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { ObstacleKind.Crate, 3 });
                LevelRuntimeState a = Build(small), b = Build(large);
                await art.PrepareAsync(a, CancellationToken.None); await art.PrepareAsync(b, CancellationToken.None);
                board = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<PuzzleWorldBoard>("Assets/Prefabs/Game/Puzzle/PuzzleWorldBoard.prefab"));
                board.Draw(a, art); Record("small-first", board, small);
                int[] cells = Ids(board, "cells");
                for (int i = 0; i < 10; i++)
                {
                    board.Draw(a, art); Check(Ids(board, "cells").SequenceEqual(cells), "셀 반복 재사용 " + i);
                }
                tiny = UnityEngine.Object.Instantiate(small);
                BoardCoordinate[] inactive = a.Cells.Where(cell => cell.Coordinate.Row >= 3 || cell.Coordinate.Column >= 3).Select(cell => cell.Coordinate).ToArray();
                Levels.Editor.LevelBoardEditing.Apply(tiny, Levels.Editor.LevelBrush.Erase, RabbitColor.Type1, inactive);
                Levels.Editor.LevelBoardEditing.Apply(tiny, Levels.Editor.LevelBrush.Deactivate, RabbitColor.Type1, inactive);
                board.Draw(Build(tiny), art); Record("nine-active-cells", board, tiny);
                Check(((IEnumerable)Field(board, "cells")).Cast<PuzzleCellView>().Count(cell => cell.gameObject.activeSelf) == 9 && Ids(board, "cells").SequenceEqual(cells), "실제 활성9/풀81 인스턴스 유지");
                board.Draw(a, art); Record("reactivated-81-cells", board, small);
                Check(((IEnumerable)Field(board, "cells")).Cast<PuzzleCellView>().All(cell => cell.gameObject.activeSelf), "셀81 재활성화");
                BoardCoordinate at = new BoardCoordinate(0, 0);
                SpriteRenderer selected = board.OccupantAt(at); Vector3 origin = selected.transform.localPosition;
                int order = selected.sortingOrder;
                board.Preview(at, new Vector3(.3f, .2f, 0));
                Check(selected.sortingOrder == 50 && selected.transform.localPosition != origin, "선택 order/위치 변경");
                board.ClearPreview(); Check(selected.sortingOrder == order && selected.transform.localPosition == origin, "선택 종료 원래 order/위치");
                object snapshot = Call(board, "Capture");
                Color color = selected.color; Vector3 scale = selected.transform.localScale;
                selected.color = Color.magenta; selected.sortingOrder = 777; selected.transform.localScale *= 2;
                Call(snapshot, "Swap", at, new BoardCoordinate(0, 1)); Call(snapshot, "Restore");
                Check(selected.color == color && selected.sortingOrder == order && selected.transform.localScale == scale && selected.transform.localPosition == origin, "스냅샷 색/order/크기/위치 복원");
                selected.color = Color.blue; selected.sortingOrder = 701; selected.transform.localRotation = Quaternion.Euler(0, 0, 45);
                selected.transform.localScale *= 3; selected.transform.localPosition += Vector3.one;
                Record("dirty-before-draw", board, small); board.Draw(a, art); Record("dirty-after-draw", board, small);
                Check(selected.color == Color.blue && selected.sortingOrder == 701 && selected.transform.localRotation == Quaternion.Euler(0, 0, 45), "직접 Draw는 색/order/회전을 초기화하지 않는 현재 기준");
                Check(selected.transform.localScale == scale && selected.transform.localPosition == origin, "직접 Draw는 크기/위치를 복원");
                selected.color = color; selected.sortingOrder = order; selected.transform.localRotation = Quaternion.identity;
                board.Draw(b, art); Record("large-first", board, large);
                int[] bodies = Ids(board, "bodies"), decorations = Ids(board, "decorations");
                Check(bodies.Length > 0 && decorations.Length > 0, "실제 장애물/장식 생성");
                for (int i = 0; i < 5; i++)
                {
                    board.Draw(a, art); Record("small-after-large-" + i, board, small);
                    Check(Ids(board, "bodies").SequenceEqual(bodies) && Ids(board, "decorations").SequenceEqual(decorations), "작은 판 용량/인스턴스 유지 " + i);
                    Check(((IEnumerable)Field(board, "bodies")).Cast<SpriteRenderer>().All(image => !image.gameObject.activeSelf) && ((IEnumerable)Field(board, "decorations")).Cast<SpriteRenderer>().All(image => !image.gameObject.activeSelf), "작은 판 잔여 장애물/장식 숨김 " + i);
                    board.Draw(b, art); Record("large-reuse-" + i, board, large);
                    Check(Ids(board, "cells").SequenceEqual(cells) && Ids(board, "bodies").SequenceEqual(bodies) && Ids(board, "decorations").SequenceEqual(decorations), "큰 판 다시 동일 인스턴스 " + i);
                }
                SpriteRenderer[] children = board.GetComponentsInChildren<SpriteRenderer>(true);
                UnityEngine.Object.DestroyImmediate(board.gameObject); board = null;
                Check(children.All(image => image == null), "소유 보드 파괴 자식 렌더러 해제");
            }
            catch (Exception error) { Results.Add("FAIL " + error); Debug.LogException(error); exit = 1; }
            finally
            {
                if (board != null) UnityEngine.Object.DestroyImmediate(board.gameObject);
                if (small != null) UnityEngine.Object.DestroyImmediate(small);
                if (large != null) UnityEngine.Object.DestroyImmediate(large);
                if (tiny != null) UnityEngine.Object.DestroyImmediate(tiny);
                art.Dispose(); File.WriteAllLines(Evidence + "/board-results.txt", Results); File.WriteAllLines(Evidence + "/board-observations.jsonl", Rows);
            }
            EditorApplication.Exit(exit);
        }
        [Serializable] private sealed class Observation
        {
            public string name, levelJson, runtimeMutation;
            public int seed = 12345;
            public int[] cells, bodies, decorations;
            public int[] supply, clips, effects;
            public bool[] clipActive, effectActive;
            public RendererState[] renderers;
        }
        [Serializable] private sealed class RendererState
        {
            public int id, order, groupOrder;
            public string name, sprite;
            public bool active, enabled;
            public Vector3 position, worldPosition, scale;
            public Quaternion rotation;
            public Color color;
        }
        private static void Record(string name, PuzzleWorldBoard board, LevelDefinition level)
            => Rows.Add(JsonUtility.ToJson(new Observation { name = name, levelJson = JsonUtility.ToJson(level), cells = Ids(board, "cells"), bodies = Ids(board, "bodies"), decorations = Ids(board, "decorations"),
                runtimeMutation = name.StartsWith("supply") || name.StartsWith("settlement") ? "Build(level,12345); clear runtime cell(8,4) Content/Color; SettlementResolution.Resolve; SupplyImage/Begin use resulting records" : null,
                supply = Ids(board, "supplyImages"), clips = Ids(board, "supplyClips"), effects = ((IEnumerable)Field(board, "effects")).Cast<object>().Select(effect => ((Transform)Field(effect, "root")).GetInstanceID()).ToArray(),
                clipActive = ((IEnumerable)Field(board, "supplyClips")).Cast<SpriteMask>().Select(mask => mask.gameObject.activeInHierarchy).ToArray(),
                effectActive = ((IEnumerable)Field(board, "effects")).Cast<object>().Select(effect => ((Transform)Field(effect, "root")).gameObject.activeInHierarchy).ToArray(),
                renderers = board.GetComponentsInChildren<SpriteRenderer>(true).Select(image => new RendererState { id = image.GetInstanceID(), name = image.name, order = image.sortingOrder, groupOrder = image.GetComponentInParent<SortingGroup>()?.sortingOrder ?? image.sortingOrder, sprite = image.sprite == null ? null : image.sprite.name,
                    active = image.gameObject.activeInHierarchy, enabled = image.enabled, position = image.transform.localPosition, worldPosition = image.transform.position, scale = image.transform.localScale, rotation = image.transform.localRotation, color = image.color }).ToArray() }));
    }
}
