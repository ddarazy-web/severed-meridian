using System;
using System.Collections.ObjectModel;
using System.Linq;
using Board;
using Levels;

namespace Simulation
{
    public sealed class StartConditionReport
    {
        public ReadOnlyCollection<MatchPattern> Matches { get; }
        public ReadOnlyCollection<ActionCandidate> Actions { get; }
        public int SwapCount => Actions.Count(action => action.Second.HasValue);
        public int ActivationCount => Actions.Count - SwapCount;
        public bool IsSatisfied => Matches.Count == 0 && SwapCount > 0;
        public string Message => IsSatisfied ? "시작 조건 통과" : Matches.Count > 0 ? "현재 후보 미충족: 완성 매칭 있음" : "현재 후보 미충족: 유효한 교환 없음";
        public StartConditionReport(LevelRuntimeState state) { Matches = MatchQuery.Find(state); Actions = ActionQuery.Find(state); }
    }

    public enum StartingBoardStatus { Searching, Success, DefinitionError, FixedMatch, FullyFixedFailure, LimitReached, Exhausted }

    // 한 번의 검색이 독립 후보/난수를 소유한다. Advance 호출 단위는 결과/방문 순서에 영향을 주지 않는다.
    public sealed class StartingBoardSearch
    {
        public const int DefaultLimit = 100000;
        public const string AlgorithmVersion = "starting-board-dfs-v1";
        private readonly LevelRuntimeState work;
        private readonly RuntimeCell[] variables;
        private readonly RabbitColor[][] choices;
        private readonly int[] next;
        private int depth;
        public StartingBoardStatus Status { get; private set; }
        public bool IsDone => Status != StartingBoardStatus.Searching;
        public LevelRuntimeState State => Status == StartingBoardStatus.Success ? work : null;
        public ReadOnlyCollection<LevelValidationIssue> Issues { get; }
        public int Attempts { get; private set; }
        public int Limit { get; }
        public int Seed { get; }
        public string DefinitionFingerprint { get; }
        public int RandomDrawCount => work?.Random.DrawCount ?? 0;
        public string Message => Status switch
        {
            StartingBoardStatus.Success => "시작 조건 통과", StartingBoardStatus.DefinitionError => "정의 오류 또는 실행 구성 미지원",
            StartingBoardStatus.FixedMatch => "고정 조건 위반: 변경 불가 칸만으로 완성 매칭이 있습니다.",
            StartingBoardStatus.FullyFixedFailure => "전체 고정 미충족: 바꿀 일반 색 칸이 없고 시작 조건을 만족하지 않습니다.",
            StartingBoardStatus.LimitReached => "탐색 한도 도달: 이 결과만으로 해결 불가능을 뜻하지 않습니다.",
            StartingBoardStatus.Exhausted => "전체 변수 할당 탐색 완료: 고정 조건 아래에서 시작 조건을 만족하는 구성이 없습니다.",
            _ => "시작 조건 구성 중"
        };

        public StartingBoardSearch(LevelDefinition definition, int seed, int limit = DefaultLimit)
        {
            if (limit < 0) throw new ArgumentOutOfRangeException(nameof(limit));
            Limit = limit; Seed = seed;
            LevelStateBuildResult built = LevelStateBuilder.Build(definition, seed); Issues = built.Issues;
            if (!built.IsBuilt) { Status = StartingBoardStatus.DefinitionError; return; }
            work = built.State; DefinitionFingerprint = work.DefinitionFingerprint;
            if (new StartConditionReport(work).IsSatisfied) { Status = StartingBoardStatus.Success; return; }
            BoardCoordinate[] fixedCells = work.InitialBlocks.Where(block => block.Kind == InitialBlockKind.FixedNormal).Select(block => block.Coordinate).ToArray();
            variables = work.Cells.Where(cell => cell.IsActive && cell.Content == RuntimeContent.Normal && !fixedCells.Contains(cell.Coordinate)).ToArray();
            if (variables.Length == 0) { Status = StartingBoardStatus.FullyFixedFailure; return; }
            foreach (RuntimeCell cell in variables) cell.Color = null;
            if (MatchQuery.Find(work).Count > 0) { Status = StartingBoardStatus.FixedMatch; return; }
            choices = new RabbitColor[variables.Length][]; next = new int[variables.Length];
            Status = StartingBoardStatus.Searching;
        }

        public void Advance(int assignmentBudget = 128)
        {
            if (assignmentBudget <= 0) throw new ArgumentOutOfRangeException(nameof(assignmentBudget));
            int assigned = 0;
            while (!IsDone && assigned < assignmentBudget)
            {
                if (depth == variables.Length)
                {
                    if (new StartConditionReport(work).IsSatisfied) { Status = StartingBoardStatus.Success; return; }
                    depth--; variables[depth].Color = null; continue;
                }
                if (choices[depth] == null)
                {
                    choices[depth] = work.Colors.ToArray();
                    for (int i = choices[depth].Length - 1; i > 0; i--)
                    { int j = work.Random.Next(i + 1); (choices[depth][i], choices[depth][j]) = (choices[depth][j], choices[depth][i]); }
                    next[depth] = 0;
                }
                if (next[depth] == choices[depth].Length)
                {
                    variables[depth].Color = null; choices[depth] = null;
                    if (depth == 0) { Status = StartingBoardStatus.Exhausted; return; }
                    depth--; variables[depth].Color = null; continue;
                }
                if (Attempts >= Limit) { Status = StartingBoardStatus.LimitReached; return; }
                variables[depth].Color = choices[depth][next[depth]++]; Attempts++; assigned++;
                if (MatchQuery.HasMatchAt(work, variables[depth].Coordinate)) variables[depth].Color = null;
                else depth++;
            }
        }
    }

    public static class StartingBoardBuilder
    {
        public static StartingBoardSearch Build(LevelDefinition definition, int seed, int limit = StartingBoardSearch.DefaultLimit)
        {
            StartingBoardSearch search = new StartingBoardSearch(definition, seed, limit);
            while (!search.IsDone) search.Advance();
            return search;
        }
    }
}
