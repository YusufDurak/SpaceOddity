using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(NetworkObject))]
public class ShipHelm : NetworkBehaviour
{
    private bool playerInRange;
    private PlayerEquipmentManager player;
    public SpaceShipController ship;

    private ulong playerInRangeId;

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
                
                // Notify client that they're in range
                NotifyPlayerInRangeClientRpc(playerManager.OwnerClientId, true);
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
                // Notify client that they're out of range
                NotifyPlayerInRangeClientRpc(playerManager.OwnerClientId, false);
                
            player = null;
            playerInRange = false;
                playerInRangeId = 0;
            }
        }
    }

    void Update()
    {
        if (!IsClient) return;

        // Check if local player is the current pilot (can leave helm even if not in trigger)
        bool isCurrentPilot = ship.IsControlled && ship.CurrentPilot != null && 
                              ship.CurrentPilot.OwnerClientId == NetworkManager.Singleton.LocalClientId;

        // Check if player is in range and has Pilot role (can take helm)
        bool canTakeHelm = playerInRange && player != null && player.CurrentRole == RoleType.Pilot;

        if (isCurrentPilot || canTakeHelm)
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (!ship.IsControlled)
                {
                    RequestControlServerRpc(player.NetworkObjectId);
                }
                else if (isCurrentPilot)
                {
                    ReleaseControlServerRpc();
                }
            }
        }
    }


    [ServerRpc(RequireOwnership = false)]
    private void RequestControlServerRpc(ulong playerNetworkObjectId)
    {
        if (!playerInRange) return;

        if (player != null && player.NetworkObjectId == playerNetworkObjectId && player.CurrentRole == RoleType.Pilot)
        {
            if (!ship.IsControlled)
            {
                ship.EnableControlServerRpc(true, playerNetworkObjectId);
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void ReleaseControlServerRpc()
    {
        if (ship.CurrentPilot != null)
        {
            ship.EnableControlServerRpc(false, ship.CurrentPilot.NetworkObjectId);
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
