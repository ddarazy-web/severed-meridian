using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
namespace LevelAuthoring.Editor
{
    public static class JsonDependencySetup
    {
        private static AddRequest request;
        private static double deadline;
        public static void Run()
        {
            request = Client.Add("com.unity.nuget.newtonsoft-json@3.2.1");
            deadline = EditorApplication.timeSinceStartup + 300;
            EditorApplication.update += Poll;
        }
        private static void Poll()
        {
            if (!request.IsCompleted && EditorApplication.timeSinceStartup < deadline) return;
            EditorApplication.update -= Poll;
            bool success = request.IsCompleted && request.Status == StatusCode.Success;
            if (success) Debug.Log("JSON dependency: " + request.Result.packageId);
            else Debug.LogError("JSON dependency: " + (request.Error?.message ?? "timeout"));
            EditorApplication.Exit(success ? 0 : 1);
        }
    }
}
