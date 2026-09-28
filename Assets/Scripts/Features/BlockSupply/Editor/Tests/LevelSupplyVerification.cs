using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Board;
using UnityEditor;
using UnityEngine;

namespace Levels.Editor
{
    public static partial class LevelSupplyVerification
    {
        private const string Evidence = "Logs/LevelSupplyVerification";
        private const string StatePath = Evidence + "/state.json";
        private const string Fixtures = "Assets/Scripts/Features/Levels/Editor/Tests/Fixtures";
        private static readonly List<string> Results = new List<string>();
        private static string folder;
        private static LevelDefinition level;

        [Serializable]
        private sealed class State
        {
            public string folder;
            public int processId;
            public string[] paths, json, guids;
        }

        [Serializable]
        private sealed class Version3Fields
        {
            public int levelNumber = 0, moveCount = 0;
            public List<RabbitColor> colors = null;
            public BoardDefinition board = null;
            public List<InitialBlockDefinition> initialBlocks = null;
            public List<ObstaclePlacementDefinition> obstacles = null;
            public List<CoverPlacementDefinition> covers = null;
            public List<DustPlacementDefinition> dust = null;
            public LevelFlowDefinition flow = null;
            public List<LevelConnectionDefinition> connections = null;
        }

        public static void Prepare()
        {
            Directory.CreateDirectory(Evidence);
            if (File.Exists(StatePath)) throw new InvalidOperationException("기존 공급 검증 재시작/정리를 먼저 완료하세요.");
            folder = "Assets/__LevelSupplyVerification_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            try
            {
                VerifyModels(); VerifyEdgeCases(); SaveState();
                File.WriteAllLines(Evidence + "/data-results.txt", Results);
            }
            catch (Exception exception)
            {
                Results.Add("FAIL " + exception); File.WriteAllLines(Evidence + "/data-results.txt", Results);
                AssetDatabase.DeleteAsset(folder); throw;
            }
        }

