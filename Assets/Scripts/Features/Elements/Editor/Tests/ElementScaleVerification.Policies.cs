using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AutoPlay;
using Board;
using Levels;
using Levels.Editor;
using Simulation;
using UnityEditor;
using UnityEngine;

namespace Elements.Editor
{
    public static partial class ElementScaleVerification
    {
        private static readonly List<string> PolicyMeasurements = new List<string>();
        private static object PolicyInvoke(object owner, string method, params object[] arguments) => owner.GetType()
            .GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(owner, arguments);

        private static Dictionary<string, int> PolicyCounts(DroneTargetManager manager, string phase)
        {
            object registry = typeof(DroneTargetManager).GetField("policies", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(manager);
            IDictionary calls = (IDictionary)registry.GetType().GetField("invocations", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(registry);
            Type key = typeof(DroneTargetManager).Assembly.GetType("Simulation.DroneTargetPolicy");
            Dictionary<string, int> result = Enum.GetValues(key).Cast<object>().ToDictionary(value => value.ToString(),
                value => calls.Contains(value) ? (int)calls[value] : 0);
            PolicyMeasurements.Add(phase + "\tcacheBuilds=" + manager.CacheBuildCount + "\treservations=" + manager.ReservationCount +
                "\t" + string.Join("\t", result.Select(item => item.Key + "=" + item.Value)));
            Check(result.Where(item => item.Key != "Durability").All(item => item.Value == 0), phase + " 비활성 정책 호출0");
            return result;
        }

        public static void RunPolicies()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 Editor에서만 실행한다.");
            Results.Clear(); Owned.Clear(); PolicyMeasurements.Clear(); int exit = 0;
            try
            {
                PackedElementDefinition body = PackedElementDefinition.FromDefinition(LegacyElementDefinitions.Get(ObstacleKind.Scrap));
                body.id = "phase05.policy.even-body"; body.displayName = "검사용 짝수 턴 고철"; body.reaction = 3;
                PackedElementDefinition supply = PackedElementDefinition.FromDefinition(LegacyElementDefinitions.GetSupply(SupplyKind.Scrap));
                supply.id = "phase05.policy.scrap-supply"; supply.displayName = "검사용 늦은 고철 공급";
                ElementCatalogAsset catalog = ScriptableObject.CreateInstance<ElementCatalogAsset>(); Owned.Add(catalog);
                using (SerializedObject input = new SerializedObject(catalog))
                {
                    SerializedProperty entries = input.FindProperty("definitions"); entries.arraySize = 2;
                    int index = 0;
                    foreach (PackedElementDefinition value in new[] { body, supply })
                    {
                        ElementDefinitionAsset asset = ScriptableObject.CreateInstance<ElementDefinitionAsset>(); Owned.Add(asset);
                        JsonUtility.FromJsonOverwrite("{\"definition\":" + JsonUtility.ToJson(value) + "}", asset);
                        entries.GetArrayElementAtIndex(index++).objectReferenceValue = asset;
                    }
                    input.ApplyModifiedPropertiesWithoutUndo();
                }
                LevelDefinition level = (LevelDefinition)typeof(PowerEffectVerification).GetMethod("Make", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
                Owned.Add(level);
                using (SerializedObject input = new SerializedObject(level))
                { input.FindProperty("elementCatalog").objectReferenceValue = catalog; input.ApplyModifiedPropertiesWithoutUndo(); }
                LevelElementMigration.Apply(level);
                BoardCoordinate original = new BoardCoordinate(4, 4), generated = new BoardCoordinate(0, 0);
                Type editing = typeof(ElementScaleVerification).Assembly.GetType("Elements.Editor.ElementPlacementEditing");
                PlacementBrush brush = (PlacementBrush)editing.GetMethod("ForDefinition").Invoke(null, new object[] { level, new ElementId(body.id) });
                brush.ReplaceExisting = true; brush.Durability = 1;
                Check(LevelObstacleEditing.Apply(level, brush, new[] { original }).Changed == 1, "드론 정책의 실제 새 행동 본체 배치");
                JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":" + (int)MissionKind.Scrap + ",\"count\":2}]," +
                    "\"elementSupply\":{\"sources\":[{\"coordinate\":{\"row\":0,\"column\":0},\"mode\":1,\"exhaustion\":0," +
                    "\"items\":[{\"definitionId\":\"" + supply.id + "\",\"count\":1,\"durability\":1}]}]}}", level);
                LevelStateBuildResult built = LevelStateBuilder.Build(level, 12345);
                Check(built.IsBuilt, "실제 미션/늦은 공급 입력 유효 " + string.Join(";", built.Issues));
                LevelRuntimeState state = built.State;
                LevelRuntimeState hidden = LevelStateBuilder.Build(level, 12345).State;
                MethodInfo next = typeof(SimulationRandom).GetMethod("Next", BindingFlags.NonPublic | BindingFlags.Instance);
                next.Invoke(hidden.Random, new object[] { 2 }); next.Invoke(hidden.Random, new object[] { 2 });
                MethodInfo snapshot = typeof(LevelInitialStateVerification).GetMethod("Snapshot", BindingFlags.NonPublic | BindingFlags.Static);
                Check((string)snapshot.Invoke(null, new object[] { BotObservationBuilder.Capture(new BoardActionExecutor(state)) }) ==
                    (string)snapshot.Invoke(null, new object[] { BotObservationBuilder.Capture(new BoardActionExecutor(hidden)) }),
                    "새 행동 판도 숨은 난수만 다른 공개 관찰 동일");
                ConstructorInfo contextConstructor = typeof(TurnEffectContext).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null,
                    new[] { typeof(int), typeof(IEnumerable<MatchedBlockChange>) }, null);
                TurnEffectContext context = (TurnEffectContext)contextConstructor.Invoke(new object[] { 2, Array.Empty<MatchedBlockChange>() });
                DroneTargetManager manager = (DroneTargetManager)Activator.CreateInstance(typeof(DroneTargetManager),
                    BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] { state, context }, null);
                Check(manager.Query().Single().Coordinate.Equals(original), "짝수 턴 새 행동은 기존 내구도 미션 정책 사용");
                int initial = PolicyCounts(manager, "initial")["Durability"], builds = manager.CacheBuildCount;
                manager.Query();
                Check(manager.CacheBuildCount == builds && PolicyCounts(manager, "repeat")["Durability"] == initial,
                    "동일 후보 반복 조회는 검색/정책 추가 호출0");
                manager.QueryArea(PowerArea.Blast3);
                Check(PolicyCounts(manager, "overlap-area")["Durability"] == initial, "겹치는 범위 검색도 칸별 정책 결과 공유");
                int request = (int)PolicyInvoke(manager, "Request", generated);
                Check(manager.ReservationCount == 1 && PolicyCounts(manager, "reserve")["Durability"] == initial,
                    "실제 예약은 공유 후보와 정책 결과 사용");
                DroneTarget landed = (DroneTarget)PolicyInvoke(manager, "Land", request, generated);
                Check(landed.Coordinate.Equals(original) && manager.ReservationCount == 0 &&
                    PolicyCounts(manager, "land-revalidate")["Durability"] == initial, "착탄 재검증/반환도 유효 캐시 재사용");
                List<EffectRecord> effects = new List<EffectRecord>();
                typeof(PowerEffectResolution).GetMethod("ApplyHammer", BindingFlags.NonPublic | BindingFlags.Static)
                    .Invoke(null, new object[] { state, original, context, effects, null });
                PolicyInvoke(manager, "Invalidate"); manager.Query();
                Check(state.Missions[0].Progress == 1 && PolicyCounts(manager, "removed")["Durability"] == initial,
                    "실제 제거 뒤 대상 없는 내구도 정책 추가 호출0");
                typeof(RuntimeCell).GetProperty("Content").SetValue(state.CellAt(generated), RuntimeContent.Empty);
                Type supplyRegistry = typeof(ElementId).Assembly.GetType("Elements.ElementSupplyBehaviorRegistry");
                supplyRegistry.GetMethod("Apply", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null,
                    new object[] { catalog.CreateCatalog().Get(new ElementId(supply.id)), state, state.CellAt(generated), new SupplyItem(SupplyKind.Scrap, durability: 1) });
                PolicyInvoke(manager, "Invalidate");
                Check(manager.Query().Single().Coordinate.Equals(generated), "실제 공급 행동으로 늦게 생성된 본체 인식");
                int supplied = PolicyCounts(manager, "supplied")["Durability"];
                Check(supplied == initial + 1, "늦은 공급의 새 대상에 해당 정책1회 추가 호출");
                request = (int)PolicyInvoke(manager, "Request", original);
                typeof(PowerEffectResolution).GetMethod("ApplyHammer", BindingFlags.NonPublic | BindingFlags.Static)
                    .Invoke(null, new object[] { state, generated, context, effects, null });
                PolicyInvoke(manager, "Invalidate");
                DroneTarget retargeted = (DroneTarget)PolicyInvoke(manager, "Refresh", request, original);
                Check(state.Missions[0].Remaining == 0 && retargeted != null && !retargeted.IsMission &&
                    PolicyCounts(manager, "completed-retarget")["Durability"] == supplied, "미션 완료/무효 예약 재탐색은 완료 정책 추가 호출0");
                PolicyInvoke(manager, "Land", request, original);
                Check(manager.ReservationCount == 0 && PolicyCounts(manager, "final-return")["Durability"] == supplied,
                    "재선정 착탄 뒤 예약 잔류0/완료 정책 호출0");
                PolicyMeasurements.Add("INFO 생성/제거는 실제 공통 행동 적용; 갱신은 기존 Invalidate 계약 호출. 슬롯/native 수명은 별도 검사.");
            }
            catch (Exception error) { Results.Add("FAIL " + error); Debug.LogException(error); exit = 1; }
            finally
            {
                foreach (UnityEngine.Object item in Owned) if (item != null) UnityEngine.Object.DestroyImmediate(item);
                Owned.Clear(); File.WriteAllLines("Logs/ElementFramework/Phase05/policy-results.txt", Results);
                File.WriteAllLines("Logs/ElementFramework/Phase05/policy-measurements.txt", PolicyMeasurements);
            }
            EditorApplication.Exit(exit);
        }
    }
}
