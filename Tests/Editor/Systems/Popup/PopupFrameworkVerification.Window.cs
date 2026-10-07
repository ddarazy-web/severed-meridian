using System;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PopupUI.Editor
{
    public static partial class PopupFrameworkVerification
    {
        private const string WindowTestKey = "PopupFramework.Stage03.WindowTest";
        private const string WindowCatalogFolder = "Assets/Data/PopupStage03OwnedTests";
        private const string WindowCatalogPath = WindowCatalogFolder + "/PopupCatalog.asset";
        private static PopupManagementWindow toolWindow;
        private static int windowTestPhase, windowTestWait;
        private static PopupTemplateWindow templateDiagnosticWindow;
        private static string previousTemplateRequest;
        private static double templateDiagnosticStarted;
        public static void RunWindowControls()
        {
            results.Clear();
            try
            {
                Check(!Directory.Exists(WindowCatalogFolder) && !File.Exists(WindowCatalogFolder + ".meta"), "관리 창 시험 catalog 경로 미사용");
                AssetDatabase.CreateFolder("Assets/Data", "PopupStage03OwnedTests");
                SessionState.SetBool(WindowTestKey, true);
                PopupCatalog catalog = ScriptableObject.CreateInstance<PopupCatalog>();
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Popup/PopupTemplate.prefab");
                catalog.Configure(new[] { new PopupCatalog.Entry { Id = "original", Prefab = prefab.GetComponent<PopupView>(), Restorable = true } });
                AssetDatabase.CreateAsset(catalog, WindowCatalogPath); AssetDatabase.SaveAssetIfDirty(catalog);
                SessionState.SetBool(WindowTestKey, true);
                toolWindow = ScriptableObject.CreateInstance<PopupManagementWindow>(); toolWindow.ShowUtility();
                toolWindow.rootVisualElement.Q<ObjectField>("popup-catalog").value = catalog;
                windowTestPhase = 0; windowTestWait = 0; EditorApplication.update += VerifyCatalogUI;
            }
            catch (Exception error) { FinishWindowVerification(error); }
        }
        private static void VerifyCatalogUI()
        {
            try
            {
                PopupCatalog catalog = AssetDatabase.LoadAssetAtPath<PopupCatalog>(WindowCatalogPath);
                if (windowTestPhase == 0)
                {
                    toolWindow.rootVisualElement.Query<Foldout>().ForEach(fold => fold.value = true);
                    TextField id = toolWindow.rootVisualElement.Q<PropertyField>().Query<TextField>().ToList().FirstOrDefault(field => field.bindingPath != null && (field.bindingPath.EndsWith(".Id") || field.bindingPath == "Id"));
                    if (id == null) { if (++windowTestWait > 300) throw new InvalidOperationException("실제 catalog ID 편집 필드가 생성되지 않았습니다."); return; }
                    Check(!toolWindow.TryOpen("original") && catalog.Entries[0].Id == "original", "Edit Mode 시험 조작 거부 원본 불변");
                    Undo.IncrementCurrentGroup(); id.value = "changed"; windowTestPhase = 1; windowTestWait = 0; return;
                }
                if (++windowTestWait < 3) return;
                windowTestWait = 0;
                if (windowTestPhase == 4)
                {
                    if (EditorApplication.timeSinceStartup - templateDiagnosticStarted < 1) return;
                    Check(templateDiagnosticWindow.rootVisualElement.Children().OfType<Label>().Single().text.Contains("C# 식별자"),
                        "이전 생성 성공 후 잘못된 요청 진단 실제 폴링 뒤 유지");
                    templateDiagnosticWindow.Close(); templateDiagnosticWindow = null;
                    SessionState.SetString("PopupUI.TemplateRequest", previousTemplateRequest); previousTemplateRequest = null;
                    EditorApplication.update -= VerifyCatalogUI;
                    Directory.CreateDirectory(Output); File.WriteAllLines(Output + "stage03-window-results.txt", results);
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    EditorApplication.EnterPlaymode(); return;
                }
                if (windowTestPhase == 1)
                {
                    Check(catalog.Entries[0].Id == "changed" && EditorUtility.IsDirty(catalog), "실제 UI catalog 편집 저장 전 dirty");
                    Undo.FlushUndoRecordObjects(); Undo.PerformUndo(); windowTestPhase = 2; return;
                }
                if (windowTestPhase == 2)
                { Check(catalog.Entries[0].Id == "original", "실제 UI catalog 편집 Undo 복원"); Undo.PerformRedo(); windowTestPhase = 3; return; }
                Check(catalog.Entries[0].Id == "changed" && toolWindow.SaveSelectedCatalog() && !EditorUtility.IsDirty(catalog) && File.ReadAllText(WindowCatalogPath).Contains("changed"), "실제 UI catalog Redo와 명시 저장");
                Selection.activeObject = catalog;
                Check(EditorApplication.ExecuteMenuItem("Tools/Popup/등록 검사"), "등록 검사 메뉴 실제 실행");
                toolWindow.Close(); toolWindow = null; Selection.activeObject = null;
                previousTemplateRequest = SessionState.GetString("PopupUI.TemplateRequest", "");
                SessionState.SetString("PopupUI.TemplateRequest", "{\"Feature\":\"OwnedDiagnostic\",\"Name\":\"Previous\",\"Id\":\"owned.previous\",\"Status\":\"Completed\",\"Error\":\"old success\"}");
                templateDiagnosticWindow = ScriptableObject.CreateInstance<PopupTemplateWindow>(); templateDiagnosticWindow.ShowUtility();
                templateDiagnosticWindow.rootVisualElement.Q<TextField>("popup-template-name").value = "class";
                Button generate = templateDiagnosticWindow.rootVisualElement.Q<Button>("popup-template-generate");
                using (NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled()) { evt.target = generate; generate.SendEvent(evt); }
                Check(templateDiagnosticWindow.rootVisualElement.Children().OfType<Label>().Single().text.Contains("C# 식별자"), "잘못된 요청 진단 실제 생성 버튼 표시");
                templateDiagnosticStarted = EditorApplication.timeSinceStartup; windowTestPhase = 4;
            }
            catch (Exception error) { FinishWindowVerification(error); }
        }
        [InitializeOnLoadMethod]
        private static void InstallWindowVerification()
        {
            EditorApplication.playModeStateChanged += state => {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(WindowTestKey, false)) WindowControlsPlayAsync().Forget(Debug.LogException);
            };
        }
        private static async UniTask WindowControlsPlayAsync()
        {
            GameObject first = null, second = null; PopupService firstService = null, secondService = null;
            Exception failure = null;
            try
            {
                results.Clear(); results.AddRange(File.ReadAllLines(Output + "stage03-window-results.txt"));
                PopupCatalog catalog = AssetDatabase.LoadAssetAtPath<PopupCatalog>(WindowCatalogPath);
                first = new GameObject("Popup-window-first", typeof(RectTransform), typeof(PopupHost));
                second = new GameObject("Popup-window-second", typeof(RectTransform), typeof(PopupHost));
                firstService = new PopupService(catalog); secondService = new PopupService(catalog);
                first.GetComponent<PopupHost>().Attach(firstService, new PopupContext("first", "feature", "game"));
                second.GetComponent<PopupHost>().Attach(secondService, new PopupContext("second", "feature", "game"));
                toolWindow = ScriptableObject.CreateInstance<PopupManagementWindow>(); toolWindow.ShowUtility();
                toolWindow.rootVisualElement.Q<ObjectField>("popup-catalog").value = catalog;
                Check(!toolWindow.rootVisualElement.Q<Button>("popup-save").enabledSelf && !toolWindow.SaveSelectedCatalog(), "Play Mode catalog 저장 UI와 호출 거부");
                toolWindow.SelectHost(first.GetComponent<PopupHost>());
                Check(toolWindow.TryOpen("changed") && firstService.Count == 1 && secondService.Count == 0, "Play Mode 선택 Host만 시험 열기");
                toolWindow.SelectHost(second.GetComponent<PopupHost>());
                Check(toolWindow.TryOpen("changed") && secondService.Count == 1 && firstService.Count == 1, "Host 교체 이전 목록 불변");
                PopupExitTicket ticket = secondService.BeginSceneExit(true);
                await UniTask.Delay(500, ignoreTimeScale: true);
                Check(toolWindow.rootVisualElement.Q<Label>("popup-summary").text.Contains("이동 준비 True"), "관리 창 이동 준비 실제 표시 자동 갱신");
                Check(!toolWindow.TryOpen("changed") && !toolWindow.TryClose(secondService.Top.Value) && secondService.Count == 1, "이동 준비 시험 열기 닫기 거부");
                secondService.RollbackSceneExit(ticket);
                Check(toolWindow.TryClose(secondService.Top.Value) && secondService.Count == 0 && firstService.Count == 1, "선택 Host 핸들만 시험 닫기");
                toolWindow.Close(); toolWindow = null;
                firstService.CloseAll(); secondService.CloseAll();
                Check(catalog.Entries[0].Id == "changed" && !EditorUtility.IsDirty(catalog), "시험 조작과 창 종료 catalog 저장 영향0");
            }
            catch (Exception error) { failure = error; }
            finally
            {
                if (second != null) second.GetComponent<PopupHost>().Detach();
                if (first != null) first.GetComponent<PopupHost>().Detach();
                if (first != null) UnityEngine.Object.Destroy(first); if (second != null) UnityEngine.Object.Destroy(second);
                FinishWindowVerification(failure);
            }
        }
        private static void FinishWindowVerification(Exception error)
        {
            EditorApplication.update -= VerifyCatalogUI;
            if (toolWindow != null) { toolWindow.Close(); toolWindow = null; }
            if (templateDiagnosticWindow != null) { templateDiagnosticWindow.Close(); templateDiagnosticWindow = null; }
            if (previousTemplateRequest != null) { SessionState.SetString("PopupUI.TemplateRequest", previousTemplateRequest); previousTemplateRequest = null; }
            if (error != null) results.Add("FAIL " + error);
            if (SessionState.GetBool(WindowTestKey, false)) AssetDatabase.DeleteAsset(WindowCatalogFolder);
            SessionState.EraseBool(WindowTestKey);
            Directory.CreateDirectory(Output); results.Add("UTC " + DateTime.UtcNow.ToString("O"));
            File.WriteAllLines(Output + "stage03-window-results.txt", results); EditorApplication.Exit(error == null ? 0 : 1);
        }
    }
}
