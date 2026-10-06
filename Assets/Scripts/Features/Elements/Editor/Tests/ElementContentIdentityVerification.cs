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
    /// <summary>선택된 블록 정의가 복사·교환·낙하·섞기에서 점유자와 함께 이동하는지 검사한다.</summary>
    public static class ElementContentIdentityVerification
    {
        private static readonly List<string> Results = new List<string>();
        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); Results.Add("PASS " + message); }
        private static BoardCoordinate C(int row, int column) => new BoardCoordinate(row, column);
        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 Editor에서만 실행한다.");
            Results.Clear(); int exit = 0; LevelDefinition level = null;
            try
            {
                PropertyInfo identity = typeof(RuntimeCell).GetProperty("ContentElement", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                Check(identity != null, "실행 블록에 선택 정의 조회 계약 존재");
                level = (LevelDefinition)typeof(PowerEffectVerification).GetMethod("Make", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                ElementPlacementDefinition[] placements = LevelElementMigration.Preview(level);
                ElementDefinition first = ElementDefinition.CreateSupply(new ElementId("block.fixture.first"), "첫 토끼",
                    new ElementSupplyProfile(ElementSupplyBehavior.FixedNormal, RuntimeContent.Normal));
                ElementDefinition second = ElementDefinition.CreateSupply(new ElementId("block.fixture.second"), "둘째 토끼",
                    new ElementSupplyProfile(ElementSupplyBehavior.FixedNormal, RuntimeContent.Normal));
                placements.Single(item => item.coordinate.Equals(C(0, 0))).definitionId = first.Id.Value;
                placements.Single(item => item.coordinate.Equals(C(0, 1))).definitionId = second.Id.Value;
                List<ElementDefinition> definitions = new List<ElementDefinition> { first, second };
                foreach (string id in placements.Select(item => item.definitionId).Distinct().Where(id => id != first.Id.Value && id != second.Id.Value))
                    definitions.Add(LegacyElementDefinitions.DefaultCatalog.Get(new ElementId(id)));
                string array = string.Join(",", placements.Select(JsonUtility.ToJson));
                JsonUtility.FromJsonOverwrite("{\"schemaVersion\":5,\"elements\":[" + array + "]}", level);
                string original = JsonUtility.ToJson(level);
                LevelStateBuildResult built = LevelStateBuilder.Build(level, 7182, new ElementCatalog(definitions));
                Check(built.IsBuilt, "동일 일반 블록 행동의 서로 다른 두 ID 구성 " + string.Join(";", built.Issues));
                ElementDefinition Get(RuntimeCell cell) => (ElementDefinition)identity.GetValue(cell);
                Check(ReferenceEquals(Get(built.State.CellAt(C(0, 0))), first) && ReferenceEquals(Get(built.State.CellAt(C(0, 1))), second),
                    "초기 구성은 색과 함께 선택 블록 정의 유지");
                LevelRuntimeState copy = (LevelRuntimeState)Activator.CreateInstance(typeof(LevelRuntimeState),
                    BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { built.State }, null);
                Check(ReferenceEquals(Get(copy.CellAt(C(0, 0))), first), "작업 사본의 불변 블록 정의 유지");
                BoardActionExecutor executor = new BoardActionExecutor(built.State);
                ItemUseResult swap = executor.UseItem(BoardItem.Swap, C(0, 0), C(0, 1));
                Check(swap.IsApplied && ReferenceEquals(Get(executor.State.CellAt(C(0, 0))), second) &&
                    ReferenceEquals(Get(executor.State.CellAt(C(0, 1))), first), "실제 교환 아이템은 정의도 점유자와 교환");
                Check(ReferenceEquals(Get(built.State.CellAt(C(0, 0))), first), "교환은 입력 상태 불변");
                PropertyInfo content = typeof(RuntimeCell).GetProperty("Content");
                content.SetValue(copy.CellAt(C(1, 0)), RuntimeContent.Empty);
                Check(Get(copy.CellAt(C(1, 0))) == null, "점유자 제거 시 이전 정의 제거");
                SettlementResult fallen = SettlementResolution.Resolve(copy);
                Check(fallen.IsApplied && ReferenceEquals(Get(fallen.State.CellAt(C(1, 0))), first),
                    "실제 수직 낙하는 선택 블록 정의를 목적지로 전달");
                ShuffleResult shuffle = ShuffleResolution.Resolve(built.State);
                Check(shuffle.Reason == ShuffleReason.Applied && shuffle.State.Cells.Count(cell => ReferenceEquals(Get(cell), first)) == 1 &&
                    shuffle.State.Cells.Count(cell => ReferenceEquals(Get(cell), second)) == 1,
                    "실제 섞기는 개별 정의의 개수 유지");
                Type supply = typeof(LevelRuntimeState).Assembly.GetType("Elements.ElementSupplyBehaviorRegistry");
                supply.GetMethod("Apply", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,
                    new object[] { second, copy, copy.CellAt(C(0, 0)), new SupplyItem(SupplyKind.FixedNormal, 1, RabbitColor.Type2) });
                Check(ReferenceEquals(Get(copy.CellAt(C(0, 0))), second), "같은 종류 재공급도 새 선택 정의로 교체");
                BoardActionExecutor boosted = new BoardActionExecutor(built.State, new[] { StartBooster.Rocket });
                Check(boosted.State.Cells.Where(cell => cell.Content == RuntimeContent.Rocket).All(cell =>
                    ReferenceEquals(Get(cell), LegacyElementDefinitions.GetSupply(SupplyKind.Rocket))) &&
                    boosted.State.Cells.Any(cell => cell.Content == RuntimeContent.Rocket), "시작 부스터는 새 파워 정의로 교체");
                LevelRuntimeState matched = (LevelRuntimeState)Activator.CreateInstance(typeof(LevelRuntimeState),
                    BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { built.State }, null);
                for (int column = 0; column < 4; column++) typeof(RuntimeCell).GetProperty("Color").SetValue(matched.CellAt(C(4, column)), (RabbitColor?)RabbitColor.Type1);
                TurnEffectContext context = (TurnEffectContext)Activator.CreateInstance(typeof(TurnEffectContext),
                    BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { 1, Array.Empty<MatchedBlockChange>() }, null);
                object decisions = typeof(MatchResolution).GetMethod("SelectAutomatic", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { MatchQuery.Find(matched), context, matched.Random });
                typeof(MatchResolution).GetMethod("ApplyLayers", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { matched, decisions, 1, context });
                Check(matched.Cells.Any(cell => cell.Content == RuntimeContent.Rocket) &&
                    matched.Cells.Where(cell => cell.Content == RuntimeContent.Rocket).All(cell => ReferenceEquals(Get(cell),
                        LegacyElementDefinitions.GetSupply(SupplyKind.Rocket))), "실제 매칭 생성은 새 파워 정의로 교체");
                Check(JsonUtility.ToJson(level) == original, "복사·교환·낙하·섞기·공급은 제작 원본 불변");
            }
            catch (Exception error) { Results.Add("FAIL " + error); Debug.LogException(error); exit = 1; }
            finally
            {
                if (level != null) UnityEngine.Object.DestroyImmediate(level);
                File.WriteAllLines("Logs/ElementFramework/Phase03/content-identity-results.txt", Results);
            }
            EditorApplication.Exit(exit);
        }
    }
}
