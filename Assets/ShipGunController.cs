using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(NetworkObject))]
public class ShipGunController : NetworkBehaviour
{
    [Header("References")]
    public PlayerEquipmentManager player;
    public Transform gunSeatPoint;
    public Transform gunPivot;
    public GameObject bulletPrefab;
    public Transform firePoint;

    [Header("Settings")]
    public float fireRate = 0.25f;
    public float rotationSpeed = 200f;

    [Header("Rotation Limits")]
    public float minAngle = -90f;
    public float maxAngle = 90f;

    [Header("Turret Range")]
    public float fireRange = 5f;
    public float bulletSpeed = 20f;

    // Network synchronized state
    private NetworkVariable<float> gunRotation = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    
    private NetworkVariable<ulong> controllingPlayerId = new NetworkVariable<ulong>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private float nextFireTime = 0f;
    private bool isControllingGun = false;
    private bool playerInRange = false;
    private Vector3 targetWorldPosition;
    private ulong playerInRangeId;

    public bool IsControllingGun => isControllingGun;
    public PlayerEquipmentManager CurrentPlayer { get; private set; }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        // Subscribe to rotation changes
        gunRotation.OnValueChanged += OnGunRotationChanged;
        controllingPlayerId.OnValueChanged += OnControllingPlayerChanged;
    }

    public override void OnNetworkDespawn()
    {
        gunRotation.OnValueChanged -= OnGunRotationChanged;
        controllingPlayerId.OnValueChanged -= OnControllingPlayerChanged;
        base.OnNetworkDespawn();
    }

    private void OnGunRotationChanged(float oldRotation, float newRotation)
    {
        // Only apply network rotation if this client is not controlling the gun
        // (controlling client updates locally for immediate feedback)
        if (!isControllingGun && gunPivot != null)
        {
            // Apply rotation in local space
            gunPivot.localRotation = Quaternion.Euler(0f, 0f, newRotation);
        }
    }

    private void OnControllingPlayerChanged(ulong oldPlayerId, ulong newPlayerId)
    {
        // Update controlling player reference
        if (newPlayerId != 0 && NetworkManager.SpawnManager.SpawnedObjects.ContainsKey(newPlayerId))
        {
            NetworkObject playerNetObj = NetworkManager.SpawnManager.SpawnedObjects[newPlayerId];
            CurrentPlayer = playerNetObj.GetComponent<PlayerEquipmentManager>();
            isControllingGun = CurrentPlayer != null && CurrentPlayer.OwnerClientId == NetworkManager.Singleton.LocalClientId;
            
            // Disable movement on client side as well
            if (isControllingGun && CurrentPlayer != null)
            {
                PlayerMovement playerMovement = CurrentPlayer.GetComponent<PlayerMovement>();
                if (playerMovement != null)
                {
                    playerMovement.SetMovementDisabled(true);
                }
            }
        }
        else
        {
            // Re-enable movement when no longer controlling
            if (CurrentPlayer != null)
            {
                PlayerMovement playerMovement = CurrentPlayer.GetComponent<PlayerMovement>();
                if (playerMovement != null)
                {
                    playerMovement.SetMovementDisabled(false);
                }
            }
            
            CurrentPlayer = null;
            isControllingGun = false;
        }
    }

    void Update()
    {
        // Only owner can control the gun
        if (!IsOwner && !isControllingGun) return;

        // Only allow taking control if player is in range (client-side check)
        if (IsClient && Input.GetKeyDown(KeyCode.Space) && playerInRange && player != null)
        {
            if (!isControllingGun && player.CurrentRole == RoleType.Gunner)
            {
                RequestControlServerRpc(player.NetworkObjectId);
            }
            else if (isControllingGun)
            {
                ReleaseControlServerRpc();
            }
        }

        // Only controlling player can aim and fire
        if (isControllingGun && CurrentPlayer != null && CurrentPlayer.OwnerClientId == NetworkManager.Singleton.LocalClientId)
        {
            UpdateTargetPosition();
            // Send target position and request aim update on server
            AimAtTargetServerRpc(targetWorldPosition);
            // Also update locally for immediate feedback
            AimAtTarget();

            if (Input.GetMouseButton(0))
                TryFireGun();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestControlServerRpc(ulong playerNetworkObjectId)
    {
        if (!playerInRange) return;
        
        if (playerNetworkObjectId != 0 && NetworkManager.SpawnManager.SpawnedObjects.ContainsKey(playerNetworkObjectId))
        {
            NetworkObject playerNetObj = NetworkManager.SpawnManager.SpawnedObjects[playerNetworkObjectId];
            PlayerEquipmentManager playerManager = playerNetObj.GetComponent<PlayerEquipmentManager>();
            
            if (playerManager != null && playerManager.CurrentRole == RoleType.Gunner)
            {
                controllingPlayerId.Value = playerNetworkObjectId;
                CurrentPlayer = playerManager;
                
                // Disable player movement
                PlayerMovement playerMovement = playerManager.GetComponent<PlayerMovement>();
                if (playerMovement != null)
                {
                    playerMovement.SetMovementDisabled(true);
                }
                
                // Freeze player Rigidbody
                Rigidbody playerRb = playerManager.GetComponent<Rigidbody>();
                if (playerRb != null)
                {
                    playerRb.constraints = RigidbodyConstraints.FreezeAll;
                    playerRb.linearVelocity = Vector3.zero;
                    playerRb.angularVelocity = Vector3.zero;
                }
                
                // Move player to gun seat and parent to it
                if (gunSeatPoint != null)
                {
                    Vector3 newPosition = gunSeatPoint.position;
                    newPosition.z = playerManager.transform.position.z;
                    playerManager.transform.position = newPosition;
                    playerManager.transform.rotation = gunSeatPoint.rotation;
                    playerManager.transform.SetParent(gunSeatPoint, true);
                }

                Debug.Log("Player is now controlling the gun.");
            }
            else
            {
                Debug.Log("Player must be Gunner to control this gun!");
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void ReleaseControlServerRpc()
    {
        if (CurrentPlayer != null && CurrentPlayer.NetworkObjectId == controllingPlayerId.Value)
        {
            // Re-enable player movement
            PlayerMovement playerMovement = CurrentPlayer.GetComponent<PlayerMovement>();
            if (playerMovement != null)
            {
                playerMovement.SetMovementDisabled(false);
            }
            
            // Unfreeze player Rigidbody
            Rigidbody playerRb = CurrentPlayer.GetComponent<Rigidbody>();
            if (playerRb != null)
            {
                playerRb.constraints = RigidbodyConstraints.FreezeRotation;
            }
            
            // Unparent player from gun seat
            CurrentPlayer.transform.SetParent(null);
            
            // Position player slightly behind the gun seat when exiting
            if (gunSeatPoint != null)
            {
                Vector3 exitPos = gunSeatPoint.position;
                // Exit position relative to the gun seat's local space
                if (gunSeatPoint.parent != null)
                {
                    exitPos = gunSeatPoint.parent.TransformPoint(gunSeatPoint.localPosition + Vector3.down * 1.5f);
                }
                else
                {
                    exitPos = gunSeatPoint.position + Vector3.down * 1.5f;
                }
                exitPos.z = CurrentPlayer.transform.position.z;
                CurrentPlayer.transform.position = exitPos;
            }
            
            controllingPlayerId.Value = 0;
            CurrentPlayer = null;
            Debug.Log("Player left the gun.");
        }
    }

    private void UpdateTargetPosition()
    {
        if (Camera.main == null) return;
        Plane targetPlane = new Plane(Vector3.forward, gunPivot.position);
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        float distance;
        if (targetPlane.Raycast(ray, out distance))
            targetWorldPosition = ray.GetPoint(distance);
    }

    private void AimAtTarget()
    {
        if (gunPivot == null) return;
        
        // Convert target position to local space relative to the turret's parent (ship)
        Transform parentTransform = gunPivot.parent;
        if (parentTransform == null) parentTransform = transform;
        
        Vector3 localTargetPos = parentTransform.InverseTransformPoint(targetWorldPosition);
        Vector3 localPivotPos = parentTransform.InverseTransformPoint(gunPivot.position);
        Vector3 localDir = localTargetPos - localPivotPos;
        localDir.z = 0f;

        // Calculate angle in local space
        float angle = Mathf.Atan2(localDir.y, localDir.x) * Mathf.Rad2Deg;
        float targetZRotation = angle - 90f;

        // Get current local rotation
        float currentLocalAngle = gunPivot.localEulerAngles.z;
        if (currentLocalAngle > 180f) currentLocalAngle -= 360f;

        // Clamp target angle within limits (in local space)
        float clampedTarget = Mathf.Clamp(targetZRotation, minAngle, maxAngle);
        float newLocalAngle = Mathf.MoveTowardsAngle(currentLocalAngle, clampedTarget, rotationSpeed * Time.deltaTime);
        
        // Update local rotation immediately for responsiveness
        gunPivot.localRotation = Quaternion.Euler(0f, 0f, newLocalAngle);
    }

    [ServerRpc(RequireOwnership = false)]
    private void AimAtTargetServerRpc(Vector3 targetPos)
    {
        if (gunPivot == null) return;
        targetWorldPosition = targetPos;
        
        // Convert target position to local space relative to the turret's parent (ship)
        Transform parentTransform = gunPivot.parent;
        if (parentTransform == null) parentTransform = transform;
        
        Vector3 localTargetPos = parentTransform.InverseTransformPoint(targetWorldPosition);
        Vector3 localPivotPos = parentTransform.InverseTransformPoint(gunPivot.position);
        Vector3 localDir = localTargetPos - localPivotPos;
        localDir.z = 0f;

        // Calculate angle in local space
        float angle = Mathf.Atan2(localDir.y, localDir.x) * Mathf.Rad2Deg;
        float targetZRotation = angle - 90f;

        // Get current local rotation
        float currentLocalAngle = gunPivot.localEulerAngles.z;
        if (currentLocalAngle > 180f) currentLocalAngle -= 360f;

        // Clamp target angle within limits (in local space)
        float clampedTarget = Mathf.Clamp(targetZRotation, minAngle, maxAngle);
        float newLocalAngle = Mathf.MoveTowardsAngle(currentLocalAngle, clampedTarget, rotationSpeed * Time.fixedDeltaTime);
        
        // Update local rotation on server
        gunPivot.localRotation = Quaternion.Euler(0f, 0f, newLocalAngle);
        
        // Update network variable on server (this will sync to all clients)
        gunRotation.Value = newLocalAngle;
    }

    private void TryFireGun()
    {
        if (Time.time >= nextFireTime)
        {
            nextFireTime = Time.time + fireRate;
            FireGunServerRpc(firePoint.position, targetWorldPosition);
        }
    }

    [ServerRpc]
    private void FireGunServerRpc(Vector3 firePosition, Vector3 targetPosition)
    {
        if (bulletPrefab == null || firePoint == null) return;

        Vector3 fireDir = (targetPosition - firePosition).normalized;
        
        // Spawn bullet as network object
        GameObject bulletObj = Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);
        bulletObj.transform.right = fireDir;
        
        NetworkObject bulletNetObj = bulletObj.GetComponent<NetworkObject>();
        if (bulletNetObj != null)
        {
            bulletNetObj.Spawn(true);
        }

        Rigidbody2D rb = bulletObj.GetComponent<Rigidbody2D>();
            if (rb != null)
                rb.linearVelocity = fireDir * bulletSpeed;
            else
            {
            Rigidbody rb3D = bulletObj.GetComponent<Rigidbody>();
                if (rb3D != null)
                    rb3D.linearVelocity = fireDir * bulletSpeed;
                else
                    Debug.LogWarning("Bullet has no Rigidbody!");
        }
    }

    // --- Trigger detection ---
    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;
        
        if (other.CompareTag("Player"))
        {
            PlayerEquipmentManager playerManager = other.GetComponent<PlayerEquipmentManager>();
            if (playerManager != null)
            {
                player = playerManager;
            playerInRange = true;
                playerInRangeId = playerManager.NetworkObjectId;
                
                // Notify client
                NotifyPlayerInRangeClientRpc(playerManager.OwnerClientId, true);
            Debug.Log("Player entered gun range!");
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsServer) return;
        
        if (other.CompareTag("Player"))
        {
            PlayerEquipmentManager playerManager = other.GetComponent<PlayerEquipmentManager>();
            if (playerManager != null && playerManager.NetworkObjectId == playerInRangeId)
            {
                // Notify client
                NotifyPlayerInRangeClientRpc(playerManager.OwnerClientId, false);
                
                playerInRange = false;
                
                // Automatically release gun if player leaves trigger
                if (isControllingGun && CurrentPlayer != null && CurrentPlayer.NetworkObjectId == playerInRangeId)
                {
                    ReleaseControlServerRpc();
                }
                
                Debug.Log("Player left gun range!");
            }
        }
    }
    
    [ClientRpc]
    private void NotifyPlayerInRangeClientRpc(ulong clientId, bool inRange)
    {
        // Only notify the specific client
        if (NetworkManager.Singleton.LocalClientId != clientId) return;
        
        if (inRange)
        {
            // Find local player
            if (NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(clientId) != null)
            {
                player = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(clientId).GetComponent<PlayerEquipmentManager>();
                playerInRange = true;
            }
        }
        else
        {
            player = null;
            playerInRange = false;
        }
    }
}
