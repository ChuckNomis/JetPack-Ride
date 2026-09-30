using NUnit.Framework;
using JetpackRide.UI;

public class TextFadePulseTests
{
    [Test]
    public void AlphaAt_StartOfCycle_IsFullyVisible()
    {
        Assert.AreEqual(1f, TextFadePulse.AlphaAt(0f, period: 2f, minAlpha: 0.2f, maxAlpha: 1f), 0.001f);
    }

    [Test]
    public void AlphaAt_HalfPeriod_IsFaintest()
    {
        Assert.AreEqual(0.2f, TextFadePulse.AlphaAt(1f, period: 2f, minAlpha: 0.2f, maxAlpha: 1f), 0.001f);
    }

    [Test]
    public void AlphaAt_FullPeriod_IsBackToFullyVisible()
    {
        Assert.AreEqual(1f, TextFadePulse.AlphaAt(2f, period: 2f, minAlpha: 0.2f, maxAlpha: 1f), 0.001f);
    }

    [Test]
    public void AlphaAt_StaysWithinRange()
    {
        for (float t = 0f; t < 5f; t += 0.07f)
        {
            float a = TextFadePulse.AlphaAt(t, period: 1.3f, minAlpha: 0.2f, maxAlpha: 1f);
            Assert.That(a, Is.InRange(0.2f - 0.0001f, 1f + 0.0001f));
        }
    }

    [Test]
    public void AlphaAt_ZeroPeriod_ReturnsMaxAlpha()
    {
        Assert.AreEqual(1f, TextFadePulse.AlphaAt(0.4f, period: 0f, minAlpha: 0.2f, maxAlpha: 1f), 0.001f);
    }
}
