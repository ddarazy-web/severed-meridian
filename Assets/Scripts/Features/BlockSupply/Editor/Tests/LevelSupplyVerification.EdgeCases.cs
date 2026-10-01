using System;
using System.Linq;
using Board;
using UnityEditor;
using UnityEngine;

namespace Levels.Editor
{
    public static partial class LevelSupplyVerification
    {
        private static void VerifyEdgeCases()
        {
            LevelDefinition previous = level;
            level = LevelAssetOperations.CreateAtPath(folder + "/Edges.asset"); SetInt(level, "levelNumber", 55003);
            Mission(MissionKind.Color, 1);
            string clean = JsonUtility.ToJson(level);
            try
            {
                LevelBoardEditing.Apply(level, LevelBrush.Fixed, RabbitColor.Type1, new[] { C(3, 3) });
                LevelFlowEditing.SetPath(level, new[] { C(3, 3), C(4, 3) });
                string flowBefore = JsonUtility.ToJson(level.Flow);
                Check(LevelSupplyEditing.PlaceSources(level, new[] { C(3, 3) }) == null && level.InitialBlocks.Count == 1 &&
                    JsonUtility.ToJson(level.Flow) == flowBefore, "생성구 배치 시 초기 블록·경로 보존");
                string sourceBefore = JsonUtility.ToJson(level.Supply);
                LevelFlowEditing.SetGravity(level, new[] { C(7, 7) }, GravityDirection.Left);
                Check(JsonUtility.ToJson(level.Supply) == sourceBefore, "중력 변경은 생성구를 자동 추가하지 않음");
                Check(LevelSupplyEditing.PlaceSources(level, new[] { C(3, 3) }, true) == null && LevelSupplyRules.FindSource(level, C(3, 3)) == -1,
                    "생성구 삭제");
                Undo.PerformUndo();
                Check(JsonUtility.ToJson(level.Supply) == sourceBefore, "생성구 삭제 한 번 Undo");
                JsonUtility.FromJsonOverwrite(clean, level);
                LevelSupplyEditing.PlaceSources(level, level.Supply.Sources.Select(source => source.Coordinate).ToArray(), true);
                Check(LevelSupplyEditing.AddTopSources(level) == null && level.Supply.Sources.Count == BoardDefinition.DefaultColumns, "명시적 상단 생성구 일괄 추가");
                Undo.PerformUndo(); Check(level.Supply.Sources.Count == 0, "상단 생성구 일괄 추가 한 번 Undo");
                JsonUtility.FromJsonOverwrite(clean, level);
                Check(LevelMissionEditing.Add(level) == null && LevelMissionEditing.Add(level) == null && LevelMissionEditing.Add(level) == null && level.Missions.Count == 4,
                    "동일 색 미션 중복 없이 4개까지 추가");
                string before = JsonUtility.ToJson(level);
                Check(LevelMissionEditing.Add(level) != null && JsonUtility.ToJson(level) == before, "5번째 미션 추가 거절");
                Check(LevelMissionEditing.Set(level, 1, MissionKind.Color, level.Missions[0].Color, 5) != null && JsonUtility.ToJson(level) == before, "동일 대상 미션 수정 거절");
                Check(LevelMissionEditing.Set(level, 0, MissionKind.Color, RabbitColor.Type1, 0) != null && JsonUtility.ToJson(level) == before, "0 목표 편집 거절");
                using (SerializedObject data = new SerializedObject(level)) { data.FindProperty("missions").arraySize = 5; data.ApplyModifiedProperties(); }
                Check(Issues().Any(issue => issue.Code == LevelValidationCode.InvalidMission), "외부 5개 미션 오류 보존 검사");
                JsonUtility.FromJsonOverwrite(clean, level);
                Check(LevelSupplyEditing.SetSourceProperty(level, new[] { 0 }, "mode", (int)SupplyMode.Fixed) == null, "경계 검사 고정 목록 설정");
                before = JsonUtility.ToJson(level);
                foreach (SupplyItem bad in new[] { new SupplyItem((SupplyKind)99), new SupplyItem(SupplyKind.Bomb, 0),
                    new SupplyItem(SupplyKind.Scrap, 1, durability: 6), new SupplyItem(SupplyKind.Rocket, 1, direction: (RocketDirection)99) })
                    Check(LevelSupplyEditing.SetItems(level, 0, new[] { bad }) != null && JsonUtility.ToJson(level) == before, "잘못된 공급 항목 거절 " + bad.Kind + "/" + bad.Count);
                using (SerializedObject data = new SerializedObject(level)) { data.FindProperty("colors").arraySize = 3; data.ApplyModifiedProperties(); }
                before = JsonUtility.ToJson(level);
                Check(LevelSupplyEditing.SetItems(level, 0, new[] { new SupplyItem(SupplyKind.FixedNormal, 1, RabbitColor.Type5) }) != null && JsonUtility.ToJson(level) == before, "레벨 미사용 색 공급 거절");
                foreach (string text in new[] { "{}", "{\"items\":[]}", "{\"type\":\"MatchSupplyList\",\"version\":99,\"items\":[]}" })
                    Check(LevelSupplyEditing.Paste(level, new[] { 0 }, text, false) != null && JsonUtility.ToJson(level) == before, "클립보드 종류/버전 필수 " + text);
                Check(LevelSupplyEditing.PlaceSources(level, new[] { C(-1, 0) }) != null && JsonUtility.ToJson(level) == before, "범위 밖 생성구 거절");
                LevelBoardEditing.Apply(level, LevelBrush.Deactivate, RabbitColor.Type1, new[] { C(8, 8) });
                before = JsonUtility.ToJson(level);
                Check(LevelSupplyEditing.PlaceSources(level, new[] { C(8, 8) }) != null && JsonUtility.ToJson(level) == before, "비활성 생성구 배치 거절");
                JsonUtility.FromJsonOverwrite(clean, level);
                Check(LevelSupplyEditing.SetSourceProperty(level, new[] { 0 }, "mode", (int)SupplyMode.Fixed) == null &&
                    LevelSupplyEditing.SetItems(level, 0, new[] { new SupplyItem(SupplyKind.Recovery, 4) }) == null, "회수 고정 공급 설정");
                before = JsonUtility.ToJson(level);
                Check(LevelSupplyEditing.SetSourceProperty(level, new[] { 1 }, "mode", (int)SupplyMode.MaintainRecovery) != null && JsonUtility.ToJson(level) == before, "다른 위치 회수 고정/유지 충돌 거절");
                Mission(MissionKind.Recovery, 5);
                Check(Issues().Any(issue => issue.Code == LevelValidationCode.InsufficientSupply), "회수 고정 공급 수량 부족 검사");
                JsonUtility.FromJsonOverwrite(clean, level);
                SetInt(level, "schemaVersion", 3);
                before = JsonUtility.ToJson(level);
                Check(!LevelSchemaUpgrade.Upgrade(level, out _) && JsonUtility.ToJson(level) == before, "v3 예상 밖 신규 공급/미션 전환 거절");
                SetInt(level, "schemaVersion", 99); before = JsonUtility.ToJson(level);
                Check(!LevelSchemaUpgrade.Upgrade(level, out _) && JsonUtility.ToJson(level) == before, "미래 저장 버전 전환 거절");
                JsonUtility.FromJsonOverwrite(clean, level);
                Mission(MissionKind.Appliance, 1);
                LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)ObstacleKind.Appliance, Durability = 9 }, new[] { C(5, 5) });
                Check(LevelMissionRules.Supply(level, level.Missions[0]).Initial == 1 && !Issues().Any(issue => issue.Code == LevelValidationCode.InsufficientSupply), "2×2 9내구 폐가전은 미션 1개");
                Mission(MissionKind.ColorLock, 2);
                LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)ObstacleKind.ColorLock, Durability = 3, Color = RabbitColor.Type1 }, new[] { C(4, 0) });
                LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)ObstacleKind.ColorLock, Durability = 2, Color = RabbitColor.Type2 }, new[] { C(4, 1) });
                Check(LevelMissionRules.Supply(level, level.Missions[0]).Initial == 2, "자물쇠는 색·내구도와 무관하게 개체 합산");
                using (SerializedObject data = new SerializedObject(level))
                {
                    SerializedProperty list = data.FindProperty("supply.sources");
                    list.InsertArrayElementAtIndex(0);
                    LevelFlowEditing.SetCoordinate(list.GetArrayElementAtIndex(0).FindPropertyRelative("coordinate"), C(0, 0));
                    data.ApplyModifiedProperties();
                }
                before = JsonUtility.ToJson(level);
                Check(Issues().Any(issue => issue.Code == LevelValidationCode.InvalidSupply) && JsonUtility.ToJson(level) == before, "중복 생성구 검사 비파괴");
                AssetDatabase.SaveAssetIfDirty(level);
            }
            finally { level = previous; }
        }
    }
}
