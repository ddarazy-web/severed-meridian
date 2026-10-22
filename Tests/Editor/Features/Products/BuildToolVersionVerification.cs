using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Products.Editor;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEngine;
using UnityEngine.UIElements;

public static class BuildToolVersionVerification
{
    public static object Main()
    {
        Type implementation = typeof(BuildToolProfiles).Assembly.GetType("Products.Editor.BuildToolVersionSettings", true);
        MethodInfo save = implementation.GetMethod("Save", BindingFlags.Public | BindingFlags.Static);
        MethodInfo read = implementation.GetMethod("Read", BindingFlags.Public | BindingFlags.Static);
        string folder = "Assets/__VersionProbe-" + Guid.NewGuid().ToString("N");
        string activeVersion = PlayerSettings.bundleVersion;
        BuildProfile active = BuildProfile.GetActiveBuildProfile();
        int checkedProfiles = 0;
        EditorWindow window = ScriptableObject.CreateInstance<EditorWindow>();
        window.Show();
        AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
        try
        {
            foreach (ProductDefinition product in ProductProfiles.Definitions)
            {
                string path = folder + "/" + product.Name + ".asset";
                if (!AssetDatabase.CopyAsset(product.ProfilePath, path)) throw new Exception("복제 실패");
                BuildToolProfileInfo profile = BuildToolProfiles.Discover().Single(p => p.Path == path);
                string code = product.Target != BuildTarget.Android && product.Target != BuildTarget.iOS ? null : "17";
                save.Invoke(null, new object[] { profile, "2.3.4", code });
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                profile = BuildToolProfiles.Discover().Single(p => p.Path == path);
                object restored = read.Invoke(null, new object[] { profile });
                Check((string)restored.GetType().GetField("Name").GetValue(restored) == "2.3.4", "이름 복원");
                Check((string)restored.GetType().GetField("Code").GetValue(restored) == code, "코드 복원");
                string persisted = File.ReadAllText(path);
                Check(persisted.Contains("2.3.4"), "디스크 영구 저장");
                Reject(save, profile, "", code);
                if (code != null) Reject(save, profile, "2.3.4", "-1");
                Check(File.ReadAllText(path) == persisted, "잘못된 입력은 저장본 보존");
                MethodInfo draw = implementation.GetMethod("Draw", BindingFlags.Public | BindingFlags.Static);
                var ui = window.rootVisualElement;
                ui.Clear();
                int saves = 0;
                draw.Invoke(null, new object[] { ui, profile, new Action(() => saves++) });
                ui.Q<TextField>("build-version-name").value = "2.3.5";
                Check(saves == 1, "UI 입력 자동 저장");
                ui.Clear(); draw.Invoke(null, new object[] { ui, profile, new Action(() => saves++) });
                Check(ui.Q<TextField>("build-version-name").value == "2.3.5", "UI 재생성 시 마지막 버전 복원");
                if (code != null)
                {
                    ui.Q<TextField>("build-version-code").value = "-1";
                    Check(ui.Q<TextField>("build-version-code").value == code, "UI 잘못된 코드 복원");
                    Check(saves == 1, "잘못된 UI 입력은 저장하지 않음");
                }
                else Check(ui.Q("build-version-code") == null, "Windows에는 지원하지 않는 코드 입력 숨김");
                checkedProfiles++;
            }
            Check(BuildProfile.GetActiveBuildProfile() == active && PlayerSettings.bundleVersion == activeVersion, "활성 프로필과 버전 보존");
            return "PASS " + checkedProfiles + " profiles: persistence, UI autosave/reopen, invalid input, active profile isolation";
        }
        finally { window.Close(); AssetDatabase.DeleteAsset(folder); }
    }
    private static void Reject(MethodInfo save, BuildToolProfileInfo profile, string name, string code)
    {
        try { save.Invoke(null, new object[] { profile, name, code }); }
        catch (TargetInvocationException error) when (error.InnerException is ArgumentException) { return; }
        throw new Exception("잘못된 버전 입력을 허용함");
    }
    private static void Check(bool condition, string label) { if (!condition) throw new Exception(label); }
}
