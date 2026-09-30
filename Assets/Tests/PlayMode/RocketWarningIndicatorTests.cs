using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using JetpackRide.Hazards;
using JetpackRide.Pooling;

public class RocketWarningIndicatorTests
{
    private static readonly Vector2 Band = new(-3.5f, 3.5f);

    [UnityTest]
    public IEnumerator PlayAndDespawnAsync_DespawnsAfterLifetime()
    {
        var poolGo = new GameObject("Pool");
        var pool = poolGo.AddComponent<ObjectPoolManager>();
        var go = new GameObject("Warning");
        var indicator = go.AddComponent<RocketWarningIndicator>();
        indicator.Configure(pool, "rocketWarning");
        yield return null;

        bool completed = false;
        RunAsync();
        async void RunAsync()
        {
            await indicator.PlayAndDespawnAsync(0.05f);
            completed = true;
        }

        yield return new WaitForSeconds(0.2f);

        Assert.IsTrue(completed);
        Object.DestroyImmediate(go);
        Object.DestroyImmediate(poolGo);
    }

    private static (RocketWarningIndicator indicator, Transform player, GameObject poolGo) Build(float playerY)
    {
        var poolGo = new GameObject("Pool");
        var pool = poolGo.AddComponent<ObjectPoolManager>();
        var go = new GameObject("Warning");
        go.AddComponent<SpriteRenderer>();
        var indicator = go.AddComponent<RocketWarningIndicator>();
        indicator.Configure(pool, "rocketWarning");
        var player = new GameObject("Player").transform;
        player.position = new Vector3(-6f, playerY, 0f);
        return (indicator, player, poolGo);
    }

    [UnityTest]
    public IEnumerator TrackAndLockAsync_FollowsPlayerY_AtCappedSpeed()
    {
        var (indicator, player, poolGo) = Build(playerY: 3f);
        yield return null;

        float? locked = null;
        RunAsync();
        async void RunAsync() => locked = await indicator.TrackAndLockAsync(player, trackSeconds: 0.5f, lockSeconds: 0.05f, trackSpeed: 2f, Band);

        yield return new WaitForSeconds(0.25f);
        float midY = indicator.transform.position.y;
        Assert.Greater(midY, 0.1f, "moves toward the player");
        Assert.Less(midY, 1f, "but no faster than trackSpeed");

        yield return new WaitForSeconds(0.5f);
        Assert.IsTrue(locked.HasValue);
        Assert.AreEqual(1f, locked.Value, 0.15f, "0.5 s at 2 units/s");
        Object.DestroyImmediate(indicator.gameObject);
        Object.DestroyImmediate(player.gameObject);
        Object.DestroyImmediate(poolGo);
    }

    [UnityTest]
    public IEnumerator TrackAndLockAsync_ClampsToBand()
    {
        var (indicator, player, poolGo) = Build(playerY: 10f);
        yield return null;

        float? locked = null;
        RunAsync();
        async void RunAsync() => locked = await indicator.TrackAndLockAsync(player, 0.1f, 0.05f, trackSpeed: 1000f, Band);

        yield return new WaitForSeconds(0.3f);
        Assert.AreEqual(3.5f, locked.Value, 1e-3f);
        Object.DestroyImmediate(indicator.gameObject);
        Object.DestroyImmediate(player.gameObject);
        Object.DestroyImmediate(poolGo);
    }

    [UnityTest]
    public IEnumerator TrackAndLockAsync_LockedYIsStable_WhilePlayerKeepsMoving()
    {
        var (indicator, player, poolGo) = Build(playerY: 2f);
        yield return null;

        float? locked = null;
        RunAsync();
        async void RunAsync() => locked = await indicator.TrackAndLockAsync(player, 0.1f, lockSeconds: 0.4f, trackSpeed: 1000f, Band);

        yield return new WaitForSeconds(0.2f);
        Assert.IsTrue(indicator.IsLocked);
        float lockY = indicator.transform.position.y;
        player.position = new Vector3(-6f, -3f, 0f);
        yield return new WaitForSeconds(0.1f);
        Assert.AreEqual(lockY, indicator.transform.position.y, 1e-4f, "locked warning ignores the player");

        yield return new WaitForSeconds(0.3f);
        Assert.AreEqual(lockY, locked.Value, 1e-4f);
        Assert.AreEqual(2f, lockY, 1e-3f);
        Object.DestroyImmediate(indicator.gameObject);
        Object.DestroyImmediate(player.gameObject);
        Object.DestroyImmediate(poolGo);
    }

    [UnityTest]
    public IEnumerator TrackAndLockAsync_RaisesLockBlinked_OncePerRedFlash()
    {
        var (indicator, player, poolGo) = Build(playerY: 0f);
        yield return null;

        int blinks = 0;
        indicator.LockBlinked += () => blinks++;
        bool done = false;
        RunAsync();
        async void RunAsync()
        {
            await indicator.TrackAndLockAsync(player, 0.05f, lockSeconds: 0.3f, trackSpeed: 1000f, Band);
            done = true;
        }

        for (float t = 0f; t < 1f && !done; t += Time.deltaTime) yield return null;
        Assert.IsTrue(done);
        Assert.AreEqual(3, blinks, "0.3 s lock at 10 Hz = 3 red flashes");
        Object.DestroyImmediate(indicator.gameObject);
        Object.DestroyImmediate(player.gameObject);
        Object.DestroyImmediate(poolGo);
    }

    [UnityTest]
    public IEnumerator TrackAndLockAsync_DespawnedMidTrack_LeavesNextPoolReuseAlone()
    {
        var (indicator, player, poolGo) = Build(playerY: 3f);
        yield return null;

        RunAsync();
        async void RunAsync() => await indicator.TrackAndLockAsync(player, 0.2f, 0.05f, trackSpeed: 1000f, Band);

        yield return null;
        // A run reset despawns the warning and the pool hands it out again for a new, fixed warning.
        indicator.OnDespawned();
        indicator.OnSpawned();
        indicator.gameObject.SetActive(true);
        indicator.transform.position = new Vector3(8.5f, -2f, 0f);

        yield return new WaitForSeconds(0.4f);
        Assert.AreEqual(-2f, indicator.transform.position.y, 1e-4f, "stale tracking must not move the reused warning");
        Assert.IsTrue(indicator.gameObject.activeSelf, "stale call must not despawn the reused warning");
        Assert.IsFalse(indicator.IsLocked);
        Object.DestroyImmediate(indicator.gameObject);
        Object.DestroyImmediate(player.gameObject);
        Object.DestroyImmediate(poolGo);
    }
}
