using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Board;
using Simulation;
using UnityEditor;
using UnityEngine;

namespace Levels.Editor
{
    // 소유한 메모리 정의에서 규칙 정답을 확인한다. 사용자 에셋은 수정하지 않는다.
    public static partial class StartingBoardVerification
    {
        private const string Evidence = "Logs/StartingBoardVerification";
        private static readonly List<string> Results = new List<string>();
        private static readonly List<LevelDefinition> Definitions = new List<LevelDefinition>();
        private static BoardCoordinate C(int row, int column) => new BoardCoordinate(row, column);
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            Results.Add("PASS " + message);
        }
        private static string Snapshot(object value) => (string)typeof(LevelInitialStateVerification)
            .GetMethod("Snapshot", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new[] { value });
        private static void Set(RuntimeCell cell, string property, object value) => typeof(RuntimeCell)
            .GetProperty(property).GetSetMethod(true).Invoke(cell, new[] { value });
        private static LevelDefinition Definition(string json = null)
        {
            LevelDefinition level = ScriptableObject.CreateInstance<LevelDefinition>();
            Definitions.Add(level);
            JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":0,\"color\":0,\"count\":12}]}", level);
            if (json != null) JsonUtility.FromJsonOverwrite(json, level);
            return level;
        }
        private static LevelRuntimeState Build(LevelDefinition level)
        {
            LevelStateBuildResult built = LevelStateBuilder.Build(level, 12345);
            if (!built.IsBuilt) throw new InvalidOperationException(string.Join(" | ", built.Issues));
            return built.State;
        }
        private static LevelRuntimeState Empty()
        {
            LevelRuntimeState state = Build(Definition());
            foreach (RuntimeCell cell in state.Cells)
            { Set(cell, "Content", RuntimeContent.Empty); Set(cell, "Color", null); }
            return state;
        }
        private static void Normal(LevelRuntimeState state, BoardCoordinate cell, int color = 0)
        { Set(state.CellAt(cell), "Content", RuntimeContent.Normal); Set(state.CellAt(cell), "Color", (RabbitColor)color); }
        private static void Power(LevelRuntimeState state, BoardCoordinate cell, RuntimeContent kind)
        { Set(state.CellAt(cell), "Content", kind); Set(state.CellAt(cell), "Color", null); }

        public static void Data()
        {
            Directory.CreateDirectory(Evidence);
            Results.Clear();
            try
            {
                Patterns(); Actions(); Starts(); Edges();
                File.WriteAllLines(Evidence + "/data-results.txt", Results);
                Debug.Log("[StartingBoard] PASS " + Results.Count);
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Results.Add("FAIL " + error);
                File.WriteAllLines(Evidence + "/data-results.txt", Results);
                Debug.LogException(error); EditorApplication.Exit(1);
            }
            finally
            {
                foreach (LevelDefinition definition in Definitions) UnityEngine.Object.DestroyImmediate(definition);
                Definitions.Clear();
            }
        }

        private static void Patterns()
        {
            foreach (int axis in new[] { 0, 1 })
                foreach (int length in new[] { 3, 4, 5, 6, 10 })
                {
                    LevelRuntimeState state = Empty();
                    BoardCoordinate[] cells = Enumerable.Range(0, length).Select(i => C(axis == 0 ? 0 : i, axis == 0 ? i : 0)).ToArray();
                    foreach (BoardCoordinate cell in cells) Normal(state, cell);
                    MatchPattern match = MatchQuery.Find(state).Single();
                    Check(match.Cells.SequenceEqual(cells) && match.Kind == (length == 3 ? MatchKind.Three : length == 4 ? MatchKind.Rocket : MatchKind.Magnet), $"직선 {axis}/{length} 좌표와 단일 패턴");
                    if (length == 4) Check(match.RocketDirection == (axis == 0 ? RocketDirection.Vertical : RocketDirection.Horizontal), "로켓 반대 축");
                }
            // 손으로 지정한 모서리 네 방향과 T 네 방향. 조회 구현의 회전 함수를 사용하지 않는다.
            string[] shapes = { "00 01 02 12 22", "02 12 20 21 22", "00 10 20 21 22", "00 01 02 10 20",
                "00 01 02 11 21", "02 11 12 22 10", "01 11 20 21 22", "00 10 11 12 20" };
            foreach (string shape in shapes)
            {
                LevelRuntimeState state = Empty();
                BoardCoordinate[] cells = shape.Split(' ').Select(s => C(s[0] - '0', s[1] - '0')).OrderBy(c => c.Row).ThenBy(c => c.Column).ToArray();
                foreach (BoardCoordinate cell in cells) Normal(state, cell);
                Check(MatchQuery.Find(state).Single(m => m.Kind == MatchKind.Bomb).Cells.SequenceEqual(cells), "폭탄 방향 " + shape);
            }
            LevelRuntimeState square = Empty();
            foreach (BoardCoordinate cell in new[] { C(0, 0), C(0, 1), C(1, 0), C(1, 1) }) Normal(square, cell);
            Check(MatchQuery.Find(square).Single().Kind == MatchKind.Drone, "2x2 단독 매칭");
            Normal(square, C(0, 2));
            Check(MatchQuery.Find(square).Count == 2, "2x2 추가 이웃도 독립 패턴 유지");
            Normal(square, C(0, 3)); Normal(square, C(0, 4));
            Check(MatchQuery.Find(square).Select(m => m.Kind).SequenceEqual(new[] { MatchKind.Magnet, MatchKind.Drone }), "자석과 드론 겹침 좌표 별도 보존");
            Set(square.CellAt(C(0, 0)), "Cover", CoverKind.Web);
            Check(MatchQuery.Find(square).Count == 2, "거미줄 매칭 참여");
            Set(square.CellAt(C(0, 0)), "Cover", CoverKind.Mold);
            Check(MatchQuery.Find(square).Single().Kind == MatchKind.Rocket, "곰팡이 연결 제외");
            Power(square, C(0, 2), RuntimeContent.Bomb);
            Check(MatchQuery.Find(square).Count == 0, "파워는 일반 색 연결에서 제외");
            foreach (BoardEdge wall in new[] { new BoardEdge(C(0, 0), C(0, 1)), new BoardEdge(C(0, 0), C(1, 0)), new BoardEdge(C(0, 1), C(1, 1)), new BoardEdge(C(1, 0), C(1, 1)) })
            {
                LevelDefinition level = Definition(); LevelFlowEditing.SetWalls(level, new[] { wall }, false);
                LevelRuntimeState state = Build(level);
                foreach (RuntimeCell cell in state.Cells) { Set(cell, "Content", RuntimeContent.Empty); Set(cell, "Color", null); }
                foreach (BoardCoordinate cell in new[] { C(0, 0), C(0, 1), C(1, 0), C(1, 1) }) Normal(state, cell);
                Check(MatchQuery.Find(state).Count == 0, "2x2 내부 벽 " + wall);
                Check(!ActionQuery.Swap(state, wall.A, wall.B).IsAllowed, "벽 너머 교환 금지");
            }
        }

        private static void Actions()
        {
            LevelRuntimeState state = Empty();
            Normal(state, C(0, 0)); Normal(state, C(0, 1), 1); Normal(state, C(0, 2)); Normal(state, C(1, 1));
            Check(ActionQuery.Swap(state, C(1, 1), C(0, 1)).IsAllowed, "일반 교환 새 직선 매칭");
            Check(!ActionQuery.Swap(state, C(0, 0), C(0, 2)).IsAllowed && !ActionQuery.Swap(state, C(0, 0), C(1, 0)).IsAllowed, "비인접과 빈칸 거절");
            string before = Snapshot(state), first = Snapshot(ActionQuery.Find(state));
            for (int i = 0; i < 5; i++) Check(Snapshot(ActionQuery.Find(state)) == first && Snapshot(state) == before, "반복 조회 순서/전체 상태/난수 불변 " + i);
            Check(new StartConditionReport(state).IsSatisfied, "매칭 없음+교환 있음 시작 통과");
            Set(state.CellAt(C(1, 1)), "Cover", CoverKind.Web);
            Check(!ActionQuery.Swap(state, C(1, 1), C(0, 1)).IsAllowed, "거미줄 이동 금지");
            Set(state.CellAt(C(1, 1)), "Cover", null); Set(state.CellAt(C(1, 1)), "DustDurability", 3);
            Check(ActionQuery.Swap(state, C(1, 1), C(0, 1)).IsAllowed && state.CellAt(C(1, 1)).DustDurability == 3, "먼지 이동 허용/미손상");
            LevelRuntimeState onlySquare = Empty();
            foreach (BoardCoordinate cell in new[] { C(0, 0), C(1, 0), C(1, 1), C(0, 2) }) Normal(onlySquare, cell);
            Normal(onlySquare, C(0, 1), 1);
            ActionCandidate square = ActionQuery.Swap(onlySquare, C(0, 1), C(0, 2));
            Check(square.IsAllowed && square.Matches.Single().Kind == MatchKind.Drone, "2x2만 완성한 교환 허용");
            Normal(onlySquare, C(5, 0)); Normal(onlySquare, C(5, 1)); Normal(onlySquare, C(5, 2));
            Check(!ActionQuery.Swap(onlySquare, C(1, 0), C(1, 1)).IsAllowed, "기존 다른 매칭/같은 색 교환으로 허용하지 않음");
            RuntimeContent[] powers = { RuntimeContent.Rocket, RuntimeContent.Bomb, RuntimeContent.Drone, RuntimeContent.Magnet };
            for (int a = 0; a < powers.Length; a++) for (int b = a; b < powers.Length; b++)
            {
                LevelRuntimeState pair = Empty(); Power(pair, C(0, 0), powers[a]); Power(pair, C(0, 1), powers[b]); Normal(pair, C(5, 5));
                Check(ActionQuery.Swap(pair, C(0, 0), C(0, 1)).Kind == QueryActionKind.SwapCombination && ActionQuery.Swap(pair, C(0, 0), C(0, 1)).IsAllowed, "파워 조합 " + powers[a] + "/" + powers[b]);
            }
            foreach (RuntimeContent power in powers)
            {
                LevelRuntimeState single = Empty(); Power(single, C(0, 0), power);
                Check(ActionQuery.Activate(single, C(0, 0)).IsAllowed == (power != RuntimeContent.Magnet), "단독 발동 " + power);
                Check(!new StartConditionReport(single).IsSatisfied, "제자리 발동만으로 시작 통과 금지 " + power);
                Normal(single, C(0, 1));
                Check(ActionQuery.Swap(single, C(0, 0), C(0, 1)).IsAllowed, "파워 일반 교환 " + power);
                Set(single.CellAt(C(0, 0)), "Cover", CoverKind.Web);
                Check(!ActionQuery.Activate(single, C(0, 0)).IsAllowed, "덮인 파워 발동 금지 " + power);
            }
            LevelRuntimeState magnet = Empty(); Power(magnet, C(0, 0), RuntimeContent.Magnet); Normal(magnet, C(5, 5));
            Set(magnet.CellAt(C(5, 5)), "Cover", CoverKind.Web);
            Check(ActionQuery.Activate(magnet, C(0, 0)).IsAllowed, "자석 거미줄 내부 대상 포함");
            Set(magnet.CellAt(C(5, 5)), "Cover", CoverKind.Mold);
            Check(!ActionQuery.Activate(magnet, C(0, 0)).IsAllowed, "자석 곰팡이 대상 제외");
            Power(magnet, C(0, 1), RuntimeContent.Rocket);
            Check(!ActionQuery.Swap(magnet, C(0, 0), C(0, 1)).IsAllowed, "일반 대상 없는 자석 로켓 금지");
            Power(magnet, C(0, 1), RuntimeContent.Magnet);
            Check(ActionQuery.Swap(magnet, C(0, 0), C(0, 1)).IsAllowed, "일반 대상 없는 자석 자석 허용");
            Power(magnet, C(0, 1), RuntimeContent.Recovery);
            Check(!ActionQuery.Swap(magnet, C(0, 0), C(0, 1)).IsAllowed, "자석 회수 교환 금지");
        }

        private static void Starts()
        {
            foreach (int colors in new[] { 3, 4, 5 })
            {
                LevelDefinition level = Definition("{\"colors\":[" + string.Join(",", Enumerable.Range(0, colors)) + "]}");
                string before = JsonUtility.ToJson(level); bool dirty = EditorUtility.IsDirty(level);
                System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
                StartingBoardSearch a = StartingBoardBuilder.Build(level, 12345);
                watch.Stop();
                Check(a.Status == StartingBoardStatus.Success && new StartConditionReport(a.State).IsSatisfied, colors + "색 시작 구성 성공 / " + a.Attempts + "회 / " + watch.ElapsedMilliseconds + "ms");
                foreach (int budget in new[] { 1, 64, 100000 })
                {
                    StartingBoardSearch b = new StartingBoardSearch(level, 12345);
                    while (!b.IsDone) b.Advance(budget);
                    Check(Snapshot(a.State) == Snapshot(b.State) && a.Attempts == b.Attempts && a.RandomDrawCount == b.RandomDrawCount, "프레임 분할 재현 " + colors + "/" + budget);
                }
                Check(before == JsonUtility.ToJson(level) && dirty == EditorUtility.IsDirty(level), "검색 원본 JSON/dirty 보존 " + colors);
                StartingBoardSearch limited = StartingBoardBuilder.Build(level, 12345, 0);
                Check(limited.Status == StartingBoardStatus.LimitReached && limited.State == null && limited.Attempts == 0, "상한 도달은 불가능과 구별 " + colors);
            }
            LevelDefinition fixedMatch = Definition(@"{""initialBlocks"":[{""coordinate"":{""row"":0,""column"":0},""kind"":1,""fixedColor"":0},{""coordinate"":{""row"":0,""column"":1},""kind"":1,""fixedColor"":0},{""coordinate"":{""row"":0,""column"":2},""kind"":1,""fixedColor"":0}]}");
            Check(StartingBoardBuilder.Build(fixedMatch, 3).Status == StartingBoardStatus.FixedMatch, "고정 일반만의 위반 증명");
            LevelDefinition tiny = Definition();
            using (SerializedObject edit = new SerializedObject(tiny))
            {
                SerializedProperty cells = edit.FindProperty("board.cells");
                for (int i = 1; i < cells.arraySize; i++) cells.GetArrayElementAtIndex(i).FindPropertyRelative("isActive").boolValue = false;
                edit.ApplyModifiedPropertiesWithoutUndo();
            }
            StartingBoardSearch exhausted = StartingBoardBuilder.Build(tiny, 1);
            Check(exhausted.Status == StartingBoardStatus.Exhausted && exhausted.Attempts == 5, "한 칸 변수 전체 할당 소진 증명");
            JsonUtility.FromJsonOverwrite(@"{""initialBlocks"":[{""coordinate"":{""row"":0,""column"":0},""kind"":2}]}", tiny);
            Check(StartingBoardBuilder.Build(tiny, 1).Status == StartingBoardStatus.FullyFixedFailure, "파워 한 칸 전체 고정 미충족");
        }
    }
}
