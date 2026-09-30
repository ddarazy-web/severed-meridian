using System;
using System.Linq;
using System.Collections.Generic;
using MemoryPack;

namespace Levels
{
    [MemoryPackable]
    public partial class LevelPack
    {
        public int FormatVersion { get; set; }
        public int FirstLevel { get; set; }
        public PackedLevel[] Levels { get; set; }
    }

    public static class LevelPackCodec
    {
        public const int FormatVersion = 1;
        public const int LevelsPerPack = 50;

        public static int FirstLevel(int number)
        {
            if (number < 1) throw new ArgumentOutOfRangeException(nameof(number));
            return ((number - 1) / LevelsPerPack) * LevelsPerPack + 1;
        }

        public static string Address(int number) => $"Levels/levels-{FirstLevel(number):D6}";

        public static byte[] Encode(IEnumerable<LevelDefinition> levels)
        {
            PackedLevel[] data = levels.OrderBy(level => level.LevelNumber).Select(level => level.ToPacked()).ToArray();
            if (data.Length == 0) throw new ArgumentException("빈 레벨 팩입니다.");
            LevelPack pack = new LevelPack { FormatVersion = FormatVersion, FirstLevel = FirstLevel(data[0].LevelNumber), Levels = data };
            Validate(pack);
            return MemoryPackSerializer.Serialize(pack);
        }

        public static LevelPack Decode(byte[] bytes)
        {
            LevelPack pack = MemoryPackSerializer.Deserialize<LevelPack>(bytes);
            Validate(pack);
            return pack;
        }

        public static LevelDefinition ReadLevel(byte[] bytes, int number)
        {
            LevelPack pack = Decode(bytes);
            if (pack.FirstLevel != FirstLevel(number)) throw new InvalidOperationException("요청한 레벨 구간과 파일이 다릅니다.");
            PackedLevel data = pack.Levels.SingleOrDefault(level => level.LevelNumber == number);
            if (data == null) throw new KeyNotFoundException($"MemoryPack에 레벨 {number}이 없습니다.");
            return LevelDefinition.FromPacked(data);
        }

        private static void Validate(LevelPack pack)
        {
            if (pack == null || pack.FormatVersion != FormatVersion) throw new InvalidOperationException("지원하지 않는 레벨 팩 버전입니다.");
            if (pack.Levels == null || pack.Levels.Length == 0 || pack.Levels.Length > LevelsPerPack)
                throw new InvalidOperationException("레벨 팩 수량 오류입니다.");
            HashSet<int> numbers = new HashSet<int>();
            foreach (PackedLevel level in pack.Levels)
                if (level == null || level.SchemaVersion != LevelDefinition.CurrentSchemaVersion ||
                    FirstLevel(level.LevelNumber) != pack.FirstLevel || !numbers.Add(level.LevelNumber))
                    throw new InvalidOperationException("레벨 팩의 스키마·번호·중복 오류입니다.");
        }
    }
}
