using UnityEngine;
using Unity.Netcode;

public enum RoleType
{
    None,
    Pilot,
    Gunner,
    Engineer,
    Mechanic
}

[RequireComponent(typeof(NetworkObject))]
public class PlayerEquipmentManager : NetworkBehaviour
{
    // === Network Variables ===
    private NetworkVariable<RoleType> currentRole = new NetworkVariable<RoleType>(
        RoleType.None,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<bool> isAnyItemEquipped = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // === Local References ===
    private Equipments equippedItem;

    public RoleType CurrentRole => currentRole.Value;
    public bool IsAnyItemEquipped => isAnyItemEquipped.Value;

    // === Network Lifecycle ===
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        currentRole.OnValueChanged += OnRoleChanged;
        isAnyItemEquipped.OnValueChanged += OnEquipStateChanged;
    }

    public override void OnNetworkDespawn()
    {
        currentRole.OnValueChanged -= OnRoleChanged;
        isAnyItemEquipped.OnValueChanged -= OnEquipStateChanged;
        base.OnNetworkDespawn();
    }

    // === Update Loop ===
    private void Update()
    {
        Debug.Log($"IsOwner: {IsOwner}");
        
        if (!IsOwner) return;

        if (Input.GetKeyDown(KeyCode.E))
        {
            if (!isAnyItemEquipped.Value)
            {
                TryEquipNearbyItem();
            }
            else
            {
                UnequipItemRpc(); // modern RPC çağrısı
            }
        }
    }

    // === Item Detection ===
    private void TryEquipNearbyItem()
    {
        float radius = 2f;
        Collider[] hits = Physics.OverlapSphere(transform.position, radius);

        foreach (Collider hit in hits)
        {
            Equipments item = hit.GetComponent<Equipments>();
            if (item != null && !item.isEquipped.Value)
            {
                EquipItemRpc(item.GetComponent<NetworkObject>().NetworkObjectId);
                return;
            }
        }
    }

    // === RPC Methods ===
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void EquipItemRpc(ulong itemNetworkObjectId)
    {
        if (!NetworkManager.SpawnManager.SpawnedObjects.ContainsKey(itemNetworkObjectId))
            return;

        NetworkObject itemNetObj = NetworkManager.SpawnManager.SpawnedObjects[itemNetworkObjectId];
        if (itemNetObj == null) return;

        Equipments item = itemNetObj.GetComponent<Equipments>();
        if (item == null || item.isEquipped.Value) return;

        equippedItem = item;
        item.isEquipped.Value = true;
        isAnyItemEquipped.Value = true;
        currentRole.Value = item.assignedRole;

        Debug.Log($"[Server] {OwnerClientId} equipped {item.itemName} as {currentRole.Value}");
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void UnequipItemRpc()
    {
        if (equippedItem != null)
        {
            equippedItem.isEquipped.Value = false;
            Debug.Log($"[Server] {OwnerClientId} unequipped {equippedItem.itemName}");
            equippedItem = null;
        }

        isAnyItemEquipped.Value = false;
        currentRole.Value = RoleType.None;
    }

    // === Value Change Callbacks ===
    private void OnRoleChanged(RoleType oldRole, RoleType newRole)
    {
        Debug.Log($"[Client {OwnerClientId}] Role changed: {oldRole} → {newRole}");
        
    }

    private void OnEquipStateChanged(bool oldValue, bool newValue)
    {
        Debug.Log($"[Client {OwnerClientId}] Equip state: {oldValue} → {newValue}");
    }

    // === Visuals / FX ===
   

  

    // === Gizmos ===
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 2f);
    }
}
