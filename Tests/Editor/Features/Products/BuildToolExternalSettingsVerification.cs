using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using Products.Editor;
using UnityEditor;
using UnityEditor.Build.Profile;

public static class BuildToolExternalSettingsVerification
{
    public static object Main()
    {
        Type type = typeof(BuildToolProfiles).Assembly.GetType("Products.Editor.BuildToolExternalSettings", true);
        MethodInfo read = type.GetMethod("Read"), save = type.GetMethod("Save");
        string folder = "Assets/__ExternalSettings-" + Guid.NewGuid().ToString("N");
        string preferences = "Temp/ExternalSettings-" + Guid.NewGuid().ToString("N") + ".json";
        var active = BuildProfile.GetActiveBuildProfile();
        string activeVersion = PlayerSettings.bundleVersion;
        int count = 0;
        AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
        try
        {
            foreach (var product in ProductProfiles.Definitions)
            {
                string path = folder + "/" + product.Name + ".asset";
                Check(AssetDatabase.CopyAsset(product.ProfilePath, path), "copy");
                var profile = BuildToolProfiles.Discover().Single(p => p.Path == path);
                string original = File.ReadAllText(path);
                JObject current = (JObject)read.Invoke(null, new object[] { profile, preferences });
                Check(File.ReadAllText(path) == original && !File.Exists(preferences), "read is read-only");
                Check(current["options"].Count() > 0 && current["revision"] != null, "metadata");
                Check(current.Descendants().OfType<JProperty>().All(p => !p.Name.ToLowerInvariant().Contains("password")), "no password fields");
                string code = product.Target == BuildTarget.StandaloneWindows64 ? null : "19";
                var patch = new JObject { ["expectedRevision"] = current["revision"], ["appVersion"] = new JObject { ["name"] = "3.2.1" },
                    ["options"] = new JObject { ["debug-mode"] = true } };
                if (code != null) patch["appVersion"]["code"] = code;
                JObject result = (JObject)save.Invoke(null, new object[] { profile, patch, preferences });
                Check((string)result["appVersion"]["name"] == "3.2.1", "version saved");
                Check((bool)result["options"].Single(o => (string)o["id"] == "debug-mode")["effective"], "option saved");
                string saved = File.ReadAllText(path), prefs = File.ReadAllText(preferences);
                Reject(save, profile, patch, preferences);
                patch["expectedRevision"] = result["revision"];
                patch["options"]["unknown-option"] = true;
                Reject(save, profile, patch, preferences);
                ((JObject)patch["options"]).Remove("unknown-option");
                patch["appVersion"]["name"] = "";
                Reject(save, profile, patch, preferences);
                Check(File.ReadAllText(path) == saved && File.ReadAllText(preferences) == prefs, "invalid input unchanged");
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                profile = BuildToolProfiles.Discover().Single(p => p.Path == path);
                result = (JObject)read.Invoke(null, new object[] { profile, preferences });
                Check((string)result["appVersion"]["name"] == "3.2.1", "reload");
                patch["expectedRevision"] = result["revision"];
                patch["appVersion"]["name"] = "4.0.0";
                saved = File.ReadAllText(path);
                using (var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    Reject(save, profile, patch, preferences);
                Check(File.ReadAllText(path) == saved && File.ReadAllText(preferences) == prefs, "locked profile cannot report success");
                using (var held = new FileStream(preferences, FileMode.Open, FileAccess.Read, FileShare.Read))
                    Reject(save, profile, patch, preferences);
                Check(File.ReadAllText(path) == saved && File.ReadAllText(preferences) == prefs, "partial save rollback on IO failure");
                profile = BuildToolProfiles.Discover().Single(p => p.Path == path);
                result = (JObject)read.Invoke(null, new object[] { profile, preferences });
                Check((string)result["appVersion"]["name"] == "3.2.1", "rollback restores Unity settings");
                patch["expectedRevision"] = result["revision"];
                patch["appVersion"]["code"] = "-1";
                Reject(save, profile, patch, preferences);
                Check(File.ReadAllText(path) == saved, "invalid or unsupported code unchanged");
                var nameOnly = new JObject { ["expectedRevision"] = result["revision"], ["appVersion"] = new JObject { ["name"] = "3.2.2" } };
                result = (JObject)save.Invoke(null, new object[] { profile, nameOnly, preferences });
                Check((string)result["appVersion"]["code"] == code && File.ReadAllText(preferences) == prefs, "omitted fields preserved");
                File.Delete(preferences);
                count++;
            }
            Check(BuildProfile.GetActiveBuildProfile() == active && PlayerSettings.bundleVersion == activeVersion, "active profile isolation");
            var batch = typeof(BuildToolProfiles).Assembly.GetType("Products.Editor.BuildToolBatch").GetMethod("ExecuteSettings");
            JObject catalog = (JObject)batch.Invoke(null, new object[] { new JObject { ["version"] = 2, ["requestId"] = "catalog-test", ["operation"] = "settings" } });
            Check(catalog["profiles"].Count() == 5, "canonical catalog");
            try { batch.Invoke(null, new object[] { new JObject { ["version"] = 2, ["requestId"] = "invalid-profile", ["operation"] = "settings", ["profileGuid"] = "missing" } }); throw new Exception("unknown profile accepted"); }
            catch (TargetInvocationException) { }
            return "PASS " + count + " profiles: read-only, metadata, save/reload, stale revision, invalid input, omitted fields, IO rollback, catalog, isolation";
        }
        finally { AssetDatabase.DeleteAsset(folder); if (File.Exists(preferences)) File.Delete(preferences); }
    }
    private static void Reject(MethodInfo method, object profile, JObject patch, string path)
    {
        try { method.Invoke(null, new object[] { profile, patch, path }); }
        catch (TargetInvocationException) { return; }
        throw new Exception("invalid input accepted");
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
