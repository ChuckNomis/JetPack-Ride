using UnityEngine;

namespace JetpackRide.Core
{
    public readonly struct DifficultySnapshot
    {
        public readonly float ScrollSpeed;
        public readonly float ObstacleSpawnInterval;
        public readonly float RocketSpawnInterval;
        public readonly float RampProgress01;

        public DifficultySnapshot(float scrollSpeed, float obstacleSpawnInterval, float rocketSpawnInterval, float rampProgress01)
        {
            ScrollSpeed = scrollSpeed;
            ObstacleSpawnInterval = obstacleSpawnInterval;
            RocketSpawnInterval = rocketSpawnInterval;
            RampProgress01 = rampProgress01;
        }
    }

    public static class DifficultyEvaluator
    {
        public static DifficultySnapshot Evaluate(float distanceMeters, GameConfig config)
        {
            float clampedDistance = Mathf.Max(0f, distanceMeters);

            float scrollSpeed = Mathf.Min(
                config.BaseScrollSpeed + config.ScrollSpeedPerMeter * clampedDistance,
                config.MaxScrollSpeed);

            float rampT = config.DifficultyRampDistance <= 0f
                ? 1f
                : Mathf.Clamp01(clampedDistance / config.DifficultyRampDistance);

            float curveT = Mathf.Clamp01(config.DifficultyCurve.Evaluate(rampT));

            float obstacleInterval = Mathf.Lerp(config.BaseObstacleSpawnInterval, config.MinObstacleSpawnInterval, curveT);
            float rocketInterval = Mathf.Lerp(config.BaseRocketSpawnInterval, config.MinRocketSpawnInterval, curveT);

            return new DifficultySnapshot(scrollSpeed, obstacleInterval, rocketInterval, rampT);
        }
    }
}
