using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Levels.Editor
{
    public static partial class PlanningVerification
    {
        /// <summary>기존 규칙 검사를 재사용하되 이전 단계 증거를 덮어쓰지 않고 이번 결과를 별도로 남긴다.</summary>
        public static void Regression()
        {
            Directory.CreateDirectory(Evidence); Results.Clear(); bool failed = false;
            var suites = new[]
            {
                (typeof(GeneratorVerification), new[] { "DataChecks" }),
                (typeof(LevelInitialStateVerification), new[] { "DataChecks" }),
                (typeof(StartingBoardVerification), new[] { "Patterns", "Actions", "Starts", "Edges" }),
                (typeof(BoardActionVerification), new[] { "DataChecks" }),
                (typeof(PowerEffectVerification), new[] { "DataChecks" }),
                (typeof(SettlementVerification), new[] { "DataChecks" }),
                (typeof(CascadeVerification), new[] { "DataChecks" }),
                (typeof(TargetPowerVerification), new[] { "DataChecks", "SupplementalChecks" }),
                (typeof(CombinationVerification), new[] { "DataChecks", "SupplementalChecks" }),
                (typeof(LayerVerification), new[] { "DataChecks", "SupplementalChecks" }),
                (typeof(ScrapVerification), new[] { "DataChecks", "MovementChecks", "InteractionChecks", "SupplyEdgeChecks" }),
                (typeof(MoldVerification), new[] { "DataChecks", "SupplementalChecks" }),
                (typeof(FixedObstacleVerification), new[] { "DataChecks", "MatchChecks", "MagnetChecks", "DroneChecks", "EdgeChecks" }),
                (typeof(RecoveryVerification), new[] { "DataChecks", "MovementChecks", "SwapChecks", "SupplyEdges" }),
                (typeof(RoundEndVerification), new[] { "DataChecks", "EdgeChecks" }),
                (typeof(ItemBoosterVerification), new[] { "DataChecks", "EdgeChecks" })
            };
            foreach (var suite in suites)
            {
                string owned = null;
                List<string> records = (List<string>)suite.Item1.GetField("Results", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
                records.Clear();
                try
                {
                    if (suite.Item1 == typeof(LevelInitialStateVerification))
                    {
                        owned = "Assets/__PlanningRegression_" + Guid.NewGuid().ToString("N");
                        AssetDatabase.CreateFolder("Assets", Path.GetFileName(owned));
                        suite.Item1.GetField("folder", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, owned);
                    }
                    foreach (string method in suite.Item2) suite.Item1.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                    Results.Add("PASS " + suite.Item1.Name + " " + records.Count);
                }
                catch (Exception error) { failed = true; records.Add("FAIL " + error); Results.Add("FAIL " + suite.Item1.Name); }
                finally
                {
                    File.WriteAllLines(Evidence + "/engine-regression-" + suite.Item1.Name + ".txt", records);
                    if (owned != null && !AssetDatabase.DeleteAsset(owned)) { failed = true; Results.Add("FAIL 소유 임시 에셋 정리 " + owned); }
                }
            }
            File.WriteAllLines(Evidence + "/engine-regression-results.txt", Results);
            EditorApplication.Exit(failed ? 1 : 0);
        }
    }
}
