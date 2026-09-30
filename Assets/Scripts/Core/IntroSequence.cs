using UnityEngine;
using JetpackRide.Environment;
using JetpackRide.Player;

namespace JetpackRide.Core
{
    // Observer on GameManager.StateChanged: on Intro, puts the player off-screen left on the floor,
    // shakes the camera (AudioManager plays the explosion on the same state change), pauses, walks the
    // player to its home X while the world stands still, then starts the run. Any state change,
    // destroy, or newer intro makes an in-flight sequence stop without calling BeginRun.
    public class IntroSequence : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private PlayerController player;
        [SerializeField] private CameraShake cameraShake;
        [Tooltip("World X the player starts at (just off-screen left).")]
        [SerializeField] private float startX = -11f;
        [SerializeField] private float shakeSeconds = 0.4f;
        [SerializeField] private float shakeStrength = 0.3f;
        [Tooltip("Beat between the end of the shake and the walk-in.")]
        [SerializeField] private float pauseSeconds = 0.3f;
        [SerializeField] private float walkSeconds = 1.2f;

        private Rigidbody2D body;
        private int generation;

        public float HomeX { get; private set; }

        // Wiring in Start/OnDestroy so serialized (or test-injected) references are assigned first.
        private void Start()
        {
            body = player.GetComponent<Rigidbody2D>();
            HomeX = player.transform.position.x;
            gameManager.StateChanged += HandleStateChanged;
        }

        private void OnDestroy()
        {
            generation++;
            if (gameManager != null) gameManager.StateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(GameState state)
        {
            generation++;
            if (state == GameState.Intro) PlayAsync(generation).Forget();
        }

        private bool IsStale(int gen) => this == null || gen != generation || gameManager.CurrentState != GameState.Intro;

        private async Awaitable PlayAsync(int gen)
        {
            PlaceAt(startX);
            if (cameraShake != null) cameraShake.Shake(shakeSeconds, shakeStrength).Forget();

            await Awaitable.WaitForSecondsAsync(shakeSeconds + pauseSeconds);
            if (IsStale(gen)) return;

            for (float t = 0f; t < walkSeconds; t += Time.deltaTime)
            {
                PlaceAt(Mathf.Lerp(startX, HomeX, t / walkSeconds));
                await Awaitable.NextFrameAsync();
                if (IsStale(gen)) return;
            }

            PlaceAt(HomeX);
            gameManager.BeginRun();
        }

        // Teleport (the body's X is frozen in the scene, so it can't be moved by velocity).
        private void PlaceAt(float x)
        {
            var pos = new Vector2(x, player.MinY);
            body.position = pos;
            body.linearVelocity = Vector2.zero;
            player.transform.position = new Vector3(pos.x, pos.y, player.transform.position.z);
        }
    }
}
