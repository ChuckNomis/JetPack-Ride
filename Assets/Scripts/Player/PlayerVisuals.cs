using UnityEngine;
using JetpackRide.Core;

namespace JetpackRide.Player
{
    public enum PlayerPose { Fly, Run }

    // Pose visuals observed from PlayerController/GameManager: a run cycle (sprite swap in code, no
    // Animator) while Running on the floor and not thrusting, the fly sprite otherwise. GameOver is
    // left to PlayerDeathAnimator. With no run frames it keeps the fly sprite and, when the sprite
    // sits on a child object, bobs it slightly. Also tilts the (child) sprite forward while
    // free-falling; the root, which carries the collider and rigidbody, never rotates.
    public class PlayerVisuals : MonoBehaviour
    {
        [SerializeField] private PlayerController controller;
        [SerializeField] private GameManager gameManager;
        [SerializeField] private SpriteRenderer target;
        [SerializeField] private Sprite[] runFrames;
        [SerializeField] private float runFps = 12f;
        [Tooltip("Run-cycle frame indices where a foot hits the floor; each raises Footstep.")]
        [SerializeField] private int[] footstepFrames = { 0, 4 };
        [SerializeField] private float floorEpsilon = 0.05f;
        [SerializeField] private float fallbackBobHeight = 0.04f;
        [SerializeField] private float fallbackBobHz = 6f;
        [Header("Free-fall tilt")]
        [Tooltip("Forward (clockwise) tilt in degrees at full fall speed.")]
        [SerializeField] private float maxFallTilt = 12f;
        [Tooltip("Downward speed (units/s) at which the tilt reaches maxFallTilt.")]
        [SerializeField] private float fallTiltVelocity = 6f;
        [Tooltip("Degrees per second the tilt eases toward its target.")]
        [SerializeField] private float tiltSpeed = 90f;

        private Rigidbody2D body;
        private Sprite flySprite;
        private Vector3 targetRestLocalPosition;
        private bool thrusting;
        private float runClock;
        private int lastRunFrame = -1;

        public PlayerPose Pose { get; private set; }
        public event System.Action Footstep;

        // Wiring in Start/OnDestroy so serialized (or test-injected) references are assigned first.
        private void Start()
        {
            if (target == null) target = GetComponentInChildren<SpriteRenderer>();
            flySprite = target.sprite;
            targetRestLocalPosition = target.transform.localPosition;
            body = controller.GetComponent<Rigidbody2D>();
            controller.ThrustingChanged += HandleThrustingChanged;
        }

        private void OnDestroy()
        {
            if (controller != null) controller.ThrustingChanged -= HandleThrustingChanged;
        }

        private void HandleThrustingChanged(bool value) => thrusting = value;

        private bool HasChildVisual => target.transform != controller.transform;

        private void LateUpdate()
        {
            var state = gameManager.CurrentState;
            if (state == GameState.GameOver) return; // PlayerDeathAnimator owns sprite and rotation

            bool onFloor = controller.transform.position.y <= controller.MinY + floorEpsilon;
            Pose = state == GameState.Running && onFloor && !thrusting ? PlayerPose.Run : PlayerPose.Fly;

            if (Pose == PlayerPose.Run) ShowRunFrame();
            else ShowFly();

            if (HasChildVisual) UpdateTilt(onFloor);
        }

        private void ShowRunFrame()
        {
            runClock += Time.deltaTime;
            if (runFrames != null && runFrames.Length > 0)
            {
                int index = (int)(runClock * runFps) % runFrames.Length;
                target.sprite = runFrames[index] != null ? runFrames[index] : flySprite;
                if (index != lastRunFrame && System.Array.IndexOf(footstepFrames, index) >= 0) Footstep?.Invoke();
                lastRunFrame = index;
                return;
            }

            target.sprite = flySprite;
            if (HasChildVisual)
            {
                float bob = Mathf.Abs(Mathf.Sin(runClock * fallbackBobHz * Mathf.PI)) * fallbackBobHeight;
                target.transform.localPosition = targetRestLocalPosition + Vector3.up * bob;
            }
        }

        private void ShowFly()
        {
            runClock = 0f;
            lastRunFrame = -1;
            target.sprite = flySprite;
            if (HasChildVisual) target.transform.localPosition = targetRestLocalPosition;
        }

        private void UpdateTilt(bool onFloor)
        {
            float vy = body != null ? body.linearVelocity.y : 0f;
            float targetAngle = !onFloor && !thrusting && vy < 0f
                ? -Mathf.Clamp01(-vy / fallTiltVelocity) * maxFallTilt
                : 0f;
            float current = target.transform.localEulerAngles.z;
            float next = Mathf.MoveTowardsAngle(current, targetAngle, tiltSpeed * Time.deltaTime);
            target.transform.localRotation = Quaternion.Euler(0f, 0f, next);
        }
    }
}
