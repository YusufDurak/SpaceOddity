using UnityEngine;

public class ShipHelm : MonoBehaviour
{
    private bool playerInRange;
    private PlayerEquipmentManager player;
    public SpaceShipController ship;

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            player = other.GetComponent<PlayerEquipmentManager>();
            if (player != null)
                playerInRange = true;
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            player = null;
            playerInRange = false;
        }
    }

    void Update()
    {
        if (playerInRange && player != null && player.currentRole == RoleType.Pilot)
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (!ship.isControlled)
                    ship.EnableControl(true, player);
                else if (ship.currentPilot == player)
                    ship.EnableControl(false, player);
            }
        }
    }
}
