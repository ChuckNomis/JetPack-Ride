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
  Active gameplay: the player takes thrust input, hazards/rockets/coins spawn and scroll in from the right, distance and score tick up (`DistanceTracker`), and difficulty (scroll speed, spawn rate, rocket aggression) ramps up with distance via `DifficultyEvaluator`.
* **`GameOver`**  
  Triggered when the player collides with a zapper or a rocket (`PlayerController.OnTriggerEnter2D`). Shows final distance/coins and high score. A short input lockout (`RestartController`, `GameConfig.RestartLockoutSeconds`) prevents an accidental instant restart; pressing again returns to `GetReady`.

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
| `Player/PlayerController` | `Player` | Reads thrust input, applies upward force while held, clamps the player between `MinY`/`MaxY`, and detects hazard/coin collisions via trigger. |
| `Player/RestartController` | `Player` | Handles the restart input action, including the post-death lockout timer. |
| `Hazards/HazardMover` | `Obstacle_Zapper`, `Rocket` prefabs | Moves a spawned hazard left at a configured speed and despawns it back to its pool once it scrolls past `despawnX`. |
| `Hazards/ObstacleBehaviour` | `Obstacle_Zapper` prefab | Marker component for static hazards; death is driven by the `Hazard` tag + collider, read by `PlayerController`. |
| `Hazards/RocketBehaviour` | `Rocket` prefab | Optional homing: steers the rocket's Y toward the player at a capped rate when spawned as a homing rocket. |
| `Pickups/CoinBehaviour` | `Coin` prefab | Tracks collected state and returns itself to the pool once collected. |
| `Pooling/ObjectPoolManager` | `ObjectPoolManager` | Generic id-keyed object pools (`UnityEngine.Pool.ObjectPool`) for hazards, rockets, and coins; notifies `IPoolable` components on spawn/despawn. |
| `Pooling/IPoolable` | (interface) | `OnSpawned()`/`OnDespawned()` hooks implemented by pooled components to reset per-spawn state. |
| `Spawning/SpawnManager` | `SpawnManager` | Runs independent obstacle/rocket spawn loops timed by `DifficultyEvaluator`, spawning from the pool and configuring each instance's `HazardMover`. |
| `UI/UIManager` | `UIManager` | Switches Title/HUD/Game Over panels per `GameState` and updates distance/coins/high-score text. |

---

## Scene Setup & Audio

`Assets/Scenes/MainGame.unity` wiring:

* **`GameManager`** — the `GameManager` component, assigned a `GameConfig` asset.
* **`ObjectPoolManager`** — pool entries for `Obstacle_Zapper`, `Rocket`, and `Coin` prefabs.
* **`SpawnManager`**, **`RunResetService`**, **`DistanceTracker`** — gameplay-loop services described above.
* **`Player`** — `Rigidbody2D` + `PlayerController` + `RestartController`, clamped to the play area (`MinY`/`MaxY`).
* **`Background_1` / `Background_2`** — two `ParallaxLayer` tiles (`BackdropMain` sprite) that infinitely scroll left and wrap, giving the illusion of an endless track.
* **`UIManager`** — Title / HUD / Game Over panels, TMP text bound to `GameManager` events, font applied from `Assets/Art/Fonts` (New Athletic M54).

**Audio:** raw sound assets are imported (`Assets/Audio/` — zap, launch, death, fly, menu, gameplay stingers) but not yet wired to any `AudioSource`/gameplay event. Hooking these up is still pending.

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
* EditMode/PlayMode automated test suites (NUnit) covering game state, difficulty, pooling, spawning, and player/hazard behaviour.

**Pending / known gaps:**
* Audio is imported but not yet wired to gameplay events (thrust, coin pickup, death, menu).
* `MainGame.unity` layout (sizing, HUD placement) is still being iterated on — not yet considered final.
