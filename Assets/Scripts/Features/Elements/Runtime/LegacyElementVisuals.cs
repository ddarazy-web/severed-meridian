using System;
using System.Collections.Generic;
using System.Linq;
using Levels;

namespace Elements
{
    /// <summary>확정된 구형 아트를 명시적 상태 표로 옮기는 호환 경계. 신규 ID는 별칭을 직접 등록한다.</summary>
    public static class LegacyElementVisuals
    {
        public static ElementVisualCatalog Catalog { get; } = ElementVisualCatalog.FromDto(Create());

        public static ElementVisualCatalog WithOverrides(ElementVisualCatalogDto overrides)
        {
            if (overrides == null) return Catalog;
            ElementVisualCatalogDto builtin = Catalog.ToDto();
            ElementVisualDefinitionDto[] selected = overrides.definitions ?? Array.Empty<ElementVisualDefinitionDto>();
            ElementVisualBindingDto[] aliases = overrides.bindings ?? Array.Empty<ElementVisualBindingDto>();
            HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal), ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (ElementVisualDefinitionDto value in selected)
                if (value == null || string.IsNullOrWhiteSpace(value.key) || !keys.Add(value.key))
                    throw ElementVisualFrame.Error(value?.key ?? "<null>", "중복 또는 빈 시각 키");
            foreach (ElementVisualBindingDto value in aliases)
                if (value == null || string.IsNullOrWhiteSpace(value.id) || !ids.Add(value.id))
                    throw ElementVisualFrame.Error(value?.id ?? "<null>", "중복 또는 빈 ID 별칭");
            return ElementVisualCatalog.FromDto(new ElementVisualCatalogDto
            {
                definitions = builtin.definitions.Where(value => !keys.Contains(value.key)).Concat(selected).ToArray(),
                bindings = builtin.bindings.Where(value => !ids.Contains(value.id)).Concat(aliases).ToArray()
            });
        }

        private static ElementVisualCatalogDto Create()
        {
            // 정적 속성 구성보다 먼저 쓸 수 있도록 이 배열은 지역 값으로 둔다.
            string[] colors = { "pink", "yellow", "blue", "green", "purple" };
            List<ElementVisualDefinitionDto> definitions = new List<ElementVisualDefinitionDto>();
            List<ElementVisualBindingDto> bindings = new List<ElementVisualBindingDto>();
            void Register(string key, IEnumerable<ElementVisualFrameDto> states, params string[] ids)
            {
                ElementVisualFrameDto[] frames = states.ToArray();
                // 구형 효과 선택도 시각 등록 경계에서 주소 참조로 확정한다.
                foreach (ElementVisualFrameDto frame in frames)
                {
                    frame.effects = Effects(key, frame.color, colors);
                    frame.effectAnimations = EffectAnimations(key, frame.effects);
                }
                definitions.Add(new ElementVisualDefinitionDto { key = key, states = frames });
                foreach (string id in ids) bindings.Add(new ElementVisualBindingDto { id = id, visualKey = key });
            }
            Register("legacy.normal", colors.Select((color, index) => new ElementVisualFrameDto
            { color = index, path = "Blocks/rabbit-" + color + "-v1-256" }), "supply.normal.random", "supply.normal.fixed");
            List<ElementVisualFrameDto> rockets = new List<ElementVisualFrameDto>();
            foreach (int direction in new[] { 0, 1 })
            {
                string shape = direction == 0 ? "horizontal" : "vertical";
                for (int frame = 0; frame < 4; frame++)
                    rockets.Add(new ElementVisualFrameDto
                    {
                        direction = direction, frame = frame,
                        path = "PowerBlocks/cleaning-rocket-" + shape + (frame == 0 ? "-v1" : "-launch-frame-" + (frame + 1) + "-v1-256"),
                        size = direction == 0 ? 1.12f : 1,
                        offsetX = direction == 0 ? -28.5f / 256 : 0,
                        offsetY = direction == 0 ? (frame < 2 ? 28f : -23f) / 256 : 0
                    });
            }
            Register("legacy.rocket", rockets, "power.rocket");
            Register("legacy.bomb", new[] { new ElementVisualFrameDto { path = "PowerBlocks/moon-bomb-v1" } }, "power.bomb");
            Register("legacy.magnet", new[] { new ElementVisualFrameDto { path = "PowerBlocks/rainbow-magnet-v1" } }, "power.magnet");
            List<ElementVisualFrameDto> drones = new List<ElementVisualFrameDto>
            { new ElementVisualFrameDto { path = "PowerBlocks/collection-drone-v1" } };
            for (int frame = 1; frame <= 4; frame++) drones.Add(new ElementVisualFrameDto
            { frame = frame, path = "PowerBlocks/collection-drone-rotor-4frames-v1", sheetColumns = 2, sheetRows = 2, sheetFrame = frame - 1 });
            Register("legacy.drone", drones, "power.drone");
            Register("legacy.recovery", new[] { new ElementVisualFrameDto { path = "BoardDevices/Recovery/recovery-part-v1-256" } }, "supply.recovery");
            Register("legacy.crate", Durability("Obstacles/Crate/crate", 6, .96f), "obstacle.crate.wood");
            Register("legacy.scrap", Durability("Obstacles/Scrap/scrap", 5, .96f), "obstacle.scrap", "supply.scrap");
            Register("legacy.capsule", Durability("Obstacles/RecoveryCapsule/recovery-capsule", 5, .96f), "obstacle.recovery-capsule");
            Register("legacy.color-lock", colors.SelectMany((color, index) => Durability("Obstacles/ColorLock/color-lock-" + color, 3, .96f, index)), "obstacle.color-lock");
            Register("legacy.rod-box", colors.SelectMany((color, index) => Durability("Obstacles/MetalRodBox/metal-rod-box-" + color, 9, 2.16f, index)), "obstacle.metal-rod-box");
            List<ElementVisualFrameDto> charges = new List<ElementVisualFrameDto>();
            for (int required = 3; required <= 5; required++)
                for (int charge = 0; charge <= required; charge++) charges.Add(new ElementVisualFrameDto
                { charge = charge, requiredCharge = required, path = "Obstacles/Generator/generator-charge-" + charge + "-of-" + required + "-v1-512", size = 2.16f });
            Register("legacy.generator", charges, "obstacle.generator");
            Register("legacy.web", Enumerable.Range(1, 3).Select(value => new ElementVisualFrameDto
            { durability = value, path = "Obstacles/Web/web-durability-" + value + "-v2-256", order = 20 }), "cover.web");
            Register("legacy.mold", new[] { new ElementVisualFrameDto { path = "Obstacles/Mold/mold-base-v1-256", order = 20 } }, "cover.mold");
            Register("legacy.dust", Durability("Obstacles/Dust/dust", 3, 1, order: 2), "floor.dust");
            return new ElementVisualCatalogDto { definitions = definitions.ToArray(), bindings = bindings.ToArray() };
        }
        private static IEnumerable<ElementVisualFrameDto> Durability(string prefix, int maximum, float size, int color = -1, int order = 10)
            => Enumerable.Range(1, maximum).Select(value => new ElementVisualFrameDto
            { durability = value, color = color, path = prefix + "-durability-" + value + "-v1-256", size = size, order = order });

