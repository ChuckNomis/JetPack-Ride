# Start Intro Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Pressing start on the title screen plays a ~1.9 s intro (start-explosion sound + camera shake, short pause, player walks in from off-screen left while the world stands still) before the run begins.

**Architecture:** A new `GameState.Intro` sits between `GetReady` and `Running`. `GameManager.StartIntro()` enters it; an `IntroSequence` component on the Player observes the state, runs an async `Awaitable` sequence (place player off-screen, `CameraShake`, pause, walk to home X) and then calls `GameManager.BeginRun()`. Everything that should stay idle (spawning, distance, background scroll, jetpack input) already only runs in `Running`; UI, audio and player visuals observe `Intro` like they observe the other states.

**Tech Stack:** Unity 6000.3.20f1, C# (`JetpackRide.Runtime` assembly), `UnityEngine.Awaitable`, NUnit PlayMode tests (`JetpackRide.PlayModeTests`), editor tooling in `Assets/Editor/PrefabBuilder.cs`.

**Spec:** `docs/superpowers/plans/2026-09-29-game-feel.md` — section **Task 9.8: Start intro (explosion, camera shake, walk-in)**.

## Global Constraints

- Unity editor: `C:\Program Files\Unity\Hub\Editor\6000.3.20f1\Editor\Unity.exe`. Batchmode tests need Unity **closed** (check `Temp/UnityLockfile` does not exist); otherwise run the same tests from Window > General > Test Runner.
- Async flows use `Awaitable` (+ `AwaitableExtensions.Forget()`), not coroutines, in runtime code.
- Event wiring in `Start`/`OnDestroy` (not `OnEnable`) so serialized or test-injected references are set first — same as every existing observer.
- Intro timings (spec): start X ≈ -11, shake 0.4 s, shake strength 0.3, pause 0.3 s, walk 1.2 s, home X = player's scene X (-6). All serialized on `IntroSequence`.
- The world stands still during the intro; gameplay music starts at `Running` (unchanged); menu music stops at `Intro`.
- Existing callers of `GameManager.BeginRun()` (many tests) must keep working unchanged.
- Never commit `Assets/Art/Fonts/New Athletic M54 SDF.asset` (TextMeshPro runtime glyph noise). After any batchmode run, if `git diff ProjectSettings/ProjectSettings.asset` is empty but the file shows as modified (line endings only), `git checkout -- ProjectSettings/ProjectSettings.asset`.
- Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

**Running tests (batchmode, Unity closed)** — from the repo root in Git Bash:

```bash
UNITY="/c/Program Files/Unity/Hub/Editor/6000.3.20f1/Editor/Unity.exe"
OUT="$TEMP/jr-tests"; mkdir -p "$OUT"
"$UNITY" -batchmode -projectPath . -runTests -testPlatform PlayMode -testFilter "<Filter>" -testResults "$OUT/r.xml" -logFile "$OUT/r.log"; echo "exit $?"
grep -o '<test-run [^>]*' "$OUT/r.xml" | grep -o 'total="[0-9]*"\|passed="[0-9]*"\|failed="[0-9]*"'
```

Exit code 0 = all passed, 2 = test failures, 1 = compile/other error (read `r.log`). `-testFilter` takes a class name (e.g. `IntroSequenceTests`) or a `;`-separated list. Use `-testPlatform EditMode` for EditMode.

## Review Focus

1. Pressing start (or thrust) again during the intro must do nothing — no second intro, no skipped intro. → Task 4 test `RestartPressed_DuringIntro_IsIgnored`.
2. Restart after dying mid-air: the next intro must still start the player on the floor off-screen left, not in the air. → Task 3 test starts the player at y = 2 and asserts floor Y.
3. The camera must end exactly where it started, even if a shake is started while another is running or the camera is disabled mid-shake. → Task 2 tests `Shake_Overlapping_RestoresOriginalRest` and `Disable_MidShake_RestoresRest`.
4. Leaving `Intro` mid-sequence (state change) or destroying the intro object (scene unload) must not call `BeginRun()` later or throw. → Task 3 tests `LeavingIntro_MidSequence_DoesNotBeginRun` and `Destroyed_MidSequence_DoesNotBeginRun`.
5. A scene without a `CameraShake` wired must still complete the intro. → Task 3 test `NoCameraShake_StillBeginsRun`.

---

## File Structure

