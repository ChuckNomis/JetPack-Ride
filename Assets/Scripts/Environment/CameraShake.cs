using UnityEngine;

namespace JetpackRide.Environment
{
    // Jitters the camera around its rest position with a linearly fading random offset, then puts it
    // back exactly. A shake started while another runs keeps the original rest position; a newer
    // shake (or disabling the component) makes older loops stop touching the transform.
    public class CameraShake : MonoBehaviour
    {
        private Vector3 restPosition;
        private int generation;

        public bool IsShaking { get; private set; }

        public async Awaitable Shake(float seconds, float strength)
        {
            int gen = ++generation;
            if (!IsShaking) restPosition = transform.localPosition;
            IsShaking = true;

            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                if (this == null || !enabled || gen != generation) return;
                float falloff = 1f - t / seconds;
                transform.localPosition = restPosition + (Vector3)(Random.insideUnitCircle * strength * falloff);
                await Awaitable.NextFrameAsync();
            }

            if (this == null || !enabled || gen != generation) return;
            Restore();
        }

        private void OnDisable()
        {
            if (!IsShaking) return;
            generation++;
            Restore();
        }

        private void Restore()
        {
            transform.localPosition = restPosition;
            IsShaking = false;
        }
    }
}
