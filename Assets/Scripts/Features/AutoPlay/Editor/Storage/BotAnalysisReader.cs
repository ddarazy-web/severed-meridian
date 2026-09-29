using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using AutoPlay;
using Levels;
using Simulation;
using UnityEngine;

namespace Levels.Editor
{
    /// <summary>
    /// 한 번에 한 판 파일을 읽는 분석용 로더. 조회 중 원본 파일을 복구·저장하지 않는다.
    /// 판 파일이 요약보다 먼저 저장된 경우만 메모리에서 복구하고 차이를 사용자에게 알린다.
    /// </summary>
    internal sealed class BotAnalysisReader
    {
        private readonly string[] paths;
        private readonly List<BotGameSummary> games = new List<BotGameSummary>();
        private readonly int expectedRecorded;
        private readonly int[] expectedCounts;
        private readonly string[] gameHashes;
        internal string DirectoryPath { get; }
        internal BotBatchRecord Record { get; }
        internal IReadOnlyList<BotGameSummary> Games { get; }
        internal IReadOnlyList<LevelMissionDefinition> Missions { get; }
        internal int LevelNumber { get; }
        internal int InitialMoves { get; }
        internal string DefinitionIssue { get; }
        internal bool IsDone { get; private set; }
        internal string Error { get; private set; }
        internal string Warning { get; private set; }
        internal int FileCount => paths.Length;

