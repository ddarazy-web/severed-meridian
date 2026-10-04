using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using UnityEngine.UI;

namespace PopupUI.Editor
{
    [InitializeOnLoad]
    public static class PopupTemplateGenerator
    {
        private const string RequestKey = "PopupUI.TemplateRequest";
        [Serializable]
        private sealed class Generation
        {
            public string Feature, Name, Id, Status, Error, Started;
        }
        private static readonly HashSet<string> Keywords = new HashSet<string>("abstract as base bool break byte case catch char checked class const continue decimal default delegate do double else enum event explicit extern false finally fixed float for foreach goto if implicit in int interface internal is lock long namespace new null object operator out override params private protected public readonly ref return sbyte sealed short sizeof stackalloc static string struct switch this throw true try typeof uint ulong unchecked unsafe ushort using virtual void volatile while record required file scoped var dynamic".Split(' '), StringComparer.Ordinal);
        static PopupTemplateGenerator()
        {
            EditorApplication.update += CompletePending;
            CompilationPipeline.assemblyCompilationFinished += OnCompilationFinished;
        }
        public static string Status => ReadRequest()?.Status ?? "Idle";
        public static string Details
        {
            get
            {
                Generation request = ReadRequest();
                return request == null ? "생성 요청 없음" : request.Status + " / " + request.Feature + "/" + request.Name + " / ID " + request.Id + "\n" + request.Error;
            }
        }
        private static Generation ReadRequest()
        {
            string json = SessionState.GetString(RequestKey, "");
            return json.Length == 0 ? null : JsonUtility.FromJson<Generation>(json);
        }
        private static void SaveRequest(Generation request) { SessionState.SetString(RequestKey, JsonUtility.ToJson(request)); }
        public static string[] ValidateRequest(string feature, string name, string id)
        {
            List<string> errors = new List<string>();
            if (string.IsNullOrEmpty(feature) || !Regex.IsMatch(feature, "^[A-Za-z_][A-Za-z0-9_]*$") || Keywords.Contains(feature)) errors.Add("기능 이름은 예약어가 아닌 C# 식별자여야 합니다.");
            else if (Regex.IsMatch(feature, "^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])$", RegexOptions.IgnoreCase)) errors.Add("기능 이름에 Windows 장치 경로를 사용할 수 없습니다.");
            if (string.IsNullOrEmpty(name) || !Regex.IsMatch(name, "^[A-Za-z_][A-Za-z0-9_]*$") || Keywords.Contains(name)) errors.Add("팝업 이름은 예약어가 아닌 C# 식별자여야 합니다.");
            else if (Regex.IsMatch(name, "^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])$", RegexOptions.IgnoreCase)) errors.Add("팝업 이름에 Windows 장치 경로를 사용할 수 없습니다.");
            if (string.IsNullOrWhiteSpace(id)) errors.Add("ID가 비어 있습니다.");
            if (errors.Count != 0) return errors.ToArray();
            string source = "Assets/Scripts/Features/" + feature + "/Runtime/UI/" + name;
            string[] paths = { source + "State.cs", source + "View.cs", "Assets/Prefabs/UI/" + feature + "/" + name + ".prefab" };
            foreach (string path in paths)
                if (File.Exists(path) || Directory.Exists(path) || File.Exists(path + ".meta")) errors.Add("기존 출력과 충돌합니다: " + path);
            return errors.ToArray();
        }
        public static void Request(string feature, string name, string id)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || Status == "Pending") throw new InvalidOperationException("Play Mode 또는 컴파일/생성 진행 중에는 생성할 수 없습니다.");
            string[] errors = ValidateRequest(feature, name, id);
            if (errors.Length != 0) throw new InvalidOperationException(string.Join("\n", errors));
            string source = "Assets/Scripts/Features/" + feature + "/Runtime/UI/";
            Generation request = new Generation { Feature = feature, Name = name, Id = id, Status = "Pending", Started = DateTime.UtcNow.ToString("O") };
            List<string> created = new List<string>();
            try
            {
                Directory.CreateDirectory(source);
                string state = $@"using PopupUI;
namespace {feature}
{{
    public sealed class {name}State : PopupState
    {{
        public string Text = ""새 팝업"";
        public override PopupState Copy() => new {name}State {{ Text = Text }};
    }}
}}
";
                string view = $@"using PopupUI;
using UnityEngine;
using UnityEngine.UI;
namespace {feature}
{{
    public sealed class {name}View : PopupView
    {{
        [SerializeField] private Text label;
        [SerializeField] private Button closeButton;
        private {name}State value = new {name}State();
        public override void ApplyState(PopupState state)
        {{
            value = state == null ? new {name}State() : ({name}State)state.Copy();
            if (label != null) label.text = value.Text;
        }}
        public override PopupState CaptureState() => value.Copy();
        private void OnEnable() {{ if (closeButton != null) closeButton.onClick.AddListener(Close); }}
        private void OnDisable() {{ if (closeButton != null) closeButton.onClick.RemoveListener(Close); }}
    }}
}}
";
                foreach (KeyValuePair<string, string> output in new[] { new KeyValuePair<string, string>(source + name + "State.cs", state), new KeyValuePair<string, string>(source + name + "View.cs", view) })
                {
                    // 사전 검사 뒤에도 CreateNew로 기존 파일 덮어쓰기를 막는다.
                    using (FileStream file = new FileStream(output.Key, FileMode.CreateNew, FileAccess.Write))
                    { created.Add(output.Key); using (StreamWriter writer = new StreamWriter(file)) writer.Write(output.Value); }
                    string meta = output.Key + ".meta";
                    using (FileStream file = new FileStream(meta, FileMode.CreateNew, FileAccess.Write))
                    { created.Add(meta); using (StreamWriter writer = new StreamWriter(file)) writer.Write("fileFormatVersion: 2\nguid: " + Guid.NewGuid().ToString("N") + "\n"); }
                }
                SaveRequest(request); AssetDatabase.Refresh();
            }
            catch (Exception error)
            {
                foreach (string path in created) File.Delete(path);
                request.Status = "Failed"; request.Error = error.Message; SaveRequest(request); throw;
            }
        }
        private static void OnCompilationFinished(string assembly, CompilerMessage[] messages)
        {
            Generation request = ReadRequest(); if (request == null || request.Status != "Pending") return;
            foreach (CompilerMessage message in messages)
            {
                if (message.type != CompilerMessageType.Error) continue;
                request.Status = "Failed"; request.Error = "스크립트 컴파일 실패: " + message.message; SaveRequest(request); break;
            }
        }
        private static void CompletePending()
        {
            Generation request = ReadRequest();
            if (request == null || request.Status != "Pending" || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            GameObject root = null;
            try
            {
                string scriptPath = "Assets/Scripts/Features/" + request.Feature + "/Runtime/UI/" + request.Name + "View.cs";
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
                Type viewType = script == null ? null : script.GetClass();
                if (viewType == null)
                {
                    if (DateTime.UtcNow > DateTime.Parse(request.Started, null, System.Globalization.DateTimeStyles.RoundtripKind).AddMinutes(5)) throw new InvalidOperationException("컴파일된 View를 찾지 못했습니다.");
                    return;
                }
                if (!typeof(PopupView).IsAssignableFrom(viewType) || viewType.IsAbstract) throw new InvalidOperationException("생성 View 타입이 유효하지 않습니다.");
                string prefabPath = "Assets/Prefabs/UI/" + request.Feature + "/" + request.Name + ".prefab";
                if (File.Exists(prefabPath) || File.Exists(prefabPath + ".meta")) throw new InvalidOperationException("프리팹 출력이 이미 존재합니다.");
                Directory.CreateDirectory(Path.GetDirectoryName(prefabPath));
                root = new GameObject(request.Name, typeof(RectTransform), typeof(CanvasGroup), typeof(Image)); root.SetActive(false);
                root.GetComponent<Image>().color = new Color(0, 0, 0, 0.4f);
                PopupView view = (PopupView)root.AddComponent(viewType);
                GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image)); panel.transform.SetParent(root.transform, false);
                panel.GetComponent<RectTransform>().sizeDelta = new Vector2(400, 240);
                GameObject label = new GameObject("Text", typeof(RectTransform), typeof(Text)); label.transform.SetParent(panel.transform, false);
                label.GetComponent<RectTransform>().sizeDelta = new Vector2(360, 130);
                Text text = label.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.text = "새 팝업"; text.color = Color.black; text.alignment = TextAnchor.MiddleCenter;
                GameObject close = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button)); close.transform.SetParent(panel.transform, false);
                RectTransform buttonRect = close.GetComponent<RectTransform>(); buttonRect.sizeDelta = new Vector2(100, 40); buttonRect.anchoredPosition = new Vector2(0, -80);
                GameObject caption = new GameObject("Caption", typeof(RectTransform), typeof(Text)); caption.transform.SetParent(close.transform, false);
                RectTransform captionRect = caption.GetComponent<RectTransform>(); captionRect.anchorMin = Vector2.zero; captionRect.anchorMax = Vector2.one; captionRect.offsetMin = captionRect.offsetMax = Vector2.zero;
                Text captionText = caption.GetComponent<Text>(); captionText.font = text.font; captionText.text = "닫기"; captionText.color = Color.black; captionText.alignment = TextAnchor.MiddleCenter;
                SerializedObject data = new SerializedObject(view); data.FindProperty("label").objectReferenceValue = text; data.FindProperty("closeButton").objectReferenceValue = close.GetComponent<Button>(); data.ApplyModifiedPropertiesWithoutUndo();
                view.SetDefaultSelection(close); root.SetActive(true);
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                if (saved == null) throw new InvalidOperationException("프리팹 저장 실패");
                request.Status = "Completed"; request.Error = "State/View: Assets/Scripts/Features/" + request.Feature + "/Runtime/UI/\n프리팹: " + prefabPath; SaveRequest(request);
            }
            catch (Exception error) { request.Status = "Failed"; request.Error = error.Message; SaveRequest(request); }
            finally { if (root != null) UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
