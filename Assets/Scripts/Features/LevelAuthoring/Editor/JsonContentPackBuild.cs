using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Elements;
using Elements.Editor;
using LevelAuthoring.Documents;
using LevelAuthoring.Runtime;
using Levels;
using Levels.Editor;
using UnityEngine;

namespace LevelAuthoring.Editor
{
    // 한 번 고정한 제작 스냅샷만 변환한다. 파일/Addressables 교체는 이 경계 밖에서 수행한다.
    public static class JsonContentPackBuild
    {
        public const string GenerationPath = "Assets/Data/LevelPacks/content-generation.bytes";
        public static string SourceHash(ContentSnapshot snapshot) => ContentSnapshotFingerprint.Compute(snapshot);

        public static Dictionary<string, byte[]> CreateBytes(ContentSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            using var graph = new AuthoringObjectGraph(snapshot.Documents);
            LevelDefinition[] levels = snapshot.Documents.Where(document => document.Kind == "level")
                .Select(document => (LevelDefinition)graph.Resolve(document.Id)).OrderBy(level => level.LevelNumber).ToArray();
            if (levels.Length == 0) throw new ContentFormatException("변환할 JSON 레벨이 없습니다.");
            ElementCatalogAsset[] catalogs = snapshot.Documents.Where(document => document.Kind == "catalog")
                .Select(document => (ElementCatalogAsset)graph.Resolve(document.Id)).ToArray();
            byte[] elementBytes = ElementContentPackBuild.CreateBytes(catalogs);
            ElementContentData content = ElementContentPackCodec.Decode(elementBytes);
            Dictionary<string, byte[]> outputs = LevelPackBuild.CreatePackBytes(levels);
            // 출시 로더와 같은 바이트 해석을 모두 통과해야 교체할 수 있다.
            foreach (LevelDefinition source in levels)
            {
                LevelDefinition loaded = LevelPackCodec.ReadLevel(outputs[LevelPackBuild.FilePath(source.LevelNumber)], source.LevelNumber, content);
                UnityEngine.Object.DestroyImmediate(loaded);
            }
            outputs.Add(ElementContentPackBuild.OutputPath, elementBytes);
            var addressed = outputs.ToDictionary(pair => pair.Key == ElementContentPackBuild.OutputPath ? ElementContentPackCodec.Address :
                "Levels/" + Path.GetFileNameWithoutExtension(pair.Key), pair => pair.Value, StringComparer.Ordinal);
            byte[] generationBytes = ContentPackGenerationCodec.Encode(SourceHash(snapshot), addressed);
            ContentPackGeneration generation = ContentPackGenerationCodec.Decode(generationBytes);
            foreach (var pair in addressed) ContentPackGenerationCodec.Verify(generation, pair.Key, pair.Value);
            outputs.Add(GenerationPath, generationBytes);
            return outputs;
        }
    }
}

