using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using MemoryPack;

namespace Levels
{
    [MemoryPackable]
    public partial class ContentPackGeneration
    {
        public int Version { get; set; } = 1;
        public string SourceHash { get; set; }
        public string[] Addresses { get; set; }
        public string[] Hashes { get; set; }
    }

    // 기존 레벨/요소 포맷은 유지하고 동일한 제작 세대의 바이트인지 별도로 검증한다.
    public static class ContentPackGenerationCodec
    {
        public const string Address = "Levels/content-generation";
        public static string Hash(byte[] bytes)
        {
            using var algorithm = SHA256.Create();
            return BitConverter.ToString(algorithm.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
        public static byte[] Encode(string sourceHash, IReadOnlyDictionary<string, byte[]> packs)
        {
            string[] addresses = packs.Keys.OrderBy(value => value, StringComparer.Ordinal).ToArray();
            var value = new ContentPackGeneration { SourceHash = sourceHash, Addresses = addresses, Hashes = addresses.Select(address => Hash(packs[address])).ToArray() };
            Validate(value);
            return MemoryPackSerializer.Serialize(value);
        }
        public static ContentPackGeneration Decode(byte[] bytes)
        {
            var value = MemoryPackSerializer.Deserialize<ContentPackGeneration>(bytes); Validate(value); return value;
        }
        public static void Verify(ContentPackGeneration generation, string address, byte[] bytes)
        {
            Validate(generation);
            int index = Array.IndexOf(generation.Addresses, address);
            if (index < 0 || generation.Hashes[index] != Hash(bytes))
                throw new InvalidOperationException("콘텐츠 팩의 제작 세대가 다르거나 손상됐습니다: " + address);
        }
        private static void Validate(ContentPackGeneration value)
        {
            if (value == null || value.Version != 1 || string.IsNullOrEmpty(value.SourceHash) || value.SourceHash.Length != 64 ||
                value.Addresses == null || value.Hashes == null || value.Addresses.Length == 0 || value.Addresses.Length != value.Hashes.Length ||
                value.Addresses.Any(string.IsNullOrWhiteSpace) || value.Addresses.Distinct(StringComparer.Ordinal).Count() != value.Addresses.Length ||
                value.Hashes.Any(hash => hash == null || hash.Length != 64))
                throw new InvalidOperationException("콘텐츠 세대 정보 형식이 잘못됐습니다.");
        }
    }
}
