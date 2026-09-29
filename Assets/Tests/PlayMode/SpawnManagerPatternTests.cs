using NUnit.Framework;
using JetpackRide.Spawning;

public class SpawnManagerPatternTests
{
    [Test]
    public void DetermineObstacleClusterSize_LowRamp_ReturnsOne()
    {
        Assert.AreEqual(1, SpawnManager.DetermineObstacleClusterSize(0.1f));
    }

    [Test]
    public void DetermineObstacleClusterSize_HighRamp_ReturnsTwo()
    {
        Assert.AreEqual(2, SpawnManager.DetermineObstacleClusterSize(0.8f));
    }

    [Test]
    public void DetermineObstacleClusterSize_AtThreshold_ReturnsOne()
    {
        Assert.AreEqual(1, SpawnManager.DetermineObstacleClusterSize(0.6f));
    }
}