- Modify `Assets/Scripts/Core/GameState.cs` — add `Intro`.
- Modify `Assets/Scripts/Core/GameManager.cs` — `StartIntro()`, shared `ResetRunStats()`.
- Create `Assets/Scripts/Environment/CameraShake.cs` — camera jitter with exact restore.
- Create `Assets/Scripts/Core/IntroSequence.cs` — the timed intro; the only thing that calls `BeginRun()` from `Intro`.
- Modify `Assets/Scripts/Player/RestartController.cs` — `GetReady` + start → `StartIntro()`.
- Modify `Assets/Scripts/Player/PlayerVisuals.cs` — run pose in `Intro`.
- Modify `Assets/Scripts/UI/UIManager.cs` — `Intro` hides all panels (falls out of existing `ShowPanelFor`; test only).
- Modify `Assets/Scripts/Audio/AudioManager.cs` — `Intro`: stop music, play `startExplosionSfx`.
- Modify `Assets/Editor/PrefabBuilder.cs` — menu item **Jetpack Ride/Wire Start Intro**.
- Scene `Assets/Scenes/MainGame.unity` — changed only by that menu item.
- Tests: `Assets/Tests/PlayMode/GameManagerTests.cs`, `CameraShakeTests.cs` (new), `IntroSequenceTests.cs` (new), `RestartControllerTests.cs`, `PlayerVisualsTests.cs`, `UIManagerTests.cs`, `AudioManagerTests.cs`.

---

### Task 1: `GameState.Intro` and `GameManager.StartIntro()`

**Files:**
- Modify: `Assets/Scripts/Core/GameState.cs`
- Modify: `Assets/Scripts/Core/GameManager.cs`
- Test: `Assets/Tests/PlayMode/GameManagerTests.cs`

**Interfaces:**
- Produces: `GameState.Intro` (enum value, declared between `GetReady` and `Running`); `public void GameManager.StartIntro()` — resets `DistanceMeters`, `CoinsThisRun`, `Score` to 0 and sets state `Intro` (raises `StateChanged(GameState.Intro)`). `BeginRun()` unchanged in behaviour.

- [ ] **Step 1: Write the failing tests** — add to `GameManagerTests` (uses its existing `NewManager()`):

```csharp
    [UnityTest]
    public IEnumerator StartIntro_ResetsStatsAndEntersIntro()
    {
        var manager = NewManager();
        yield return null;
        manager.BeginRun();
        manager.AddDistance(50f);
        manager.CollectCoin();
        manager.EndRun();
        manager.ReturnToGetReady();

        GameState? raised = null;
        manager.StateChanged += s => raised = s;
        manager.StartIntro();

        Assert.AreEqual(GameState.Intro, manager.CurrentState);
        Assert.AreEqual(GameState.Intro, raised);
        Assert.AreEqual(0f, manager.DistanceMeters);
        Assert.AreEqual(0, manager.CoinsThisRun);
        Assert.AreEqual(0, manager.Score);
        Object.DestroyImmediate(manager.gameObject);
    }

    [UnityTest]
    public IEnumerator DuringIntro_DistanceAndCoinsAreIgnored_ThenBeginRunEntersRunning()
    {
        var manager = NewManager();
        yield return null;
        manager.StartIntro();

        manager.AddDistance(10f);
        manager.CollectCoin();
        Assert.AreEqual(0f, manager.DistanceMeters);
        Assert.AreEqual(0, manager.CoinsThisRun);

        manager.BeginRun();
        Assert.AreEqual(GameState.Running, manager.CurrentState);
        Object.DestroyImmediate(manager.gameObject);
    }
```

- [ ] **Step 2: Run to verify failure** — Filter `GameManagerTests`. Expected: exit 1, compile error `'GameState' does not contain a definition for 'Intro'` / `'GameManager' does not contain a definition for 'StartIntro'`.

- [ ] **Step 3: Implement.** `GameState.cs`:

```csharp
namespace JetpackRide.Core
{
    public enum GameState
    {
        GetReady,
        Intro,
        Running,
        GameOver
    }
}
```

`GameManager.cs` — replace `BeginRun()` with:

```csharp
        // Title -> intro (IntroSequence plays it, then calls BeginRun). Stats reset here so the HUD
        // never shows the previous run's numbers once Running starts.
        public void StartIntro()
        {
            ResetRunStats();
            SetState(GameState.Intro);
        }

        public void BeginRun()
        {
            ResetRunStats();
            SetState(GameState.Running);
        }

        private void ResetRunStats()
        {
            DistanceMeters = 0f;
            CoinsThisRun = 0;
            Score = 0;
        }
```

- [ ] **Step 4: Run to verify pass** — Filter `GameManagerTests`. Expected: exit 0, all passed.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Core/GameState.cs Assets/Scripts/Core/GameManager.cs Assets/Tests/PlayMode/GameManagerTests.cs
git commit -m "feat: GameState.Intro and GameManager.StartIntro (Task 9.8)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: `CameraShake`

**Files:**
- Create: `Assets/Scripts/Environment/CameraShake.cs`
- Test: `Assets/Tests/PlayMode/CameraShakeTests.cs` (new)

**Interfaces:**
- Produces: `namespace JetpackRide.Environment`, `public class CameraShake : MonoBehaviour` with `public Awaitable Shake(float seconds, float strength)` (jitters `transform.localPosition` by a random offset up to `strength`, fading linearly to 0, then restores the exact rest position) and `public bool IsShaking { get; }`.

