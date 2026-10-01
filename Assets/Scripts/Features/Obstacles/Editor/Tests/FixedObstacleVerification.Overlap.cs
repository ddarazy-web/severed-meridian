using System;
using System.IO;
using System.Linq;
using Board;
using Simulation;
using UnityEditor;
using UnityEngine;

namespace Levels.Editor
{
    public static partial class FixedObstacleVerification
    {
        public static void OverlapData()
        {
            Directory.CreateDirectory(Evidence); Results.Clear(); int exit = 0;
            try
            {
                var cases = new[] {
                    ("가로 로켓", -1, C(4, 0), C(4, 4), 2, 9),
                    ("세로 로켓", -2, C(0, 4), C(4, 4), 2, 9),
                    ("폭탄 모서리", -3, C(4, 4), C(2, 2), 1, 9),
                    ("폭탄 한 변", -3, C(4, 4), C(2, 3), 2, 9),
                    ("폭탄 범위 밖", -3, C(4, 4), C(1, 1), 0, 9),
                    ("로켓+로켓", 0, C(4, 4), C(3, 6), 2, 9),
                    ("로켓+폭탄 네 칸", 1, C(4, 4), C(3, 6), 4, 9),
                    ("로켓+폭탄 두 칸", 1, C(4, 4), C(2, 7), 2, 9),
                    ("폭탄+폭탄 한 칸", 3, C(4, 4), C(1, 2), 1, 9),
                    ("폭탄+폭탄 두 칸", 3, C(4, 4), C(1, 3), 2, 9),
                    ("폭탄+폭탄 네 칸", 3, C(4, 4), C(2, 3), 4, 9),
                    ("자석+자석 네 칸", 9, C(4, 4), C(0, 0), 4, 9),
                    ("자석+자석 내구도 하한", 9, C(4, 4), C(0, 0), 3, 3)
                };
                foreach (var test in cases)
                {
                    LevelDefinition level = test.Item2 < 0 ? Make() : (LevelDefinition)Invoke(typeof(CombinationVerification), "Make", null, test.Item2, RocketDirection.Horizontal, null);
                    try
                    {
                        Obstacle(level, ObstacleKind.Appliance, test.Item6, test.Item4);
                        if (test.Item2 < 0) Place(level, test.Item3, test.Item2 == -3 ? InitialBlockKind.Bomb : InitialBlockKind.Rocket,
                            test.Item2 == -2 ? RocketDirection.Vertical : RocketDirection.Horizontal);
                        LevelRuntimeState before = Build(level); string original = Snapshot(before);
                        int body = before.CellAt(test.Item4).ObstacleIndex.Value;
                        BoardActionExecutor executor = new BoardActionExecutor(before);
                        BoardActionResult action = test.Item2 < 0 ? executor.Activate(test.Item3) : executor.Swap(C(4, 4), C(4, 5));
                        Check(action.IsApplied, "겹침 피해 실제 실행 " + test.Item1);
                        EffectRecord[] hits = action.Effects.Where(effect => effect.Response == DamageResponse.Damage && before.CellAt(effect.Target).ObstacleIndex == body).ToArray();
                        Check(executor.State.Obstacles[body].Durability == test.Item6 - test.Item5 && hits.Length == test.Item5,
                            "겹친 칸 수만큼 내구도 감소 " + test.Item1 + "=" + test.Item5);
                        Check(hits.Select(effect => (effect.HitGroup, effect.Target)).Distinct().Count() == hits.Length,
                            "같은 공격의 같은 칸 중복 피해 없음 " + test.Item1);
                        Check(hits.Select((hit, index) => hit.DurabilityBefore == test.Item6 - index && hit.DurabilityAfter == test.Item6 - index - 1).All(value => value),
                            "각 칸의 실제 단계 기록 보존 " + test.Item1);
                        Check(Snapshot(before) == original, "원본 상태 보존 " + test.Item1);
                        Check(executor.State.Cells.Count(cell => cell.ObstacleIndex == body) == (test.Item5 == test.Item6 ? 0 : 4),
                            "하나의 2x2 본체 유지 또는 전체 제거 " + test.Item1);
                    }
                    finally { UnityEngine.Object.DestroyImmediate(level); }
                }
            }
            catch (Exception error) { Results.Add("FAIL " + error); exit = 1; }
            File.WriteAllLines(Evidence + "/overlap-results.txt", Results);
            if (Application.isBatchMode) EditorApplication.Exit(exit);
        }
    }
}
