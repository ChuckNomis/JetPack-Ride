# Jetpack Joyride Clone (Unity)
A Jetpack Joyride clone built as a course final assignment for **Game Development with Unity**. The project focuses on core gameplay mechanics, state-driven game loops, background rendering, and dynamic physics-based controls.

<img width="100%" alt="giphy" src="https://github.com/user-attachments/assets/607f4580-de97-4888-bfcc-a0aa3eba386d" />

---

## Tech Stack & Engine

* **Engine:** Unity 6 (`6000.3.20f1`)
* **Render Pipeline:** Universal Render Pipeline (URP 2D)

---

## Controls

* **`Space`** or **`Left Mouse Click`**: applies upward impulse and activates the jet. The initial input also transitions the game from the start screen into active gameplay.

---

## Game Loop & State Management

The core gameplay flow is managed by a centralized state machine via `GameManager.CurrentState` (`GameState` enum):

* **`GetReady`**  
  Title screen. Pressing `Space`/click starts the run (`GameManager.BeginRun()`), resetting distance, coins, and score. `RunResetService` clears any pooled hazards left over from the previous run.
* **`Running`**  
  Active gameplay: the player takes thrust input, zappers/rockets/coins spawn and scroll in from the right (each rocket preceded by a warning telegraph), distance and score tick up (`DistanceTracker`), and difficulty (scroll speed, spawn rate, rocket aggression) ramps up with distance via `DifficultyEvaluator`.
* **`GameOver`**  
  Triggered when the player collides with a zapper or a rocket (`PlayerController.OnTriggerEnter2D`). The player explodes and plays a death animation (dead sprite, hop, backward tumble). Shows final distance/coins and high score. A short input lockout (`RestartController`, `GameConfig.RestartLockoutSeconds`) prevents an accidental instant restart; pressing again returns to `GetReady`.

---

## Architecture & Scripts (`Assets/Scripts`)

| Script | Attached To | Description |
|---|---|---|
| `Core/GameManager` | `GameManager` | Central state machine (`GameState`: `GetReady`/`Running`/`GameOver`). Owns distance, coins, score, and persisted high score (`PlayerPrefs`); fires `StateChanged`/`DistanceChanged`/`CoinsChanged`/`ScoreChanged` events. |
| `Core/GameConfig` | ScriptableObject asset | Tunable balance values: scroll speed curve, jetpack thrust/gravity, rocket speed multiplier, obstacle/rocket spawn intervals, difficulty ramp curves, coin value, restart lockout. |
| `Core/DifficultyEvaluator` | (static, no GameObject) | Pure function mapping distance travelled → a `DifficultySnapshot` (scroll speed, spawn intervals, rocket aggression), driven by `GameConfig`'s curves. |
| `Core/DistanceTracker` | `DistanceTracker` | Ticks `GameManager.AddDistance()` each frame using the current scroll speed while `Running`. |
| `Core/RunResetService` | `RunResetService` | On transition to `GetReady`, despawns every pooled hazard/coin so a new run starts clean. |
| `Environment/ParallaxLayer` | `Background_1` / `Background_2` | Infinite horizontal scroll: wraps a tile back by `TileWidth * TileCount` once it scrolls past `-TileWidth`, so multiple equal-speed tiles stay offset instead of converging. |
| `Player/PlayerController` | `Player` | Reads thrust input, applies upward force while held, clamps the player between `MinY`/`MaxY`, and detects hazard/coin collisions via trigger. Plays the jetpack spark particles while thrusting and spawns the death explosion. |
| `Player/PlayerDeathAnimator` | `Player` | Observer on `GameManager.StateChanged`: on `GameOver` swaps to the dead sprite, hops, and tumbles a full backward flip (async `Awaitable`); `GetReady` restores the flying pose. |
| `Player/RestartController` | `Player` | Handles the restart input action, including the post-death lockout timer. |
| `Hazards/HazardMover` | `Obstacle_Zapper`, `Rocket` prefabs | Moves a spawned hazard left at a configured speed and despawns it back to its pool once it scrolls past `despawnX`. |
| `Hazards/ObstacleBehaviour` | `Obstacle_Zapper` prefab | Marker component for static hazards; death is driven by the `Hazard` tag + collider, read by `PlayerController`. |
| `Hazards/RocketBehaviour` | `Rocket` prefab | Optional homing: steers the rocket's Y toward the player at a capped rate when spawned as a homing rocket. |
| `Hazards/RocketWarningIndicator` | `RocketWarning` prefab | Pooled warning telegraph shown at the right screen edge before a rocket arrives; despawns itself after the lead time and ignores stale despawns after pool reuse. |
| `Pickups/CoinBehaviour` | `Coin` prefab | Tracks collected state, spawns the coin sparkle FX, and returns itself to the pool once collected. |
| `Pooling/ObjectPoolManager` | `ObjectPoolManager` | Generic id-keyed object pools (`UnityEngine.Pool.ObjectPool`) for hazards, rockets, and coins; notifies `IPoolable` components on spawn/despawn. |
| `Pooling/IPoolable` | (interface) | `OnSpawned()`/`OnDespawned()` hooks implemented by pooled components to reset per-spawn state. |
| `Spawning/SpawnManager` | `SpawnManager` | Runs independent obstacle, rocket, and coin spawn loops timed by `DifficultyEvaluator`, spawning from the pool and configuring each instance's `HazardMover`. Obstacles cluster in pairs late in the ramp (kept within `MaxClusterYDelta` so an open lane exists); rockets come in distance-based volleys of 1–3 (`rocketPairsFromMeters`, `rocketTriplesFromMeters`), each with its own warning; coins spawn as singles or arcing chains of 5–7. |
| `Audio/AudioManager` | `AudioManager` | Observer on `GameManager`/`PlayerController`/`SpawnManager` events: switches menu/gameplay music and plays coin, death, rocket-launch SFX and the jetpack loop. |
| `UI/UIManager` | `UIManager` | Switches Title/HUD/Game Over panels per `GameState` and updates distance/coins/high-score text. |