- [ ] **Step 1: Write the failing tests** — create `Assets/Tests/PlayMode/CameraShakeTests.cs`:

```csharp
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using JetpackRide.Core;
using JetpackRide.Environment;

public class CameraShakeTests
{
    private static readonly Vector3 Rest = new(1f, 2f, -10f);

    private static CameraShake Build()
    {
        var go = new GameObject("Camera");
        go.transform.localPosition = Rest;
        return go.AddComponent<CameraShake>();
    }

    [UnityTest]
    public IEnumerator Shake_MovesCamera_ThenRestoresExactRest()
    {
        var shake = Build();
        yield return null;

        shake.Shake(0.2f, 0.5f).Forget();
        float maxOffset = 0f;
        for (float t = 0f; t < 0.1f; t += Time.deltaTime)
        {
            yield return null;
            maxOffset = Mathf.Max(maxOffset, (shake.transform.localPosition - Rest).magnitude);
        }
        Assert.Greater(maxOffset, 0.01f, "camera moves while shaking");
        Assert.LessOrEqual(maxOffset, 0.5f + 1e-4f, "never further than strength");

        yield return new WaitForSeconds(0.3f);
        Assert.IsFalse(shake.IsShaking);
        Assert.AreEqual(Rest, shake.transform.localPosition);
        Object.DestroyImmediate(shake.gameObject);
    }

    [UnityTest]
    public IEnumerator Shake_Overlapping_RestoresOriginalRest()
    {
        var shake = Build();
        yield return null;

        shake.Shake(0.2f, 0.5f).Forget();
        yield return new WaitForSeconds(0.05f);
        shake.Shake(0.1f, 0.5f).Forget(); // starts while displaced

        yield return new WaitForSeconds(0.4f);
        Assert.AreEqual(Rest, shake.transform.localPosition);
        Object.DestroyImmediate(shake.gameObject);
    }

    [UnityTest]
    public IEnumerator Disable_MidShake_RestoresRest()
    {
        var shake = Build();
        yield return null;

        shake.Shake(1f, 0.5f).Forget();
        yield return new WaitForSeconds(0.05f);
        shake.enabled = false;

        Assert.AreEqual(Rest, shake.transform.localPosition);
        yield return new WaitForSeconds(0.1f);
        Assert.AreEqual(Rest, shake.transform.localPosition, "stale shake loop leaves the camera alone");
        Object.DestroyImmediate(shake.gameObject);
    }
}
```

- [ ] **Step 2: Run to verify failure** — Filter `CameraShakeTests`. Expected: exit 1, compile error `The type or namespace name 'CameraShake' could not be found`.

- [ ] **Step 3: Implement** — create `Assets/Scripts/Environment/CameraShake.cs`:

```csharp
using UnityEngine;

namespace JetpackRide.Environment
{
    // Jitters the camera around its rest position with a linearly fading random offset, then puts it
    // back exactly. A shake started while another runs keeps the original rest position; a newer
    // shake (or disabling the component) makes older loops stop touching the transform.
    public class CameraShake : MonoBehaviour
    {
        private Vector3 restPosition;
        private int generation;

        public bool IsShaking { get; private set; }

        public async Awaitable Shake(float seconds, float strength)
        {
            int gen = ++generation;
            if (!IsShaking) restPosition = transform.localPosition;
            IsShaking = true;

            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                if (this == null || !enabled || gen != generation) return;
                float falloff = 1f - t / seconds;
                transform.localPosition = restPosition + (Vector3)(Random.insideUnitCircle * strength * falloff);
                await Awaitable.NextFrameAsync();
            }

            if (this == null || !enabled || gen != generation) return;
            Restore();
        }

        private void OnDisable()
        {
            if (!IsShaking) return;
            generation++;
            Restore();
        }

        private void Restore()
        {
            transform.localPosition = restPosition;
            IsShaking = false;
        }
    }
}
```

- [ ] **Step 4: Run to verify pass** — Filter `CameraShakeTests`. Expected: exit 0, 3 passed.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Environment/CameraShake.cs Assets/Scripts/Environment/CameraShake.cs.meta Assets/Tests/PlayMode/CameraShakeTests.cs Assets/Tests/PlayMode/CameraShakeTests.cs.meta
git commit -m "feat: CameraShake with exact rest restore (Task 9.8)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

(`.meta` files are generated by Unity on the test run; if one is missing, include whatever `git status` shows for the new files.)

---

### Task 3: `IntroSequence`

**Files:**
- Create: `Assets/Scripts/Core/IntroSequence.cs`
- Test: `Assets/Tests/PlayMode/IntroSequenceTests.cs` (new)

