using UnityEngine;
using JetpackRide.Core;
using JetpackRide.Player;
using JetpackRide.Spawning;

namespace JetpackRide.Audio
{
    // Observer consumer (GDD §6 audio): reacts to GameManager/PlayerController/SpawnManager events,
    // so no gameplay class needs to know audio exists.
    public class AudioManager : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private PlayerController player;
        [SerializeField] private SpawnManager spawner;
        [SerializeField] private PlayerVisuals visuals;

        [Header("Music")]
        [SerializeField] private AudioClip menuMusic;
        [SerializeField] private AudioClip gameplayMusic;
        [SerializeField, Range(0f, 1f)] private float musicVolume = 0.5f;

        [Header("SFX")]
        [Tooltip("Master SFX volume; each sound's own volume below is multiplied by it.")]
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 0.8f;
        [SerializeField] private AudioClip coinSfx;
        [SerializeField, Range(0f, 1f)] private float coinVolume = 1f;
        [SerializeField] private AudioClip deathSfx;
        [SerializeField, Range(0f, 1f)] private float deathVolume = 1f;
        [SerializeField] private AudioClip rocketLaunchSfx;
        [SerializeField, Range(0f, 1f)] private float rocketLaunchVolume = 1f;
        [SerializeField] private AudioClip startExplosionSfx;
        [SerializeField, Range(0f, 1f)] private float startExplosionVolume = 1f;
        [SerializeField] private AudioClip warningBlinkSfx;
        [SerializeField, Range(0f, 1f)] private float warningBlinkVolume = 1f;
        [SerializeField] private AudioClip footstepSfx;
        [SerializeField, Range(0f, 1f)] private float footstepVolume = 1f;
        [Tooltip("Random pitch range per footstep so one clip doesn't sound repetitive.")]
        [SerializeField] private Vector2 footstepPitchRange = new(0.9f, 1.1f);
        [SerializeField] private AudioClip jetpackLoop;
        [SerializeField, Range(0f, 1f)] private float jetpackVolume = 0.6f;

        public AudioSource MusicSource { get; private set; }
        public AudioClip LastSfx { get; private set; }
        public float LastSfxVolume { get; private set; }
        public bool JetpackLoopActive => jetpackRequested;

        private AudioSource sfxSource;
        private AudioSource jetpackSource;
        private AudioSource footstepSource;
        private bool jetpackRequested;

        private void Awake()
        {
            MusicSource = CreateSource(loop: true);
            sfxSource = CreateSource(loop: false);
            jetpackSource = CreateSource(loop: true);
            footstepSource = CreateSource(loop: false); // own source: per-step pitch must not bend other SFX
        }

        // Event wiring in Start/OnDestroy, not OnEnable, so serialized (or test-injected)
        // references are assigned before they're read — same pattern as UIManager/SpawnManager.
        private void Start()
        {
            MusicSource.volume = musicVolume;
            sfxSource.volume = sfxVolume;
            jetpackSource.volume = sfxVolume * jetpackVolume;
            jetpackSource.clip = jetpackLoop;
            footstepSource.volume = sfxVolume;

            gameManager.StateChanged += HandleStateChanged;
            gameManager.CoinsChanged += HandleCoinsChanged;
            if (player != null) player.ThrustingChanged += SetJetpackActive;
            if (spawner != null)
            {
                spawner.RocketSpawned += HandleRocketSpawned;
                spawner.RocketWarningBlinked += HandleWarningBlinked;
            }
            if (visuals != null) visuals.Footstep += HandleFootstep;

            HandleStateChanged(gameManager.CurrentState);
        }

        private void OnDestroy()
        {
            if (gameManager != null)
            {
                gameManager.StateChanged -= HandleStateChanged;
                gameManager.CoinsChanged -= HandleCoinsChanged;
            }
            if (player != null) player.ThrustingChanged -= SetJetpackActive;
            if (spawner != null)
            {
                spawner.RocketSpawned -= HandleRocketSpawned;
                spawner.RocketWarningBlinked -= HandleWarningBlinked;
            }
            if (visuals != null) visuals.Footstep -= HandleFootstep;
        }

        public void SetJetpackActive(bool active)
        {
            jetpackRequested = active;
            if (active && jetpackSource.clip != null && !jetpackSource.isPlaying) jetpackSource.Play();
            else if (!active) jetpackSource.Stop();
        }

        private void HandleStateChanged(GameState state)
        {
            switch (state)
            {
                case GameState.GetReady:
                    PlayMusic(menuMusic);
                    break;
                case GameState.Intro:
                    PlayMusic(null);
                    PlaySfx(startExplosionSfx, startExplosionVolume);
                    break;
                case GameState.Running:
                    PlayMusic(gameplayMusic);
                    break;
                case GameState.GameOver:
                    PlayMusic(null);
                    SetJetpackActive(false);
                    PlaySfx(deathSfx, deathVolume);
                    break;
            }
        }

        private void HandleCoinsChanged(int coins)
        {
            if (coins > 0) PlaySfx(coinSfx, coinVolume);
        }

        private void HandleRocketSpawned() => PlaySfx(rocketLaunchSfx, rocketLaunchVolume);

        private void HandleWarningBlinked() => PlaySfx(warningBlinkSfx, warningBlinkVolume);

        private void HandleFootstep()
        {
            if (footstepSfx == null) return;
            LastSfx = footstepSfx;
            LastSfxVolume = footstepVolume;
            footstepSource.pitch = Random.Range(footstepPitchRange.x, footstepPitchRange.y);
            footstepSource.PlayOneShot(footstepSfx, footstepVolume);
        }

        private void PlayMusic(AudioClip clip)
        {
            if (MusicSource.clip == clip && (clip == null || MusicSource.isPlaying)) return;
            MusicSource.Stop();
            MusicSource.clip = clip;
            if (clip != null) MusicSource.Play();
        }

        // volume scales on top of the source's master sfxVolume.
        private void PlaySfx(AudioClip clip, float volume)
        {
            if (clip == null) return;
            LastSfx = clip;
            LastSfxVolume = volume;
            sfxSource.PlayOneShot(clip, volume);
        }

        private AudioSource CreateSource(bool loop)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            return source;
        }
    }
}
