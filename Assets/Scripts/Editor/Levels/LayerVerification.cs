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
    public static partial class LayerVerification
    {
        private const string Evidence = "Logs/LayerVerification";
        private static readonly List<string> Results = new List<string>();
        private static BoardCoordinate C(int r, int c) => new BoardCoordinate(r, c);
        private static object Invoke(Type type, string name, object owner, params object[] args)
            => type.GetMethod(name, BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, args);
        private static void Set(object owner, string name, object value) => owner.GetType().GetProperty(name).GetSetMethod(true).Invoke(owner, new[] { value });
        private static string Snapshot(object value) => (string)Invoke(typeof(LevelInitialStateVerification), "Snapshot", null, value);
        private static void Check(bool condition, string name) { if (!condition) throw new InvalidOperationException(name); Results.Add("PASS " + name); }
        private static LevelDefinition Make() => (LevelDefinition)Invoke(typeof(PowerEffectVerification), "Make", null);
        private static LevelRuntimeState Build(LevelDefinition level, int seed = 12345)
        {
            LevelStateBuildResult valid = LevelStateBuilder.Build(level, seed);
            if (valid.IsBuilt) return valid.State;
            // 효과 단위 시험은 아래에서 층을 주입하므로 미션 가용 수량 검사는 실제 UI 시험에서 수행한다.
            string json = JsonUtility.ToJson(level); LevelMissionDefinition[] missions = level.Missions.ToArray();
            JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":0,\"color\":0,\"count\":100}]}", level);
            LevelStateBuildResult built = LevelStateBuilder.Build(level, seed); JsonUtility.FromJsonOverwrite(json, level);
            if (!built.IsBuilt) throw new InvalidOperationException(string.Join(" | ", built.Issues));
            RuntimeMission[] runtime = missions.Select(m => (RuntimeMission)Activator.CreateInstance(typeof(RuntimeMission), BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] { m }, null)).ToArray();
            typeof(LevelRuntimeState).GetField("<Missions>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(built.State, Array.AsReadOnly(runtime));
            return built.State;
        }
        private static TurnEffectContext Context() => (TurnEffectContext)Invoke(typeof(TargetPowerVerification), "Context", null);
        private static void Web(RuntimeCell cell, int durability) { Set(cell, "Cover", CoverKind.Web); Set(cell, "CoverDurability", durability); }
        private static List<EffectRecord> Hit(LevelRuntimeState state, BoardCoordinate target, TurnEffectContext context)
        {
            List<EffectRecord> records = new List<EffectRecord>();
            object[] args = { state, Array.Empty<MatchedBlockChange>(), (BoardCoordinate?)target, context, records, null };
            if (!(bool)Invoke(typeof(PowerEffectResolution), "Apply", null, args)) throw new InvalidOperationException((string)args[5]);
            return records;
        }
        private static void Missions(LevelDefinition level) => JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":0,\"color\":0,\"count\":100},{\"kind\":1,\"count\":100},{\"kind\":2,\"count\":100},{\"kind\":4,\"count\":100}]}", level);
        public static void Data()
        {
            Directory.CreateDirectory(Evidence); Results.Clear();
            try { DataChecks(); File.WriteAllLines(Evidence + "/data-results.txt", Results); EditorApplication.Exit(0); }
            catch (Exception error) { Results.Add("FAIL " + error); File.WriteAllLines(Evidence + "/data-results.txt", Results); Debug.LogException(error); EditorApplication.Exit(1); }
        }
        private static void DataChecks()
        {
            for (int durability = 1; durability <= 3; durability++)
                foreach (RuntimeContent content in new[] { RuntimeContent.Normal, RuntimeContent.Rocket, RuntimeContent.Bomb, RuntimeContent.Drone, RuntimeContent.Magnet })
                {
                    LevelDefinition level = Make(); Missions(level); LevelRuntimeState state = Build(level); RuntimeCell cell = state.CellAt(C(4, 4));
                    Set(cell, "Content", content); Set(cell, "Color", content == RuntimeContent.Normal ? (RabbitColor?)RabbitColor.Type1 : null);
                    Set(cell, "RocketDirection", content == RuntimeContent.Rocket ? (RocketDirection?)RocketDirection.Horizontal : null);
                    Web(cell, durability); Set(cell, "DustDurability", 3); TurnEffectContext context = Context();
                    string before = Snapshot(state);
                    Check(!ActionQuery.Activate(state, C(4, 4)).IsAllowed && !ActionQuery.Swap(state, C(4, 4), C(4, 5)).IsAllowed, "덮개 직접 조작 금지 " + content + durability);
                    Check(DamageReaction.Evaluate(state, C(4, 4), DamageCause.AdjacentMatch, C(4, 3), context).Response == DamageResponse.None && Snapshot(state) == before,
                        "인접 매칭 피해 없음/조회 무변경 " + content + durability);
                    List<EffectRecord> first = Hit(state, C(4, 4), context);
                    Check(cell.Content == content && cell.CoverDurability == durability - 1 && cell.DustDurability == 3 && first.All(e => e.Response != DamageResponse.Activate), "덮개1피해/내용물·먼지 보존 " + content + durability);
                    Check(state.Missions[0].Progress == 0 && state.Missions[2].Progress == (durability == 1 ? 1 : 0), "거미줄 완전 제거만 집계 " + content + durability);
                    if (durability > 1)
                    {
                        Hit(state, C(4, 4), context); Check(cell.CoverDurability == durability - 1, "동일 턴 추가 거미줄 피해 없음 " + content + durability);
                        Hit(state, C(4, 4), Context()); Check(cell.CoverDurability == durability - 2, "다음 턴 거미줄 재피해 " + content + durability);
                    }
                    else
                    {
                        List<EffectRecord> second = Hit(state, C(4, 4), context);
                        Check(cell.Content == RuntimeContent.Empty && (content == RuntimeContent.Normal ? cell.DustDurability == 2 : cell.DustDurability == 3), "별도 타격 노출 내용물 처리 " + content);
                        if (content != RuntimeContent.Normal) Check(second.Count(e => e.Response == DamageResponse.Activate && e.Target.Equals(C(4, 4))) == 1, "벗긴 파워 별도 타격 발동 " + content);
                    }
                }

            for (int durability = 1; durability <= 3; durability++)
            {
                LevelDefinition level = Make(); Missions(level); LevelRuntimeState state = Build(level); RuntimeCell cell = state.CellAt(C(4, 4));
                Set(cell, "Color", RabbitColor.Type1); Set(cell, "DustDurability", durability); TurnEffectContext context = Context();
                Hit(state, C(4, 4), context);
                Check(cell.DustDurability == durability - 1 && state.Missions[0].Progress == 1 && state.Missions[3].Progress == (durability == 1 ? 1 : 0), "일반 소비 색상/먼지 독립 집계 " + durability);
                Set(cell, "Content", RuntimeContent.Normal); Set(cell, "Color", RabbitColor.Type1); Hit(state, C(4, 4), context);
                Check(cell.DustDurability == durability - 1 && state.Missions[0].Progress == 2, "유입 일반 소비에도 먼지 턴1피해 " + durability);
            }
            MatchingChecks(); TargetChecks(); CombinationChecks();
        }

        private static void MatchingChecks()
        {
            LevelDefinition level = (LevelDefinition)Invoke(typeof(BoardActionVerification), "RocketBoard", null); Missions(level);
            LevelRuntimeState state = Build(level); Web(state.CellAt(C(3, 2)), 2); Set(state.CellAt(C(3, 2)), "DustDurability", 2); Set(state.CellAt(C(3, 3)), "DustDurability", 1);
            BoardActionExecutor executor = new BoardActionExecutor(state); BoardActionResult action = executor.Swap(C(2, 3), C(3, 3));
            Check(action.IsApplied && action.Changes.Count(c => c.IsConsumed) == 3 && action.Changes.Count(c => !c.IsConsumed) == 1, "매칭 거미줄 참여와 실제 소비 분리");
            Check(executor.State.CellAt(C(3, 2)).CoverDurability == 1 && executor.State.CellAt(C(3, 2)).DustDurability == 2 && executor.State.CellAt(C(3, 3)).DustDurability == 0, "매칭 보존 칸 먼지 유지/생성 칸 먼지 피해");
            Check(executor.State.Missions[0].Progress == 3 && executor.State.Missions[3].Progress == 1, "매칭 파워 생성 소비 포함/보존 제외");

            LevelDefinition line = (LevelDefinition)Invoke(typeof(BoardActionVerification), "Make", null,
                new Dictionary<BoardCoordinate, int> { [C(0, 0)] = 0, [C(0, 1)] = 0, [C(0, 2)] = 0, [C(0, 3)] = 0 }, 20);
            BoardActionExecutor automatic = (BoardActionExecutor)Invoke(typeof(CascadeVerification), "Automatic", null, line, 12345);
            Web(automatic.State.CellAt(C(0, 1)), 3);
            Invoke(typeof(TurnEffectContext), "RecordArrival", automatic.TurnEffects, C(9, 9), C(0, 1), 1, true);
            CascadeStepResult relocated = automatic.ResolveAutomaticMatch();
            Check(relocated.IsApplied && (relocated.Decisions[0].Spawn.Value.Equals(C(0, 0)) || relocated.Decisions[0].Spawn.Value.Equals(C(0, 2))), "덮인 최종 도착 칸에서 최단거리 대체 생성");
            Check(automatic.State.CellAt(C(0, 1)).Content == RuntimeContent.Normal && automatic.State.CellAt(C(0, 1)).CoverDurability == 2, "대체 생성 후 원래 칸 보존");

            BoardActionExecutor residual = (BoardActionExecutor)Invoke(typeof(CascadeVerification), "Automatic", null, line, 12345);
            foreach (RuntimeCell cell in residual.State.Cells.Where(c => c.IsActive)) Web(cell, 3);
            CascadeStepResult first = residual.ResolveAutomaticMatch();
            Check(first.IsApplied && !first.Decisions[0].Spawn.HasValue && residual.State.Cells.Where(c => c.IsActive).All(c => c.CoverDurability == 2), "전부 덮인 패턴 생성 생략/피해1");
            Check(residual.Settle().IsApplied && residual.ResolveAutomaticMatch().Reason == CascadeStepReason.Blocked && residual.Outcome.Kind == BoardOutcomeKind.Blocked, "변화 없는 잔존 매칭 안정화 후 재배치 불가");
            // 효과 단계 입력을 통해 새 유입과 피해 기록의 서로 다른 수명을 검증한다.
            // 종료된 판을 다시 진행시키지 않고, 안정 판정 직전의 별도 효과 사례를 구성한다.
            residual = (BoardActionExecutor)Invoke(typeof(CascadeVerification), "Automatic", null, line, 12345);
            foreach (RuntimeCell cell in residual.State.Cells.Where(c => c.IsActive)) Web(cell, 3);
            residual.ResolveAutomaticMatch(); residual.Settle();
            Set(residual, "Phase", BoardActionPhase.WaitingForAutomaticMatch);
            Invoke(typeof(TurnEffectContext), "RecordArrival", residual.TurnEffects, C(9, 9), C(0, 0), 2, true);
            Check(residual.ResolveAutomaticMatch().IsApplied && residual.LastCascadeStep.Reason == CascadeStepReason.Matched && residual.State.CellAt(C(0, 0)).CoverDurability == 2,
                "새 같은 색 유입은 매칭 참여/동일 턴 거미줄 재피해 없음");
        }

        private static void TargetChecks()
        {
            LevelDefinition level = Make(); Missions(level); LevelRuntimeState state = Build(level); TurnEffectContext context = Context();
            RuntimeCell web = state.CellAt(C(4, 4)), exposed = state.CellAt(C(9, 9));
            foreach (RuntimeCell cell in state.Cells) if (cell.Content == RuntimeContent.Normal) Set(cell, "Color", RabbitColor.Type2);
            Set(web, "Color", RabbitColor.Type1); Web(web, 2); Set(web, "DustDurability", 1); Set(exposed, "Color", RabbitColor.Type1); Set(exposed, "DustDurability", 1);
            DroneTargetManager manager = (DroneTargetManager)Invoke(typeof(TargetPowerVerification), "Manager", null, state, context);
            DroneTarget wrapped = manager.Query().Single(t => t.Coordinate.Equals(web.Coordinate));
            Check(wrapped.Contributions.All(c => c.MissionIndex == 2) && wrapped.Contributions[0].ExpectedComplete == 0, "노출색 우선/거미줄 미션 유지/먼지 가림 제외");
            DroneTarget open = manager.Query().Single(t => t.Coordinate.Equals(exposed.Coordinate));
            Check(open.Contributions.Select(c => c.MissionIndex).OrderBy(i => i).SequenceEqual(new[] { 0, 3 }), "노출 일반 색상+먼지 기여 동시 조회");
            Hit(state, exposed.Coordinate, context); Invoke(typeof(DroneTargetManager), "Invalidate", manager);
            wrapped = manager.Query().Single(t => t.Coordinate.Equals(web.Coordinate));
            Check(wrapped.Contributions.Any(c => c.MissionIndex == 0 && c.ExpectedComplete == 0 && c.IsFallback), "노출색 없으면 묶인 색 기여/색 완료0");
            int request = (int)Invoke(typeof(DroneTargetManager), "Request", manager, C(0, 0));
            Check(manager.ExpectedDamage == 1 && manager.ExpectedComplete == 0, "색/거미줄 미션 공동 기여 물리 피해1");
            Hit(state, web.Coordinate, context); Invoke(typeof(DroneTargetManager), "Invalidate", manager);
            Check(manager.ExpectedDamage == 0, "내구도 변경/이번 턴 피해 후 예약 예상 반환");
            Invoke(typeof(DroneTargetManager), "Land", manager, request, C(0, 0)); Check(manager.ReservationCount == 0, "층 변경 재탐색 후 예약 해제");
        }

        private static void CombinationChecks()
        {
            for (int pair = 0; pair < 10; pair++)
            {
                LevelDefinition level = (LevelDefinition)Invoke(typeof(CombinationVerification), "Make", null, pair, RocketDirection.Horizontal, null); Missions(level);
                LevelRuntimeState state = Build(level); Web(state.CellAt(C(4, 6)), 2); Set(state.CellAt(C(4, 6)), "DustDurability", 2);
                Set(state.CellAt(C(4, 7)), "DustDurability", 1); string before = Snapshot(state);
                BoardActionExecutor executor = new BoardActionExecutor(state); BoardActionResult action = executor.Swap(C(4, 4), C(4, 5));
                Check(action.IsApplied && Snapshot(state) == before, "조합10종 층 지원/원본 보존 " + pair);
                Check(executor.State.CellAt(C(4, 6)).Content == RuntimeContent.Normal && executor.State.CellAt(C(4, 6)).DustDurability == 2, "조합 덮개 내용물/먼지 보존 " + pair);
                Check(executor.State.CellAt(C(4, 6)).CoverDurability >= 1, "조합 중첩 거미줄 최대1피해 " + pair);
            }
        }

        private static LevelDefinition CreateFixture(int pair)
        {
            LevelDefinition level = (LevelDefinition)Invoke(typeof(CombinationVerification), "Make", null, pair, RocketDirection.Horizontal, null);
            Invoke(typeof(PowerEffectVerification), "Crate", null, level, C(9, 9), 2);
            Check(LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Cover, Kind = (int)CoverKind.Web, Durability = 2 }, new[] { C(4, 6) }).Changed == 1, "실제 편집 거미줄 배치 " + pair);
            Check(LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Dust, Durability = 2 }, new[] { C(4, 6), C(4, 7), C(9, 9) }).Changed == 3, "실제 편집 먼지 중첩 배치 " + pair);
            JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":0,\"color\":0,\"count\":100},{\"kind\":1,\"count\":1},{\"kind\":2,\"count\":1},{\"kind\":4,\"count\":3}]}", level);
            Check(LevelDefinitionValidator.Validate(level).Count == 0 && new StartConditionReport(LevelStateBuilder.Build(level, 12345).State).IsSatisfied, "실제 4미션/층 배치 및 시작 검증 " + pair);
            return level;
        }
    }
}