**Interfaces:**
- Consumes: `GameState.Intro`, `GameManager.StartIntro()`, `GameManager.BeginRun()`, `GameManager.StateChanged` (Task 1); `CameraShake.Shake(float, float)` (Task 2); `PlayerController.MinY` (existing public field/property on `JetpackRide.Player.PlayerController`).
- Produces: `namespace JetpackRide.Core`, `public class IntroSequence : MonoBehaviour` with serialized fields `gameManager` (`GameManager`), `player` (`PlayerController`), `cameraShake` (`CameraShake`, optional), `startX` = -11, `shakeSeconds` = 0.4, `shakeStrength` = 0.3, `pauseSeconds` = 0.3, `walkSeconds` = 1.2; `public float HomeX { get; }` (player X captured in `Start`). Task 6 wires these field names by string.

- [ ] **Step 1: Write the failing tests** — create `Assets/Tests/PlayMode/IntroSequenceTests.cs`:

```csharp
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using JetpackRide.Core;
using JetpackRide.Environment;
using JetpackRide.Player;

public class IntroSequenceTests
{
    private const System.Reflection.BindingFlags Flags =
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

    private class Rig
    {
        public GameManager Manager;
        public PlayerController Player;
        public Rigidbody2D Body;
        public CameraShake Shake;
        public IntroSequence Intro;

        public void Destroy()
        {
            if (Intro != null) Object.DestroyImmediate(Intro.gameObject);
            if (Shake != null) Object.DestroyImmediate(Shake.gameObject);
            Object.DestroyImmediate(Player.gameObject);
            Object.DestroyImmediate(Manager.gameObject);
        }
    }

    // Player starts in the air (as after a mid-air death) at the scene home X.
    private static Rig Build(bool withShake = true)
    {
        var manager = new GameObject("GameManager").AddComponent<GameManager>();
        var config = ScriptableObject.CreateInstance<GameConfig>();
        typeof(GameManager).GetField("config", Flags).SetValue(manager, config);

        var playerGo = new GameObject("Player");
        playerGo.transform.position = new Vector3(-6f, 2f, 0f);
        var body = playerGo.AddComponent<Rigidbody2D>();
        var player = playerGo.AddComponent<PlayerController>();
        typeof(PlayerController).GetField("config", Flags).SetValue(player, config);
        typeof(PlayerController).GetField("gameManager", Flags).SetValue(player, manager);
        player.MinY = -3.5f;
        player.MaxY = 3.5f;

        CameraShake shake = null;
        if (withShake)
        {
            var cam = new GameObject("Camera");
            cam.transform.position = new Vector3(0f, 0f, -10f);
            shake = cam.AddComponent<CameraShake>();
        }

        var intro = new GameObject("Intro").AddComponent<IntroSequence>();
        void Set(string field, object value) => typeof(IntroSequence).GetField(field, Flags).SetValue(intro, value);
        Set("gameManager", manager);
        Set("player", player);
        Set("cameraShake", shake);
        Set("startX", -11f);
        Set("shakeSeconds", 0.05f);
        Set("shakeStrength", 0.3f);
        Set("pauseSeconds", 0.05f);
        Set("walkSeconds", 0.2f);

        return new Rig { Manager = manager, Player = player, Body = body, Shake = shake, Intro = intro };
    }

    private static IEnumerator WaitForState(GameManager manager, GameState state, float timeout)
    {
        for (float t = 0f; t < timeout && manager.CurrentState != state; t += Time.deltaTime) yield return null;
    }

    [UnityTest]
    public IEnumerator StartIntro_PlacesPlayerOffscreen_WalksHome_ThenBeginsRun()
    {
        var rig = Build();
        yield return null; // Start: captures HomeX = -6, subscribes

        rig.Manager.StartIntro();
        Assert.AreEqual(-11f, rig.Body.position.x, 1e-3f, "starts off-screen left");
        Assert.AreEqual(-3.5f, rig.Body.position.y, 1e-3f, "on the floor, even after a mid-air death");

        yield return new WaitForSeconds(0.2f); // mid-walk
        Assert.AreEqual(GameState.Intro, rig.Manager.CurrentState);
        Assert.Greater(rig.Body.position.x, -11f);
        Assert.Less(rig.Body.position.x, -6f);

        yield return WaitForState(rig.Manager, GameState.Running, 2f);
        Assert.AreEqual(GameState.Running, rig.Manager.CurrentState);
        Assert.AreEqual(-6f, rig.Intro.HomeX, 1e-3f);
        Assert.AreEqual(-6f, rig.Body.position.x, 1e-3f, "ends at home X");
        Assert.AreEqual(-3.5f, rig.Body.position.y, 0.05f);
        rig.Destroy();
    }

    [UnityTest]
    public IEnumerator NoCameraShake_StillBeginsRun()
    {
        var rig = Build(withShake: false);
        yield return null;

        rig.Manager.StartIntro();
        yield return WaitForState(rig.Manager, GameState.Running, 2f);

        Assert.AreEqual(GameState.Running, rig.Manager.CurrentState);
        rig.Destroy();
    }

    [UnityTest]
    public IEnumerator LeavingIntro_MidSequence_DoesNotBeginRun()
    {
        var rig = Build();
        yield return null;

        rig.Manager.StartIntro();
        yield return new WaitForSeconds(0.02f);
        rig.Manager.ReturnToGetReady();

        yield return new WaitForSeconds(0.5f);
        Assert.AreEqual(GameState.GetReady, rig.Manager.CurrentState);
        rig.Destroy();
    }

    [UnityTest]
    public IEnumerator Destroyed_MidSequence_DoesNotBeginRun()
    {
        var rig = Build();
        yield return null;

        rig.Manager.StartIntro();
        yield return new WaitForSeconds(0.12f); // inside the walk
        Object.DestroyImmediate(rig.Intro.gameObject);

        yield return new WaitForSeconds(0.5f);
        Assert.AreEqual(GameState.Intro, rig.Manager.CurrentState);
        LogAssert.NoUnexpectedReceived();
        rig.Destroy();
    }

    [UnityTest]
    public IEnumerator SecondIntro_AfterFirst_PlaysAgain()
    {
        var rig = Build();
        yield return null;

        rig.Manager.StartIntro();
        yield return WaitForState(rig.Manager, GameState.Running, 2f);
        rig.Manager.EndRun();
        rig.Manager.ReturnToGetReady();

        rig.Manager.StartIntro();
        Assert.AreEqual(-11f, rig.Body.position.x, 1e-3f);
        yield return WaitForState(rig.Manager, GameState.Running, 2f);
        Assert.AreEqual(GameState.Running, rig.Manager.CurrentState);
        Assert.AreEqual(-6f, rig.Body.position.x, 1e-3f);
        rig.Destroy();
    }
}
```

