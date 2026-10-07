using System;

namespace Elements
{
    /// <summary>규칙 팩과 별도로 전달하는 표현 값. Unity 제작 원본 참조를 포함하지 않는다.</summary>
    [Serializable]
    public sealed class ElementVisualCatalogDto
    {
        public ElementVisualDefinitionDto[] definitions = Array.Empty<ElementVisualDefinitionDto>();
        public ElementVisualBindingDto[] bindings = Array.Empty<ElementVisualBindingDto>();
    }

    [Serializable]
    public sealed class ElementVisualBindingDto
    {
        public string id;
        public string visualKey;
    }

    [Serializable]
    public sealed class ElementVisualDefinitionDto
    {
        public string key;
        public ElementVisualFrameDto[] states = Array.Empty<ElementVisualFrameDto>();
        public string[] generates = Array.Empty<string>();
    }

    [Serializable]
    public sealed class ElementVisualEffectDto
    {
        public string key;
        public ElementVisualFrameDto[] frames = Array.Empty<ElementVisualFrameDto>();
    }

    [Serializable]
    public sealed class ElementVisualFrameDto
    {
        // -1은 해당 축과 무관한 그림이다. frame은 기본 정지 프레임0만 허용한다.
        public int color = -1;
        public int durability = -1;
        public int charge = -1;
        public int requiredCharge = -1;
        public int direction = -1;
        public int frame;
        public int logicalSize = -1;
        public string path;
        public float pivotX = .5f;
        public float pivotY = .5f;
        public float size = 1;
        public float offsetX;
        public float offsetY;
        public float angle;
        public int order = 10;
        public int sheetColumns = 1;
        public int sheetRows = 1;
        public int sheetFrame;
        public string[] effects = Array.Empty<string>();
        public ElementVisualEffectDto[] effectAnimations = Array.Empty<ElementVisualEffectDto>();
    }
}
