using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Board;
using GameScreen;
using Simulation;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

namespace Levels.Editor
{
    public static class ArtworkBaselineVerification
    {
        private const string Evidence = "Logs/ElementFramework/Stage09";
        private static readonly List<string> Results = new List<string>();
        private static readonly List<string> Observations = new List<string>();
        private static readonly HashSet<string> Seen = new HashSet<string>();
        private static void Check(bool value, string name) { if (!value) throw new InvalidOperationException(name); Results.Add("PASS " + name); }
        private static void Set(object target, string name, object value) => target.GetType().GetProperty(name).GetSetMethod(true).Invoke(target, new[] { value });
        private static string Snapshot(object value) => (string)typeof(LevelInitialStateVerification).GetMethod("Snapshot", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new[] { value });

        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 Editor에서만 실행한다.");
            Directory.CreateDirectory(Evidence); Results.Clear(); Observations.Clear(); Seen.Clear(); int exit = 0;
            LevelDefinition level = ScriptableObject.CreateInstance<LevelDefinition>();
            try { Content(level); Bodies(); Devices(); Frames(); EditorNulls(); }
            catch (Exception error) { Results.Add("FAIL " + error); Debug.LogException(error); exit = 1; }
            finally
            {
                UnityEngine.Object.DestroyImmediate(level);
                File.WriteAllLines(Evidence + "/baseline-results.txt", Results);
                File.WriteAllLines(Evidence + "/artwork-observations.jsonl", Observations);
            }
            EditorApplication.Exit(exit);
        }

