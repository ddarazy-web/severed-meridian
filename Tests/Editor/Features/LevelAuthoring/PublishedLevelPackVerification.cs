using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Levels;
using MemoryPack;
using UnityEditor;
using UnityEngine;

namespace LevelAuthoring.Editor
{
    public static class PublishedLevelPackVerification
    {
        public static void Run()
        {
            string root = "Logs/LobbyStage02/published-" + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(root);
            var results = new List<string>();
            int exit = 1;
            try
            {
                MethodInfo decode = typeof(LevelPackCodec).GetMethod("DecodePublished");
                Check(decode != null, "Selected-level published decoder missing");
                byte[] bytes = File.ReadAllBytes("Assets/Data/LevelPacks/levels-000001.bytes");
                var watch = Stopwatch.StartNew();
                object pack = decode.Invoke(null, new object[] { bytes });
                watch.Stop(); results.Add("decodeMs=" + watch.Elapsed.TotalMilliseconds);
                MethodInfo create = pack.GetType().GetMethod("CreateLevel");
                foreach (int number in new[] { 1, 2, 15 })
                {
                    watch.Restart();
                    LevelDefinition actual = (LevelDefinition)create.Invoke(pack, new object[] { number });
                    watch.Stop(); results.Add("selected=" + number + ", createMs=" + watch.Elapsed.TotalMilliseconds);
                    LevelDefinition expected = LevelPackCodec.ReadLevel(bytes, number);
                    try { Check(LevelPackCodec.Snapshot(actual).SequenceEqual(LevelPackCodec.Snapshot(expected)), "Published/authoring level content differs: " + number); }
                    finally { UnityEngine.Object.DestroyImmediate(actual); UnityEngine.Object.DestroyImmediate(expected); }
                }
                foreach (int missing in new[] { 0, 16, 51 })
                {
                    bool rejected = false;
                    try { create.Invoke(pack, new object[] { missing }); }
                    catch (TargetInvocationException) { rejected = true; }
                    Check(rejected, "Missing/wrong interval accepted");
                }
                byte[] corrupt = (byte[])bytes.Clone(); corrupt[4] = 99;
                bool badVersion = false;
                try { decode.Invoke(null, new object[] { corrupt }); }
                catch (TargetInvocationException) { badVersion = true; }
                Check(badVersion, "Unknown version accepted");
                LevelDefinition changed = (LevelDefinition)create.Invoke(pack, new object[] { 1 });
                changed.Tutorial.steps.Clear();
                UnityEngine.Object.DestroyImmediate(changed);
                LevelDefinition restored = (LevelDefinition)create.Invoke(pack, new object[] { 1 });
                try { Check(restored.HasTutorial, "Created level mutated retained pack"); }
                finally { UnityEngine.Object.DestroyImmediate(restored); }
                PackedTutorialLevelPack tutorial = LevelPackCodec.DecodeTutorial(bytes);
                ElementLevelPack elements = LevelPackCodec.DecodeElements(tutorial.ElementPack);
                var completeDefinitions = elements.Definitions;
                string requiredSupply = Elements.LegacyElementMap.Get(Levels.SupplyKind.Rocket).Value;
                elements.Definitions = elements.Definitions.Where(definition => definition.id != requiredSupply).ToArray();
                Check(elements.Definitions.Length < completeDefinitions.Length, "Missing reference fixture did not remove a required supply");
                byte[] missingPayload = MemoryPackSerializer.Serialize(elements);
                byte[] missingBytes = new byte[missingPayload.Length + 8];
                Array.Copy(tutorial.ElementPack, missingBytes, 8); Array.Copy(missingPayload, 0, missingBytes, 8, missingPayload.Length);
                bool missingReference = false;
                try { decode.Invoke(null, new object[] { missingBytes }); }
                catch (TargetInvocationException error) when (error.InnerException is System.Collections.Generic.KeyNotFoundException) { missingReference = true; }
                Check(missingReference, "Required supply definition accepted when absent");
                results.Add("PASS published decoder rejects missing required supply definition");
                elements.Definitions = completeDefinitions;
                elements.Levels.Single(entry => entry.LevelNumber == 2).MoveCount = 0;
                byte[] payload = MemoryPackSerializer.Serialize(elements);
                byte[] elementBytes = new byte[payload.Length + 8];
                Array.Copy(tutorial.ElementPack, elementBytes, 8); Array.Copy(payload, 0, elementBytes, 8, payload.Length);
                object structural = decode.Invoke(null, new object[] { elementBytes });
                LevelDefinition valid = (LevelDefinition)create.Invoke(structural, new object[] { 1 });
                UnityEngine.Object.DestroyImmediate(valid);
                bool invalidSelected = false;
                try { create.Invoke(structural, new object[] { 2 }); }
                catch (TargetInvocationException error) when (error.InnerException is ArgumentException) { invalidSelected = true; }
                Check(invalidSelected, "Selected level validation skipped");
                results.Add("PASS retained pack isolation; nonselected level not constructed/validated; selected invalid level rejected");
                for (int run = 0; run < 4; run++)
                {
                    watch.Restart(); LevelDefinition old = LevelPackCodec.ReadLevel(bytes, 1); watch.Stop();
                    double oldMs = watch.Elapsed.TotalMilliseconds; UnityEngine.Object.DestroyImmediate(old);
                    watch.Restart(); object prepared = decode.Invoke(null, new object[] { bytes });
                    LevelDefinition selected = (LevelDefinition)create.Invoke(prepared, new object[] { 1 }); watch.Stop();
                    results.Add($"run={run}, oldReadMs={oldMs:F3}, publishedDecodeAndCreateMs={watch.Elapsed.TotalMilliseconds:F3}");
                    UnityEngine.Object.DestroyImmediate(selected);
                }
                results.Add("PASS one decoded pack reused for 1/2/15; full content parity; missing/interval/version rejection");
                exit = 0;
            }
            catch (Exception error) { results.Add("FAIL " + error); UnityEngine.Debug.LogException(error); }
            File.WriteAllLines(Path.Combine(root, "result.txt"), results);
            EditorApplication.Exit(exit);
        }
        private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    }
}
