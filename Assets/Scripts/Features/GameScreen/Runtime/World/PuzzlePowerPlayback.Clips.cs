using System.Linq;
using System.Collections.Generic;
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
            // 표시 전용 난수는 규칙 난수와 Unity 전역 난수를 소비하지 않는다. 표적은 이미 확정된 기록만 사용한다.
            var choreography = new System.Random(System.Guid.NewGuid().GetHashCode());
            Vector3 orbitCenter = new Vector3((float)choreography.NextDouble() * .24f - .12f, (float)choreography.NextDouble() * .24f - .12f, 0);
            float[] ringPhases = Enumerable.Range(0, 4).Select(_ => (float)choreography.NextDouble() * Mathf.PI * 2).ToArray();
            float spin = choreography.Next(2) == 0 ? -1 : 1;
            bool gentleLift = timeline.Attacks.Count(attack => attack.Record.IsFlight) <= 3 &&
                timeline.Combination?.Kind != PowerCombinationKind.RocketDrone &&
                timeline.Combination?.Kind != PowerCombinationKind.BombDrone &&
                timeline.Combination?.Kind != PowerCombinationKind.MagnetDrone;
            List<(int ring, int place, float start, float end)> orbitSlots = new List<(int, int, float, float)>();
            foreach (PuzzleEffectTimeline.Attack attack in timeline.Attacks)
            {
                PowerAttackRecord record = attack.Record;
                Vector3 origin = PuzzleWorldBoard.CellPosition(record.Origin);
                Vector3 center = PuzzleWorldBoard.CellPosition(record.Center);
                float start = attack.Start;
                if (record.IsFlight)
                {
                    float flight = Mathf.Clamp(Vector3.Distance(origin, center) * .045f, .18f, .38f);
                    float hoverStart = timeline.Attacks.Where(prior => !prior.Record.IsFlight && prior.Record.Origin.Equals(record.Origin))
                        .Select(prior => prior.Start).DefaultIfEmpty(0).Min();
                    var siblings = timeline.Attacks.Where(candidate => candidate.Record.IsFlight && candidate.Record.Origin.Equals(record.Origin)).ToList();
                    int lane = siblings.IndexOf(attack);
                    Vector3 departure;
                    if (gentleLift)
                    {
                        // 소수 드론은 생성점에서 천천히 떠오른 뒤 대기한다. 세 드론은 좌우로 나누어 본체를 분리한다.
                        departure = origin + new Vector3(lane - (siblings.Count - 1) * .5f, .65f, 0);
                        clips.Add(new Clip { Label = "Drone-hover", Paths = new[] { "PowerBlocks/collection-drone-rotor-4frames-v1" },
                            Start = hoverStart, End = start, From = origin, To = departure, Size = .92f, Sheet = true, LiftOnly = true });
                    }
                    else
                    {
                        // 큰 원을 화면 안에 유지한다. 같은 원에서는 위상 간격과 각속도를 공유해 추월·겹침을 막는다.
                        var available = new List<(int ring, int place)>();
                        for (int ring = 1; ring <= 4; ring++)
                        {
                            int places = Mathf.FloorToInt(Mathf.PI / Mathf.Asin(.96f / (2 * ring)));
                            for (int place = 0; place < places; place++)
                                if (!orbitSlots.Any(slot => slot.ring == ring && slot.place == place && hoverStart < slot.end && slot.start < start))
                                    available.Add((ring, place));
                        }
                        var chosen = available[choreography.Next(available.Count)];
                        float orbitRadius = chosen.ring;
                        int ringPlaces = Mathf.FloorToInt(Mathf.PI / Mathf.Asin(.96f / (2 * orbitRadius)));
                        float orbitPhase = ringPhases[chosen.ring - 1] + chosen.place * Mathf.PI * 2 / ringPlaces;
                        float orbitPeriod = 1.3f + orbitRadius * .25f;
                        float orbitDirection = chosen.ring % 2 == 0 ? spin : -spin;
                        Vector3 laneOffset = orbitCenter - origin;
                        orbitSlots.Add((chosen.ring, chosen.place, hoverStart, start));
                        if (start > hoverStart)
                            clips.Add(new Clip { Label = "Drone-hover", Paths = new[] { "PowerBlocks/collection-drone-rotor-4frames-v1" },
                                Start = hoverStart, End = start, From = origin, To = center, LaneOffset = laneOffset, Size = .92f, Sheet = true,
                                OrbitRadius = orbitRadius, OrbitPhase = orbitPhase, OrbitPeriod = orbitPeriod, OrbitDirection = orbitDirection });
                        departure = DroneOrbitPosition(origin, laneOffset, start - hoverStart, start, orbitRadius, orbitPhase, orbitPeriod, orbitDirection);
                    }
                    Vector3 headingToTarget = (center - departure).normalized;
                    Vector3 perpendicular = new Vector3(-headingToTarget.y, headingToTarget.x, 0);
                    float bend = Mathf.Clamp(Vector3.Distance(departure, center) * .18f, .35f, .85f) * (lane % 2 == 0 ? 1 : -1);
                    clips.Add(new Clip { Label = "Drone-flight", Paths = new[] { "PowerBlocks/collection-drone-rotor-4frames-v1" },
                        Start = start, End = start + flight, From = departure, To = center, Size = .92f, Sheet = true,
                        Control1 = departure + headingToTarget * .25f + perpendicular * bend,
                        Control2 = center - headingToTarget * .35f + perpendicular * bend * .6f });
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

