using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Board;
using Simulation;
using UnityEditor;

namespace Levels.Editor
{
    public static partial class SettlementVerification
    {
        public static void Supplemental()
        {
            Results.Clear();
            try
            {
                foreach (SupplyMode mode in new[] { SupplyMode.MaintainScrap, SupplyMode.MaintainRecovery })
                {
                    LevelDefinition level = Make(new[] { C(0, 0), C(1, 0) });
                    if (mode == SupplyMode.MaintainRecovery)
                    {
                        LevelFlowEditing.SetArrival(level, C(1, 0), false);
                        UnityEngine.JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":" + (int)MissionKind.Recovery + ",\"count\":2}]}", level);
                    }
                    LevelSupplyEditing.PlaceSources(level, new[] { C(0, 0) });
                    string error = LevelSupplyEditing.SetSourceProperty(level, new[] { 0 }, "mode", (int)mode);
                    if (error != null) throw new InvalidOperationException(error);
                    LevelRuntimeState state = Build(level); Empty(state, new[] { C(1, 0) }); string before = Snapshot(state);
                    Check(SettlementResolution.Resolve(state).IsApplied && Snapshot(state) == before, "고철·회수 유지 지원/원본 보존 " + mode);
                }
                LevelDefinition recovery = Make(new[] { C(0, 0), C(1, 0) });
                Source(recovery, C(0, 0), SupplyExhaustion.Stop, new SupplyItem(SupplyKind.FixedNormal), new SupplyItem(SupplyKind.Recovery));
                LevelRuntimeState unsupported = Build(recovery); Empty(unsupported, new[] { C(1, 0) }); string original = Snapshot(unsupported);
                Check(SettlementResolution.Resolve(unsupported).IsApplied && Snapshot(unsupported) == original, "뒤쪽 고정 회수 목록 지원/원본 보존");

                foreach (string kind in new[] { "Web", "Mold", "Dust", "Recovery" })
                {
                    LevelRuntimeState state = Build(Make(new[] { C(0, 0), C(1, 0) }));
                    if (kind == "Web" || kind == "Mold") Set(state.CellAt(C(0, 0)), "Cover", kind == "Web" ? CoverKind.Web : CoverKind.Mold);
                    else if (kind == "Dust") Set(state.CellAt(C(0, 0)), "DustDurability", 1);
                    else Set(state.CellAt(C(0, 0)), "Content", RuntimeContent.Recovery);
                    Empty(state, new[] { C(1, 0) }); string before = Snapshot(state);
                    SettlementResult layered = SettlementResolution.Resolve(state);
                    Check(layered.IsApplied && Snapshot(state) == before,
                        "거미줄/먼지/회수 정착 지원·원본 보존 " + kind);
                }

                LevelDefinition merge = Make(new[] { C(0, 0), C(1, 0), C(1, 1) });
                LevelFlowEditing.SetGravity(merge, new[] { C(1, 0) }, GravityDirection.Right);
                LevelFlowEditing.SetPortal(merge, C(0, 0), C(1, 1));
                LevelFlowEditing.SetMerge(merge, C(1, 1), new[] { C(0, 0), C(1, 0) });
                typeof(PowerEffectVerification).GetMethod("Crate", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { merge, C(0, 0), 2 });
                LevelRuntimeState blocked = Build(merge); Empty(blocked, new[] { C(1, 1) }); string flow = Snapshot(blocked.Flow);
                SettlementResult merged = SettlementResolution.Resolve(blocked);
                Check(merged.IsApplied && merged.Records.Single().Source.Equals(C(1, 0)) && merged.State.Obstacles[0].Durability == 2, "합류 우선 공급원 고정 상자이면 차선 이동");
                Check(Snapshot(merged.State.Flow) == flow && merged.State.Cells.Select(c => c.Gravity).SequenceEqual(blocked.Cells.Select(c => c.Gravity)), "이동 후 바닥/경로/통로/합류 설정 유지");

                LevelRuntimeState separate = Build(Make(new[] { C(0, 0), C(1, 1), C(0, 4), C(1, 5) }));
                Empty(separate, new[] { C(1, 1), C(1, 5) }); SettlementResult independent = SettlementResolution.Resolve(separate);
                Check(independent.IsApplied && independent.Records.Count == 2 && independent.Records.All(r => r.Kind == MovementKind.Diagonal && r.Batch == 1) && independent.RandomAfter == independent.RandomBefore && independent.Records.Select(r => r.Target).Distinct().Count() == 2, "독립 대각선 동시 묶음/중복 점유 없음/난수 무소비");

                BoardActionExecutor executor = new BoardActionExecutor(Build(FallingBoard()));
                Check(executor.Swap(C(3, 3), C(2, 3)).IsApplied, "조회 보존 검사용 실제 효과 완료");
                string beforeQuery = Snapshot(executor.State);
                bool[] protectedBefore = executor.State.Cells.Select(c => executor.TurnEffects.IsProtected(c.Coordinate)).ToArray();
                MovementQuery.Find(executor.State); MovementQuery.Find(executor.State, true);
                Check(Snapshot(executor.State) == beforeQuery && executor.State.Cells.Select(c => executor.TurnEffects.IsProtected(c.Coordinate)).SequenceEqual(protectedBefore) && executor.TurnEffects.HasFired(C(2, 3)), "효과 후 이동 조회는 난수/공급/보호/발동 기록 무변경");

                LevelDefinition random = Make(new[] { C(0, 0), C(1, 0) });
                Source(random, C(0, 0), SupplyExhaustion.Stop, new SupplyItem(SupplyKind.RandomNormal, 1));
                LevelRuntimeState empty = Build(random); Empty(empty, new[] { C(0, 0), C(1, 0) }); SettlementResult supplied = SettlementResolution.Resolve(empty);
                Check(supplied.IsApplied && supplied.RandomAfter - supplied.RandomBefore == 1 && supplied.State.Supply.Sources[0].ItemIndex == 1 && supplied.State.Supply.Sources[0].ItemConsumed == 0 && supplied.EmptyCells.Count == 1, "고정 목록 RandomNormal 수량/난수/Stop");
                File.WriteAllLines(Evidence + "/supplemental-results.txt", Results); EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Results.Add("FAIL " + error); File.WriteAllLines(Evidence + "/supplemental-results.txt", Results);
                UnityEngine.Debug.LogException(error); EditorApplication.Exit(1);
            }
        }
    }
}