**Editor tooling (`Assets/Editor/PrefabBuilder.cs`):** menu items under **Jetpack Ride/** that generate or wire assets — `Build Prefabs` (base prefabs; overwrites hand-scaled ones, so avoid re-running), `Build Rocket Warning`, `Build Particle FX`, `Wire Audio And Build Settings`, and `Wire Death Animation And Coins`.

---

## Scene Setup & Audio

`Assets/Scenes/MainGame.unity` wiring:

* **`GameManager`** — the `GameManager` component, assigned a `GameConfig` asset.
* **`ObjectPoolManager`** — pool entries `obstacle`, `rocket`, `coin`, and `rocketWarning` (`Obstacle_Zapper`, `Rocket`, `Coin`, `RocketWarning` prefabs).
* **`SpawnManager`**, **`RunResetService`**, **`DistanceTracker`** — gameplay-loop services described above.
* **`Player`** — `Rigidbody2D` + `PlayerController` + `RestartController` + `PlayerDeathAnimator`, clamped to the play area (`MinY`/`MaxY`); references the `FX_JetpackSpark` child and `FX_Explosion` prefab.
* **`AudioManager`** — music/SFX clips from `Assets/Audio`, bound to `GameManager`, `Player`, and `SpawnManager`.
* **`Background_1` / `Background_2`** — two `ParallaxLayer` tiles (`BackdropMain` sprite) that infinitely scroll left and wrap, giving the illusion of an endless track.
* **`UIManager`** — Title / HUD / Game Over panels, TMP text bound to `GameManager` events, font applied from `Assets/Art/Fonts` (New Athletic M54).

`MainGame` is the only scene in Build Settings.

**Audio:** `AudioManager` (observer) plays `mainmenu.wav` on the title screen and `Gameplay.wav` during a run (both streamed), `right.wav` on coin pickup, `DiedEletricity.wav` on death, the launch sound on each rocket spawn, and a `FlyTest.wav` loop while thrusting.

---

## Features & Current Status

**Working:**
* Get Ready → Running → Game Over state machine with event-driven UI updates.
* Jetpack thrust/gravity physics with player bounds clamping.
* Object pooling for obstacles, rockets, and coins (no runtime allocation churn).
* Distance-driven difficulty ramp: scroll speed, obstacle/rocket spawn rate, and rocket homing aggression all scale with distance travelled.
* Rockets scroll faster than static zapper obstacles (`GameConfig.RocketSpeedMultiplier`) and can optionally home in on the player.
* Coin collection feeding into score, alongside distance.
* Persisted high score (`PlayerPrefs`) shown on the title screen and Game Over panel.
* Full run reset on restart — no leftover hazards from the previous run.
* Infinite-scrolling background (2-tile belt, `ParallaxLayer` + `TileCount`).
* Coins spawn as single pickups and arcing chains.
* Random rocket volleys: pairs from 250m, up to three from 500m, always with a flyable lane.
* Death animation (dead sprite, hop, backward tumble).
* Rocket warning telegraph, particle FX (jetpack sparks, coin sparkle, death explosion), paired obstacles late in a run, and ease-in difficulty curves (GDD §8.2 polish).
* Music and SFX wired to gameplay events.
* EditMode/PlayMode automated test suites (NUnit) covering game state, difficulty, pooling, spawning (patterns, volleys, clusters), audio, rocket warnings, death animation, and player/hazard behaviour.

**Pending / known gaps:**
* `MainGame.unity` layout (sizing, HUD placement) is still being iterated on — not yet considered final.

**Planned — Phase 9: Game Feel & Juice** ([plan](docs/superpowers/plans/2026-09-29-game-feel.md), not started):
* Zapper speed multiplier in `GameConfig` (independent of scroll speed).
* Vertical, horizontal, and diagonal zappers with length variants, always leaving a flyable lane.
* Coins only in batches, in shapes: line, arc, arrow, box, rectangle.
* Safety distance so coins and zappers never overlap or spawn touching.
* Rocket lock-on: the warning tracks the player's height for 1–2s, locks, then the rocket flies straight (replaces in-flight homing).
* Running animation on the floor (asset source still being decided).
* Forward body tilt while free-falling.
* Optional start animation (code-only run-in vs. wall-break still being decided).

---

## Development Notes

* **First open of a fresh clone:** Unity starts in an empty untitled scene (blue screen). Open `Assets/Scenes/MainGame.unity` once; Unity remembers it afterwards.
* **MCP for Unity:** the `com.coplaydev.unity-mcp` package lets an AI agent drive the editor (run tests, take screenshots). Its test runner may set `m_EnterPlayModeOptions` to `1` (skip domain reload) in `ProjectSettings/EditorSettings.asset` — do not commit that; revert with `git restore ProjectSettings/EditorSettings.asset`.
