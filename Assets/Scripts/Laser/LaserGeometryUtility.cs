using UnityEngine;

namespace Laser
{
    public static class LaserGeometryUtility
    {
        public static float DistancePointToSegment(Vector2 point, Vector2 segmentStart, Vector2 segmentEnd)
        {
            Vector2 ab = segmentEnd - segmentStart;
            float lengthSqr = ab.sqrMagnitude;

            if (lengthSqr <= Mathf.Epsilon)
                return Vector2.Distance(point, segmentStart);

            float t = Vector2.Dot(point - segmentStart, ab) / lengthSqr;
            t = Mathf.Clamp01(t);

            Vector2 closest = segmentStart + ab * t;
            return Vector2.Distance(point, closest);
        }

        public static bool TryGetSegmentIntersection(Vector2 a, Vector2 b, Vector2 c, Vector2 d, out Vector2 intersection)
        {
            intersection = Vector2.zero;

            Vector2 r = b - a;
            Vector2 s = d - c;
            float denominator = Cross(r, s);

            if (Mathf.Abs(denominator) <= 0.0001f)
                return false;

            float t = Cross(c - a, s) / denominator;
            float u = Cross(c - a, r) / denominator;

            if (t < 0f || t > 1f || u < 0f || u > 1f)
                return false;

            intersection = a + t * r;
            return true;
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }
    }
}