- [ ] **Step 2: Run to verify failure** — Filter `IntroSequenceTests`. Expected: exit 1, compile error `The type or namespace name 'IntroSequence' could not be found`.

- [ ] **Step 3: Implement** — create `Assets/Scripts/Core/IntroSequence.cs`:

```csharp
using UnityEngine;
using JetpackRide.Environment;
using JetpackRide.Player;

namespace JetpackRide.Core
{
    // Observer on GameManager.StateChanged: on Intro, puts the player off-screen left on the floor,
    // shakes the camera (AudioManager plays the explosion on the same state change), pauses, walks the
    // player to its home X while the world stands still, then starts the run. Any state change,
    // destroy, or newer intro makes an in-flight sequence stop without calling BeginRun.
    public class IntroSequence : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private PlayerController player;
        [SerializeField] private CameraShake cameraShake;
        [Tooltip("World X the player starts at (just off-screen left).")]
        [SerializeField] private float startX = -11f;
        [SerializeField] private float shakeSeconds = 0.4f;
        [SerializeField] private float shakeStrength = 0.3f;
        [Tooltip("Beat between the end of the shake and the walk-in.")]
        [SerializeField] private float pauseSeconds = 0.3f;
        [SerializeField] private float walkSeconds = 1.2f;

        private Rigidbody2D body;
        private int generation;

        public float HomeX { get; private set; }

        // Wiring in Start/OnDestroy so serialized (or test-injected) references are assigned first.
        private void Start()
        {
            body = player.GetComponent<Rigidbody2D>();
            HomeX = player.transform.position.x;
            gameManager.StateChanged += HandleStateChanged;
        }

        private void OnDestroy()
        {
            generation++;
            if (gameManager != null) gameManager.StateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(GameState state)
        {
            generation++;
            if (state == GameState.Intro) PlayAsync(generation).Forget();
        }

        private bool IsStale(int gen) => this == null || gen != generation || gameManager.CurrentState != GameState.Intro;

        private async Awaitable PlayAsync(int gen)
        {
            PlaceAt(startX);
            if (cameraShake != null) cameraShake.Shake(shakeSeconds, shakeStrength).Forget();

            await Awaitable.WaitForSecondsAsync(shakeSeconds + pauseSeconds);
            if (IsStale(gen)) return;

            for (float t = 0f; t < walkSeconds; t += Time.deltaTime)
            {
                PlaceAt(Mathf.Lerp(startX, HomeX, t / walkSeconds));
                await Awaitable.NextFrameAsync();
                if (IsStale(gen)) return;
            }

            PlaceAt(HomeX);
            gameManager.BeginRun();
        }

        // Teleport (the body's X is frozen in the scene, so it can't be moved by velocity).
        private void PlaceAt(float x)
        {
            var pos = new Vector2(x, player.MinY);
            body.position = pos;
            body.linearVelocity = Vector2.zero;
            player.transform.position = new Vector3(pos.x, pos.y, player.transform.position.z);
        }
    }
}
```

- [ ] **Step 4: Run to verify pass** — Filter `IntroSequenceTests`. Expected: exit 0, 5 passed. If `StartIntro_PlacesPlayerOffscreen...`'s mid-walk check is flaky on a slow machine, raise its `walkSeconds` in `Build` to 0.4 and keep the 0.2 s mid-walk wait (don't loosen the assertions).

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Core/IntroSequence.cs Assets/Scripts/Core/IntroSequence.cs.meta Assets/Tests/PlayMode/IntroSequenceTests.cs Assets/Tests/PlayMode/IntroSequenceTests.cs.meta
git commit -m "feat: IntroSequence plays shake, pause and walk-in, then begins the run (Task 9.8)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Start goes to the intro; input ignored during it

