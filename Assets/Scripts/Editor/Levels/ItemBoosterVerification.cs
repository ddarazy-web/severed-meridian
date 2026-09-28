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
    public static partial class ItemBoosterVerification
    {
        private const string Evidence = "Logs/ItemBoosterVerification";
        private static readonly List<string> Results = new List<string>();
        private static BoardCoordinate C(int row, int column) => new BoardCoordinate(row, column);
        private static object Invoke(Type type, string method, params object[] args) => type.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, args);
        private static string Snapshot(object value) => (string)Invoke(typeof(LevelInitialStateVerification), "Snapshot", value);
        private static void Set(object owner, string property, object value) => owner.GetType().GetProperty(property).GetSetMethod(true).Invoke(owner, new[] { value });
        private static void Check(bool pass, string name) { if (!pass) throw new InvalidOperationException(name); Results.Add("PASS " + name); }
        private static LevelRuntimeState Build(LevelDefinition level) => LevelStateBuilder.Build(level, 12345).State;
        private static void Finish(BoardActionExecutor executor)
        {
            int count = 0;
            while (executor.HasPendingCascade && count++ < 1000) executor.AdvanceCascade();
            Check(!executor.HasPendingCascade, "아이템 후속 처리 종료");
        }
        public static void Data()
        {
            Directory.CreateDirectory(Evidence); Results.Clear();
            try { DataChecks(); File.WriteAllLines(Evidence + "/data-results.txt", Results); EditorApplication.Exit(0); }
            catch (Exception error) { Results.Add("FAIL " + error); File.WriteAllLines(Evidence + "/data-results.txt", Results); Debug.LogException(error); EditorApplication.Exit(1); }
        }
        private static void DataChecks()
        {
            LevelDefinition level = (LevelDefinition)Invoke(typeof(PowerEffectVerification), "Make");
            LevelRuntimeState raw = Build(level);
            Set(raw.CellAt(C(0, 0)), "DustDurability", 3);
            BoardActionExecutor hammer = new BoardActionExecutor(raw);
            ItemUseResult hit = hammer.UseItem(BoardItem.Hammer, C(0, 0));
            Check(hit.IsApplied && hammer.State.CellAt(C(0, 0)).Content == RuntimeContent.Empty && hammer.State.CellAt(C(0, 0)).DustDurability == 3, "망치 일반 제거·먼지 직접 피해 없음");
            Check(hammer.State.Missions[0].Progress == 1 && hammer.Turn == 1 && hammer.State.MovesRemaining == 20 && !hammer.TurnEffects.ConsumesMove, "망치 수집·독립 턴·이동 무소모");
            string pending = Snapshot(hammer.State);
            Check(!hammer.UseItem(BoardItem.Shuffle).IsApplied && Snapshot(hammer.State) == pending && hammer.ItemUses.Count == 1, "후속 처리 중 중복 사용 거절");
            Finish(hammer); Check(hammer.TurnEffects.MoldSpread.Reason == MoldSpreadReason.NoTurn, "아이템 턴 확산 생략 문맥 유지");

            foreach (CoverKind cover in new[] { CoverKind.Web, CoverKind.Mold })
            {
                LevelRuntimeState state = Build(level); RuntimeCell cell = state.CellAt(C(0, 0));
                Set(cell, "Content", RuntimeContent.Rocket); Set(cell, "Color", null); Set(cell, "RocketDirection", (RocketDirection?)RocketDirection.Horizontal);
                Set(cell, "Cover", (CoverKind?)cover); Set(cell, "CoverDurability", 1);
                BoardActionExecutor covered = new BoardActionExecutor(state);
                Check(covered.UseItem(BoardItem.Hammer, C(0, 0)).IsApplied && covered.State.CellAt(C(0, 0)).Content == RuntimeContent.Rocket &&
                    !covered.State.CellAt(C(0, 0)).Cover.HasValue && !covered.TurnEffects.HasFired(C(0, 0)), "망치 덮개만 제거·내부 파워 보존 " + cover);
            }
            LevelRuntimeState powered = Build(level);
            Set(powered.CellAt(C(0, 0)), "Content", RuntimeContent.Rocket); Set(powered.CellAt(C(0, 0)), "Color", null);
            Set(powered.CellAt(C(0, 0)), "RocketDirection", (RocketDirection?)RocketDirection.Horizontal);
            Set(powered.CellAt(C(0, 1)), "DustDurability", 2);
            BoardActionExecutor powerHit = new BoardActionExecutor(powered);
            Check(powerHit.UseItem(BoardItem.Hammer, C(0, 0)).Effects.Any(e => e.Response == DamageResponse.Activate) && powerHit.State.CellAt(C(0, 1)).DustDurability == 1,
                "망치 파워 발동·파워의 먼지 피해는 정상");

            BoardActionExecutor swap = new BoardActionExecutor(Build(level));
            RabbitColor? first = swap.State.CellAt(C(0, 0)).Color, second = swap.State.CellAt(C(0, 1)).Color;
            Check(swap.UseItem(BoardItem.Swap, C(0, 0), C(0, 1)).IsApplied && swap.State.CellAt(C(0, 0)).Color == second &&
                swap.State.CellAt(C(0, 1)).Color == first && swap.State.MovesRemaining == 20, "매칭 없는 자리 바꾸기 유지·무소모");
            BoardActionExecutor invalid = new BoardActionExecutor(Build(level)); string invalidBefore = Snapshot(invalid.State);
            Check(!invalid.UseItem(BoardItem.Swap, C(0, 0), C(8, 8)).IsApplied && !invalid.UseItem(BoardItem.Hammer, C(-1, 0)).IsApplied &&
                Snapshot(invalid.State) == invalidBefore && invalid.ItemUses.Count == 0 && invalid.Turn == 0, "잘못된 대상은 보드·난수·턴·사용 기록 무변경");
            LevelRuntimeState twoPowers = Build(level);
            Set(twoPowers.CellAt(C(0, 0)), "Content", RuntimeContent.Bomb); Set(twoPowers.CellAt(C(0, 0)), "Color", null);
            Set(twoPowers.CellAt(C(0, 1)), "Content", RuntimeContent.Magnet); Set(twoPowers.CellAt(C(0, 1)), "Color", null);
            BoardActionExecutor powerSwap = new BoardActionExecutor(twoPowers);
            ItemUseResult pair = powerSwap.UseItem(BoardItem.Swap, C(0, 0), C(0, 1));
            Check(pair.IsApplied && pair.Effects.Count == 0 && powerSwap.TurnEffects.Combination == null && powerSwap.State.CellAt(C(0, 0)).Content == RuntimeContent.Magnet,
                "자리 바꾸기 파워끼리도 위치만 교환");
            BoardActionExecutor shuffled = new BoardActionExecutor(Build(level));
            Check(shuffled.UseItem(BoardItem.Shuffle).IsApplied && shuffled.State.MovesRemaining == 20 && MatchQuery.Find(shuffled.State).Count == 0 &&
                ActionQuery.Find(shuffled.State).Count > 0 && shuffled.Phase == BoardActionPhase.WaitingForAutomaticMatch, "아이템 섞기 공통 조건·낙하 없이 안정 판정 대기");
            Finish(shuffled); Check(shuffled.TurnEffects.MoldSpread.Reason == MoldSpreadReason.NoTurn, "섞기도 곰팡이 확산 없음");

            StartBooster[] selection = { StartBooster.Magnet, StartBooster.Bomb, StartBooster.Rocket };
            LevelRuntimeState boosterSource = Build(level); string boosterBefore = Snapshot(boosterSource);
            BoardActionExecutor boosters = new BoardActionExecutor(boosterSource, selection);
            Check(boosters.PendingBoosters.Count == 0 && boosters.BoosterPlacements.Select(p => p.Booster).SequenceEqual(selection.Reverse()), "부스터 선택 순서와 무관하게 로켓·폭탄·자석 배치");
            Check(boosters.BoosterPlacements.Select(p => p.Coordinate).Distinct().Count() == 3 && boosters.State.MovesRemaining == 20 && boosters.Turn == 0 &&
                boosters.State.Missions[0].Progress == 0 && Snapshot(boosterSource) == boosterBefore, "부스터 서로 다른 칸·원본·미션·이동 보존");
            BoardActionExecutor repeat = new BoardActionExecutor(boosterSource, selection);
            Check(Snapshot(boosters.State) == Snapshot(repeat.State) && Snapshot(boosters.BoosterPlacements) == Snapshot(repeat.BoosterPlacements), "부스터 위치·방향·난수 동일 시드 재현");
            LevelRuntimeState scarce = Build(level);
            foreach (RuntimeCell cell in scarce.Cells.Skip(1)) { Set(cell, "Content", RuntimeContent.Empty); Set(cell, "Color", null); }
            BoardActionExecutor queued = new BoardActionExecutor(scarce, selection);
            Check(queued.BoosterPlacements.Count == 1 && queued.PendingBoosters.SequenceEqual(new[] { StartBooster.Bomb, StartBooster.Magnet }), "후보 부족 부스터 순서 대기");
            foreach (RuntimeMission mission in scarce.Missions) Set(mission, "Progress", mission.Target);
            BoardActionExecutor won = new BoardActionExecutor(scarce, selection); Finish(won);
            Check(won.Outcome.Kind == BoardOutcomeKind.Won && won.BoosterPlacements.Count == 0 && won.PendingBoosters.Count == 3, "종료 우선·대기 부스터 미사용");
            Check(!won.UseItem(BoardItem.Hammer, C(0, 0)).IsApplied, "종료 후 아이템 차단");
            UnityEngine.Object.DestroyImmediate(level);
        }
    }
}
