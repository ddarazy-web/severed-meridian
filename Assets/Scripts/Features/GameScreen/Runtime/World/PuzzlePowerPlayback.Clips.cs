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
            foreach (MatchedBlockChange change in timeline.Changes)
            {
                Vector3 position = PuzzleWorldBoard.CellPosition(change.Coordinate);
                if (change.IsConsumed)
                    AddNormalEffect(before.CellAt(change.Coordinate), change.OriginalColor, "match", "match", position, 0, .2f, 1.1f);
                if (change.IsTransformation)
                    AddNormalEffect(before.CellAt(change.Coordinate), change.OriginalColor, "creation", "power-creation", position, .12f, .2f, 1.3f);
                // 직접 매칭의 레이어 변화는 EffectRecord가 아닌 MatchedBlockChange에 남는다.
                if (change.CoverBefore > change.CoverAfter)
                    AddLayerEffect(before.CellAt(change.Coordinate), true, "web-break", position, .12f);
                if (change.IsConsumed && before.CellAt(change.Coordinate).DustDurability > final.CellAt(change.Coordinate).DustDurability &&
                    !timeline.Reactions.Any(reaction => reaction.Record.Target.Equals(change.Coordinate) && reaction.Record.DustBefore > reaction.Record.DustAfter))
                    AddLayerEffect(before.CellAt(change.Coordinate), false, "dust-clear", position, .12f);
            }
            if (timeline.Combination?.IsTransformation == true)
                foreach (PowerTransformation transformation in timeline.Combination.Transformations)
                {
                    Vector3 position = PuzzleWorldBoard.CellPosition(transformation.Coordinate);
                    AddPowerEffect(before, transformation.Coordinate, RuntimeContent.Magnet, "transform", "magnet-transform", position, position, 0, .35f, 1.2f);
                }
            foreach (DroneFlightMotion flight in timeline.Flights)
                foreach (DroneFlightMotion.Phase phase in flight.Phases)
                {
                    Elements.ElementVisualFrame[] frames = Enumerable.Range(1, 4).Select(frame =>
                        art.Visuals.Animation(PowerSource(before, flight.Record.Origin, RuntimeContent.Drone), RuntimeContent.Drone, RocketDirection.Horizontal, frame)).ToArray();
                    clips.Add(new Clip { Label = phase.Kind == DroneFlightPhaseKind.Dash ? "Drone-flight" : "Drone-hover",
                        Paths = frames.Select(frame => frame.Path).ToArray(), VisualFrames = frames,
                        Start = phase.Start, End = phase.End, From = phase.From, To = phase.To,
                        Size = .92f, DronePhase = phase });
                }
            foreach (PuzzleEffectTimeline.Attack attack in timeline.Attacks)
            {
                PowerAttackRecord record = attack.Record;
                Vector3 origin = PuzzleWorldBoard.CellPosition(record.Origin);
                Vector3 center = PuzzleWorldBoard.CellPosition(record.Center);
                float start = attack.Start;
                if (record.IsFlight)
                {
                    float flight = attack.FlightDuration;
                    AddPowerEffect(before, record.Origin, RuntimeContent.Drone, "impact", "drone-impact", center, center, start + flight, .2f, 1.3f);
                    start += flight;
                }
                if (record.Area == PowerArea.Blast3 || record.Area == PowerArea.Blast5 || record.Power == RuntimeContent.Bomb)
                    AddPowerEffect(before, record.Origin, RuntimeContent.Bomb, "blast", "bomb-explosion", center, center, start, .24f,
                        record.Area == PowerArea.Blast5 ? 5 : 3);
                else if (record.Power == RuntimeContent.Magnet)
                {
                    foreach (BoardCoordinate target in record.Targets)
                    {
                        Vector3 position = PuzzleWorldBoard.CellPosition(target);
                        AddPowerEffect(before, record.Origin, RuntimeContent.Magnet, "pull", "magnet-pull", position, center, start,
                            attack.ImpactAt(target) - start, .9f);
                    }
                }
                else if (record.Power == RuntimeContent.Rocket || record.Area == PowerArea.Horizontal || record.Area == PowerArea.Vertical)
                    BuildRocketClips(attack, start, center, before);
                else if (!record.IsFlight)
                    foreach (BoardCoordinate target in record.Targets)
                    {
                        Vector3 position = PuzzleWorldBoard.CellPosition(target);
                        AddPowerEffect(before, record.Origin, RuntimeContent.Drone, "impact", "drone-impact", position, position, attack.ImpactAt(target), .2f, 1.1f);
                    }
            }
            foreach (PuzzleEffectTimeline.Reaction reaction in timeline.Reactions)
            {
                EffectRecord record = reaction.Record;
                Vector3 position = PuzzleWorldBoard.CellPosition(record.Target);
                if (record.CoverAfter < record.CoverBefore)
                {
                    bool mold = before.CellAt(record.Target).Cover == CoverKind.Mold;
                    AddLayerEffect(before.CellAt(record.Target), true, mold ? "mold-clear" : "web-break", position, reaction.Time);
                }
                if (record.DustAfter < record.DustBefore)
                    AddLayerEffect(before.CellAt(record.Target), false, "dust-clear", position, reaction.Time);
                foreach (int removed in record.RemovedObstacleIndices)
                {
                    if (reaction.BodyIndex == removed && record.Response == DamageResponse.Damage) continue;
                    RuntimeObstacle removedBody = before.Obstacles[removed];
                    int extent = Elements.ElementVisualLookup.Size(removedBody.Element);
                    Vector3 removedPosition = PuzzleWorldBoard.CellPosition(removedBody.Definition.Coordinate) + new Vector3((extent - 1) * .5f, -(extent - 1) * .5f);
                    AddRegisteredEffect(removedBody.Element, art.Visuals.Obstacle(removedBody), "damage", removedPosition, removedPosition,
                        reaction.Time, .2f, extent == 2 ? 2.16f : 1.1f);
                }
                if (!reaction.BodyIndex.HasValue) continue;
                RuntimeObstacle body = before.Obstacles[reaction.BodyIndex.Value];
                int size = Elements.ElementVisualLookup.Size(body.Element);
                bool large = size > 1;
                if (large) position = PuzzleWorldBoard.CellPosition(body.Definition.Coordinate) + new Vector3((size - 1) * .5f, -(size - 1) * .5f);
                if (record.Response == DamageResponse.Charge)
                    AddRegisteredEffect(body.Element, art.Visuals.Obstacle(body), "charge", position, position, reaction.Time, .2f, large ? 2.16f : 1.1f);
                if (record.Response != DamageResponse.Damage || record.DurabilityAfter >= record.DurabilityBefore) continue;
                AddRegisteredEffect(body.Element, art.Visuals.Obstacle(body), "damage", position, position,
                    reaction.Time, .2f, large ? 2.16f : 1.1f);
            }
        }

        private void BuildRocketClips(PuzzleEffectTimeline.Attack attack, float start, Vector3 center, LevelRuntimeState before)
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
                        Elements.ElementVisualFrame[] frames = Enumerable.Range(0, 4).Select(frame =>
                            art.Visuals.Animation(PowerSource(before, record.Origin, RuntimeContent.Rocket), RuntimeContent.Rocket,
                                alongRow ? RocketDirection.Horizontal : RocketDirection.Vertical, frame)).ToArray();
                        float angle = sign > 0 ? 0 : 180;
                        clips.Add(new Clip { Label = "Rocket-flight", Paths = frames.Select(frame => frame.Path).ToArray(), VisualFrames = frames,
                            Start = start, End = arrival, From = launch, To = destination, Size = .92f, Angle = angle, MotionDelay = .06f });
                        AddPowerEffect(before, record.Origin, RuntimeContent.Rocket, "trail", "rocket-trail", launch, destination, start + .06f,
                            Mathf.Max(.01f, arrival - start - .06f), 1.1f, (alongRow ? 0 : 90) + angle);
                    }
                }
            }
            foreach (BoardCoordinate target in record.Targets)
            {
                Vector3 position = PuzzleWorldBoard.CellPosition(target);
                AddPowerEffect(before, record.Origin, RuntimeContent.Rocket, "impact", "rocket-impact", position, position, attack.ImpactAt(target), .2f, 1.1f);
            }
        }

        private void AddRegisteredEffect(Elements.ElementDefinition definition, Elements.ElementVisualFrame visual, string key,
            Vector3 from, Vector3 to, float start, float duration, float size, float angle = 0, string label = null)
        {
            if (visual == null || !visual.EffectAnimations.TryGetValue(key, out var selected))
                throw new System.InvalidOperationException("요소 ID '" + definition.Id.Value + "': 등록되지 않은 효과 키 '" + key + "'");
            Elements.ElementVisualFrame[] frames = selected.ToArray();
            clips.Add(new Clip { Label = label ?? (key == "charge" ? "Generator-charge" : "Body-damage"),
                Paths = frames.Select(frame => frame.Path).ToArray(), VisualFrames = frames,
                From = from, To = to, Start = start, End = start + duration, Size = size, Angle = angle });
        }
        private void AddNormalEffect(RuntimeCell cell, RabbitColor color, string key, string label,
            Vector3 position, float start, float duration, float size)
        {
            Elements.ElementDefinition definition = cell.ContentElement ?? Elements.LegacyElementDefinitions.GetContent(RuntimeContent.Normal);
            Elements.ElementVisualFrame visual = art.Visuals.Resolver.Resolve(definition.Id,
                new Elements.ElementVisualState((int)color, 0, 0, 0, 0, 0, 1));
            AddRegisteredEffect(definition, visual, key, position, position, start, duration, size, label: label);
        }
        private void AddLayerEffect(RuntimeCell cell, bool cover, string label, Vector3 position, float start)
        {
            Elements.ElementDefinition definition = cover ? cell.CoverElement ?? Elements.LegacyElementDefinitions.Get(cell.Cover.Value)
                : cell.DustElement ?? Elements.LegacyElementDefinitions.GetDust();
            AddRegisteredEffect(definition, cover ? art.Visuals.Cover(cell) : art.Visuals.Dust(cell), "clear",
                position, position, start, .2f, 1.1f, label: label);
        }
        private RuntimeCell PowerSource(LevelRuntimeState before, BoardCoordinate origin, RuntimeContent content)
        {
            RuntimeCell source = before.CellAt(origin);
            if (source.Content != content && timeline.Combination != null)
                source = new[] { before.CellAt(timeline.Combination.First), before.CellAt(timeline.Combination.Center) }
                    .FirstOrDefault(cell => cell.Content == content) ?? source;
            return source;
        }
        private void AddPowerEffect(LevelRuntimeState before, BoardCoordinate origin, RuntimeContent content, string key, string label,
            Vector3 from, Vector3 to, float start, float duration, float size, float angle = 0)
        {
            RuntimeCell source = PowerSource(before, origin, content);
            Elements.ElementDefinition definition = source.Content == content ? source.ContentElement : null;
            definition ??= Elements.LegacyElementDefinitions.GetContent(content);
            Elements.ElementVisualFrame visual = art.Visuals.Resolver.Resolve(definition.Id,
                new Elements.ElementVisualState(-1, 0, 0, 0, (int)(source.RocketDirection ?? RocketDirection.Horizontal), 0, 1));
            AddRegisteredEffect(definition, visual, key, from, to, start, duration, size, angle, label);
        }
    }
}

