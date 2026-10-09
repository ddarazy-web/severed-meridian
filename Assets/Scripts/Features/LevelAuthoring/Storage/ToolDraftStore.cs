#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.IO;
using System.Text;
using LevelAuthoring.Documents;
using LevelAuthoring.Editing;
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
                    if (File.Exists(path)) File.Replace(temporary, path, path + ".previous");
                    else File.Move(temporary, path);
                    lastSequence = sequence;
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
        }

        public JObject Read()
        {
            lock (gate)
            {
                if (!File.Exists(path) && !File.Exists(path + ".previous")) return null;
                try { JObject record = JObject.Parse(File.ReadAllText(path)); Validate(record); return record; }
                catch (Exception error) when (error is IOException || error is JsonException || error is ContentFormatException)
                {
                    if (!File.Exists(path + ".previous")) throw new ContentFormatException("레벨툴 복구 기록을 읽을 수 없습니다.", error);
                    JObject previous = JObject.Parse(File.ReadAllText(path + ".previous")); Validate(previous); return previous;
                }
            }
        }

        private static void Validate(JObject record)
        {
            if ((int?)record["version"] != 1 || record["workspace"]?.Type != JTokenType.String || !(record["view"] is JObject))
                throw new ContentFormatException("레벨툴 복구 기록 형식이 잘못됐습니다.");
            new AuthoringToolWorkspace().RestoreState((string)record["workspace"]);
        }
    }
}
#endif
