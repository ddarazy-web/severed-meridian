using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Simulation;
using UnityEditor;
using UnityEngine;

namespace Levels.Editor
{
    public static partial class FixedObstacleVerification
    {
        private static readonly List<string> BaselineRows = new List<string>();
        private static bool recordingBaseline;

        [Serializable]
        private sealed class BaselineObservation
        {
            public string name, action, unity, powerVersion, input, before, after;
            public int seed, movesRemaining, turn, lastHit;
            public BaselineBody[] bodies;
            public int[] missionProgress;
            public BaselineEffect[] effects;
        }

        [Serializable]
        private sealed class BaselineBody
        {
            public int index, kind, color, row, column, durability, charge;
            public Board.BoardCoordinate[] occupied;
        }

        [Serializable]
        private sealed class BaselineEffect
        {
            public int sourceRow, sourceColumn, targetRow, targetColumn, cause, response, hitGroup;
            public int durabilityBefore, durabilityAfter, coverBefore, coverAfter, dustBefore, dustAfter;
            public int[] removedBodies;
        }

        // 기존 피해 검사를 재사용하며 원본 에셋 대신 메모리 사례의 입력과 실제 결과만 기록한다.
        public static void Baseline()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("EF-01 검사는 별도 배치 에디터에서 실행하세요.");
            Directory.CreateDirectory(Evidence);
            Results.Clear(); BaselineRows.Clear(); recordingBaseline = true;
            try
            {
                DataChecks();
                MatchChecks();
                File.WriteAllLines(Evidence + "/baseline-results.txt", Results);
                // 범위 표와 점유/효과 검사는 기존 진입점을 그대로 실행한다. 이 메서드는 배치 종료를 수행한다.
                OverlapData();
            }
            catch (Exception error)
            {
                Results.Add("FAIL " + error);
                File.WriteAllLines(Evidence + "/baseline-results.txt", Results);
                File.WriteAllLines(Evidence + "/baseline-observations.jsonl", BaselineRows);
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }

        private static void ObserveBaseline(string name, LevelDefinition input, string before,
            LevelRuntimeState after, TurnEffectContext context, IEnumerable<EffectRecord> effects, string action)
        {
            if (!recordingBaseline) return;
            BaselineRows.Add(JsonUtility.ToJson(new BaselineObservation
            {
                name = name, seed = 12345, action = action, unity = Application.unityVersion,
                powerVersion = PowerEffectResolution.Version,
                input = JsonUtility.ToJson(input), before = before, after = Snapshot(after),
                bodies = after.Obstacles.Select((body, index) => new BaselineBody
                {
                    index = index, kind = (int)body.Definition.Kind, color = (int)body.Definition.Color,
                    row = body.Definition.Coordinate.Row, column = body.Definition.Coordinate.Column,
                    durability = body.Durability, charge = body.Charge,
                    occupied = after.Cells.Where(cell => cell.ObstacleIndex == index).Select(cell => cell.Coordinate).ToArray()
                }).ToArray(),
                missionProgress = after.Missions.Select(mission => mission.Progress).ToArray(),
                movesRemaining = after.MovesRemaining, turn = context.Turn, lastHit = context.LastHit,
                effects = effects.Select(effect => new BaselineEffect
                {
                    sourceRow = effect.Source.Row, sourceColumn = effect.Source.Column,
                    targetRow = effect.Target.Row, targetColumn = effect.Target.Column,
                    cause = (int)effect.Cause, response = (int)effect.Response,
                    hitGroup = effect.HitGroup, durabilityBefore = effect.DurabilityBefore, durabilityAfter = effect.DurabilityAfter,
                    coverBefore = effect.CoverBefore, coverAfter = effect.CoverAfter, dustBefore = effect.DustBefore, dustAfter = effect.DustAfter,
                    removedBodies = effect.RemovedObstacleIndices.ToArray()
                }).ToArray()
            }));
        }
    }
}
