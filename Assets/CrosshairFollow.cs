using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(NetworkObject))]
public class CrosshairFollow : NetworkBehaviour
{
    public Camera mainCamera;
    public bool hideSystemCursor = true;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        // Only show crosshair for owner
        if (!IsOwner)
        {
            gameObject.SetActive(false);
            return;
        }
        
        if (hideSystemCursor)
            Cursor.visible = false;

        if (mainCamera == null)
            mainCamera = Camera.main;
    }

    void Update()
    {
        // Only update for owner
        if (!IsOwner) return;
        
        if (mainCamera == null) return;
        
        Vector3 mousePos = Input.mousePosition;
        mousePos.z = Mathf.Abs(mainCamera.transform.position.z);
        Vector3 worldPos = mainCamera.ScreenToWorldPoint(mousePos);
        worldPos.z = 0f; // keep crosshair on world plane

        transform.position = worldPos;
    }
    
    public override void OnNetworkDespawn()
    {
        // Restore cursor when crosshair is destroyed
        if (IsOwner && hideSystemCursor)
        {
            Cursor.visible = true;
        }
        base.OnNetworkDespawn();
    }
}
