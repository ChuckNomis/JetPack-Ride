using UnityEngine;
using JetpackRide.Core;
using JetpackRide.Input;
using JetpackRide.Pickups;

namespace JetpackRide.Player
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private GameConfig config;
        [SerializeField] private GameManager gameManager;

        public float MinY = -3.5f;
        public float MaxY = 4.5f;

        [SerializeField] private ParticleSystem jetpackSpark;
        [SerializeField] private GameObject explosionPrefab;

        public event System.Action Died;
        public event System.Action<bool> ThrustingChanged;

        private Rigidbody2D body;
        private PlayerInputActions actions;
        private bool pendingThrustHeld;
        private bool thrusting;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            actions = new PlayerInputActions();
        }

        // Config-dependent setup lives in Start, not Awake, so serialized (or test-injected)
        // references are guaranteed to be assigned before they're read.
        private void Start()
        {
            body.gravityScale = config.GravityScale;
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
            actions.Dispose();
        }

        private void Update()
        {
            pendingThrustHeld = actions.Gameplay.Thrust.IsPressed();
        }

        private void FixedUpdate()
        {
            // Clamp in every state, so the player rests on the floor during GetReady/GameOver
            // instead of falling out of the play area.
            ClampToPlayBounds();

            bool running = gameManager.CurrentState == GameState.Running;
            SetThrusting(running && pendingThrustHeld);
            if (!running) return;

            ApplyThrust(pendingThrustHeld);
        }

        // Driven from FixedUpdate rather than ApplyThrust, which returns early outside Running and
        // would leave sparks (and the jetpack sound) running after death.
        private void SetThrusting(bool value)
        {
            if (jetpackSpark != null)
            {
                if (value && !jetpackSpark.isEmitting) jetpackSpark.Play();
                else if (!value && jetpackSpark.isEmitting) jetpackSpark.Stop();
            }

            if (value == thrusting) return;
            thrusting = value;
            ThrustingChanged?.Invoke(value);
        }

        private void ClampToPlayBounds()
        {
            var pos = body.position;
            if (pos.y <= MaxY && pos.y >= MinY) return;

            pos.y = Mathf.Clamp(pos.y, MinY, MaxY);
            body.position = pos;
            var velocity = body.linearVelocity;
            if ((velocity.y > 0f && pos.y >= MaxY) || (velocity.y < 0f && pos.y <= MinY))
            {
                body.linearVelocity = new Vector2(velocity.x, 0f);
            }
        }

        public void ApplyThrust(bool held)
        {
            if (gameManager.CurrentState != GameState.Running) return;
            if (held)
            {
                body.AddForce(Vector2.up * config.JetpackThrust, ForceMode2D.Force);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            // State guard first: once EndRun() has left Running, a coin overlapping on the same
            // frame as the death is not credited.
            if (gameManager.CurrentState != GameState.Running) return;

            if (other.TryGetComponent<CoinBehaviour>(out var coin))
            {
                if (!coin.Collected)
                {
                    coin.Collect();
                    gameManager.CollectCoin();
                }
                return;
            }

            if (other.CompareTag("Hazard"))
            {
                if (explosionPrefab != null) Instantiate(explosionPrefab, transform.position, Quaternion.identity);
                Died?.Invoke();
                gameManager.EndRun();
            }
        }
    }
}
