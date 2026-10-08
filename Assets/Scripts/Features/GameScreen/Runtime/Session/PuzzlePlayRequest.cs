using System;
using Elements;
using Levels;
using MemoryPack;

namespace GameScreen
{
    /// <summary>제작 환경과 무관한 실행 스냅샷. 원본 에셋과 반환 사본의 소유권을 공유하지 않는다.</summary>
    public sealed class PuzzlePlayRequest
    {
        private readonly byte[] levelBytes;
        private readonly byte[] visualBytes;
        public int LevelNumber { get; }
        public int Seed { get; }

        public PuzzlePlayRequest(byte[] snapshot, int levelNumber, int seed, ElementVisualCatalogDto visuals)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (snapshot.Length == 0) throw new ArgumentException("빈 실행 스냅샷입니다.", nameof(snapshot));
            if (levelNumber < 1) throw new ArgumentOutOfRangeException(nameof(levelNumber));
            levelBytes = (byte[])snapshot.Clone();
            visualBytes = visuals == null ? null : MemoryPackSerializer.Serialize(visuals);
            LevelNumber = levelNumber; Seed = seed;
        }

        public static PuzzlePlayRequest Capture(LevelDefinition source, int seed, ElementVisualCatalogDto visuals = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return new PuzzlePlayRequest(LevelPackCodec.Snapshot(source), source.LevelNumber, seed,
                visuals ?? ElementVisualLookup.ForLevel(source).ToDto());
        }

        // 반환한 레벨은 실행 세션이 인수하거나 호출자가 해제한다.
        public LevelDefinition CreateDefinition() => LevelPackCodec.ReadLevel(levelBytes, LevelNumber);
        public ElementVisualCatalogDto CreateVisuals() => visualBytes == null ? null :
            MemoryPackSerializer.Deserialize<ElementVisualCatalogDto>(visualBytes);
    }
}
