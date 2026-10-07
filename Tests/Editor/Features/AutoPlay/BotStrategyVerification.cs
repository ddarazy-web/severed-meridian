using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AutoPlay;
using Board;
using UnityEditor;

namespace Levels.Editor
{
    /// <summary>공개 값만으로 전략을 검사한다. 실행 결과와의 통합 검증은 별도로 수행한다.</summary>
    public static class BotStrategyVerification
    {
        private static readonly List<string> Results = new List<string>();

        /// <summary>종류별 점수·범위·동점 처리를 검사하고 전용 Unity 프로세스를 종료한다.</summary>
        public static void Run()
        {
            Exception failure = null;
            try
            {
                CheckBodies();
                CheckLayers();
                CheckPowers();
                CheckSelection();
                CheckRecovery();
            }
            catch (Exception error) { failure = error; Results.Add("FAIL " + error); }
            Directory.CreateDirectory("Logs/BotBasicVerification");
            File.WriteAllLines("Logs/BotBasicVerification/strategy-results.txt", Results);
            EditorApplication.Exit(failure == null ? 0 : 1);
        }

        /// <summary>실제 규칙의 인접/직격 차이, 내구도, 발전기의 연결 완료 중복을 고정한다.</summary>
        private static void CheckBodies()
        {
            foreach (ObstacleKind kind in new[] { ObstacleKind.Crate, ObstacleKind.Scrap, ObstacleKind.Safe, ObstacleKind.ColorLock, ObstacleKind.Appliance })
            {
                Fixture f = new Fixture();
                f.Body(kind, 1, new[] { C(1, 1) }, RabbitColor.Type1);
                f.Mission(Kind(kind));
                BotAction match = f.Match(C(2, 0), C(2, 1), C(2, 2));
                Check(f.Score(match) == (kind == ObstacleKind.Safe ? 0 : 8), "매칭 인접 피해 " + kind);
                f.Walls.Add(new BoardEdge(C(2, 1), C(1, 1)));
                Check(f.Score(match) == 0, "벽이 인접 피해 차단 " + kind);
                f.Walls.Clear();
                if (kind == ObstacleKind.ColorLock)
                {
                    f.Cells[11] = Cell(C(2, 1), color: RabbitColor.Type2);
                    Check(f.Score(match) == 0, "자물쇠 지정 색 불일치 무기여");
                }
                f.Cells[12] = Cell(C(2, 2), BotContent.Bomb);
                Check(f.Score(f.Activate(C(2, 2))) == 8, "폭탄 직접 피해 " + kind);
            }

            Fixture appliance = new Fixture();
            appliance.Body(ObstacleKind.Appliance, 9, new[] { C(0, 0), C(0, 1), C(1, 0), C(1, 1) });
            appliance.Mission(MissionKind.Appliance);
            appliance.Cells[12] = Cell(C(2, 2), BotContent.Bomb);
            Check(appliance.Score(appliance.Activate(C(2, 2))) == 1, "폐가전 한 칸 명중은 내구도 1 진행");
            appliance.Cells[12] = Cell(C(2, 2), BotContent.Magnet);
            appliance.Cells[13] = Cell(C(2, 3), BotContent.Magnet);
            Check(appliance.Score(appliance.Swap(BotActionKind.CombinationSwap, C(2, 2), C(2, 3))) == 4,
                "폐가전 네 점유 칸 명중은 한 타격에 최대 4피해");

            Fixture generator = new Fixture();
            generator.Body(ObstacleKind.Crate, 1, new[] { C(1, 1) });
            generator.Body(ObstacleKind.Generator, 1, new[] { C(1, 2) }, required: 1, targets: new[] { 6 });
            generator.Mission(MissionKind.Crate);
            generator.Cells[12] = Cell(C(2, 2), BotContent.Bomb);
            Check(generator.Score(generator.Activate(C(2, 2))) == 8, "발전기 완료와 직접 파괴의 같은 본체 중복 집계 금지");
        }

