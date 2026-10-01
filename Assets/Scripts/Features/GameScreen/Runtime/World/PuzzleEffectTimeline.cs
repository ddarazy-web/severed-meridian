using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Board;
using Simulation;
using UnityEngine;

namespace GameScreen
{
    // 규칙 기록을 시간으로 옮기는 순수 표시 계산이다. 상태·표적·난수는 변경하지 않는다.
    public sealed class PuzzleEffectTimeline
    {
        public sealed class Attack
        {
            public PowerAttackRecord Record { get; }
            public float Start { get; }
            public float End => Mathf.Max(Start + .2f, Record.Targets.Count == 0 ? Start : Record.Targets.Max(ImpactAt)) + .2f;
            internal Attack(PowerAttackRecord record, float start) { Record = record; Start = start; }
            public float ImpactAt(BoardCoordinate target)
            {
                float launch = Start;
                if (Record.IsFlight)
                {
                    float flight = Mathf.Clamp(Vector3.Distance(PuzzleWorldBoard.CellPosition(Record.Origin), PuzzleWorldBoard.CellPosition(Record.Center)) * .045f, .18f, .38f);
                    launch += flight;
                    if (Record.Area != PowerArea.Horizontal && Record.Area != PowerArea.Vertical)
                        return launch + (Record.Area == PowerArea.Point ? 0 : .12f);
                }
                if (Record.Power == RuntimeContent.Rocket || Record.Area == PowerArea.Horizontal || Record.Area == PowerArea.Vertical)
                    return launch + RocketTravel(target);
                return launch + (Record.Power == RuntimeContent.Magnet ? .35f : .12f);
            }
            private float RocketTravel(BoardCoordinate target)
            {
                bool cross = Record.Area == PowerArea.Cross || Record.Area == PowerArea.WideCross;
                int width = Record.Area == PowerArea.WideCross ? 1 : 0;
                float arrival = float.PositiveInfinity;
                if ((!cross && Record.Area != PowerArea.Vertical) || (cross && Mathf.Abs(target.Row - Record.Center.Row) <= width))
                    arrival = Ray(true);
                if (Record.Area == PowerArea.Vertical || (cross && Mathf.Abs(target.Column - Record.Center.Column) <= width))
                    arrival = Mathf.Min(arrival, Ray(false));
                return arrival;

                float Ray(bool horizontal)
                {
                    int distance = horizontal ? target.Column - Record.Center.Column : target.Row - Record.Center.Row;
                    int length = Record.Targets.Where(cell => horizontal ? cell.Row == target.Row : cell.Column == target.Column)
                        .Select(cell => horizontal ? cell.Column - Record.Center.Column : cell.Row - Record.Center.Row)
                        .Where(delta => distance == 0 || delta * distance >= 0).Select(Mathf.Abs).DefaultIfEmpty(0).Max();
                    // 최소/최대 시간은 한 방향의 전체 비행에 적용한다. 각 칸은 같은 이동 비율로 도착한다.
                    return .06f + Mathf.Clamp(length * .06f, .12f, .36f) * Mathf.Abs(distance) / Mathf.Max(1, length);
                }
            }
        }

        public sealed class Reaction
        {
            public EffectRecord Record { get; }
            public float Time { get; }
            public float ContactTime { get; }
            public int? BodyIndex { get; }
            internal Reaction(EffectRecord record, float time, float contactTime, int? body)
            { Record = record; Time = time; ContactTime = contactTime; BodyIndex = body; }
        }

        public ReadOnlyCollection<Attack> Attacks { get; }
        public ReadOnlyCollection<Reaction> Reactions { get; }
        public ReadOnlyCollection<MatchedBlockChange> Changes { get; }
        public PowerCombination Combination { get; }
        public float Duration { get; }

