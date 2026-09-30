using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using JetpackRide.Core;
using JetpackRide.Player;

public class PlayerShadowTests
{
    private const System.Reflection.BindingFlags Flags =
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

    private static readonly Vector2 GroundSize = new(0.9f, 0.22f);
    private static readonly Vector2 DotSize = new(0.12f, 0.08f);
    private const float FeetOffset = -0.34f;

    private class Rig
    {
        public GameObject Go;
        public PlayerShadow Shadow;
        public SpriteRenderer PlayerRenderer;
        public GameManager Manager;

        public SpriteRenderer ShadowRenderer => Shadow.ShadowTransform.GetComponent<SpriteRenderer>();

        public void SetY(float y)
        {
            var p = Go.transform.position;
            Go.transform.position = new Vector3(p.x, y, p.z);
        }

        public void Destroy()
        {
            Object.DestroyImmediate(Go);
            Object.DestroyImmediate(Manager.gameObject);
        }
    }

    private static Rig Build()
    {
        var managerGo = new GameObject("GameManager");
        var manager = managerGo.AddComponent<GameManager>();
        var config = ScriptableObject.CreateInstance<GameConfig>();
        typeof(GameManager).GetField("config", Flags).SetValue(manager, config);

        var go = new GameObject("Player");
        go.transform.position = new Vector3(-6f, -3.5f, 0f);
        // Kinematic so gravity never moves the player away from the heights the tests set.
        go.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sortingOrder = 5;
        var controller = go.AddComponent<PlayerController>();
        typeof(PlayerController).GetField("config", Flags).SetValue(controller, config);
        typeof(PlayerController).GetField("gameManager", Flags).SetValue(controller, manager);
        controller.MinY = -3.5f;
        controller.MaxY = 3.5f;

        var shadow = go.AddComponent<PlayerShadow>();
        typeof(PlayerShadow).GetField("controller", Flags).SetValue(shadow, controller);
        typeof(PlayerShadow).GetField("gameManager", Flags).SetValue(shadow, manager);
        typeof(PlayerShadow).GetField("target", Flags).SetValue(shadow, renderer);
        typeof(PlayerShadow).GetField("feetOffset", Flags).SetValue(shadow, FeetOffset);
        typeof(PlayerShadow).GetField("groundSize", Flags).SetValue(shadow, GroundSize);
        typeof(PlayerShadow).GetField("dotSize", Flags).SetValue(shadow, DotSize);

        return new Rig { Go = go, Shadow = shadow, PlayerRenderer = renderer, Manager = manager };
    }

    [UnityTest]
    public IEnumerator OnFloor_FullOvalAtTheFeet_BehindThePlayer()
    {
        var rig = Build();
        yield return null;
        rig.Manager.BeginRun();
        yield return null;

        var t = rig.Shadow.ShadowTransform;
        Assert.AreEqual(0f, rig.Shadow.Height01, 1e-4f);
        Assert.AreEqual(GroundSize.x, t.localScale.x, 1e-4f);
        Assert.AreEqual(GroundSize.y, t.localScale.y, 1e-4f);
        Assert.AreEqual(-6f, t.position.x, 1e-4f);
        Assert.AreEqual(-3.5f + FeetOffset, t.position.y, 1e-4f);
        Assert.AreEqual(rig.PlayerRenderer.sortingOrder - 1, rig.ShadowRenderer.sortingOrder);
        Assert.IsTrue(rig.ShadowRenderer.enabled);
        rig.Destroy();
    }

    [UnityTest]
    public IEnumerator Climbing_ShrinksTheShadow_ToADotAtMaxHeight_WhileStayingOnTheFloor()
    {
        var rig = Build();
        yield return null;
        rig.Manager.BeginRun();

        float previousWidth = float.MaxValue;
        foreach (float y in new[] { -3.5f, -1f, 0f, 2f, 3.5f })
        {
            rig.SetY(y);
            yield return null;
            var t = rig.Shadow.ShadowTransform;
            Assert.Less(t.localScale.x, previousWidth + 1e-4f, $"shrinks as the player climbs (y={y})");
            Assert.AreEqual(-3.5f + FeetOffset, t.position.y, 1e-4f, "the shadow stays on the floor");
            previousWidth = t.localScale.x;
        }

        var dot = rig.Shadow.ShadowTransform.localScale;
        Assert.AreEqual(1f, rig.Shadow.Height01, 1e-4f);
        Assert.AreEqual(DotSize.x, dot.x, 1e-4f);
        Assert.AreEqual(DotSize.y, dot.y, 1e-4f);
        rig.Destroy();
    }

    [UnityTest]
    public IEnumerator MidHeight_IsBetweenOvalAndDot()
    {
        var rig = Build();
        yield return null;
        rig.Manager.BeginRun();
        rig.SetY(0f);
        yield return null;

        var scale = rig.Shadow.ShadowTransform.localScale;
        Assert.Greater(scale.x, DotSize.x);
        Assert.Less(scale.x, GroundSize.x);
        rig.Destroy();
    }

    [UnityTest]
    public IEnumerator TitleScreen_HidesTheShadow()
    {
        var rig = Build();
        yield return null;
        yield return null;
        Assert.AreEqual(GameState.GetReady, rig.Manager.CurrentState);
        Assert.IsFalse(rig.ShadowRenderer.enabled);
        rig.Destroy();
    }
}
