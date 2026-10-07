using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AutoPlay;
using Board;
using Simulation;
using UnityEditor;
using UnityEngine;

namespace Levels.Editor
{
    public static class BotObservationBaselineVerification
    {
        private const string Evidence = "Logs/ElementFramework/Stage12";
        private static readonly List<string> Results = new List<string>();
        private static readonly List<string> Rows = new List<string>();
        private static readonly List<LevelDefinition> Owned = new List<LevelDefinition>();
        private static void Check(bool value, string name) { if (!value) throw new InvalidOperationException(name); Results.Add("PASS " + name); }
        private static string Snapshot(object value) => (string)typeof(LevelInitialStateVerification).GetMethod("Snapshot", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new[] { value });
        private static void Set(object owner, string property, object value) => owner.GetType().GetProperty(property).GetSetMethod(true).Invoke(owner, new[] { value });
        private static LevelDefinition Make()
        {
            LevelDefinition level = (LevelDefinition)typeof(PowerEffectVerification).GetMethod("Make", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null); Owned.Add(level); return level;
        }
        private static BoardActionExecutor Build(LevelDefinition level, int seed = 12345)
        {
            LevelStateBuildResult built = LevelStateBuilder.Build(level, seed);
            if (!built.IsBuilt) throw new InvalidOperationException(string.Join(" | ", built.Issues)); return new BoardActionExecutor(built.State);
        }
        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 Editor에서만 실행한다.");
            Directory.CreateDirectory(Evidence); int exit = 0;
            try
            {
                LevelDefinition normal = Make(); BoardActionExecutor original = Build(normal);
                BotObservation first = Observe("normal", normal, original);
                Check(first.Rows == 9 && first.Columns == 9 && first.Cells.Count == 81 && first.Bodies.Count == 0, "정상81칸/본체0 실제 값");
                string frozen = Snapshot(first); Set(original.State.CellAt(new BoardCoordinate(0, 1)), "Color", RabbitColor.Type5);
                Check(Snapshot(first) == frozen && Snapshot(Observe("visible-change", normal, original)) != frozen, "공개 변경은 새 관찰에만 반영");
                Check(first.MovesRemaining == original.State.MovesRemaining && first.Missions.Single().Kind == MissionKind.Color && first.Missions.Single().Color == RabbitColor.Type1 && first.Missions.Single().Remaining == 12, "남은 이동/색 미션 Type1·12 실제 값");
                Set(original.State.Missions[0], "Progress", 3);
                BotObservation progressed = Observe("mission-progress", normal, original, mutation: "Color(0,1)=Type5; mission.Progress=3");
                Check(progressed.Missions.Single().Remaining == 9 && first.Missions.Single().Remaining == 12 && Snapshot(first) == frozen, "미션 진행12→9·과거 스냅샷 독립");

                LevelDefinition matching = Make();
                foreach (BoardCoordinate cell in new[] { new BoardCoordinate(3, 3), new BoardCoordinate(3, 5), new BoardCoordinate(2, 4) })
                    typeof(PowerEffectVerification).GetMethod("Place", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { matching, cell, InitialBlockKind.FixedNormal, RocketDirection.Horizontal, RabbitColor.Type1 });
                typeof(PowerEffectVerification).GetMethod("Place", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { matching, new BoardCoordinate(3, 4), InitialBlockKind.FixedNormal, RocketDirection.Horizontal, RabbitColor.Type2 });
                BotObservation matches = Observe("matching-candidates", matching, Build(matching));
                Check(matches.Actions.Any(action => action.Kind == BotActionKind.MatchSwap && action.Matches.Any(match => match.Color == RabbitColor.Type1 && match.Cells.Contains(new BoardCoordinate(3, 3)) && match.Cells.Contains(new BoardCoordinate(3, 4)) && match.Cells.Contains(new BoardCoordinate(3, 5)))), "일반 교환 매칭 후보·색·3칸 실제 값");

                foreach (InitialBlockKind kind in new[] { InitialBlockKind.Rocket, InitialBlockKind.Bomb, InitialBlockKind.Drone, InitialBlockKind.Magnet })
                {
                    LevelDefinition level = Make(); BoardCoordinate at = new BoardCoordinate(4, 4);
                    typeof(PowerEffectVerification).GetMethod("Place", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { level, at, kind, RocketDirection.Vertical, RabbitColor.Type1 });
                    BotObservation view = Observe("power-" + kind, level, Build(level));
                    Check(view.CellAt(at).Content.ToString() == kind.ToString() && view.Actions.Any(action => action.Kind == BotActionKind.Activate && action.First.Equals(at)), "공개 파워/발동 후보 " + kind);
                    Check(view.CellAt(at).Color == null && (kind != InitialBlockKind.Rocket || view.CellAt(at).RocketDirection == RocketDirection.Vertical), "파워 색 비노출/로켓 방향 " + kind);
                    if (kind == InitialBlockKind.Rocket)
                    {
                        typeof(PowerEffectVerification).GetMethod("Place", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { level, new BoardCoordinate(4, 5), InitialBlockKind.Bomb, RocketDirection.Horizontal, RabbitColor.Type1 });
                        BotObservation combined = Observe("power-combination", level, Build(level));
                        Check(combined.Actions.Any(action => action.Kind == BotActionKind.CombinationSwap && action.First.Equals(at) && action.Second.Value.Equals(new BoardCoordinate(4, 5))), "인접 로켓/폭탄 조합 후보 실제 값");
                    }
                }
                foreach (ObstacleKind kind in new[] { ObstacleKind.Crate, ObstacleKind.Scrap, ObstacleKind.Safe, ObstacleKind.ColorLock, ObstacleKind.Appliance })
                {
                    LevelDefinition level = Make(); BoardCoordinate at = new BoardCoordinate(3, 3);
                    typeof(FixedObstacleVerification).GetMethod("Obstacle", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { level, kind, 1, at, RabbitColor.Type3 });
                    BoardActionExecutor executor = Build(level); BotObservation view = Observe("body-" + kind, level, executor);
                    BotBody body = view.Bodies.Single(); int size = LevelPlacementRules.Size(kind);
                    Check(body.Key == 30 && body.Kind == kind && body.Durability == 1 && body.Cells.Count == size * size && body.Cells.All(cell => view.CellAt(cell).BodyKey == 30), "좌표 키/내구도/점유 " + kind);
                    Check(body.Color == (kind == ObstacleKind.ColorLock ? RabbitColor.Type3 : (RabbitColor?)null), "공개 본체 색 제한 " + kind);
                    string saved = Snapshot(view);
                    foreach (RuntimeCell cell in executor.State.Cells.Where(cell => cell.ObstacleIndex == 0)) { Set(cell, "Content", RuntimeContent.Empty); Set(cell, "ObstacleIndex", null); Set(cell, "Color", null); }
                    Set(executor.State.Obstacles[0], "Durability", 0);
                    BotObservation removed = Observe("removed-" + kind, level, executor);
                    Check(removed.Bodies.Count == 0 && removed.Cells.All(cell => cell.BodyKey == null) && Snapshot(view) == saved, "제거 본체/점유 유령 없음·과거 스냅샷 보존 " + kind);
                }
                LevelDefinition generator = (LevelDefinition)typeof(GeneratorVerification).GetMethod("Make", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { ObstacleKind.Crate, 3 }); Owned.Add(generator);
                LevelFlowEditing.SetWalls(generator, new[] { new BoardEdge(new BoardCoordinate(0, 0), new BoardCoordinate(1, 0)) }, false);
                LevelFlowEditing.SetPortal(generator, new BoardCoordinate(1, 1), new BoardCoordinate(2, 1));
                LevelFlowEditing.SetArrival(generator, new BoardCoordinate(8, 0), false);
                BoardActionExecutor machine = Build(generator); Set(machine.State.Obstacles.First(body => body.Definition.Kind == ObstacleKind.Generator), "Charge", 2);
                BotObservation publicMachine = Observe("generator-devices", generator, machine, mutation: "generator.Charge=2");
                BotBody publicGenerator = publicMachine.Bodies.Single(body => body.Kind == ObstacleKind.Generator);
                Check(publicGenerator.RequiredCharge == 3 && publicGenerator.Charge == 2 && publicGenerator.Cells.Count == 4 && publicGenerator.ConnectedTargets.Count == 1, "발전기 충전2/목표3/점유4/활성연결1");
                Check(publicMachine.Walls.Count == 1 && publicMachine.Portals.Count == 1 && publicMachine.Arrivals.Count == 1 && publicMachine.Missions.Count > 0, "벽/통로/출구/미션 공개");
                LevelDefinition history = UnityEngine.Object.Instantiate(generator); Owned.Add(history);
                string renamed = JsonUtility.ToJson(history);
                for (int i = 0; i < generator.Obstacles.Count; i++) renamed = renamed.Replace(generator.Obstacles[i].Id, "history-id-" + i);
                JsonUtility.FromJsonOverwrite(renamed, history);
                using (SerializedObject data = new SerializedObject(history)) { data.FindProperty("obstacles").MoveArrayElement(0, 1); data.ApplyModifiedPropertiesWithoutUndo(); }
                BoardActionExecutor other = Build(history); Set(other.State.Obstacles.First(body => body.Definition.Kind == ObstacleKind.Generator), "Charge", 2);
                Check(machine.State.Obstacles[0].Definition.Id != other.State.Obstacles[0].Definition.Id && machine.State.Obstacles[0].Definition.Kind != other.State.Obstacles[0].Definition.Kind, "비교 입력 ID/본체 배열 순서 실제 차이");
                Check(Snapshot(publicMachine) == Snapshot(Observe("history-renamed-reordered", history, other, mutation: "renamed IDs; reordered bodies; generator.Charge=2")), "숨은 ID/인덱스 변경 관찰·공개 후보 동일");
                HiddenChecks();
                typeof(BotObservationVerification).GetMethod("CheckPublicGraph", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { typeof(BotObservation), new HashSet<Type>() });
                Check(true, "중첩 DTO 형식 계약 재사용·내부 정의/상태/난수 참조 없음");
                foreach (PropertyInfo property in typeof(BotObservation).GetProperties())
                    if (property.GetValue(first) is IList list) Check(list.IsReadOnly && list.IsFixedSize, "관찰 컬렉션 쓰기 차단 " + property.Name);
            }
            catch (Exception error) { Results.Add("FAIL " + error); Debug.LogException(error); exit = 1; }
            finally
            {
                foreach (LevelDefinition level in Owned) if (level != null) UnityEngine.Object.DestroyImmediate(level);
                File.WriteAllLines(Evidence + "/baseline-results.txt", Results); File.WriteAllLines(Evidence + "/observation-values.jsonl", Rows);
            }
            EditorApplication.Exit(exit);
        }
        private static void HiddenChecks()
        {
            LevelDefinition level = (LevelDefinition)typeof(ItemBoosterVerification).GetMethod("PlayFixture", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null); Owned.Add(level);
            BoardActionExecutor executor = Build(level);
            BoardCoordinate hidden = new BoardCoordinate(0, 0);
            Set(executor.State.CellAt(hidden), "Cover", CoverKind.Mold); Set(executor.State.CellAt(hidden), "CoverDurability", 1);
            Set(executor.State.CellAt(new BoardCoordinate(2, 2)), "Cover", CoverKind.Web); Set(executor.State.CellAt(new BoardCoordinate(2, 2)), "CoverDurability", 2);
            Set(executor.State.CellAt(new BoardCoordinate(2, 3)), "DustDurability", 2);
            BotObservation a = Observe("hidden-mold-A", level, executor, mutation: "Mold(0,0),Web2(2,2),Dust2(2,3)");
            Check(a.Actions.Count > 0, "숨은 정보 비교 입력의 실제 행동 후보 존재");
            Check(a.CellAt(hidden).Content == BotContent.Unknown && a.CellAt(hidden).Color == null && a.CellAt(hidden).BodyKey == null && a.CellAt(hidden).RocketDirection == null, "곰팡이 Unknown/내부 값 비노출");
            Check(a.CellAt(new BoardCoordinate(2, 2)).CoverDurability == 2 && a.CellAt(new BoardCoordinate(2, 3)).DustDurability == 2, "덮개/먼지 실제 내구도2");
            Check(a.Actions.All(action => !action.First.Equals(hidden) && (!action.Second.HasValue || !action.Second.Value.Equals(hidden)) && action.Matches.All(match => !match.Cells.Contains(hidden))), "가려진 칸 행동/매칭 후보 없음");
            Set(executor.State.CellAt(hidden), "Content", RuntimeContent.Rocket); Set(executor.State.CellAt(hidden), "Color", RabbitColor.Type5); Set(executor.State.CellAt(hidden), "RocketDirection", RocketDirection.Vertical);
            typeof(SimulationRandom).GetMethod("Next", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(executor.State.Random, new object[] { 100 });
            BotObservation b = Observe("hidden-mold-B", level, executor, mutation: "A plus hidden Rocket/Type5/Vertical; Random.Next(100)");
            Check(Snapshot(a) == Snapshot(b), "곰팡이 숨은 내용/방향/난수 쌍 관찰·후보 동일");
            TurnEffectContext context = (TurnEffectContext)Activator.CreateInstance(typeof(TurnEffectContext), BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] { 1, Array.Empty<MatchedBlockChange>() }, null);
            Set(executor, "TurnEffects", context);
            DroneTargetManager targets = (DroneTargetManager)Activator.CreateInstance(typeof(DroneTargetManager), BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] { executor.State, context }, null);
            typeof(DroneTargetManager).GetMethod("Request", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(targets, new object[] { new BoardCoordinate(4, 4) });
            Check(targets.ReservationCount == 1, "실제 비공개 드론 예약1 생성");
            BotObservation reserved = Observe("hidden-reservation", level, executor, mutation: "DroneTargetManager.Request(4,4); reservation=1; selection may advance RNG");
            Check(Snapshot(b) == Snapshot(reserved), "예약/조회 캐시 변화에도 공개 관찰·후보 동일");
            string supplyBefore = null;
            foreach (int variant in new[] { 0, 1 })
            {
                JsonUtility.FromJsonOverwrite(variant == 0 ? "{\"supply\":{\"sources\":[{\"coordinate\":{\"row\":0,\"column\":0},\"mode\":1,\"items\":[{\"kind\":1,\"count\":2,\"color\":0}]}]}}" : "{\"supply\":{\"sources\":[{\"coordinate\":{\"row\":0,\"column\":0},\"mode\":1,\"items\":[{\"kind\":3,\"count\":3},{\"kind\":1,\"count\":7,\"color\":4}]}]}}", level);
                BoardActionExecutor supply = Build(level, variant == 0 ? 123 : 987);
                if (variant == 1) { Set(supply.State.Supply.Sources[0], "ItemIndex", 1); Set(supply.State.Supply.Sources[0], "ItemConsumed", 1); }
                BotObservation visible = Observe("hidden-supply-" + variant, level, supply, variant == 0 ? 123 : 987, "fixed initial cells; supply list/seed/cursor variant " + variant);
                if (variant == 0) { a = visible; supplyBefore = Snapshot(supply.State.Supply); }
                else { Check(supplyBefore != Snapshot(supply.State.Supply), "비교 입력 공급/커서 실제 차이"); Check(Snapshot(a) == Snapshot(visible), "숨은 공급/커서/시드 변경 공개 관찰·후보 동일"); }
            }
        }
        [Serializable] private sealed class Observation
        {
            public string name, mutation, inputJson, stateBefore, stateAfter, observation, actions, turnEffects;
            public int seed, drawBefore, drawAfter, cellCount, bodyCount, missionCount, actionCount;
        }
        private static BotObservation Observe(string name, LevelDefinition level, BoardActionExecutor executor, int seed = 12345, string mutation = null)
        {
            string before = Snapshot(executor.State), input = JsonUtility.ToJson(level), effects = Snapshot(executor.TurnEffects), global = JsonUtility.ToJson(UnityEngine.Random.state);
            int draws = executor.State.Random.DrawCount; BotObservation view = BotObservationBuilder.Capture(executor);
            Check(before == Snapshot(executor.State) && input == JsonUtility.ToJson(level) && effects == Snapshot(executor.TurnEffects) && draws == executor.State.Random.DrawCount && global == JsonUtility.ToJson(UnityEngine.Random.state), name + " 조회 전후 원본/정의/턴/난수 보존");
            Check(Snapshot(view) == Snapshot(BotObservationBuilder.Capture(executor)), name + " 반복 조회 동일");
            Rows.Add(JsonUtility.ToJson(new Observation { name = name, mutation = mutation, inputJson = input, seed = seed, stateBefore = before, stateAfter = Snapshot(executor.State), turnEffects = effects,
                drawBefore = draws, drawAfter = executor.State.Random.DrawCount, observation = Snapshot(view), actions = Snapshot(view.Actions), cellCount = view.Cells.Count, bodyCount = view.Bodies.Count, missionCount = view.Missions.Count, actionCount = view.Actions.Count }));
            return view;
        }
    }
}
