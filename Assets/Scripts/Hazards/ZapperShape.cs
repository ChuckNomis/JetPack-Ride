using UnityEngine;
using JetpackRide.Pooling;

namespace JetpackRide.Hazards
{
    public enum ZapperOrientation { Vertical, Horizontal, DiagonalUp, DiagonalDown }

    // Sizes a pooled zapper to a length and orientation. The sprite is 9-sliced (orb end caps as
    // borders), so only the bolt between the orbs stretches. Rotation lives on this transform, so the
    // collider rotates with the beam.
    [RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D))]
    public class ZapperShape : MonoBehaviour, IPoolable
    {
        // Fraction of the sprite the collider covers: the glow around the beam is harmless.
        [SerializeField, Range(0.1f, 1f)] private float beamWidthFactor = 0.6f;
        [SerializeField] private float endInset = 0.06f;
        [SerializeField] private Sprite[] frames;
        [SerializeField] private float framesPerSecond = 12f;

        private SpriteRenderer spriteRenderer;
        private BoxCollider2D box;
        private float frameClock;

        // Collider thickness in world units (for spawn layout).
        public float WorldThickness => box.size.x * transform.lossyScale.x;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            box = GetComponent<BoxCollider2D>();
            spriteRenderer.drawMode = SpriteDrawMode.Sliced;
        }

        public void SetFrames(Sprite[] newFrames, float framesPerSecond)
        {
            frames = newFrames;
            this.framesPerSecond = framesPerSecond;
        }

        public void Configure(ZapperOrientation orientation, float worldLength)
        {
            float localLength = worldLength / Mathf.Max(1e-4f, transform.lossyScale.y);
            ApplyLength(localLength);
            transform.rotation = Quaternion.Euler(0f, 0f, Spawning.ZapperLayout.AngleDegrees(orientation));
        }

        public void OnSpawned()
        {
            if (spriteRenderer == null) Awake();
            ApplyLength(spriteRenderer.sprite != null ? spriteRenderer.sprite.bounds.size.y : spriteRenderer.size.y);
            transform.rotation = Quaternion.identity;
            frameClock = 0f;
        }

        public void OnDespawned() { }

        private void ApplyLength(float localLength)
        {
            float width = spriteRenderer.sprite != null ? spriteRenderer.sprite.bounds.size.x : spriteRenderer.size.x;
            spriteRenderer.size = new Vector2(width, localLength);
            box.size = new Vector2(width * beamWidthFactor, Mathf.Max(0.01f, localLength - 2f * endInset));
            box.offset = Vector2.zero;
        }

        private void Update()
        {
            if (frames == null || frames.Length < 2 || framesPerSecond <= 0f) return;
            frameClock += Time.deltaTime;
            int index = (int)(frameClock * framesPerSecond) % frames.Length;
            if (frames[index] != null) spriteRenderer.sprite = frames[index];
        }
    }
}
