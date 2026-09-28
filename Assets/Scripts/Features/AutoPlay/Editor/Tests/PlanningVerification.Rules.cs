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
    public static partial class PlanningVerification
    {
        /// <summary>기존 대표 레벨을 공개 값만으로 재구성해 실제 공통 규칙의 첫 피해와 비교한다.</summary>
        public static void Rules()
        {
            List<LevelDefinition> owned = new List<LevelDefinition>();
            Exception failure = null; Results.Clear(); Directory.CreateDirectory(Evidence);
            try
            {
                foreach (ObstacleKind kind in new[] { ObstacleKind.Crate, ObstacleKind.Scrap, ObstacleKind.Safe, ObstacleKind.ColorLock, ObstacleKind.Appliance })
                {
                    LevelDefinition level = (LevelDefinition)typeof(PowerEffectVerification).GetMethod("Make", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                    owned.Add(level);
                    typeof(FixedObstacleVerification).GetMethod("Obstacle", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,
                        new object[] { level, kind, 3, new BoardCoordinate(4, 4), RabbitColor.Type1 });
                    CompareRules(level, kind.ToString());
                }
                LevelDefinition generator = (LevelDefinition)typeof(GeneratorVerification).GetMethod("Make", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { ObstacleKind.Crate, 3 });
                owned.Add(generator); CompareRules(generator, "발전기·공개 연결");
                foreach (MissionKind kind in new[] { MissionKind.Web, MissionKind.Mold, MissionKind.Dust })
                {
                    LevelDefinition level = (LevelDefinition)typeof(PowerEffectVerification).GetMethod("Make", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                    owned.Add(level);
                    string layer = kind == MissionKind.Dust ? "\"dust\":[{\"coordinate\":{\"row\":4,\"column\":4},\"durability\":2}]" :
                        "\"covers\":[{\"coordinate\":{\"row\":4,\"column\":4},\"kind\":" + (kind == MissionKind.Web ? 0 : 1) + ",\"durability\":" + (kind == MissionKind.Mold ? 1 : 2) + "}]";
                    JsonUtility.FromJsonOverwrite("{" + layer + "}", level);
                    CompareRules(level, kind.ToString());
                }
            }
            catch (Exception error) { failure = error; Results.Add("FAIL " + error); }
            finally { foreach (LevelDefinition level in owned) if (level != null) UnityEngine.Object.DestroyImmediate(level); }
            File.WriteAllLines(Evidence + "/rules-results.txt", Results);
            EditorApplication.Exit(failure == null ? 0 : 1);
        }

        /// <summary>공개 현재 내구도·충전·미션을 복원하고 같은 폭탄 행동의 즉시 피해를 비교한다.</summary>
        /// <param name="level">검사 소유 대표 레벨.</param><param name="name">사례 이름.</param>
        private static void CompareRules(LevelDefinition level, string name)
        {
            BoardCoordinate bomb = new BoardCoordinate(4, 3);
            typeof(PowerEffectVerification).GetMethod("Place", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,
                new object[] { level, bomb, InitialBlockKind.Bomb, RocketDirection.Horizontal, RabbitColor.Type1 });
            JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":0,\"color\":0,\"count\":100}]}", level);
            LevelStateBuildResult built = LevelStateBuilder.Build(level, 12345);
            Check(built.IsBuilt, name + " 대표 정의 정합성 통과");
            BoardActionExecutor actual = new BoardActionExecutor(built.State);
            foreach (RuntimeObstacle body in actual.State.Obstacles)
            {
                if (body.Definition.Kind == ObstacleKind.Generator) Set(body, "Charge", 1);
                else Set(body, "Durability", 2);
            }
            Set(actual.State.Missions[0], "Progress", 4);
            BotObservation visible = BotObservationBuilder.Capture(actual);
            Type type = typeof(PlanningSearch).Assembly.GetType("AutoPlay.PlanningBranch");
            BindingFlags instance = BindingFlags.Instance | BindingFlags.NonPublic;
            object branch = type.GetMethod("Create", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { visible, 0 });
            BoardActionExecutor assumed = (BoardActionExecutor)type.GetField("executor", instance).GetValue(branch);
            Check(Snapshot(BotObservationBuilder.Capture(assumed)) == Snapshot(visible), name + " 현재 공개 칸·내구도·충전·미션·연결·후보 보존");
            string before = Snapshot(actual.State);
            BotAction action = visible.Actions.Single(a => a.Kind == BotActionKind.Activate && a.First.Equals(bomb));
            type.GetMethod("Apply", instance).Invoke(branch, new object[] { action });
            Check(Snapshot(actual.State) == before, name + " 가정 피해가 실제 판에 전파되지 않음");
            Check(actual.Activate(bomb).IsApplied, name + " 같은 실제 파워 명령 수락");
            // 공급/가정 난수·숨은 내용은 서로 다르므로 전체 상태를 억지로 같다고 보지 않는다.
            // 같은 공통 파워 명령의 공개 피해 결과, 비용과 현재 목표 감소를 비교한다.
            string ActualDamage(LevelRuntimeState state) => string.Join(";", state.Cells.Select(cell =>
                cell.Coordinate + ":" + cell.Cover + ":" + cell.CoverDurability + ":" + cell.DustDurability)) + "|" +
                string.Join(";", state.Obstacles.OrderBy(body => body.Definition.Coordinate.Row).ThenBy(body => body.Definition.Coordinate.Column)
                    .Select(body => body.Definition.Kind + ":" + body.Durability + ":" + body.Charge)) + "|" +
                string.Join(";", state.Missions.Select(mission => mission.Remaining)) + "|" + state.MovesRemaining;
            Check(ActualDamage(actual.State) == ActualDamage(assumed.State), name + " 공통 피해·충전·덮개·먼지·미션·이동 비용 일치");
        }
    }
}
