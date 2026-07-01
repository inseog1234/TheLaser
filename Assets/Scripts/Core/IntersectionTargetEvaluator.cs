using System.Collections.Generic;
using UnityEngine;

namespace Core
{
    public static class IntersectionTargetEvaluator
    {
        public readonly struct SegmentInput
        {
            public readonly Vector2 Start;
            public readonly Vector2 End;
            public readonly LaserColorKind Color;
            public readonly int BeamId;

            public SegmentInput(Vector2 start, Vector2 end, LaserColorKind color, int beamId)
            {
                Start = start;
                End = end;
                Color = color;
                BeamId = beamId;
            }
        }

        private sealed class IntersectionCluster
        {
            public Vector2 Point;
            public readonly Dictionary<int, LaserColorKind> BeamColors = new();
        }

        public static bool IsTargetActivated(
            Vector2 targetPoint,
            float detectionRadius,
            int requiredIntersectionCount,
            IReadOnlyList<LaserColorKind> requiredColors,
            bool requireDifferentColors,
            IReadOnlyList<SegmentInput> segments)
        {
            if (segments == null || segments.Count <= 1)
                return false;

            float safeRadius = Mathf.Max(0.01f, detectionRadius);
            int requiredCount = Mathf.Clamp(requiredIntersectionCount, 2, 3);
            List<IntersectionCluster> clusters = new();

            for (int a = 0; a < segments.Count; a++)
            {
                for (int b = a + 1; b < segments.Count; b++)
                {
                    SegmentInput segmentA = segments[a];
                    SegmentInput segmentB = segments[b];

                    if (segmentA.BeamId == segmentB.BeamId)
                        continue;

                    if (!TryGetSegmentIntersection(segmentA.Start, segmentA.End, segmentB.Start, segmentB.End, out Vector2 intersection))
                        continue;

                    if (Vector2.Distance(targetPoint, intersection) > safeRadius)
                        continue;

                    IntersectionCluster cluster = FindOrCreateCluster(clusters, intersection, safeRadius);
                    cluster.BeamColors[segmentA.BeamId] = segmentA.Color;
                    cluster.BeamColors[segmentB.BeamId] = segmentB.Color;
                }
            }

            for (int i = 0; i < clusters.Count; i++)
            {
                if (IsClusterMatched(clusters[i], requiredCount, requiredColors, requireDifferentColors))
                    return true;
            }

            return false;
        }

        private static IntersectionCluster FindOrCreateCluster(List<IntersectionCluster> clusters, Vector2 point, float mergeRadius)
        {
            for (int i = 0; i < clusters.Count; i++)
            {
                if (Vector2.Distance(clusters[i].Point, point) <= mergeRadius)
                    return clusters[i];
            }

            IntersectionCluster cluster = new IntersectionCluster { Point = point };
            clusters.Add(cluster);
            return cluster;
        }

        private static bool IsClusterMatched(
            IntersectionCluster cluster,
            int requiredCount,
            IReadOnlyList<LaserColorKind> requiredColors,
            bool requireDifferentColors)
        {
            if (cluster == null || cluster.BeamColors.Count < requiredCount)
                return false;

            List<LaserColorKind> availableColors = new(cluster.BeamColors.Values);

            if (requireDifferentColors && CountDistinctColors(availableColors) < requiredCount)
                return false;

            List<LaserColorKind> specificRequiredColors = BuildSpecificRequiredColors(requiredColors);
            if (specificRequiredColors.Count <= 0)
                return true;

            for (int i = 0; i < specificRequiredColors.Count; i++)
            {
                if (!availableColors.Remove(specificRequiredColors[i]))
                    return false;
            }

            return cluster.BeamColors.Count >= requiredCount;
        }

        private static List<LaserColorKind> BuildSpecificRequiredColors(IReadOnlyList<LaserColorKind> requiredColors)
        {
            List<LaserColorKind> result = new();
            if (requiredColors == null)
                return result;

            for (int i = 0; i < requiredColors.Count; i++)
            {
                if (requiredColors[i] != LaserColorKind.Default)
                    result.Add(requiredColors[i]);
            }

            return result;
        }

        private static int CountDistinctColors(List<LaserColorKind> colors)
        {
            HashSet<LaserColorKind> distinct = new();
            for (int i = 0; i < colors.Count; i++)
                distinct.Add(colors[i]);

            return distinct.Count;
        }

        private static bool TryGetSegmentIntersection(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2, out Vector2 intersection)
        {
            Vector2 p = a1;
            Vector2 r = a2 - p;
            Vector2 q = b1;
            Vector2 s = b2 - q;
            float cross = r.x * s.y - r.y * s.x;

            if (Mathf.Abs(cross) < 0.0001f)
            {
                intersection = Vector2.zero;
                return false;
            }

            Vector2 qp = q - p;
            float t = (qp.x * s.y - qp.y * s.x) / cross;
            float u = (qp.x * r.y - qp.y * r.x) / cross;

            if (t < 0f || t > 1f || u < 0f || u > 1f)
            {
                intersection = Vector2.zero;
                return false;
            }

            intersection = p + t * r;
            return true;
        }
    }
}
