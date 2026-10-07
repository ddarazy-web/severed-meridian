using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
        // 출시 콘텐츠를 바꾸지 않고 신규 행동이 공통 적용/미션 경로로 연결되는지 확인한다.
        public static void RunBehavior()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 Editor에서만 실행한다.");
            Results.Clear(); Owned.Clear(); int exit = 0;
            try
            {
                PackedElementDefinition packed = PackedElementDefinition.FromDefinition(LegacyElementDefinitions.Get(ObstacleKind.Crate));
                packed.id = "phase05.behavior.even-turn-body"; packed.displayName = "검사용 짝수 턴 본체"; packed.reaction = 3;
                ElementDefinition definition = packed.ToDefinition();
                Check((int)definition.RequireReactionBehavior() == 3, "새 고유 행동의 제작 프로필 검증/등록 키");
                ElementDefinitionAsset authored = ScriptableObject.CreateInstance<ElementDefinitionAsset>(); Owned.Add(authored);
                JsonUtility.FromJsonOverwrite("{\"definition\":" + JsonUtility.ToJson(packed) + "}", authored);
                ElementCatalogAsset catalog = ScriptableObject.CreateInstance<ElementCatalogAsset>(); Owned.Add(catalog);
                using (SerializedObject input = new SerializedObject(catalog))
                {
                    SerializedProperty entries = input.FindProperty("definitions"); entries.arraySize = 1;
                    entries.GetArrayElementAtIndex(0).objectReferenceValue = authored; input.ApplyModifiedPropertiesWithoutUndo();
                }
                LevelDefinition level = (LevelDefinition)typeof(PowerEffectVerification).GetMethod("Make", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
                Owned.Add(level);
                using (SerializedObject input = new SerializedObject(level))
                { input.FindProperty("elementCatalog").objectReferenceValue = catalog; input.ApplyModifiedPropertiesWithoutUndo(); }
                LevelElementMigration.Apply(level);
                BoardCoordinate target = new BoardCoordinate(4, 4);
                Type editing = typeof(ElementScaleVerification).Assembly.GetType("Elements.Editor.ElementPlacementEditing");
                PlacementBrush brush = (PlacementBrush)editing.GetMethod("ForDefinition").Invoke(null, new object[] { level, definition.Id });
                brush.ReplaceExisting = true; brush.Durability = 1;
                Check(LevelObstacleEditing.Apply(level, brush, new[] { target }).Changed == 1, "새 행동의 실제 제작 선택/배치");
                JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":" + (int)MissionKind.Crate + ",\"count\":1}]}", level);
                LevelStateBuildResult built = LevelStateBuilder.Build(level, 12345);
                Check(built.IsBuilt && built.State.Obstacles.Single().Element.Id == definition.Id,
                    "새 행동 제작 정의의 공통 런타임 배치 " + string.Join(";", built.Issues));
                byte[] bytes = LevelPackCodec.Snapshot(level);
                LevelDefinition restored = LevelPackCodec.ReadLevel(bytes, level.LevelNumber); Owned.Add(restored);
                LevelStateBuildResult rebuilt = LevelStateBuilder.Build(restored, 12345);
                Check(rebuilt.IsBuilt && LevelPackCodec.Snapshot(restored).SequenceEqual(bytes), "새 행동의 팩2 제작/정의 왕복");
                string original = JsonUtility.ToJson(level), fingerprint = LevelStateBuilder.Fingerprint(level);
                foreach (LevelRuntimeState state in new[] { built.State, rebuilt.State })
                {
                    ConstructorInfo constructor = typeof(TurnEffectContext).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance,
                        null, new[] { typeof(int), typeof(IEnumerable<MatchedBlockChange>) }, null);
                    TurnEffectContext odd = (TurnEffectContext)constructor.Invoke(new object[] { 1, Array.Empty<MatchedBlockChange>() });
                    TurnEffectContext even = (TurnEffectContext)constructor.Invoke(new object[] { 2, Array.Empty<MatchedBlockChange>() });
                    int draws = state.Random.DrawCount;
                    Check(DamageReaction.Evaluate(state, target, DamageCause.Power, target, odd).Response == DamageResponse.Protected &&
                        state.Obstacles[0].Durability == 1 && state.Missions[0].Progress == 0, "홀수 턴 고유 보호 반응/조회 무변경");
                    Check(DamageReaction.Evaluate(state, target, DamageCause.Power, target, even).Response == DamageResponse.Damage,
                        "짝수 턴 공통 내구도 반응으로 위임");
                    List<EffectRecord> effects = new List<EffectRecord>();
                    object[] arguments = { state, target, even, effects, null };
                    bool applied = (bool)typeof(PowerEffectResolution).GetMethod("ApplyHammer", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, arguments);
                    Check(applied && state.Obstacles[0].Durability == 0 && state.CellAt(target).Content == RuntimeContent.Empty &&
                        state.Missions[0].Progress == 1 && effects.Any(effect => effect.Response == DamageResponse.Damage),
                        "새 행동도 실제 공통 피해/제거/미션/효과 기록 경로 사용");
                    Check(state.Random.DrawCount == draws, "새 행동의 조회/피해/제거는 규칙 난수 소비0");
                }
                Check(JsonUtility.ToJson(level) == original && LevelStateBuilder.Fingerprint(level) == fingerprint,
                    "새 행동 검사 후 제작 원본/규칙 지문 무변경");
                Check(built.State.Obstacles[0].Durability == rebuilt.State.Obstacles[0].Durability &&
                    built.State.Missions[0].Progress == rebuilt.State.Missions[0].Progress, "새 행동 Asset/MemoryPack 실제 적용 결과 동등");
            }
            catch (Exception error) { Results.Add("FAIL " + error); Debug.LogException(error); exit = 1; }
            finally
            {
                foreach (UnityEngine.Object item in Owned) if (item != null) UnityEngine.Object.DestroyImmediate(item);
                Owned.Clear(); File.WriteAllLines("Logs/ElementFramework/Phase05/behavior-results.txt", Results);
            }
            EditorApplication.Exit(exit);
        }
    }
}
