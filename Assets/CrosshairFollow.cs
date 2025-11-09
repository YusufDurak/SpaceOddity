using UnityEngine;

public class CrosshairFollow : MonoBehaviour
{
    public Camera mainCamera;
    public bool hideSystemCursor = true;

    void Start()
    {
        if (hideSystemCursor)
            Cursor.visible = false;

        if (mainCamera == null)
            mainCamera = Camera.main;
    }

    void Update()
    {
        Vector3 mousePos = Input.mousePosition;
        mousePos.z = Mathf.Abs(mainCamera.transform.position.z);
        Vector3 worldPos = mainCamera.ScreenToWorldPoint(mousePos);
        worldPos.z = 0f; // keep crosshair on world plane

        transform.position = worldPos;
    }
}
