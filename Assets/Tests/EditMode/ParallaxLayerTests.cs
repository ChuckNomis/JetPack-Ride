using NUnit.Framework;
using UnityEngine;
using JetpackRide.Environment;

public class ParallaxLayerTests
{
    [Test]
    public void ComputeWrappedPosition_MovesLeftByScaledSpeed()
    {
        var go = new GameObject("Layer");
        var layer = go.AddComponent<ParallaxLayer>();
        layer.ScrollSpeedMultiplier = 0.5f;
        layer.TileWidth = 20f;

        var result = layer.ComputeWrappedPosition(new Vector3(0f, 0f, 0f), baseScrollSpeed: 10f, deltaTime: 1f);

        Assert.AreEqual(-5f, result.x, 0.001f); // 10 * 0.5 * 1s
        Object.DestroyImmediate(go);
    }

    [Test]
    public void ComputeWrappedPosition_WrapsAfterPassingTileWidth()
    {
        var go = new GameObject("Layer");
        var layer = go.AddComponent<ParallaxLayer>();
        layer.ScrollSpeedMultiplier = 1f;
        layer.TileWidth = 20f;

        var result = layer.ComputeWrappedPosition(new Vector3(-19.5f, 0f, 0f), baseScrollSpeed: 10f, deltaTime: 1f);

        // -19.5 - 10 = -29.5, past -TileWidth (-20), wraps by +TileWidth
        Assert.AreEqual(-9.5f, result.x, 0.001f);
        Object.DestroyImmediate(go);
    }

    [Test]
    public void ComputeWrappedPosition_WithTileCountTwo_WrapsByDoubleTileWidth()
    {
        var go = new GameObject("Layer");
        var layer = go.AddComponent<ParallaxLayer>();
        layer.ScrollSpeedMultiplier = 1f;
        layer.TileWidth = 20f;
        layer.TileCount = 2;

        var result = layer.ComputeWrappedPosition(new Vector3(-19.5f, 0f, 0f), baseScrollSpeed: 10f, deltaTime: 1f);

        // -19.5 - 10 = -29.5, past -TileWidth (-20), wraps by +(TileWidth * TileCount) = +40
        Assert.AreEqual(10.5f, result.x, 0.001f);
        Object.DestroyImmediate(go);
    }
}
