using System;
using System.IO;
using System.Linq;
using Board;
using Simulation;
using UnityEditor;
using UnityEngine;

namespace Levels.Editor
{
    public static partial class SettlementVerification
    {
        private const string FreshEvidence = "Logs/FreshDiagonalVerification";

        [InitializeOnLoadMethod]
        private static void FreshSupplyRequest()
        {
            if (!File.Exists(FreshEvidence + "/request.txt")) return;
            string label = File.ReadAllText(FreshEvidence + "/request.txt").Trim();
            File.Delete(FreshEvidence + "/request.txt");
            EditorApplication.delayCall += () => VerifyFreshSupply(label);
        }

        // 사용자 씬·Play Mode·Editor 종료 상태를 바꾸지 않는 순수 규칙 검사다.
        [MenuItem("Tools/Match/신규 공급 대각선 규칙 검증")]
        public static void FreshSupply() => VerifyFreshSupply("manual");

        private static void VerifyFreshSupply(string label)
        {
            Directory.CreateDirectory(FreshEvidence); Results.Clear();
            try
            {
                LevelDefinition old = Make(new[] { C(0, 0), C(1, 1) });
                LevelRuntimeState existing = Build(old); Empty(existing, new[] { C(1, 1) });
                string unchanged = Snapshot(existing);
                SettlementResult held = SettlementResolution.Resolve(existing);
                Check(held.IsApplied && held.Records.Count == 0 && held.State.CellAt(C(0, 0)).Content == RuntimeContent.Normal,
                    "기존 블록은 대각선 빈칸으로 이동하지 않음");
                Check(Snapshot(existing) == unchanged && held.RandomAfter == held.RandomBefore, "기존 보드·난수 보존");
                UnityEngine.Object.DestroyImmediate(old);

                BoardCoordinate[] chain = { C(0, 0), C(1, 1), C(2, 2) };
                LevelDefinition supplied = Make(chain);
                Source(supplied, chain[0], SupplyExhaustion.Stop, new SupplyItem(SupplyKind.FixedNormal));
                LevelRuntimeState empty = Build(supplied); Empty(empty, chain);
                SettlementResult filled = SettlementResolution.Resolve(empty);
                Check(filled.IsApplied && filled.Records.Count(r => r.Kind == MovementKind.Supply) == 1 &&
                    filled.Records.Count(r => r.Kind == MovementKind.Diagonal) == 2 && filled.State.CellAt(chain[2]).Content == RuntimeContent.Normal,
                    "신규 공급 블록은 여러 Batch 대각선 이동 가능");
                LevelRuntimeState next = filled.State;
                Set(next.CellAt(chain[1]), "Content", RuntimeContent.Normal); Set(next.CellAt(chain[1]), "Color", RabbitColor.Type1);
                Empty(next, new[] { chain[2] });
                Check(SettlementResolution.Resolve(next).Records.Count == 0, "다음 채움에서는 이전 공급 블록도 기존 블록");
                UnityEngine.Object.DestroyImmediate(supplied);

                BoardCoordinate[] points = { C(1, 0), C(0, 1), C(1, 1), C(2, 1) };
                foreach (bool exhausted in new[] { false, true })
                {
                    LevelDefinition priority = Make(points);
                    Source(priority, C(1, 0), SupplyExhaustion.Stop, new SupplyItem(SupplyKind.FixedNormal, 1, RabbitColor.Type1));
                    Source(priority, C(0, 1), SupplyExhaustion.Stop, new SupplyItem(SupplyKind.FixedNormal, 1, RabbitColor.Type2));
                    LevelRuntimeState state = Build(priority); Empty(state, points);
                    if (exhausted) Set(state.Supply.Sources[1], "ItemIndex", 1);
                    SettlementResult result = SettlementResolution.Resolve(state);
                    Check(result.IsApplied && result.State.CellAt(C(2, 1)).Color == (exhausted ? RabbitColor.Type1 : RabbitColor.Type2),
                        "상단 공급 예상 우선·소진 시에만 대각선 대체 " + exhausted);
                    Check(exhausted == result.Records.Any(r => r.Kind == MovementKind.Diagonal), "멀리 있는 상단 공급 도착 전 대각선 선점 금지 " + exhausted);
                    Check(Snapshot(result.State) == Snapshot(SettlementResolution.Resolve(state).State), "같은 시드 반복 결과 동일 " + exhausted);
                    UnityEngine.Object.DestroyImmediate(priority);
                }
                LevelDefinition queued = Make(new[] { C(0, 0), C(0, 1), C(1, 1), C(2, 1) });
                Source(queued, C(0, 0), SupplyExhaustion.Stop, new SupplyItem(SupplyKind.FixedNormal, 1, RabbitColor.Type1));
                Source(queued, C(0, 1), SupplyExhaustion.Stop, new SupplyItem(SupplyKind.FixedNormal, 2, RabbitColor.Type2));
                LevelRuntimeState waiting = Build(queued); Empty(waiting, waiting.Cells.Where(c => c.IsActive).Select(c => c.Coordinate));
                SettlementResult ordered = SettlementResolution.Resolve(waiting);
                Check(ordered.IsApplied && !ordered.Records.Any(r => r.Kind == MovementKind.Diagonal) && ordered.State.CellAt(C(1, 1)).Color == RabbitColor.Type2,
                    "앞선 공급이 도착한 뒤 다음 상단 공급을 기다려 대각선 선점 방지");
                UnityEngine.Object.DestroyImmediate(queued);
                DataChecks();
            }
            catch (Exception error) { Results.Add("FAIL " + error); }
            File.WriteAllLines(FreshEvidence + "/" + label + "-results.txt", Results);
        }
    }
}
