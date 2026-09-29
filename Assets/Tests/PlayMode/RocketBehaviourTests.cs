using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using JetpackRide.Hazards;

public class RocketBehaviourTests
{
    // Rockets no longer home in flight (Phase 9.5): the warning tracks, the rocket flies straight.
    [UnityTest]
    public IEnumerator Rocket_FliesStraight_EvenWithPlayerAbove()
    {
        var player = new GameObject("Player");
        player.transform.position = new Vector3(-5f, 5f, 0f);
        var go = new GameObject("Rocket");
        go.AddComponent<RocketBehaviour>();
        go.transform.position = Vector3.zero;
        yield return null;

        for (int i = 0; i < 10; i++) yield return new WaitForFixedUpdate();

        Assert.AreEqual(0f, go.transform.position.y, 0.001f);
        Assert.IsNull(typeof(RocketBehaviour).GetMethod("SetTarget"), "homing API removed");
        Object.DestroyImmediate(go);
        Object.DestroyImmediate(player);
    }
}
