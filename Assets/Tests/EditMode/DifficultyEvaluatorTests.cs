using NUnit.Framework;
using UnityEngine;
using JetpackRide.Core;

public class DifficultyEvaluatorTests
{
    private GameConfig NewConfig()
    {
        var config = ScriptableObject.CreateInstance<GameConfig>();
        return config;
    }

    [Test]
    public void Evaluate_AtZeroDistance_ReturnsBaseValues()
    {
        var config = NewConfig();
        var snapshot = DifficultyEvaluator.Evaluate(0f, config);

        Assert.AreEqual(config.BaseScrollSpeed, snapshot.ScrollSpeed, 0.001f);
        Assert.AreEqual(config.BaseObstacleSpawnInterval, snapshot.ObstacleSpawnInterval, 0.001f);
        Assert.AreEqual(config.BaseRocketSpawnInterval, snapshot.RocketSpawnInterval, 0.001f);
        Assert.AreEqual(0f, snapshot.RampProgress01, 0.001f);
    }

    [Test]
    public void Evaluate_AtRampDistance_ReturnsFloorValues()
    {
        var config = NewConfig();
        var snapshot = DifficultyEvaluator.Evaluate(config.DifficultyRampDistance, config);

        Assert.AreEqual(config.MinObstacleSpawnInterval, snapshot.ObstacleSpawnInterval, 0.001f);
        Assert.AreEqual(config.MinRocketSpawnInterval, snapshot.RocketSpawnInterval, 0.001f);
        Assert.AreEqual(1f, snapshot.RampProgress01, 0.001f);
    }

    [Test]
    public void Evaluate_FarPastRampDistance_ClampsAtFloorAndMax()
    {
        var config = NewConfig();
        var snapshot = DifficultyEvaluator.Evaluate(config.DifficultyRampDistance * 10f, config);

        Assert.AreEqual(config.MinObstacleSpawnInterval, snapshot.ObstacleSpawnInterval, 0.001f);
        Assert.AreEqual(config.MinRocketSpawnInterval, snapshot.RocketSpawnInterval, 0.001f);
        Assert.LessOrEqual(snapshot.ScrollSpeed, config.MaxScrollSpeed + 0.001f);
        Assert.AreEqual(1f, snapshot.RampProgress01, 0.001f);
    }

    [Test]
    public void Evaluate_ScrollSpeed_NeverExceedsMax()
    {
        var config = NewConfig();
        var snapshot = DifficultyEvaluator.Evaluate(1_000_000f, config);
        Assert.LessOrEqual(snapshot.ScrollSpeed, config.MaxScrollSpeed + 0.001f);
    }

    [Test]
    public void Evaluate_NegativeDistance_ClampsRampProgressToZero()
    {
        var config = NewConfig();
        var snapshot = DifficultyEvaluator.Evaluate(-500f, config);
        Assert.AreEqual(0f, snapshot.RampProgress01, 0.001f);
        Assert.AreEqual(config.BaseObstacleSpawnInterval, snapshot.ObstacleSpawnInterval, 0.001f);
    }
}
