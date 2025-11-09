using System;
using UnityEngine;

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

public class PlayerStats : MonoBehaviour, IReadOnlyPlayerStats, IDamageable
{
    [Header("Movement")]
    [SerializeField, Min(0f)] private float baseMoveSpeed = 5f;
    [SerializeField, Min(0f)] private float moveSpeedMultiplier = 1f;

    [Header("Health")]
    [SerializeField, Min(1f)] private float baseMaxHealth = 100f;
    [SerializeField, Min(0f)] private float maxHealthOverflowClamp = 1000f;
    [SerializeField, Min(0f)] private float currentHealth = 0f;
    [SerializeField] private bool isDead = false;

    [Header("Ect.")]
    [SerializeField] private GameObject playerModel;


    // --- Events / snapshots ---
    public event Action<PlayerStatsSnapshot> StatsChanged;
    public static event Action OnDeath;

    private bool _deathSent;
    private Transform spawnPoint;

    private Rigidbody _rb;
    private Transform _root;


    private void Awake()
    {
        _root = transform;
        _rb = _root.GetComponent<Rigidbody>();

        currentHealth = Mathf.Clamp(baseMaxHealth, 0f, baseMaxHealth + maxHealthOverflowClamp);
        isDead = false;
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
        //playerModel = GetComponentInChildren<Animator>(true).gameObject;
        ResetPlayer();
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
            baseMaxHealth, maxHealthOverflowClamp, currentHealth, isDead
        );

    // --- Public READ-ONLY properties (external code cannot set directly) ---
    public float BaseMoveSpeed => baseMoveSpeed;
    public float MoveSpeedMultiplier => moveSpeedMultiplier;
    public float CurrentMoveSpeed => baseMoveSpeed * moveSpeedMultiplier;

    public float BaseMaxHealth => baseMaxHealth;
    public float CurrentHealth => currentHealth;
    public bool IsDead => isDead;

    // -------- MASTER SETTER (only way to mutate from outside) --------
    // (Gemini) Alkol ve Rotasyon kısımları çıkarıldı.
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
        // Movement
        if (u.BaseMoveSpeed.HasValue) baseMoveSpeed = Mathf.Max(0f, u.BaseMoveSpeed.Value);
        if (u.MoveSpeedMultiplier.HasValue) moveSpeedMultiplier = Mathf.Max(0f, u.MoveSpeedMultiplier.Value);

        // Health
        if (u.BaseMaxHealth.HasValue)
        {
            baseMaxHealth = Mathf.Max(1f, u.BaseMaxHealth.Value);
            currentHealth = Mathf.Clamp(currentHealth, 0f, baseMaxHealth + maxHealthOverflowClamp);
        }
        if (u.MaxHealthOverflowClamp.HasValue)
        {
            maxHealthOverflowClamp = Mathf.Max(0f, u.MaxHealthOverflowClamp.Value);
            currentHealth = Mathf.Clamp(currentHealth, 0f, baseMaxHealth + maxHealthOverflowClamp);
        }
        if (u.CurrentHealth.HasValue)
        {
            currentHealth = Mathf.Clamp(u.CurrentHealth.Value, 0f, baseMaxHealth + maxHealthOverflowClamp);
            if (currentHealth <= 0f) SetDead(true);
            else if (isDead && currentHealth > 0f) SetDead(false);
        }
        if (u.IsDead.HasValue) SetDead(u.IsDead.Value);


        if (notify) StatsChanged?.Invoke(GetSnapshot());
    }

    // -------- Specific helpers still allowed (gameplay actions) --------
    public void TakeDamage(float damageValue)
    {
        if (isDead) return;

        currentHealth = Mathf.Clamp(currentHealth - Mathf.Max(0f, damageValue), 0f, baseMaxHealth + maxHealthOverflowClamp);

        if (currentHealth <= 0f)
            SetDead(true);

        StatsChanged?.Invoke(GetSnapshot());
    }


    public void Heal(float amount)
    {
        if (amount <= 0f) return;
        currentHealth = Mathf.Clamp(currentHealth + amount, 0f, baseMaxHealth + maxHealthOverflowClamp);
        if (isDead && currentHealth > 0f) SetDead(false);
        StatsChanged?.Invoke(GetSnapshot());
    }
    
    // (Gemini) AddAlchol metodu çıkarıldı.

    private void SetDead(bool value)
    {
        if (isDead == value) return;
        isDead = value;

        if (isDead)
        {
            if (!_deathSent)
            {
                _deathSent = true;
                playerModel.SetActive(false);

                if (_rb)
                {
                    _rb.isKinematic = true;
                    _rb.linearVelocity = Vector3.zero;
                    _rb.angularVelocity = Vector3.zero;
                }

                OnDeath?.Invoke();
            }
        }
        else
        {
            _deathSent = false; // revived
            playerModel.SetActive(true);
        }
    }


    public void ResetPlayer()
    {
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
        currentHealth = baseMaxHealth;
        SetDead(false);
        StatsChanged?.Invoke(GetSnapshot());
    }
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetPlayer();
        }
    }
}