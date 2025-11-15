using UnityEngine;

[System.Serializable]
public class BackgroundLayer
{
    public Transform layerTransform;
    [Range(0f, 1f)]
    public float scrollSpeed = 0.5f;
    public float spriteWidth = 20f;
}

public class Parallax : MonoBehaviour
{
    [SerializeField] private BackgroundLayer[] layers;

    private Transform cam;
    private Vector3 previousCamPos;

    private bool initialized = false;

    private void LateUpdate()
    {
        // Camera not spawned yet  try to find it
        if (cam == null)
        {
            if (Camera.main != null)
            {
                cam = Camera.main.transform;
                previousCamPos = cam.position;
                initialized = true;
            }
            else
            {
                return; // still no camera, skip this frame
            }
        }

        if (!initialized) return;

        Vector3 camDelta = cam.position - previousCamPos;

        foreach (var layer in layers)
        {
            // Parallax movement
            layer.layerTransform.position += new Vector3(
                camDelta.x * layer.scrollSpeed,
                camDelta.y * layer.scrollSpeed,
                0
            );

            // Infinite loop
            float dist = cam.position.x - layer.layerTransform.position.x;

            if (Mathf.Abs(dist) > layer.spriteWidth)
            {
                float direction = Mathf.Sign(dist);
                layer.layerTransform.position += new Vector3(
                    layer.spriteWidth * 2f * direction,
                    0,
                    0
                );
            }
        }

        previousCamPos = cam.position;
    }
}
