using System;
using UnityEngine;
using Unity.Netcode;

/// Read-only view other scripts can depend on
public interface IReadOnlyPlayerStats
{
    // Movement
    float BaseMoveSpeed { get; }
    float MoveSpeedMultiplier { get; }

    // Health
    float BaseMaxHealth { get; }
    float CurrentHealth { get; }
    bool IsDead { get; }

    // Derived
    float CurrentMoveSpeed { get; }
}

[RequireComponent(typeof(NetworkObject))]
public class PlayerStats : NetworkBehaviour, IReadOnlyPlayerStats, IDamageable
{
    [Header("Movement")]
    [SerializeField, Min(0f)] private float baseMoveSpeed = 5f;
    [SerializeField, Min(0f)] private float moveSpeedMultiplier = 1f;

    [Header("Health")]
    [SerializeField, Min(1f)] private float baseMaxHealth = 100f;
    [SerializeField, Min(0f)] private float maxHealthOverflowClamp = 1000f;
    
    // Network synchronized variables
    private NetworkVariable<float> currentHealth = new NetworkVariable<float>(
        100f, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server
    );
    private NetworkVariable<bool> isDead = new NetworkVariable<bool>(
        false, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server
    );

    [Header("Ect.")]
    [SerializeField] private GameObject playerModel;


    // --- Events / snapshots ---
    public event Action<PlayerStatsSnapshot> StatsChanged;
    public static event Action OnDeath;

    private bool _deathSent;
    private Transform spawnPoint;

    private Rigidbody _rb;
    private Transform _root;


    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        _root = transform;
        _rb = _root.GetComponent<Rigidbody>();
        
        // Subscribe to network variable changes
        currentHealth.OnValueChanged += OnHealthChanged;
        isDead.OnValueChanged += OnDeathStateChanged;
        
        // Initialize on server
        if (IsServer)
        {
            currentHealth.Value = Mathf.Clamp(baseMaxHealth, 0f, baseMaxHealth + maxHealthOverflowClamp);
            isDead.Value = false;
        }
        
        _deathSent = false;
        
        // Fallback to tag only if not wired in the Inspector
        if (!spawnPoint)
        {
            var go = GameObject.FindGameObjectWithTag("PlayerSpawnPosition");
            if (go) spawnPoint = go.transform;
        }
        
