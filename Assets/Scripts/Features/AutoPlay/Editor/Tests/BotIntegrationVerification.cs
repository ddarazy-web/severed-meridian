using System;
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
    /// <summary>종류별 작은 레벨에서 봇 경계와 공통 실행기를 함께 검증한다.</summary>
    public static class BotIntegrationVerification
    {
        private static readonly List<string> Results = new List<string>();
        private static readonly List<LevelDefinition> Owned = new List<LevelDefinition>();

        /// <summary>좁은 대표 사례를 실행하고 소유 메모리 레벨을 모두 정리한다.</summary>
        public static void Run()
        {
            Exception failure = null;
            try { Bodies(); Layers(); Combinations(); Flow(); InitialEnding(); }
            catch (Exception error) { failure = error; Results.Add("FAIL " + error); }
            finally { foreach (LevelDefinition level in Owned) if (level != null) UnityEngine.Object.DestroyImmediate(level); }
            Directory.CreateDirectory("Logs/BotBasicVerification");
            File.WriteAllLines("Logs/BotBasicVerification/integration-results.txt", Results);
            EditorApplication.Exit(failure == null ? 0 : 1);
        }

        /// <summary>정상 막힘과 상태 불일치, 성공 이후 후속 오류를 분리하는 경계 검사.</summary>
        public static void Edges()
        {
            Exception failure = null;
            try
            {
                LevelDefinition blocked = Make(typeof(SettlementVerification), "Make", (object)new[] { C(0, 0), C(0, 1) });
                Place(blocked, C(0, 0), InitialBlockKind.Rocket);
                using (BotPlaySession session = new BotPlaySession(blocked, 12345))
                {
                    Drain(session); session.Begin(false); Drain(session);
                    Check(session.Status == BotSessionStatus.Blocked && session.Outcome.Kind == BoardOutcomeKind.Blocked,
                        "제거 후 유효 행동·재배치가 불가능한 판은 공통 막힘 결과로 종료");
                }
                LevelDefinition mismatch = Make(typeof(ItemBoosterVerification), "PlayFixture");
                using (BotPlaySession session = new BotPlaySession(mismatch, 12345))
                {
                    Drain(session);
                    // 외부 변조로 발생한 불일치를 주입한다. 정상 경로에서는 공통 실행기가
                    // 안정 경계에서 재배치/막힘을 판정하므로 이 상태를 반환하지 않는다.
                    foreach (RuntimeCell cell in session.State.Cells)
                    { Set(cell, "Content", RuntimeContent.Empty); Set(cell, "Color", null); Set(cell, "RocketDirection", null); }
                    string before = Snapshot(session.State);
                    session.Begin(true); session.Advance();
                    Check(session.Status == BotSessionStatus.Error && session.Outcome == null && session.Message.Contains("후보"),
                        "입력 가능한데 후보가 없는 불일치는 패배 대신 실행 오류");
                    Check(Snapshot(session.State) == before && session.Records.Count == 0, "후보 없음 오류에서 임의 섞기·이동 차감 없음");
                }
                LevelDefinition won = Make(typeof(RecoveryVerification), "Make", 1);
                Invoke(typeof(RecoveryVerification), "Place", won, C(9, 0)); Place(won, C(9, 9), InitialBlockKind.Rocket);
                using (BotPlaySession session = new BotPlaySession(won, 12345))
                {
                    int remaining = 2000;
                    while (session.Outcome == null && session.NeedsAdvance && remaining-- > 0) session.Advance();
                    Check(session.Outcome?.Kind == BoardOutcomeKind.Won && session.NeedsAdvance, "성공 확정과 라스트팡 진행 중 경계 확보");
                    BoardActionExecutor executor = (BoardActionExecutor)typeof(BotPlaySession).GetField("executor", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(session);
                    // 수백 번의 동일 파워 연쇄를 만들지 않고 공통 실행기의 실제 한도 분기를
                    // 강제로 도달시킨다. 오류 판정 자체는 다음 AdvanceCascade가 수행한다.
                    Set(executor, "LastPangWaves", executor.CascadeLimit);
                    session.Advance();
                    Check(session.Status == BotSessionStatus.Error && session.Outcome.Kind == BoardOutcomeKind.Won && session.Message.Contains("라스트팡"),
                        "라스트팡 실행 오류는 성공 기록을 유지하면서 세션 오류로 구분");
                }
            }
            catch (Exception error) { failure = error; Results.Add("FAIL " + error); }
            finally { foreach (LevelDefinition level in Owned) if (level != null) UnityEngine.Object.DestroyImmediate(level); }
            File.WriteAllLines("Logs/BotBasicVerification/edge-results.txt", Results);
            EditorApplication.Exit(failure == null ? 0 : 1);
        }

        /// <param name="target">검사 사본.</param><param name="property">경계 조건 속성.</param><param name="value">주입 값.</param>
        private static void Set(object target, string property, object value)
            => target.GetType().GetProperty(property).GetSetMethod(true).Invoke(target, new[] { value });

        /// <summary>모든 본체 종류에서 기본 전략의 선택이 실제로 목표를 진행시키는지 확인한다.</summary>
        private static void Bodies()
        {
            foreach (ObstacleKind kind in new[] { ObstacleKind.Crate, ObstacleKind.Scrap, ObstacleKind.Safe, ObstacleKind.ColorLock, ObstacleKind.Appliance })
            {
                LevelDefinition level = Make(typeof(PowerEffectVerification), "Make");
                Invoke(typeof(FixedObstacleVerification), "Obstacle", level, kind, 3, C(4, 4), RabbitColor.Type1);
                Place(level, C(4, 3), InitialBlockKind.Bomb);
                MissionKind mission = kind switch { ObstacleKind.Crate => MissionKind.Crate, ObstacleKind.Scrap => MissionKind.Scrap,
                    ObstacleKind.Safe => MissionKind.Safe, ObstacleKind.ColorLock => MissionKind.ColorLock, _ => MissionKind.Appliance };
                Mission(level, mission);
                BoardActionExecutor result = Compare(level, kind.ToString());
                Check(result.State.Obstacles[0].Durability < 3, kind + " 기본 전략 선택으로 실제 유효 피해");
            }
            LevelDefinition generator = Make(typeof(GeneratorVerification), "Make", ObstacleKind.Crate, 3);
            Place(generator, C(4, 3), InitialBlockKind.Bomb);
            BoardActionExecutor charged = Compare(generator, "발전기 연결");
            Check(charged.State.Obstacles[0].Charge == 1 && GeneratorRules.ActiveConnections(charged.State).Count == 1,
                "발전기 기본 선택은 1충전·연결 유지");
        }

        /// <summary>거미줄·곰팡이·먼지를 실제 배치한 레벨에서 목표 진행과 내용물 처리 경계를 확인한다.</summary>
        private static void Layers()
        {
            foreach (MissionKind kind in new[] { MissionKind.Web, MissionKind.Mold, MissionKind.Dust })
            {
                LevelDefinition level = Make(typeof(PowerEffectVerification), "Make");
                string layer = kind == MissionKind.Dust ? "\"dust\":[{\"coordinate\":{\"row\":4,\"column\":4},\"durability\":1}]" :
                    "\"covers\":[{\"coordinate\":{\"row\":4,\"column\":4},\"kind\":" + (kind == MissionKind.Web ? 0 : 1) + ",\"durability\":1}]";
                JsonUtility.FromJsonOverwrite("{" + layer + "}", level);
                Place(level, C(4, 3), InitialBlockKind.Bomb); Mission(level, kind);
                BoardActionExecutor result = Compare(level, kind.ToString());
                Check(result.State.Missions[0].Progress == 1, kind + " 기본 선택으로 실제 목표 완료");
            }
        }

        /// <summary>공개 조합 후보를 제출하여 10개 조합 모두 같은 공통 효과·후속 처리 경로를 통과시킨다.</summary>
        private static void Combinations()
        {
            for (int pair = 0; pair < 10; pair++)
            {
                LevelDefinition level = Make(typeof(CombinationVerification), "Make", pair, RocketDirection.Horizontal, null);
                Compare(level, "파워 조합 " + pair, observation => observation.Actions.Single(action =>
                    action.Kind == BotActionKind.CombinationSwap && action.First.Equals(C(4, 4)) && action.Second.Value.Equals(C(4, 5))));
            }
        }

        /// <summary>네 방향 중력·고정 공급과 직접 경로·회수·통로를 별개의 작은 사례로 검증한다.</summary>
        private static void Flow()
        {
            foreach (GravityDirection direction in Enum.GetValues(typeof(GravityDirection)))
            {
                bool horizontal = direction == GravityDirection.Left || direction == GravityDirection.Right;
                BoardCoordinate[] cells = Enumerable.Range(2, 3).Select(i => horizontal ? C(2, i) : C(i, 2)).ToArray();
                bool reverse = direction == GravityDirection.Left || direction == GravityDirection.Up;
                BoardCoordinate source = reverse ? cells[2] : cells[0], end = reverse ? cells[0] : cells[2];
                LevelDefinition level = Make(typeof(SettlementVerification), "Make", (object)cells);
                string error = LevelFlowEditing.SetGravity(level, cells, direction);
                if (error != null) throw new InvalidOperationException(error);
                Place(level, end, InitialBlockKind.Rocket, horizontal ? RocketDirection.Horizontal : RocketDirection.Vertical);
                Invoke(typeof(SettlementVerification), "Source", level, source, SupplyExhaustion.Stop,
                    new[] { new SupplyItem(SupplyKind.FixedNormal, 1, RabbitColor.Type1), new SupplyItem(SupplyKind.FixedNormal, 1, RabbitColor.Type2), new SupplyItem(SupplyKind.FixedNormal, 1, RabbitColor.Type3) });
                JsonUtility.FromJsonOverwrite("{\"moveCount\":1}", level);
                BoardActionExecutor result = Compare(level, "중력·고정 공급 " + direction,
                    observation => observation.Actions.Single(action => action.Kind == BotActionKind.Activate && action.First.Equals(end)));
                SettlementRecord[] moves = result.CascadeHistory.Where(step => step.Settlement != null).SelectMany(step => step.Settlement.Records).ToArray();
                Check(moves.Any(move => move.Kind == MovementKind.Gravity) && moves.Count(move => move.Kind == MovementKind.Supply) == 3,
                    direction + " 실제 중력 이동·고정 목록 3개 소비");
            }
            LevelDefinition recovery = Make(typeof(RecoveryVerification), "PlayFixture");
            BoardActionExecutor collected = Compare(recovery, "경로·회수·도착·부품 공급", observation => observation.Actions.Single(action =>
                action.Kind == BotActionKind.Activate && action.First.Equals(C(9, 0))));
            Check(collected.State.Recoveries.Count == 2 && collected.Outcome?.Kind == BoardOutcomeKind.Won,
                "직접 경로를 통한 기존 부품·공급 부품의 도착 회수");

            BoardCoordinate[] portalCells = { C(0, 0), C(1, 0), C(1, 1), C(4, 4), C(4, 5) };
            LevelDefinition portal = Make(typeof(SettlementVerification), "Make", (object)portalCells);
            string portalError = LevelFlowEditing.SetPortal(portal, C(0, 0), C(4, 4));
            if (portalError != null) throw new InvalidOperationException(portalError);
            Place(portal, C(4, 4), InitialBlockKind.Rocket);
            JsonUtility.FromJsonOverwrite("{\"moveCount\":1}", portal);
            BoardActionExecutor transported = Compare(portal, "입출구 통로", observation => observation.Actions.Single(action =>
                action.Kind == BotActionKind.Activate && action.First.Equals(C(4, 4))));
            Check(transported.CascadeHistory.Where(step => step.Settlement != null).SelectMany(step => step.Settlement.Records)
                .Any(move => move.Kind == MovementKind.Portal), "실제 통로 입구에서 출구로 이동");
        }

        /// <summary>입력 전에 회수 목표가 완료되는 판도 공통 초기 처리와 라스트팡을 따른다.</summary>
        private static void InitialEnding()
        {
            LevelDefinition level = Make(typeof(RecoveryVerification), "Make", 1);
            Invoke(typeof(RecoveryVerification), "Place", level, C(9, 0));
            Place(level, C(9, 9), InitialBlockKind.Rocket);
            using (BotPlaySession session = new BotPlaySession(level, 12345))
            {
                Drain(session);
                Check(session.Status == BotSessionStatus.Won && session.Records.Count == 0 && session.State.Recoveries.Count == 1,
                    "시작부터 회수 완료인 판은 봇 입력 없이 성공·라스트팡 완료");
            }
        }

        /// <summary>기본 선택 또는 지정한 공개 후보를 양쪽 경로로 실행해 사본·효과·난수까지 비교한다.</summary>
        /// <param name="level">검사 레벨.</param><param name="name">사례 이름.</param><param name="select">명령 종류를 고정할 경우 후보 선택.</param>
        /// <returns>검사가 끝난 공통 수동 실행기. 사례별 실제 효과를 추가 확인한다.</returns>
        private static BoardActionExecutor Compare(LevelDefinition level, string name, Func<BotObservation, BotAction> select = null)
        {
            string original = JsonUtility.ToJson(level); bool dirty = EditorUtility.IsDirty(level);
            using BotPlaySession session = new BotPlaySession(level, 12345);
            Drain(session);
            Check(session.Status == BotSessionStatus.Ready, name + " 시작 조건 통과: " + session.Message);
            BotObservation observation = session.Observe();
            BotAction action = select == null ? BasicBotStrategy.Choose(observation).Action : select(observation);
            BoardActionExecutor manual = new BoardActionExecutor(StartingBoardBuilder.Build(level, 12345).State);
            Check(session.TrySubmit(observation, action), name + " 공개 후보 제출 수락");
            BoardActionResult applied = action.Kind == BotActionKind.Activate ? manual.Activate(action.First) : manual.Swap(action.First, action.Second.Value);
            Check(applied.IsApplied, name + " 동일 수동 명령 수락");
            int remaining = 20000;
            while (manual.HasPendingCascade && remaining-- > 0) manual.AdvanceCascade();
            if (manual.HasPendingCascade) throw new InvalidOperationException(name + " 수동 후속 처리 한도");
            Drain(session);
            BoardActionExecutor bot = (BoardActionExecutor)typeof(BotPlaySession).GetField("executor", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(session);
            Check(Snapshot(manual.State) == Snapshot(session.State) && Snapshot(manual.Outcome) == Snapshot(session.Outcome), name + " 상태·미션·난수·종료 동등");
            Check(Snapshot(manual.TurnEffects) == Snapshot(bot.TurnEffects) && Snapshot(manual.CascadeHistory) == Snapshot(bot.CascadeHistory),
                name + " 피해·드론 예약·보호·이동·공급 기록 동등");
            if (bot.Phase == BoardActionPhase.Ready && bot.Outcome == null)
            {
                string before = Snapshot(bot.State) + Snapshot(bot.TurnEffects) + Snapshot(bot.CascadeHistory);
                BotObservation next = BotObservationBuilder.Capture(bot);
                foreach (BotAction candidate in next.Actions) BasicBotStrategy.Evaluate(next, candidate);
                BasicBotStrategy.Choose(next);
                Check(before == Snapshot(bot.State) + Snapshot(bot.TurnEffects) + Snapshot(bot.CascadeHistory),
                    name + " 이전 턴의 피해·드론 예약 기록이 있는 상태도 관찰·평가 무변경");
            }
            Check(JsonUtility.ToJson(level) == original && EditorUtility.IsDirty(level) == dirty, name + " 원본 JSON·dirty 보존");
            return manual;
        }

        /// <param name="session">진행할 검사 세션.</param>
        private static void Drain(BotPlaySession session)
        {
            int remaining = 20000;
            while (session.NeedsAdvance && remaining-- > 0) session.Advance();
            if (session.NeedsAdvance) throw new InvalidOperationException("검사 진행 한도");
        }
        /// <param name="type">기존 검사 클래스.</param><param name="method">구성 함수.</param><param name="args">구성 인자.</param><returns>반환 값.</returns>
        private static object Invoke(Type type, string method, params object[] args) => type.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
        /// <param name="type">기존 구성 소유자.</param><param name="method">레벨 구성 함수.</param><param name="args">인자.</param><returns>소유권을 기록한 메모리 레벨.</returns>
        private static LevelDefinition Make(Type type, string method, params object[] args)
        { LevelDefinition level = (LevelDefinition)Invoke(type, method, args); Owned.Add(level); return level; }
        /// <param name="level">검사 사본.</param><param name="at">배치 칸.</param><param name="kind">파워 종류.</param><param name="direction">로켓 방향.</param>
        private static void Place(LevelDefinition level, BoardCoordinate at, InitialBlockKind kind, RocketDirection direction = RocketDirection.Horizontal)
            => Invoke(typeof(PowerEffectVerification), "Place", level, at, kind, direction, RabbitColor.Type1);
        /// <param name="level">검사 사본.</param><param name="kind">검사할 목표 종류.</param>
        private static void Mission(LevelDefinition level, MissionKind kind) => JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":" + (int)kind + ",\"count\":1}]}", level);
        /// <param name="row">행.</param><param name="column">열.</param><returns>좌표.</returns>
        private static BoardCoordinate C(int row, int column) => new BoardCoordinate(row, column);
        /// <param name="pass">검사 결과.</param><param name="name">검사명.</param>
        private static void Check(bool pass, string name) { if (!pass) throw new InvalidOperationException(name); Results.Add("PASS " + name); }
        /// <param name="value">비교 값.</param><returns>결정적 상태 표현.</returns>
        private static string Snapshot(object value) => (string)Invoke(typeof(LevelInitialStateVerification), "Snapshot", value);
    }
}
