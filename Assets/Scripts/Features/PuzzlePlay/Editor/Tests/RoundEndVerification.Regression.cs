using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Levels.Editor
{
    public static partial class RoundEndVerification
    {
        public static void Supplemental()
        {
            Directory.CreateDirectory(Evidence); Results.Clear();
            try { EdgeChecks(); File.WriteAllLines(Evidence + "/edge-results.txt", Results); SettlementVerification.Supplemental(); }
            catch (Exception error) { Results.Add("FAIL " + error); File.WriteAllLines(Evidence + "/supplemental-failure.txt", Results); Debug.LogException(error); EditorApplication.Exit(1); }
        }

        public static void EditingRegression()
        {
            Directory.CreateDirectory(Evidence); Results.Clear();
            try { EdgeChecks(); File.WriteAllLines(Evidence + "/edge-results.txt", Results); ConnectionGraphVerification.StartMerges(); }
            catch (Exception error) { Results.Add("FAIL " + error); File.WriteAllLines(Evidence + "/editing-failure.txt", Results); Debug.LogException(error); EditorApplication.Exit(1); }
        }

        public static void Audit()
        {
            Directory.CreateDirectory(Evidence); Results.Clear();
            try
            {
                DataChecks(); File.WriteAllLines(Evidence + "/data-results.txt", Results); Results.Clear();
                EdgeChecks(); File.WriteAllLines(Evidence + "/edge-results.txt", Results);
            }
            catch (Exception error)
            {
                Results.Add("FAIL " + error); File.WriteAllLines(Evidence + "/audit-failure.txt", Results); Debug.LogException(error); EditorApplication.Exit(1); return;
            }
            Regression();
        }

        public static void EndingRegression()
        {
            Directory.CreateDirectory(Evidence); Results.Clear(); bool failed = false;
            foreach (Type type in new[] { typeof(CascadeVerification), typeof(RecoveryVerification) })
            {
                List<string> records = (List<string>)type.GetField("Results", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null); records.Clear();
                try
                {
                    Invoke(type, "DataChecks");
                    if (type == typeof(RecoveryVerification))
                        foreach (string method in new[] { "MovementChecks", "SwapChecks", "SupplyEdges" }) Invoke(type, method);
                    Results.Add("PASS " + type.Name + " " + records.Count);
                }
                catch (Exception error) { failed = true; records.Add("FAIL " + error); Results.Add("FAIL " + type.Name + " " + error); Debug.LogException(error); }
                File.WriteAllLines(Evidence + "/regression-" + type.Name + ".txt", records);
            }
            File.WriteAllLines(Evidence + "/ending-regression-results.txt", Results); EditorApplication.Exit(failed ? 1 : 0);
        }

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
                (typeof(RecoveryVerification), new[] { "DataChecks", "MovementChecks", "SwapChecks", "SupplyEdges" })
            };
            foreach (var suite in suites)
            {
                string owned = null;
                List<string> records = (List<string>)suite.Item1.GetField("Results", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null); records.Clear();
                try
                {
                    if (suite.Item1 == typeof(LevelInitialStateVerification))
                    {
                        owned = "Assets/__RoundEndRegression_" + Guid.NewGuid().ToString("N"); AssetDatabase.CreateFolder("Assets", Path.GetFileName(owned));
                        suite.Item1.GetField("folder", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, owned);
                    }
                    foreach (string method in suite.Item2) Invoke(suite.Item1, method);
                    Results.Add("PASS " + suite.Item1.Name + " " + records.Count);
                }
                catch (Exception error) { failed = true; records.Add("FAIL " + error); Results.Add("FAIL " + suite.Item1.Name + " " + error); Debug.LogException(error); }
                finally
                {
                    File.WriteAllLines(Evidence + "/regression-" + suite.Item1.Name + ".txt", records);
                    if (owned != null) AssetDatabase.DeleteAsset(owned);
                }
            }
            File.WriteAllLines(Evidence + "/regression-results.txt", Results); EditorApplication.Exit(failed ? 1 : 0);
        }
    }
}





