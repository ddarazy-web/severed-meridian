using System;
using System.Collections.Generic;
using System.Linq;

namespace Products.Editor
{
    public enum BuildToolOptionStage { PrepareCompilation, BeforeBuild, AfterBuild, Cleanup }

    public sealed class BuildToolOptionDefinition
    {
        public string Id, Label, Tooltip;
        public int Version = 1;
        public bool DefaultEnabled;
        public string DisabledLabel, EnabledLabel;
        public string[] Requires = Array.Empty<string>(), Conflicts = Array.Empty<string>(), Settings = Array.Empty<string>(), Symbols = Array.Empty<string>();
        public Func<ProductDefinition, bool> AppliesTo = product => true;
    }

    public interface IBuildToolOptionHandler
    {
        BuildToolOptionDefinition Definition { get; }
        bool Handles(BuildToolOptionStage stage);
        void Execute(BuildToolOptionStage stage, BuildToolOptionContext context);
    }

    // SDK 어댑터가 실제 설정 접근과 변경 전 예상 지문을 제공한다. 설치·제거는 처리하지 않는다.
    public sealed class BuildToolOptionSetting
    {
        public const string SharedSymbols = "profile.scriptingDefines";
        public string Key;
        public Func<BuildToolRequest, string> Read;
        public Action<BuildToolRequest, string> Write;
        public Func<BuildToolRequest, string, string> PredictFingerprint;
        public string[] ProtectedSymbols = Array.Empty<string>();
    }

    public sealed partial class BuildToolOptions
    {
        public static readonly BuildToolOptions Default = new BuildToolOptions(new IBuildToolOptionHandler[] { new BuildToolDebugMode(), new BuildToolAndroidBundle() });
        private readonly Dictionary<string, IBuildToolOptionHandler> handlers;
        private readonly Dictionary<string, BuildToolOptionSetting> settings;

        public BuildToolOptions(IBuildToolOptionHandler[] registrations, params BuildToolOptionSetting[] settingRegistrations)
        {
            handlers = registrations.ToDictionary(handler => handler.Definition.Id, StringComparer.Ordinal);
            settings = settingRegistrations.ToDictionary(setting => setting.Key, StringComparer.Ordinal);
            foreach (var setting in settings.Values)
                if (string.IsNullOrWhiteSpace(setting.Key) || setting.Read == null || setting.Write == null || setting.PredictFingerprint == null)
                    throw new InvalidOperationException("옵션 설정 접근·예상 지문 등록이 필요합니다.");
            foreach (var handler in handlers.Values)
            {
                var definition = handler.Definition;
                if (string.IsNullOrWhiteSpace(definition.Id) || definition.Id.StartsWith("__", StringComparison.Ordinal) || definition.Version < 1 ||
                    string.IsNullOrWhiteSpace(definition.Label) || string.IsNullOrWhiteSpace(definition.Tooltip) || definition.AppliesTo == null)
                    throw new InvalidOperationException("옵션 ID·버전·이름·설명이 필요합니다.");
                if (definition.Symbols.Any(symbol => string.IsNullOrWhiteSpace(symbol) || symbol.StartsWith("PRODUCT_", StringComparison.Ordinal) ||
                    !(char.IsLetter(symbol[0]) || symbol[0] == '_') || symbol.Any(character => !char.IsLetterOrDigit(character) && character != '_')))
                    throw new InvalidOperationException("옵션 심볼은 제품 필수 심볼과 분리된 식별자여야 합니다.");
                foreach (string key in definition.Settings)
                    if (key.StartsWith("profile.", StringComparison.Ordinal) || key.StartsWith("product.", StringComparison.Ordinal) ||
                        key.StartsWith("shared.", StringComparison.Ordinal) || !settings.ContainsKey(key))
                        throw new InvalidOperationException("필수/공유 설정 침범 또는 미등록 옵션 설정: " + key);
                if (definition.Symbols.Length > 0 && !settings.ContainsKey(BuildToolOptionSetting.SharedSymbols))
                    throw new InvalidOperationException("공유 심볼 접근자를 먼저 등록하세요.");
            }
        }

        public IReadOnlyList<BuildToolOptionDefinition> Available(ProductDefinition product) => handlers.Values.Select(handler => handler.Definition)
            .Where(definition => definition.AppliesTo(product)).OrderBy(definition => definition.Id, StringComparer.Ordinal).ToArray();

        public List<BuildToolOptionState> Resolve(ProductDefinition product, IReadOnlyDictionary<string, bool> choices)
        {
            var available = Available(product).ToDictionary(definition => definition.Id, StringComparer.Ordinal);
            var values = available.Values.ToDictionary(definition => definition.Id,
                definition => choices != null && choices.TryGetValue(definition.Id, out bool chosen) ? chosen : definition.DefaultEnabled, StringComparer.Ordinal);
            var owners = new Dictionary<string, string>(); var visiting = new HashSet<string>(); var visited = new HashSet<string>();
            var result = new List<BuildToolOptionState>();
            foreach (string id in available.Keys.OrderBy(value => value, StringComparer.Ordinal)) Visit(id);
            return result;

            void Visit(string id)
            {
                if (visited.Contains(id)) return;
                if (!visiting.Add(id)) throw new InvalidOperationException("옵션 의존성 순환: " + id);
                var definition = available[id];
                foreach (string dependency in definition.Requires)
                {
                    if (!available.ContainsKey(dependency)) throw new InvalidOperationException("지원하지 않는 옵션 의존성: " + id + " → " + dependency);
                    if (values[id] && !values[dependency]) throw new InvalidOperationException(definition.Label + ": " + dependency + " 옵션을 먼저 켜세요.");
                    Visit(dependency);
                }
                if (values[id] && definition.Conflicts.Any(conflict => values.TryGetValue(conflict, out bool enabled) && enabled))
                    throw new InvalidOperationException("상충하는 옵션이 켜져 있습니다: " + definition.Label);
                // OFF도 설정을 적용하므로 설정 소유권은 OFF 항목까지 검사한다.
                foreach (string key in definition.Settings)
                {
                    if (owners.TryGetValue(key, out string owner) && owner != id) throw new InvalidOperationException("설정 소유권 충돌: " + owner + " / " + id + " / " + key);
                    owners[key] = id;
                }
                visiting.Remove(id); visited.Add(id);
                result.Add(new BuildToolOptionState { id = id, version = definition.Version, enabled = values[id] });
            }
        }

        internal IBuildToolOptionHandler Handler(BuildToolOptionState state)
        {
            if (!handlers.TryGetValue(state.id, out var handler) || handler.Definition.Version != state.version)
                throw new InvalidOperationException("확정한 옵션 처리기가 없거나 버전이 변경됐습니다: " + state.id);
            return handler;
        }

        internal BuildToolOptionSetting Setting(string key) => settings.TryGetValue(key, out var setting) ? setting :
            throw new InvalidOperationException("복원에 필요한 설정 접근자가 없습니다: " + key);
    }
}
