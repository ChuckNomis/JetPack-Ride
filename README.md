# Jetpack Ride — a Jetpack Joyride Clone in Unity

Final project for the **Game Development with Unity** course.

Jetpack Ride is an endless side-scroller inspired by *Jetpack Joyride*. The player flies a jetpack through an endless lab, dodging electric zappers and homing rockets and collecting coins. The run gets harder the farther you go.

<img width="100%" alt="Gameplay" src="https://github.com/user-attachments/assets/607f4580-de97-4888-bfcc-a0aa3eba386d" />

**Team:** Alfredo Limin, Nadav Simon

---

## Contents

1. [How to Run](#how-to-run)
2. [Controls](#controls)
3. [Gameplay](#gameplay)
4. [Difficulty Ramp](#difficulty-ramp--read-this-before-grading)
5. [Features](#features)
6. [Architecture](#architecture)
7. [Design Patterns](#design-patterns)
8. [Automated Tests](#automated-tests)
9. [Editor Tooling](#editor-tooling)
10. [Credits](#credits)

---

## How to Run

* **Unity version:** Unity 6 (`6000.3.20f1`), Universal Render Pipeline (2D).
* Open the project in Unity Hub, then open **`Assets/Scenes/MainGame.unity`** and press Play.
  * A fresh clone opens in an empty untitled scene (a blue screen). Open `MainGame` once and Unity remembers it.
* `MainGame` is the only scene in Build Settings.
* The view is locked to 16:9 (1920×1080). Other window shapes get black bars, so the game and the UI look the same on every screen.

---

## Controls

| Action | Keyboard / Mouse |
|---|---|
| Start a run (title screen) | `Space` / Left click |
| Fly (hold) | `Space` / Left click |
| Pause | `Esc` or the on-screen pause button (top right) |
| Resume | `Esc` or the **Continue** button |
| Back to title after game over | `Space` / Left click (after a 1-second lockout) |

The game is designed for keyboard and mouse. The input actions also have gamepad bindings (`A` / right trigger to fly, `Start` to pause), but they have not been play-tested.

The pause screen has **Continue** and **Restart** buttons. Restart abandons the run without saving a high score and replays the intro.

---

## Gameplay

The game is driven by a state machine in `GameManager` (`GameState`):

1. **GetReady (title screen):** shows the high score. Pressing start begins the intro.
2. **Intro:** an explosion blasts through the left wall, the camera shakes, and the player walks in. Then the run begins.
3. **Running:** hold to fly and release to fall. Zappers, rocket volleys and coin patterns come in from the right. Score = metres travelled + 5 per coin.
4. **GameOver:** touching a zapper or a rocket kills the player. The player explodes, plays a death tumble, and the world freezes. The final distance, coins and high score are shown. A short lockout prevents an accidental instant restart.

The game can be paused during the intro and the run. Pausing freezes time and audio.

---

## Difficulty Ramp

> **Note for the checker:** the first few hundred metres of a run are deliberately easy. Many hazards only unlock later in the run, so a short play session will **not** show them.
> **Zappers start out vertical only**, and **rockets start as single rockets**. To see everything the game has, survive past about **1,700 m**.

Difficulty grows with **distance travelled**. Full difficulty is reached at **2,500 m** (`GameConfig.DifficultyRampDistance`).

### Unlock timeline

| Distance | What changes |
|---|---|
| 0 m | **Vertical zappers only**, in the two shorter lengths. Single rockets. All coin patterns are available at their smallest sizes. |
| 250 m | Rocket volleys can be **pairs** (50% single / 50% pair). |
| **500 m** | **Zapper shapes unlock:** zappers can now be **horizontal** or **diagonal (±45°)** (40% vertical, 30% horizontal, 15% diagonal up, 15% diagonal down). Rocket volleys can be **triples** (30% single / 40% pair / 30% triple). |
| 1,000 m | The **longest zapper** length unlocks. |
| **1,625 m** | Zappers come in **pairs** (two zappers per spawn). |
| 2,200 m | Scroll speed reaches its maximum (it starts at 15 and grows linearly to 26). |
| 2,500 m | Full difficulty: the fastest spawn rates and the shortest rocket lock-on. |

### What ramps continuously

* **Scroll speed:** the world moves faster the farther you go.
* **Spawn rate:** the time between zapper spawns drops from 1.8 s to 0.6 s, and between rocket volleys from 4 s to 1.2 s. These follow an ease-in curve, so the early game stays gentle and the pressure builds later.
* **Rocket lock-on time:** shrinks from 2 s to 1 s, so the player has less time to react.
* **Coin patterns:** grow larger.

### Rockets: only one rocket per volley tracks the player

Every volley is announced by red warning signs at the right edge of the screen, one sign per rocket:

* **Exactly one rocket in each volley is a homing rocket.** Its warning follows the player's height for the lock-on time. It then blinks red with a beep as it locks, and the rocket fires straight at the locked height.
* **Any other rockets in the volley are fixed.** Their warnings stay at a random height and they launch first, after a short 0.5 s warning.
* Which rocket in the volley tracks is random, so it's not always the top or bottom one.
* Rockets in a volley are always at least 3 units apart, and fixed rockets never line up with the homing one into a wall. There is always a gap to fly through.

### Fairness guarantees

* A zapper pair always leaves an open lane (at least 1.3 units) to fly through (`ZapperLayout`).
* Coins never touch zappers (at least 1 unit apart while both are on screen). Instead of cutting a coin pattern, the zapper is moved up or down or pushed later (`SpawnSafety`).

---

## Features

* State-driven game loop: title → intro → run → game over, with a pause on top.
* Jetpack physics: thrust and gravity on a `Rigidbody2D`, clamped between the floor and the ceiling.
* Zappers in 4 orientations and 3 lengths (9-sliced sprite with a 4-frame flicker), with a collider that hugs the beam.
* Rocket volleys of 1–3 rockets with warning signs, one of which locks on to the player.
* Coins in 5 patterns: line, arc, arrow, box and rectangle.
* Distance-based difficulty ramp (see above).
* Object pooling for zappers, rockets, coins and warnings, so nothing is allocated during a run.
* Persisted high score (`PlayerPrefs`).
* Pause menu (Esc / gamepad Start / on-screen button) with Continue and Restart.
* Infinite scrolling background.
* Game feel:
  * an intro wall-blast explosion with camera shake and a walk-in
  * an 8-frame run cycle on the floor and a forward tilt while falling
  * a ground shadow that shrinks as the player flies higher
  * jetpack sparks, coin sparkles, a death explosion and a death tumble
  * the world freezes on death
* Audio: menu and gameplay music, a jetpack loop, footsteps, coin, rocket launch, lock-on beeps, death and the start explosion. Each sound has its own volume.
* HUD: distance, coin count with a coin icon, and a pause button.
* 16:9 letterboxing on any screen shape.

---

## Architecture

Scripts live in `Assets/Scripts`, grouped by area. The design is **event-driven**: `GameManager` owns the state and fires events (`StateChanged`, `DistanceChanged`, `CoinsChanged`, `ScoreChanged`, `PausedChanged`). The UI, audio, player visuals and spawning systems observe those events instead of polling. All balance values live in one ScriptableObject, `GameConfig`, so they can be tuned in the Inspector without code changes.

| Area | Script | Responsibility |
|---|---|---|
| Core | `GameManager` | State machine (`GetReady` / `Intro` / `Running` / `GameOver`), the pause flag, distance, coins, score and the persisted high score. |
| | `GameConfig` | ScriptableObject with every tuning value: speeds, physics, spawn intervals, difficulty curve, rocket lock-on and scoring. |
| | `DifficultyEvaluator` | A pure function: distance → `DifficultySnapshot` (scroll speed, spawn intervals, ramp progress). |
| | `DistanceTracker` | Adds distance each frame from the current scroll speed. |
| | `IntroSequence` | The start intro: wall blast, camera shake, walk-in, then `BeginRun()`. |
| | `RunResetService` | Returns every pooled object when the game goes back to the title, so each run starts clean. |
| Spawning | `SpawnManager` | Separate async spawn loops for zappers, rocket volleys and coins, all timed by the difficulty ramp. |
| | `ZapperLayout` | Picks zapper orientation, length and position, and guarantees an open lane. |
| | `CoinPatterns` | Builds the 5 coin shapes, scaled by difficulty. |
| | `SpawnSafety` | Predicts whether a coin and a zapper moving at different speeds will ever overlap on screen. |
| Hazards | `HazardMover` | Moves pooled objects left and returns them to the pool off-screen. Frozen on death. |
| | `ZapperShape` | Sizes and rotates a zapper, fits its collider and animates its flicker. |
| | `RocketWarningIndicator` | The rocket warning sign: tracks the player's height, then blinks as it locks. |
| | `RocketBehaviour`, `ObstacleBehaviour` | Marker components for rockets and static hazards. |
| Pickups | `CoinBehaviour` | Coin collection and sparkle effect. |
| Pooling | `ObjectPoolManager`, `IPoolable` | Generic pools keyed by id (`UnityEngine.Pool`), with spawn/despawn hooks. |
| Player | `PlayerController` | Thrust input, physics, play-area clamp and collisions. |
| | `PlayerVisuals` | Run cycle, fly sprite, fall tilt and footstep events. |
| | `PlayerDeathAnimator` | Dead sprite, hop and backward tumble. |
| | `PlayerShadow` | A ground shadow that shrinks with height. |
| | `RestartController` | Start/restart input with the post-death lockout. |
| Environment | `ParallaxLayer` | Infinite scrolling background tiles. |
| | `CameraShake` | Fading camera shake. |
| | `CameraLetterbox` | 16:9 viewport with black bars. |
| UI | `UIManager` | Title / HUD / Game Over panels and their text. |
| | `PauseMenu` | Pause input, the pause panel and the HUD pause button. |
| Audio | `AudioManager` | Music and sound effects, driven by game events. |

---

## Design Patterns

### Object Pool

Every object that spawns repeatedly during a run is pooled instead of being created and destroyed. This avoids garbage-collection spikes in an endless game.

* `Pooling/ObjectPoolManager` wraps Unity's `UnityEngine.Pool.ObjectPool` in pools keyed by id. The scene has four pools: `obstacle` (zappers), `rocket`, `coin` and `rocketWarning`.
* `SpawnManager` takes objects from the pools (`pool.Spawn(...)`).
* `HazardMover` returns zappers, rockets and coins once they scroll off-screen, and `RocketWarningIndicator` returns warning signs when they finish (`pool.Despawn(...)`).
* `RunResetService` returns everything at once when a new run starts (`DespawnAll()`).
* Pooled components implement `Pooling/IPoolable` (`OnSpawned()` / `OnDespawned()`) to reset their state on reuse. For example, `ZapperShape` resets its size and flicker, and `HazardMover` clears its frozen flag.

### Observer

Systems never poll each other. They subscribe to C# events (`event Action<...>`) and react when something changes.

* **`GameManager`** publishes `StateChanged`, `PausedChanged`, `DistanceChanged`, `CoinsChanged` and `ScoreChanged`. Its subscribers include:
  * `UIManager` (switches panels and updates text)
  * `PauseMenu` (the pause panel and the HUD pause button)
  * `AudioManager` (music and sound effects)
  * `SpawnManager` (starts and stops the spawn loops)
  * `IntroSequence`, `PlayerDeathAnimator`, `RunResetService` and `RestartController`
* **`PlayerController`** publishes `ThrustingChanged`, which `PlayerVisuals` (sprite) and `AudioManager` (jetpack loop) listen to.
* **`PlayerVisuals`** publishes `Footstep` for footstep sounds.
* **`SpawnManager`** and **`RocketWarningIndicator`** publish rocket events (`RocketSpawned`, `RocketWarningBlinked`, `LockBlinked`) for the launch sound and the lock-on beep.

Every subscriber unsubscribes when it is destroyed or disabled. Thanks to this, `GameManager` knows nothing about the UI, audio or visuals. New features plug in as new subscribers: for example, the HUD pause button only listens to `StateChanged` and `PausedChanged`.

*Not used:* **Singletons.** Components get their references through serialized Inspector fields instead, which keeps them easy to test in isolation. **Coroutines.** Timed sequences such as the intro, rocket lock-on, the death animation and the spawn loops use Unity 6's `Awaitable` (async/await), the newer alternative to coroutines.

---

## Automated Tests

The project has over 150 automated tests (NUnit, Unity Test Framework) in `Assets/Tests`:

* **EditMode:** pure logic such as the difficulty evaluator, zapper layout, coin patterns, spawn safety, letterbox math and background wrapping.
* **PlayMode:** the game state machine and pause, spawning (volleys, zapper clusters, coin and zapper clearance), pooling, rocket warnings, the player controller and visuals, the death animation, the intro, audio and the UI.

To run them: **Window → General → Test Runner**, then run the EditMode and PlayMode tabs.

---

## Editor Tooling

`Assets/Editor/PrefabBuilder.cs` adds a **Jetpack Ride** menu in the Unity editor. Its items build prefabs and wire the scene from code (zapper variants, rocket warnings, particle effects, audio, the player visuals, the intro wall blast, the pause menu). This keeps the setup reproducible.

---

## Credits

* Sound Effects: [Pixabay.com](https://pixabay.com/)
* Font, Sprites, Sounds: [https://github.com/Gustavo-Pauli/Jetpack-Goodride](https://github.com/Gustavo-Pauli/Jetpack-Goodride)
* Built with Unity 6 and the Universal Render Pipeline.

