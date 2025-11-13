using UnityEngine;
using System.Collections;
using Unity.Netcode;

/// <summary>
/// Ship controller with network synchronization.
/// NetworkTransform component is required and should be configured to sync Rigidbody position/rotation.
/// Movement is server-authoritative - only the current pilot can send input to the server.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Unity.Netcode.Components.NetworkTransform))]
public class SpaceShipController : NetworkBehaviour
{
    [Header("Ship Settings")]
    public float moveSpeed = 10f;
    public float rotationSpeed = 2f;
    public Transform helmSeat; // The exact center of the helm
    

    // Network synchronized control state
    private NetworkVariable<bool> isControlled = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    
    private NetworkVariable<ulong> currentPilotId = new NetworkVariable<ulong>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public bool IsControlled => isControlled.Value;
    public PlayerEquipmentManager CurrentPilot { get; private set; }

    private Rigidbody rb;
    private Vector2 input;
    private float lockedPilotZ; // Store the Z position when pilot is locked

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezePositionZ |
                         RigidbodyConstraints.FreezeRotationX |
                         RigidbodyConstraints.FreezeRotationY;
        
        // Configure Rigidbody for smooth movement
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.linearDamping = 5f; // Add drag to smooth out movement
        rb.angularDamping = 5f; // Add angular drag to smooth out rotation
        
