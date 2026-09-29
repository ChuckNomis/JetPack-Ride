using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using JetpackRide.Hazards;

public class ZapperShapeTests
{
    private static ZapperShape Build(float scale = 3f)
    {
        var tex = new Texture2D(46, 110);
        var sprite = Sprite.Create(tex, new Rect(0, 0, 46, 110), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(0, 32, 0, 32));
        var go = new GameObject("Zapper");
        go.transform.localScale = new Vector3(scale, scale, 1f);
        go.AddComponent<SpriteRenderer>().sprite = sprite;
        go.AddComponent<BoxCollider2D>();
        return go.AddComponent<ZapperShape>();
    }

    [UnityTest]
    public IEnumerator Configure_SetsSlicedLengthColliderAndRotation()
    {
        var shape = Build();
        yield return null;

        shape.Configure(ZapperOrientation.DiagonalUp, 6f);

        var renderer = shape.GetComponent<SpriteRenderer>();
        var collider = shape.GetComponent<BoxCollider2D>();
        Assert.AreEqual(SpriteDrawMode.Sliced, renderer.drawMode);
        Assert.AreEqual(2f, renderer.size.y, 1e-4f, "6 world units at scale 3");
        Assert.AreEqual(0.46f, renderer.size.x, 1e-4f, "width unchanged");
        Assert.Less(collider.size.x, renderer.size.x, "collider hugs the beam, not the glow");
        Assert.Less(collider.size.y, renderer.size.y);
        Assert.Greater(collider.size.y, renderer.size.y * 0.8f);
        Assert.AreEqual(45f, shape.transform.eulerAngles.z, 1e-3f);
        Assert.AreEqual(collider.size.x * 3f, shape.WorldThickness, 1e-4f);
    }

    [UnityTest]
    public IEnumerator OnSpawned_ResetsToVerticalNativeLength()
    {
        var shape = Build();
        yield return null;
        shape.Configure(ZapperOrientation.Horizontal, 6f);

        shape.OnSpawned();

        Assert.AreEqual(1.1f, shape.GetComponent<SpriteRenderer>().size.y, 1e-4f);
        Assert.AreEqual(0f, shape.transform.eulerAngles.z, 1e-3f);
    }

    [UnityTest]
    public IEnumerator Frames_CycleOverTime()
    {
        var shape = Build();
        var other = Sprite.Create(new Texture2D(46, 110), new Rect(0, 0, 46, 110), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(0, 32, 0, 32));
        var first = shape.GetComponent<SpriteRenderer>().sprite;
        shape.SetFrames(new[] { first, other }, framesPerSecond: 30f);

        yield return new WaitForSeconds(0.1f);

        var renderer = shape.GetComponent<SpriteRenderer>();
        bool sawOther = false;
        for (int i = 0; i < 10 && !sawOther; i++)
        {
            if (renderer.sprite == other) sawOther = true;
            yield return null;
        }
        Assert.IsTrue(sawOther);
    }
}
