using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PopupUI.Editor
{
    public static partial class PopupFrameworkVerification
    {
        private const string TemplateTestKey = "PopupFramework.Stage03.TemplateTest";
        private const string TemplateFeature = "PopupOwnedTemplateTest";
        private const string TemplateSource = "Assets/Scripts/Features/PopupOwnedTemplateTest";
        private const string TemplatePrefabs = "Assets/Prefabs/UI/PopupOwnedTemplateTest";
        public static void RunTemplateCompilationFailure()
        { SessionState.SetBool(TemplateTestKey + ".failure", true); RunTemplateGeneration(); }
        public static void RunTemplateGeneration()
        {
            results.Clear();
            try
            {
                Check(!Directory.Exists(TemplateSource) && !Directory.Exists(TemplatePrefabs), "템플릿 시험 전용 경로 미사용");
                SessionState.SetBool(TemplateTestKey, true);
                SessionState.SetString(TemplateTestKey + ".deadline", DateTime.UtcNow.AddMinutes(5).ToString("O"));
                Directory.CreateDirectory(Output); File.WriteAllLines(Output + "stage03-template-results.txt", results);
                PopupTemplateGenerator.Request(TemplateFeature, "Example", "popup.example");
                if (SessionState.GetBool(TemplateTestKey + ".failure", false))
                {
                    SessionState.SetString(TemplateTestKey + ".original", File.ReadAllText("Assets/Scripts/Systems/Popup/Runtime/PopupView.cs"));
                    File.AppendAllText(TemplateSource + "/Runtime/UI/ExampleState.cs", "\n#error PopupStage03OwnedExpectedCompilationFailure\n");
                    AssetDatabase.Refresh();
                }
                ResumeTemplateVerification();
            }
            catch (Exception error) { FinishTemplateVerification(error); }
        }
        [InitializeOnLoadMethod]
        private static void ResumeTemplateVerification()
        {
            if (!SessionState.GetBool(TemplateTestKey, false)) return;
            EditorApplication.update -= VerifyGeneratedTemplate;
            EditorApplication.update += VerifyGeneratedTemplate;
        }
        private static void VerifyGeneratedTemplate()
        {
            if (PopupTemplateGenerator.Status != "Completed" && PopupTemplateGenerator.Status != "Failed")
            {
                if (DateTime.UtcNow > DateTime.Parse(SessionState.GetString(TemplateTestKey + ".deadline", DateTime.UtcNow.ToString("O")), null, System.Globalization.DateTimeStyles.RoundtripKind))
                    FinishTemplateVerification(new TimeoutException("템플릿 생성 대기 시간 초과"));
                return;
            }
            GameObject root = null; PopupCatalog catalog = null; PopupService service = null;
            Exception failure = null;
            try
            {
                results.Clear(); results.AddRange(File.ReadAllLines(Output + "stage03-template-results.txt"));
                if (SessionState.GetBool(TemplateTestKey + ".failure", false))
                {
                    Check(PopupTemplateGenerator.Status == "Failed" && PopupTemplateGenerator.Details.Contains("컴파일 실패"), "실제 컴파일 오류 미완료 진단");
                    Check(!File.Exists(TemplatePrefabs + "/Example.prefab") && File.Exists(TemplateSource + "/Runtime/UI/ExampleView.cs"), "컴파일 실패 프리팹 미생성 생성 소스 유지");
                    Check(File.ReadAllText("Assets/Scripts/Systems/Popup/Runtime/PopupView.cs") == SessionState.GetString(TemplateTestKey + ".original", ""), "컴파일 실패 기존 런타임 원본 보존");
                    return;
                }
                Check(PopupTemplateGenerator.Status == "Completed", "생성 스크립트 실제 컴파일과 프리팹 완료");
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TemplatePrefabs + "/Example.prefab");
                Check(prefab != null && prefab.GetComponent<PopupView>() != null && prefab.GetComponent<CanvasGroup>() != null, "생성 프리팹 View 연결");
                catalog = ScriptableObject.CreateInstance<PopupCatalog>();
                catalog.Configure(new[] { new PopupCatalog.Entry { Id = "popup.example", Prefab = prefab.GetComponent<PopupView>(), Restorable = true } });
                Check(PopupCatalogValidation.Validate(catalog).Length == 0, "생성 프리팹 등록 검사");
                root = new GameObject("Popup-generated-test", typeof(RectTransform), typeof(PopupHost));
                service = new PopupService(catalog); root.GetComponent<PopupHost>().Attach(service, new PopupContext("template", "feature", "session"));
                PopupHandle handle = service.Open("popup.example", null);
                PopupState state = service.GetView(handle).CaptureState(); PopupState copied = state.Copy();
                Check(!ReferenceEquals(state, copied) && state.GetType() == copied.GetType(), "생성 State 값 캡처 독립 복사");
                copied.GetType().GetField("Text").SetValue(copied, "복사본 수정");
                Check((string)state.GetType().GetField("Text").GetValue(state) == "새 팝업" && (string)service.GetView(handle).CaptureState().GetType().GetField("Text").GetValue(service.GetView(handle).CaptureState()) == "새 팝업", "생성 State 복사본 수정 원본 뷰 불변");
                Check(service.Close(handle) && service.Count == 0, "생성 View 열기 닫기");
                string sourcePath = TemplateSource + "/Runtime/UI/ExampleView.cs";
                string original = File.ReadAllText(sourcePath); bool rejected = false;
                try { PopupTemplateGenerator.Request(TemplateFeature, "Example", "popup.example"); } catch (InvalidOperationException) { rejected = true; }
                Check(rejected && File.ReadAllText(sourcePath) == original, "재생성 충돌 거부 기존 파일 보존");
            }
            catch (Exception error) { failure = error; }
            finally
            {
                service?.CloseAll(); if (root != null) UnityEngine.Object.DestroyImmediate(root);
                if (catalog != null) UnityEngine.Object.DestroyImmediate(catalog);
                FinishTemplateVerification(failure);
            }
        }
        private static void FinishTemplateVerification(Exception error)
        {
            EditorApplication.update -= VerifyGeneratedTemplate;
            bool owned = SessionState.GetBool(TemplateTestKey, false); SessionState.EraseBool(TemplateTestKey);
            SessionState.EraseBool(TemplateTestKey + ".failure"); SessionState.EraseString(TemplateTestKey + ".original");
            if (error != null) results.Add("FAIL " + error);
            if (owned)
            {
                // 고정된 시험 전용 두 폴더만 삭제하고 사용자 기능 폴더는 건드리지 않는다.
                string workspace = Path.GetFullPath("Assets") + Path.DirectorySeparatorChar;
                foreach (string path in new[] { TemplateSource, TemplatePrefabs })
                {
                    if (!Path.GetFullPath(path).StartsWith(workspace, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("시험 경로 이탈");
                    if (Directory.Exists(path)) AssetDatabase.DeleteAsset(path);
                }
            }
            Directory.CreateDirectory(Output); results.Add("UTC " + DateTime.UtcNow.ToString("O"));
            File.WriteAllLines(Output + "stage03-template-results.txt", results); EditorApplication.Exit(error == null ? 0 : 1);
        }
    }
}

