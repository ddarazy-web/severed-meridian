using System;
using System.Collections.Generic;
using System.Linq;
using MemoryPack;

namespace Levels
{
    [MemoryPackable]
    public partial class LevelLobbyEntry
    {
        public string LevelId { get; set; }
        public int LevelNumber { get; set; }
        public string DisplayName { get; set; }
    }

    [MemoryPackable]
    public partial class LevelLobbyIndex
    {
        public int FormatVersion { get; set; } = 1;
        public LevelLobbyEntry[] Entries { get; set; }
    }

    public static class LevelLobbyIndexCodec
    {
        public const string Address = "Levels/lobby-index";
        public const string OutputPath = "Assets/Data/LevelPacks/lobby-index.bytes";

        public static byte[] Encode(IEnumerable<LevelLobbyEntry> entries)
        {
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            LevelLobbyIndex value = new LevelLobbyIndex { Entries = entries.ToArray() };
            Validate(value, false);
            value.Entries = value.Entries.OrderBy(entry => entry.LevelNumber).ToArray();
            return MemoryPackSerializer.Serialize(value);
        }

        public static LevelLobbyIndex Decode(byte[] bytes)
        {
            LevelLobbyIndex value = MemoryPackSerializer.Deserialize<LevelLobbyIndex>(bytes);
            Validate(value, true);
            return value;
        }

        private static void Validate(LevelLobbyIndex value, bool requireOrder)
        {
            if (value == null || value.FormatVersion != 1 || value.Entries == null)
                throw new ArgumentException("레벨 목록 버전 또는 항목이 올바르지 않습니다.");
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            HashSet<int> numbers = new HashSet<int>();
            int previous = 0;
            foreach (LevelLobbyEntry entry in value.Entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.LevelId) || entry.LevelNumber < 1 ||
                    entry.DisplayName == null || !ids.Add(entry.LevelId) || !numbers.Add(entry.LevelNumber) ||
                    requireOrder && entry.LevelNumber <= previous)
                    throw new ArgumentException("레벨 목록의 ID·번호·이름·정렬이 올바르지 않습니다.");
                previous = entry.LevelNumber;
            }
        }
    }
}
