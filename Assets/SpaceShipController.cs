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
    public float rotationSpeed = 80f;
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

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezePositionZ |
                         RigidbodyConstraints.FreezeRotationX |
                         RigidbodyConstraints.FreezeRotationY;
        
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
        
        input = new Vector2(moveInput, turnInput);
        
        // Send input to server for authoritative movement
        MoveShipServerRpc(input);
    }
    
    private void FixedUpdate()
    {
        // Only server applies movement (authoritative server)
        if (!IsServer) return;
        if (!isControlled.Value) return;

        // --- Move the ship ---
        if (input.x != 0f)
            rb.MovePosition(transform.position + transform.up * input.x * moveSpeed * Time.fixedDeltaTime);

        if (input.y != 0f)
            rb.MoveRotation(rb.rotation * Quaternion.Euler(0, 0, -input.y * rotationSpeed * Time.fixedDeltaTime));

        // --- Freeze the ship completely if no input ---
        if (input.x == 0f && input.y == 0f)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
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
        // Only server can enable/disable control
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

                    // Stop any running movement coroutines before starting a new one
                    StopAllCoroutines();
                    StartCoroutine(MovePlayerToHelm(pilot));

                    // Freeze player Rigidbody to lock movement
                    var playerRb = pilot.GetComponent<Rigidbody>();
                    if (playerRb)
                        playerRb.constraints = RigidbodyConstraints.FreezeAll;
                }
            }
        }
        else
        {
            if (CurrentPilot != null)
            {
                // --- UNLOCK PLAYER POSITION ---
                CurrentPilot.transform.SetParent(null);

                Vector3 exitPos = helmSeat.position + transform.up * -1.5f;
                exitPos.z = CurrentPilot.transform.position.z; // keep Z same
                CurrentPilot.transform.position = exitPos;

                // Unfreeze player Rigidbody
                var playerRb = CurrentPilot.GetComponent<Rigidbody>();
                if (playerRb)
                    playerRb.constraints = RigidbodyConstraints.FreezeRotation;

                CurrentPilot = null;
            }
            
            currentPilotId.Value = 0;
            input = Vector2.zero;
        }
    }

    private IEnumerator MovePlayerToHelm(PlayerEquipmentManager pilot)
    {
        Vector3 start = pilot.transform.position;
        Vector3 target = helmSeat.position;
        target.z = start.z; // keep Z position unchanged

        float duration = 0.4f; // smooth movement duration
        float elapsed = 0f;

        // Detach first to prevent local offset
        pilot.transform.SetParent(null);

        // Smoothly move to helm
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            pilot.transform.position = Vector3.Lerp(start, target, t);
            yield return null;
        }

        // Snap to final position and lock orientation
        pilot.transform.position = target;
        pilot.transform.rotation = helmSeat.rotation;
        pilot.transform.SetParent(transform);
    }

}
