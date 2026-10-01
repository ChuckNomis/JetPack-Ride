using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using JetpackRide.Core;
using JetpackRide.Input;

namespace JetpackRide.UI
{
    // Esc / gamepad Start toggles pause mid-run (GameManager decides when that's allowed) and shows
    // the pause panel with Continue and Restart. Lives on an always-active object, not on the panel.
    // The HUD pause button does the same as Esc, but only pauses: it is shown during the intro and
    // the run, and is not clickable while the pause panel is up.
    public class PauseMenu : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button pauseButton;

        private PlayerInputActions actions;

        private void Awake()
        {
            actions = new PlayerInputActions();
            actions.Gameplay.Pause.performed += OnPauseInput;
        }

        // Wiring in Start/OnDestroy so serialized (or test-injected) references are assigned first.
        private void Start()
        {
            gameManager.PausedChanged += HandlePausedChanged;
            continueButton.onClick.AddListener(gameManager.Resume);
            restartButton.onClick.AddListener(gameManager.RestartRun);
            pausePanel.SetActive(gameManager.IsPaused);

            gameManager.StateChanged += HandleStateChanged;
            pauseButton.onClick.AddListener(gameManager.Pause);
            HandleStateChanged(gameManager.CurrentState);
            pauseButton.interactable = !gameManager.IsPaused;
        }

        private void OnEnable()
        {
            actions.Gameplay.Enable();
        }

        private void OnDisable()
        {
            actions.Gameplay.Disable();
        }

        private void OnDestroy()
        {
            if (gameManager != null)
            {
                gameManager.PausedChanged -= HandlePausedChanged;
                gameManager.StateChanged -= HandleStateChanged;
            }
            if (continueButton != null) continueButton.onClick.RemoveListener(gameManager.Resume);
            if (restartButton != null) restartButton.onClick.RemoveListener(gameManager.RestartRun);
            if (pauseButton != null) pauseButton.onClick.RemoveListener(gameManager.Pause);
            actions.Gameplay.Pause.performed -= OnPauseInput;
            actions.Dispose();
        }

        private void OnPauseInput(InputAction.CallbackContext ctx) => gameManager.TogglePause();

        private void HandleStateChanged(GameState state)
        {
            pauseButton.gameObject.SetActive(state == GameState.Running || state == GameState.Intro);
        }

        private void HandlePausedChanged(bool paused)
        {
            pausePanel.SetActive(paused);
            pauseButton.interactable = !paused;
            // Pre-select Continue so keyboard/gamepad users can confirm without the mouse.
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(paused ? continueButton.gameObject : null);
        }
    }
}
