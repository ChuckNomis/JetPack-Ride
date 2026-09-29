using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using JetpackRide.Core;
using JetpackRide.Player;

public class PlayerDeathAnimatorTests
{
    private const System.Reflection.BindingFlags Flags =
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

    private (PlayerDeathAnimator animator, SpriteRenderer renderer, GameManager manager, Sprite alive, Sprite dead) Build()
    {
        var managerGo = new GameObject("GameManager");
        var manager = managerGo.AddComponent<GameManager>();
        typeof(GameManager).GetField("config", Flags).SetValue(manager, ScriptableObject.CreateInstance<GameConfig>());

        var tex = new Texture2D(4, 4);
        var alive = Sprite.Create(tex, new Rect(0, 0, 4, 4), Vector2.one * 0.5f);
        var dead = Sprite.Create(tex, new Rect(0, 0, 2, 2), Vector2.one * 0.5f);

        var playerGo = new GameObject("Player");
        playerGo.AddComponent<Rigidbody2D>().gravityScale = 0f;
        var renderer = playerGo.AddComponent<SpriteRenderer>();
        renderer.sprite = alive;
        var animator = playerGo.AddComponent<PlayerDeathAnimator>();
        typeof(PlayerDeathAnimator).GetField("gameManager", Flags).SetValue(animator, manager);
        typeof(PlayerDeathAnimator).GetField("deadSprite", Flags).SetValue(animator, dead);
        return (animator, renderer, manager, alive, dead);
    }

    [UnityTest]
    public IEnumerator Death_ShowsDeadSprite_AndTumbles()
    {
        var (animator, renderer, manager, _, dead) = Build();
        yield return null;
        manager.BeginRun();

        manager.EndRun();
        Assert.AreSame(dead, renderer.sprite);
        Assert.Greater(animator.GetComponent<Rigidbody2D>().linearVelocity.y, 0f, "death hop");

        yield return new WaitForSeconds(0.15f);
        Assert.Greater(Mathf.Abs(Mathf.DeltaAngle(0f, animator.transform.eulerAngles.z)), 1f, "mid-tumble rotation");

        Object.DestroyImmediate(animator.gameObject);
        Object.DestroyImmediate(manager.gameObject);
    }

    [UnityTest]
    public IEnumerator ReturnToGetReady_RestoresAliveSprite_AndUprightPose()
    {
        var (animator, renderer, manager, alive, _) = Build();
        yield return null;
        manager.BeginRun();
        manager.EndRun();
        yield return new WaitForSeconds(0.1f);

        manager.ReturnToGetReady();
        yield return null;

        Assert.AreSame(alive, renderer.sprite);
        Assert.AreEqual(0f, Mathf.DeltaAngle(0f, animator.transform.eulerAngles.z), 1e-3f);

        Object.DestroyImmediate(animator.gameObject);
        Object.DestroyImmediate(manager.gameObject);
    }

    [UnityTest]
    public IEnumerator ChildVisual_TumblesTheChild_NotTheRoot()
    {
        var managerGo = new GameObject("GameManager");
        var manager = managerGo.AddComponent<GameManager>();
        typeof(GameManager).GetField("config", Flags).SetValue(manager, ScriptableObject.CreateInstance<GameConfig>());
        var tex = new Texture2D(4, 4);
        var alive = Sprite.Create(tex, new Rect(0, 0, 4, 4), Vector2.one * 0.5f);
        var dead = Sprite.Create(tex, new Rect(0, 0, 2, 2), Vector2.one * 0.5f);

        var playerGo = new GameObject("Player");
        playerGo.AddComponent<Rigidbody2D>().gravityScale = 0f;
        var visual = new GameObject("Visual");
        visual.transform.SetParent(playerGo.transform, false);
        var renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sprite = alive;
        var animator = playerGo.AddComponent<PlayerDeathAnimator>();
        typeof(PlayerDeathAnimator).GetField("gameManager", Flags).SetValue(animator, manager);
        typeof(PlayerDeathAnimator).GetField("deadSprite", Flags).SetValue(animator, dead);
        yield return null;
        manager.BeginRun();

        manager.EndRun();
        Assert.AreSame(dead, renderer.sprite);
        yield return new WaitForSeconds(0.15f);
        Assert.Greater(Mathf.Abs(Mathf.DeltaAngle(0f, visual.transform.localEulerAngles.z)), 1f, "child tumbles");
        Assert.AreEqual(0f, Mathf.DeltaAngle(0f, playerGo.transform.eulerAngles.z), 1e-3f, "root (collider) stays upright");

        manager.ReturnToGetReady();
        yield return null;
        Assert.AreSame(alive, renderer.sprite);
        Assert.AreEqual(0f, Mathf.DeltaAngle(0f, visual.transform.localEulerAngles.z), 1e-3f);

        Object.DestroyImmediate(playerGo);
        Object.DestroyImmediate(managerGo);
    }
}
