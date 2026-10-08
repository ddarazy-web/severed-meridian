using System;
using System.Linq;
using System.Reflection;
using Board;
using Levels;
using Levels.Editor;
using Simulation;
using UnityEngine;

namespace Tutorial.Editor
{
    public static partial class TutorialComposerConditionsVerification
    {
        private static void VerifyDamageOrigins()
        {
            PropertyInfo origin = typeof(EffectRecord).GetProperty("Origin");
            Check(origin != null, "피해 기록에 구체적인 단독/조합 공격 원인 존재");
            object Invoke(Type owner, string method, params object[] args) => owner.GetMethod(method,
                BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
            foreach (bool combined in new[] { false, true })
            {
                LevelDefinition level = combined
                    ? (LevelDefinition)Invoke(typeof(CombinationVerification), "Make", 1, RocketDirection.Horizontal, null)
                    : (LevelDefinition)Invoke(typeof(FixedObstacleVerification), "Make");
                try
                {
                    BoardCoordinate obstacle = combined ? new BoardCoordinate(3, 6) : new BoardCoordinate(4, 4);
                    int durability = combined ? 4 : 2;
                    Invoke(typeof(FixedObstacleVerification), "Obstacle", level, ObstacleKind.Appliance, durability, obstacle, RabbitColor.Type1);
                    Invoke(typeof(FixedObstacleVerification), "SetMission", level, ObstacleKind.Appliance);
                    if (!combined) Invoke(typeof(FixedObstacleVerification), "Place", level, new BoardCoordinate(4, 0), InitialBlockKind.Rocket, RocketDirection.Horizontal, RabbitColor.Type1);
                    LevelRuntimeState state = (LevelRuntimeState)Invoke(typeof(FixedObstacleVerification), "Build", level, 12345);
                    BoardActionExecutor executor = new BoardActionExecutor(state);
                    BoardActionResult action = combined ? executor.Swap(new BoardCoordinate(4, 4), new BoardCoordinate(4, 5)) : executor.Activate(new BoardCoordinate(4, 0));
                    Check(action.IsApplied, "원인 검사용 실제 파워 실행 " + combined);
                    EffectRecord[] hits = action.Effects.Where(value => value.Response == DamageResponse.Damage).ToArray();
                    Check(hits.Sum(value => value.DurabilityBefore - value.DurabilityAfter) == durability, "2×2 실제 감소량은 남은 내구도까지 " + combined);
                    Check(hits.All(value => origin.GetValue(value).ToString() == (combined ? "RocketBomb" : "Rocket")), "단독 로켓과 로켓+폭탄의 피해 원인은 분리 " + combined);
                    Check(hits.Count(value => value.DurabilityAfter == 0) == 1, "2×2 최종 제거 타격 한 번 " + combined);
                    object[] records = ((System.Collections.IEnumerable)typeof(TurnEffectContext).GetProperty("ElementRecords", BindingFlags.Instance | BindingFlags.NonPublic)
                        .GetValue(executor.TurnEffects)).Cast<object>().ToArray();
                    object[] damage = records.Where(value => value.GetType().GetProperty("Kind").GetValue(value).ToString() == "Damaged").ToArray();
                    Check(damage.Sum(value => (int)value.GetType().GetProperty("Before").GetValue(value) - (int)value.GetType().GetProperty("After").GetValue(value)) == durability,
                        "튜토리얼 사건 스트림에 실제 개체별 감소량 전달 " + combined);
                    object[] removed = records.Where(value => value.GetType().GetProperty("Kind").GetValue(value).ToString() == "Removed" &&
                        hits.Last().Target.Equals((BoardCoordinate)value.GetType().GetProperty("Coordinate").GetValue(value))).ToArray();
                    Check(removed.Any(value => value.GetType().GetProperty("Origin").GetValue(value).ToString() == (combined ? "RocketBomb" : "Rocket")),
                        "제거 사건은 마지막 타격 원인 보존 " + combined);
                    object[] bodyRemovals = records.Where(value => value.GetType().GetProperty("Kind").GetValue(value).ToString() == "Removed" &&
                        Equals(value.GetType().GetProperty("Occurrence").GetValue(value), damage[0].GetType().GetProperty("Occurrence").GetValue(damage[0]))).ToArray();
                    Check(bodyRemovals.All(value => value.GetType().GetProperty("EventCoordinate") != null &&
                        hits.Last().Target.Equals((BoardCoordinate)value.GetType().GetProperty("EventCoordinate").GetValue(value))),
                        "2×2 점유 좌표와 최종 제거 타격 위치를 분리 " + combined);
                }
                finally { UnityEngine.Object.DestroyImmediate(level); }
            }
            foreach (PowerCombinationKind kind in Enum.GetValues(typeof(PowerCombinationKind)))
            {
                LevelDefinition level = (LevelDefinition)Invoke(typeof(CombinationVerification), "Make", (int)kind, RocketDirection.Horizontal, null);
                try
                {
                    LevelRuntimeState state = (LevelRuntimeState)Invoke(typeof(CombinationVerification), "Build", level, 12345);
                    BoardActionExecutor executor = new BoardActionExecutor(state);
                    BoardActionResult action = executor.Swap(new BoardCoordinate(4, 4), new BoardCoordinate(4, 5));
                    Check(action.IsApplied && action.Effects.Count > 0 && action.Effects.All(value => value.Origin.ToString() == kind.ToString()),
                        "조합 자체의 변환·드론 착탄까지 같은 조합 원인 보존 " + kind);
                    object[] records = ((System.Collections.IEnumerable)typeof(TurnEffectContext).GetProperty("ElementRecords", BindingFlags.Instance | BindingFlags.NonPublic)
                        .GetValue(executor.TurnEffects)).Cast<object>().ToArray();
                    Check(records.Where(value => value.GetType().GetProperty("Kind").GetValue(value).ToString() == "Removed")
                        .All(value => value.GetType().GetProperty("Origin").GetValue(value).ToString() == kind.ToString()),
                        "조합 재료·변환 원본·효과 제거의 원인 기록 " + kind);
                    if (kind == PowerCombinationKind.MagnetRocket)
                    {
                        object[] generated = records.Where(value => value.GetType().GetProperty("Kind").GetValue(value).ToString() == "Generated").ToArray();
                        Check(generated.Length > 0 && generated.All(value => value.GetType().GetProperty("RocketDirection")?.GetValue(value) is RocketDirection),
                            "생성 직후 소모된 로켓도 생성 사건에 방향을 보존");
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(level); }
            }
            LevelDefinition chain = (LevelDefinition)Invoke(typeof(CombinationVerification), "Make", 1, RocketDirection.Horizontal, null);
            try
            {
                Invoke(typeof(FixedObstacleVerification), "Obstacle", chain, ObstacleKind.Appliance, 9, new BoardCoordinate(2, 7), RabbitColor.Type1);
                Invoke(typeof(FixedObstacleVerification), "SetMission", chain, ObstacleKind.Appliance);
                Invoke(typeof(FixedObstacleVerification), "Place", chain, new BoardCoordinate(4, 7), InitialBlockKind.Bomb, RocketDirection.Horizontal, RabbitColor.Type1);
                LevelRuntimeState state = (LevelRuntimeState)Invoke(typeof(FixedObstacleVerification), "Build", chain, 12345);
                BoardActionResult action = new BoardActionExecutor(state).Swap(new BoardCoordinate(4, 4), new BoardCoordinate(4, 5));
                EffectRecord[] hits = action.Effects.Where(value => value.Response == DamageResponse.Damage).ToArray();
                Check(action.IsApplied && hits.Any(value => value.Origin == EffectOrigin.RocketBomb) && hits.Any(value => value.Origin == EffectOrigin.Bomb),
                    "조합이 건드린 기존 폭탄의 후속 피해는 단독 폭탄 원인");
            }
            finally { UnityEngine.Object.DestroyImmediate(chain); }
        }
    }
}
