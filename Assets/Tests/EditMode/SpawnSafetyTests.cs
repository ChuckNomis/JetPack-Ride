using NUnit.Framework;
using UnityEngine;
using JetpackRide.Spawning;

public class SpawnSafetyTests
{
    private const float Margin = 1f;
    private const float DespawnX = -14f;

    private static Rect At(float x, float y, float w = 1f, float h = 1f) => new(x, y, w, h);

    [Test]
    public void SeparatedVertically_NeverOverlaps()
    {
        // 1.5 apart vertically, same X, same speed.
        Assert.IsFalse(SpawnSafety.WillOverlap(At(10f, 0f), 12f, At(10f, 2.5f), 12f, Margin, DespawnX));
    }

    [Test]
    public void WithinMarginVertically_AndSameX_Overlaps()
    {
        // 0.5 apart: touching counts as overlap when closer than the margin.
        Assert.IsTrue(SpawnSafety.WillOverlap(At(10f, 0f), 12f, At(10f, 1.5f), 12f, Margin, DespawnX));
    }

    [Test]
    public void SameSpeed_ApartInX_NeverMeets()
    {
        Assert.IsFalse(SpawnSafety.WillOverlap(At(10f, 0f), 12f, At(13f, 0f), 12f, Margin, DespawnX));
    }

    [Test]
    public void SameSpeed_WithinMarginInX_Overlaps()
    {
        Assert.IsTrue(SpawnSafety.WillOverlap(At(10f, 0f), 12f, At(11.5f, 0f), 12f, Margin, DespawnX));
    }

    [Test]
    public void FasterObjectBehind_CatchesUpOnScreen_Overlaps()
    {
        // Slow zapper ahead (x 10), fast coins behind (x 16) catch it before either leaves.
        Assert.IsTrue(SpawnSafety.WillOverlap(At(10f, 0f), 8f, At(16f, 0f), 12f, Margin, DespawnX));
    }

    [Test]
    public void FasterObjectBehind_CatchesUpOnlyAfterDespawn_DoesNotOverlap()
    {
        // Tiny speed difference: the gap only closes long after both have left the screen.
        Assert.IsFalse(SpawnSafety.WillOverlap(At(10f, 0f), 11.9f, At(16f, 0f), 12f, Margin, DespawnX));
    }

    [Test]
    public void SlowerObjectBehind_DriftsAway_DoesNotOverlap()
    {
        Assert.IsFalse(SpawnSafety.WillOverlap(At(10f, 0f), 12f, At(13f, 0f), 8f, Margin, DespawnX));
    }

    [Test]
    public void IsSymmetric()
    {
        var a = At(10f, 0f); var b = At(16f, 0f);
        Assert.AreEqual(SpawnSafety.WillOverlap(a, 8f, b, 12f, Margin, DespawnX), SpawnSafety.WillOverlap(b, 12f, a, 8f, Margin, DespawnX));
    }
}
