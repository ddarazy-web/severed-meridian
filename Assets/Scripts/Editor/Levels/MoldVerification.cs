using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Board;
using Simulation;
using UnityEditor;
using UnityEngine;

namespace Levels.Editor
{
    public static partial class MoldVerification
    {
        private const string Evidence = "Logs/MoldVerification";
        private static readonly List<string> Results = new List<string>();
        private static BoardCoordinate C(int r, int c) => new BoardCoordinate(r, c);
        private static object Invoke(Type type, string name, object owner, params object[] args)
            => type.GetMethod(name, BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, args);
        private static void Set(object owner, string name, object value) => owner.GetType().GetProperty(name).GetSetMethod(true).Invoke(owner, new[] { value });
        private static string Snapshot(object value) => (string)Invoke(typeof(LevelInitialStateVerification), "Snapshot", null, value);
        private static void Check(bool condition, string name) { if (!condition) throw new InvalidOperationException(name); Results.Add("PASS " + name); }
        private static LevelDefinition Make() => (LevelDefinition)Invoke(typeof(PowerEffectVerification), "Make", null);
        private static LevelRuntimeState Build(LevelDefinition level, int seed = 12345)
        {
            LevelStateBuildResult result = LevelStateBuilder.Build(level, seed);
            if (!result.IsBuilt) throw new InvalidOperationException(string.Join(" | ", result.Issues));
            return result.State;
        }
        private static TurnEffectContext Context(int turn = 1) => (TurnEffectContext)Activator.CreateInstance(typeof(TurnEffectContext), BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] { turn, Array.Empty<MatchedBlockChange>() }, null);
        private static void Mold(LevelDefinition level, params BoardCoordinate[] cells)
        {
            PlacementEditResult result = LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Cover, Kind = (int)CoverKind.Mold, Durability = 1 }, cells);
            if (result.Changed != cells.Length) throw new InvalidOperationException("곰팡이 배치 실패");
        }
        private static void Missions(LevelDefinition level) => JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":0,\"color\":0,\"count\":100},{\"kind\":8,\"count\":0}]}", level);
        private static List<EffectRecord> Hit(LevelRuntimeState state, BoardCoordinate target, TurnEffectContext context)
            => (List<EffectRecord>)Invoke(typeof(LayerVerification), "Hit", null, state, target, context);
        private static MoldSpreadRecord End(LevelRuntimeState state, TurnEffectContext context)
            => (MoldSpreadRecord)Invoke(typeof(BoardActionExecutor).Assembly.GetType("Simulation.MoldRules"), "FinishTurn", null, state, context);
        public static void Data()
        {
            Directory.CreateDirectory(Evidence); Results.Clear();
            try { DataChecks(); File.WriteAllLines(Evidence + "/data-results.txt", Results); EditorApplication.Exit(0); }
            catch (Exception error) { Results.Add("FAIL " + error); File.WriteAllLines(Evidence + "/data-results.txt", Results); Debug.LogException(error); EditorApplication.Exit(1); }
        }
        private static void DataChecks()
        {
            foreach (RuntimeContent content in new[] { RuntimeContent.Normal, RuntimeContent.Rocket, RuntimeContent.Bomb, RuntimeContent.Drone, RuntimeContent.Magnet })
            {
                LevelDefinition level = Make(); Missions(level);
                Invoke(typeof(PowerEffectVerification), "Place", null, level, C(4, 4), (InitialBlockKind)(int)content, RocketDirection.Vertical, RabbitColor.Type1);
                Mold(level, C(4, 4)); LevelRuntimeState state = Build(level); RuntimeCell cell = state.CellAt(C(4, 4)); Set(cell, "DustDurability", 2);
                TurnEffectContext context = Context(); string before = Snapshot(state);
                Check(!ActionQuery.Activate(state, C(4, 4)).IsAllowed && !ActionQuery.Swap(state, C(4, 4), C(4, 5)).IsAllowed && !MovementQuery.Find(state).Any(m => m.Source.Equals(C(4, 4)) && m.IsAllowed), "곰팡이 조작/이동 차단 " + content);
                Check(MissionProgressRules.Query(state, C(4, 4), context).Single().MissionIndex == 1 && Snapshot(state) == before, "숨은 색 기여 없음/곰팡이 기여/조회 무변경 " + content);
                Check(DamageReaction.Evaluate(state, C(4, 4), DamageCause.AdjacentMatch, C(4, 3), context).Response == DamageResponse.CoverDamage, "인접 매칭 곰팡이 제거 반응 " + content);
                List<EffectRecord> first = Hit(state, C(4, 4), context);
                Check(cell.Cover == null && cell.Content == content && cell.DustDurability == 2 && context.RemovedMold && state.Missions[1].Remaining == 0 && state.Missions[0].Progress == 0 && !first.Any(e => e.Response == DamageResponse.Activate), "한 타격 덮개만 제거/내용물·미션·먼지 " + content);
                Check(content != RuntimeContent.Rocket || cell.RocketDirection == RocketDirection.Vertical, "숨은 로켓 방향 보존 " + content);
                Check(End(state, context).Reason == MoldSpreadReason.RemovedThisTurn, "이번 수 제거 확산 생략 " + content);
                List<EffectRecord> second = Hit(state, C(4, 4), context);
                Check(cell.Content == RuntimeContent.Empty && (content == RuntimeContent.Normal ? cell.DustDurability == 1 : cell.DustDurability == 2), "별도 타격 내용물 처리 " + content);
                if (content != RuntimeContent.Normal) Check(second.Any(e => e.Response == DamageResponse.Activate && e.Target.Equals(C(4, 4))), "노출 파워 다음 타격 발동 " + content);
            }
            SpreadChecks(); MagnetChecks();
        }
        private static void SpreadChecks()
        {
            HashSet<BoardCoordinate> chosen = new HashSet<BoardCoordinate>();
            for (int seed = 1; seed <= 16; seed++)
            {
                LevelDefinition level = Make(); Missions(level); Mold(level, C(4, 4), C(4, 6));
                LevelRuntimeState state = Build(level, seed); TurnEffectContext context = Context(); string original = JsonUtility.ToJson(level);
                MoldSpreadRecord record = End(state, context); chosen.Add(record.Target.Value);
                Check(record.Reason == MoldSpreadReason.Spread && record.CandidateCount == 7 && state.Cells.Count(c => c.Cover == CoverKind.Mold) == 3 && state.Missions[1].Remaining == 3 && state.Missions[0].Progress == 0, "중복 후보 제거/보드1확산/동적 미션 " + seed);
                Check(record.RandomAfter == record.RandomBefore + 1 && JsonUtility.ToJson(level) == original, "복수 후보 난수1회/원본 보존 " + seed);
                string after = Snapshot(state); Check(ReferenceEquals(End(state, context), record) && Snapshot(state) == after, "같은 종료 재호출 무변경 " + seed);
                LevelRuntimeState replay = Build(level, seed); Check(Snapshot(End(replay, Context())) == Snapshot(record) && Snapshot(replay) == after, "같은 입력 확산 전체 재현 " + seed);
                Hit(state, record.Target.Value, context); Check(state.Missions[1].Remaining == 2 && state.Cells.Count(c => c.Cover == CoverKind.Mold) == 2, "확산분 제거 미션 갱신 " + seed);
            }
            Check(chosen.Count > 1, "복수 시드 확산 위치 변동");
            foreach (int count in new[] { 0, 1 })
            {
                LevelDefinition level = Make(); Missions(level); Mold(level, C(4, 4)); LevelRuntimeState state = Build(level);
                foreach (RuntimeCell cell in state.Cells.Where(c => c.Cover == null)) { Set(cell, "Content", RuntimeContent.Empty); Set(cell, "Color", null); }
                if (count == 1) { Set(state.CellAt(C(4, 5)), "Content", RuntimeContent.Rocket); Set(state.CellAt(C(4, 5)), "RocketDirection", RocketDirection.Vertical); Set(state.CellAt(C(4, 5)), "DustDurability", 3); }
                MoldSpreadRecord record = End(state, Context());
                Check(record.Reason == (count == 0 ? MoldSpreadReason.NoCandidate : MoldSpreadReason.Spread) && record.RandomBefore == record.RandomAfter, "후보0/1 무난수 " + count);
                if (count == 1) Check(state.CellAt(C(4, 5)).Content == RuntimeContent.Rocket && state.CellAt(C(4, 5)).RocketDirection == RocketDirection.Vertical && state.CellAt(C(4, 5)).DustDurability == 3, "파워 확산 내용물/방향/먼지 보존");
            }
            LevelDefinition completed = Make(); Mold(completed, C(4, 4)); LevelRuntimeState noMission = Build(completed); Set(noMission.Missions[0], "Progress", noMission.Missions[0].Target);
            Check(End(noMission, Context()).Reason == MoldSpreadReason.MissionsComplete && noMission.Cells.Count(c => c.Cover == CoverKind.Mold) == 1, "곰팡이 미션 없는 전체 달성 확산 생략");
            LevelRuntimeState initial = Build(completed); string saved = Snapshot(initial); Check(End(initial, Context(0)).Reason == MoldSpreadReason.NoTurn && Snapshot(initial) == saved, "초기 구성 이동수 소비없음 무확산");
            LevelRuntimeState clean = Build(Make()); Check(End(clean, Context()).Reason == MoldSpreadReason.NoMold, "곰팡이 없음 재생성 금지");
        }
        private static void MagnetChecks()
        {
            for (int pair = 0; pair < 10; pair++)
            {
                LevelDefinition level = (LevelDefinition)Invoke(typeof(CombinationVerification), "Make", null, pair, RocketDirection.Horizontal, null); Missions(level); Mold(level, C(4, 6));
                BoardActionExecutor executor = new BoardActionExecutor(Build(level)); Check(executor.Swap(C(4, 4), C(4, 5)).IsApplied, "파워 조합 곰팡이 지원 " + pair);
                if (pair >= 6 && pair <= 8) Check(executor.TurnEffects.Combination.Transformations.All(t => !t.Coordinate.Equals(C(4, 6))), "자석 색 변환 숨은 블록 제외 " + pair);
                if (pair == 9) Check(executor.State.CellAt(C(4, 6)).Cover == null && executor.State.CellAt(C(4, 6)).Content == RuntimeContent.Normal, "자석자석 곰팡이만 제거");
            }
            foreach (int pair in new[] { 6, 7, 8 })
            {
                LevelDefinition level = (LevelDefinition)Invoke(typeof(CombinationVerification), "Make", null, pair, RocketDirection.Horizontal, null);
                Mold(level, level.InitialBlocks.Where(b => b.Kind == InitialBlockKind.FixedNormal).Select(b => b.Coordinate).ToArray());
                LevelRuntimeState state = Build(level); string before = Snapshot(state);
                Check(!ActionQuery.Swap(state, C(4, 4), C(4, 5)).IsAllowed && Snapshot(state) == before, "숨은 색만 존재하면 색 조합 금지 " + pair);
            }
        }
    }
}
