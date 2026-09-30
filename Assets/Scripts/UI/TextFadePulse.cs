using UnityEngine;
using TMPro;

namespace JetpackRide.UI
{
    // Fades a text in and out on a loop (e.g. "press to start" prompts). Restarts fully
    // visible each time it's enabled, so a panel shown again never pops in mid-fade.
    [RequireComponent(typeof(TMP_Text))]
    public class TextFadePulse : MonoBehaviour
    {
        [SerializeField] private float period = 1.6f;
        [SerializeField, Range(0f, 1f)] private float minAlpha = 0.15f;
        [SerializeField, Range(0f, 1f)] private float maxAlpha = 1f;

        private TMP_Text text;
        private float elapsed;

        private void Awake() => text = GetComponent<TMP_Text>();

        private void OnEnable()
        {
            elapsed = 0f;
            Apply();
        }

        private void OnDisable()
        {
            if (text != null) text.alpha = maxAlpha;
        }

        // Unscaled so the prompt keeps pulsing even if time is slowed or paused.
        private void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            Apply();
        }

        private void Apply()
        {
            if (text != null) text.alpha = AlphaAt(elapsed, period, minAlpha, maxAlpha);
        }

        // Cosine ease: maxAlpha at t = 0, minAlpha at half a period, back to maxAlpha at a full period.
        public static float AlphaAt(float t, float period, float minAlpha, float maxAlpha)
        {
            if (period <= 0f) return maxAlpha;
            float wave = (Mathf.Cos(t / period * 2f * Mathf.PI) + 1f) * 0.5f;
            return Mathf.Lerp(minAlpha, maxAlpha, wave);
        }
    }
}