        // Reset player on spawn
        if (IsServer)
        {
            ResetPlayer();
        }
    }

    public override void OnNetworkDespawn()
    {
        // Unsubscribe from network variable changes
        currentHealth.OnValueChanged -= OnHealthChanged;
        isDead.OnValueChanged -= OnDeathStateChanged;
        base.OnNetworkDespawn();
    }

    private void Awake()
    {
        _root = transform;
        _rb = _root.GetComponent<Rigidbody>();
        _deathSent = false;
    }

    private void Start()
    {
        // Fallback to tag only if not wired in the Inspector
        if (!spawnPoint)
        {
            var go = GameObject.FindGameObjectWithTag("PlayerSpawnPosition");
            if (go) spawnPoint = go.transform;
        }
    }
    
    private void OnHealthChanged(float oldValue, float newValue)
    {
        StatsChanged?.Invoke(GetSnapshot());
    }
    
    private void OnDeathStateChanged(bool oldValue, bool newValue)
    {
        if (newValue && !_deathSent)
        {
            _deathSent = true;
            if (playerModel != null)
                playerModel.SetActive(false);

            if (_rb != null)
            {
                _rb.isKinematic = true;
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }

            OnDeath?.Invoke();
        }
        else if (!newValue && _deathSent)
        {
            _deathSent = false;
            if (playerModel != null)
                playerModel.SetActive(true);
        }
        
        StatsChanged?.Invoke(GetSnapshot());
    }

    // === SNAPSHOT ===
    public readonly struct PlayerStatsSnapshot
    {
        // Movement
        public readonly float BaseMoveSpeed;
        public readonly float MoveSpeedMultiplier;

        // Health
        public readonly float BaseMaxHealth;
        public readonly float MaxHealthOverflowClamp;
        public readonly float CurrentHealth;
        public readonly bool IsDead;

        // Derived
        public float CurrentMoveSpeed => BaseMoveSpeed * MoveSpeedMultiplier;

        public PlayerStatsSnapshot(
            // movement
            float baseMoveSpeed, float moveSpeedMultiplier,
            // health
            float baseMaxHealth, float maxHealthOverflowClamp, float currentHealth, bool isDead)
        {
            BaseMoveSpeed = baseMoveSpeed;
            MoveSpeedMultiplier = moveSpeedMultiplier;

            BaseMaxHealth = baseMaxHealth;
            MaxHealthOverflowClamp = maxHealthOverflowClamp;
            CurrentHealth = currentHealth;
            IsDead = isDead;
        }
    }

    public PlayerStatsSnapshot GetSnapshot() =>
        new PlayerStatsSnapshot(
            baseMoveSpeed, moveSpeedMultiplier,
            baseMaxHealth, maxHealthOverflowClamp, currentHealth.Value, isDead.Value
        );

    // --- Public READ-ONLY properties (external code cannot set directly) ---
    public float BaseMoveSpeed => baseMoveSpeed;
    public float MoveSpeedMultiplier => moveSpeedMultiplier;
    public float CurrentMoveSpeed => baseMoveSpeed * moveSpeedMultiplier;

    public float BaseMaxHealth => baseMaxHealth;
    public float CurrentHealth => currentHealth.Value;
    public bool IsDead => isDead.Value;

    // -------- MASTER SETTER (only way to mutate from outside) --------
    // (Gemini) Alkol ve Rotasyon kısımları çıkarıldı.
    // Only server can modify stats
    public void MasterSet(
        // movement
        float? baseMoveSpeed = null,
        float? moveSpeedMultiplier = null,
        // health
        float? baseMaxHealth = null,
        float? maxHealthOverflowClamp = null,
        float? currentHealth = null,
        bool? isDead = null,
        // notify
        bool notify = true)
    {
        // Only server can modify stats
        if (!IsServer) return;
        
        Apply(new PlayerStatsUpdate
        {
            // movement
            BaseMoveSpeed = baseMoveSpeed,
            MoveSpeedMultiplier = moveSpeedMultiplier,
            // health
            BaseMaxHealth = baseMaxHealth,
            MaxHealthOverflowClamp = maxHealthOverflowClamp,
            CurrentHealth = currentHealth,
            IsDead = isDead,
        }, notify);
    }

    public struct PlayerStatsUpdate
    {
        // movement
        public float? BaseMoveSpeed;
        public float? MoveSpeedMultiplier;

        // health
        public float? BaseMaxHealth;
        public float? MaxHealthOverflowClamp;
        public float? CurrentHealth;
        public bool? IsDead;
    }

    // (Gemini) Alkol ve Rotasyon kısımları çıkarıldı.
    private void Apply(PlayerStatsUpdate u, bool notify)
    {
        // Only server can apply changes
        if (!IsServer) return;
        
        // Movement (local variables, not synchronized)
        if (u.BaseMoveSpeed.HasValue) baseMoveSpeed = Mathf.Max(0f, u.BaseMoveSpeed.Value);
        if (u.MoveSpeedMultiplier.HasValue) moveSpeedMultiplier = Mathf.Max(0f, u.MoveSpeedMultiplier.Value);

        // Health (network synchronized)
        if (u.BaseMaxHealth.HasValue)
        {
            baseMaxHealth = Mathf.Max(1f, u.BaseMaxHealth.Value);
            currentHealth.Value = Mathf.Clamp(currentHealth.Value, 0f, baseMaxHealth + maxHealthOverflowClamp);
        }
        if (u.MaxHealthOverflowClamp.HasValue)
        {
            maxHealthOverflowClamp = Mathf.Max(0f, u.MaxHealthOverflowClamp.Value);
            currentHealth.Value = Mathf.Clamp(currentHealth.Value, 0f, baseMaxHealth + maxHealthOverflowClamp);
        }
        if (u.CurrentHealth.HasValue)
        {
            currentHealth.Value = Mathf.Clamp(u.CurrentHealth.Value, 0f, baseMaxHealth + maxHealthOverflowClamp);
            if (currentHealth.Value <= 0f) SetDead(true);
            else if (isDead.Value && currentHealth.Value > 0f) SetDead(false);
        }
        if (u.IsDead.HasValue) SetDead(u.IsDead.Value);

        if (notify) StatsChanged?.Invoke(GetSnapshot());
    }

    // -------- Specific helpers still allowed (gameplay actions) --------
    // Only server can apply damage
    public void TakeDamage(float damageValue)
    {
        if (!IsServer) return;
        if (isDead.Value) return;

        currentHealth.Value = Mathf.Clamp(currentHealth.Value - Mathf.Max(0f, damageValue), 0f, baseMaxHealth + maxHealthOverflowClamp);

        if (currentHealth.Value <= 0f)
            SetDead(true);

        StatsChanged?.Invoke(GetSnapshot());
    }


    public void Heal(float amount)
    {
        if (!IsServer) return;
        if (amount <= 0f) return;
        currentHealth.Value = Mathf.Clamp(currentHealth.Value + amount, 0f, baseMaxHealth + maxHealthOverflowClamp);
        if (isDead.Value && currentHealth.Value > 0f) SetDead(false);
        StatsChanged?.Invoke(GetSnapshot());
    }
    
    // (Gemini) AddAlchol metodu çıkarıldı.

    private void SetDead(bool value)
    {
        if (!IsServer) return;
        if (isDead.Value == value) return;
        isDead.Value = value;
        
        // Death state changes are handled in OnDeathStateChanged callback
    }


    public void ResetPlayer()
    {
        // Only server can reset player
        if (!IsServer) return;
        
        // Choose a valid target position
        Vector3 target = _root.position;
        if (spawnPoint) target = spawnPoint.position;

        // Teleport via Rigidbody when available
        if (_rb)
        {
            _rb.isKinematic = false;

            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;

            // Two-step to be extra safe with interpolation:
            _rb.position = target;          // set immediately
            _rb.MovePosition(target);       // ensure physics sees it
        }
        else
        {
            _root.position = target;        // non-physics fallback
        }

        // Restore health / state
        currentHealth.Value = baseMaxHealth;
        SetDead(false);
        StatsChanged?.Invoke(GetSnapshot());
    }
    
    private void Update()
    {
        // Only owner can reset themselves
        if (!IsOwner) return;
        
        if (Input.GetKeyDown(KeyCode.R))
        {
            // Request reset from server
            RequestResetServerRpc();
        }
    }
    
    [ServerRpc]
    private void RequestResetServerRpc()
    {
        ResetPlayer();
    }
}
