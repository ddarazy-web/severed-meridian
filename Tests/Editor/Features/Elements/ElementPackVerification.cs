using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Levels;
using Levels.Editor;
using Board;
using MemoryPack;
using Simulation;
using UnityEditor;
using UnityEngine;

namespace Elements.Editor
{
    public static class ElementPackVerification
    {
        private static readonly List<string> Results = new List<string>();
        private static readonly List<LevelDefinition> Owned = new List<LevelDefinition>();
        private static readonly List<string> States = new List<string>();
        private static readonly List<ScriptableObject> CatalogSources = new List<ScriptableObject>();
        private static void Check(bool condition, string label)
        { if (!condition) throw new InvalidOperationException(label); Results.Add("PASS " + label); }
        private static void Reject(Action action, string label)
        {
            try { action(); } catch (Exception) { Results.Add("PASS " + label); return; }
            throw new InvalidOperationException(label);
        }
        private static LevelDefinition Make(int number = 1)
        {
            LevelDefinition level = (LevelDefinition)typeof(PowerEffectVerification)
                .GetMethod("Make", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            Owned.Add(level); JsonUtility.FromJsonOverwrite("{\"levelNumber\":" + number + "}", level); return level;
        }
        private static string State(LevelRuntimeState state) => string.Join("|", typeof(LevelRuntimeState).GetProperties()
            .Where(property => property.GetIndexParameters().Length == 0 && property.Name != "SchemaVersion" && property.Name != "DefinitionFingerprint")
            .OrderBy(property => property.Name, StringComparer.Ordinal).Select(property => property.Name + "=" +
                typeof(LevelInitialStateVerification).GetMethod("Snapshot", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new[] { property.GetValue(state) }))) + "|NextRandom=" + NextRandom(state);

        private static string NextRandom(LevelRuntimeState state)
        {
            object copy = typeof(SimulationRandom).GetMethod("Copy", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(state.Random, null);
            MethodInfo next = typeof(SimulationRandom).GetMethod("Next", BindingFlags.Instance | BindingFlags.NonPublic);
            return string.Join(",", new[] { 2, 5, 123, 4, 1000 }.Select(bound => next.Invoke(copy, new object[] { bound })));
        }

        private static void Record(BoardActionExecutor[] games, string label)
        {
            for (int i = 0; i < games.Length; i++) States.Add(label + " input=" + i + " schema=" + games[i].State.SchemaVersion +
                " fingerprint=" + games[i].State.DefinitionFingerprint + " logic=" + State(games[i].State));
        }

        private static byte[] Rewrite(byte[] original, Action<ElementLevelPack> edit)
        {
            ElementLevelPack value = LevelPackCodec.DecodeElements(original); edit(value);
            byte[] payload = MemoryPackSerializer.Serialize(value), result = new byte[payload.Length + 8];
            Array.Copy(original, result, 8); Array.Copy(payload, 0, result, 8, payload.Length); return result;
        }

        private static void DefinitionTable()
        {
            ElementDefinition first = new ElementDefinition(new ElementId("body.pack.first"), "첫 신규 본체", new ElementPlacementProfile(1, 9),
                null, new ElementDamageSourcePolicy(true, false, false, true), null, new ElementDamageAggregationPolicy(false),
                new ElementRemovalMissionProfile(MissionKind.Crate), ElementReactionBehavior.Durability);
            ElementDefinition second = new ElementDefinition(new ElementId("body.pack.second"), "둘째 신규 본체", new ElementPlacementProfile(1, 13),
                null, new ElementDamageSourcePolicy(true, true, false, true), null, new ElementDamageAggregationPolicy(false),
                new ElementRemovalMissionProfile(MissionKind.Crate), ElementReactionBehavior.Durability);
            ElementDefinition supply = ElementDefinition.CreateSupply(new ElementId("supply.pack.body"), "신규 본체 공급", ElementSupplyProfile.ForObstacle(first.Id, "pack-"));
            ElementDefinition bomb = ElementDefinition.CreateSupply(new ElementId("power.pack.bomb"), "신규 폭탄", new ElementSupplyProfile(ElementSupplyBehavior.Power, RuntimeContent.Bomb));
            ElementDefinition rocket = ElementDefinition.CreateSupply(new ElementId("power.pack.rocket"), "신규 로켓", new ElementSupplyProfile(ElementSupplyBehavior.Power, RuntimeContent.Rocket));
            ElementDefinition random = ElementDefinition.CreateSupply(new ElementId("supply.pack.random"), "신규 무작위 파워", ElementSupplyProfile.ForRandomPower(new[] { bomb.Id, rocket.Id }));
            ElementDefinition unused = new ElementDefinition(new ElementId("body.pack.unused"), "미등장 본체", new ElementPlacementProfile(1, 2),
                null, new ElementDamageSourcePolicy(true, true, false, true), null, new ElementDamageAggregationPolicy(false),
                new ElementRemovalMissionProfile(MissionKind.Scrap), ElementReactionBehavior.Durability);
            ElementCatalog catalog = new ElementCatalog(new[] { first, second, supply, bomb, rocket, random, unused });
            LevelDefinition level = Make();
            JsonUtility.FromJsonOverwrite("{\"schemaVersion\":5,\"elements\":[{\"definitionId\":\"body.pack.first\",\"layer\":1,\"instanceId\":\"one\",\"coordinate\":{\"row\":2,\"column\":2},\"durability\":7},{\"definitionId\":\"body.pack.second\",\"layer\":1,\"instanceId\":\"two\",\"coordinate\":{\"row\":2,\"column\":4},\"durability\":11}],\"elementSupply\":{\"sources\":[{\"coordinate\":{\"row\":0,\"column\":0},\"mode\":1,\"exhaustion\":0,\"items\":[{\"definitionId\":\"supply.pack.body\",\"count\":1,\"durability\":7},{\"definitionId\":\"supply.pack.random\",\"count\":1}]}]},\"missions\":[{\"kind\":1,\"count\":2}]}", level);
            ElementCatalogAsset sourceCatalog = ScriptableObject.CreateInstance<ElementCatalogAsset>(); CatalogSources.Add(sourceCatalog);
            List<ElementDefinitionAsset> sourceDefinitions = new List<ElementDefinitionAsset>();
            foreach (ElementDefinition definition in catalog.Definitions)
            {
                ElementDefinitionAsset asset = ScriptableObject.CreateInstance<ElementDefinitionAsset>(); CatalogSources.Add(asset);
                typeof(ElementDefinitionAsset).GetField("definition", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(asset, PackedElementDefinition.FromDefinition(definition));
                sourceDefinitions.Add(asset);
            }
            typeof(ElementCatalogAsset).GetField("definitions", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(sourceCatalog, sourceDefinitions);
            using (SerializedObject serialized = new SerializedObject(level))
            { serialized.FindProperty("elementCatalog").objectReferenceValue = sourceCatalog; serialized.ApplyModifiedPropertiesWithoutUndo(); }
            string original = JsonUtility.ToJson(level);
            byte[] bytes = LevelPackCodec.Encode(new[] { level }, catalog);
            ElementLevelPack dto = LevelPackCodec.DecodeElements(bytes);
            HashSet<string> ids = new HashSet<string>(dto.Definitions.Select(value => value.id));
            Check(new[] { first, second, supply, random, bomb, rocket }.All(value => ids.Contains(value.Id.Value)), "배치·공급 본체·무작위 행동 선택 참조의 정의 폐쇄 포함");
            Check(!ids.Contains(unused.Id.Value) && !ids.Contains(LegacyElementMap.Get(ObstacleKind.Safe).Value), "미등장 장애물은 정의 표에서 제외");
            LevelWithCatalog loaded = LevelPackCodec.ReadLevelWithCatalog(bytes, 1); Owned.Add(loaded.Level);
            LevelStateBuildResult build = LevelStateBuilder.Build(loaded.Level, 913);
            Check(build.IsBuilt && build.State.Obstacles[0].Durability == 7 && build.State.Obstacles[1].Durability == 11, "팩의 신규 ID별 내구도 실제 상태 보존");
            Check(build.State.Obstacles[0].Element.Id.Equals(first.Id) && build.State.Obstacles[1].Element.Id.Equals(second.Id), "로드된 본체는 팩 카탈로그의 두 신규 정의 사용");
            LevelStateBuildResult assetBuild = LevelStateBuilder.Build(level, 913);
            Check(assetBuild.IsBuilt && State(assetBuild.State) == State(build.State) && assetBuild.State.DefinitionFingerprint == build.State.DefinitionFingerprint,
                "제작 카탈로그가 연결된 Asset과 팩2의 상태·지문 동일");
            Check(DamageReaction.Evaluate(build.State, new BoardCoordinate(2, 2), DamageCause.Power, new BoardCoordinate(2, 2)).Response == DamageResponse.None &&
                DamageReaction.Evaluate(build.State, new BoardCoordinate(2, 4), DamageCause.Power, new BoardCoordinate(2, 4)).Response != DamageResponse.None,
                "로드된 서로 다른 ID의 파워 피해 정책 실제 실행");
            LevelDefinition reread = LevelPackCodec.ReadLevel(bytes, 1); Owned.Add(reread);
            Check(LevelStateBuilder.Fingerprint(loaded.Level) == LevelStateBuilder.Fingerprint(reread) && bytes.SequenceEqual(LevelPackCodec.Snapshot(loaded.Level)), "불변 카탈로그를 포함한 팩2 스냅샷/다시하기 지문 동일");
            Check(JsonUtility.ToJson(level) == original, "신규 ID 팩 인코딩도 원본 불변");
            LevelDefinition jsonCopy = ScriptableObject.CreateInstance<LevelDefinition>(); Owned.Add(jsonCopy);
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(loaded.Level), jsonCopy);
            LevelStateBuildResult copied = LevelStateBuilder.Build(jsonCopy, 913);
            Check(copied.IsBuilt && State(copied.State) == State(build.State) && LevelStateBuilder.Fingerprint(jsonCopy) == LevelStateBuilder.Fingerprint(loaded.Level),
                "반복 시험/재생의 JSON 사본도 팩 카탈로그의 정의 값과 지문 보존; built=" + copied.IsBuilt + "; issues=" + string.Join(";", copied.Issues) +
                (copied.IsBuilt ? "; state=" + (State(copied.State) == State(build.State)) + "; fingerprint=" + (LevelStateBuilder.Fingerprint(jsonCopy) == LevelStateBuilder.Fingerprint(loaded.Level)) +
                    "; normalPrefix=" + (loaded.Catalog.Get(LegacyElementMap.Get(SupplyKind.RandomNormal)).Supply.BodyIdPrefix ?? "<null>") + "/" +
                    (jsonCopy.CreateElementCatalog().Get(LegacyElementMap.Get(SupplyKind.RandomNormal)).Supply.BodyIdPrefix ?? "<null>") : ""));
            using (AutoPlay.BotBatchSession batch = new AutoPlay.BotBatchSession(level, new[] { 17 }, (record, game) => { }))
            {
                LevelDefinition recorded = ScriptableObject.CreateInstance<LevelDefinition>(); Owned.Add(recorded);
                JsonUtility.FromJsonOverwrite(batch.Record.definitionJson, recorded);
                Check(recorded.ElementCatalog == null && LevelStateBuilder.Fingerprint(recorded) == batch.Record.fingerprint &&
                    State(LevelStateBuilder.Build(recorded, 913).State) == State(build.State), "실제 반복 시험 기록은 제작 에셋 참조 없는 카탈로그 사본 보존");
            }
            Reject(() => LevelPackCodec.ReadLevel(Rewrite(bytes, pack => pack.Definitions = pack.Definitions.Where(value => value.id != first.Id.Value).ToArray()), 1), "배치/공급 본체 정의 누락 거절");
            Reject(() => LevelPackCodec.ReadLevel(Rewrite(bytes, pack => pack.Definitions = pack.Definitions.Where(value => value.id != rocket.Id.Value).ToArray()), 1), "무작위 선택 정의 누락 거절");
            Reject(() => LevelPackCodec.ReadLevel(Rewrite(bytes, pack => pack.Definitions = pack.Definitions.Concat(new[] { pack.Definitions[0] }).ToArray()), 1), "팩 정의 ID 중복 거절");
            Reject(() => LevelPackCodec.ReadLevel(Rewrite(bytes, pack => pack.Definitions = pack.Definitions.Where(value => value.id != LegacyElementMap.Get(SupplyKind.Drone).Value).ToArray()), 1), "미배치 매칭 생성 행동 정의 누락 거절");
            Reject(() => LevelPackCodec.ReadLevel(Rewrite(bytes, pack => pack.Levels[0].Connections.Add(JsonUtility.FromJson<LevelConnectionDefinition>(
                "{\"generatorId\":\"missing-generator\",\"targetId\":\"one\",\"vertices\":[{\"row\":2,\"column\":2}]}"))), 1), "팩 연결 본체 참조 누락 거절");
            Reject(() => LevelPackCodec.ReadLevel(Rewrite(bytes, pack => pack.Levels[0].SchemaVersion = 99), 1), "팩2 미지원 레벨 스키마 거절");
        }

        private static void GameplayParity()
        {
            LevelDefinition level = Make();
            LevelSupplyEditing.AddTopSources(level);
            foreach (var item in new[] { (row: 3, column: 2, color: RabbitColor.Type1), (3, 3, RabbitColor.Type2),
                (3, 4, RabbitColor.Type1), (2, 3, RabbitColor.Type1) })
            {
                BoardCoordinate at = new BoardCoordinate(item.Item1, item.Item2);
                LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Block, Erase = true }, new[] { at });
                PlacementEditResult result = LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Block,
                    Kind = (int)InitialBlockKind.FixedNormal, Color = item.Item3 }, new[] { at });
                Check(result.Changed == 1, "교환 비교 입력 설정 " + at);
            }
            byte[] oldBytes = LevelPackCodec.Encode(new[] { level });
            byte[] bytes = LevelPackCodec.Encode(new[] { level }, LegacyElementDefinitions.DefaultCatalog);
            LevelDefinition v1 = LevelPackCodec.ReadLevel(oldBytes, 1), v2 = LevelPackCodec.ReadLevel(bytes, 1);
            Owned.Add(v1); Owned.Add(v2);
            BoardActionExecutor[] games = new[] { level, v1, v2 }.Select(source => new BoardActionExecutor(LevelStateBuilder.Build(source, 26791).State)).ToArray();
            BoardActionResult[] swaps = games.Select(game => game.Swap(new BoardCoordinate(2, 3), new BoardCoordinate(3, 3))).ToArray();
            Check(swaps.All(result => result.IsApplied) && swaps.Select(result => result.Changes.Count).Distinct().Count() == 1, "Asset·팩1·팩2 실제 매칭 교환 적용");
            Check(State(games[0].State) == State(games[1].State) && State(games[0].State) == State(games[2].State), "교환 직후 미션·난수·전체 상태 동일");
            Record(games, "swap");
            int steps = 0;
            while (games.Any(game => game.HasPendingCascade))
            {
                Check(games.All(game => game.HasPendingCascade) && ++steps <= 400, "세 입력의 연쇄 단계 동기화 " + steps);
                CascadeStepResult[] result = games.Select(game => game.AdvanceCascade()).ToArray();
                Check(result.Select(step => step.Reason).Distinct().Count() == 1 && State(games[0].State) == State(games[1].State) &&
                    State(games[0].State) == State(games[2].State), "연쇄·낙하·공급·미션·난수 동일 " + steps);
                Record(games, "cascade-" + steps);
            }
            Check(steps > 0 && games[0].CascadeHistory.Any(step => step.Settlement?.Records.Any(record => record.Kind == MovementKind.Supply) == true), "실제 정착과 신규 공급을 거쳐 완료");
            Dictionary<string, byte[]> memory = LevelPackBuild.CreatePackBytes(new[] { level, Make(51) });
            Check(memory.Count == 2 && memory.Values.All(value => LevelPackCodec.DecodeElements(value).FormatVersion == 2), "배포 선검증은 50레벨 구간 팩2를 메모리에서만 구성");
            var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.GetSettings(false);
            string before = JsonUtility.ToJson(settings);
            LevelPackBuild.ValidateExclusion(settings);
            Check(JsonUtility.ToJson(settings) == before, "원본 배포 제외 검사는 Addressables 원본을 변경하지 않음");
            foreach (string path in Directory.GetFiles("Assets/Data/LevelPacks", "levels-*.bytes"))
            {
                byte[] disk = File.ReadAllBytes(path); LevelPack pack = LevelPackCodec.Decode(disk);
                LevelDefinition diskLevel = LevelPackCodec.ReadLegacyLevel(disk, pack.Levels[0].LevelNumber); Owned.Add(diskLevel);
                byte[] upgraded = LevelPackCodec.Encode(new[] { diskLevel }, LegacyElementDefinitions.DefaultCatalog);
                LevelDefinition upgradedLevel = LevelPackCodec.ReadLevel(upgraded, diskLevel.LevelNumber); Owned.Add(upgradedLevel);
                Check(State(LevelStateBuilder.Build(diskLevel, 9837).State) == State(LevelStateBuilder.Build(upgradedLevel, 9837).State), "기존 디스크 팩1과 메모리 팩2 실제 입력 동일 " + Path.GetFileName(path));
                Check(disk.SequenceEqual(File.ReadAllBytes(path)), "디스크 팩 원문 보존 " + Path.GetFileName(path));
            }
        }

        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 Editor에서만 실행한다.");
            Results.Clear(); Owned.Clear(); States.Clear(); CatalogSources.Clear(); int exit = 0;
            try
            {
                MethodInfo encode = typeof(LevelPackCodec).GetMethod("Encode", new[] { typeof(IEnumerable<LevelDefinition>), typeof(ElementCatalog) });
                Check(encode != null, "정의 표를 포함한 팩2 인코딩 진입점");
                Func<LevelDefinition[], byte[]> pack = levels => (byte[])encode.Invoke(null, new object[] { levels, LegacyElementDefinitions.DefaultCatalog });
                LevelDefinition original = Make(); string before = JsonUtility.ToJson(original);
                byte[] v1 = LevelPackCodec.Encode(new[] { original });
                byte[] v2 = pack(new[] { original });
                Check(!v1.SequenceEqual(v2), "팩1과 팩2는 별도 바이트 계약");
                LevelDefinition copy = LevelPackCodec.ReadLevel(v2, 1); Owned.Add(copy);
                Check(copy.SchemaVersion == 5 && copy.ElementCatalog == null && copy.Elements.Count > 0, "팩2는 ID 배치와 원본 에셋 없는 입력 복원");
                Check(JsonUtility.ToJson(original) == before, "구형 원본의 메모리 변환은 원본 불변");
                LevelStateBuildResult a = LevelStateBuilder.Build(original, 7221), b = LevelStateBuilder.Build(copy, 7221);
                Check(a.IsBuilt && b.IsBuilt && State(a.State) == State(b.State), "Asset4와 메모리 팩2의 전체 논리 초기 상태 동일");
                Check(v2.SequenceEqual(pack(new[] { copy })), "팩2 재인코딩 바이트 동일");
                LevelDefinition restart = LevelPackCodec.ReadLevel(v2, 1); Owned.Add(restart);
                Check(LevelStateBuilder.Fingerprint(copy) == LevelStateBuilder.Fingerprint(restart), "다시 읽기 지문은 Unity 인스턴스와 독립");
                Check(State(b.State) == State(LevelStateBuilder.Build(restart, 7221).State), "다시하기 같은 시드 상태 동일");
                foreach (int number in new[] { 1, 50, 51, 100, 101 })
                {
                    LevelDefinition numbered = Make(number), loaded = LevelPackCodec.ReadLevel(pack(new[] { numbered }), number); Owned.Add(loaded);
                    Check(loaded.LevelNumber == number && LevelPackCodec.FirstLevel(number) == (number <= 50 ? 1 : number <= 100 ? 51 : 101), "팩2 구간 경계 " + number);
                }
                LevelDefinition last = Make(50), next = Make(51);
                byte[] sparse = pack(new[] { last, original });
                Reject(() => pack(new[] { original, original }), "팩2 중복 레벨 거절");
                Reject(() => pack(new[] { last, next }), "팩2 구간 혼합 거절");
                Reject(() => LevelPackCodec.ReadLevel(sparse, 2), "팩2 누락 레벨 거절");
                Reject(() => LevelPackCodec.ReadLevel(sparse, 51), "팩2 다른 구간 요청 거절");
                Reject(() => LevelPackCodec.ReadLevel(v2.Take(9).ToArray(), 1), "팩2 잘린 본문 거절");
                byte[] wrong = (byte[])v2.Clone(); wrong[4] = 99;
                Reject(() => LevelPackCodec.ReadLevel(wrong, 1), "팩2 미지원 헤더 버전 거절");
                LevelDefinition old = LevelPackCodec.ReadLevel(v1, 1); Owned.Add(old);
                Check(old.SchemaVersion == 4 && v1.SequenceEqual(LevelPackCodec.Encode(new[] { old })), "명시적 팩1 읽기와 원문 재인코딩 보존");
                LevelElementMigration.Apply(original);
                Reject(() => original.ToPacked(), "신형 ID 데이터를 구형 DTO로 저장 거절");
                JsonUtility.FromJsonOverwrite("{\"elements\":[{\"definitionId\":\"missing.body\",\"layer\":1,\"instanceId\":\"missing\",\"durability\":1}]}", original);
                Reject(() => pack(new[] { original }), "팩2 미등록 배치 ID 거절");
                DefinitionTable();
                GameplayParity();
            }
            catch (Exception error) { Results.Add("FAIL " + error); exit = 1; }
            finally
            {
                foreach (LevelDefinition level in Owned) { Undo.ClearUndo(level); UnityEngine.Object.DestroyImmediate(level); }
                foreach (ScriptableObject source in CatalogSources) UnityEngine.Object.DestroyImmediate(source);
                File.WriteAllLines("Logs/ElementFramework/Phase03/pack-results.txt", Results);
                File.WriteAllLines("Logs/ElementFramework/Phase03/pack-gameplay-states.txt", States);
                EditorApplication.Exit(exit);
            }
        }
    }
}
