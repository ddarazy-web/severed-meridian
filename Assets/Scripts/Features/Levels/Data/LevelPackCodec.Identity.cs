using System;
using System.Collections.Generic;
using System.Linq;
using MemoryPack;
using Tutorial;

namespace Levels
{
    [MemoryPackable(SerializeLayout.Explicit)]
    public partial class PackedTutorialIdentity
    {
        [MemoryPackOrder(0)] public byte[] ExecutionPack;
        [MemoryPackOrder(1)] public PackedTutorialIdentityEntry[] Entries;
    }
    [MemoryPackable(SerializeLayout.Explicit)]
    public partial class PackedTutorialIdentityEntry
    {
        [MemoryPackOrder(0)] public int LevelNumber;
        [MemoryPackOrder(1)] public string CompletionId;
        [MemoryPackOrder(2)] public int[] PreviousLevelNumbers;
    }
    public static partial class LevelPackCodec
    {
        private static byte[] EncodeIdentity(byte[] execution, PackedTutorialLevelPack source)
        {
            if (!source.Tutorials.Any(entry => !string.IsNullOrEmpty(entry.Tutorial?.completionId) || entry.Tutorial?.previousLevelNumbers?.Count > 0)) return execution;
            PackedTutorialIdentity data = new PackedTutorialIdentity
            {
                ExecutionPack = execution,
                Entries = source.Tutorials.Select(entry => new PackedTutorialIdentityEntry
                { LevelNumber = entry.LevelNumber, CompletionId = entry.Tutorial?.completionId ?? "", PreviousLevelNumbers = entry.Tutorial?.previousLevelNumbers?.ToArray() ?? Array.Empty<int>() }).ToArray()
            };
            byte[] payload = MemoryPackSerializer.Serialize(data);
            byte[] bytes = new byte[payload.Length + 8]; Array.Copy(TutorialMagic, bytes, 4); bytes[4] = 6;
            Array.Copy(payload, 0, bytes, 8, payload.Length); return bytes;
        }
        private static PackedTutorialLevelPack DecodeIdentity(byte[] bytes)
        {
            PackedTutorialIdentity data = MemoryPackSerializer.Deserialize<PackedTutorialIdentity>(bytes.AsSpan(8));
            if (data?.ExecutionPack == null || data.ExecutionPack.Length < 8 || !IsTutorialPack(data.ExecutionPack) ||
                data.ExecutionPack[4] < 3 || data.ExecutionPack[4] > 5 || data.Entries == null)
                throw new InvalidOperationException("튜토리얼 완료 정보 팩의 실행 데이터가 없습니다.");
            PackedTutorialLevelPack pack = DecodeTutorial(data.ExecutionPack);
            if (data.Entries.Length != pack.Tutorials.Length) throw new InvalidOperationException("튜토리얼 완료 정보 누락입니다.");
            HashSet<int> seen = new HashSet<int>();
            foreach (PackedTutorialIdentityEntry entry in data.Entries)
            {
                if (entry == null || !seen.Add(entry.LevelNumber) || entry.PreviousLevelNumbers == null || entry.CompletionId == null ||
                    entry.PreviousLevelNumbers.Any(number => number < 1) || entry.PreviousLevelNumbers.Distinct().Count() != entry.PreviousLevelNumbers.Length ||
                    entry.PreviousLevelNumbers.Length > 0 && string.IsNullOrWhiteSpace(entry.CompletionId))
                    throw new InvalidOperationException("튜토리얼 완료 정보의 번호·ID·중복 오류입니다.");
                PackedLevelTutorial level = pack.Tutorials.SingleOrDefault(value => value.LevelNumber == entry.LevelNumber);
                if (level == null || level.Tutorial == null && (entry.CompletionId != "" || entry.PreviousLevelNumbers.Length > 0))
                    throw new InvalidOperationException("튜토리얼 완료 정보가 없는 레벨을 참조합니다.");
                if (level.Tutorial == null) continue;
                level.Tutorial.completionId = entry.CompletionId;
                level.Tutorial.previousLevelNumbers = entry.PreviousLevelNumbers.ToList();
            }
            ValidateTutorialPack(pack); return pack;
        }
    }
}
