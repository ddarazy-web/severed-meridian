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
    /// <summary>공개 관찰의 불변성과 숨은 정보 차단을 확인하는 전용 프로세스 검증.</summary>
    public static class BotObservationVerification
    {
        private static readonly List<string> Results = new List<string>();

        /// <summary>임시 메모리 레벨만 검사하고 검증 전용 Unity를 종료한다.</summary>
        public static void Run()
        {
            LevelDefinition level = null;
            Exception failure = null;
            try
            {
                level = (LevelDefinition)typeof(ItemBoosterVerification).GetMethod("PlayFixture", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                BoardActionExecutor a = new BoardActionExecutor(LevelStateBuilder.Build(level, 123).State);
                BoardCoordinate hiddenAt = new BoardCoordinate(0, 0);
                Set(a.State.CellAt(hiddenAt), "Cover", CoverKind.Mold);
                Set(a.State.CellAt(hiddenAt), "CoverDurability", 1);
                string before = Snapshot(a.State);
                BotObservation first = BotObservationBuilder.Capture(a);
                Check(Snapshot(a.State) == before, "관찰·후보 조회 후 상태와 난수 보존");
                Check(first.CellAt(hiddenAt).Content == BotContent.Unknown && first.CellAt(hiddenAt).Color == null &&
                    first.CellAt(hiddenAt).RocketDirection == null && first.CellAt(hiddenAt).BodyKey == null, "곰팡이 내부는 Unknown으로 가림");
                string observed = Snapshot(first);
                string choice = Snapshot(BasicBotStrategy.Choose(first));
                string scores = string.Join("\n", first.Actions.Select(action => Snapshot(BasicBotStrategy.Evaluate(first, action))));
                Check(Snapshot(a.State) == before, "전체 후보 평가·선택 후 실행 상태 보존");
                Set(a.State.CellAt(hiddenAt), "Content", RuntimeContent.Rocket);
                Set(a.State.CellAt(hiddenAt), "Color", RabbitColor.Type5);
                Set(a.State.CellAt(hiddenAt), "RocketDirection", RocketDirection.Horizontal);
                typeof(SimulationRandom).GetMethod("Next", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(a.State.Random, new object[] { 100 });
                Check(Snapshot(BotObservationBuilder.Capture(a)) == observed, "숨은 색·파워·방향·난수 변경에도 관찰과 후보 동일");
                BotObservation hiddenChanged = BotObservationBuilder.Capture(a);
                Check(Snapshot(BasicBotStrategy.Choose(hiddenChanged)) == choice, "숨은 정보 변경에도 기본 전략 선택 동일");
                Check(string.Join("\n", hiddenChanged.Actions.Select(action => Snapshot(BasicBotStrategy.Evaluate(hiddenChanged, action)))) == scores,
                    "숨은 정보 변경에도 모든 후보 점수·이유 동일");
                Check(Snapshot(first) == observed, "관찰 후 원본 변경이 이전 관찰에 반영되지 않음");
                bool readOnly = false;
                try { ((IList<BotCell>)first.Cells)[0] = first.Cells[1]; }
                catch (NotSupportedException) { readOnly = true; }
                Check(readOnly, "관찰 컬렉션 수정 차단");
                Set(a.State.CellAt(new BoardCoordinate(0, 1)), "Color", RabbitColor.Type5);
                Check(Snapshot(first) == observed && Snapshot(BotObservationBuilder.Capture(a)) != observed, "공개 값 변경은 새 관찰에만 반영");
                Check(first.Actions.Count > 0, "유효 행동 후보 존재");
                Check(first.Actions.All(action => !action.First.Equals(hiddenAt) && (!action.Second.HasValue || !action.Second.Value.Equals(hiddenAt)) &&
                    action.Matches.All(match => !match.Cells.Contains(hiddenAt))), "가려진 칸은 조작·매칭 후보에 없음");
                // 선언된 관찰 API에 런타임 상태나 에셋 참조를 새로 끼워 넣는 회귀를 잡는다.
                Type[] contracts = { typeof(BotObservation), typeof(BotCell), typeof(BotBody), typeof(BotMission), typeof(BotAction), typeof(BotMatch) };
                foreach (Type contract in contracts)
                {
                    Check(contract.GetProperties().All(property => property.SetMethod == null), "관찰 속성 쓰기 차단 " + contract.Name);
                    Check(contract.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).All(field =>
                        !typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType) && field.FieldType != typeof(LevelRuntimeState) &&
                        field.FieldType != typeof(BoardActionExecutor) && field.FieldType != typeof(SimulationRandom)), "직접 내부 참조 없음 " + contract.Name);
                }
                CheckPublicGraph(typeof(BotObservation), new HashSet<Type>());
                Check(true, "공개 계약 중첩 필드까지 허용 값·복사 컬렉션만 포함");
                BoardActionExecutor running = new BoardActionExecutor(LevelStateBuilder.Build(level, 123).State);
                ActionCandidate candidate = ActionQuery.Find(running.State).First();
                if (candidate.Kind == QueryActionKind.Activate) running.Activate(candidate.First);
                else running.Swap(candidate.First, candidate.Second.Value);
                bool rejected = false;
                try { BotObservationBuilder.Capture(running); }
                catch (InvalidOperationException) { rejected = true; }
                Check(rejected, "후속 처리 중 관찰 차단");
                CheckHiddenSupply(level);
            }
            catch (Exception error) { failure = error; Results.Add("FAIL " + error); Debug.LogException(error); }
            finally { if (level != null) UnityEngine.Object.DestroyImmediate(level); }
            Directory.CreateDirectory("Logs/BotBasicVerification");
            File.WriteAllLines("Logs/BotBasicVerification/observation-results.txt", Results);
            EditorApplication.Exit(failure == null ? 0 : 1);
        }

        /// <param name="pass">조건 충족 여부.</param><param name="name">검사 이름.</param>
        private static void Check(bool pass, string name)
        { if (!pass) throw new InvalidOperationException(name); Results.Add("PASS " + name); }

        /// <param name="value">기존 검증 도구로 직렬화할 객체.</param><returns>공개 속성의 결정적 표현.</returns>
        private static string Snapshot(object value) => (string)typeof(LevelInitialStateVerification)
            .GetMethod("Snapshot", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new[] { value });

        /// <param name="target">검증 사본.</param><param name="property">바꿀 내부 속성.</param><param name="value">변경할 값.</param>
        private static void Set(object target, string property, object value)
            => target.GetType().GetProperty(property).GetSetMethod(true).Invoke(target, new[] { value });

        /// <summary>첫 관찰만 같고 미래 공급과 실행 전용 시드가 다른 두 사본을 비교한다.</summary>
        /// <param name="level">검사에서 소유한 메모리 레벨. 사용자 에셋은 전달하지 않는다.</param>
        private static void CheckHiddenSupply(LevelDefinition level)
        {
            JsonUtility.FromJsonOverwrite("{\"supply\":{\"sources\":[{\"coordinate\":{\"row\":0,\"column\":0},\"mode\":1,\"exhaustion\":0,\"items\":[{\"kind\":1,\"count\":2,\"color\":0},{\"kind\":2,\"count\":5,\"direction\":0}]}]}}", level);
            LevelStateBuildResult first = LevelStateBuilder.Build(level, 123);
            Check(first.IsBuilt, "숨은 공급 비교 A 정의 유효");
            JsonUtility.FromJsonOverwrite("{\"supply\":{\"sources\":[{\"coordinate\":{\"row\":0,\"column\":0},\"mode\":1,\"exhaustion\":0,\"items\":[{\"kind\":3,\"count\":3},{\"kind\":1,\"count\":7,\"color\":4}]}]}}", level);
            LevelStateBuildResult second = LevelStateBuilder.Build(level, 987);
            Check(second.IsBuilt, "숨은 공급 비교 B 정의 유효");
            BoardActionExecutor a = new BoardActionExecutor(first.State), b = new BoardActionExecutor(second.State);
            Check(Snapshot(a.State.Supply) != Snapshot(b.State.Supply) && a.State.DefinitionFingerprint != b.State.DefinitionFingerprint,
                "비교 사본의 공급 종류·순서·수량·지문 실제 차이 확인");
            // 초기 고정 배치가 같은 레벨이므로 시드가 달라도 공개 칸은 같아야 한다.
            // 공급 커서를 움직여도 과거/미래의 목록 소비 위치를 봇에 전달하지 않는다.
            Set(b.State.Supply.Sources[0], "ItemIndex", 1); Set(b.State.Supply.Sources[0], "ItemConsumed", 1);
            typeof(SimulationRandom).GetMethod("Next", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(b.State.Random, new object[] { 100 });
            string beforeA = Snapshot(a.State), beforeB = Snapshot(b.State);
            BotObservation visibleA = BotObservationBuilder.Capture(a), visibleB = BotObservationBuilder.Capture(b);
            Check(Snapshot(visibleA) == Snapshot(visibleB), "숨은 공급·커서·시드·난수만 다른 쌍의 관찰·후보 동일");
            Check(Snapshot(BasicBotStrategy.Choose(visibleA)) == Snapshot(BasicBotStrategy.Choose(visibleB)), "숨은 공급 쌍의 선택·이유 동일");
            Check(string.Join("\n", visibleA.Actions.Select(action => Snapshot(BasicBotStrategy.Evaluate(visibleA, action)))) ==
                string.Join("\n", visibleB.Actions.Select(action => Snapshot(BasicBotStrategy.Evaluate(visibleB, action)))), "숨은 공급 쌍의 전체 후보 점수 동일");
            Check(beforeA == Snapshot(a.State) && beforeB == Snapshot(b.State), "숨은 공급 비교·조회·평가 후 양쪽 상태·난수·커서 보존");
        }

        /// <summary>일반 참조형 내부에 상태를 숨겨 추가하는 것도 막도록 계약의 필드 형식을 재귀 검사한다.</summary>
        /// <param name="type">검사할 형식.</param><param name="visited">중복 순회를 막을 방문 형식.</param>
        private static void CheckPublicGraph(Type type, HashSet<Type> visited)
        {
            if (!visited.Add(type) || type.IsPrimitive || type.IsEnum || type == typeof(string)) return;
            Type nullable = Nullable.GetUnderlyingType(type);
            if (nullable != null) { CheckPublicGraph(nullable, visited); return; }
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(System.Collections.ObjectModel.ReadOnlyCollection<>))
            { CheckPublicGraph(type.GetGenericArguments()[0], visited); return; }
            Type[] allowed = { typeof(BotObservation), typeof(BotCell), typeof(BotBody), typeof(BotMission), typeof(BotAction), typeof(BotMatch),
                typeof(BoardCoordinate), typeof(BoardEdge), typeof(FlowPortal) };
            if (!allowed.Contains(type)) throw new InvalidOperationException("관찰에 허용하지 않은 중첩 형식 " + type.FullName);
            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                CheckPublicGraph(field.FieldType, visited);
        }
    }
}
