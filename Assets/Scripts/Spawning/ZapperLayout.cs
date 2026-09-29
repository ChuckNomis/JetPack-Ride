using System.Collections.Generic;
using UnityEngine;
using JetpackRide.Hazards;

namespace JetpackRide.Spawning
{
    public struct ZapperPlacement
    {
        public ZapperOrientation Orientation;
        public float Length;
        // X is relative to the spawn point (the cluster's left edge sits on it); Y is world.
        public Vector2 Center;
    }

    // Pure zapper placement rules: which orientation/length to use, where the rotated beam fits, and
    // how a cluster is laid out so a flyable lane always survives.
    public static class ZapperLayout
    {
        public const float OrientationUnlockRamp = 0.2f;
        public const float LongLengthUnlockRamp = 0.4f;

        public static float AngleDegrees(ZapperOrientation orientation) => orientation switch
        {
            ZapperOrientation.Horizontal => 90f,
            ZapperOrientation.DiagonalUp => 45f,
            ZapperOrientation.DiagonalDown => -45f,
            _ => 0f,
        };

        // Vertical only early in a run; afterwards 40% vertical, 30% horizontal, 15% each diagonal.
        public static ZapperOrientation PickOrientation(float rampT, float random01)
        {
            if (rampT < OrientationUnlockRamp || random01 < 0.4f) return ZapperOrientation.Vertical;
            if (random01 < 0.7f) return ZapperOrientation.Horizontal;
            return random01 < 0.85f ? ZapperOrientation.DiagonalUp : ZapperOrientation.DiagonalDown;
        }

        // lengths is ordered short..long; the last entry only appears once the ramp passes LongLengthUnlockRamp.
        public static float PickLength(float rampT, float random01, float[] lengths)
        {
            int available = rampT < LongLengthUnlockRamp ? Mathf.Max(1, lengths.Length - 1) : lengths.Length;
            return lengths[Mathf.Min(available - 1, (int)(random01 * available))];
        }

        // AABB half-size of a beam of `length` (along local Y) and `thickness`, rotated by its orientation.
        public static Vector2 WorldExtents(ZapperOrientation orientation, float length, float thickness)
        {
            float rad = AngleDegrees(orientation) * Mathf.Deg2Rad;
            float cos = Mathf.Abs(Mathf.Cos(rad)), sin = Mathf.Abs(Mathf.Sin(rad));
            return new Vector2(thickness * cos + length * sin, thickness * sin + length * cos) * 0.5f;
        }

        public static float ClampCenterY(float y, float halfHeight, Vector2 yRange)
        {
            float lo = yRange.x + halfHeight, hi = yRange.y - halfHeight;
            return lo > hi ? (yRange.x + yRange.y) * 0.5f : Mathf.Clamp(y, lo, hi);
        }

        // Largest free stretch of `range` not covered by any (min, max) interval.
        public static float LargestGap(IReadOnlyList<Vector2> intervals, Vector2 range)
        {
            var sorted = new List<Vector2>(intervals);
            sorted.Sort((a, b) => a.x.CompareTo(b.x));
            float best = 0f, cursor = range.x;
            foreach (var iv in sorted)
            {
                best = Mathf.Max(best, Mathf.Min(iv.x, range.y) - cursor);
                cursor = Mathf.Max(cursor, iv.y);
            }
            return Mathf.Max(best, range.y - cursor);
        }

        // Lays out `count` zappers left to right, `xGap` apart edge to edge. Each member is re-rolled
        // until the union of their vertical spans still leaves a gap >= minLane; if that never happens
        // it repeats the previous member's shape and height, which can't shrink the lane.
        public static ZapperPlacement[] PlanCluster(int count, float rampT, Vector2 yRange, float thickness,
            float[] lengths, float minLane, float xGap, System.Func<float> random01)
        {
            const int attempts = 8;
            var result = new ZapperPlacement[count];
            var spans = new List<Vector2>(count);
            float rightEdge = 0f;

            for (int i = 0; i < count; i++)
            {
                ZapperPlacement? chosen = null;
                for (int attempt = 0; attempt < attempts && chosen == null; attempt++)
                {
                    var orientation = PickOrientation(rampT, random01());
                    float length = PickLength(rampT, random01(), lengths);
                    float halfH = WorldExtents(orientation, length, thickness).y;
                    float y = ClampCenterY(Mathf.Lerp(yRange.x, yRange.y, random01()), halfH, yRange);

                    spans.Add(new Vector2(y - halfH, y + halfH));
                    if (LargestGap(spans, yRange) >= minLane)
                        chosen = new ZapperPlacement { Orientation = orientation, Length = length, Center = new Vector2(0f, y) };
                    spans.RemoveAt(spans.Count - 1);
                }

                // First member fallback: a horizontal beam is only `thickness` tall, so it always leaves a lane.
                var placement = chosen ?? (i > 0
                    ? result[i - 1]
                    : new ZapperPlacement { Orientation = ZapperOrientation.Horizontal, Length = lengths[0], Center = new Vector2(0f, (yRange.x + yRange.y) * 0.5f) });

                var ext = WorldExtents(placement.Orientation, placement.Length, thickness);
                float left = i == 0 ? 0f : rightEdge + xGap;
                placement.Center = new Vector2(left + ext.x, placement.Center.y);
                rightEdge = left + 2f * ext.x;

                result[i] = placement;
                spans.Add(new Vector2(placement.Center.y - ext.y, placement.Center.y + ext.y));
            }
            return result;
        }
    }
}
