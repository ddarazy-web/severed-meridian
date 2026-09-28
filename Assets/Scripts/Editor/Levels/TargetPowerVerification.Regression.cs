using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Levels.Editor
{
    public static partial class TargetPowerVerification
    {
        public static void FinalChecks()
        {
            try
            {
                Restart(); Results.Clear(); SupplementalChecks();
                File.WriteAllLines(Evidence + "/supplemental-results.txt", Results);
            }
            catch (Exception error)
            {
                Results.Add("FAIL " + error); File.WriteAllLines(Evidence + "/final-failure.txt", Results);
                Debug.LogException(error); EditorApplication.Exit(1); return;
            }
            Regression();
        }

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
                (typeof(CascadeVerification), new[] { "DataChecks" })
            };
            foreach (var suite in suites)
            {
                string owned = null;
                try
                {
                    // 8단계 데이터 검사는 에셋 재로드를 포함하므로 소유 폴더를 제공한다.
                    if (suite.Item1 == typeof(LevelInitialStateVerification))
                    {
                        owned = "Assets/__TargetPowerRegression_" + Guid.NewGuid().ToString("N");
                        AssetDatabase.CreateFolder("Assets", Path.GetFileName(owned));
                        suite.Item1.GetField("folder", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, owned);
                    }
                    List<string> records = (List<string>)suite.Item1.GetField("Results", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
                    records.Clear();
                    foreach (string method in suite.Item2) Invoke(suite.Item1, method, null);
                    File.WriteAllLines(Evidence + "/regression-" + suite.Item1.Name + ".txt", records);
                    Results.Add("PASS " + suite.Item1.Name + " " + records.Count);
                }
                catch (Exception error) { failed = true; Results.Add("FAIL " + suite.Item1.Name + " " + error); Debug.LogException(error); }
                finally { if (owned != null) AssetDatabase.DeleteAsset(owned); }
            }
            File.WriteAllLines(Evidence + "/regression-results.txt", Results); EditorApplication.Exit(failed ? 1 : 0);
        }
    }
}