        /// <summary>덮개 제거와 내용물 소비를 구분하고, 먼지는 소비된 일반 블록에서만 센다.</summary>
        private static void CheckLayers()
        {
            Fixture f = new Fixture();
            f.Mission(MissionKind.Web); f.Mission(MissionKind.Color, RabbitColor.Type1); f.Mission(MissionKind.Dust);
            f.Cells[11] = Cell(C(2, 1), cover: CoverKind.Web, durability: 2, dust: 1);
            BotAction match = f.Match(C(2, 0), C(2, 1), C(2, 2));
            Check(f.Score(match) == 17, "거미줄 매칭: 노출 2개 색 수집 + 줄 1피해, 내부 색·먼지 보존");
            f.Body(ObstacleKind.Crate, 1, new[] { C(1, 1) }); f.Mission(MissionKind.Crate);
            Check(f.Score(match) == 25, "거미줄에 덮인 매칭 칸도 인접 상자 피해 전달");
            f.Cells[11] = Cell(C(2, 1), cover: CoverKind.Web, durability: 1, dust: 1);
            Check(f.Score(match) == 32, "거미줄 마지막 내구도는 완료량으로 계산");

            Fixture dust = new Fixture(); dust.Mission(MissionKind.Dust);
            dust.Cells[12] = Cell(C(2, 2), BotContent.Bomb, dust: 1);
            dust.Cells[11] = Cell(C(2, 1), dust: 2);
            Check(dust.Score(dust.Activate(C(2, 2))) == 1, "파워 아래 먼지는 보존, 일반 블록 아래 먼지만 1피해");

            Fixture mold = new Fixture(); mold.Mission(MissionKind.Mold);
            mold.Cells[6] = Cell(C(1, 1), BotContent.Unknown, cover: CoverKind.Mold, durability: 1);
            Check(mold.Score(mold.Match(C(2, 0), C(2, 1), C(2, 2))) == 8, "곰팡이는 인접 매칭으로 제거");
            mold.Cells[12] = Cell(C(2, 2), BotContent.Magnet);
            Check(mold.Score(mold.Swap(BotActionKind.PowerSwap, C(2, 2), C(2, 3))) == 0, "색 자석은 숨은 칸이나 인접 곰팡이를 직접 제거하지 않음");
        }

        /// <summary>화면상 직접 범위만 평가하며 임의 색·드론 도착은 확정 기여에 넣지 않는다.</summary>
        private static void CheckPowers()
        {
            foreach (BotContent content in new[] { BotContent.Rocket, BotContent.Bomb, BotContent.Drone, BotContent.Magnet })
            {
                Fixture f = new Fixture(); f.Cells[12] = Cell(C(2, 2), content, rocket: RocketDirection.Horizontal);
                BotAction action = f.Activate(C(2, 2));
                BotChoice choice = BasicBotStrategy.Evaluate(f.Board(), action);
                int expected = content == BotContent.Bomb ? 9 : content == BotContent.Magnet ? 0 : 5;
                Check(choice.ClearValue == expected, "단일 파워 직접 범위 " + content);
                Check(choice.Reason.Contains("미예측"), "파워 평가 불확실성 표시 " + content);
            }
            foreach (var pair in new[] {
                (BotContent.Rocket, BotContent.Rocket, 9), (BotContent.Rocket, BotContent.Bomb, 21),
                (BotContent.Bomb, BotContent.Bomb, 25), (BotContent.Drone, BotContent.Drone, 9),
                (BotContent.Rocket, BotContent.Drone, 5), (BotContent.Bomb, BotContent.Drone, 5),
                (BotContent.Magnet, BotContent.Magnet, 25), (BotContent.Magnet, BotContent.Rocket, 0),
                (BotContent.Magnet, BotContent.Bomb, 0), (BotContent.Magnet, BotContent.Drone, 0) })
            {
                Fixture f = new Fixture();
                f.Cells[11] = Cell(C(2, 1), pair.Item1); f.Cells[12] = Cell(C(2, 2), pair.Item2);
                BotChoice choice = BasicBotStrategy.Evaluate(f.Board(), f.Swap(BotActionKind.CombinationSwap, C(2, 1), C(2, 2)));
                Check(choice.ClearValue == pair.Item3, "조합의 직접 확인 범위 " + pair.Item1 + "+" + pair.Item2);
            }
        }