        private static void Content(LevelDefinition level)
        {
            LevelRuntimeState state = LevelStateBuilder.Build(level, 12345).State;
            if (state == null)
            {
                JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":0,\"color\":0,\"count\":1}]}", level);
                state = LevelStateBuilder.Build(level, 12345).State;
            }
            Check(state != null, "메모리 상태 구성"); RuntimeCell cell = state.CellAt(new BoardCoordinate(4, 4));
            foreach (RabbitColor color in Enum.GetValues(typeof(RabbitColor)))
            {
                Set(cell, "Content", RuntimeContent.Normal); Set(cell, "Color", (RabbitColor?)color);
                Asset("rabbit-" + color, PuzzleArtworkPaths.Content(cell), Snapshot(cell));
            }
            foreach (InitialBlockKind power in new[] { InitialBlockKind.Rocket, InitialBlockKind.Bomb, InitialBlockKind.Drone, InitialBlockKind.Magnet })
                foreach (RocketDirection direction in Enum.GetValues(typeof(RocketDirection)))
                {
                    Set(cell, "Content", (RuntimeContent)(int)power); Set(cell, "RocketDirection", (RocketDirection?)direction);
                    string before = Snapshot(cell); string path = PuzzleArtworkPaths.Content(cell);
                    Check(path == PuzzleArtworkPaths.Power(power, direction) && before == Snapshot(cell), "파워 경로/읽기 전용 " + power + direction);
                    Asset(power + "-" + direction, path, before);
                }
            Set(cell, "Content", RuntimeContent.Recovery); Asset("recovery", PuzzleArtworkPaths.Content(cell), Snapshot(cell));
            Set(cell, "Content", RuntimeContent.Empty); Check(PuzzleArtworkPaths.Content(cell) == null, "빈칸 내용물 null");
            Set(cell, "Content", RuntimeContent.Normal); Set(cell, "Color", null); Check(PuzzleArtworkPaths.Content(cell) == null, "색 없는 일반 null");
            Set(cell, "Content", RuntimeContent.Recovery); Set(cell, "Cover", (CoverKind?)CoverKind.Mold);
            string snapshot = Snapshot(cell); Check(PuzzleArtworkPaths.Content(cell) == null && snapshot == Snapshot(cell), "곰팡이 은폐/상태 보존");
            Asset("mold", PuzzleArtworkPaths.Cover(cell)); Set(cell, "Cover", null); Check(PuzzleArtworkPaths.Cover(cell) == null, "덮개 없음 null");
            for (int durability = 1; durability <= 3; durability++)
            {
                Set(cell, "Cover", (CoverKind?)CoverKind.Web); Set(cell, "CoverDurability", durability);
                Asset("web-" + durability, PuzzleArtworkPaths.Cover(cell)); Asset("dust-" + durability, PuzzleArtworkPaths.Dust(durability));
            }
            Check(PuzzleArtworkPaths.Dust(0) == null, "먼지0 null");
            RuntimeCell inactive = (RuntimeCell)Activator.CreateInstance(typeof(RuntimeCell), BindingFlags.NonPublic | BindingFlags.Instance, null,
                new object[] { new BoardCoordinate(4, 4), false }, null);
            Set(inactive, "Content", RuntimeContent.Recovery); Check(PuzzleArtworkPaths.Content(inactive) == null, "비활성 내용물 null");
            Observations.Add(JsonUtility.ToJson(new Mapping { label = "null-contracts", detail = "Empty/noColor/Mold/inactive Content=null; noCover=null; Dust0=null" }));
        }

        private static void Bodies()
        {
            foreach (ObstacleKind kind in Enum.GetValues(typeof(ObstacleKind)))
            {
                if (kind == ObstacleKind.Generator)
                {
                    for (int required = 3; required <= 5; required++)
                        for (int charge = 0; charge <= required; charge++)
                        {
                            RuntimeObstacle body = Body(kind, 0, RabbitColor.Type1, required); Set(body, "Charge", charge);
                            Asset("generator-" + charge + "-of-" + required, PuzzleArtworkPaths.Obstacle(body), Snapshot(body));
                        }
                    continue;
                }
                foreach (RabbitColor color in kind == ObstacleKind.Appliance || kind == ObstacleKind.ColorLock ?
                    (RabbitColor[])Enum.GetValues(typeof(RabbitColor)) : new[] { RabbitColor.Type1 })
                {
                    for (int durability = 1; durability <= LevelPlacementRules.MaxDurability(kind); durability++)
                    {
                        RuntimeObstacle body = Body(kind, durability, color, 3); string before = Snapshot(body);
                        Asset(kind + "-" + color + "-" + durability, PuzzleArtworkPaths.Obstacle(body), before);
                        Check(before == Snapshot(body), "본체 조회 무변경 " + kind + color + durability);
                    }
                    Check(PuzzleArtworkPaths.Obstacle(Body(kind, 0, color, 3)) == null, "제거 본체 null " + kind + color);
                }
            }
        }

        private static RuntimeObstacle Body(ObstacleKind kind, int durability, RabbitColor color, int required)
        {
            ObstaclePlacementDefinition definition = JsonUtility.FromJson<ObstaclePlacementDefinition>("{\"kind\":" + (int)kind + ",\"durability\":" + durability + ",\"color\":" + (int)color + ",\"requiredCharge\":" + required + "}");
            return (RuntimeObstacle)Activator.CreateInstance(typeof(RuntimeObstacle), BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] { definition }, null);
        }

        private static void Devices()
        {
            Asset("floor-runtime-center", PuzzleArtworkPaths.Floor); Asset("arrival", PuzzleArtworkPaths.Arrival);
            foreach (bool vertical in new[] { false, true }) { Asset("wall-" + vertical, PuzzleArtworkPaths.Wall(vertical)); Asset("wire-" + vertical, PuzzleArtworkPaths.Wire(vertical)); }
            for (int index = 0; index < 4; index++) foreach (bool exit in new[] { false, true }) Asset("portal-" + index + exit, PuzzleArtworkPaths.Portal(index, exit));
            for (int slot = 0; slot < 3; slot++) foreach (bool connected in new[] { false, true }) Asset("terminal-" + slot + connected, PuzzleArtworkPaths.Terminal(slot, connected));
            foreach (string path in Directory.GetFiles("Assets/Textures/BoardTerrain/Floor", "*.png")) Asset("floor-editor-tile", path.Substring("Assets/Textures/".Length).Replace("\\", "/").Replace(".png", ""));
            foreach (MissionKind kind in Enum.GetValues(typeof(MissionKind)))
            {
                LevelMissionDefinition mission = JsonUtility.FromJson<LevelMissionDefinition>("{\"kind\":" + (int)kind + ",\"color\":0,\"count\":1}");
                string path = PuzzleArtworkPaths.Mission(mission); if (path != null) Asset("mission-" + kind, path);
            }
        }

        private static void Frames()
        {
            foreach (string direction in new[] { "horizontal", "vertical" })
            {
                Asset("rocket-frame1-" + direction, "PowerBlocks/cleaning-rocket-" + direction + "-v1");
                for (int frame = 2; frame <= 4; frame++) Asset("rocket-frame" + frame + "-" + direction, "PowerBlocks/cleaning-rocket-" + direction + "-launch-frame-" + frame + "-v1-256");
            }
            Asset("drone-sheet-frames0-3", "PowerBlocks/collection-drone-rotor-4frames-v1");
            for (int frame = 1; frame <= 4; frame++) Asset("charge-frame-" + frame, "Effects/GeneratorCharge/charge-pulse-" + frame.ToString("00") + "-v1-256");
            foreach (string file in Directory.GetFiles("Assets/Textures/Effects", "*.png", SearchOption.AllDirectories).OrderBy(s => s))
                Asset("effect-frame", file.Substring("Assets/Textures/".Length).Replace("\\", "/").Replace(".png", ""));
            Observations.Add(JsonUtility.ToJson(new Mapping { label = "frame-order", detail = "Rocket base,2,3,4; charge01..04; effects01..N; Drone full sheet, masked frame0=topLeft,1=topRight,2=bottomLeft,3=bottomRight, .06s per frame (source-reviewed)" }));
        }

        private static void EditorNulls()
        {
            int count = LevelBoardArtwork.RequestedAtlasCount;
            Check(LevelBoardArtwork.Rabbit((RabbitColor)99) == null, "Editor invalid color null");
            Check(LevelBoardArtwork.Block(InitialBlockKind.FixedNormal, RocketDirection.Horizontal) == null, "Editor normal power mapping null");
            Check(LevelBoardArtwork.Cover(CoverKind.Web, 0) == null && LevelBoardArtwork.Dust(0) == null, "Editor invalid layer null");
            Check(LevelBoardArtwork.Obstacle(Body(ObstacleKind.Crate, 0, RabbitColor.Type1, 3).Definition) == null, "Editor removed body null");
            Check(count == LevelBoardArtwork.RequestedAtlasCount, "Editor null 조회 로드 없음");
        }

        private static void Asset(string label, string relative, string input = null)
        {
            Check(relative != null, "유효 경로 " + label);
            string path = "Assets/Textures/" + relative + ".png"; string address = BoardSpriteAtlas.AddressFor(relative);
            string atlas = "Assets/Textures/Atlases/" + address + ".spriteatlasv2";
            Check(File.Exists(path) && File.Exists(atlas), "원본/아틀라스 존재 " + label);
            string guid = AssetDatabase.AssetPathToGUID(path); string yaml = File.ReadAllText(atlas);
            Check(guid.Length == 32, "원본 GUID " + label);
            bool packed = yaml.Contains("guid: " + guid);
            for (string parent = Path.GetDirectoryName(path); !packed && parent != null && parent.StartsWith("Assets"); parent = Path.GetDirectoryName(parent))
            {
                string parentGuid = AssetDatabase.AssetPathToGUID(parent.Replace("\\", "/"));
                packed = parentGuid.Length == 32 && yaml.Contains("guid: " + parentGuid);
            }
            Check(packed, "packable 포함 " + label);
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var entry = settings == null ? null : settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(atlas));
            Check(entry != null && entry.address == address, "아틀라스 주소 등록 " + label);
            Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Check(sprites.Length > 0 && texture != null, "읽기 전용 스프라이트 메타데이터 " + label);
            if (Seen.Add(relative)) Observations.Add(JsonUtility.ToJson(new Mapping
            {
                label = label, path = relative, address = address, guid = guid, atlas = atlas,
                width = texture.width, height = texture.height,
                sprites = sprites.Select(s => s.name + ":rect=" + s.rect + ":pivot=" + s.pivot + ":ppu=" + s.pixelsPerUnit).ToArray(),
                inputProperties = input ?? label, detail = "image metadata read-only; Editor correspondence source evidence recorded separately; no atlas loading/packing"
            }));
        }

        [Serializable] private sealed class Mapping
        {
            public string label, path, address, guid, atlas, detail, inputProperties;
            public int width, height;
            public string[] sprites;
        }
    }
}
