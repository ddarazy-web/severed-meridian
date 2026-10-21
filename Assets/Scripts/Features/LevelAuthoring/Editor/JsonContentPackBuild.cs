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
using UnityEditor;
using UnityEngine.TextCore.LowLevel;

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
            LevelLobbyEntry[] lobbyEntries = snapshot.DocumentIds.Select(snapshot.GetDocumentInfo)
                .Where(info => info.Kind == "level").Select(info => new LevelLobbyEntry
                {
                    LevelId = info.Id,
                    LevelNumber = info.LevelNumber,
                    DisplayName = info.DisplayName
                }).ToArray();
            Font lobbyFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Lobby/MoonRabbitLobby-Regular.ttf");
            if (lobbyFont == null) throw new ContentFormatException("로비 폰트가 없습니다. 폰트 리소스를 확인해 주세요.");
            FontEngine.InitializeFontEngine();
            if (FontEngine.LoadFontFace(lobbyFont, 16) != FontEngineError.Success)
                throw new ContentFormatException("로비 폰트의 글리프를 검사할 수 없습니다.");
            var checkedCharacters = new HashSet<char>();
            try
            {
                foreach (LevelLobbyEntry entry in lobbyEntries)
                {
                    foreach (char character in entry.DisplayName ?? string.Empty)
                    {
                        if (!char.IsWhiteSpace(character) && checkedCharacters.Add(character) && !FontEngine.TryGetGlyphIndex(character, out _))
                            throw new ContentFormatException($"레벨 {entry.LevelNumber} 이름에 로비 폰트가 지원하지 않는 문자 U+{(int)character:X4}가 있습니다. 폰트 범위를 갱신하거나 이름을 수정해 주세요.");
                    }
                }
            }
            finally { FontEngine.UnloadFontFace(); }
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
            // 로비는 게임 본문 대신 같은 게시 세대의 작은 목록만 읽는다.
            outputs.Add(LevelLobbyIndexCodec.OutputPath, LevelLobbyIndexCodec.Encode(lobbyEntries));
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