        public PuzzleEffectTimeline(LevelRuntimeState before, IEnumerable<MatchedBlockChange> changes,
            IEnumerable<EffectRecord> effects, PowerPresentationTrace trace)
        {
            Changes = changes.ToList().AsReadOnly(); Combination = trace?.Combination;
            PowerAttackRecord[] records = trace?.Attacks.ToArray() ?? Array.Empty<PowerAttackRecord>();
            Dictionary<int, Attack> attacks = new Dictionary<int, Attack>();
            Dictionary<(int, BoardCoordinate), float> activations = new Dictionary<(int, BoardCoordinate), float>();
            Dictionary<string, float> previous = new Dictionary<string, float>();
            EffectRecord[] sourceEffects = effects.ToArray();
            Dictionary<(int, int), Queue<float>> bodyContacts = new Dictionary<(int, int), Queue<float>>();
            List<Reaction> reactions = new List<Reaction>();
            foreach (EffectRecord effect in sourceEffects)
            {
                PowerAttackRecord record = records.FirstOrDefault(attack => attack.HitGroup == effect.HitGroup);
                Attack attack = record == null ? null : Schedule(record);
                BoardCoordinate impact = effect.Cause == DamageCause.MagnetAdjacent ? effect.Source : effect.Target;
                // 변환 시작 기록에는 공격이 없으므로 변환 완료 시점에 소비한다.
                float time = attack?.ImpactAt(impact) ?? (Combination?.IsTransformation == true ? .35f : effect.Cause == DamageCause.AdjacentMatch ? .12f : 0);
                int? body = before.CellAt(effect.Target).ObstacleIndex;
                if (body.HasValue && attack != null && effect.Response == DamageResponse.Damage && effect.Cause == DamageCause.Power)
                {
                    // 하나의 본체는 가까운 점유 칸부터 접촉한다. 피해 기록 자체의 순서와 값은 바꾸지 않는다.
                    var contactKey = (effect.HitGroup, body.Value);
                    if (!bodyContacts.TryGetValue(contactKey, out Queue<float> contacts))
                    {
                        contacts = new Queue<float>(sourceEffects.Where(candidate => candidate.HitGroup == effect.HitGroup &&
                            candidate.Response == DamageResponse.Damage && candidate.Cause == DamageCause.Power &&
                            before.CellAt(candidate.Target).ObstacleIndex == body).Select(candidate => attack.ImpactAt(candidate.Target)).OrderBy(value => value));
                        bodyContacts.Add(contactKey, contacts);
                    }
                    time = contacts.Dequeue();
                }
                float contactTime = time;
                bool reacts = effect.Response == DamageResponse.Remove || effect.Response == DamageResponse.Activate ||
                    effect.Response == DamageResponse.Damage || effect.Response == DamageResponse.CoverDamage || effect.Response == DamageResponse.Charge;
                // 실제로 적용된 같은 본체의 타격 순서를 보존한다. 무효/중복 반응은 시간을 늘리지 않는다.
                string key = body.HasValue ? "body:" + body.Value : "cell:" + effect.Target;
                if (reacts)
                {
                    if (previous.TryGetValue(key, out float last)) time = Mathf.Max(time, last + .06f);
                    foreach (int removed in effect.RemovedObstacleIndices)
                        if (previous.TryGetValue("body:" + removed, out float priorRemoval)) time = Mathf.Max(time, priorRemoval + .06f);
                    previous[key] = time;
                    foreach (int removed in effect.RemovedObstacleIndices) previous["body:" + removed] = time;
                }
                // 본체 손상 pulse의 간격 때문에 로켓의 이동·착탄 시각을 늦추지 않는다.
                reactions.Add(new Reaction(effect, time, contactTime, body));
                if (effect.Response == DamageResponse.Activate) activations[(effect.HitGroup, effect.Target)] = time;
            }
            foreach (PowerAttackRecord record in records) Schedule(record);
            Attacks = records.Select(record => attacks[record.HitGroup]).ToList().AsReadOnly();
            Reactions = reactions.AsReadOnly();
            Duration = Mathf.Max(Changes.Count > 0 ? .32f : 0,
                Mathf.Max(Attacks.Count > 0 ? Attacks.Max(attack => attack.End) : 0, Reactions.Count > 0 ? Reactions.Max(reaction => reaction.Time) + .2f : 0));
            if (Combination != null) Duration = Mathf.Max(Duration, Combination.IsTransformation ? .55f : .2f);

            Attack Schedule(PowerAttackRecord record)
            {
                if (attacks.TryGetValue(record.HitGroup, out Attack existing)) return existing;
                float start = Combination?.IsTransformation == true ? .35f : 0;
                if (activations.TryGetValue((record.ParentHitGroup, record.Origin), out float trigger)) start = Mathf.Max(start, trigger);
                else
                {
                    PowerAttackRecord parent = records.FirstOrDefault(candidate => candidate.HitGroup == record.ParentHitGroup);
                    if (parent != null) start = Mathf.Max(start, Schedule(parent).ImpactAt(record.Origin));
                }
                for (int i = 0; i < record.WaitForAttacks; i++) start = Mathf.Max(start, Schedule(records[i]).End);
                if (record.IsFlight)
                {
                    float hoverStart = records.Where(prior => !prior.IsFlight && prior.Origin.Equals(record.Origin))
                        .Select(prior => Schedule(prior).Start).DefaultIfEmpty(0).Min();
                    start = Mathf.Max(start, hoverStart + .72f);
                }
                // 실제 재탐색 기록이 있을 때만 선회하며 새 표적을 확인하는 시간을 표시한다.
                if (record.Retargeted) start += .18f;
                Attack attack = new Attack(record, start); attacks.Add(record.HitGroup, attack); return attack;
            }
        }
    }
}