**Files:**
- Modify: `Assets/Scripts/Player/RestartController.cs` (the `HandleRestartPressed` switch)
- Test: `Assets/Tests/PlayMode/RestartControllerTests.cs`

**Interfaces:**
- Consumes: `GameManager.StartIntro()`, `GameState.Intro` (Task 1).
- Produces: `RestartController.HandleRestartPressed()` — `GetReady` → `StartIntro()`; `Intro` → no-op; `GameOver` → `ReturnToGetReady()` (unchanged).

- [ ] **Step 1: Update/add tests.** In `RestartControllerTests`, replace the test `RestartPressed_FromGetReady_BeginsRun` with:

```csharp
    [UnityTest]
    public IEnumerator RestartPressed_FromGetReady_StartsIntro()
    {
        var (controller, manager) = Build(0f);
        yield return null;

        yield return new WaitForSeconds(0.05f);
        controller.HandleRestartPressed();
        yield return null;

        Assert.AreEqual(GameState.Intro, manager.CurrentState);
        Object.DestroyImmediate(controller.gameObject);
        Object.DestroyImmediate(manager.gameObject);
    }

    [UnityTest]
    public IEnumerator RestartPressed_DuringIntro_IsIgnored()
    {
        var (controller, manager) = Build(0f);
        yield return null;
        yield return new WaitForSeconds(0.05f);
        controller.HandleRestartPressed(); // GetReady -> Intro

        int stateChanges = 0;
        manager.StateChanged += _ => stateChanges++;
        controller.HandleRestartPressed(); // pressed again mid-intro
        yield return null;

        Assert.AreEqual(GameState.Intro, manager.CurrentState);
        Assert.AreEqual(0, stateChanges, "no restart of the intro, no skip to Running");
        Object.DestroyImmediate(controller.gameObject);
        Object.DestroyImmediate(manager.gameObject);
    }
```

- [ ] **Step 2: Run to verify failure** — Filter `RestartControllerTests`. Expected: exit 2; `RestartPressed_FromGetReady_StartsIntro` fails with `Expected: Intro But was: Running`.

- [ ] **Step 3: Implement** — in `RestartController.HandleRestartPressed`, change the `GetReady` case:

```csharp
                case GameState.GetReady:
                    gameManager.StartIntro();
                    break;
```