        /// <summary>미션 우선, 파워 우선, 순서와 무관한 좌표 동점을 확인한다.</summary>
        private static void CheckSelection()
        {
            Fixture f = new Fixture(); f.Mission(MissionKind.Color, RabbitColor.Type1);
            f.Cells[0] = Cell(C(0, 0), BotContent.Magnet);
            BotAction randomMagnet = f.Activate(C(0, 0));
            BotAction match = f.Match(C(2, 0), C(2, 1), C(2, 2));
            Check(ReferenceEquals(BasicBotStrategy.Choose(f.Board()).Action, match), "무작위 자석 가치보다 확인된 미션 기여 우선");
            f.Missions.Clear();
            Check(ReferenceEquals(BasicBotStrategy.Choose(f.Board()).Action, randomMagnet), "미션 동점은 파워 가치 우선");
            f.Actions.Clear(); f.Cells[24] = Cell(C(4, 4), BotContent.Magnet);
            f.Activate(C(4, 4)); f.Actions.Add(randomMagnet);
            Check(ReferenceEquals(BasicBotStrategy.Choose(f.Board()).Action, randomMagnet), "후보 열거 순서보다 좌표 동점 규칙 우선");
            f.Actions.Clear();
            Check(BasicBotStrategy.Choose(f.Board()) == null, "후보 없는 관찰은 선택 없음 반환, 패배 선언 없음");
        }

        /// <summary>회수는 공개된 도착 칸의 직접 기여만 평가하며 숨은 중력 경로를 추측하지 않는다.</summary>
        private static void CheckRecovery()
        {
            Fixture f = new Fixture();
            f.Mission(MissionKind.Recovery);
            f.Cells[12] = Cell(C(2, 2), BotContent.Recovery);
            f.Cells[13] = Cell(C(2, 3), BotContent.Rocket);
            BotAction swap = f.Swap(BotActionKind.PowerSwap, C(2, 2), C(2, 3));
            BotObservation arrival = Create<BotObservation>(5, 5, 20, f.Cells, f.Bodies, f.Missions, f.Actions, f.Walls,
                new[] { C(2, 3) }, Array.Empty<FlowPortal>());
            Check(BasicBotStrategy.Evaluate(arrival, swap).MissionValue == 8, "회수 부품을 공개 도착 칸으로 직접 교환하는 기여");
            Check(f.Score(swap) == 0, "도착 칸이 아닌 교환은 미래 낙하 회수를 가정하지 않음");
            f.Cells[17] = Cell(C(3, 2), BotContent.Bomb);
            Check(f.Score(f.Activate(C(3, 2))) == 0, "부품 아래 제거만으로 숨은 경로의 회수 성공을 예측하지 않음");
        }

        /// <param name="pass">충족 여부.</param><param name="name">검사 이름.</param>
        private static void Check(bool pass, string name)
        { if (!pass) throw new InvalidOperationException(name); Results.Add("PASS " + name); }

        /// <param name="row">행.</param><param name="column">열.</param><returns>좌표.</returns>
        private static BoardCoordinate C(int row, int column) => new BoardCoordinate(row, column);

        /// <summary>Editor 어셈블리에서 내부 생성자를 호출해 공개 값만으로 검증 입력을 만든다.</summary>
        /// <param name="args">생성자 값.</param><returns>검증용 공개 계약 객체.</returns>
        private static T Create<T>(params object[] args) => (T)Activator.CreateInstance(typeof(T),
            BindingFlags.Instance | BindingFlags.NonPublic, null, args, null);

