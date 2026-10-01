using UnityEngine;

namespace JetpackRide.Environment
{
    // Keeps the camera at a fixed aspect ratio (16:9 by default) whatever the window/screen shape:
    // a wider screen gets black bars left and right (pillarbox), a taller one gets bars top and bottom
    // (letterbox). Gameplay positions (spawn/despawn X, MinY/MaxY) are authored for the 16:9 view, so
    // this keeps the visible world identical on every display. The Screen Space - Camera canvas follows
    // the camera viewport, so the UI stays inside the 16:9 area too.
    [RequireComponent(typeof(Camera))]
    public class CameraLetterbox : MonoBehaviour
    {
        [SerializeField] private float targetWidth = 1920f;
        [SerializeField] private float targetHeight = 1080f;

        private Camera cam;
        private Camera barsCamera;
        private int lastScreenWidth = -1;
        private int lastScreenHeight = -1;

        public float TargetAspect => targetWidth / targetHeight;

        // Normalized viewport rect that fits targetAspect centered inside a screen of screenAspect.
        public static Rect ComputeViewport(float screenAspect, float targetAspect)
        {
            if (screenAspect <= 0f || targetAspect <= 0f) return new Rect(0f, 0f, 1f, 1f);

            float scaleHeight = screenAspect / targetAspect;
            if (scaleHeight < 1f)
                return new Rect(0f, (1f - scaleHeight) * 0.5f, 1f, scaleHeight); // too tall: bars top/bottom

            float scaleWidth = 1f / scaleHeight;
            return new Rect((1f - scaleWidth) * 0.5f, 0f, scaleWidth, 1f); // too wide: bars left/right
        }

        private void Awake()
        {
            cam = GetComponent<Camera>();
            CreateBarsCamera();
        }

        private void OnEnable() => lastScreenWidth = lastScreenHeight = -1;

        private void LateUpdate()
        {
            if (Screen.width == lastScreenWidth && Screen.height == lastScreenHeight) return;
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            Apply();
        }

        public void Apply()
        {
            if (cam == null) cam = GetComponent<Camera>();
            float screenAspect = Screen.height > 0 ? (float)Screen.width / Screen.height : TargetAspect;
            cam.rect = ComputeViewport(screenAspect, TargetAspect);
        }

        private void OnDisable()
        {
            if (cam != null) cam.rect = new Rect(0f, 0f, 1f, 1f);
        }

        private void OnDestroy()
        {
            if (barsCamera != null) Destroy(barsCamera.gameObject);
        }

        // A full-screen camera that renders nothing and clears to black before the main camera draws,
        // so the area outside the main camera's viewport is solid black instead of stale pixels.
        private void CreateBarsCamera()
        {
            var go = new GameObject("LetterboxBars") { hideFlags = HideFlags.DontSave };
            barsCamera = go.AddComponent<Camera>();
            barsCamera.clearFlags = CameraClearFlags.SolidColor;
            barsCamera.backgroundColor = Color.black;
            barsCamera.cullingMask = 0;
            barsCamera.orthographic = true;
            barsCamera.depth = cam.depth - 1f;
            barsCamera.rect = new Rect(0f, 0f, 1f, 1f);
        }
    }
}