        // Subscribe to control state changes
        isControlled.OnValueChanged += OnControlStateChanged;
        currentPilotId.OnValueChanged += OnPilotChanged;
    }

    public override void OnNetworkDespawn()
    {
        isControlled.OnValueChanged -= OnControlStateChanged;
        currentPilotId.OnValueChanged -= OnPilotChanged;
        base.OnNetworkDespawn();
    }

    private void OnControlStateChanged(bool oldValue, bool newValue)
    {
        // Handle control state changes if needed
    }

    private void OnPilotChanged(ulong oldPilotId, ulong newPilotId)
    {
        // Update current pilot reference
        if (newPilotId != 0 && NetworkManager.SpawnManager.SpawnedObjects.ContainsKey(newPilotId))
        {
            NetworkObject pilotNetObj = NetworkManager.SpawnManager.SpawnedObjects[newPilotId];
            CurrentPilot = pilotNetObj.GetComponent<PlayerEquipmentManager>();
        }
        else
        {
            CurrentPilot = null;
        }
    }

    void Update()
    {
        // Only current pilot can control the ship
        if (!isControlled.Value || CurrentPilot == null) return;
        
        // Check if local client is the pilot's owner
        if (NetworkManager.Singleton == null || CurrentPilot.OwnerClientId != NetworkManager.Singleton.LocalClientId) return;

        float moveInput = Input.GetAxis("Vertical");
        float turnInput = Input.GetAxis("Horizontal");
        
        Vector2 newInput = new Vector2(moveInput, turnInput);
        
        // Only send input if it changed (reduces network traffic)
        if (newInput != input)
        {
            input = newInput;
            // Send input to server for authoritative movement
            MoveShipServerRpc(input);
        }
    }
    
    private void FixedUpdate()
    {
        // Only server applies movement (authoritative server)
        if (!IsServer) return;
        if (!isControlled.Value) return;

        // Use velocity-based movement for smoother physics
        // Forward/backward movement
        Vector3 targetVelocity = transform.up * input.x * moveSpeed;
        rb.linearVelocity = new Vector3(targetVelocity.x, targetVelocity.y, rb.linearVelocity.z);

        // Rotation
        float targetAngularVelocity = -input.y * rotationSpeed;
        rb.angularVelocity = new Vector3(0, 0, targetAngularVelocity);
        
        // Keep pilot locked to helm seat position
        if (CurrentPilot != null && helmSeat != null)
        {
            // Use helm seat's local position directly (it's a child of the ship)
            Vector3 targetLocalPos = helmSeat.localPosition;
            // Always use the stored Z position to prevent jitter
            targetLocalPos.z = lockedPilotZ;
            CurrentPilot.transform.localPosition = targetLocalPos;
            CurrentPilot.transform.localRotation = helmSeat.localRotation;
        }
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void MoveShipServerRpc(Vector2 moveInput, ServerRpcParams rpcParams = default)
    {
        // Only allow movement if controlled and the requesting client is the pilot
        if (!isControlled.Value || CurrentPilot == null) return;
        
        // Verify the requesting client is the pilot's owner
        ulong clientId = rpcParams.Receive.SenderClientId;
        if (CurrentPilot.OwnerClientId != clientId) return;
        
        input = moveInput;
    }


    [ServerRpc(RequireOwnership = false)]
    public void EnableControlServerRpc(bool state, ulong pilotNetworkObjectId)
    {
        EnableControl(state, pilotNetworkObjectId);
    }
    
    public void EnableControl(bool state, ulong pilotNetworkObjectId)
    {
        if (!IsServer) return;

        isControlled.Value = state;

        if (state)
        {
            if (pilotNetworkObjectId != 0 && NetworkManager.SpawnManager.SpawnedObjects.ContainsKey(pilotNetworkObjectId))
            {
                NetworkObject pilotNetObj = NetworkManager.SpawnManager.SpawnedObjects[pilotNetworkObjectId];
                PlayerEquipmentManager pilot = pilotNetObj.GetComponent<PlayerEquipmentManager>();
                
                if (pilot != null)
                {
                    currentPilotId.Value = pilotNetworkObjectId;
                    CurrentPilot = pilot;

                    // Disable player movement
                    PlayerMovement playerMovement = pilot.GetComponent<PlayerMovement>();
                    if (playerMovement != null)
                    {
                        playerMovement.SetMovementDisabled(true);
                    }

                    var playerRb = pilot.GetComponent<Rigidbody>();
                    if (playerRb)
                    {
                        playerRb.isKinematic = true; // Fiziği tamamen kapatmak daha güvenli
                        playerRb.detectCollisions = false; // Gemiyle çarpışmasını engelle
                        // Lock Z position constraint
                        playerRb.constraints = RigidbodyConstraints.FreezeAll;
                    }

                    StopAllCoroutines();
                    // PilotNetObj'yi parametre olarak gönderiyoruz
                    StartCoroutine(MovePlayerToHelm(pilot, pilotNetObj)); 
                }
            }
        }
        else
        {
            if (CurrentPilot != null)
            {
                // Re-enable player movement
                PlayerMovement playerMovement = CurrentPilot.GetComponent<PlayerMovement>();
                if (playerMovement != null)
                {
                    playerMovement.SetMovementDisabled(false);
                }

                // UNLOCK PLAYER
                // NetworkParenting'i kaldırıyoruz
                NetworkObject pilotNetObj = CurrentPilot.GetComponent<NetworkObject>();
                pilotNetObj.TryRemoveParent(); 

                Vector3 exitPos = helmSeat.position + transform.up * -1.5f;
                exitPos.z = CurrentPilot.transform.position.z; // Preserve Z position
                CurrentPilot.transform.position = exitPos;
                CurrentPilot.transform.rotation = Quaternion.identity; // Rotasyonu düzelt

                var playerRb = CurrentPilot.GetComponent<Rigidbody>();
                if (playerRb)
                {
                    playerRb.isKinematic = false;
                    playerRb.detectCollisions = true;
                    // Restore original constraints
                    playerRb.constraints = RigidbodyConstraints.FreezeRotation;
                    if (playerMovement != null && playerMovement.IsLockZToInitial)
                    {
                        playerRb.constraints |= RigidbodyConstraints.FreezePositionZ;
                    }
                }

                CurrentPilot = null;
            }
            
            currentPilotId.Value = 0;
            input = Vector2.zero;
        }
    }

   private IEnumerator MovePlayerToHelm(PlayerEquipmentManager pilot, NetworkObject pilotNetObj)
    {
        Vector3 startWorldPos = pilot.transform.position;
        Vector3 targetWorldPos = helmSeat.position;
        
        // Preserve Z position from start
        float preservedZ = startWorldPos.z;
        targetWorldPos.z = preservedZ;
        
        float duration = 0.4f;
        float elapsed = 0f;

        // ÖNCE Parenting işlemini yap (NetworkObject ile)
        // Bu işlem server-side olduğu için diğer clientlara otomatik bildirilir.
        pilotNetObj.TrySetParent(GetComponent<NetworkObject>());

        // Convert to local space after parenting
        Vector3 startLocalPos = transform.InverseTransformPoint(startWorldPos);
        Vector3 targetLocalPos = transform.InverseTransformPoint(targetWorldPos);
        // Ensure Z is preserved in local space
        targetLocalPos.z = transform.InverseTransformPoint(new Vector3(targetWorldPos.x, targetWorldPos.y, preservedZ)).z;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            // Use local position since player is now child of ship
            Vector3 currentLocalPos = Vector3.Lerp(startLocalPos, targetLocalPos, t);
            // Lock Z in local space
            currentLocalPos.z = targetLocalPos.z;
            pilot.transform.localPosition = currentLocalPos;
            yield return null;
        }

        // Snap to final position and rotation
        pilot.transform.localPosition = targetLocalPos;
        pilot.transform.localRotation = helmSeat.localRotation;
        
        // Store the locked Z position for continuous locking
        lockedPilotZ = targetLocalPos.z;
        
        // Ensure Z position is locked (double-check)
        Vector3 finalPos = pilot.transform.localPosition;
        finalPos.z = lockedPilotZ;
        pilot.transform.localPosition = finalPos;
    }

}
