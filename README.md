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
| `Core/GameManager` | `GameManager` | Central state machine (`GameState`: `GetReady`/`Intro`/`Running`/`GameOver`; `StartIntro()` from the title, `BeginRun()` when the intro ends). Owns distance, coins, score, and persisted high score (`PlayerPrefs`); fires `StateChanged`/`DistanceChanged`/`CoinsChanged`/`ScoreChanged` events. |
| `Core/GameConfig` | ScriptableObject asset | Tunable balance values: scroll speed curve, jetpack thrust/gravity, rocket, zapper and coin speed multipliers, obstacle/rocket spawn intervals, difficulty ramp curve, rocket lock-on timing (track/lock seconds, track speed), coin value, restart lockout. |
| `Core/DifficultyEvaluator` | (static, no GameObject) | Pure function mapping distance travelled → a `DifficultySnapshot` (scroll speed, spawn intervals, ramp progress), driven by `GameConfig`'s curves. |
| `Core/DistanceTracker` | `DistanceTracker` | Ticks `GameManager.AddDistance()` each frame using the current scroll speed while `Running`. |
| `Core/RunResetService` | `RunResetService` | On transition to `GetReady`, despawns every pooled hazard/coin so a new run starts clean. |
| `Environment/ParallaxLayer` | `Background_1` / `Background_2` | Infinite horizontal scroll: wraps a tile back by `TileWidth * TileCount` once it scrolls past `-TileWidth`, so multiple equal-speed tiles stay offset instead of converging. |
| `Player/PlayerController` | `Player` | Reads thrust input, applies upward force while held, clamps the player between `MinY`/`MaxY`, and detects hazard/coin collisions via trigger. Plays the jetpack spark particles while thrusting and spawns the death explosion. |
| `Player/PlayerDeathAnimator` | `Player` | Observer on `GameManager.StateChanged`: on `GameOver` swaps the child `Visual`'s sprite to the dead sprite, hops, and tumbles it a full backward flip (async `Awaitable`); `GetReady` restores the flying pose. |
| `Player/PlayerVisuals` | `Player` | Observer on `PlayerController.ThrustingChanged`/`GameManager` state: run cycle (code sprite swap) on the floor while `Running` and not thrusting, fly sprite otherwise, plus free-fall forward tilt of the child `Visual`; leaves `GameOver` to `PlayerDeathAnimator`; hides the player on the title screen (`GetReady`) so the first appearance is the intro walk-in. |
| `Player/PlayerShadow` | `Player` | Runtime-generated oval shadow on the floor under the player (its own top-level object, so the root scale and death tumble don't distort it): full size on the floor, shrinking and fading to a small dot at `MaxY`. Hidden on the title screen like the player. Runs in edit mode (`[ExecuteAlways]`) so `feetOffset`, `groundSize`/`dotSize`, `groundAlpha`/`dotAlpha`, and `edgeSoftness` can be tuned live in the Inspector. |
| `Core/IntroSequence` | `Player` | Observer on `GameManager.StateChanged`: on `Intro`, places the player off-screen left on the floor, shakes the camera, pauses, walks the player in to its home X while the world stands still, then calls `BeginRun()`. Stops without starting the run if the state changes or it is destroyed mid-intro. |
| `Environment/CameraShake` | `Main Camera` | `Shake(seconds, strength)`: fading random jitter; restores the exact rest position (also on disable or overlapping shakes). |
| `Player/RestartController` | `Player` | Handles the restart input action, including the post-death lockout timer. |
| `Hazards/HazardMover` | `Obstacle_Zapper`, `Rocket`, `Coin` prefabs | Moves a spawned object left at a configured speed and despawns it back to its pool once it scrolls past `despawnX`. `Frozen` stops it in place (set on game over; cleared on every spawn). |
| `Hazards/ObstacleBehaviour` | `Obstacle_Zapper` prefab | Marker component for static hazards; death is driven by the `Hazard` tag + collider, read by `PlayerController`. |
| `Hazards/RocketBehaviour` | `Rocket` prefab | Marker component; rockets fly straight (aiming happens before launch via the tracking warning). |
| `Hazards/ZapperShape` | `Obstacle_Zapper` prefab | Sizes a 9-sliced zapper to a length and orientation (collider hugs the beam), cycles the 4 flicker frames, and resets on pool reuse. |
| `Hazards/RocketWarningIndicator` | `RocketWarning` prefab | Pooled warning telegraph shown at the right screen edge before a rocket arrives; despawns itself after the lead time and ignores stale despawns after pool reuse. `TrackAndLockAsync` follows the player's height, then blinks/pulses as it locks and returns the locked Y. |
| `Pickups/CoinBehaviour` | `Coin` prefab | Tracks collected state, spawns the coin sparkle FX, and returns itself to the pool once collected. |
| `Pooling/ObjectPoolManager` | `ObjectPoolManager` | Generic id-keyed object pools (`UnityEngine.Pool.ObjectPool`) for hazards, rockets, and coins; notifies `IPoolable` components on spawn/despawn. |
| `Pooling/IPoolable` | (interface) | `OnSpawned()`/`OnDespawned()` hooks implemented by pooled components to reset per-spawn state. |
| `Spawning/SpawnManager` | `SpawnManager` | Runs independent obstacle, rocket, and coin spawn loops timed by `DifficultyEvaluator`, spawning from the pool and configuring each instance's `HazardMover`. Zappers come in mixed orientations/lengths and pair up late in the ramp, laid out by `ZapperLayout` so an open lane (`MinZapperLane`) always exists; rockets come in distance-based volleys of 1–3 (`rocketPairsFromMeters`, `rocketTriplesFromMeters`), each with its own warning; one locks on and launches after the fixed ones; coins come only in `CoinPatterns` batches. Tracks live coins/zappers (each coin tagged with its batch) so they keep `CoinZapperClearance` apart (`SpawnSafety`). A zapper that would touch coins is shifted vertically, else pushed right past the whole batch (up to `maxZapperPush`), and the next obstacle waits the same extra time so zapper spacing is kept; only if that fails are whole batches removed — a coin shape is never cut. On `GameOver` it freezes every live zapper and coin. |
| `Audio/AudioManager` | `AudioManager` | Observer on `GameManager`/`PlayerController`/`SpawnManager` events: switches menu/gameplay music (silent during `Intro`, which plays the start explosion) and plays coin, death, rocket-launch SFX, a beep on each red blink of a locking rocket warning (`wrong_blink.wav`, `wrong.wav` sped up 2.5x to fit a blink), footsteps from `PlayerVisuals.Footstep` (own source, random pitch per step; `footstepSfx` unassigned until a clip is chosen) and the jetpack loop. Each SFX has its own volume slider (0–1), scaled by the master `sfxVolume`. |
| `UI/UIManager` | `UIManager` | Switches Title/HUD/Game Over panels per `GameState` and updates distance/coins/high-score text; resets the HUD coin count to the new run's value as soon as `Running` starts. |

**Editor tooling (`Assets/Editor/PrefabBuilder.cs`):** menu items under **Jetpack Ride/** that generate or wire assets — `Build Prefabs` (base prefabs; overwrites hand-scaled ones, so avoid re-running), `Build Rocket Warning`, `Build Particle FX`, `Wire Audio And Build Settings`, `Wire Death Animation And Coins`, `Build Zapper Variants` (9-slices the zapper sprites and adds `ZapperShape` + flicker frames to `Obstacle_Zapper`), `Wire Player Visuals` (moves the Player sprite onto a child `Visual` and wires `PlayerVisuals`/`PlayerDeathAnimator` to it, importing and assigning the run frames), and `Wire Start Intro` (adds `IntroSequence` to the Player and `CameraShake` to Main Camera, and assigns `startExplosion1.mp3` to the AudioManager).

---

## Scene Setup & Audio

`Assets/Scenes/MainGame.unity` wiring:

* **`GameManager`** — the `GameManager` component, assigned a `GameConfig` asset.
* **`ObjectPoolManager`** — pool entries `obstacle`, `rocket`, `coin`, and `rocketWarning` (`Obstacle_Zapper`, `Rocket`, `Coin`, `RocketWarning` prefabs).
* **`SpawnManager`**, **`RunResetService`**, **`DistanceTracker`** — gameplay-loop services described above.
* **`Player`** — `Rigidbody2D` + `PlayerController` + `RestartController` + `PlayerDeathAnimator` + `PlayerShadow`, clamped to the play area (`MinY`/`MaxY`); references the `FX_JetpackSpark` child and `FX_Explosion` prefab.
* **`AudioManager`** — music/SFX clips from `Assets/Audio`, bound to `GameManager`, `Player`, and `SpawnManager`.
* **`Background_1` / `Background_2`** — two `ParallaxLayer` tiles (`BackdropMain` sprite) that infinitely scroll left and wrap, giving the illusion of an endless track.
* **`UIManager`** — Title / HUD / Game Over panels, TMP text bound to `GameManager` events, font applied from `Assets/Art/Fonts` (New Athletic M54). The HUD's `CoinsText` sizes to its number (`ContentSizeFitter`) and has a `CoinIcon` child (`Coin` sprite) anchored to its right edge, so the icon follows the count as it widens.

`MainGame` is the only scene in Build Settings.

**Audio:** `AudioManager` (observer) plays `mainmenu.wav` on the title screen and `Gameplay.wav` during a run (both streamed), `right.wav` on coin pickup, `DiedEletricity.wav` on death, the launch sound on each rocket spawn, and a `FlyTest.wav` loop while thrusting.

---

## Features & Current Status

**Working:**
* Get Ready → Running → Game Over state machine with event-driven UI updates.
* Jetpack thrust/gravity physics with player bounds clamping.
* Object pooling for obstacles, rockets, and coins (no runtime allocation churn).
* Distance-driven difficulty ramp: scroll speed, obstacle/rocket spawn rate, and rocket lock-on track time all scale with distance travelled.
* Rockets scroll faster than static zapper obstacles (`GameConfig.RocketSpeedMultiplier`) and fly straight.
* Rocket lock-on: from the start of a run, every volley has one warning that follows the player's height (capped speed), blinks red for 0.3s as it locks, then the rocket launches straight at the locked height. In a multi-rocket volley the other rockets are fixed: they get the normal 0.5s warning and launch first, so none is dropped and they never form a wall with the tracked one. The track time shrinks from 2s early to 1s at full difficulty (`GameConfig.RocketTrackSeconds`) — that is the rocket difficulty.
* Zapper and coin speeds are independently tunable (`GameConfig.ZapperSpeedMultiplier`, `GameConfig.CoinSpeedMultiplier`; `1.0` = locked to the background). Both are 0.5 so coins and zappers move together.
* Vertical, horizontal, and ±45° diagonal zappers in three lengths (9-sliced sprite, 4-frame flicker); only vertical early on, and a cluster always leaves a flyable lane (`ZapperLayout`).
* Coins and zappers keep at least 1 unit apart for as long as both are on screen, even when zappers drift (`SpawnSafety`); coins re-roll their height or skip, zappers shift vertically or are pushed right past the whole coin batch (a later, slightly faster zapper is nudged right so it can't catch up with an on-screen batch). Coin shapes are never cut: at worst a whole batch is removed.
* Coin collection feeding into score, alongside distance.
* Persisted high score (`PlayerPrefs`) shown on the title screen and Game Over panel.
* Full run reset on restart — no leftover hazards from the previous run.
* Infinite-scrolling background (2-tile belt, `ParallaxLayer` + `TileCount`).
* Coins only spawn in batches (`CoinPatterns`): line, filled arc, filled arrow (`>`), filled box and filled rectangle, all available from the start of a run; patterns grow with difficulty.
* Zappers and coins freeze in place with the rest of the world when the player dies.
* Ground shadow under the player that shrinks to a dot as they fly higher (`PlayerShadow`).
* HUD coin count with a coin icon that follows the number.
* Random rocket volleys: pairs from 250m, up to three from 500m, always with a flyable lane.
* Death animation (dead sprite, hop, backward tumble).
* Start intro: pressing start plays the start explosion (`startExplosion1.mp3`) with a camera shake, then the player walks in from off-screen left before the run begins (`IntroSequence`, `CameraShake`).
* Run pose on the floor (`PlayerVisuals`): plays an 8-frame run cycle (`Art/Sprites/PlayerRun/`, sliced from `Source/sprites/character-running-frames.png`) while running on the floor and not thrusting, fly sprite otherwise.
* Forward tilt (up to 12°, eased by fall speed) while free-falling; the sprite lives on a child `Visual`, so tilt and the death tumble never rotate the collider.
* Rocket warning telegraph, particle FX (jetpack sparks, coin sparkle, death explosion), paired obstacles late in a run, and ease-in difficulty curves (GDD §8.2 polish).
* Music and SFX wired to gameplay events, including a beep per lock-on blink and run-cycle footsteps (`PlayerVisuals.footstepFrames`, default frames 0 and 4).
* EditMode/PlayMode automated test suites (NUnit) covering game state, difficulty, pooling, spawning (patterns, volleys, clusters), audio, rocket warnings, death animation, and player/hazard behaviour.

**Pending / known gaps:**
* `MainGame.unity` layout (sizing, HUD placement) is still being iterated on — not yet considered final.

**Phase 9: Game Feel & Juice** ([plan](docs/superpowers/plans/2026-09-29-game-feel.md)): complete.

---

## Development Notes

* **First open of a fresh clone:** Unity starts in an empty untitled scene (blue screen). Open `Assets/Scenes/MainGame.unity` once; Unity remembers it afterwards.
* **MCP for Unity:** the `com.coplaydev.unity-mcp` package lets an AI agent drive the editor (run tests, take screenshots). Its test runner may set `m_EnterPlayModeOptions` to `1` (skip domain reload) in `ProjectSettings/EditorSettings.asset` — do not commit that; revert with `git restore ProjectSettings/EditorSettings.asset`.
