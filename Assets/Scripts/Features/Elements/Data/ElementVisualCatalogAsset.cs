using UnityEngine;
using System.Linq;

namespace Elements
{
    /// <summary>제작 입력만 보유한다. 게임 진입에는 검증된 DTO/불변 카탈로그를 전달한다.</summary>
    public sealed class ElementVisualCatalogAsset : ScriptableObject
    {
        [SerializeField] private VisualAuthoringCatalog catalog = new VisualAuthoringCatalog();
        [SerializeField] private string planningDocument;
        public string PlanningDocument => planningDocument;
        public ElementVisualCatalog CreateCatalog() => LegacyElementVisuals.WithOverrides(catalog.ToDto());
        public ElementVisualCatalogDto ToDto() => CreateCatalog().ToDto();

        // 실행 DTO의 재귀 타입을 Unity 원본에 직접 저장하지 않는다. 효과 프레임은
        // 효과를 다시 포함하지 못하는 기존 검증 계약과 같은 유한한 제작 구조다.
        [System.Serializable] private sealed class VisualAuthoringCatalog
        {
            public VisualAuthoringDefinition[] definitions = System.Array.Empty<VisualAuthoringDefinition>();
            public ElementVisualBindingDto[] bindings = System.Array.Empty<ElementVisualBindingDto>();
            public ElementVisualCatalogDto ToDto() => new ElementVisualCatalogDto
            {
                bindings = bindings,
                definitions = definitions?.Select(value => value == null ? null : new ElementVisualDefinitionDto
                { key = value.key, generates = value.generates, states = value.states?.Select(frame => frame?.ToDto()).ToArray() }).ToArray()
            };
        }
        [System.Serializable] private sealed class VisualAuthoringDefinition
        {
            public string key;
            public VisualAuthoringFrame[] states = System.Array.Empty<VisualAuthoringFrame>();
            public string[] generates = System.Array.Empty<string>();
        }
        [System.Serializable] private class VisualAuthoringScalarFrame
        {
            public int color = -1, durability = -1, charge = -1, requiredCharge = -1, direction = -1;
            public int frame;
            public int logicalSize = -1;
            public string path;
            public float pivotX = .5f, pivotY = .5f, size = 1, offsetX, offsetY, angle;
            public int order = 10;
            public int sheetColumns = 1, sheetRows = 1, sheetFrame;
            public virtual ElementVisualFrameDto ToDto() => new ElementVisualFrameDto
            {
                color = color, durability = durability, charge = charge, requiredCharge = requiredCharge,
                direction = direction, frame = frame, logicalSize = logicalSize, path = path,
                pivotX = pivotX, pivotY = pivotY, size = size, offsetX = offsetX, offsetY = offsetY,
                angle = angle, order = order, sheetColumns = sheetColumns, sheetRows = sheetRows, sheetFrame = sheetFrame
            };
        }
        [System.Serializable] private sealed class VisualAuthoringFrame : VisualAuthoringScalarFrame
        {
            public string[] effects = System.Array.Empty<string>();
            public VisualAuthoringEffect[] effectAnimations = System.Array.Empty<VisualAuthoringEffect>();
            public override ElementVisualFrameDto ToDto()
            {
                ElementVisualFrameDto value = base.ToDto();
                value.effects = effects;
                value.effectAnimations = effectAnimations?.Select(effect => effect == null ? null : new ElementVisualEffectDto
                { key = effect.key, frames = effect.frames?.Select(frame => frame?.ToDto()).ToArray() }).ToArray();
                return value;
            }
        }
        [System.Serializable] private sealed class VisualAuthoringEffect
        {
            public string key;
            public VisualAuthoringScalarFrame[] frames = System.Array.Empty<VisualAuthoringScalarFrame>();
        }
    }
}
