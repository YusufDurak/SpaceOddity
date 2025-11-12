using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(NetworkObject))]
public class Equipments : NetworkBehaviour
{
    public string itemName;
    public RoleType assignedRole;

    public NetworkVariable<bool> isEquipped = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
}
