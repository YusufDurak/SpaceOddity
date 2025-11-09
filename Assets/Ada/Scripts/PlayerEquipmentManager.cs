using UnityEngine;
public enum RoleType
{
    None,
    Pilot,
    Gunner,
    Engineer,
    Mechanic
}

public class PlayerEquipmentManager : MonoBehaviour


{
    public RoleType currentRole = RoleType.None;
    public bool isAnyItemEquiped = false;
    private Equipments equipments;

    private void Update()
    {
        // Check if player presses E
        if (Input.GetKeyDown(KeyCode.E) && isAnyItemEquiped == false)
        {
            TryEquipNearbyItem();
        }
        else if ( Input.GetKeyDown(KeyCode.E) && isAnyItemEquiped == true )
        {

            UnequipItem();
        }
                    
       
        
    }

    private void TryEquipNearbyItem()
    {
        // Cast a small sphere around the player to find nearby items
        float radius = 2f; // interaction range
        Collider[] hits = Physics.OverlapSphere(transform.position, radius);

        foreach (Collider hit in hits)
        {
            Equipments item = hit.GetComponent<Equipments>();
            if (item != null)
            {
                // Equip if not equipped
                if (equipments == null || equipments != item)
                {
                    EquipItem(item);
                }
                // Unequip if same item
                else
                {
                    UnequipItem();
                }
                return;
            }
        }
    }

    private void EquipItem(Equipments item)
    {
        equipments = item;
        currentRole = item.assignedRole;
        item.isEquipped = true;
        isAnyItemEquiped = true;

        Debug.Log($"Equipped {item.itemName} ? Role: {currentRole}");
        ApplyRoleVisuals();
    }

    private void UnequipItem()
    {
        if (equipments != null)
        {
            Debug.Log($"Unequipped {equipments.itemName}");
            equipments.isEquipped = false;
            isAnyItemEquiped = false;

        }

        equipments = null;
        currentRole = RoleType.None;
        RemoveRoleVisuals();
        
    }

    private void ApplyRoleVisuals()
    {
        // Example visuals or effects
        // e.g., change player color, hat model, etc.
    }

    private void RemoveRoleVisuals()
    {
        // Reset visuals if needed
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 2f); // Draw interaction radius
    }
}
