using System;
using System.Linq;
using MemoryPack;

namespace Elements
{
    public sealed class ElementContentData
    {
        public ElementCatalog Definitions { get; }
        public ElementVisualCatalog Visuals { get; }
        internal ElementContentData(ElementContentPack pack)
        {
            if (pack == null || pack.FormatVersion != 1 || pack.Definitions == null || pack.Definitions.Length == 0)
                throw new ArgumentException("요소 데이터의 버전 또는 정의가 없습니다.");
            Definitions = new ElementCatalog(pack.Definitions.Select(value => value == null
                ? throw new ArgumentException("요소 정의가 null입니다.") : value.ToDefinition()));
            Visuals = ElementVisualCatalog.FromDto(pack.Visuals);
            foreach (ElementDefinition definition in Definitions.Definitions)
                if (definition.Supply?.Behavior != ElementSupplyBehavior.RandomPower) Visuals.Get(definition.Id);
        }
    }

    /// <summary>제작 원본 대신 정의·표현 값만 내보내는 콘텐츠 팩 계약이다.</summary>
    public static class ElementContentPackCodec
    {
        public const string Address = "Elements/default-content";
        public static byte[] Encode(ElementCatalog definitions, ElementVisualCatalog visuals)
        {
            if (definitions == null || visuals == null) throw new ArgumentNullException("정의와 표현 목록이 필요합니다.");
            var pack = new ElementContentPack
            {
                Definitions = definitions.Definitions.OrderBy(value => value.Id.Value, StringComparer.Ordinal)
                    .Select(PackedElementDefinition.FromDefinition).ToArray(),
                Visuals = visuals.ToDto()
            };
            _ = new ElementContentData(pack);
            byte[] payload = MemoryPackSerializer.Serialize(pack);
            byte[] bytes = new byte[payload.Length + 8];
            bytes[0] = 0x45; bytes[1] = 0x43; bytes[2] = 0x50; bytes[3] = 0x4b; bytes[4] = 1;
            Array.Copy(payload, 0, bytes, 8, payload.Length); return bytes;
        }
        public static ElementContentData Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length <= 8 || bytes[0] != 0x45 || bytes[1] != 0x43 || bytes[2] != 0x50 ||
                bytes[3] != 0x4b || bytes[4] != 1 || bytes[5] != 0 || bytes[6] != 0 || bytes[7] != 0)
                throw new ArgumentException("지원하지 않는 요소 콘텐츠 팩 헤더입니다.");
            return new ElementContentData(MemoryPackSerializer.Deserialize<ElementContentPack>(bytes.AsSpan(8)));
        }
    }
}
