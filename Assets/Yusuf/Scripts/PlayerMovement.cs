using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Player movement controller with network synchronization.
/// NetworkTransform component is required and should be configured to sync Rigidbody position/rotation.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PlayerStats))]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Unity.Netcode.Components.NetworkTransform))]
public class PlayerMovement : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerStats stats;

    [Header("Plane/Physics")]
    [Tooltip("Karakterin Z eksenini (derinlik) kilitler. 2D oyun için true olmalı.")]
    [SerializeField] private bool lockZToInitial = true; // (Gemini) Y'den Z'ye geri değiştirildi

    [Tooltip("Rigidbody'e yerçekimi uygulansın mı?")]
    [SerializeField] private bool useGravity = false; // (Gemini) GDD  için 'false' kalması önerilir

    private Rigidbody rb;
    private Vector2 input;
    private float zLock;     // (Gemini) yLock'tan zLock'a geri değiştirildi
    
    // Flag to disable movement when controlling turrets or other equipment
    private bool isMovementDisabled = false;
    
    public void SetMovementDisabled(bool disabled)
    {
        isMovementDisabled = disabled;
        if (disabled)
        {
            // Stop any current movement
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
            }
        }
    }
    
    public bool IsLockZToInitial => lockZToInitial;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        // Configure rigidbody on spawn
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (stats == null) stats = GetComponent<PlayerStats>();
        
        zLock = transform.position.z;
        ConfigureRigidbody();
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (stats == null) stats = GetComponent<PlayerStats>();

        zLock = transform.position.z; // (Gemini) Y'den Z'ye geri değiştirildi
        ConfigureRigidbody();
    }

    private void ConfigureRigidbody()
    {
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.useGravity = useGravity;

        // Rotasyonları dondur
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        // (Gemini) Top-Down (X-Y) için Y yerine Z pozisyonunu kilitle
        if (lockZToInitial)
            rb.constraints |= RigidbodyConstraints.FreezePositionZ; // (Gemini) Y'den Z'ye geri değiştirildi
    }

    private void Update()
    {
        // Don't process input if movement is disabled (e.g., controlling turret)
        if (isMovementDisabled) return;
        
        // Check if we can control movement
        // Allow movement if:
        // 1. Not networked (no NetworkManager or not connected)
        // 2. Networked but not spawned as NetworkObject (testing/editor)
        // 3. Networked and spawned, but we're the owner
        bool canControl = true;
        
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
        {
            // We're in networked mode
            if (IsSpawned)
            {
                // Object is spawned as NetworkObject, only owner can control
                canControl = IsOwner;
            }
            // If not spawned, allow control (for testing)
        }
        // If not networked, allow control
        
        if (!canControl) return;

        // Read WASD
        Vector2 dir = Vector2.zero;
        if (Input.GetKey(KeyCode.A)) dir.x -= 1f;
        if (Input.GetKey(KeyCode.D)) dir.x += 1f;
        if (Input.GetKey(KeyCode.S)) dir.y -= 1f;
        if (Input.GetKey(KeyCode.W)) dir.y += 1f;

        // Clamp to unit circle
        input = (dir.sqrMagnitude > 1f) ? dir.normalized : dir;
    }

    private void FixedUpdate()
    {
        // Don't process movement if movement is disabled (e.g., controlling turret)
        if (isMovementDisabled) return;
        
        // Check if we can control movement
        // Allow movement if:
        // 1. Not networked (no NetworkManager or not connected)
        // 2. Networked but not spawned as NetworkObject (testing/editor)
        // 3. Networked and spawned, but we're the owner
        bool canControl = true;
        
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
        {
            // We're in networked mode
            if (IsSpawned)
            {
                // Object is spawned as NetworkObject, only owner can control
                canControl = IsOwner;
            }
            // If not spawned, allow control (for testing)
        }
        // If not networked, allow control
        
        if (!canControl) return;

        // Hedef hızı PlayerStats'tan al
        float targetSpeed = (stats != null) ? stats.CurrentMoveSpeed : 5f;

        // Transform input direction by player's rotation so movement is relative to player's facing direction
        // This ensures WASD works correctly even after the ship has rotated
        Vector3 inputDirection = new Vector3(input.x, input.y, 0f);
        Vector3 worldDirection = transform.TransformDirection(inputDirection);
        worldDirection.z = 0f; // Keep movement in X-Y plane
        // Normalize to preserve input magnitude (for diagonal movement), then scale by speed
        if (worldDirection.sqrMagnitude > 0.01f)
            worldDirection = worldDirection.normalized * inputDirection.magnitude;
        Vector3 targetVelocity = worldDirection * targetSpeed;

        // (Gemini) Eğer yerçekimi kullanıyorsak (useGravity=true),
        // Y hızı input'tan değil, Rigidbody'nin mevcut Y hızından alınmalı
        // ve W/S'nin hareketi engellenmeli.
        // Ancak GDD'niz  ve referanslarınız  yerçekimsiz bir ortamı
        // (useGravity=false) işaret ediyor. Bu yüzden aşağıdaki kod
        // useGravity'nin false olduğu varsayımıyla çalışır.
        if (useGravity)
        {
            // W/S'nin Y eksenini etkilememesi için input'u eziyoruz
            targetVelocity.y = rb.linearVelocity.y; 
        }

        // Z hızını Rigidbody'nin mevcut Z hızıyla koru (kilitli değilse)
        targetVelocity.z = rb.linearVelocity.z;

        rb.linearVelocity = targetVelocity;


        // (Gemini) Z pozisyonunu kilitli tut
        if (lockZToInitial)
        {
            var p = rb.position;
            p.z = zLock;
            rb.position = p;
        }
    }

    private void OnValidate()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (stats == null) stats = GetComponent<PlayerStats>();

        if (rb != null)
        {
            rb.useGravity = useGravity;
            // (Gemini) Y'den Z'ye geri değiştirildi
            rb.constraints = RigidbodyConstraints.FreezeRotation |
                             (lockZToInitial ? RigidbodyConstraints.FreezePositionZ : RigidbodyConstraints.None);
        }
    }
}