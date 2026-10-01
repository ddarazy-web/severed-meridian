using System;
using System.Collections.Generic;
using System.Linq;
using Board;
using Levels;
using Simulation;
using UnityEngine;

namespace GameScreen
{
    internal sealed class PuzzleBoardSettlementPlayback
    {
        private sealed class Move
        {
            internal SettlementRecord Record;
            internal Vector3 Start, End;
            internal float Begins, Seconds;
        }

        private sealed class Track
        {
            internal PuzzleBoardSnapshot.Image Image;
            internal readonly List<Move> Moves = new List<Move>();
            internal float Ends;
            internal bool Collected;
        }

        private readonly List<Track> tracks = new List<Track>();
        private PuzzleBoardSnapshot snapshot;
        private float elapsed, duration, landingSeconds;
        internal bool IsPlaying { get; private set; }

        internal void Begin(PuzzleBoardSnapshot before, PuzzleWorldBoard world, SettlementResult result, PuzzleArtwork artwork,
            int recoveryBefore, float moveTime, float supplyTime, float landingTime)
        {
            Reset(); snapshot = before; landingSeconds = Mathf.Max(.01f, landingTime);
            // 기존 20% 증가에 추가 20%를 적용한다. 저장된 원래 시간 / 1.44로 재생한다.
            moveTime /= 1.44f; supplyTime /= 1.44f;
            Dictionary<PuzzleBoardSnapshot.Image, Track> owners = new Dictionary<PuzzleBoardSnapshot.Image, Track>();
            Dictionary<BoardCoordinate, float> vacantAt = new Dictionary<BoardCoordinate, float>();
            RecoveryRecord[] recoveries = result.State.Recoveries.Skip(recoveryBefore).ToArray();
            Collect(0);
            // 규칙의 Batch는 소유권 추적에만 쓴다. 화면에서는 앞 점유자가 출발하면 뒤 점유자도 출발한다.
            foreach (IGrouping<int, SettlementRecord> batch in result.Records.GroupBy(record => record.Batch).OrderBy(group => group.Key))
            {
                foreach (SettlementRecord record in batch)
                {
                    PuzzleBoardSnapshot.Image image;
                    Vector3 end = PuzzleWorldBoard.CellPosition(record.Target), start = PuzzleWorldBoard.CellPosition(record.Source);
                    vacantAt.TryGetValue(record.Target, out float available);
                    if (record.Kind == MovementKind.Supply)
                    {
                        image = world.SupplyImage(record, result.State, artwork); snapshot.Owned.Add(image);
                        Vector3 direction = result.State.CellAt(record.Target).Gravity switch
                        {
                            GravityDirection.Up => Vector3.up, GravityDirection.Left => Vector3.left,
                            GravityDirection.Right => Vector3.right, _ => Vector3.down
                        };
                        foreach (FlowPathCell path in result.State.Flow.Paths)
                            if (path.Coordinate.Equals(record.Target) && !path.IsEnd)
                            { direction = (PuzzleWorldBoard.CellPosition(path.Next) - end).normalized; break; }
                        // 늦게 생성되는 기록도 시작부터 공급구 바깥 대기열에서 함께 내려온다.
                        // 공급구 마스크가 내부 장애물이나 이웃 칸으로 드러나는 것을 막는다.
                        start = end - direction * (1 + available / Mathf.Max(.01f, moveTime));
                    }
                    else if (!snapshot.Images.TryGetValue(record.Source, out image))
                        throw new InvalidOperationException("정착 표시 출발점 없음: " + record.Source);
                    if (!owners.TryGetValue(image, out Track track))
                    {
                        track = new Track { Image = image }; owners.Add(image, track); tracks.Add(track);
                    }
                    float begins = Mathf.Max(track.Ends, available);
                    float seconds = record.Kind == MovementKind.Supply ? Mathf.Max(.01f, supplyTime) : record.Kind == MovementKind.Portal ? Mathf.Max(.01f, moveTime) :
                        Mathf.Max(.01f, Mathf.Clamp(moveTime * Vector3.Distance(start, end), moveTime, .24f / 1.44f));
                    if (record.Kind == MovementKind.Supply) { seconds += begins; begins = 0; }
                    track.Moves.Add(new Move { Record = record, Start = start, End = end, Begins = begins, Seconds = seconds });
                    track.Ends = begins + seconds; duration = Mathf.Max(duration, track.Ends);
                    if (record.Kind != MovementKind.Supply)
                    {
                        snapshot.Images.Remove(record.Source);
                        // 통로 입구는 페이드아웃이 끝나야 뒤 블록이 들어갈 수 있다.
                        vacantAt[record.Source] = begins + (record.Kind == MovementKind.Portal ? seconds * .5f : 0);
                    }
                    snapshot.Images[record.Target] = image;
                }
                Collect(batch.Key);
            }
            IsPlaying = tracks.Count > 0;
            if (IsPlaying) Paint(0);

            void Collect(int batch)
            {
                foreach (RecoveryRecord record in recoveries.Where(record => record.Batch == batch))
                    if (snapshot.Images.TryGetValue(record.Coordinate, out PuzzleBoardSnapshot.Image image))
                    {
                        if (owners.TryGetValue(image, out Track track))
                        { track.Collected = true; vacantAt[record.Coordinate] = track.Ends; }
                        else { image.Hide(); vacantAt[record.Coordinate] = 0; }
                        snapshot.Images.Remove(record.Coordinate);
                    }
            }
        }

        private void Paint(float time)
        {
            foreach (Track track in tracks)
            {
                if (track.Collected && time >= track.Ends) { track.Image.Hide(); continue; }
                Move move = track.Moves[0];
                for (int i = 1; i < track.Moves.Count && track.Moves[i].Begins <= time; i++) move = track.Moves[i];
                float t = Mathf.Clamp01((time - move.Begins) / move.Seconds);
                Color color = track.Image.Color;
                if (move.Record.Kind == MovementKind.Portal)
                {
                    track.Image.Place(t < .5f ? move.Start : move.End);
                    color.a *= Mathf.Abs(2 * t - 1);
                }
                else
                {
                    // 칸마다 가속을 다시 시작하지 않아 긴 낙하도 끊김 없이 이어진다.
                    track.Image.Place(Vector3.Lerp(move.Start, move.End, t));
                }
                if (move.Record.Kind != MovementKind.Supply || t >= 1) track.Image.ReleaseClip();
                track.Image.Renderer.color = color;
            }
        }

        internal bool Tick(float deltaTime)
        {
            if (!IsPlaying) return false;
            elapsed += Mathf.Max(0, deltaTime);
            Paint(elapsed);
            if (elapsed < duration) return false;
            float t = Mathf.Clamp01((elapsed - duration) / landingSeconds);
            foreach (Track track in tracks)
                if (!track.Collected)
                    track.Image.Renderer.transform.localScale = Vector3.Scale(track.Image.Scale,
                        new Vector3(1 + .04f * Mathf.Sin(t * Mathf.PI), 1 - .06f * Mathf.Sin(t * Mathf.PI), 1));
            if (t < 1) return false;
            IsPlaying = false; return true;
        }

        internal void Reset()
        {
            snapshot?.Restore(); snapshot = null;
            tracks.Clear(); IsPlaying = false; elapsed = 0; duration = 0;
        }
    }
}
