using System;
using System.IO;
using Levels;
using Levels.Editor;
using UnityEngine;

namespace GameScreen.Editor
{
    public enum PuzzleEditorLevelSource { Asset, MemoryPack }

    public sealed class PuzzleEditorLaunchRequest
    {
        private readonly byte[] bytes;
        public int LevelNumber { get; }
        public int Seed { get; }
        public PuzzleEditorLevelSource Source { get; }
        internal string EncodedBytes => Convert.ToBase64String(bytes);

        private PuzzleEditorLaunchRequest(byte[] snapshot, int number, int seed, PuzzleEditorLevelSource source)
        { bytes = snapshot; LevelNumber = number; Seed = seed; Source = source; }

        public static PuzzleEditorLaunchRequest Capture(LevelDefinition source, PuzzleEditorLevelSource mode, int seed)
        {
            if (source == null) throw new InvalidOperationException("실행할 레벨을 선택하세요.");
            if (source.LevelNumber < 1) throw new InvalidOperationException("레벨 번호는 1 이상이어야 합니다.");
            byte[] snapshot;
            if (mode == PuzzleEditorLevelSource.Asset) snapshot = LevelPackCodec.Encode(new[] { source });
            else if (mode == PuzzleEditorLevelSource.MemoryPack)
            {
                string path = LevelPackBuild.FilePath(source.LevelNumber);
                if (!File.Exists(path)) throw new FileNotFoundException("생성된 MemoryPack이 없습니다. 플레이 테스트에서 MemoryPack 갱신을 실행하세요.", path);
                snapshot = File.ReadAllBytes(path);
            }
            else throw new ArgumentOutOfRangeException(nameof(mode));
            LevelDefinition validation = LevelPackCodec.ReadLevel(snapshot, source.LevelNumber);
            UnityEngine.Object.DestroyImmediate(validation);
            return new PuzzleEditorLaunchRequest(snapshot, source.LevelNumber, seed, mode);
        }

        public LevelDefinition CreateDefinition() => LevelPackCodec.ReadLevel(bytes, LevelNumber);
    }
}
