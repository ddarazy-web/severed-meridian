using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Board;
using Simulation;
using UnityEditor;

namespace Levels.Editor
{
    public static partial class CascadeVerification
    {
        public static void Supplemental()
        {
            Results.Clear();
            try
            {
                Dictionary<BoardCoordinate, int> colors = new Dictionary<BoardCoordinate, int>
                {
                    [C(0,0)]=0, [C(0,1)]=1, [C(0,2)]=0, [C(1,1)]=0,
                    [C(5,5)]=0, [C(5,6)]=1, [C(5,7)]=0, [C(6,6)]=0
                };
                LevelDefinition level = (LevelDefinition)Invoke(typeof(BoardActionVerification), "Make", null, colors, 20);
                BoardActionExecutor executor = new BoardActionExecutor(Build(level));
                Check(executor.Swap(C(0,1),C(1,1)).IsApplied, "일반 교환 첫 수"); Finish(executor);
                Check(executor.Phase == BoardActionPhase.Ready && executor.Swap(C(5,6),C(6,6)).IsApplied && executor.Turn == 2 && executor.State.MovesRemaining == 18, "정착 후 일반 교환 다음 수/빈칸 허용/턴 1회 증가"); Finish(executor);

                BoardActionExecutor tied = Automatic(Make(Enumerable.Range(0,4).Select(c=>C(0,c))));
                Arrive(tied.TurnEffects,C(0,1),2,3); Arrive(tied.TurnEffects,C(0,3),2,3);
                string beforeQuery = Context(tied); MatchQuery.Find(tied.State);
                Check(Context(tied)==beforeQuery, "자동 매칭 조회는 도착/보호/난수 무변경");
                CascadeStepResult result=tied.ResolveAutomaticMatch();
                Check(result.IsApplied && (result.Decisions[0].Spawn.Value.Equals(C(0,1)) || result.Decisions[0].Spawn.Value.Equals(C(0,3))) && result.RandomAfter==result.RandomBefore+1, "같은 회차/묶음의 최후 도착 2칸 중에서만 생성");

                BoardCoordinate[] pairs=Enumerable.Range(0,4).Select(c=>C(0,c)).Concat(Enumerable.Range(0,4).Select(c=>C(5,c))).ToArray();
                BoardActionExecutor independent=Automatic(Make(pairs));
                Arrive(independent.TurnEffects,C(0,2),1,2); Arrive(independent.TurnEffects,C(5,3),1,4);
                CascadeStepResult both=independent.ResolveAutomaticMatch();
                Check(both.Decisions.Count==2 && independent.TurnEffects.IsProtected(C(0,2)) && independent.TurnEffects.IsProtected(C(5,3)) && both.RandomAfter==both.RandomBefore, "독립 자동 파워 패턴 각각 생성/보호");
                File.WriteAllLines(Evidence+"/supplemental-results.txt",Results); EditorApplication.Exit(0);
            }
            catch(Exception error)
            {
                Results.Add("FAIL "+error); File.WriteAllLines(Evidence+"/supplemental-results.txt",Results);
                UnityEngine.Debug.LogException(error); EditorApplication.Exit(1);
            }
        }
    }
}
