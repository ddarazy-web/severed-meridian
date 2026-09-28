using System;
using System.Collections.Generic;
using System.Linq;

namespace Levels
{
    public readonly struct MissionSupplySummary
    {
        public readonly long Initial, Fixed, Maintained;
        public readonly bool Dynamic, GoalBased;
        public long Maximum => Initial + Fixed + Maintained;
        public MissionSupplySummary(long initial, long fixedCount, long maintained, bool dynamic = false, bool goalBased = false)
        { Initial = initial; Fixed = fixedCount; Maintained = maintained; Dynamic = dynamic; GoalBased = goalBased; }
        public override string ToString() => Dynamic ? $"최초 {Initial} · 동적 목표/무작위 공급: 상한 미판정" :
            $"최초 {Initial} + 고정 {Fixed} + 유지 {Maintained} = {Maximum}" + (GoalBased ? " (미션 목표 기반 상한)" : "");
    }

    public static class LevelMissionRules
    {
        public static string Name(MissionKind kind) => kind switch
        {
            MissionKind.Color => "달토끼 수집", MissionKind.Crate => "나무상자 제거", MissionKind.Web => "거미줄 제거",
            MissionKind.Scrap => "고철 제거", MissionKind.Dust => "먼지 제거", MissionKind.Safe => "금고 제거",
            MissionKind.ColorLock => "자물쇠 제거", MissionKind.Appliance => "폐가전 제거", MissionKind.Mold => "곰팡이 전부 제거",
            MissionKind.Recovery => "부품 회수", _ => "잘못된 미션"
        };

        public static MissionSupplySummary Supply(LevelDefinition level, LevelMissionDefinition mission)
        {
            long initial = mission.Kind switch
            {
                MissionKind.Color => level.InitialBlocks?.Count(block => block.FixedColor == mission.Color) ?? 0,
                MissionKind.Web => level.Covers?.Count(cover => cover.Kind == CoverKind.Web) ?? 0,
                MissionKind.Mold => level.Covers?.Count(cover => cover.Kind == CoverKind.Mold) ?? 0,
                MissionKind.Dust => level.Dust?.Count ?? 0,
                MissionKind.Recovery => level.RecoveryParts?.Count ?? 0,
                _ => level.Obstacles?.Count(obstacle => mission.Kind switch
                {
                    MissionKind.Crate => obstacle.Kind == ObstacleKind.Crate, MissionKind.Scrap => obstacle.Kind == ObstacleKind.Scrap,
                    MissionKind.Safe => obstacle.Kind == ObstacleKind.Safe, MissionKind.ColorLock => obstacle.Kind == ObstacleKind.ColorLock,
                    MissionKind.Appliance => obstacle.Kind == ObstacleKind.Appliance, _ => false
                }) ?? 0
            };
            if (mission.Kind == MissionKind.Color || mission.Kind == MissionKind.Mold) return new MissionSupplySummary(initial, 0, 0, true);
            long fixedCount = mission.Kind == MissionKind.Scrap ? LevelSupplyRules.FixedCount(level, SupplyKind.Scrap) :
                mission.Kind == MissionKind.Recovery ? LevelSupplyRules.FixedCount(level, SupplyKind.Recovery) : 0;
            bool scrap = mission.Kind == MissionKind.Scrap && LevelSupplyRules.HasMode(level, SupplyMode.MaintainScrap) && level.Supply.ScrapTarget > 0;
            bool recovery = mission.Kind == MissionKind.Recovery && LevelSupplyRules.HasMode(level, SupplyMode.MaintainRecovery) && level.Supply.RecoveryTarget > 0;
            long maintained = scrap ? Math.Max(0, level.Supply.ScrapLimit) : recovery ? Math.Max(0, (long)mission.Count - initial - fixedCount) : 0;
            return new MissionSupplySummary(initial, fixedCount, maintained, false, recovery);
        }

        public static void Validate(LevelDefinition level, List<LevelValidationIssue> issues)
        {
            if (level.SchemaVersion < 4) return;
            if (level.Missions == null || level.Missions.Count < 1 || level.Missions.Count > 4)
                issues.Add(new LevelValidationIssue(LevelValidationCode.InvalidMission, "미션은 1~4개로 지정하세요.", "missions"));
            if (level.Missions == null) return;
            HashSet<(MissionKind, int)> seen = new HashSet<(MissionKind, int)>();
            for (int i = 0; i < level.Missions.Count; i++)
            {
                LevelMissionDefinition mission = level.Missions[i];
                string path = $"missions.Array.data[{i}]";
                if (!Enum.IsDefined(typeof(MissionKind), mission.Kind))
                { issues.Add(new LevelValidationIssue(LevelValidationCode.InvalidMission, "미지원 미션 종류입니다.", path)); continue; }
                if (!seen.Add((mission.Kind, mission.Kind == MissionKind.Color ? (int)mission.Color : -1)))
                    issues.Add(new LevelValidationIssue(LevelValidationCode.InvalidMission, "같은 대상의 미션을 중복 지정할 수 없습니다.", path));
                if (mission.Kind == MissionKind.Color && (!Enum.IsDefined(typeof(RabbitColor), mission.Color) || level.Colors?.Contains(mission.Color) != true))
                    issues.Add(new LevelValidationIssue(LevelValidationCode.InvalidMission, "레벨 사용 색을 지정하세요.", path + ".color"));
                if (mission.Kind != MissionKind.Mold && mission.Count <= 0)
                    issues.Add(new LevelValidationIssue(LevelValidationCode.InvalidMission, "미션 목표는 양수여야 합니다.", path + ".count"));
                if (mission.Kind == MissionKind.Recovery && level.Flow?.Arrivals?.Any(cell => LevelFlowRules.Active(level, cell) &&
                    LevelSupplyRules.FindSource(level, cell) == -1 && level.Flow.Portals?.Any(portal => portal.Entrance.Equals(cell) || (portal.HasExit && portal.Exit.Equals(cell))) != true) != true)
                    issues.Add(new LevelValidationIssue(LevelValidationCode.InvalidMission, "회수 미션에 유효한 도착 바닥이 없습니다.", path));
                MissionSupplySummary quantity = Supply(level, mission);
                if (!quantity.Dynamic && mission.Count > quantity.Maximum)
                    issues.Add(new LevelValidationIssue(LevelValidationCode.InsufficientSupply, $"{Name(mission.Kind)} 목표 {mission.Count}: {quantity}", path));
            }
        }
    }
}
