using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Levels.Editor
{
    public static partial class ScrapVerification
    {
        public static void Regression()
        {
            Directory.CreateDirectory(Evidence); Results.Clear(); bool failed = false;
            var suites = new[]
            {
                (typeof(LevelInitialStateVerification), new[] { "DataChecks" }),
                (typeof(StartingBoardVerification), new[] { "Patterns", "Actions", "Starts", "Edges" }),
                (typeof(BoardActionVerification), new[] { "DataChecks" }),
                (typeof(PowerEffectVerification), new[] { "DataChecks", "SupplementalChecks" }),
                (typeof(SettlementVerification), new[] { "DataChecks" }),
                (typeof(CascadeVerification), new[] { "DataChecks" }),
                (typeof(TargetPowerVerification), new[] { "DataChecks", "SupplementalChecks" }),
                (typeof(CombinationVerification), new[] { "DataChecks", "SupplementalChecks" }),
                (typeof(LayerVerification), new[] { "DataChecks", "SupplementalChecks" })
            };
            foreach (var suite in suites)
            {
                string owned = null;
                List<string> records = (List<string>)suite.Item1.GetField("Results", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null); records.Clear();
                try
                {
                    if (suite.Item1 == typeof(LevelInitialStateVerification))
                    {
                        owned = "Assets/__ScrapRegression_" + Guid.NewGuid().ToString("N"); AssetDatabase.CreateFolder("Assets", Path.GetFileName(owned));
                        suite.Item1.GetField("folder", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, owned);
                    }
                    foreach (string method in suite.Item2) Invoke(suite.Item1, method, null);
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
