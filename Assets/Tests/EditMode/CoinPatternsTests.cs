using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using JetpackRide.Spawning;

public class CoinPatternsTests
{
    private const float S = 0.8f;

    private static IEnumerable<CoinPattern> AllShapes()
    {
        yield return CoinPatterns.Line(5, S);
        yield return CoinPatterns.Arc(7, S, 1.2f);
        yield return CoinPatterns.Arrow(3, S);
        yield return CoinPatterns.Box(4, 3, S);
        yield return CoinPatterns.Rect(3, 2, S);
    }

    private static void AssertWellFormed(CoinPattern p)
    {
        Assert.GreaterOrEqual(p.Offsets.Length, 2, "no single coins");
        var seen = new HashSet<Vector2Int>();
        foreach (var o in p.Offsets)
        {
            Assert.IsTrue(seen.Add(new Vector2Int(Mathf.RoundToInt(o.x * 1000), Mathf.RoundToInt(o.y * 1000))), $"duplicate coin at {o}");
            Assert.GreaterOrEqual(o.x, p.Bounds.xMin - 1e-4f);
            Assert.LessOrEqual(o.x, p.Bounds.xMax + 1e-4f);
            Assert.GreaterOrEqual(o.y, p.Bounds.yMin - 1e-4f);
            Assert.LessOrEqual(o.y, p.Bounds.yMax + 1e-4f);
        }
        Assert.AreEqual(0f, p.Bounds.xMin, 1e-4f, "pattern starts at x = 0");
        Assert.AreEqual(0f, p.Bounds.yMin, 1e-4f, "pattern sits on y = 0");
    }

    [Test]
    public void AllShapes_AreWellFormed()
    {
        foreach (var p in AllShapes()) AssertWellFormed(p);
    }

    [Test]
    public void Line_IsEvenlySpacedAndFlat()
    {
        var p = CoinPatterns.Line(4, S);
        Assert.AreEqual(4, p.Offsets.Length);
        Assert.AreEqual(3 * S, p.Bounds.width, 1e-4f);
        Assert.AreEqual(0f, p.Bounds.height, 1e-4f);
    }

    [Test]
    public void Arc_IsFilledHumpPeakingInTheMiddle()
    {
        // Column tops 0, 1.13, 1.6, 1.13, 0; each column hangs down from its top in steps of S
        // -> 1 + 2 + 3 + 2 + 1 coins.
        var p = CoinPatterns.Arc(5, S, 2 * S);
        Assert.AreEqual(9, p.Offsets.Length);
        Assert.AreEqual(4 * S, p.Bounds.width, 1e-4f);
        Assert.AreEqual(2 * S, p.Bounds.height, 1e-4f);

        var middle = new List<float>();
        foreach (var o in p.Offsets) if (Mathf.Abs(o.x - 2 * S) < 1e-4f) middle.Add(o.y);
        middle.Sort();
        Assert.AreEqual(3, middle.Count);
        Assert.AreEqual(0f, middle[0], 1e-4f);
        Assert.AreEqual(S, middle[1], 1e-4f);
        Assert.AreEqual(2 * S, middle[2], 1e-4f);
    }

    [Test]
    public void Arrow_IsFilledChevronPointingRight()
    {
        var p = CoinPatterns.Arrow(3, S); // filled columns of 5, 3, 1 -> 9 coins
        Assert.AreEqual(9, p.Offsets.Length);
        Assert.AreEqual(2 * S, p.Bounds.width, 1e-4f);
        Assert.AreEqual(4 * S, p.Bounds.height, 1e-4f);
        // The rightmost coin is the tip, vertically centred.
        var tip = p.Offsets[0];
        foreach (var o in p.Offsets) if (o.x > tip.x) tip = o;
        Assert.AreEqual(p.Bounds.center.y, tip.y, 1e-4f);
    }

    [Test]
    public void Box_IsFilledGrid()
    {
        var p = CoinPatterns.Box(4, 3, S);
        Assert.AreEqual(4 * 3, p.Offsets.Length);
        Assert.AreEqual(3 * S, p.Bounds.width, 1e-4f);
        Assert.AreEqual(2 * S, p.Bounds.height, 1e-4f);
    }

    [Test]
    public void Rect_IsFilledGrid()
    {
        var p = CoinPatterns.Rect(3, 2, S);
        Assert.AreEqual(6, p.Offsets.Length);
        Assert.AreEqual(2 * S, p.Bounds.width, 1e-4f);
        Assert.AreEqual(S, p.Bounds.height, 1e-4f);
    }

    [Test]
    public void Pick_EveryKindFromTheStart_AndAlwaysWellFormed()
    {
        var earlyKinds = new HashSet<CoinPatternKind>();
        var rng = new System.Random(3);
        for (int i = 0; i < 500; i++)
        {
            var early = CoinPatterns.Pick(0f, (float)rng.NextDouble(), S, 1.2f);
            AssertWellFormed(early);
            earlyKinds.Add(early.Kind);
            AssertWellFormed(CoinPatterns.Pick((float)rng.NextDouble(), (float)rng.NextDouble(), S, 1.2f));
        }
        Assert.AreEqual(5, earlyKinds.Count, "all shapes appear at the start of a run");
    }

    [Test]
    public void Pick_LateRun_ProducesEveryKind_AndBiggerPatterns()
    {
        var kinds = new HashSet<CoinPatternKind>();
        for (float r = 0f; r < 1f; r += 0.01f) kinds.Add(CoinPatterns.Pick(1f, r, S, 1.2f).Kind);
        Assert.AreEqual(5, kinds.Count);

        Assert.Greater(CoinPatterns.Pick(1f, 0.01f, S, 1.2f).Offsets.Length, CoinPatterns.Pick(0f, 0.01f, S, 1.2f).Offsets.Length,
            "same kind, bigger late in a run");
    }
}
