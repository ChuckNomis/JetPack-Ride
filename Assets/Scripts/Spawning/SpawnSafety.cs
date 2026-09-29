using UnityEngine;

namespace JetpackRide.Spawning
{
    // Clearance rule between scrolling objects (coins vs zappers). Both move left at their own
    // constant speed; zappers may drift relative to coins when GameConfig.ZapperSpeedMultiplier != 1.
    public static class SpawnSafety
    {
        // True if a and b come closer than `margin` (touching included) at any time before either
        // has scrolled past despawnX.
        public static bool WillOverlap(Rect a, float speedA, Rect b, float speedB, float margin, float despawnX)
        {
            if (a.yMax + margin <= b.yMin || b.yMax + margin <= a.yMin) return false;

            float horizon = Mathf.Max(0f, Mathf.Min(TimeToLeave(a, speedA, despawnX), TimeToLeave(b, speedB, despawnX)));

            // With dv = speedA - speedB, the margin-inflated X intervals intersect while
            // lower < dv * t < upper. Over t in [0, horizon], dv * t sweeps [min(0, dv*h), max(0, dv*h)].
            float dv = speedA - speedB;
            float lower = a.xMin - b.xMax - margin;
            float upper = a.xMax + margin - b.xMin;
            float sweep = float.IsInfinity(horizon) ? (dv == 0f ? 0f : Mathf.Sign(dv) * float.MaxValue) : dv * horizon;
            float lo = Mathf.Min(0f, sweep), hi = Mathf.Max(0f, sweep);
            return lo < upper && hi > lower;
        }

        private static float TimeToLeave(Rect r, float speed, float despawnX) =>
            speed > 0f ? (r.xMax - despawnX) / speed : float.PositiveInfinity;
    }
}
