#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using LevelAuthoring.Documents;
using LevelAuthoring.Editing;
using LevelAuthoring.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LevelAuthoring.Storage
{
    // 제작 원본과 분리한 복구 기록이다. 미완성 문서와 실행 취소 이력을 그대로 보관한다.
    public sealed class ToolDraftStore
    {
        private readonly string path;
        private readonly object gate = new object();
        private long lastSequence;
        public string ReadWarning { get; private set; }
        public ToolDraftStore(string directory) { path = Path.Combine(Path.GetFullPath(directory), "draft.json"); }

        public void Save(string workspace, JObject view, long sequence)
        {
            lock (gate)
            {
                if (sequence <= lastSequence) return;
                JObject record = new JObject { ["version"] = 1, ["workspace"] = workspace, ["view"] = view.DeepClone() };
                Validate(record);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string temporary = path + ".pending-" + Guid.NewGuid().ToString("N");
                try
                {
                    byte[] bytes = new UTF8Encoding(false).GetBytes(record.ToString(Formatting.None));
                    using (FileStream stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                    Validate(JObject.Parse(File.ReadAllText(temporary)));
                    if (File.Exists(path))
                    {
                        bool preservePrevious = false;
                        try { Validate(JObject.Parse(File.ReadAllText(path))); }
                        catch (Exception error) when (error is IOException || error is JsonException || error is ContentFormatException)
                        { preservePrevious = true; }
                        // 손상된 현재 기록 때문에 마지막 정상 이전 기록까지 잃지 않게 한다.
                        File.Replace(temporary, path, preservePrevious ? null : path + ".previous");
                    }
                    else File.Move(temporary, path);
                    lastSequence = sequence;
                    ReadWarning = null;
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
        }

        public JObject Read()
        {
            lock (gate)
            {
                ReadWarning = null;
                if (!File.Exists(path) && !File.Exists(path + ".previous")) return null;
                try { JObject record = JObject.Parse(File.ReadAllText(path)); Validate(record); return record; }
                catch (Exception error) when (error is IOException || error is JsonException || error is ContentFormatException)
                {
                    if (!File.Exists(path + ".previous")) throw new ContentFormatException("레벨툴 복구 기록을 읽을 수 없습니다.", error);
                    JObject previous = ReadPrevious();
                    ReadWarning = "현재 복구 기록을 읽을 수 없어 이전 복구 기록을 사용했습니다.";
                    return previous;
                }
            }
        }

        public JObject ReadPrevious()
        {
            lock (gate)
            {
                if (!File.Exists(path + ".previous")) return null;
                try { JObject previous = JObject.Parse(File.ReadAllText(path + ".previous")); Validate(previous); return previous; }
                catch (Exception error) when (error is IOException || error is JsonException || error is ContentFormatException)
                { throw new ContentFormatException("레벨툴의 이전 복구 기록을 읽을 수 없습니다.", error); }
            }
        }

        // view는 복구 화면과 입력 초안이다. 구버전에서 없던 필드는 생략을 허용한다.
        // 반환값은 없으며 복원할 수 없는 형식은 ContentFormatException으로 거절한다.
        public static void ValidateView(JObject view)
        {
            try
            {
                if (view == null) throw new ContentFormatException("복구 화면 정보가 없습니다.");
                foreach (string key in new[] { "layer", "brush", "color", "direction", "boardTouchTool", "mobilePanel", "inspectorPage", "toolBotStrategy", "multiMode", "newTutorialCondition", "sharedTutorialDraft", "flowTool", "connectionGenerator", "connectionTarget", "selectedShape" })
                    if (view[key] != null && view[key].Type != JTokenType.Null && view[key].Type != JTokenType.String)
                        throw new ContentFormatException("복구 화면의 문자열 형식이 잘못됐습니다: " + key);
                foreach (string key in new[] { "boardTouchAdd", "boardTouchReplace", "autoFitBoard", "materialsVisible", "levelSettingsVisible", "boardFocus", "tutorialPreview", "wireDrawing" })
                    if (view[key] != null && view[key].Type != JTokenType.Null && view[key].Type != JTokenType.Boolean)
                        throw new ContentFormatException("복구 화면의 선택 형식이 잘못됐습니다: " + key);
                foreach (string key in new[] { "anchor", "seed", "batchCount", "toolBotSeed", "multiSamples", "tutorialStep", "flowFirst", "selectedConnection" })
                    if (view[key] != null && view[key].Type != JTokenType.Null &&
                        (view[key].Type != JTokenType.Integer || view[key].Value<long>() < int.MinValue || view[key].Value<long>() > int.MaxValue))
                        throw new ContentFormatException("복구 화면의 정수 형식이 잘못됐습니다: " + key);
                foreach (string key in new[] { "cellSize", "boardX", "boardY", "propertiesY", "levelsY", "paletteY" })
                    if (view[key] != null && view[key].Type != JTokenType.Null &&
                        (view[key].Type != JTokenType.Integer && view[key].Type != JTokenType.Float ||
                            float.IsNaN(view[key].Value<float>()) || float.IsInfinity(view[key].Value<float>())))
                        throw new ContentFormatException("복구 화면의 위치 형식이 잘못됐습니다: " + key);
                if (view["tutorialPick"] != null && view["tutorialPick"].Type != JTokenType.Null && !(view["tutorialPick"] is JObject))
                    throw new ContentFormatException("복구 튜토리얼 선택 형식이 잘못됐습니다.");
                if (view["tutorialPick"] is JObject pick)
                {
                    if (!(pick["cells"] is JArray cells) || cells.Any(item => item.Type != JTokenType.Integer || item.Value<long>() < 0 || item.Value<long>() >= 81) ||
                        !(pick["baseline"] is JObject) || pick["step"]?.Type != JTokenType.Integer || pick["step"].Value<long>() < 0 || pick["step"].Value<long>() > int.MaxValue)
                        throw new ContentFormatException("복구 튜토리얼 선택 위치가 잘못됐습니다.");
                    foreach (string key in new[] { "level", "source", "flow", "mode", "condition", "binding", "field" })
                        if (pick[key] != null && pick[key].Type != JTokenType.Null && pick[key].Type != JTokenType.String)
                            throw new ContentFormatException("복구 튜토리얼 선택 값이 잘못됐습니다.");
                }
                foreach (string key in new[] { "multiSelection", "flowRoute", "wireRoute", "pendingInputs" })
                {
                    if (view[key] == null || view[key].Type == JTokenType.Null) continue;
                    if (!(view[key] is JArray array)) throw new ContentFormatException("복구 화면의 목록 형식이 잘못됐습니다: " + key);
                    if (key == "multiSelection" && array.Any(item => item.Type != JTokenType.String) ||
                        (key == "flowRoute" || key == "wireRoute") && array.Any(item => item.Type != JTokenType.Integer || item.Value<long>() < int.MinValue || item.Value<long>() > int.MaxValue))
                        throw new ContentFormatException("복구 화면의 목록 값이 잘못됐습니다: " + key);
                    if (key != "pendingInputs") continue;
                    foreach (JToken token in array)
                    {
                        if (!(token is JObject input) || !(input["path"] is JArray path) ||
                            path.Any(item => item.Type != JTokenType.Integer || item.Value<long>() < 0 || item.Value<long>() > int.MaxValue) ||
                            new[] { "name", "type", "text" }.Any(name => input[name]?.Type != JTokenType.String) ||
                            input["focused"]?.Type != JTokenType.Boolean)
                            throw new ContentFormatException("복구 입력 초안 형식이 잘못됐습니다.");
                        foreach (string keyIndex in new[] { "cursor", "selection" })
                            if (input[keyIndex]?.Type != JTokenType.Integer || input[keyIndex].Value<long>() < int.MinValue || input[keyIndex].Value<long>() > int.MaxValue)
                                throw new ContentFormatException("복구 입력 선택 위치가 잘못됐습니다.");
                    }
                }
                if (view["sharedTutorialDraft"]?.Type == JTokenType.String && (string)view["sharedTutorialDraft"] != null)
                    SharedTutorialDraft.Restore((string)view["sharedTutorialDraft"]);
            }
            catch (Exception error) when (error is JsonException || error is ArgumentException || error is InvalidCastException ||
                error is FormatException || error is OverflowException || error is NullReferenceException || error is InvalidOperationException)
            { throw new ContentFormatException("복구 화면 또는 공유 튜토리얼 초안 형식이 잘못됐습니다.", error); }
        }

        private static void Validate(JObject record)
        {
            if (record["version"]?.Type != JTokenType.Integer || record["version"].ToString() != "1" ||
                record["workspace"]?.Type != JTokenType.String || !(record["view"] is JObject))
                throw new ContentFormatException("레벨툴 복구 기록 형식이 잘못됐습니다.");
            try
            {
                new AuthoringToolWorkspace().RestoreState((string)record["workspace"]);
                ValidateView((JObject)record["view"]);
            }
            catch (Exception error) when (error is ArgumentException || error is InvalidCastException || error is FormatException || error is OverflowException)
            { throw new ContentFormatException("레벨툴 복구 작업공간 형식이 잘못됐습니다.", error); }
        }
    }
}
#endif
