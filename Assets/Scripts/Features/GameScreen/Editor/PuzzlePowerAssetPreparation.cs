using System;
using System.Linq;
using Board;
using Levels.Editor;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;

namespace GameScreen.Editor
{
    // 지정된 시트와 파워 아틀라스만 작성한다. 번들/플레이어 빌드나 전역 SaveAssets를 실행하지 않는다.
    public static class PuzzlePowerAssetPreparation
    {
        public static void Prepare()
        {
            const string sheet = "Assets/Textures/PowerBlocks/collection-drone-rotor-4frames-v1.png";
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(sheet);
            if (importer == null) throw new InvalidOperationException("드론 원본 시트 없음");
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.maxTextureSize = 512; importer.mipmapEnabled = false; importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            string address = BoardSpriteAtlas.Prefix + "PowerBlocks";
            string path = BoardAtlasPrebuild.AtlasPath(address);
            SpriteAtlasAsset atlas = new SpriteAtlasAsset();
            atlas.Add(BoardAtlasPrebuild.SourcePaths().Where(source => BoardAtlasPrebuild.AddressForPath(source) == address)
                .Select(source => AssetDatabase.LoadAssetAtPath<Texture2D>(source)).Cast<UnityEngine.Object>().ToArray());
            SpriteAtlasAsset.Save(atlas, path); AssetDatabase.ImportAsset(path);
            SpriteAtlasImporter atlasImporter = (SpriteAtlasImporter)AssetImporter.GetAtPath(path);
            atlasImporter.includeInBuild = false;
            atlasImporter.SaveAndReimport();
            Debug.Log("Stage08: drone sheet and PowerBlocks atlas authored; no content build.");
        }
    }
}
