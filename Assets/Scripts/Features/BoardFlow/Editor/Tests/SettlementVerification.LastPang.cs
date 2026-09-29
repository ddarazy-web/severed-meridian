using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Board;
using Simulation;
using UnityEngine;

namespace Levels.Editor
{
    public static partial class SettlementVerification
    {
        /// <summary>실제 게임을 여러 판 돌리지 않고 공급 종류·색상·예약 보존을 작은 보드로 검사한다.</summary>
        public static void LastPangSupply()
        {
            Directory.CreateDirectory(Evidence); Results.Clear();
            try
            {
                MethodInfo resolve = typeof(SettlementResolution).GetMethod("Resolve", new[] { typeof(LevelRuntimeState), typeof(TurnEffectContext), typeof(bool) });
                Check(resolve != null, "라스트팡 전용 공급 진입점");
                BoardCoordinate[] column = Enumerable.Range(0, 10).Select(i => C(i, 0)).ToArray();
                foreach (SupplyKind kind in (SupplyKind[])Enum.GetValues(typeof(SupplyKind)))
                {
                    LevelDefinition level = Make(column);
                    try
                    {
                        JsonUtility.FromJsonOverwrite("{\"colors\":[0,1,2,3],\"initialBlocks\":[]}", level);
                        Source(level, C(0, 0), SupplyExhaustion.Stop, new SupplyItem(kind, 10));
                        LevelRuntimeState input = Build(level, 879610835); Empty(input, column);
                        string before = Snapshot(input);
                        SettlementResult result = (SettlementResult)resolve.Invoke(null, new object[] { input, null, true });
                        Check(result.IsApplied && result.State.Cells.Where(c => c.IsActive).All(c => c.Content == RuntimeContent.Normal), "일반 블록만 공급 " + kind);
                        Check(result.State.Supply.Sources[0].ItemIndex == 0 && result.State.Supply.Sources[0].ItemConsumed == 0, "예약 목록 미소비 " + kind);
                        Check(Snapshot(input) == before && input.Colors.Count == 4, "원본·레벨 색상 설정 보존 " + kind);
                        Check(result.State.Cells.Where(c => c.IsActive).Select(c => c.Color).Distinct().Count() == 5, "4종 레벨에서도 5종 공급 " + kind);
                        if (kind == SupplyKind.Rocket)
                            Check(SettlementResolution.Resolve(input).State.Cells.Where(c => c.IsActive).All(c => c.Content == RuntimeContent.Rocket), "일반 플레이의 로켓 예약 공급 유지");
                    }
                    finally { UnityEngine.Object.DestroyImmediate(level); }
                }
            }
            catch (Exception error) { Results.Add("FAIL " + error); }
            File.WriteAllLines(Evidence + "/last-pang-results.txt", Results);
        }

        public static void LastPangAndReplay()
        {
            LastPangSupply();
            if (Results.Any(r => r.StartsWith("FAIL"))) throw new InvalidOperationException("공급 검사 실패 · 결과 파일 확인");
            LastPangRecordDiagnostic.CheckUpdatedSupply();
        }
    }
}
