using System.Collections.Generic;
using System.Linq;
using Simulation;
using UnityEngine;

namespace GameScreen
{
    internal sealed partial class PuzzlePowerPlayback
    {
        // 실제 재생 클립의 돌파 시각과 확정된 유효 반응만 소리에 전달한다.
        internal IEnumerable<PuzzleFeedbackCue> AudioCues
        {
            get
            {
                if (timeline.Changes.Any(change => change.IsConsumed)) yield return new PuzzleFeedbackCue(PuzzleFeedbackCueKind.Match, .12f);
                foreach (PuzzleEffectTimeline.Attack attack in timeline.Attacks)
                {
                    PowerAttackRecord record = attack.Record;
                    var valid = timeline.Reactions.Where(reaction => reaction.Record.HitGroup == record.HitGroup &&
                        (reaction.Record.Response == DamageResponse.Remove || reaction.Record.Response == DamageResponse.Damage ||
                         reaction.Record.Response == DamageResponse.Activate || reaction.Record.Response == DamageResponse.CoverDamage ||
                         reaction.Record.Response == DamageResponse.Charge)).ToArray();
                    float launch = attack.Start;
                    if (record.IsFlight)
                    {
                        Vector3 center = PuzzleWorldBoard.CellPosition(record.Center);
                        Clip flight = clips.FirstOrDefault(clip => clip.Label == "Drone-flight" && clip.Start == attack.Start && clip.To == center);
                        if (flight != null)
                        {
                            yield return new PuzzleFeedbackCue(PuzzleFeedbackCueKind.DroneDive, flight.Start);
                            launch = flight.End;
                        }
                        if (valid.Length > 0) yield return new PuzzleFeedbackCue(PuzzleFeedbackCueKind.DroneHit, valid.Min(reaction => reaction.Time));
                    }
                    if (valid.Length == 0) continue;
                    if (record.Area == PowerArea.Blast3 || record.Area == PowerArea.Blast5 || record.Power == RuntimeContent.Bomb)
                        yield return new PuzzleFeedbackCue(PuzzleFeedbackCueKind.Bomb, launch);
                    else if (record.Power == RuntimeContent.Magnet)
                        yield return new PuzzleFeedbackCue(PuzzleFeedbackCueKind.Magnet, valid.Min(reaction => reaction.Time));
                    else if (record.Power == RuntimeContent.Rocket || record.Area == PowerArea.Horizontal || record.Area == PowerArea.Vertical)
                        yield return new PuzzleFeedbackCue(PuzzleFeedbackCueKind.Rocket, launch + .06f);
                }
            }
        }
    }
}
