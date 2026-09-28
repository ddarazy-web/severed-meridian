using System;
using System.IO;
using System.Linq;
using Board;
using Simulation;
using UnityEditor;
using UnityEngine;

namespace Levels.Editor
{
    public static partial class ScrapVerification
    {
        public static void Edges()
        {
            Results.Clear();
            try
            {
                foreach (bool maintain in new[] { false, true })
                {
                    LevelDefinition level = Sparse(C(0, 0), C(1, 1), C(2, 1)); LevelFlowEditing.SetPortal(level, C(2, 1), C(0, 0));
                    if (maintain) Maintain(level, new[] { C(0, 0) }, 1, 3, 2); else Fixed(level, C(0, 0), new SupplyItem(SupplyKind.Scrap, durability: 2));
                    LevelRuntimeState state = Build(level); Empty(state, state.Cells.Where(c => c.IsActive).Select(c => c.Coordinate).ToArray()); string before = Snapshot(state);
                    SettlementResult result = SettlementResolution.Resolve(state);
                    Check(result.Reason == SettlementReason.Repeating && result.State == null && Snapshot(state) == before && state.Obstacles.Count == 0 && state.Supply.ScrapGenerated == 0,
                        "고철 생성 후 반복 실패에서 본체/커서/카운터/난수 전체 폐기 " + maintain);
                }
                LevelDefinition full = Make(); Place(full, C(9, 8), 2); Place(full, C(9, 9), 2); Maintain(full, new[] { C(0, 0) }, 1, 6, 3);
                LevelRuntimeState over = Build(full); Empty(over, new[] { C(0, 0) }); SettlementResult maintained = SettlementResolution.Resolve(over);
                Check(maintained.IsApplied && maintained.State.LiveScrapCount == 2 && maintained.State.Supply.ScrapGenerated == 0 && maintained.State.CellAt(C(0, 0)).Content == RuntimeContent.Normal, "최초 배치 목표 초과시 제거/추가 없이 일반 공급");
                LevelDefinition random = Make(); Fixed(random, C(0, 0), new SupplyItem(SupplyKind.Scrap));
                LevelSupplyEditing.SetSourceProperty(random, new[] { 0 }, "exhaustion", (int)SupplyExhaustion.Random);
                LevelRuntimeState queue = Build(random); Empty(queue, new[] { C(0, 0) }); queue = SettlementResolution.Resolve(queue).State; Hit(queue, C(0, 0), Context());
                SettlementResult fallback = SettlementResolution.Resolve(queue);
                Check(fallback.IsApplied && fallback.State.CellAt(C(0, 0)).Content == RuntimeContent.Normal && fallback.RandomAfter == fallback.RandomBefore + 1 && fallback.State.Supply.Sources[0].ItemIndex == 1, "고정 고철 소진 Random 전환");

                LevelDefinition last = Make(); Power(last, C(4, 3), RuntimeContent.Rocket); Place(last, C(4, 4), 5); Place(last, C(4, 5), 1);
                JsonUtility.FromJsonOverwrite("{\"moveCount\":1}", last); Mission(last, 2); BoardActionExecutor executor = new BoardActionExecutor(Build(last));
                Check(executor.Activate(C(4, 3)).IsApplied && executor.State.Obstacles[0].Durability == 4 && executor.State.Obstacles[1].Durability == 0 && executor.State.CellAt(C(4, 6)).Content == RuntimeContent.Empty,
                    "로켓 모든 고철 관통/뒤 일반 블록 제거");
                Finish(executor); Check(executor.State.MovesRemaining == 0 && !executor.HasPendingCascade && executor.State.Missions[0].Progress == 1 && !executor.Activate(C(0, 0)).IsApplied, "고철 마지막 수 정착/자동 매칭 완료 후 행동 금지"); Occupancy(executor.State, "마지막 수");
                LevelDefinition adjacent = Make(); Power(adjacent, C(4, 3), RuntimeContent.Rocket); Place(adjacent, C(3, 4), 3);
                BoardActionExecutor blast = new BoardActionExecutor(Build(adjacent)); blast.Activate(C(4, 3));
                Check(blast.State.Obstacles[0].Durability == 3, "파워가 옆 일반 블록 제거해도 인접 매칭 피해 없음");
                LevelDefinition magnet = Make(); Power(magnet, C(4, 3), RuntimeContent.Magnet); Place(magnet, C(4, 4), 3);
                BoardActionExecutor magnetic = new BoardActionExecutor(Build(magnet));
                Check(magnetic.Activate(C(4, 3)).IsApplied && magnetic.State.Obstacles[0].Durability == 3 && magnetic.State.CellAt(C(4, 4)).ObstacleIndex == 0, "단독 자석 일반 색 선택에서 고철 제외");
                LevelDefinition targets = Make(); Place(targets, C(4, 4), 1); Invoke(typeof(PowerEffectVerification), "Crate", null, targets, C(4, 5), 2);
                JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":1,\"count\":1},{\"kind\":" + (int)MissionKind.Scrap + ",\"count\":1}]}", targets);
                LevelRuntimeState separate = Build(targets); TurnEffectContext turn = Context();
                Check(MissionProgressRules.Query(separate, C(4, 4), turn).Single().MissionIndex == 1 && MissionProgressRules.Query(separate, C(4, 5), turn).Single().MissionIndex == 0, "상자·고철 미션 기여 구분");
                Hit(separate, C(4, 4), turn); Check(separate.Missions[0].Progress == 0 && separate.Missions[1].Progress == 1, "고철 제거가 상자 미션 증가시키지 않음");
                LevelDefinition conflict = Make(); Fixed(conflict, C(0, 0), new SupplyItem(SupplyKind.Scrap)); Fixed(conflict, C(0, 2), new SupplyItem(SupplyKind.FixedNormal));
                LevelRuntimeState unsupported = Build(conflict);
                typeof(RuntimeSource).GetField("<Mode>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(unsupported.Supply.Sources[1], SupplyMode.MaintainScrap);
                string original = Snapshot(unsupported);
                Check(SettlementResolution.Resolve(unsupported).Reason == SettlementReason.Unsupported && Snapshot(unsupported) == original, "강제 주입 혼용도 실행전 거절/무변경");
                File.WriteAllLines(Evidence + "/edge-results.txt", Results); EditorApplication.Exit(0);
            }
            catch (Exception error) { Results.Add("FAIL " + error); File.WriteAllLines(Evidence + "/edge-results.txt", Results); Debug.LogException(error); EditorApplication.Exit(1); }
        }
    }
}
