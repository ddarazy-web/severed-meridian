using System.Collections.Generic;
using System.Linq;
using Board;
using Simulation;
using UnityEngine;

namespace GameScreen.Editor
{
    public static partial class PuzzleSettlementAnimationVerification
    {
        private static void VerifyRecordedFrames(PuzzleGameSession session, PuzzleWorldBoard board, SettlementResult result,
            Dictionary<BoardCoordinate, SpriteRenderer> owners, string name)
        {
            Dictionary<SpriteRenderer, List<SettlementRecord>> routes = new Dictionary<SpriteRenderer, List<SettlementRecord>>();
            HashSet<SpriteRenderer> collected = new HashSet<SpriteRenderer>();
            foreach (IGrouping<int, SettlementRecord> batch in result.Records.GroupBy(record => record.Batch).OrderBy(group => group.Key))
            {
                foreach (SettlementRecord record in batch)
                {
                    SpriteRenderer image = owners[record.Source];
                    if (!routes.ContainsKey(image)) routes.Add(image, new List<SettlementRecord>());
                    routes[image].Add(record); owners.Remove(record.Source); owners[record.Target] = image;
                }
                foreach (RecoveryRecord recovery in result.State.Recoveries.Where(record => record.Batch == batch.Key))
                    if (owners.TryGetValue(recovery.Coordinate, out SpriteRenderer image))
                    { collected.Add(image); owners.Remove(recovery.Coordinate); }
            }
            bool pathValid = true, hiddenRecovery = false, landingSeen = false, portalExitSeen = false;
            // Batch별 대기 시간을 가정하지 않고, 실제 프레임이 기록 경로를 벗어나지 않는지 확인한다.
            for (int frame = 0; frame < 1000 && session.IsPresenting; frame++)
            {
                foreach (KeyValuePair<SpriteRenderer, List<SettlementRecord>> route in routes)
                {
                    SpriteRenderer image = route.Key;
                    if (!image.enabled) { hiddenRecovery |= collected.Contains(image); continue; }
                    float size = image.name.StartsWith("Obstacle-") ? .96f : .92f * BoardArtworkLayout.ContentScale(image.sprite);
                    Vector3 offset = new Vector3(BoardArtworkLayout.ContentOffsetX(image.sprite), BoardArtworkLayout.ContentOffsetY(image.sprite), 0) * size;
                    Vector3 position = board.transform.InverseTransformPoint(image.transform.position) - offset;
                    bool onPath = false;
                    foreach (SettlementRecord record in route.Value)
                    {
                        Vector3 start = PuzzleWorldBoard.CellPosition(record.Source), end = PuzzleWorldBoard.CellPosition(record.Target);
                        if (record.Kind == MovementKind.Portal)
                        {
                            onPath |= Vector3.Distance(position, start) < .001f || Vector3.Distance(position, end) < .001f;
                            portalExitSeen |= Vector3.Distance(position, end) < .001f && image.color.a > 0 && image.color.a < 1;
                        }
                        else
                        {
                            Vector3 delta = end - start;
                            float fraction = Mathf.Clamp01(Vector3.Dot(position - start, delta) / delta.sqrMagnitude);
                            onPath |= Vector3.Distance(position, start + fraction * delta) < .001f;
                        }
                    }
                    pathValid &= onPath;
                    float scale = size / Mathf.Max(image.sprite.bounds.size.x, image.sprite.bounds.size.y);
                    landingSeen |= image.transform.localScale.x > scale * 1.001f && image.transform.localScale.y < scale * .999f;
                }
                Tick(session, .01f);
            }
            Check(pathValid && !session.IsPresenting, name + " 동시 재생 모든 프레임이 기록 경로 유지·종료");
            if (collected.Count > 0) Check(hiddenRecovery, name + " 회수 도착 후 최종 Draw 전에 이미지 숨김");
            if (routes.Values.Any(route => route.Any(record => record.Kind == MovementKind.Portal)))
                Check(portalExitSeen, "포털 출구 중간 프레임·사이 칸 통과 없음");
            if (owners.Count > 0) Check(landingSeen, name + " 낙하 후 착지 중간 축척");
        }
    }
}
