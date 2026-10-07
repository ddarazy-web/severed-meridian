using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;

namespace Elements
{
    /// <summary>복사된 상태 선택자와 표시 값. Sprite/제작 에셋/수정 가능한 배열을 보유하지 않는다.</summary>
    public sealed class ElementVisualFrame
    {
        private readonly int[] selectors;
        public string Path { get; }
        public Vector2 Pivot { get; }
        public float Size { get; }
        public Vector2 Offset { get; }
        public float Angle { get; }
        public int Order { get; }
        public int SheetColumns { get; }
        public int SheetRows { get; }
        public int SheetFrame { get; }
        public IReadOnlyList<string> Effects { get; }
        public IReadOnlyDictionary<string, ReadOnlyCollection<ElementVisualFrame>> EffectAnimations { get; }

        internal ElementVisualFrame(ElementVisualFrameDto dto, string key)
        {
            if (dto == null) throw Error(key, "빈 상태");
            selectors = new[] { dto.color, dto.durability, dto.charge, dto.requiredCharge, dto.direction, dto.frame, dto.logicalSize };
            if (selectors.Any(value => value < -1) || dto.frame < 0 || dto.logicalSize == 0)
                throw Error(key, "유효하지 않은 상태 선택자");
            ValidatePath(dto.path, key);
            if (!Finite(dto.size) || dto.size <= 0 || !Finite(dto.pivotX) || !Finite(dto.pivotY) ||
                dto.pivotX < 0 || dto.pivotX > 1 || dto.pivotY < 0 || dto.pivotY > 1 ||
                !Finite(dto.offsetX) || !Finite(dto.offsetY) || !Finite(dto.angle)) throw Error(key, "유효하지 않은 표시 수치");
            if (dto.sheetColumns < 1 || dto.sheetRows < 1 || dto.sheetFrame < 0 ||
                dto.sheetFrame >= (long)dto.sheetColumns * dto.sheetRows) throw Error(key, "시트 프레임 범위 초과");
            string[] effects = (dto.effects ?? Array.Empty<string>()).ToArray();
            foreach (string path in effects) ValidatePath(path, key);
            Path = dto.path; Pivot = new Vector2(dto.pivotX, dto.pivotY); Size = dto.size;
            Offset = new Vector2(dto.offsetX, dto.offsetY); Angle = dto.angle; Order = dto.order;
            SheetColumns = dto.sheetColumns; SheetRows = dto.sheetRows; SheetFrame = dto.sheetFrame;
            Dictionary<string, ReadOnlyCollection<ElementVisualFrame>> animations = new Dictionary<string, ReadOnlyCollection<ElementVisualFrame>>(StringComparer.Ordinal);
            foreach (ElementVisualEffectDto animation in dto.effectAnimations ?? Array.Empty<ElementVisualEffectDto>())
            {
                if (animation == null || string.IsNullOrWhiteSpace(animation.key) || animation.frames == null || animation.frames.Length == 0)
                    throw Error(key, "효과 키 또는 프레임 누락");
                if (animation.frames.Any(frame => frame == null || frame.effectAnimations?.Length > 0 || frame.effects?.Length > 0))
                    throw Error(key, "효과 프레임은 추가 효과를 생성하지 않습니다.");
                ReadOnlyCollection<ElementVisualFrame> frames = Array.AsReadOnly(animation.frames.Select(frame => new ElementVisualFrame(frame, key)).ToArray());
                if (!animations.TryAdd(animation.key, frames)) throw Error(key, "중복 효과 키 '" + animation.key + "'");
            }
            EffectAnimations = new ReadOnlyDictionary<string, ReadOnlyCollection<ElementVisualFrame>>(animations);
            Effects = Array.AsReadOnly(effects.Distinct(StringComparer.Ordinal).ToArray());
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        internal static ArgumentException Error(string key, string message) => new ArgumentException($"시각 정의 '{key}': {message}");
        internal static void ValidatePath(string path, string key)
        {
            if (string.IsNullOrWhiteSpace(path) || path.Contains("\\") || path.Contains("..") || path.StartsWith("/") ||
                path.EndsWith("/") || path.Split('/').Length < 2) throw Error(key, "잘못된 자원 키 '" + path + "'");
        }
        internal bool Matches(ElementVisualState state)
            => Match(0, state.Color) && Match(1, state.Durability) && Match(2, state.Charge) &&
                Match(3, state.RequiredCharge) && Match(4, state.Direction) && Match(5, state.Frame) && Match(6, state.LogicalSize);
        private bool Match(int axis, int value) => selectors[axis] == -1 || selectors[axis] == value;
        internal bool Overlaps(ElementVisualFrame other)
        {
            for (int i = 0; i < selectors.Length; i++)
                if (selectors[i] != -1 && other.selectors[i] != -1 && selectors[i] != other.selectors[i]) return false;
            return true;
        }
        internal ElementVisualFrameDto ToDto() => new ElementVisualFrameDto
        {
            color = selectors[0], durability = selectors[1], charge = selectors[2], requiredCharge = selectors[3],
            direction = selectors[4], frame = selectors[5], logicalSize = selectors[6], path = Path,
            pivotX = Pivot.x, pivotY = Pivot.y, size = Size, offsetX = Offset.x, offsetY = Offset.y,
            angle = Angle, order = Order, sheetColumns = SheetColumns, sheetRows = SheetRows, sheetFrame = SheetFrame,
            effects = Effects.ToArray(), effectAnimations = EffectAnimations.Select(pair => new ElementVisualEffectDto
            { key = pair.Key, frames = pair.Value.Select(frame => frame.ToDto()).ToArray() }).ToArray()
        };
    }

    public sealed class ElementVisualDefinition
    {
        public string Key { get; }
        public ReadOnlyCollection<ElementVisualFrame> States { get; }
        public ReadOnlyCollection<ElementId> Generates { get; }
        internal ElementVisualDefinition(ElementVisualDefinitionDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.key)) throw ElementVisualFrame.Error("<null>", "키 누락");
            Key = dto.key;
            ElementVisualFrame[] states = (dto.states ?? Array.Empty<ElementVisualFrameDto>())
                .Select(value => new ElementVisualFrame(value, Key)).ToArray();
            if (states.Length == 0) throw ElementVisualFrame.Error(Key, "상태 누락");
            for (int i = 0; i < states.Length; i++)
                for (int j = 0; j < i; j++)
                    if (states[i].Overlaps(states[j])) throw ElementVisualFrame.Error(Key, $"중복/겹치는 상태 {j}/{i}");
            States = Array.AsReadOnly(states);
            Generates = Array.AsReadOnly((dto.generates ?? Array.Empty<string>()).Select(value => new ElementId(value)).ToArray());
        }
        internal ElementVisualFrame Resolve(ElementId id, ElementVisualState state)
        {
            foreach (ElementVisualFrame frame in States) if (frame.Matches(state)) return frame;
            throw ElementVisualFrame.Error(id.Value, "등록되지 않은 표시 상태 (" + state + ")");
        }
        internal ElementVisualDefinitionDto ToDto() => new ElementVisualDefinitionDto
        { key = Key, states = States.Select(value => value.ToDto()).ToArray(), generates = Generates.Select(value => value.Value).ToArray() };
    }
}
