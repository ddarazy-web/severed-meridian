using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using Products.Editor;

public static class BuildToolFirstBuildFingerprintVerification
{
    // 성공한 첫 실제 빌드 뒤 실행한다. 새 빌드를 호출하거나 설정을 저장하지 않는다.
    public static string Main()
    {
        var runner = BuildToolController.Runner;
        var request = runner.Current;
        if (request.phase != BuildToolRunPhase.Succeeded || request.id != File.ReadAllText("Logs/WindowsLevelToolFirstBuild-id.txt"))
            throw new InvalidOperationException("첫 레벨툴 빌드 요청이 성공해야 합니다.");
        var backend = new UnityProfileBuildBackend();
        var profile = backend.Read(request.profileGuid);
        string fingerprint = backend.Fingerprint(profile);
        BuildToolOptions.Default.CheckFingerprint(request, backend);
        PlayerSettings.SetDynamicBatchingForPlatform(request.target, PlayerSettings.GetDynamicBatchingForPlatform(request.target));
        if (backend.Fingerprint(profile) != fingerprint)
            throw new InvalidOperationException("동일 배칭 재적용이 지문을 변경했습니다.");

        var settingsMethod = typeof(ProductProfiles).GetMethod("ProfileSettings", BindingFlags.Static | BindingFlags.NonPublic);
        var settings = (UnityEngine.Object)settingsMethod.Invoke(null, new object[] { profile.Profile, false });
        var serialized = new SerializedObject(settings);
        var name = serialized.FindProperty("productName");
        string originalName = name.stringValue;
        bool wasDirty = EditorUtility.IsDirty(settings);
        bool postBuildRejected = false, preparationRejected = false;
        try
        {
            name.stringValue = originalName + "-external-change-fixture";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            try { BuildToolOptions.Default.CheckFingerprint(request, backend); }
            catch (InvalidOperationException) { postBuildRejected = true; }
            try
            {
                typeof(UnityProfileBuildBackend).GetMethod("PrepareRenderPipelineSettings", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(backend, new object[] { request, profile });
            }
            catch (TargetInvocationException error) when (error.InnerException is InvalidOperationException)
            { preparationRejected = true; }
        }
        finally
        {
            serialized.Update();
            serialized.FindProperty("productName").stringValue = originalName;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            if (!wasDirty) EditorUtility.ClearDirty(settings);
        }
        if (!postBuildRejected || !preparationRejected || backend.Fingerprint(profile) != fingerprint)
            throw new InvalidOperationException("외부 변경 차단 또는 검사 원복이 실패했습니다.");
        BuildToolOptions.Default.CheckFingerprint(request, backend);
        string result = "PASS first request succeeded; repeated batching stable; external changes rejected before preparation and after build; fixture restored";
        File.WriteAllText("Logs/FirstBuildFingerprintVerification-result.txt", result);
        return result;
    }
}
