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

The core gameplay flow is managed by a centralized state machine via `GameManager.State` (`GameState` enum):

* **`NotStarted`**  
  Displays the main start screen (animated floating logo and tap prompt).
* **`Playing`**  
  WIP
* **`GameOver`**  
  Triggered when the player collides with a zapper or a rocket.

---

## Architecture & Scripts (`Assets/Scripts`)

| Script | Attached To | Description |
|---|---|---|
WIP

---

## Scene Setup & Audio

WIP

---

## Features & Current Status

WIP