        private static void VerifyModels()
        {
            foreach (string name in new[] { "Version3Flow", "Version3Invalid", "Version3Saved" })
            {
                string path = folder + "/" + name + ".asset";
                File.Copy(Fixtures + "/" + name + ".txt", path);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                LevelDefinition old = AssetDatabase.LoadAssetAtPath<LevelDefinition>(path);
                string original = JsonUtility.ToJson(old), guid = AssetDatabase.AssetPathToGUID(path);
                string legacy = JsonUtility.ToJson(JsonUtility.FromJson<Version3Fields>(File.ReadAllText(Fixtures + "/" + name + ".json")));
                Check(old.SchemaVersion == 3 && !LevelBoardEditing.CanEdit(old), name + " 실제 v3 읽기 전용");
                LevelDefinitionValidator.Validate(old);
                Check(JsonUtility.ToJson(old) == original && !EditorUtility.IsDirty(old), name + " 검사 비파괴");
                Check(LevelSchemaUpgrade.Upgrade(old, out _), name + " v4 명시 전환");
                Check(JsonUtility.ToJson(JsonUtility.FromJson<Version3Fields>(JsonUtility.ToJson(old))) == legacy,
                    name + " 전체 기존 필드·오류·흐름·전선·ID 보존");
                Check(old.Supply.Sources.Count == 0 && old.Missions.Count == 0 && old.RecoveryParts.Count == 0, name + " 신규 설정 자동 추측 없음");
                Undo.PerformUndo();
                Check(JsonUtility.ToJson(old) == original, name + " 전환 한 번 Undo");
                Undo.PerformRedo();
                Check(old.SchemaVersion == 4 && AssetDatabase.AssetPathToGUID(path) == guid, name + " Redo·GUID 보존");
                AssetDatabase.SaveAssetIfDirty(old);
            }

            level = LevelAssetOperations.CreateAtPath(folder + "/Supply.asset");
            SetInt(level, "levelNumber", 55001);
            Check(level.Supply.Sources.Count == 10 && level.Supply.Sources.Select(source => source.Coordinate).Distinct().Count() == 10, "새 레벨 상단 생성구 10개");
            Mission(MissionKind.Color, 30);
            Check(LevelDefinitionValidator.Validate(level).Count == 0, "색 수집은 초기 수량 부족으로 오판하지 않음");
            Check(LevelSupplyEditing.SetSourceProperty(level, new[] { 0, 1 }, "mode", (int)SupplyMode.Fixed) == null, "생성구 공통 방식 변경");
            Check(LevelSupplyEditing.SetItems(level, 0, new[] { new SupplyItem(SupplyKind.Rocket, 2, direction: RocketDirection.Vertical), new SupplyItem(SupplyKind.Scrap, 3, durability: 5) }) == null, "종류별 공급 속성·수량 기록");
            string clipboard = LevelSupplyEditing.Copy(level, 0);
            string before = JsonUtility.ToJson(level);
            Check(LevelSupplyEditing.Paste(level, new[] { 1 }, clipboard, false) == null && level.Supply.Sources[1].Items.Count == 2, "목록 교체");
            Undo.PerformUndo(); Check(JsonUtility.ToJson(level) == before, "붙여넣기 한 번 Undo"); Undo.PerformRedo();
            Check(LevelSupplyEditing.SetItems(level, 0, new[] { new SupplyItem(SupplyKind.Bomb, 1) }) == null && level.Supply.Sources[1].Items[0].Kind == SupplyKind.Rocket, "붙여넣기 깊은 복사");
            Check(LevelSupplyEditing.Paste(level, new[] { 0, 1 }, clipboard, true) == null && level.Supply.Sources[0].Items.Count == 3 && level.Supply.Sources[1].Items.Count == 4, "다중 목록 뒤에 추가");
            before = JsonUtility.ToJson(level);
            Check(LevelSupplyEditing.Paste(level, new[] { 0, 2 }, clipboard, false) != null && JsonUtility.ToJson(level) == before, "부적합 대상 포함 시 전체 원본 보존");
            Check(LevelSupplyEditing.Paste(level, new[] { 0 }, "not json", false) != null && JsonUtility.ToJson(level) == before, "잘못된 클립보드 원본 보존");
            Check(LevelSupplyEditing.SetSourceProperty(level, new[] { 2 }, "mode", (int)SupplyMode.MaintainScrap) != null && JsonUtility.ToJson(level) == before, "다른 생성구 고철 고정/유지 충돌 거절");
            Check(LevelSupplyEditing.SetSourceProperty(level, new[] { 0 }, "mode", (int)SupplyMode.Random) != null && JsonUtility.ToJson(level) == before, "목록 있는 생성구 방식 암묵 전환 금지");
            Check(LevelSupplyEditing.SetItems(level, 0, Array.Empty<SupplyItem>()) == null && LevelSupplyEditing.SetItems(level, 1, Array.Empty<SupplyItem>()) == null, "목록 명시 비우기");
            Check(LevelSupplyEditing.SetSourceProperty(level, new[] { 0, 1 }, "mode", (int)SupplyMode.MaintainScrap) == null, "여러 고철 유지 생성구");
            SetInt(level, "supply.scrapTarget", 2); SetInt(level, "supply.scrapLimit", 6);
            LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)ObstacleKind.Scrap, Durability = 5 }, new[] { C(5, 0), C(5, 1) });
            Mission(MissionKind.Scrap, 8);
            MissionSupplySummary summary = LevelMissionRules.Supply(level, level.Missions[0]);
            Check(summary.Initial == 2 && summary.Maintained == 6 && summary.Maximum == 8, "두 생성구의 공통 한도는 한 번만 합산");
            Check(!Issues().Any(issue => issue.Code == LevelValidationCode.InsufficientSupply), "고철 2+6 목표 8 수량 검사 통과");
            Mission(MissionKind.Scrap, 9);
            Check(Issues().Any(issue => issue.Code == LevelValidationCode.InsufficientSupply), "고철 2+6 목표 9 부족 검사");
            Check(LevelSupplyEditing.PlaceRecovery(level, new[] { C(6, 6) }) == null, "회수 최초 배치");
            Check(LevelPlacementRules.BlockSpaceError(level, C(6, 6)) != null && LevelPlacementRules.ObstacleSpaceError(level, C(6, 6), ObstacleKind.Crate) != null, "회수 부품 점유를 기존 블록/장애물 도구도 존중");
            Check(LevelSupplyEditing.SetSourceProperty(level, new[] { 2 }, "mode", (int)SupplyMode.MaintainRecovery) == null, "고철과 별도 생성구 회수 유지");
            Mission(MissionKind.Recovery, 6);
            summary = LevelMissionRules.Supply(level, level.Missions[0]);
            Check(summary.Initial == 1 && summary.Maintained == 5 && summary.GoalBased, "회수 잔여 목표 기반 공급 상한");
            Check(Issues().Any(issue => issue.Message.Contains("도착 바닥")), "회수 도착 바닥 누락 오류");
            using (SerializedObject data = new SerializedObject(level))
            {
                LevelFlowEditing.SetCoordinates(data.FindProperty("flow.arrivals"), new[] { C(9, 6) }); data.ApplyModifiedProperties();
            }
            Check(!Issues().Any(issue => issue.Message.Contains("도착 바닥")), "유효 도착 바닥 확인");
            before = JsonUtility.ToJson(level);
            Check(LevelSupplyEditing.PlaceSources(level, new[] { C(9, 6) }) != null && JsonUtility.ToJson(level) == before, "생성구/도착 바닥 중첩 원자적 거절");
            Mission(MissionKind.Mold, 0);
            Check(!Issues().Any(issue => issue.Code == LevelValidationCode.InvalidMission || issue.Code == LevelValidationCode.InsufficientSupply), "곰팡이는 고정 수량이 아닌 전부 제거");
            Mission(MissionKind.Recovery, 6);
            Check(Issues().Count == 0, "공급·회수·미션 최종 정상 구성");
        }

        private static BoardCoordinate C(int row, int column) => new BoardCoordinate(row, column);
        private static List<LevelValidationIssue> Issues() => LevelDefinitionValidator.Validate(level);
        private static void Mission(MissionKind kind, int count)
        {
            using SerializedObject data = new SerializedObject(level);
            SerializedProperty list = data.FindProperty("missions"); list.arraySize = 1;
            SerializedProperty item = list.GetArrayElementAtIndex(0);
            item.FindPropertyRelative("kind").intValue = (int)kind; item.FindPropertyRelative("count").intValue = count;
            item.FindPropertyRelative("color").intValue = 0; data.ApplyModifiedProperties();
        }
        private static void SetInt(LevelDefinition target, string path, int value)
        { using SerializedObject data = new SerializedObject(target); data.FindProperty(path).intValue = value; data.ApplyModifiedProperties(); }
        private static void Check(bool value, string message)
        {
            Results.Add((value ? "PASS " : "FAIL ") + message);
            File.WriteAllLines(Evidence + "/progress.txt", Results);
            if (!value) throw new InvalidOperationException(message);
        }
        private static void SaveState()
        {
            string[] paths = AssetDatabase.FindAssets("t:LevelDefinition", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path).ToArray();
            foreach (string path in paths) AssetDatabase.SaveAssetIfDirty(AssetDatabase.LoadAssetAtPath<LevelDefinition>(path));
            File.WriteAllText(StatePath, JsonUtility.ToJson(new State { folder = folder, processId = System.Diagnostics.Process.GetCurrentProcess().Id,
                paths = paths, json = paths.Select(path => JsonUtility.ToJson(AssetDatabase.LoadAssetAtPath<LevelDefinition>(path))).ToArray(),
                guids = paths.Select(AssetDatabase.AssetPathToGUID).ToArray() }, true));
        }
        public static void Restart()
        {
            State state = JsonUtility.FromJson<State>(File.ReadAllText(StatePath));
            Check(state.processId != System.Diagnostics.Process.GetCurrentProcess().Id, "별도 Unity 프로세스 재시작");
            for (int i = 0; i < state.paths.Length; i++)
            {
                LevelDefinition saved = AssetDatabase.LoadAssetAtPath<LevelDefinition>(state.paths[i]);
                Check(saved != null && JsonUtility.ToJson(saved) == state.json[i] && AssetDatabase.AssetPathToGUID(state.paths[i]) == state.guids[i],
                    "재시작 전체 값·GUID 보존 " + Path.GetFileName(state.paths[i]));
            }
            Check(state.folder.StartsWith("Assets/__LevelSupplyVerification_", StringComparison.Ordinal) && AssetDatabase.DeleteAsset(state.folder), "임시 에셋 정리");
            File.WriteAllLines(Evidence + "/restart-results.txt", Results);
            File.Move(StatePath, Evidence + "/completed-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + ".json");
        }
    }
}
