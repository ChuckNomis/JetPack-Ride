using UnityEngine;
using JetpackRide.Core;

namespace JetpackRide.Player
{
    public enum PlayerPose { Fly, Run }

    // Pose visuals observed from PlayerController/GameManager: a run cycle (sprite swap in code, no
    // Animator) while Running on the floor and not thrusting, the fly sprite otherwise. GameOver is
    // left to PlayerDeathAnimator. With no run frames it keeps the fly sprite and, when the sprite
    // sits on a child object, bobs it slightly.
    public class PlayerVisuals : MonoBehaviour
    {
        [SerializeField] private PlayerController controller;
        [SerializeField] private GameManager gameManager;
        [SerializeField] private SpriteRenderer target;
        [SerializeField] private Sprite[] runFrames;
        [SerializeField] private float runFps = 12f;
        [SerializeField] private float floorEpsilon = 0.05f;
        [SerializeField] private float fallbackBobHeight = 0.04f;
        [SerializeField] private float fallbackBobHz = 6f;

        private Sprite flySprite;
        private Vector3 targetRestLocalPosition;
        private bool thrusting;
        private float runClock;

        public PlayerPose Pose { get; private set; }

        // Wiring in Start/OnDestroy so serialized (or test-injected) references are assigned first.
        private void Start()
        {
            if (target == null) target = GetComponentInChildren<SpriteRenderer>();
            flySprite = target.sprite;
            targetRestLocalPosition = target.transform.localPosition;
            controller.ThrustingChanged += HandleThrustingChanged;
        }

        private void OnDestroy()
        {
            if (controller != null) controller.ThrustingChanged -= HandleThrustingChanged;
        }

        private void HandleThrustingChanged(bool value) => thrusting = value;

        private bool CanBob => target.transform != controller.transform;

        private void LateUpdate()
        {
            var state = gameManager.CurrentState;
            if (state == GameState.GameOver) return;

            bool onFloor = controller.transform.position.y <= controller.MinY + floorEpsilon;
            Pose = state == GameState.Running && onFloor && !thrusting ? PlayerPose.Run : PlayerPose.Fly;

            if (Pose == PlayerPose.Run)
            {
                runClock += Time.deltaTime;
                if (runFrames != null && runFrames.Length > 0)
                {
                    int index = (int)(runClock * runFps) % runFrames.Length;
                    target.sprite = runFrames[index] != null ? runFrames[index] : flySprite;
                }
                else
                {
                    target.sprite = flySprite;
                    if (CanBob)
                    {
                        float bob = Mathf.Abs(Mathf.Sin(runClock * fallbackBobHz * Mathf.PI)) * fallbackBobHeight;
                        target.transform.localPosition = targetRestLocalPosition + Vector3.up * bob;
                    }
                }
                return;
            }

            runClock = 0f;
            target.sprite = flySprite;
            if (CanBob) target.transform.localPosition = targetRestLocalPosition;
        }
    }
}
