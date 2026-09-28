using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Board;
using Simulation;
using UnityEditor;
using UnityEngine;

namespace Levels.Editor
{
    public static partial class LevelInitialStateVerification
    {
        private const string Evidence = "Logs/LevelInitialStateVerification";
        private static readonly List<string> Results = new List<string>();
        private static string folder;
        private static LevelDefinition mixed;
        private static string mixedJson;
        private static BoardCoordinate C(int row, int column) => new BoardCoordinate(row, column);

        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); Results.Add("PASS " + message); Debug.Log("[InitialState] " + message); }

        // 테스트용 전체 의미 상태 기록. 런타임 저장 형식이나 공통 직렬화 프레임워크로 사용하지 않는다.
        private static string Snapshot(object value)
        {
            if (value == null) return "null";
            Type type = value.GetType();
            if (type.IsEnum || type.IsPrimitive || value is string) return type.Name + ":" + Convert.ToString(value, CultureInfo.InvariantCulture);
            if (value is IEnumerable items) return "[" + string.Join("|", items.Cast<object>().Select(Snapshot)) + "]";
            return "{" + string.Join("|", type.GetProperties(BindingFlags.Instance | BindingFlags.Public).Where(property => property.GetIndexParameters().Length == 0)
                .OrderBy(property => property.Name, StringComparer.Ordinal).Select(property => property.Name + "=" + Snapshot(property.GetValue(value)))) + "}";
        }

        private static LevelDefinition Make(string name)
        {
            LevelDefinition level = ScriptableObject.CreateInstance<LevelDefinition>();
            JsonUtility.FromJsonOverwrite("{\"levelNumber\":81001,\"missions\":[{\"kind\":0,\"color\":0,\"count\":12}]}", level);
            AssetDatabase.CreateAsset(level, folder + "/" + name + ".asset"); return level;
        }

        private static void Int(LevelDefinition level, string path, int value)
        {
            using SerializedObject edit = new SerializedObject(level);
            edit.FindProperty(path).intValue = value; edit.ApplyModifiedPropertiesWithoutUndo();
        }

        private static LevelRuntimeState Build(LevelDefinition level, int seed = 12345)
        {
            LevelStateBuildResult result = LevelStateBuilder.Build(level, seed);
            if (!result.IsBuilt) throw new InvalidOperationException(string.Join(" | ", result.Issues));
            return result.State;
        }

        private static LevelDefinition Mixed()
        {
            LevelDefinition level = Make("Mixed");
            JsonUtility.FromJsonOverwrite(@"{
              ""colors"":[0,2,4],
              ""initialBlocks"":[
                {""coordinate"":{""row"":0,""column"":0},""kind"":1,""fixedColor"":4},
                {""coordinate"":{""row"":0,""column"":1},""kind"":2,""rocketDirection"":1},
                {""coordinate"":{""row"":0,""column"":2},""kind"":3},
                {""coordinate"":{""row"":0,""column"":3},""kind"":4},
                {""coordinate"":{""row"":0,""column"":4},""kind"":5},
                {""coordinate"":{""row"":0,""column"":5},""kind"":0,""fixedColor"":1}],
              ""obstacles"":[
                {""id"":""crate"",""coordinate"":{""row"":2,""column"":0},""kind"":0,""durability"":6},
                {""id"":""scrap"",""coordinate"":{""row"":2,""column"":1},""kind"":1,""durability"":5},
                {""id"":""safe"",""coordinate"":{""row"":2,""column"":2},""kind"":2,""durability"":5},
                {""id"":""lock"",""coordinate"":{""row"":2,""column"":3},""kind"":3,""durability"":3,""color"":2},
                {""id"":""appliance"",""coordinate"":{""row"":3,""column"":0},""kind"":4,""durability"":9},
                {""id"":""generator"",""coordinate"":{""row"":5,""column"":5},""kind"":5,""requiredCharge"":3},
                {""id"":""target"",""coordinate"":{""row"":5,""column"":8},""kind"":0,""durability"":2}],
              ""covers"":[{""coordinate"":{""row"":0,""column"":0},""kind"":0,""durability"":2},{""coordinate"":{""row"":0,""column"":1},""kind"":1,""durability"":1}],
              ""dust"":[{""coordinate"":{""row"":0,""column"":0},""durability"":3},{""coordinate"":{""row"":2,""column"":0},""durability"":2}],
              ""recoveryParts"":[{""row"":8,""column"":8}],
              ""missions"":[{""kind"":0,""color"":0,""count"":12},{""kind"":9,""count"":1},{""kind"":8,""count"":0}],
              ""connections"":[{""generatorId"":""generator"",""targetId"":""target"",""vertices"":[{""row"":5,""column"":7},{""row"":5,""column"":8}]}]
            }", level);
            using (SerializedObject edit = new SerializedObject(level))
            { edit.FindProperty("board.cells").GetArrayElementAtIndex(99).FindPropertyRelative("isActive").boolValue = false; edit.ApplyModifiedPropertiesWithoutUndo(); }
            LevelFlowEditing.SetGravity(level, new[] { C(6, 1) }, GravityDirection.Right);
            LevelFlowEditing.SetPath(level, new[] { C(6, 1), C(6, 2), C(7, 2) });
            LevelFlowEditing.SetPortal(level, C(3, 6), C(1, 8));
            LevelFlowEditing.SetArrival(level, C(9, 8), false);
            LevelFlowEditing.SetWalls(level, new[] { new BoardEdge(C(8, 0), C(8, 1)) }, false);
            foreach (IGrouping<BoardCoordinate, KeyValuePair<BoardCoordinate, BoardCoordinate>> group in LevelFlowRules.Graph(level).GroupBy(pair => pair.Value).Where(group => group.Count() > 1))
                LevelFlowEditing.SetMerge(level, group.Key, group.Select(pair => pair.Key).ToArray());
            LevelSupplyEditing.PlaceSources(level, new[] { C(0, 7) });
            LevelSupplyEditing.SetSourceProperty(level, new[] { 0 }, "mode", (int)SupplyMode.Fixed);
            LevelSupplyEditing.SetItems(level, 0, new[] { new SupplyItem(SupplyKind.Rocket, 2, direction: RocketDirection.Vertical), new SupplyItem(SupplyKind.FixedNormal, 3, RabbitColor.Type3), new SupplyItem(SupplyKind.Scrap, 1, durability: 4) });
            EditorUtility.SetDirty(level); AssetDatabase.SaveAssetIfDirty(level); return level;
        }

        private static void DataChecks()
        {
            LevelDefinition random = Make("Random");
            string original = JsonUtility.ToJson(random); bool dirty = EditorUtility.IsDirty(random);
            UnityEngine.Random.State global = UnityEngine.Random.state;
            LevelRuntimeState first = Build(random), second = Build(random);
            Check(Snapshot(first) == Snapshot(second), "같은 정의와 시드 전체 상태 재현");
            // System.Random(12345)의 기존 알고리즘 기준값. 생성 코드와 별도로 고정한 첫 10칸이다.
            Check(string.Join(",", first.Cells.Take(10).Select(cell => (int)cell.Color.Value)) == "0,0,3,2,3,4,0,3,1,2", "시드 12345 첫 10칸 기준값");
            Check(first.Cells.Count == 100 && first.Cells.All(cell => cell.Content == RuntimeContent.Normal) && first.Random.DrawCount == 100, "전체 무작위 100칸 구성");
            Check(JsonUtility.ToJson(UnityEngine.Random.state) == JsonUtility.ToJson(global), "전역 Unity 난수 보존");
            Check(JsonUtility.ToJson(random) == original && EditorUtility.IsDirty(random) == dirty, "정의 JSON과 dirty 보존");
            foreach (int count in new[] { 3, 4, 5 })
            {
                using (SerializedObject edit = new SerializedObject(random))
                { SerializedProperty colors = edit.FindProperty("colors"); colors.arraySize = count; for (int i = 0; i < count; i++) colors.GetArrayElementAtIndex(i).intValue = i; edit.ApplyModifiedPropertiesWithoutUndo(); }
                LevelRuntimeState state = Build(random, count);
                Check(state.Cells.All(cell => random.Colors.Contains(cell.Color.Value)), count + "종 사용 색만 추출");
            }
            LevelDefinition fixedLevel = Make("Fixed");
            using (SerializedObject edit = new SerializedObject(fixedLevel))
            {
                SerializedProperty blocks = edit.FindProperty("initialBlocks"); blocks.arraySize = 100;
                for (int i = 0; i < 100; i++) { SerializedProperty block = blocks.GetArrayElementAtIndex(i); LevelFlowEditing.SetCoordinate(block.FindPropertyRelative("coordinate"), C(i / 10, i % 10)); block.FindPropertyRelative("kind").intValue = 1; block.FindPropertyRelative("fixedColor").intValue = i % 5; }
                edit.ApplyModifiedPropertiesWithoutUndo();
            }
            LevelRuntimeState fixedState = Build(fixedLevel);
            Check(fixedState.Random.DrawCount == 0 && fixedState.Cells.Select((cell, index) => (int)cell.Color.Value == index % 5).All(value => value), "전체 고정 색 보존 및 난수 미소비");
            Check(fixedState.Cells.Take(5).All(cell => cell.Content == RuntimeContent.Normal), "완성 매칭을 제거하지 않고 초기 후보 유지");
            mixed = Mixed(); mixedJson = JsonUtility.ToJson(mixed);
            LevelRuntimeState a = Build(mixed), b = Build(mixed);
            string bBefore = Snapshot(b);
            Check(a.Cells[99].Content == RuntimeContent.Empty && a.Cells[99].Color == null, "비활성 칸 비점유");
            Check(a.Cells[0].Color == RabbitColor.Type5 && a.Cells[5].Color != RabbitColor.Type2, "부분 고정 및 무작위 칸의 과거 색 무시");
            Check(a.Cells[1].Content == RuntimeContent.Rocket && a.Cells[1].RocketDirection == RocketDirection.Vertical && a.Cells.Skip(1).Take(4).All(cell => cell.Color == null), "파워 종류 방향 보존·일반 색 없음");
            Check(a.Cells[2].Content == RuntimeContent.Bomb && a.Cells[3].Content == RuntimeContent.Drone && a.Cells[4].Content == RuntimeContent.Magnet, "파워 네 종류 구분");
            Check(a.Obstacles.Count == 7 && a.Cells.Count(cell => cell.Content == RuntimeContent.Obstacle) == 13 && a.CellAt(C(4, 1)).ObstacleIndex == a.CellAt(C(3, 0)).ObstacleIndex, "2x2는 네 점유 칸과 단일 본체");
            Check(a.CellAt(C(8, 8)).Content == RuntimeContent.Recovery && a.Obstacles[4].Durability == 9, "회수 부품과 내구도 보존");
            Check(a.Cells[0].Cover == CoverKind.Web && a.Cells[0].CoverDurability == 2 && a.Cells[0].DustDurability == 3 && a.Cells[1].Cover == CoverKind.Mold, "덮개 아래 일반·파워와 먼지 층 보존");
            Check(a.CellAt(C(6, 1)).Gravity == GravityDirection.Right && a.Cells[0].Gravity == GravityDirection.Down, "설정 중력과 기본 아래 중력");
            Check(a.Flow.Paths.Count == 3 && a.Flow.Merges.Count > 0 && a.Flow.Walls.Count == 1 && a.Flow.Portals.Count == 1 && a.Flow.Arrivals.Single().Equals(C(9, 8)), "흐름·합류·벽·통로·도착 보존");
            Check(a.Connections.Single().Vertices.SequenceEqual(new[] { C(5, 7), C(5, 8) }) && a.Obstacles[5].Charge == 0, "연결 꼭짓점·ID 및 충전 초기값");
            Check(a.MovesRemaining == mixed.MoveCount && a.Missions.All(mission => mission.Progress == 0) && a.Missions.Last().Target == 1, "이동·미션 초기값과 곰팡이 실제 초기 목표");
            Check(a.Supply.Sources[0].Items[0].Kind == SupplyKind.Rocket && a.Supply.Sources[0].Items[0].Count == 2 && a.Supply.Sources[0].ItemIndex == 0 && a.Supply.Sources[0].ItemConsumed == 0, "고정 공급 첫 항목·수량·커서 미소비");
            ((List<BoardCoordinate>)mixed.Flow.Merges[0].Sources)[0] = C(9, 9);
            ((List<BoardCoordinate>)mixed.Connections[0].Vertices)[0] = C(9, 9);
            ((List<SupplyItem>)mixed.Supply.Sources[0].Items)[0] = new SupplyItem(SupplyKind.Bomb, 99);
            Check(Snapshot(a) == bBefore && Snapshot(b) == bBefore, "원본 중첩 목록 변경이 두 상태에 전파되지 않음");
            JsonUtility.FromJsonOverwrite(mixedJson, mixed);
            typeof(RuntimeCell).GetProperty("DustDurability").GetSetMethod(true).Invoke(a.Cells[0], new object[] { 1 });
            typeof(RuntimeObstacle).GetProperty("Durability").GetSetMethod(true).Invoke(a.Obstacles[0], new object[] { 1 });
            typeof(RuntimeSource).GetProperty("ItemConsumed").GetSetMethod(true).Invoke(a.Supply.Sources[0], new object[] { 1 });
            Check(Snapshot(b) == bBefore && JsonUtility.ToJson(mixed) == mixedJson, "실행 상태 변경이 다른 상태/원본에 전파되지 않음");
            bool readOnly = false; try { ((IList<SupplyItem>)a.Supply.Sources[0].Items)[0] = new SupplyItem(); } catch (NotSupportedException) { readOnly = true; }
            Check(readOnly, "화면에서 설정 목록 변경 불가");
            LevelDefinition maintain = Make("Maintain");
            JsonUtility.FromJsonOverwrite(@"{""supply"":{""sources"":[{""coordinate"":{""row"":0,""column"":0},""mode"":2,""items"":[]},{""coordinate"":{""row"":0,""column"":1},""mode"":3,""items"":[]}],""scrapTarget"":2,""scrapLimit"":6,""scrapDurability"":4,""recoveryTarget"":2},""missions"":[{""kind"":9,""count"":3}]}", maintain);
            LevelFlowEditing.SetArrival(maintain, C(9, 1), false);
            LevelRuntimeState maintained = Build(maintain);
            Check(maintained.Supply.ScrapRemaining == 6 && maintained.Supply.RecoveryTarget == 2 && maintained.Cells.All(cell => cell.Content == RuntimeContent.Normal), "유지 목표/한도 보존 및 추가 생성 미실행");
            Check(!LevelStateBuilder.Build(null, 0).IsBuilt, "null 정의 진단");
            foreach (int version in new[] { 1, 3, 5 })
            { Int(mixed, "schemaVersion", version); LevelStateBuildResult failure = LevelStateBuilder.Build(mixed, 0); Check(!failure.IsBuilt && failure.Issues[0].PropertyPath == "schemaVersion", "미지원 버전 " + version + " 거절"); }
            JsonUtility.FromJsonOverwrite(mixedJson, mixed);
            foreach (string path in new[] { "initialBlocks.Array.data[0].coordinate.row", "obstacles.Array.data[0].durability", "supply.sources.Array.data[0].items.Array.data[0].count" })
            {
                Int(mixed, path, -1); string invalid = JsonUtility.ToJson(mixed);
                LevelStateBuildResult failure = LevelStateBuilder.Build(mixed, 0);
                Check(!failure.IsBuilt && failure.Issues.Count > 0 && JsonUtility.ToJson(mixed) == invalid, "잘못된 정의 진단·원본 보존 " + path);
                JsonUtility.FromJsonOverwrite(mixedJson, mixed);
            }
            using (SerializedObject edit = new SerializedObject(mixed)) { edit.FindProperty("obstacles.Array.data[1].id").stringValue = "crate"; edit.ApplyModifiedPropertiesWithoutUndo(); }
            Check(!LevelStateBuilder.Build(mixed, 0).IsBuilt, "중복 본체 ID 거절"); JsonUtility.FromJsonOverwrite(mixedJson, mixed);
            LevelRuntimeState beforeNewSeed = Build(mixed); string saved = Snapshot(beforeNewSeed);
            Build(mixed, -17); Check(Snapshot(beforeNewSeed) == saved, "새 시드 생성이 기존 상태 보존");
            File.WriteAllText(Evidence + "/data-snapshot.txt", saved);
        }
    }
}
