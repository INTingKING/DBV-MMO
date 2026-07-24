using System;
using Unity.Netcode;
using UnityEngine;

public class NetworkHealth : NetworkBehaviour
{
    [SerializeField] private int startingMaxHealth = 50;
    [SerializeField] private SpriteRenderer tintRenderer;
    [SerializeField] private bool showHealthBar = true;
    [SerializeField] private Vector3 healthBarOffset = new Vector3(0f, 0.85f, 0f);

    private NetworkVariable<int> _maxHealth = new NetworkVariable<int>(
        50,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private NetworkVariable<int> _currentHealth = new NetworkVariable<int>(
        50,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private Color _baseColor = Color.white;
    private bool _baseColorCaptured;
    private WorldSpaceHealthBar _healthBar;
    private bool _spawnInitialized;

    public int MaxHealth => Mathf.Max(1, _maxHealth.Value);
    public int CurrentHealth => _currentHealth.Value;

    public bool IsDead => _currentHealth.Value <= 0;

    public event Action<NetworkHealth> Died;
    public event Action<NetworkHealth, int, int> HealthChanged;

    public override void OnNetworkSpawn()
    {
        if (tintRenderer == null)
            tintRenderer = GetComponentInChildren<SpriteRenderer>();

        CaptureBaseColor();
        _currentHealth.OnValueChanged += HandleCurrentChanged;
        _maxHealth.OnValueChanged += HandleMaxChanged;

        if (IsServer)
            InitializeServerHealth();

        _spawnInitialized = true;

        ApplyTint(CurrentHealth);
        EnsureHealthBar();
        NotifyHealthListeners();
    }

    public override void OnNetworkDespawn()
    {
        _spawnInitialized = false;
        _currentHealth.OnValueChanged -= HandleCurrentChanged;
        _maxHealth.OnValueChanged -= HandleMaxChanged;

        if (_healthBar != null)
        {
            Destroy(_healthBar.gameObject);
            _healthBar = null;
        }
    }

    private void InitializeServerHealth()
    {
        int max = Mathf.Max(1, startingMaxHealth);

        if (_maxHealth.Value != max)
            _maxHealth.Value = max;

        if (_currentHealth.Value <= 0 || _currentHealth.Value > max)
            _currentHealth.Value = max;
    }

    public int ApplyDamage(int amount, NetworkHealth source = null, bool isReflected = false)
    {
        if (!IsServer || !IsSpawned || IsDead || amount <= 0)
            return 0;

        int before = _currentHealth.Value;
        int next = Mathf.Max(0, before - amount);
        _currentHealth.Value = next;

        int dealt = before - next;

        if (!isReflected && dealt > 0 && source != null && !source.IsDead)
        {
            PlayerCombat combat = GetComponent<PlayerCombat>();
            if (combat != null)
                combat.ServerTryReflectDamage(dealt, source);
        }

        if (next <= 0)
            Died?.Invoke(this);

        return dealt;
    }

    public void ApplyHeal(int amount)
    {
        if (!IsServer || !IsSpawned || IsDead || amount <= 0)
            return;

        _currentHealth.Value = Mathf.Min(MaxHealth, _currentHealth.Value + amount);
    }

    public void FullHeal()
    {
        if (!IsServer || !IsSpawned)
            return;

        _currentHealth.Value = MaxHealth;
    }

    public void SetMaxHealth(int value, bool healToFull)
    {
        if (!IsServer || !IsSpawned)
            return;

        int max = Mathf.Max(1, value);
        _maxHealth.Value = max;

        if (healToFull || _currentHealth.Value <= 0)
            _currentHealth.Value = max;
        else if (_currentHealth.Value > max)
            _currentHealth.Value = max;

        NotifyHealthListeners();
    }

    public void SetBaseColor(Color color)
    {
        if (tintRenderer == null)
            tintRenderer = GetComponentInChildren<SpriteRenderer>();

        _baseColor = color;
        _baseColorCaptured = true;
        ApplyTint(CurrentHealth > 0 ? CurrentHealth : MaxHealth);
    }

    private void EnsureHealthBar()
    {
        if (!showHealthBar || _healthBar != null)
            return;

        GameObject barGo = new GameObject($"{name}_HealthBar");
        barGo.transform.position = transform.position + healthBarOffset;
        _healthBar = barGo.AddComponent<WorldSpaceHealthBar>();
        _healthBar.Initialize(this);
    }

    private void HandleCurrentChanged(int previous, int current)
    {
        ApplyTint(current);
        NotifyHealthListeners();
    }

    private void HandleMaxChanged(int previous, int current)
    {
        ApplyTint(CurrentHealth);
        NotifyHealthListeners();
    }

    private void NotifyHealthListeners()
    {
        HealthChanged?.Invoke(this, CurrentHealth, CurrentHealth);
    }

    private void CaptureBaseColor()
    {
        if (_baseColorCaptured || tintRenderer == null)
            return;

        _baseColor = tintRenderer.color;
        _baseColorCaptured = true;
    }

    private void ApplyTint(int current)
    {
        if (tintRenderer == null)
            return;

        CaptureBaseColor();

        float t = MaxHealth <= 0 ? 0f : Mathf.Clamp01(current / (float)MaxHealth);
        float brightness = Mathf.Lerp(0.35f, 1f, t);
        tintRenderer.color = new Color(
            _baseColor.r * brightness,
            _baseColor.g * brightness,
            _baseColor.b * brightness,
            _baseColor.a);
    }
}