(No `Intro` case: the switch already ignores states it doesn't list, which is the required no-op.)

- [ ] **Step 4: Run to verify pass** — Filter `RestartControllerTests`. Expected: exit 0, all passed.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Player/RestartController.cs Assets/Tests/PlayMode/RestartControllerTests.cs
git commit -m "feat: start on the title screen plays the intro; input ignored during it (Task 9.8)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Observers react to `Intro` (run pose, panels, explosion sound)

**Files:**
- Modify: `Assets/Scripts/Player/PlayerVisuals.cs` (the `Pose =` line in `LateUpdate`)
- Modify: `Assets/Scripts/Audio/AudioManager.cs` (SFX fields, `HandleStateChanged`)
- Test: `Assets/Tests/PlayMode/PlayerVisualsTests.cs`, `UIManagerTests.cs`, `AudioManagerTests.cs`
- `Assets/Scripts/UI/UIManager.cs` needs no code change (its `ShowPanelFor` already shows each panel only for its own state); the test pins that.

**Interfaces:**
- Consumes: `GameManager.StartIntro()`, `GameState.Intro` (Task 1).
- Produces: `AudioManager` serialized fields `startExplosionSfx` (`AudioClip`) and `startExplosionVolume` (`float`, Range 0–1, default 1) — Task 6 wires `startExplosionSfx` by name.

- [ ] **Step 1: Write the failing tests.**

`PlayerVisualsTests` (uses its existing `Build()`; the rig starts on the floor at y = -3.5):

```csharp
    [UnityTest]
    public IEnumerator Intro_OnFloor_PlaysRunCycle()
    {
        var rig = Build();
        yield return null;
        rig.Manager.StartIntro();

        var seen = new System.Collections.Generic.HashSet<Sprite>();
        for (float t = 0f; t < 0.3f; t += Time.deltaTime)
        {
            yield return null;
            seen.Add(rig.Renderer.sprite);
        }

        Assert.AreEqual(PlayerPose.Run, rig.Visuals.Pose);
        Assert.IsFalse(seen.Contains(rig.Fly));
        rig.Destroy();
    }
```

`UIManagerTests` (uses its existing `Build()` and `GetPanel(ui, name)`):

```csharp
    [UnityTest]
    public IEnumerator StartIntro_HidesAllPanels()
    {
        var (ui, manager) = Build();
        yield return null;

        manager.StartIntro();

        Assert.IsFalse(GetPanel(ui, "titlePanel").activeSelf);
        Assert.IsFalse(GetPanel(ui, "hudPanel").activeSelf);
        Assert.IsFalse(GetPanel(ui, "gameOverPanel").activeSelf);
    }
```

`AudioManagerTests` (uses its existing `Build()`, `Flags`, `Cleanup()`):

```csharp
    [UnityTest]
    public IEnumerator StartIntro_StopsMusic_PlaysStartExplosion()
    {
        var (audio, manager, _, _) = Build();
        var boom = AudioClip.Create("boom", 441, 1, 44100, false);
        typeof(AudioManager).GetField("startExplosionSfx", Flags).SetValue(audio, boom);
        typeof(AudioManager).GetField("startExplosionVolume", Flags).SetValue(audio, 0.7f);
        yield return null; // GetReady: menu music playing

        manager.StartIntro();

        Assert.IsNull(audio.MusicSource.clip, "menu music stops for the intro");
        Assert.AreSame(boom, audio.LastSfx);
        Assert.AreEqual(0.7f, audio.LastSfxVolume, 1e-4f);

        manager.BeginRun();
        Assert.IsNotNull(audio.MusicSource.clip, "gameplay music starts with the run");
        Cleanup(audio, manager);
    }
```

- [ ] **Step 2: Run to verify failure** — Filter `PlayerVisualsTests;UIManagerTests;AudioManagerTests`. Expected: exit 2 (it compiles — the new AudioManager fields are reached by reflection). Failures: `Intro_OnFloor_PlaysRunCycle` (`Expected: Run But was: Fly`) and `StartIntro_StopsMusic_PlaysStartExplosion` (`NullReferenceException`: `GetField("startExplosionSfx")` returns null). `StartIntro_HidesAllPanels` already **passes** — it pins existing `ShowPanelFor` behaviour.

- [ ] **Step 3: Implement.**

`PlayerVisuals.LateUpdate` — replace the `Pose =` line:

```csharp
            bool walking = state == GameState.Running || state == GameState.Intro; // intro walk-in uses the run cycle
            Pose = walking && onFloor && !thrusting ? PlayerPose.Run : PlayerPose.Fly;
```

`AudioManager` — add after the `rocketLaunchVolume` field:

```csharp
        [SerializeField] private AudioClip startExplosionSfx;
        [SerializeField, Range(0f, 1f)] private float startExplosionVolume = 1f;
```

and add a case to `HandleStateChanged`:

```csharp
                case GameState.Intro:
                    PlayMusic(null);
                    PlaySfx(startExplosionSfx, startExplosionVolume);
                    break;
```

- [ ] **Step 4: Run to verify pass** — Filter `PlayerVisualsTests;UIManagerTests;AudioManagerTests`. Expected: exit 0, all passed.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Player/PlayerVisuals.cs Assets/Scripts/Audio/AudioManager.cs Assets/Tests/PlayMode/PlayerVisualsTests.cs Assets/Tests/PlayMode/UIManagerTests.cs Assets/Tests/PlayMode/AudioManagerTests.cs
git commit -m "feat: run pose, hidden panels and start-explosion sound during the intro (Task 9.8)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: Wire the scene, docs, full suite, playtest

**Files:**
- Modify: `Assets/Editor/PrefabBuilder.cs` (new menu method after `WirePlayerVisuals`)
- Modify (generated): `Assets/Scenes/MainGame.unity`
- Add: `Assets/Audio/startExplosion1.mp3`, `Assets/Audio/startExplosion1.mp3.meta` (already on disk, untracked)
- Modify: `README.md`, `docs/superpowers/plans/2026-09-29-game-feel.md` (Task 9.8 status)

**Interfaces:**
- Consumes: `IntroSequence` fields `gameManager`, `player`, `cameraShake` (Task 3); `CameraShake` (Task 2); `AudioManager.startExplosionSfx` (Task 5).
- Produces: menu item **Jetpack Ride/Wire Start Intro** = `JetpackRide.EditorTools.PrefabBuilder.WireStartIntro` (idempotent).

- [ ] **Step 1: Add the menu item** — in `PrefabBuilder`, after `WirePlayerVisuals()`:

```csharp
        // Batchmode: ... -executeMethod JetpackRide.EditorTools.PrefabBuilder.WireStartIntro
        [MenuItem("Jetpack Ride/Wire Start Intro")]
        public static void WireStartIntro()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(MainScenePath);
            var player = UnityEngine.Object.FindAnyObjectByType<JetpackRide.Player.PlayerController>()
                ?? throw new InvalidOperationException("No PlayerController in " + MainScenePath);
            var gameManager = UnityEngine.Object.FindAnyObjectByType<JetpackRide.Core.GameManager>()
                ?? throw new InvalidOperationException("No GameManager in " + MainScenePath);
            var audio = UnityEngine.Object.FindAnyObjectByType<JetpackRide.Audio.AudioManager>()
                ?? throw new InvalidOperationException("No AudioManager in " + MainScenePath);
            var camera = UnityEngine.Object.FindAnyObjectByType<Camera>()
                ?? throw new InvalidOperationException("No Camera in " + MainScenePath);

            // TryGetComponent, not GetComponent + ??: a missing component is a Unity fake-null in the editor.
            if (!camera.TryGetComponent<JetpackRide.Environment.CameraShake>(out var shake))
                shake = camera.gameObject.AddComponent<JetpackRide.Environment.CameraShake>();
            if (!player.TryGetComponent<JetpackRide.Core.IntroSequence>(out var intro))
                intro = player.gameObject.AddComponent<JetpackRide.Core.IntroSequence>();

            var so = new SerializedObject(intro);
            so.FindProperty("gameManager").objectReferenceValue = gameManager;
            so.FindProperty("player").objectReferenceValue = player;
            so.FindProperty("cameraShake").objectReferenceValue = shake;
            so.ApplyModifiedPropertiesWithoutUndo();

            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/startExplosion1.mp3")
                ?? throw new InvalidOperationException("Missing Assets/Audio/startExplosion1.mp3");
            var audioSo = new SerializedObject(audio);
            audioSo.FindProperty("startExplosionSfx").objectReferenceValue = clip;
            audioSo.ApplyModifiedPropertiesWithoutUndo();

            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            Debug.Log("[PrefabBuilder] Start intro wired (IntroSequence, CameraShake, startExplosionSfx).");
        }
```

- [ ] **Step 2: Run it** — with Unity closed:

```bash
"$UNITY" -batchmode -quit -projectPath . -executeMethod JetpackRide.EditorTools.PrefabBuilder.WireStartIntro -logFile "$OUT/wire.log"; echo "exit $?"
grep -n "Start intro wired\|Exception" "$OUT/wire.log"
```

Expected: exit 0 and the `Start intro wired` line. (With Unity open instead: menu **Jetpack Ride > Wire Start Intro**, then Ctrl+S.) Check `git diff Assets/Scenes/MainGame.unity` shows: an `IntroSequence` MonoBehaviour on the Player with non-zero `gameManager`/`player`/`cameraShake` fileIDs, a `CameraShake` on Main Camera, and `startExplosionSfx: {fileID: 8300000, guid: <startExplosion1.mp3.meta guid>, type: 3}` on the AudioManager.

- [ ] **Step 3: Docs.**
  - `README.md` component table: add rows `Core/IntroSequence` | `Player` | "Observer on `GameManager.StateChanged`: on `Intro`, places the player off-screen left on the floor, shakes the camera, pauses, walks the player in to its home X while the world stands still, then calls `BeginRun()`." and `Environment/CameraShake` | `Main Camera` | "`Shake(seconds, strength)`: fading random jitter, restores the exact rest position (also on disable / overlapping shakes)."; in the `GameManager` row change `GetReady`/`Running`/`GameOver` to `GetReady`/`Intro`/`Running`/`GameOver`; add `Wire Start Intro` to the editor-tooling list; in the AudioManager row mention the start-explosion SFX on `Intro`.
  - README features list: replace the planned "Optional start animation" bullet with "* Start intro: pressing start plays the start explosion with a camera shake, then the player walks in from off-screen left before the run begins (`IntroSequence`)." and mark Phase 9 as complete if no other items remain.
  - Game-feel plan Task 9.8: add `**Status:** Done (2026-09-30), see docs/superpowers/plans/2026-09-30-start-intro.md.` under its heading.

- [ ] **Step 4: Full suite** — EditMode and PlayMode, no filter:

```bash
"$UNITY" -batchmode -projectPath . -runTests -testPlatform EditMode -testResults "$OUT/edit.xml" -logFile "$OUT/edit.log"; echo "edit exit $?"
"$UNITY" -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults "$OUT/play.xml" -logFile "$OUT/play.log"; echo "play exit $?"
```

Expected: both exit 0, `failed="0"`. Then apply the ProjectSettings line-ending rule from Global Constraints.

- [ ] **Step 5: Playtest** (Unity, `MainGame`, Play): title → press start → explosion sound + shake, title gone, short beat, player runs in from the left with footsteps, HUD and gameplay music appear as the run starts; thrust/start pressed during the intro does nothing; die → press → title → press → the intro plays again from the floor. Adjust timings on the Player's `IntroSequence` if needed (and the explosion volume on AudioManager).

- [ ] **Step 6: Commit** (never the font asset):

```bash
git add Assets/Editor/PrefabBuilder.cs Assets/Scenes/MainGame.unity Assets/Audio/startExplosion1.mp3 Assets/Audio/startExplosion1.mp3.meta README.md docs/superpowers/plans/2026-09-29-game-feel.md
git status --short   # only the font asset (and nothing else) may remain modified
git commit -m "feat: wire start intro into MainGame; docs (Task 9.8)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```
