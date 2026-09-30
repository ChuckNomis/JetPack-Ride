using UnityEngine;

namespace JetpackRide.Core
{
    [CreateAssetMenu(fileName = "GameConfig", menuName = "Jetpack Ride/Game Config")]
    public class GameConfig : ScriptableObject
    {
        [Header("Scroll Speed")]
        [SerializeField] private float baseScrollSpeed = 12f;
        [SerializeField] private float scrollSpeedPerMeter = 0.005f;
        [SerializeField] private float maxScrollSpeed = 26f;

        [Header("Jetpack Physics")]
        [SerializeField] private float jetpackThrust = 60f;
        [SerializeField] private float gravityScale = 3.8f;

        [Header("Rocket Speed")]
        [SerializeField] private float rocketSpeedMultiplier = 1.5f;

        [Header("Zapper Speed")]
        [Tooltip("1.0 = locked to the background scroll.")]
        [SerializeField] private float zapperSpeedMultiplier = 1f;

        [Header("Coin Speed")]
        [Tooltip("1.0 = locked to the background scroll. Match Zapper Speed Multiplier to keep coins and zappers together.")]
        [SerializeField] private float coinSpeedMultiplier = 1f;

        [Header("Obstacle Spawning")]
        [SerializeField] private float baseObstacleSpawnInterval = 1.8f;
        [SerializeField] private float minObstacleSpawnInterval = 0.6f;

        [Header("Rocket Spawning")]
        [SerializeField] private float baseRocketSpawnInterval = 4.0f;
        [SerializeField] private float minRocketSpawnInterval = 1.2f;

        [Header("Difficulty Ramp")]
        [SerializeField] private float difficultyRampDistance = 2500f;
        [SerializeField] private AnimationCurve difficultyCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Header("Rocket Lock-On")]
        [Tooltip("How long a tracking warning follows the player early in a run. Every volley tracks; this shrinking toward the late value is the rocket difficulty.")]
        [SerializeField] private float rocketTrackSecondsEarly = 2f;
        [Tooltip("How long it follows at full difficulty (shorter = faster pace).")]
        [SerializeField] private float rocketTrackSecondsLate = 1f;
        [SerializeField] private float rocketLockSeconds = 0.3f;
        [Tooltip("Max vertical speed (units/s) of a tracking warning.")]
        [SerializeField] private float rocketTrackSpeed = 4f;

        [Header("Scoring")]
        [SerializeField] private int coinValue = 5;

        [Header("Input")]
        [SerializeField] private float restartLockoutSeconds = 1.0f;

        public float BaseScrollSpeed => baseScrollSpeed;
        public float ScrollSpeedPerMeter => scrollSpeedPerMeter;
        public float MaxScrollSpeed => maxScrollSpeed;
        public float JetpackThrust => jetpackThrust;
        public float GravityScale => gravityScale;
        public float RocketSpeedMultiplier => rocketSpeedMultiplier;
        public float ZapperSpeedMultiplier => zapperSpeedMultiplier;
        public float CoinSpeedMultiplier => coinSpeedMultiplier;
        public float BaseObstacleSpawnInterval => baseObstacleSpawnInterval;
        public float MinObstacleSpawnInterval => minObstacleSpawnInterval;
        public float BaseRocketSpawnInterval => baseRocketSpawnInterval;
        public float MinRocketSpawnInterval => minRocketSpawnInterval;
        public float DifficultyRampDistance => difficultyRampDistance;
        public AnimationCurve DifficultyCurve => difficultyCurve;
        public float RocketLockSeconds => rocketLockSeconds;
        public float RocketTrackSpeed => rocketTrackSpeed;
        public float RocketTrackSeconds(float rampProgress01) => Mathf.Lerp(rocketTrackSecondsEarly, rocketTrackSecondsLate, Mathf.Clamp01(rampProgress01));
        public int CoinValue => coinValue;
        public float RestartLockoutSeconds => restartLockoutSeconds;
    }
}
