using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using JetpackRide.Hazards;
using JetpackRide.Spawning;

public class ZapperLayoutTests
{
    private static readonly Vector2 Band = new(-3.5f, 3.5f);
    private static readonly float[] Lengths = { 2.4f, 3.3f, 4.4f };
    private const float Thickness = 0.8f;
    private const float MinLane = 1.3f;

    [Test]
    public void PickOrientation_EarlyRun_IsAlwaysVertical()
    {
        for (float r = 0f; r < 1f; r += 0.05f)
            Assert.AreEqual(ZapperOrientation.Vertical, ZapperLayout.PickOrientation(0.1f, r));
    }

    [Test]
    public void PickOrientation_AfterUnlock_ProducesEveryOrientation()
    {
        var seen = new HashSet<ZapperOrientation>();
        for (float r = 0f; r < 1f; r += 0.01f) seen.Add(ZapperLayout.PickOrientation(0.5f, r));
        Assert.AreEqual(4, seen.Count);
    }

    [Test]
    public void PickLength_LongOnlyUnlocksLater()
    {
        for (float r = 0f; r < 1f; r += 0.05f)
            Assert.Less(ZapperLayout.PickLength(0.1f, r, Lengths), 4.4f);

        bool sawLong = false;
        for (float r = 0f; r < 1f; r += 0.05f)
            if (Mathf.Approximately(ZapperLayout.PickLength(0.9f, r, Lengths), 4.4f)) sawLong = true;
        Assert.IsTrue(sawLong);
    }

    [Test]
    public void AngleDegrees_MatchesOrientation()
    {
        Assert.AreEqual(0f, ZapperLayout.AngleDegrees(ZapperOrientation.Vertical));
        Assert.AreEqual(90f, ZapperLayout.AngleDegrees(ZapperOrientation.Horizontal));
        Assert.AreEqual(45f, ZapperLayout.AngleDegrees(ZapperOrientation.DiagonalUp));
        Assert.AreEqual(-45f, ZapperLayout.AngleDegrees(ZapperOrientation.DiagonalDown));
    }

    [Test]
    public void WorldExtents_IsHalfSizeOfRotatedBeam()
    {
        var v = ZapperLayout.WorldExtents(ZapperOrientation.Vertical, 4f, 1f);
        Assert.AreEqual(0.5f, v.x, 1e-4f);
        Assert.AreEqual(2f, v.y, 1e-4f);

        var h = ZapperLayout.WorldExtents(ZapperOrientation.Horizontal, 4f, 1f);
        Assert.AreEqual(2f, h.x, 1e-4f);
        Assert.AreEqual(0.5f, h.y, 1e-4f);

        var d = ZapperLayout.WorldExtents(ZapperOrientation.DiagonalUp, 4f, 1f);
        float expected = (1f + 4f) * Mathf.Sqrt(0.5f) / 2f;
        Assert.AreEqual(expected, d.x, 1e-4f);
        Assert.AreEqual(expected, d.y, 1e-4f);
    }

    [Test]
    public void ClampCenterY_KeepsWholeBeamInsideBand()
    {
        Assert.AreEqual(1.5f, ZapperLayout.ClampCenterY(3.4f, 2f, Band), 1e-4f);
        Assert.AreEqual(-1.5f, ZapperLayout.ClampCenterY(-9f, 2f, Band), 1e-4f);
        Assert.AreEqual(0.3f, ZapperLayout.ClampCenterY(0.3f, 2f, Band), 1e-4f);
        Assert.AreEqual(0f, ZapperLayout.ClampCenterY(3f, 5f, Band), 1e-4f, "too tall for the band: centred");
    }

    [Test]
    public void LargestGap_FindsBiggestFreeStretch()
    {
        var intervals = new List<Vector2> { new(-1f, 1f), new(0.5f, 2f) };
        Assert.AreEqual(2.5f, ZapperLayout.LargestGap(intervals, Band), 1e-4f, "-3.5..-1 is the biggest gap");
        Assert.AreEqual(7f, ZapperLayout.LargestGap(new List<Vector2>(), Band), 1e-4f);
    }

    [Test]
    public void PlanCluster_AlwaysLeavesAFlyableLane()
    {
        var rng = new System.Random(42);
        for (int trial = 0; trial < 2000; trial++)
        {
            int count = 1 + trial % 2;
            float rampT = (float)rng.NextDouble();
            var cluster = ZapperLayout.PlanCluster(count, rampT, Band, Thickness, Lengths, MinLane, 1f, () => (float)rng.NextDouble());

            Assert.AreEqual(count, cluster.Length);
            var intervals = new List<Vector2>();
            foreach (var z in cluster)
            {
                var ext = ZapperLayout.WorldExtents(z.Orientation, z.Length, Thickness);
                Assert.GreaterOrEqual(z.Center.y - ext.y, Band.x - 1e-4f, "beam inside band");
                Assert.LessOrEqual(z.Center.y + ext.y, Band.y + 1e-4f, "beam inside band");
                intervals.Add(new Vector2(z.Center.y - ext.y, z.Center.y + ext.y));
            }
            Assert.GreaterOrEqual(ZapperLayout.LargestGap(intervals, Band), MinLane - 1e-4f, $"trial {trial}");
        }
    }

    [Test]
    public void PlanCluster_MembersDoNotOverlapInX()
    {
        var rng = new System.Random(7);
        for (int trial = 0; trial < 500; trial++)
        {
            var cluster = ZapperLayout.PlanCluster(2, 1f, Band, Thickness, Lengths, MinLane, 1f, () => (float)rng.NextDouble());
            var a = ZapperLayout.WorldExtents(cluster[0].Orientation, cluster[0].Length, Thickness);
            var b = ZapperLayout.WorldExtents(cluster[1].Orientation, cluster[1].Length, Thickness);
            Assert.GreaterOrEqual(cluster[1].Center.x - b.x - (cluster[0].Center.x + a.x), 1f - 1e-4f);
            Assert.AreEqual(0f, cluster[0].Center.x - a.x, 1e-4f, "cluster starts at the spawn point");
        }
    }
}
