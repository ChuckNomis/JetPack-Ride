using UnityEngine;
using JetpackRide.Core;

namespace JetpackRide.Player
{
    // Oval on the floor under the player: full size while running, shrinking (and fading) as the
    // player climbs, down to a small dot at MaxY. The shadow is its own top-level object so the
    // player's root scale and the death tumble never distort it; it is shown whenever the player is.
    // Runs in edit mode too, so the Inspector values can be tuned without entering Play mode.
    [ExecuteAlways]
    public class PlayerShadow : MonoBehaviour
    {
        [SerializeField] private PlayerController controller;
        [SerializeField] private GameManager gameManager;
        [Tooltip("Player sprite; the shadow draws one sorting order behind it.")]
        [SerializeField] private SpriteRenderer target;
        [Tooltip("Shadow Y relative to PlayerController.MinY (the root's floor rest height). More negative = lower.")]
        [SerializeField] private float feetOffset = -0.45f;
        [Tooltip("World-unit width/height of the oval when the player is on the floor.")]
        [SerializeField] private Vector2 groundSize = new(0.9f, 0.22f);
        [Tooltip("World-unit width/height of the dot at max height.")]
        [SerializeField] private Vector2 dotSize = new(0.12f, 0.08f);
        [SerializeField, Range(0f, 1f)] private float groundAlpha = 0.7f;
        [SerializeField, Range(0f, 1f)] private float dotAlpha = 0.45f;
        [Tooltip("0 = hard edge, 1 = fully blurred from the centre.")]
        [SerializeField, Range(0f, 1f)] private float edgeSoftness = 0.2f;

        private const int TextureSize = 64;

        private SpriteRenderer shadow;
        private float builtSoftness = -1f;

        public Transform ShadowTransform => shadow != null ? shadow.transform : null;
        // 0 on the floor, 1 at max height.
        public float Height01 { get; private set; }

        private void OnDisable() => DestroyShadow();

        private void LateUpdate()
        {
            if (controller == null) return;
            EnsureShadow();

            // No player on the title screen, so no shadow either (mirrors PlayerVisuals). Always
            // shown in edit mode so it can be tuned.
            shadow.enabled = !Application.isPlaying || gameManager == null || gameManager.CurrentState != GameState.GetReady;
            if (target != null)
            {
                shadow.sortingLayerID = target.sortingLayerID;
                shadow.sortingOrder = target.sortingOrder - 1;
            }

            var playerPos = controller.transform.position;
            Height01 = Mathf.InverseLerp(controller.MinY, controller.MaxY, playerPos.y);
            shadow.transform.position = new Vector3(playerPos.x, controller.MinY + feetOffset, playerPos.z);
            Vector2 size = Vector2.Lerp(groundSize, dotSize, Height01);
            shadow.transform.localScale = new Vector3(size.x, size.y, 1f);
            shadow.color = new Color(0f, 0f, 0f, Mathf.Lerp(groundAlpha, dotAlpha, Height01));
        }

        private void EnsureShadow()
        {
            if (shadow == null)
            {
                var go = new GameObject("PlayerShadow") { hideFlags = HideFlags.HideAndDontSave };
                shadow = go.AddComponent<SpriteRenderer>();
                builtSoftness = -1f;
            }
            if (!Mathf.Approximately(builtSoftness, edgeSoftness))
            {
                DestroySprite();
                shadow.sprite = CreateSprite(edgeSoftness);
                builtSoftness = edgeSoftness;
            }
        }

        private void DestroyShadow()
        {
            if (shadow == null) return;
            DestroySprite();
            Release(shadow.gameObject);
            shadow = null;
        }

        private void DestroySprite()
        {
            if (shadow.sprite == null) return;
            Release(shadow.sprite.texture);
            Release(shadow.sprite);
            shadow.sprite = null;
        }

        private static void Release(Object obj)
        {
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }

        // 1x1 world-unit disc (pixelsPerUnit == size), solid in the middle and fading out over the
        // outer `softness` fraction of the radius; scaled into an oval per frame.
        private static Sprite CreateSprite(float softness)
        {
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var pixels = new Color32[TextureSize * TextureSize];
            float half = TextureSize * 0.5f;
            float inner = 1f - Mathf.Max(softness, 1f / half); // keep at least a pixel of anti-aliasing
            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    float r = new Vector2(x + 0.5f - half, y + 0.5f - half).magnitude / half;
                    // Hermite fade (GLSL smoothstep) from 1 at `inner` to 0 at the rim. Not
                    // Mathf.SmoothStep, which interpolates between its first two arguments.
                    float t = Mathf.InverseLerp(inner, 1f, r);
                    float alpha = 1f - t * t * (3f - 2f * t);
                    pixels[y * TextureSize + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, TextureSize, TextureSize), new Vector2(0.5f, 0.5f), TextureSize);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
