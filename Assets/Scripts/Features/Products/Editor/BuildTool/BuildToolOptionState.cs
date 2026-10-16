using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Products.Editor
{
    [Serializable]
    public sealed class BuildToolOptionState
    {
        public string id;
        public int version;
        public bool enabled;
        public List<BuildToolOptionStage> started = new List<BuildToolOptionStage>();
        public List<BuildToolOptionStage> completed = new List<BuildToolOptionStage>();
    }

    [Serializable]
    public sealed class BuildToolOptionMutation
    {
        public string owner, key, before, expected, beforeFingerprint, afterFingerprint;
        public BuildToolOptionStage stage;
        public bool applied, cleaned;
    }

    public sealed class BuildToolOptionContext
    {
        public BuildToolRequest Request { get; }
        public bool Enabled => state.enabled;
        public BuildToolResult Result => Request.result;
        private readonly BuildToolOptionState state;
        private readonly BuildToolOptionStage stage;
        private readonly BuildToolOptions options;
        private readonly BuildToolRunStore store;
        private readonly IBuildToolBackend backend;

        internal BuildToolOptionContext(BuildToolRequest request, BuildToolOptionState state, BuildToolOptionStage stage,
            BuildToolOptions options, BuildToolRunStore store, IBuildToolBackend backend)
        { Request = request; this.state = state; this.stage = stage; this.options = options; this.store = store; this.backend = backend; }

        /// <summary>등록한 설정의 변경 계획을 먼저 저장하고 적용한다. 예상 지문은 어댑터가 변경 전에 계산한다.</summary>
        /// <param name="key">해당 옵션이 소유한 설정 키.</param>
        /// <param name="value">이번 실행의 ON/OFF에 따른 적용 값.</param>
        public void Apply(string key, string value)
        {
            if (stage == BuildToolOptionStage.Cleanup || state.id != "__symbols" && !options.Handler(state).Definition.Settings.Contains(key))
                throw new InvalidOperationException("해당 옵션이 변경할 수 없는 설정입니다: " + key);
            var setting = options.Setting(key);
            var mutation = Request.optionMutations.SingleOrDefault(item => item.owner == state.id && item.stage == stage && item.key == key);
            if (mutation == null)
            {
                options.CheckFingerprint(Request, backend);
                var previous = Request.optionMutations.LastOrDefault(item => item.key == key && !item.cleaned);
                if (previous != null && setting.Read(Request) != previous.expected)
                    throw new InvalidOperationException("다음 옵션 단계 전에 외부 변경을 보존했습니다: " + key);
                mutation = new BuildToolOptionMutation { owner = state.id, stage = stage, key = key, before = setting.Read(Request), expected = value,
                    beforeFingerprint = Request.effectiveFingerprint ?? Request.fingerprint, afterFingerprint = setting.PredictFingerprint(Request, value) };
                if (string.IsNullOrEmpty(mutation.afterFingerprint)) throw new InvalidOperationException("옵션의 예상 지문이 없습니다: " + key);
                Request.optionMutations.Add(mutation); Request.effectiveFingerprint = mutation.afterFingerprint;
                store.Save(Request);
            }
            if (mutation.expected != value || mutation.cleaned) throw new InvalidOperationException("확정한 옵션 적용값이 달라졌습니다: " + key);
            string actual = setting.Read(Request); string fingerprint = backend.Fingerprint(backend.Read(Request.profileGuid));
            if (mutation.applied && actual == mutation.expected) { options.CheckFingerprint(Request, backend); return; }
            if (actual == mutation.expected && fingerprint == mutation.afterFingerprint)
            { mutation.applied = true; store.Save(Request); return; }
            if (mutation.applied || actual != mutation.before || fingerprint != mutation.beforeFingerprint)
                throw new InvalidOperationException("옵션 적용 중 외부 설정 변경을 보존했습니다: " + key);
            setting.Write(Request, mutation.expected);
            if (setting.Read(Request) != mutation.expected || backend.Fingerprint(backend.Read(Request.profileGuid)) != mutation.afterFingerprint)
                throw new InvalidOperationException("옵션 적용 결과가 변경 전 예상값과 다릅니다: " + key);
            mutation.applied = true; store.Save(Request);
        }
    }

    public sealed partial class BuildToolOptions
    {
        public void CheckFingerprint(BuildToolRequest request, IBuildToolBackend backend)
        {
            string actual = backend.Fingerprint(backend.Read(request.profileGuid));
            var pending = request.optionMutations.LastOrDefault(item => !item.cleaned && !item.applied);
            if (actual == (request.effectiveFingerprint ?? request.fingerprint) || pending != null && actual == pending.beforeFingerprint) return;
            throw new InvalidOperationException("확정 후 외부에서 프로필 설정이 변경됐습니다. 자동으로 새 기준을 채택하지 않습니다.");
        }

        public void RunStage(BuildToolOptionStage stage, BuildToolRequest request, BuildToolRunStore store, IBuildToolBackend backend)
        {
            foreach (var state in request.options) Handler(state);
            if (stage == BuildToolOptionStage.PrepareCompilation && !request.sharedSymbolsPrepared && request.options.Count > 0 && request.options.Any(state => Handler(state).Definition.Symbols.Length > 0))
            {
                const string owner = "__symbols";
                var previous = request.optionMutations.FirstOrDefault(item => item.owner == owner);
                string original = previous?.before ?? Setting(BuildToolOptionSetting.SharedSymbols).Read(request);
                string[] allSymbols = request.options.SelectMany(state => Handler(state).Definition.Symbols).Distinct().ToArray();
                string[] required = request.options.Where(state => state.enabled).SelectMany(state => Handler(state).Definition.Symbols)
                    .Concat(Setting(BuildToolOptionSetting.SharedSymbols).ProtectedSymbols).ToArray();
                string value = string.Join(";", original.Split(';').Where(symbol => !allSymbols.Contains(symbol)).Concat(required).Where(symbol => symbol.Length > 0).Distinct().OrderBy(symbol => symbol, StringComparer.Ordinal));
                new BuildToolOptionContext(request, new BuildToolOptionState { id = owner }, stage, this, store, backend).Apply(BuildToolOptionSetting.SharedSymbols, value);
                request.sharedSymbolsPrepared = true; store.Save(request);
            }
            foreach (var state in request.options)
            {
                if (state.completed.Contains(stage)) continue;
                var handler = Handler(state);
                if (!state.started.Contains(stage)) { state.started.Add(stage); store.Save(request); }
                if (handler.Handles(stage)) handler.Execute(stage, new BuildToolOptionContext(request, state, stage, this, store, backend));
                CheckFingerprint(request, backend);
                state.completed.Add(stage); store.Save(request);
                if (backend.IsBusy) return;
            }
        }

        public bool StageComplete(BuildToolOptionStage stage, BuildToolRequest request) => request.options.All(state => state.completed.Contains(stage));

        public void Cleanup(BuildToolRequest request, BuildToolRunStore store, IBuildToolBackend backend)
        {
            var errors = new List<string>();
            foreach (var mutation in request.optionMutations.AsEnumerable().Reverse())
            {
                if (mutation.cleaned) continue;
                try
                {
                    var setting = Setting(mutation.key); string actual = setting.Read(request);
                    string original = request.optionMutations.First(item => item.key == mutation.key).before;
                    // 여러 단계가 같은 키를 바꿨더라도 최종 원본이면 이전 정리는 이미 완료됐다.
                    if (actual != original && actual != mutation.before)
                    {
                        if (actual != mutation.expected) throw new InvalidOperationException("외부 변경 보존: " + mutation.key);
                        setting.Write(request, mutation.before);
                        if (setting.Read(request) != mutation.before) throw new InvalidOperationException("원복 확인 실패: " + mutation.key);
                    }
                    mutation.cleaned = true; store.Save(request);
                }
                catch (Exception error) { errors.Add(error.Message); }
            }
            foreach (var state in request.options.AsEnumerable().Reverse())
            {
                if (state.completed.Contains(BuildToolOptionStage.Cleanup) || state.started.Count == 0) continue;
                try
                {
                    var handler = Handler(state);
                    if (handler.Handles(BuildToolOptionStage.Cleanup)) handler.Execute(BuildToolOptionStage.Cleanup,
                        new BuildToolOptionContext(request, state, BuildToolOptionStage.Cleanup, this, store, backend));
                    state.completed.Add(BuildToolOptionStage.Cleanup); store.Save(request);
                }
                catch (Exception error) { errors.Add(state.id + ": " + error.Message); }
            }
            if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
        }
    }

    internal static class BuildToolPreferences
    {
        internal static JObject Read(string path)
        {
            if (!File.Exists(path)) return new JObject { ["version"] = 1 };
            var data = JObject.Parse(File.ReadAllText(path));
            if ((int?)data["version"] != 1) throw new InvalidDataException("지원하지 않는 빌드 도구 개인 설정입니다.");
            return data;
        }
        internal static Dictionary<string, bool> Choices(string path, string guid) =>
            Read(path)["options"]?[guid]?.ToObject<Dictionary<string, bool>>() ?? new Dictionary<string, bool>();
        internal static void SaveSelection(string path, string guid)
        {
            JObject data;
            try { data = Read(path); }
            catch (Newtonsoft.Json.JsonException) { data = new JObject { ["version"] = 1 }; }
            catch (InvalidDataException) { data = new JObject { ["version"] = 1 }; }
            data["profileGuid"] = guid; Save(path, data);
        }
        internal static void SaveChoice(string path, string guid, string id, bool enabled)
        {
            var data = Read(path);
            if (!(data["options"] is JObject)) data["options"] = new JObject();
            var profiles = (JObject)data["options"];
            if (!(profiles[guid] is JObject)) profiles[guid] = new JObject();
            ((JObject)profiles[guid])[id] = enabled; Save(path, data);
        }
        internal static void SaveChoices(string path, string guid, Dictionary<string, bool> choices)
        {
            var data = Read(path);
            if (!(data["options"] is JObject)) data["options"] = new JObject();
            data["options"][guid] = JObject.FromObject(choices);
            Save(path, data);
        }
        private static void Save(string path, JObject data)
        { Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, data.ToString()); }
    }
}
