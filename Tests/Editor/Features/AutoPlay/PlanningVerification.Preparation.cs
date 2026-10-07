using System;
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
        private static bool replayPreparation;
        public static void ReplayPreparation() { replayPreparation = true; Preparation(); }

        /// <summary>고정한 준비 수 사례에서 첫 수는 미완료이고 둘째 수에서 목표를 달성함을 실제 가정 실행으로 검증한다.</summary>
        public static void Preparation()
        {
            LevelDefinition level = null;
            Exception failure = null;
            Directory.CreateDirectory(Evidence); Results.Clear();
            try
            {
                BoardCoordinate[] active = Enumerable.Range(0, 25).Select(i => new BoardCoordinate(i / 5, i % 5)).ToArray();
                level = (LevelDefinition)typeof(SettlementVerification).GetMethod("Make", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { active });
                JsonUtility.FromJsonOverwrite("{\"initialBlocks\":[],\"missions\":[{\"kind\":1,\"count\":1}]}", level);
                PlacementEditResult placed = LevelObstacleEditing.Apply(level,
                    new PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)ObstacleKind.Crate, Durability = 2 }, new[] { new BoardCoordinate(3, 3) });
                Check(placed.Changed == 1, "준비 사례의 내구도 2 상자 실제 배치");
                StartingBoardSearch start = StartingBoardBuilder.Build(level, 9);
                Check(start.Status == StartingBoardStatus.Success, "준비 사례의 시작 조건 통과");
                BotObservation observation = BotObservationBuilder.Capture(new BoardActionExecutor(start.State));
                BotChoice basic = BasicBotStrategy.Choose(observation);
                PlanningSearch plan = RunSearch(observation, 1);
                BotChoice choice = plan.Result;
                Check(!plan.UsedFallback && plan.CompletedSamples == 3, "준비 수 비교의 공통 가정 표본 3개 완료");
                Check(!ReferenceEquals(choice.Action, basic.Action) && choice.MissionValue < basic.MissionValue,
                    "계획은 당장의 기본 평가가 더 낮은 다른 첫 행동을 선택");
                long[] totals = (long[])typeof(PlanningSearch).GetField("totals", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(plan);
                Check(plan.SelectedValue > totals[0], "같은 가정 표본에서 둘째 수까지의 기여가 기본 선택보다 큼");
                Type branchType = typeof(PlanningSearch).Assembly.GetType("AutoPlay.PlanningBranch");
                BindingFlags member = BindingFlags.Instance | BindingFlags.NonPublic;
                for (int sample = 0; sample < 3; sample++)
                {
                    object first = branchType.GetMethod("Create", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { observation, sample });
                    branchType.GetMethod("Apply", member).Invoke(first, new object[] { choice.Action });
                    DrainBranch(first, branchType);
                    Check(!(bool)branchType.GetProperty("Terminal", member).GetValue(first), "표본 " + sample + " 첫 행동만으로 목표가 완료되지 않음");
                    BotObservation next = (BotObservation)branchType.GetMethod("Observe", member).Invoke(first, null);
                    BotChoice[] children = (BotChoice[])typeof(PlanningSearch).GetMethod("Rank", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { next });
                    bool won = false;
                    foreach (BotChoice child in children)
                    {
                        object second = branchType.GetMethod("Fork", member).Invoke(first, null);
                        branchType.GetMethod("Apply", member).Invoke(second, new object[] { child.Action }); DrainBranch(second, branchType);
                        won |= (bool)branchType.GetProperty("Won", member).GetValue(second);
                    }
                    Check(won, "표본 " + sample + " 두 번째 실제 가정 행동으로 상자 목표 완료");
                }
                string proof = PlanningSearch.Version + "\n" + BotPlaySession.Version + "\n" + BoardActionExecutor.Version + "\n" +
                    Snapshot(observation) + "\n" + Snapshot(choice) + "\n" + plan.SelectedValue + "\n" + string.Join(",", totals);
                string file = Evidence + (replayPreparation ? "/preparation-replay-second.txt" : "/preparation-replay-first.txt");
                File.WriteAllText(file, proof);
                if (replayPreparation)
                {
                    Check(File.ReadAllText(Evidence + "/preparation-replay-first.txt") == proof, "독립 프로세스의 가정·선택·평가 근거 재현 일치");
                    Check(File.ReadAllText(Evidence + "/preparation-first-process.txt") != System.Diagnostics.Process.GetCurrentProcess().Id.ToString(), "실제로 다른 Unity 프로세스에서 재현");
                }
                else File.WriteAllText(Evidence + "/preparation-first-process.txt", System.Diagnostics.Process.GetCurrentProcess().Id.ToString());
            }
            catch (Exception error) { failure = error; Results.Add("FAIL " + error); }
            finally { if (level != null) UnityEngine.Object.DestroyImmediate(level); }
            File.WriteAllLines(Evidence + (replayPreparation ? "/preparation-replay-results.txt" : "/preparation-results.txt"), Results);
            EditorApplication.Exit(failure == null ? 0 : 1);
        }

        /// <param name="branch">공개 값에서 생성한 검사 소유 가정.</param><param name="type">가정 어댑터 형식.</param>
        private static void DrainBranch(object branch, Type type)
        {
            BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            int steps = 0;
            while ((bool)type.GetProperty("Pending", flags).GetValue(branch) && !(bool)type.GetProperty("Failed", flags).GetValue(branch) && steps++ < 128)
                type.GetMethod("Advance", flags).Invoke(branch, null);
            if ((bool)type.GetProperty("Failed", flags).GetValue(branch) || steps > 128) throw new InvalidOperationException("준비 사례의 가정 실행 실패");
        }
    }
}
