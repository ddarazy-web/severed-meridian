using System;
using System.IO;
using System.Linq;
using Board;
using Simulation;
using UnityEditor;
using UnityEngine;

namespace Levels.Editor
{
    public static partial class MoldVerification
    {
        public static void Edges()
        {
            Results.Clear();
            try
            {
                LevelDefinition adjacentLevel=Make(); Mold(adjacentLevel,C(4,4));
                LevelRuntimeState adjacentState=Build(adjacentLevel); TurnEffectContext adjacentContext=Context();
                Hit(adjacentState,C(4,3),adjacentContext);
                Check(adjacentState.CellAt(C(4,3)).Content==RuntimeContent.Empty && adjacentState.CellAt(C(4,4)).Cover==CoverKind.Mold && !adjacentContext.RemovedMold,"파워의 옆 일반 블록 제거는 곰팡이에 인접 매칭 피해를 주지 않음");
                BoardActionExecutor invalid=new BoardActionExecutor(Build(adjacentLevel)); string invalidBefore=Snapshot(invalid.State);
                Check(!invalid.Swap(C(4,4),C(4,5)).IsApplied && !invalid.Activate(C(4,4)).IsApplied && Snapshot(invalid.State)==invalidBefore && invalid.Turn==0 && invalid.TurnEffects==null,"곰팡이 무효 입력은 턴/확산/난수 상태 미변경");
                foreach(bool exposed in new[] { false,true })
                {
                    LevelDefinition level=Make(); Invoke(typeof(PowerEffectVerification),"Place",null,level,C(4,4),InitialBlockKind.Magnet,RocketDirection.Horizontal,RabbitColor.Type1);
                    Mold(level,level.InitialBlocks.Where(b=>b.Kind==InitialBlockKind.FixedNormal && (!exposed || !b.Coordinate.Equals(C(0,0)))).Select(b=>b.Coordinate).ToArray());
                    LevelRuntimeState state=Build(level); TurnEffectContext context=Context();
                    string hidden=string.Join("|",state.Cells.Where(c=>c.Cover==CoverKind.Mold).Select(Snapshot));
                    Hit(state,C(4,4),context);
                    TargetingRecord color=context.Targeting.Single(r=>r.Event==TargetingEvent.ColorSelected);
                    Check(color.Color==(exposed?(RabbitColor?)RabbitColor.Type1:null) && state.CellAt(C(4,4)).Content==RuntimeContent.Empty,"실제 피격 자석은 노출 색만 선택/없으면 소모 " + exposed);
                    Check(string.Join("|",state.Cells.Where(c=>c.Cover==CoverKind.Mold).Select(Snapshot))==hidden,"자석 색 제거로 숨은 덮개도 벗기지 않음 " + exposed);
                }
                foreach(int pair in new[]{6,7,8})
                {
                    LevelDefinition level=(LevelDefinition)Invoke(typeof(CombinationVerification),"Make",null,pair,RocketDirection.Horizontal,null);
                    Mold(level,level.InitialBlocks.Where(b=>b.Kind==InitialBlockKind.FixedNormal && !b.Coordinate.Equals(C(0,0))).Select(b=>b.Coordinate).ToArray());
                    LevelRuntimeState state=Build(level); BoardActionExecutor executor=new BoardActionExecutor(state);
                    Check(executor.Swap(C(4,4),C(4,5)).IsApplied && executor.TurnEffects.Combination.Color==state.CellAt(C(0,0)).Color && executor.TurnEffects.Combination.Transformations.Count==1 && executor.TurnEffects.Combination.Transformations[0].Coordinate.Equals(C(0,0)),"실제 색 조합 후보/변환 모두 노출 한 칸 " + pair);
                }
                LevelDefinition levelEnd=Make(); Mold(levelEnd,C(4,4));
                BoardActionExecutor failed=new BoardActionExecutor(Build(levelEnd));
                Set(failed,"Phase",BoardActionPhase.WaitingForAutomaticMatch); Set(failed,"TurnEffects",Context()); Set(failed,"Turn",1);
                RuntimeCell malformed=failed.State.CellAt(C(0,1)); Set(malformed,"Content",RuntimeContent.Obstacle);Set(malformed,"Color",null);Set(malformed,"ObstacleIndex",999);
                // 잘못된 인덱스를 주입한 상태에서는 파생 조회 대신 원시 상태를 비교한다.
                string FailureSnapshot() => Snapshot(failed.State.Cells)+Snapshot(failed.State.Obstacles)+Snapshot(failed.State.Missions)+Snapshot(failed.State.Supply)+Snapshot(failed.State.Random)+ContextSnapshot(failed);
                string before=FailureSnapshot(); bool rejected=false;
                try { failed.ResolveAutomaticMatch(); } catch(ArgumentOutOfRangeException) { rejected=true; }
                Check(rejected && FailureSnapshot()==before && failed.Phase==BoardActionPhase.WaitingForAutomaticMatch,"확산 작업 뒤 행동 조회 예외에서도 원본/난수/턴 기록 미반영");
                LevelDefinition isolated=(LevelDefinition)Invoke(typeof(BoardActionVerification),"Make",null,new System.Collections.Generic.Dictionary<BoardCoordinate,int>{[C(0,0)]=0,[C(0,1)]=1},20);Mold(isolated,C(0,0));
                BoardActionExecutor noActions=new BoardActionExecutor(Build(isolated)); Set(noActions,"Phase",BoardActionPhase.WaitingForAutomaticMatch);Set(noActions,"TurnEffects",Context());Set(noActions,"Turn",1);
                CascadeStepResult ending=noActions.ResolveAutomaticMatch();
                Check(ending.Reason==CascadeStepReason.NeedsShuffle && noActions.TurnEffects.MoldSpread.Reason==MoldSpreadReason.Spread && noActions.State.Cells.Count(c=>c.Cover==CoverKind.Mold)==2,"확산 결과로 행동 없음 판정/재배치 필요 유지");
                LevelDefinition fall=(LevelDefinition)Invoke(typeof(BoardActionVerification),"Make",null,new System.Collections.Generic.Dictionary<BoardCoordinate,int>{[C(0,0)]=2,[C(1,0)]=1,[C(2,0)]=0},20);Mold(fall,C(0,0));
                LevelRuntimeState falling=Build(fall); foreach(RuntimeCell c in falling.Cells.Where(c=>c.IsActive && c.Coordinate.Row>0)){Set(c,"Content",RuntimeContent.Empty);Set(c,"Color",null);}
                Check(SettlementResolution.Resolve(falling).State.CellAt(C(0,0)).Cover==CoverKind.Mold,"정착 중 곰팡이 고정");Hit(falling,C(0,0),Context());
                SettlementResult result=SettlementResolution.Resolve(falling);
                Check(result.IsApplied && result.State.CellAt(C(2,0)).Color==RabbitColor.Type3,"노출 후 실제 낙하/색 보존");
                File.WriteAllLines(Evidence+"/edge-results.txt",Results);EditorApplication.Exit(0);
            }
            catch(Exception error){Results.Add("FAIL "+error);File.WriteAllLines(Evidence+"/edge-results.txt",Results);Debug.LogException(error);EditorApplication.Exit(1);}
        }
    }
}
