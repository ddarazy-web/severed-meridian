using System.Linq;
using Board;
using Levels;
using Simulation;
using UnityEngine;

namespace GameScreen
{
    internal sealed partial class PuzzlePowerPlayback
    {
        // 효과 대상은 규칙이 확정한 기록에서만 가져온다.
        private void BuildClips(LevelRuntimeState before)
        {
            string[] colors = { "pink", "yellow", "blue", "green", "purple" };
            foreach (MatchedBlockChange change in timeline.Changes)
            {
                Vector3 position = PuzzleWorldBoard.CellPosition(change.Coordinate);
                if (change.IsConsumed)
                    AddEffect("Match", "match-" + colors[(int)change.OriginalColor], position, position, 0, .2f, 1.1f);
                if (change.IsTransformation)
                    AddEffect("PowerCreation", "power-creation", position, position, .12f, .2f, 1.3f);
                // 직접 매칭의 레이어 변화는 EffectRecord가 아닌 MatchedBlockChange에 남는다.
                if (change.CoverBefore > change.CoverAfter)
                    AddEffect("WebBreak", "web-break", position, position, .12f, .2f, 1.1f);
                if (change.IsConsumed && before.CellAt(change.Coordinate).DustDurability > final.CellAt(change.Coordinate).DustDurability &&
                    !timeline.Reactions.Any(reaction => reaction.Record.Target.Equals(change.Coordinate) && reaction.Record.DustBefore > reaction.Record.DustAfter))
                    AddEffect("DustClear", "dust-clear", position, position, .12f, .2f, 1.1f);
            }
            if (timeline.Combination?.IsTransformation == true)
                foreach (PowerTransformation transformation in timeline.Combination.Transformations)
                {
                    Vector3 position = PuzzleWorldBoard.CellPosition(transformation.Coordinate);
                    AddEffect("Magnet", "magnet-transform", position, position, 0, .35f, 1.2f);
                }
            foreach (DroneFlightMotion flight in timeline.Flights)
                foreach (DroneFlightMotion.Phase phase in flight.Phases)
                    clips.Add(new Clip { Label = phase.Kind == DroneFlightPhaseKind.Dash ? "Drone-flight" : "Drone-hover",
                        Paths = new[] { "PowerBlocks/collection-drone-rotor-4frames-v1" },
                        Start = phase.Start, End = phase.End, From = phase.From, To = phase.To,
                        Size = .92f, Sheet = true, DronePhase = phase });
            foreach (PuzzleEffectTimeline.Attack attack in timeline.Attacks)
            {
                PowerAttackRecord record = attack.Record;
                Vector3 origin = PuzzleWorldBoard.CellPosition(record.Origin);
                Vector3 center = PuzzleWorldBoard.CellPosition(record.Center);
                float start = attack.Start;
                if (record.IsFlight)
                {
                    float flight = attack.FlightDuration;
                    AddEffect("Drone", "drone-impact", center, center, start + flight, .2f, 1.3f);
                    start += flight;
                }
                if (record.Area == PowerArea.Blast3 || record.Area == PowerArea.Blast5 || record.Power == RuntimeContent.Bomb)
                    AddEffect("BombExplosion", "bomb-explosion", center, center, start, .24f,
                        record.Area == PowerArea.Blast5 ? 5 : 3, 8);
                else if (record.Power == RuntimeContent.Magnet)
                {
                    foreach (BoardCoordinate target in record.Targets)
                    {
                        Vector3 position = PuzzleWorldBoard.CellPosition(target);
                        AddEffect("Magnet", "magnet-pull", position, center, start,
                            attack.ImpactAt(target) - start, .9f);
                    }
                }
                else if (record.Power == RuntimeContent.Rocket || record.Area == PowerArea.Horizontal || record.Area == PowerArea.Vertical)
                    BuildRocketClips(attack, start, center);
                else if (!record.IsFlight)
                    foreach (BoardCoordinate target in record.Targets)
                    {
                        Vector3 position = PuzzleWorldBoard.CellPosition(target);
                        AddEffect("Drone", "drone-impact", position, position, attack.ImpactAt(target), .2f, 1.1f);
                    }
            }
            foreach (PuzzleEffectTimeline.Reaction reaction in timeline.Reactions)
            {
                EffectRecord record = reaction.Record;
                Vector3 position = PuzzleWorldBoard.CellPosition(record.Target);
                if (record.CoverAfter < record.CoverBefore)
                {
                    bool mold = before.CellAt(record.Target).Cover == CoverKind.Mold;
                    AddEffect(mold ? "MoldClear" : "WebBreak", mold ? "mold-clear" : "web-break", position, position, reaction.Time, .2f, 1.1f);
                }
                if (record.DustAfter < record.DustBefore)
                    AddEffect("DustClear", "dust-clear", position, position, reaction.Time, .2f, 1.1f);
                foreach (int removed in record.RemovedObstacleIndices)
                {
                    if (reaction.BodyIndex == removed && record.Response == DamageResponse.Damage) continue;
                    RuntimeObstacle removedBody = before.Obstacles[removed];
                    int extent = LevelPlacementRules.Size(removedBody.Definition.Kind);
                    Vector3 removedPosition = PuzzleWorldBoard.CellPosition(removedBody.Definition.Coordinate) + new Vector3((extent - 1) * .5f, -(extent - 1) * .5f);
                    bool crate = removedBody.Definition.Kind == ObstacleKind.Crate;
                    AddEffect(crate ? "WoodBreak" : "MetalBreak", crate ? "wood-break" : "metal-break", removedPosition, removedPosition,
                        reaction.Time, .2f, extent == 2 ? 2.16f : 1.1f);
                }
                if (!reaction.BodyIndex.HasValue) continue;
                RuntimeObstacle body = before.Obstacles[reaction.BodyIndex.Value];
                bool large = body.Definition.Kind == ObstacleKind.Appliance || body.Definition.Kind == ObstacleKind.Generator;
                if (large) position = PuzzleWorldBoard.CellPosition(body.Definition.Coordinate) + new Vector3(.5f, -.5f);
                if (record.Response == DamageResponse.Charge)
                    clips.Add(new Clip { Label = "Generator-charge", Paths = Enumerable.Range(1, 4)
                        .Select(frame => "Effects/GeneratorCharge/charge-pulse-" + frame.ToString("00") + "-v1-256").ToArray(),
                        Start = reaction.Time, End = reaction.Time + .2f, From = position, To = position, Size = large ? 2.16f : 1.1f });
                if (record.Response != DamageResponse.Damage || record.DurabilityAfter >= record.DurabilityBefore) continue;
                bool wood = body.Definition.Kind == ObstacleKind.Crate;
                AddEffect(wood ? "WoodBreak" : "MetalBreak", wood ? "wood-break" : "metal-break", position, position,
                    reaction.Time, .2f, large ? 2.16f : 1.1f);
            }
        }

