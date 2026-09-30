using UnityEngine;
using JetpackRide.Pooling;

namespace JetpackRide.Hazards
{
    public class RocketWarningIndicator : MonoBehaviour, IPoolable
    {
        [SerializeField] private Color lockedTint = new(1f, 0.35f, 0.3f, 1f);
        [SerializeField] private float lockBlinkHz = 10f;
        [SerializeField] private float lockPulseScale = 1.25f;

        private ObjectPoolManager pool;
        private string poolId;
        private int spawnGeneration;
        private SpriteRenderer spriteRenderer;
        private Color baseColor = Color.white;
        private Vector3 baseScale = Vector3.one;
        private bool visualsCaptured;

        public bool IsLocked { get; private set; }
        // Raised each time the lock blink turns red (audio hooks in via SpawnManager).
        public event System.Action LockBlinked;

        public void Configure(ObjectPoolManager pool, string poolId)
        {
            this.pool = pool;
            this.poolId = poolId;
        }

        public async Awaitable PlayAndDespawnAsync(float lifetimeSeconds)
        {
            // If a run reset despawns this warning and the pool hands it out again before the
            // delay ends, the generation changes and this stale call must leave the new use alone.
            int generation = spawnGeneration;
            await Awaitable.WaitForSecondsAsync(lifetimeSeconds);
            if (this == null || pool == null || generation != spawnGeneration) return;
            pool.Despawn(poolId, gameObject);
        }

        // Follows the player's Y (clamped to yRange, at most trackSpeed units/s) for trackSeconds,
        // then locks: holds still, blinks and pulses for lockSeconds, despawns, and returns the
        // locked Y. Returns NaN if the warning was despawned/reused meanwhile (the stale guard).
        public async Awaitable<float> TrackAndLockAsync(Transform player, float trackSeconds, float lockSeconds, float trackSpeed, Vector2 yRange)
        {
            int generation = spawnGeneration;
            CaptureVisuals();

            for (float t = 0f; t < trackSeconds; t += Time.deltaTime)
            {
                await Awaitable.NextFrameAsync();
                if (IsStale(generation)) return float.NaN;
                if (player == null) continue;
                var pos = transform.position;
                float target = Mathf.Clamp(player.position.y, yRange.x, yRange.y);
                pos.y = Mathf.MoveTowards(pos.y, target, trackSpeed * Time.deltaTime);
                transform.position = pos;
            }

            IsLocked = true;
            float lockedY = transform.position.y;
            bool wasBlinkOn = false;
            for (float t = 0f; t < lockSeconds; t += Time.deltaTime)
            {
                bool blinkOn = Mathf.Repeat(t * lockBlinkHz, 1f) < 0.5f;
                if (blinkOn && !wasBlinkOn) LockBlinked?.Invoke();
                wasBlinkOn = blinkOn;
                if (spriteRenderer != null) spriteRenderer.color = blinkOn ? lockedTint : baseColor;
                transform.localScale = baseScale * Mathf.Lerp(1f, lockPulseScale, Mathf.PingPong(t * lockBlinkHz * 2f, 1f));
                await Awaitable.NextFrameAsync();
                if (IsStale(generation)) return float.NaN;
            }

            ResetVisuals();
            if (pool != null) pool.Despawn(poolId, gameObject);
            return lockedY;
        }

        // Inactive = despawned by a run reset; a changed generation = despawned and handed out again.
        private bool IsStale(int generation) => this == null || !gameObject.activeSelf || generation != spawnGeneration;

        private void CaptureVisuals()
        {
            if (visualsCaptured) return;
            visualsCaptured = true;
            baseScale = transform.localScale;
            if (TryGetComponent(out spriteRenderer)) baseColor = spriteRenderer.color;
        }

        private void ResetVisuals()
        {
            IsLocked = false;
            if (!visualsCaptured) return;
            transform.localScale = baseScale;
            if (spriteRenderer != null) spriteRenderer.color = baseColor;
        }

        public void OnSpawned()
        {
            spawnGeneration++;
            ResetVisuals();
        }

        public void OnDespawned() => ResetVisuals();
    }
}
