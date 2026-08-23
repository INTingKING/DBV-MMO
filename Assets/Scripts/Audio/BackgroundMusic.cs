using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BackgroundMusic : MonoBehaviour
{
    public const float SourceVolume = 0.16f;
    private const float IntroFadeSeconds = 2.2f;
    private const float CombatFadeOutSeconds = 1.1f;
    private const float CombatFadeInSeconds = 2.8f;
    private const float CombatHoldSeconds = 3.5f;
    private const float MeleeThreatRange = 3.25f;
    private const float CrossfadeSeconds = 2.0f;

    private static BackgroundMusic _instance;
    private static bool _subscribed;

    private AudioSource _titleSource;
    private AudioSource _overworldSource;
    private AudioSource _combatSource;
    private float _introFade;
    private float _combatMix;
    private float _titleMix = 1f;
    private float _titleMixTarget = 1f;
    private float _combatHoldUntil;
    private int _lastLocalHp;
    private bool _hpInitialized;
    private float _hurtUntil;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _instance = null;
        _subscribed = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Application.isBatchMode)
            return;

        if (!_subscribed)
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            _subscribed = true;
        }

        EnsureExists();
        EnsureSingleListener();
        if (_instance != null)
            _instance.SetSceneMix(IsTitleScene(SceneManager.GetActiveScene()));
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureExists();
        EnsureSingleListener();
        if (_instance != null)
            _instance.SetSceneMix(IsTitleScene(scene));
    }

    public static void EnsureExists()
    {
        if (Application.isBatchMode)
            return;

        EnsureSingleListener();

        if (_instance != null)
            return;

        BackgroundMusic existing = FindFirstObjectByType<BackgroundMusic>();
        if (existing != null)
        {
            _instance = existing;
            return;
        }

        GameObject go = new GameObject("BackgroundMusic");
        DontDestroyOnLoad(go);
        go.AddComponent<BackgroundMusic>();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        bool title = IsTitleScene(SceneManager.GetActiveScene());
        _titleMix = title ? 1f : 0f;
        _titleMixTarget = _titleMix;

        _titleSource = CreateStem(ChiptuneLoop.BuildTitle());
        _overworldSource = CreateStem(ChiptuneLoop.BuildOverworld());
        _combatSource = CreateStem(ChiptuneLoop.BuildCombat());
        _titleSource.Play();
        _overworldSource.Play();
        _combatSource.Play();

        _introFade = 0f;
        _combatMix = 0f;
        _hpInitialized = false;
        ApplyStemVolumes();
        EnsureSingleListener();
    }

    private AudioSource CreateStem(AudioClip clip)
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.priority = 0;
        source.volume = 0f;
        source.clip = clip;
        return source;
    }

    private void SetSceneMix(bool titleScene)
    {
        _titleMixTarget = titleScene ? 1f : 0f;
    }

    private static bool IsTitleScene(Scene scene)
    {
        return scene.name == NetworkBootstrap.MainMenuSceneName || scene.name == "MainMenu";
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    private static void EnsureSingleListener()
    {
        if (Application.isBatchMode)
            return;

        AudioListener keep = null;
        if (Camera.main != null)
            keep = Camera.main.GetComponent<AudioListener>();

        AudioListener[] all = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (keep == null)
        {
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].isActiveAndEnabled)
                {
                    keep = all[i];
                    break;
                }
            }
        }

        if (keep == null)
        {
            GameObject host = Camera.main != null ? Camera.main.gameObject : null;
            if (host == null && _instance != null)
                host = _instance.gameObject;
            if (host == null)
            {
                Camera anyCamera = Object.FindFirstObjectByType<Camera>();
                if (anyCamera != null)
                    host = anyCamera.gameObject;
            }

            if (host == null)
                return;

            keep = host.GetComponent<AudioListener>();
            if (keep == null)
                keep = host.AddComponent<AudioListener>();
        }

        keep.enabled = true;

        all = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i] != keep)
                all[i].enabled = false;
        }
    }

    private void Update()
    {
        if (_titleSource == null || _overworldSource == null || _combatSource == null)
            return;

        _introFade = Mathf.MoveTowards(_introFade, 1f, Time.unscaledDeltaTime / IntroFadeSeconds);
        _titleMix = Mathf.MoveTowards(_titleMix, _titleMixTarget, Time.unscaledDeltaTime / CrossfadeSeconds);

        if (_titleMixTarget < 0.5f && IsLocalPlayerInCombat())
            _combatHoldUntil = Time.unscaledTime + CombatHoldSeconds;

        float combatTarget = Time.unscaledTime < _combatHoldUntil ? 1f : 0f;
        float combatSeconds = combatTarget > _combatMix ? CombatFadeOutSeconds : CombatFadeInSeconds;
        _combatMix = Mathf.MoveTowards(_combatMix, combatTarget, Time.unscaledDeltaTime / combatSeconds);

        ApplyStemVolumes();
    }

    private void ApplyStemVolumes()
    {
        float master = SourceVolume * GameSettings.MusicVolume * _introFade;
        float gameMix = 1f - _titleMix;
        if (_titleSource != null)
            _titleSource.volume = master * _titleMix;
        if (_overworldSource != null)
            _overworldSource.volume = master * gameMix * (1f - _combatMix);
        if (_combatSource != null)
            _combatSource.volume = master * gameMix * _combatMix;
    }

    private bool IsLocalPlayerInCombat()
    {
        NetworkManager nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsListening)
        {
            _hpInitialized = false;
            return false;
        }

        NetworkObject playerObj = null;
        if (nm.SpawnManager != null)
            playerObj = nm.SpawnManager.GetLocalPlayerObject();
        if (playerObj == null && nm.LocalClient != null)
            playerObj = nm.LocalClient.PlayerObject;
        if (playerObj == null)
        {
            _hpInitialized = false;
            return false;
        }

        PlayerCombat combat = playerObj.GetComponent<PlayerCombat>();
        NetworkHealth health = playerObj.GetComponent<NetworkHealth>();

        if (combat != null && (combat.HasTarget || combat.IsRespawning || combat.IsCasting))
            return true;

        if (health != null)
        {
            if (!_hpInitialized)
            {
                _lastLocalHp = health.CurrentHealth;
                _hpInitialized = true;
            }
            else if (health.CurrentHealth < _lastLocalHp)
            {
                _hurtUntil = Time.unscaledTime + 2f;
            }

            _lastLocalHp = health.CurrentHealth;

            if (health.IsDead || Time.unscaledTime < _hurtUntil)
                return true;
        }

        Vector2 pos = playerObj.transform.position;
        IReadOnlyList<EnemyAI> enemies = EnemyRegistry.Alive;
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyAI enemy = enemies[i];
            if (enemy == null || !enemy.IsSpawned)
                continue;
            if (enemy.Health != null && enemy.Health.IsDead)
                continue;

            float dist = Vector2.Distance(pos, enemy.transform.position);
            if (dist <= MeleeThreatRange)
                return true;

            if (enemy.CurrentTarget == playerObj.transform && dist <= enemy.AggroRange)
                return true;
        }

        return false;
    }
}
