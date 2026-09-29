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

        [Header("Music")]
        [SerializeField] private AudioClip menuMusic;
        [SerializeField] private AudioClip gameplayMusic;
        [SerializeField, Range(0f, 1f)] private float musicVolume = 0.5f;

        [Header("SFX")]
        [SerializeField] private AudioClip coinSfx;
        [SerializeField] private AudioClip deathSfx;
        [SerializeField] private AudioClip rocketLaunchSfx;
        [SerializeField] private AudioClip jetpackLoop;
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 0.8f;

        public AudioSource MusicSource { get; private set; }
        public AudioClip LastSfx { get; private set; }
        public bool JetpackLoopActive => jetpackRequested;

        private AudioSource sfxSource;
        private AudioSource jetpackSource;
        private bool jetpackRequested;

        private void Awake()
        {
            MusicSource = CreateSource(loop: true);
            sfxSource = CreateSource(loop: false);
            jetpackSource = CreateSource(loop: true);
        }

        // Event wiring in Start/OnDestroy, not OnEnable, so serialized (or test-injected)
        // references are assigned before they're read — same pattern as UIManager/SpawnManager.
        private void Start()
        {
            MusicSource.volume = musicVolume;
            sfxSource.volume = sfxVolume;
            jetpackSource.volume = sfxVolume * 0.6f;
            jetpackSource.clip = jetpackLoop;

            gameManager.StateChanged += HandleStateChanged;
            gameManager.CoinsChanged += HandleCoinsChanged;
            if (player != null) player.ThrustingChanged += SetJetpackActive;
            if (spawner != null) spawner.RocketSpawned += HandleRocketSpawned;

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
            if (spawner != null) spawner.RocketSpawned -= HandleRocketSpawned;
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
                case GameState.Running:
                    PlayMusic(gameplayMusic);
                    break;
                case GameState.GameOver:
                    PlayMusic(null);
                    SetJetpackActive(false);
                    PlaySfx(deathSfx);
                    break;
            }
        }

        private void HandleCoinsChanged(int coins)
        {
            if (coins > 0) PlaySfx(coinSfx);
        }

        private void HandleRocketSpawned() => PlaySfx(rocketLaunchSfx);

        private void PlayMusic(AudioClip clip)
        {
            if (MusicSource.clip == clip && (clip == null || MusicSource.isPlaying)) return;
            MusicSource.Stop();
            MusicSource.clip = clip;
            if (clip != null) MusicSource.Play();
        }

        private void PlaySfx(AudioClip clip)
        {
            if (clip == null) return;
            LastSfx = clip;
            sfxSource.PlayOneShot(clip);
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
