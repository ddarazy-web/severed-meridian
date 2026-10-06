using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Levels;
using MemoryPack;
using UnityEditor;
using UnityEngine;

namespace Elements.Editor
{
    /// <summary>제작 입력의 유효성·불변 경계·배포 왕복을 실제 값으로 검사한다.</summary>
    public static class ElementAuthoringVerification
    {
        private const string Evidence = "Logs/ElementFramework/Phase03/authoring-results.txt";
        private static readonly List<string> Results = new List<string>();
        private static readonly List<UnityEngine.Object> Created = new List<UnityEngine.Object>();
        private static Type assetType, catalogType, packedType;

        private static void Check(bool pass, string message)
        { if (!pass) throw new InvalidOperationException(message); Results.Add("PASS " + message); }
        private static ScriptableObject Asset(string json)
        {
            ScriptableObject asset = ScriptableObject.CreateInstance(assetType);
            Created.Add(asset); JsonUtility.FromJsonOverwrite(json, asset); return asset;
        }
        private static ElementDefinition Definition(ScriptableObject asset) =>
            (ElementDefinition)assetType.GetMethod("ToDefinition").Invoke(asset, null);
        private static void Reject(Action action, string id)
        {
            Exception found = null;
            try { action(); }
            catch (Exception error) { found = error is TargetInvocationException ? error.InnerException : error; }
            Check(found != null && found.Message.Contains(id), "명시적 제작 오류 " + id);
        }
        private static string Fixture(string id, string displayName = "목재 상자", int maximum = 7) =>
            "{\"definition\":{\"id\":\"" + id + "\",\"displayName\":\"" + displayName +
            "\",\"placement\":{\"enabled\":true,\"size\":1,\"maxDurability\":" + maximum +
            "},\"damage\":{\"enabled\":true,\"adjacentMatch\":true,\"power\":true,\"hammer\":true}," +
            "\"aggregation\":{\"enabled\":true,\"perHitCell\":false},\"removal\":{\"enabled\":true,\"kind\":1},\"reaction\":1}}";

        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 Editor에서만 실행한다.");
            Directory.CreateDirectory(Path.GetDirectoryName(Evidence)); Results.Clear(); int exit = 0;
            try
            {
                assetType = typeof(ElementId).Assembly.GetType("Elements.ElementDefinitionAsset");
                catalogType = typeof(ElementId).Assembly.GetType("Elements.ElementCatalogAsset");
                packedType = typeof(ElementId).Assembly.GetType("Elements.PackedElementDefinition");
                Check(assetType != null && catalogType != null && packedType != null, "제작 원본에서 정의/배포 카탈로그 구성 가능");
                // 잘못된 ID 덮어쓰기, 프로필 누락 허용, 원본 참조 보유를 각각 잡는 독립 입력이다.
                ScriptableObject first = Asset(Fixture("obstacle.fixture.wood"));
                ScriptableObject second = Asset(Fixture("obstacle.fixture.metal", maximum: 11));
                ElementDefinition wood = Definition(first), metal = Definition(second);
                Check(wood.Id.Value == "obstacle.fixture.wood" && metal.Id.Value == "obstacle.fixture.metal" &&
                    wood.RequirePlacement().MaxDurability == 7 && metal.RequirePlacement().MaxDurability == 11 &&
                    wood.RequireReactionBehavior() == ElementReactionBehavior.Durability &&
                    metal.RequireReactionBehavior() == ElementReactionBehavior.Durability, "동일 행동의 두 ID와 서로 다른 수치 보존");
                ScriptableObject source = ScriptableObject.CreateInstance(catalogType); Created.Add(source);
                SerializedObject serialized = new SerializedObject(source);
                SerializedProperty entries = serialized.FindProperty("definitions"); entries.arraySize = 2;
                entries.GetArrayElementAtIndex(0).objectReferenceValue = first;
                entries.GetArrayElementAtIndex(1).objectReferenceValue = second;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                ElementCatalog catalog = (ElementCatalog)catalogType.GetMethod("CreateCatalog").Invoke(source, null);
                Check(catalog.Count == 2 && catalog.Get(wood.Id).RequirePlacement().MaxDurability == 7 &&
                    catalog.Get(metal.Id).RequirePlacement().MaxDurability == 11, "제작 목록의 정확한 ID 조회");
                JsonUtility.FromJsonOverwrite(Fixture("obstacle.fixture.wood", "변경된 이름", 3), first);
                Check(catalog.Get(wood.Id).DisplayName == "목재 상자" && catalog.Get(wood.Id).RequirePlacement().MaxDurability == 7,
                    "원본 변경 뒤 구성된 카탈로그 불변");
                Check(Definition(first).Id == wood.Id && Definition(first).DisplayName == "변경된 이름", "표시명 변경과 영구 ID 독립");
                entries.GetArrayElementAtIndex(1).objectReferenceValue = first; serialized.ApplyModifiedPropertiesWithoutUndo();
                Reject(() => catalogType.GetMethod("CreateCatalog").Invoke(source, null), "obstacle.fixture.wood");
                Reject(() => Definition(Asset("{\"definition\":{\"id\":\"obstacle.missing\",\"displayName\":\"누락\",\"reaction\":1}}")), "obstacle.missing");
                Reject(() => Definition(Asset(Fixture("obstacle.bad-charge").Replace("\"reaction\":1", "\"reaction\":2"))), "obstacle.bad-charge");
                Reject(() => Definition(Asset(Fixture("obstacle.bad-size").Replace("\"size\":1", "\"size\":0"))), "obstacle.bad-size");
                Reject(() => Definition(Asset(Fixture(""))), "ID");
                MethodInfo from = packedType.GetMethod("FromDefinition");
                MethodInfo to = packedType.GetMethod("ToDefinition");
                object packed = from.Invoke(null, new object[] { metal });
                ElementDefinition restored = (ElementDefinition)to.Invoke(JsonUtility.FromJson(JsonUtility.ToJson(packed), packedType), null);
                Check(restored.Id == metal.Id && restored.RequirePlacement().MaxDurability == 11 && restored.RequireDamageSourcePolicy().Power,
                    "명시적 배포 값으로 정의 재구성");
                // MemoryPack의 실제 타입 지정 API로 구간 팩과 같은 직렬화 경계를 검사한다.
                byte[] bytes = MemoryPackSerializer.Serialize(packedType, packed);
                ElementDefinition binary = (ElementDefinition)to.Invoke(MemoryPackSerializer.Deserialize(packedType, bytes), null);
                Check(binary.Id.Value == "obstacle.fixture.metal" && binary.RequirePlacement().MaxDurability == 11,
                    "MemoryPack 배포 정의 왕복");
                foreach (ObstacleKind kind in Enum.GetValues(typeof(ObstacleKind)))
                {
                    ElementDefinition legacy = LegacyElementDefinitions.Get(kind);
                    ElementDefinition roundTrip = (ElementDefinition)to.Invoke(from.Invoke(null, new object[] { legacy }), null);
                    Check(roundTrip.Id == legacy.Id && roundTrip.ReactionBehavior == legacy.ReactionBehavior &&
                        roundTrip.DamageSourcePolicy.Power == legacy.DamageSourcePolicy.Power &&
                        (roundTrip.Placement?.MaxDurability ?? 0) == (legacy.Placement?.MaxDurability ?? 0) &&
                        (roundTrip.ChargePlacement?.ChargePerHit ?? 0) == (legacy.ChargePlacement?.ChargePerHit ?? 0),
                        "구형 장애물 제작/배포 수치 " + kind);
                }
                foreach (CoverKind kind in Enum.GetValues(typeof(CoverKind)))
                {
                    ElementDefinition legacy = LegacyElementDefinitions.Get(kind);
                    object value = from.Invoke(null, new object[] { legacy });
                    ElementDefinition roundTrip = (ElementDefinition)to.Invoke(MemoryPackSerializer.Deserialize(packedType,
                        MemoryPackSerializer.Serialize(packedType, value)), null);
                    Check(roundTrip.RequireLayer().Behavior == legacy.RequireLayer().Behavior &&
                        roundTrip.RequireLayer().Mission == legacy.RequireLayer().Mission &&
                        roundTrip.RequirePlacement().MaxDurability == legacy.RequirePlacement().MaxDurability &&
                        roundTrip.Turn?.InitialDurability == legacy.Turn?.InitialDurability, "덮개/번식 배포 왕복 " + kind);
                }
                ElementDefinition dust = (ElementDefinition)to.Invoke(from.Invoke(null, new object[] { LegacyElementDefinitions.GetDust() }), null);
                Check(dust.Id.Value == "floor.dust" && dust.RequireLayer().Mission == MissionKind.Dust &&
                    dust.RequirePlacement().MaxDurability == 3, "먼지 배포 프로필 보존");
                foreach (SupplyKind kind in Enum.GetValues(typeof(SupplyKind)))
                {
                    ElementDefinition legacy = LegacyElementDefinitions.GetSupply(kind);
                    object value = from.Invoke(null, new object[] { legacy });
                    ScriptableObject supplied = Asset("{\"definition\":" + JsonUtility.ToJson(value) + "}");
                    ElementDefinition roundTrip = Definition(supplied);
                    Check(roundTrip.Id == legacy.Id && roundTrip.RequireSupply().Behavior == legacy.RequireSupply().Behavior &&
                        roundTrip.RequireSupply().Content == legacy.RequireSupply().Content &&
                        roundTrip.RequireSupply().Choices.Count == legacy.RequireSupply().Choices.Count,
                        "Unity 제작 직렬화에서 공급 프로필 보존 " + kind);
                }
                object randomPacked = from.Invoke(null, new object[] { LegacyElementDefinitions.GetSupply(SupplyKind.RandomPower) });
                ElementDefinition randomDefinition = (ElementDefinition)to.Invoke(randomPacked, null);
                object supplyPacked = packedType.GetField("supply").GetValue(randomPacked);
                SupplyKind[] choices = (SupplyKind[])supplyPacked.GetType().GetField("choices").GetValue(supplyPacked);
                choices[0] = SupplyKind.Magnet;
                Check(randomDefinition.RequireSupply().Choices[0] == SupplyKind.Rocket, "배포 DTO 배열 변경이 불변 정의에 영향 없음");
                entries.arraySize = 0; serialized.ApplyModifiedPropertiesWithoutUndo();
                Check(catalog.Count == 2 && catalog.Get(wood.Id).RequirePlacement().MaxDurability == 7,
                    "제작 목록 제거 후 실행 카탈로그 유지");
                UnityEngine.Object.DestroyImmediate(second);
                Check(catalog.Get(metal.Id).RequirePlacement().MaxDurability == 11, "제작 원본 파기 후 정의 수치 유지");
            }
            catch (Exception error) { Results.Add("FAIL " + error); Debug.LogException(error); exit = 1; }
            finally
            {
                foreach (UnityEngine.Object asset in Created) if (asset != null) UnityEngine.Object.DestroyImmediate(asset);
                Created.Clear(); File.WriteAllLines(Evidence, Results);
            }
            EditorApplication.Exit(exit);
        }
    }
}