        private static ElementVisualEffectDto[] EffectAnimations(string key, string[] paths)
        {
            string[] events = key switch
            {
                "legacy.normal" => new[] { "match", "creation" },
                "legacy.rocket" => new[] { "trail", "impact" },
                "legacy.bomb" => new[] { "blast" },
                "legacy.drone" => new[] { "impact" },
                "legacy.magnet" => new[] { "pull", "transform" },
                "legacy.generator" => new[] { "charge", "damage" },
                "legacy.web" or "legacy.mold" or "legacy.dust" => new[] { "clear" },
                "legacy.crate" or "legacy.scrap" or "legacy.capsule" or "legacy.color-lock" or "legacy.rod-box" => new[] { "damage" },
                _ => Array.Empty<string>()
            };
            int count = events.Length == 0 ? 0 : paths.Length / events.Length;
            return events.Select((name, index) => new ElementVisualEffectDto { key = name,
                frames = paths.Skip(index * count).Take(count).Select(path => new ElementVisualFrameDto { path = path, order = 40 }).ToArray() }).ToArray();
        }
        private static string[] Effects(string key, int color, string[] colors)
        {
            IEnumerable<string> Animation(string category, string name, int count = 4) => Enumerable.Range(1, count)
                .Select(frame => "Effects/" + category + "/Animations/" + name + "-frame-" + frame.ToString("00") + "-v1-256");
            return (key switch
            {
                "legacy.normal" => Animation("Match", "match-" + colors[color]).Concat(Animation("PowerCreation", "power-creation")),
                "legacy.rocket" => Animation("Rocket", "rocket-trail").Concat(Animation("Rocket", "rocket-impact")),
                "legacy.bomb" => Animation("BombExplosion", "bomb-explosion", 8),
                "legacy.drone" => Animation("Drone", "drone-impact"),
                "legacy.magnet" => Animation("Magnet", "magnet-pull").Concat(Animation("Magnet", "magnet-transform")),
                "legacy.crate" => Animation("WoodBreak", "wood-break"),
                "legacy.scrap" or "legacy.capsule" or "legacy.color-lock" or "legacy.rod-box" => Animation("MetalBreak", "metal-break"),
                "legacy.generator" => Enumerable.Range(1, 4).Select(frame => "Effects/GeneratorCharge/charge-pulse-" + frame.ToString("00") + "-v1-256").Concat(Animation("MetalBreak", "metal-break")),
                "legacy.web" => Animation("WebBreak", "web-break"),
                "legacy.mold" => Animation("MoldClear", "mold-clear"),
                "legacy.dust" => Animation("DustClear", "dust-clear"),
                _ => Enumerable.Empty<string>()
            }).ToArray();
        }
    }
}
