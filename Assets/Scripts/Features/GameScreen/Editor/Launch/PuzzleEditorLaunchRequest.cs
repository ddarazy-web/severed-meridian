using System;
using System.IO;
using Levels;
using Levels.Editor;
using UnityEngine;
using Elements;
using MemoryPack;

namespace GameScreen.Editor
{
    public enum PuzzleEditorLevelSource { Asset, MemoryPack }

    public sealed class PuzzleEditorLaunchRequest
    {
        private readonly byte[] bytes;
        private readonly byte[] visualBytes;
        public int LevelNumber { get; }
        public int Seed { get; }
        public PuzzleEditorLevelSource Source { get; }
        public Tutorial.TutorialRunMode TutorialMode { get; private set; }
        internal string EncodedBytes => Convert.ToBase64String(bytes);
        internal string EncodedVisuals => Convert.ToBase64String(visualBytes);

        private PuzzleEditorLaunchRequest(byte[] snapshot, int number, int seed, PuzzleEditorLevelSource source, ElementVisualCatalog visuals)
        { bytes = snapshot; LevelNumber = number; Seed = seed; Source = source; visualBytes = MemoryPackSerializer.Serialize(visuals.ToDto()); }

        public static PuzzleEditorLaunchRequest Capture(LevelDefinition source, PuzzleEditorLevelSource mode, int seed, Tutorial.TutorialRunMode tutorialMode = Tutorial.TutorialRunMode.Automatic)
        {
            if (source == null) throw new InvalidOperationException("실행할 레벨을 선택하세요.");
            if (source.LevelNumber < 1) throw new InvalidOperationException("레벨 번호는 1 이상이어야 합니다.");
            byte[] snapshot;
            if (mode == PuzzleEditorLevelSource.Asset) snapshot = LevelPackCodec.Snapshot(source);
            else if (mode == PuzzleEditorLevelSource.MemoryPack)
            {
                string path = LevelPackBuild.FilePath(source.LevelNumber);
                if (!File.Exists(path)) throw new FileNotFoundException("생성된 MemoryPack이 없습니다. 플레이 테스트에서 MemoryPack 갱신을 실행하세요.", path);
                snapshot = File.ReadAllBytes(path);
            }
            else throw new ArgumentOutOfRangeException(nameof(mode));
            LevelDefinition validation = mode == PuzzleEditorLevelSource.MemoryPack
                ? LevelPackCodec.ReadLevel(snapshot, source.LevelNumber, ElementContentPackCodec.Decode(File.ReadAllBytes(Elements.Editor.ElementContentPackBuild.OutputPath)))
                : LevelPackCodec.ReadLevel(snapshot, source.LevelNumber);
            try
            {
                return new PuzzleEditorLaunchRequest(snapshot, source.LevelNumber, seed, mode,
                    ElementVisualLookup.ForLevel(mode == PuzzleEditorLevelSource.MemoryPack ? validation : source)) { TutorialMode = tutorialMode };
            }
            finally { UnityEngine.Object.DestroyImmediate(validation); }
        }

        public LevelDefinition CreateDefinition() => LevelPackCodec.ReadLevel(bytes, LevelNumber);
        // 효과 프레임 DTO는 재귀 타입이므로 Unity JSON 직렬화 대신 기존 MemoryPack 계약을 사용한다.
        internal static ElementVisualCatalogDto DecodeVisuals(string encoded)
            => MemoryPackSerializer.Deserialize<ElementVisualCatalogDto>(Convert.FromBase64String(encoded));
        public ElementVisualCatalog CreateVisualCatalog()
            => ElementVisualCatalog.FromDto(MemoryPackSerializer.Deserialize<ElementVisualCatalogDto>(visualBytes));
    }
}
