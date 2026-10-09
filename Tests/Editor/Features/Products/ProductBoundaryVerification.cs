using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Products.Editor
{
    public static class ProductBoundaryVerification
    {
        public static void Run()
        {
            try
            {
                Type rules = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Products.Editor.ProductProfiles")).FirstOrDefault(t => t != null);
                Check(rules != null, "product configuration and audit exist");
                MethodInfo validate = rules.GetMethod("ValidateSelection");
                validate.Invoke(null, new object[] { new[] { "PRODUCT_ANDROID_GAME" }, BuildTarget.Android });
                validate.Invoke(null, new object[] { new[] { "PRODUCT_IOS_GAME" }, BuildTarget.iOS });
                validate.Invoke(null, new object[] { new[] { "PRODUCT_STEAM_GAME" }, BuildTarget.StandaloneWindows64 });
                validate.Invoke(null, new object[] { new[] { "PRODUCT_LEVEL_EDITOR" }, BuildTarget.StandaloneWindows64 });
                Reject(() => validate.Invoke(null, new object[] { new[] { "PRODUCT_STEAM_GAME", "PRODUCT_LEVEL_EDITOR" }, BuildTarget.StandaloneWindows64 }), "two products rejected");
                Reject(() => validate.Invoke(null, new object[] { new[] { "PRODUCT_ANDROID_GAME" }, BuildTarget.iOS }), "wrong target rejected");
                Reject(() => validate.Invoke(null, new object[] { Array.Empty<string>(), BuildTarget.Android }), "missing product rejected");
                Reject(() => validate.Invoke(null, new object[] { new[] { "PRODUCT_NEW_UNKNOWN" }, BuildTarget.Android }), "unknown product rejected");
                Reject(() => rules.GetMethod("ValidateBuildOptions").Invoke(null, new object[] { BuildOptions.IncludeTestAssemblies }), "product test assemblies option rejected");
                MethodInfo dependency = rules.GetMethod("ValidateDependency");
                Reject(() => dependency.Invoke(null, new object[] { "Assets/Scenes/LevelTool.unity", false }), "game tool scene rejected");
                Reject(() => dependency.Invoke(null, new object[] { "Assets/__ProjectTests/Probe.cs", true }), "tool test dependency rejected");
                byte[] globalSettings = System.IO.File.ReadAllBytes("ProjectSettings/ProjectSettings.asset");
                rules.GetMethod("Configure").Invoke(null, null);
                Check(globalSettings.SequenceEqual(System.IO.File.ReadAllBytes("ProjectSettings/ProjectSettings.asset")), "global PlayerSettings unchanged");
                UnityEditor.Build.Profile.BuildProfile original = AssetDatabase.LoadAssetAtPath<UnityEditor.Build.Profile.BuildProfile>("Assets/Settings/BuildProfiles/Windows Steam Game.asset");
                UnityEditor.Build.Profile.BuildProfile clone = UnityEngine.Object.Instantiate(original);
                try
                {
                    clone.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/LevelTool.unity", true) };
                    Reject(() => rules.GetMethod("ValidateProfile").Invoke(null, new object[] { clone }), "game profile with tool scene rejected");
                }
                finally { UnityEngine.Object.DestroyImmediate(clone); }
                foreach (var assembly in UnityEditor.Compilation.CompilationPipeline.GetAssemblies(UnityEditor.Compilation.AssembliesType.PlayerWithoutTestAssemblies))
                {
                    string[] nunit = assembly.compiledAssemblyReferences.Where(value => System.IO.Path.GetFileName(value) == "nunit.framework.dll").ToArray();
                    if (nunit.Length == 0) continue;
                    string[] sourceUses = assembly.sourceFiles.Where(value => System.IO.File.Exists(value) && System.IO.File.ReadAllText(value).Contains("NUnit")).ToArray();
                    Debug.Log("NUNIT-DIAG assembly=" + assembly.name + "; flags=" + assembly.flags + "; paths=" + string.Join(",", nunit) + "; sourceMentions=" + string.Join(",", sourceUses));
                }
                string report = (string)rules.GetMethod("AuditAll").Invoke(null, null);
                Check(report.Contains("PRODUCT_ANDROID_GAME") && report.Contains("PRODUCT_IOS_GAME") && report.Contains("PRODUCT_STEAM_GAME") && report.Contains("PRODUCT_LEVEL_EDITOR"), "all products audited");
                System.IO.Directory.CreateDirectory("Logs/GameAuthoringStage06");
                System.IO.File.WriteAllText("Logs/GameAuthoringStage06/product-audit.txt", report);
                Debug.Log("ProductBoundaryVerification PASS\n" + report);
                EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
        static void Reject(Action action, string message)
        {
            try { action(); throw new Exception(message + " failed"); }
            catch (TargetInvocationException error) when (error.InnerException is BuildFailedException) { Debug.Log("PASS " + message); }
        }
        static void Check(bool result, string message) { if (!result) throw new Exception(message); Debug.Log("PASS " + message); }
    }
}