        /// <param name="at">좌표.</param><param name="content">내용.</param><param name="color">일반 색.</param>
        /// <param name="rocket">방향.</param><param name="cover">덮개.</param><param name="durability">덮개 내구도.</param>
        /// <param name="dust">먼지.</param><param name="body">본체 키.</param><returns>공개 칸.</returns>
        private static BotCell Cell(BoardCoordinate at, BotContent content = BotContent.Normal, RabbitColor color = RabbitColor.Type1,
            RocketDirection rocket = RocketDirection.Horizontal, CoverKind? cover = null, int durability = 0, int dust = 0, int? body = null)
            => Create<BotCell>(at, true, content, content == BotContent.Normal ? color : (RabbitColor?)null,
                content == BotContent.Rocket ? rocket : (RocketDirection?)null, cover, durability, dust, body);

        /// <param name="kind">검사 대상 종류.</param><returns>목표 종류.</returns>
        private static MissionKind Kind(ObstacleKind kind) => kind switch {
            ObstacleKind.Safe => MissionKind.Safe, ObstacleKind.Scrap => MissionKind.Scrap,
            ObstacleKind.ColorLock => MissionKind.ColorLock, ObstacleKind.Appliance => MissionKind.Appliance, _ => MissionKind.Crate };

        /// <summary>5×5 공개 판. 원본 에셋·실행기·난수·공급은 애초에 보유하지 않는다.</summary>
        private sealed class Fixture
        {
            internal readonly BotCell[] Cells = Enumerable.Range(0, 25).Select(i => Cell(C(i / 5, i % 5))).ToArray();
            internal readonly List<BotBody> Bodies = new List<BotBody>();
            internal readonly List<BotMission> Missions = new List<BotMission>();
            internal readonly List<BotAction> Actions = new List<BotAction>();
            internal readonly List<BoardEdge> Walls = new List<BoardEdge>();

            /// <returns>매번 독립된 공개 관찰.</returns>
            internal BotObservation Board() => Create<BotObservation>(5, 5, 20, Cells, Bodies, Missions, Actions, Walls,
                Array.Empty<BoardCoordinate>(), Array.Empty<FlowPortal>());
            /// <param name="action">평가 후보.</param><returns>미션 점수.</returns>
            internal int Score(BotAction action) => BasicBotStrategy.Evaluate(Board(), action).MissionValue;
            /// <param name="kind">목표 종류.</param><param name="color">목표 색.</param>
            internal void Mission(MissionKind kind, RabbitColor? color = null) => Missions.Add(Create<BotMission>(kind, color, 50));
            /// <param name="kind">본체 종류.</param><param name="durability">내구도.</param><param name="cells">점유 칸.</param>
            /// <param name="color">지정 색.</param><param name="required">충전 목표.</param><param name="targets">연결 키.</param>
            internal void Body(ObstacleKind kind, int durability, BoardCoordinate[] cells, RabbitColor? color = null,
                int required = 0, int[] targets = null)
            {
                int key = cells[0].Row * 5 + cells[0].Column;
                Bodies.Add(Create<BotBody>(key, kind, durability, color, 0, required, cells, targets ?? Array.Empty<int>()));
                foreach (BoardCoordinate at in cells) Cells[at.Row * 5 + at.Column] = Cell(at, BotContent.Obstacle, body: key);
            }
            /// <param name="cells">매칭 칸.</param><returns>고정 일반 매칭 후보.</returns>
            internal BotAction Match(params BoardCoordinate[] cells)
            {
                BotAction action = Create<BotAction>(BotActionKind.MatchSwap, C(3, 2), C(2, 2),
                    new[] { Create<BotMatch>(BotMatchKind.Three, RabbitColor.Type1, cells) });
                Actions.Add(action); return action;
            }
            /// <param name="at">발동 칸.</param><returns>발동 후보.</returns>
            internal BotAction Activate(BoardCoordinate at)
            {
                BotAction action = Create<BotAction>(BotActionKind.Activate, at, null, Array.Empty<BotMatch>());
                Actions.Add(action); return action;
            }
            /// <param name="kind">교환 종류.</param><param name="first">출발.</param><param name="second">도착.</param><returns>교환 후보.</returns>
            internal BotAction Swap(BotActionKind kind, BoardCoordinate first, BoardCoordinate second)
            {
                BotAction action = Create<BotAction>(kind, first, second, Array.Empty<BotMatch>());
                Actions.Add(action); return action;
            }
        }
    }
}