        private void BuildRocketClips(PuzzleEffectTimeline.Attack attack, float start, Vector3 center)
        {
            PowerAttackRecord record = attack.Record;
            bool horizontal = record.Area != PowerArea.Vertical && record.Direction != RocketDirection.Vertical;
            bool cross = record.Area == PowerArea.Cross || record.Area == PowerArea.WideCross;
            for (int axis = 0; axis < (cross ? 2 : 1); axis++)
            {
                bool alongRow = cross ? axis == 0 : horizontal;
                var lines = record.Targets.Where(target => !cross || (alongRow ?
                    Mathf.Abs(target.Row - record.Center.Row) <= (record.Area == PowerArea.WideCross ? 1 : 0) :
                    Mathf.Abs(target.Column - record.Center.Column) <= (record.Area == PowerArea.WideCross ? 1 : 0)))
                    .GroupBy(target => alongRow ? target.Row : target.Column);
                foreach (var line in lines)
                {
                    Vector3 launch = center;
                    if (alongRow) launch.y = PuzzleWorldBoard.CellPosition(line.First()).y;
                    else launch.x = PuzzleWorldBoard.CellPosition(line.First()).x;
                    foreach (int sign in new[] { -1, 1 })
                    {
                        var targets = line.Where(target => (alongRow ? target.Column - record.Center.Column : record.Center.Row - target.Row) * sign >= 0).ToArray();
                        if (targets.Length == 0) continue;
                        BoardCoordinate end = targets.OrderByDescending(target => Vector3.Distance(launch, PuzzleWorldBoard.CellPosition(target))).First();
                        Vector3 destination = PuzzleWorldBoard.CellPosition(end);
                        float arrival = attack.ImpactAt(end);
                        string direction = alongRow ? "horizontal" : "vertical";
                        string[] frames = new[] { "PowerBlocks/cleaning-rocket-" + direction + "-v1" }.Concat(
                            Enumerable.Range(2, 3).Select(frame => "PowerBlocks/cleaning-rocket-" + direction + "-launch-frame-" + frame + "-v1-256")).ToArray();
                        float angle = sign > 0 ? 0 : 180;
                        float size = alongRow ? .92f * 1.12f : .92f;
                        Vector3 offset = alongRow ? new Vector3(-28.5f / 256, 28f / 256, 0) * (size * sign) : Vector3.zero;
                        // 가로 발사 3·4컷의 본체는 원본 중앙 위에 있어 대기/2컷과 반대 방향 보정이 필요하다.
                        Vector3[] offsets = alongRow ? new[] { offset, offset,
                            new Vector3(-28.5f / 256, -23f / 256, 0) * (size * sign),
                            new Vector3(-28.5f / 256, -23f / 256, 0) * (size * sign) } : null;
                        clips.Add(new Clip { Label = "Rocket-flight", Paths = frames, Start = start, End = arrival,
                            From = launch, To = destination, Size = size, Angle = angle, Offset = offset, FrameOffsets = offsets, MotionDelay = .06f });
                        AddEffect("Rocket", "rocket-trail", launch, destination, start + .06f,
                            Mathf.Max(.01f, arrival - start - .06f), 1.1f, 4, (alongRow ? 0 : 90) + angle);
                    }
                }
            }
            foreach (BoardCoordinate target in record.Targets)
            {
                Vector3 position = PuzzleWorldBoard.CellPosition(target);
                AddEffect("Rocket", "rocket-impact", position, position, attack.ImpactAt(target), .2f, 1.1f);
            }
        }

        private void AddEffect(string category, string name, Vector3 from, Vector3 to, float start, float duration,
            float size, int frames = 4, float angle = 0)
        {
            clips.Add(new Clip { Label = name, Paths = Enumerable.Range(1, frames)
                .Select(frame => "Effects/" + category + "/Animations/" + name + "-frame-" + frame.ToString("00") + "-v1-256").ToArray(),
                From = from, To = to, Start = start, End = start + duration, Size = size, Angle = angle });
        }
    }
}