        /// <param name="directory">사용자가 고른 기록 폴더. JSON 안의 경로는 사용하지 않는다.</param>
        internal BotAnalysisReader(string directory)
        {
            DirectoryPath = Path.GetFullPath(directory);
            if ((File.GetAttributes(DirectoryPath) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("연결된 폴더 대신 실제 기록 폴더를 선택하세요.");
            string header = Path.Combine(DirectoryPath, "batch.json");
            if ((File.GetAttributes(header) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("연결된 기록 파일은 열 수 없습니다.");
            Record = JsonUtility.FromJson<BotBatchRecord>(File.ReadAllText(header));
            if (Record == null || Record.formatVersion != 1 || !Guid.TryParseExact(Record.id, "N", out _) ||
                string.IsNullOrEmpty(Record.definitionJson) || string.IsNullOrEmpty(Record.fingerprint) ||
                Record.seeds == null || Record.seeds.Length < 1 || Record.seeds.Length > BotBatchSession.MaximumCount ||
                Record.seeds.Distinct().Count() != Record.seeds.Length || !Enum.IsDefined(typeof(BotBatchStatus), Record.status))
                throw new InvalidDataException("지원하지 않거나 손상된 시험 형식·시드입니다.");
            expectedCounts = new[] { Record.won, Record.exhausted, Record.blocked, Record.errors, Record.stopped };
            if (expectedCounts.Any(n => n < 0) || Record.finished < 0 || Record.basicFinished < 0 || Record.planningFinished < 0 ||
                Record.finished != (long)Record.won + Record.exhausted + Record.blocked || expectedCounts.Sum(n => (long)n) > Record.Total ||
                Record.basicFinished + (long)Record.planningFinished != Record.finished)
                throw new InvalidDataException("저장 요약의 수량이 일치하지 않습니다.");
            expectedRecorded = Record.Recorded;
            // 에셋을 만들지 않고 정의의 값만 읽는다. GUID가 없던 옛 기록도 내용 지문으로 확인한다.
            LevelDefinition definition = ScriptableObject.CreateInstance<LevelDefinition>();
            definition.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                JsonUtility.FromJsonOverwrite(Record.definitionJson, definition);
                if (LevelStateBuilder.Fingerprint(definition) != Record.fingerprint) throw new InvalidDataException("레벨 사본과 지문이 다릅니다.");
                Missions = Array.AsReadOnly((definition.Missions ?? Array.Empty<LevelMissionDefinition>()).ToArray());
                LevelNumber = definition.LevelNumber;
                InitialMoves = definition.MoveCount;
                // 통계는 과거 기록 그대로 읽되, 현재 정합성 규칙에 맞지 않는 사본의 추천은 보류한다.
                List<LevelValidationIssue> issues = LevelDefinitionValidator.Validate(definition);
                DefinitionIssue = issues.Count == 0 ? null : string.Join(" / ", issues.Select(issue => issue.ToString()));
            }
            finally { UnityEngine.Object.DestroyImmediate(definition); }
            paths = Directory.GetFiles(DirectoryPath, "*.json").Where(p => Path.GetFileName(p) != "batch.json").OrderBy(p => p, StringComparer.Ordinal).ToArray();
            gameHashes = new string[paths.Length];
            if (paths.Length > Record.Total || paths.Length < expectedRecorded) throw new InvalidDataException("판 기록의 수량이 요약과 맞지 않습니다.");
            Games = games.AsReadOnly();
        }

        /// <summary>UI 갱신 사이에 호출한다. 파일 하나를 읽고 행동 배열은 검증 후 버린다.</summary>
        internal void Advance()
        {
            if (IsDone) return;
            try
            {
                if (games.Count < paths.Length)
                {
                    int index = games.Count;
                    if (Path.GetFileName(paths[index]) != index.ToString("D6") + ".json") throw new InvalidDataException("판 파일에 누락·중복·순서 오류가 있습니다: " + index);
                    if (index > 0 && !games[index - 1].IsNormal) throw new InvalidDataException("오류 또는 중단 뒤에 추가 판이 기록되어 있습니다.");
                    games.Add(new BotGameSummary(ReadGame(index)));
                    return;
                }
                int[] actual = new[] { BotSessionStatus.Won, BotSessionStatus.MovesExhausted, BotSessionStatus.Blocked, BotSessionStatus.Error, BotSessionStatus.Stopped }
                    .Select(status => games.Count(g => g.Outcome == status)).ToArray();
                // 같은 수량인데 종료 종류가 달라졌으면 쓰기 사이의 정상적인 복구가 아니다.
                BotGameSummary[] prefix = games.Take(expectedRecorded).ToArray();
                int[] prefixCounts = new[] { BotSessionStatus.Won, BotSessionStatus.MovesExhausted, BotSessionStatus.Blocked, BotSessionStatus.Error, BotSessionStatus.Stopped }
                    .Select(status => prefix.Count(g => g.Outcome == status)).ToArray();
                if (!expectedCounts.SequenceEqual(prefixCounts) ||
                    Record.basicFinished != prefix.Count(g => g.IsNormal && g.Strategy == BotStrategyKind.Basic) ||
                    Record.planningFinished != prefix.Count(g => g.IsNormal && g.Strategy == BotStrategyKind.Planning))
                    throw new InvalidDataException("요약과 판별 종료 종류가 다릅니다.");
                Record.won = actual[0]; Record.exhausted = actual[1]; Record.blocked = actual[2]; Record.errors = actual[3]; Record.stopped = actual[4];
                Record.finished = actual[0] + actual[1] + actual[2];
                Record.basicFinished = games.Count(g => g.IsNormal && g.Strategy == BotStrategyKind.Basic);
                Record.planningFinished = games.Count(g => g.IsNormal && g.Strategy == BotStrategyKind.Planning);
                if (games.Count > expectedRecorded) Warning = "요약 저장 전 확정된 판을 포함해 다시 계산했습니다. 원본 파일은 변경하지 않았습니다.";
                if (Record.finished == Record.Total) Record.status = BotBatchStatus.Completed;
                else if (Record.errors > 0) Record.status = BotBatchStatus.Error;
                else if (Record.status == BotBatchStatus.Completed) throw new InvalidDataException("완료 표시와 정상 종료 판 수가 다릅니다.");
                IsDone = true;
            }
            catch (Exception error) { Error = error.Message; IsDone = true; }
        }

        /// <summary>사례 선택 시 행동열을 다시 읽는다. 외부 파일 변경도 이 경계에서 다시 검사한다.</summary>
        /// <param name="ordinal">목록에서 선택한 판 순번.</param><returns>검증된 한 판의 전체 기록.</returns>
        internal BotBatchGame ReadGame(int ordinal)
        {
            if (ordinal < 0 || ordinal >= paths.Length) throw new ArgumentOutOfRangeException(nameof(ordinal));
            if ((File.GetAttributes(paths[ordinal]) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("연결된 판 파일은 열 수 없습니다.");
            string json = File.ReadAllText(paths[ordinal]);
            using (SHA256 hash = SHA256.Create())
            {
                string current = Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(json)));
                if (gameHashes[ordinal] != null && gameHashes[ordinal] != current) throw new InvalidDataException("조회 후 판 파일이 변경되었습니다. 이력을 다시 여세요.");
                gameHashes[ordinal] = current;
            }
            BotBatchGame game = JsonUtility.FromJson<BotBatchGame>("{}");
            // FromJson의 누락 정수는 0이 된다. 덮어읽기 전 알 수 없음(-1)을 넣어 구기록을 구별한다.
            game.usedMoves = game.remainingMoves = -1;
            JsonUtility.FromJsonOverwrite(json, game);
            if (game == null || game.batchId != Record.id || game.ordinal != ordinal || game.seed != Record.seeds[ordinal / 2] ||
                game.strategy != (ordinal % 2 == 0 ? BotStrategyKind.Basic : BotStrategyKind.Planning) || game.actions == null || game.missions == null ||
                (game.outcome != BotSessionStatus.Won && game.outcome != BotSessionStatus.MovesExhausted && game.outcome != BotSessionStatus.Blocked &&
                game.outcome != BotSessionStatus.Error && game.outcome != BotSessionStatus.Stopped))
                throw new InvalidDataException("판 기록의 식별자·순서·종료 종류가 잘못되었습니다: " + ordinal);
            if (game.usedMoves < -1 || game.remainingMoves < -1 || game.missions.Any(m => m == null || m.remaining < 0 ||
                !Enum.IsDefined(typeof(MissionKind), m.kind) || !Enum.IsDefined(typeof(RabbitColor), m.color)))
                throw new InvalidDataException("판 기록의 이동 수·잔여 미션이 잘못되었습니다: " + ordinal);
            foreach (BotBatchAction action in game.actions)
                if (action == null || !Enum.IsDefined(typeof(BotActionKind), action.kind) || action.turn < 1 ||
                    action.movesBefore < 0 || action.movesAfter < 0 || action.randomBefore < 0 || action.randomAfter < action.randomBefore ||
                    (action.kind != BotActionKind.Activate) != action.hasSecond)
                    throw new InvalidDataException("행동 기록의 종류·이동 수·난수 소비가 잘못되었습니다: " + ordinal);
            return game;
        }
    }
}
