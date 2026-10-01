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
    public class PauseMenu : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button restartButton;

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
            if (gameManager != null) gameManager.PausedChanged -= HandlePausedChanged;
            if (continueButton != null) continueButton.onClick.RemoveListener(gameManager.Resume);
            if (restartButton != null) restartButton.onClick.RemoveListener(gameManager.RestartRun);
            actions.Gameplay.Pause.performed -= OnPauseInput;
            actions.Dispose();
        }

        private void OnPauseInput(InputAction.CallbackContext ctx) => gameManager.TogglePause();

        private void HandlePausedChanged(bool paused)
        {
            pausePanel.SetActive(paused);
            // Pre-select Continue so keyboard/gamepad users can confirm without the mouse.
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(paused ? continueButton.gameObject : null);
        }
    }
}
