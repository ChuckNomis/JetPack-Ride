using System.Threading;
using UnityEngine;
using JetpackRide.Core;

namespace JetpackRide.Player
{
    // Death animation: on GameOver the player swaps to the dead sprite, hops, and tumbles backward
    // while falling to the floor; GetReady restores the flying pose for the next run. Sprite and
    // rotation live on `target` (the child Visual), so the root's collider never rotates.
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerDeathAnimator : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private SpriteRenderer target;
        [SerializeField] private Sprite deadSprite;
        [SerializeField] private float hopSpeed = 6f;
        [SerializeField] private float tumbleSeconds = 0.6f;
        [SerializeField] private float tumbleDegrees = 360f;

        private SpriteRenderer spriteRenderer;
        private Rigidbody2D body;
        private Sprite aliveSprite;
        private CancellationTokenSource tumbleCts;

        private void Awake()
        {
            if (target == null) target = GetComponentInChildren<SpriteRenderer>();
            spriteRenderer = target;
            body = GetComponent<Rigidbody2D>();
        }

        // Wiring in Start/OnDestroy so serialized (or test-injected) references are assigned first.
        private void Start()
        {
            aliveSprite = spriteRenderer.sprite;
            gameManager.StateChanged += HandleStateChanged;
        }

        private void OnDestroy()
        {
            if (gameManager != null) gameManager.StateChanged -= HandleStateChanged;
            tumbleCts?.Cancel();
        }

        private void HandleStateChanged(GameState state)
        {
            tumbleCts?.Cancel();
            if (state == GameState.GameOver)
            {
                if (deadSprite != null) spriteRenderer.sprite = deadSprite;
                body.linearVelocity = new Vector2(0f, hopSpeed);
                tumbleCts = new CancellationTokenSource();
                TumbleAsync(tumbleCts.Token).Forget();
            }
            else
            {
                spriteRenderer.sprite = aliveSprite;
                spriteRenderer.transform.localRotation = Quaternion.identity;
            }
        }

        private async Awaitable TumbleAsync(CancellationToken token)
        {
            float elapsed = 0f;
            while (elapsed < tumbleSeconds)
            {
                await Awaitable.NextFrameAsync(token);
                elapsed += Time.deltaTime;
                // Backward flip (counter-clockwise), easing out so it settles lying flat.
                float t = Mathf.Clamp01(elapsed / tumbleSeconds);
                float eased = 1f - (1f - t) * (1f - t);
                spriteRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, eased * tumbleDegrees);
            }
            spriteRenderer.transform.localRotation = Quaternion.identity;
        }
    }
}
