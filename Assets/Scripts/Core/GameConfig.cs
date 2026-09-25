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
        [SerializeField] private float jetpackThrust = 28f;
        [SerializeField] private float gravityScale = 3.8f;

        [Header("Obstacle Spawning")]
        [SerializeField] private float baseObstacleSpawnInterval = 1.8f;
        [SerializeField] private float minObstacleSpawnInterval = 0.6f;

        [Header("Rocket Spawning")]
        [SerializeField] private float baseRocketSpawnInterval = 4.0f;
        [SerializeField] private float minRocketSpawnInterval = 1.2f;

        [Header("Difficulty Ramp")]
        [SerializeField] private float difficultyRampDistance = 2500f;
        [SerializeField] private AnimationCurve difficultyCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        [SerializeField] private AnimationCurve rocketAggressionCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Header("Scoring")]
        [SerializeField] private int coinValue = 5;

        [Header("Input")]
        [SerializeField] private float restartLockoutSeconds = 1.0f;

        public float BaseScrollSpeed => baseScrollSpeed;
        public float ScrollSpeedPerMeter => scrollSpeedPerMeter;
        public float MaxScrollSpeed => maxScrollSpeed;
        public float JetpackThrust => jetpackThrust;
        public float GravityScale => gravityScale;
        public float BaseObstacleSpawnInterval => baseObstacleSpawnInterval;
        public float MinObstacleSpawnInterval => minObstacleSpawnInterval;
        public float BaseRocketSpawnInterval => baseRocketSpawnInterval;
        public float MinRocketSpawnInterval => minRocketSpawnInterval;
        public float DifficultyRampDistance => difficultyRampDistance;
        public AnimationCurve DifficultyCurve => difficultyCurve;
        public AnimationCurve RocketAggressionCurve => rocketAggressionCurve;
        public int CoinValue => coinValue;
        public float RestartLockoutSeconds => restartLockoutSeconds;
    }
}
