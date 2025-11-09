using UnityEngine;
using System.Collections;
public class SpaceShipController : MonoBehaviour
{
    [Header("Ship Settings")]
    public float moveSpeed = 10f;
    public float rotationSpeed = 80f;
    public Transform helmSeat; // The exact center of the helm
    

    [HideInInspector] public bool isControlled = false;
    [HideInInspector] public PlayerEquipmentManager currentPilot;

    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezePositionZ |
                         RigidbodyConstraints.FreezeRotationX |
                         RigidbodyConstraints.FreezeRotationY;
    }

    void Update()
    {
        if (!isControlled) return;

        float moveInput = Input.GetAxis("Vertical");
        float turnInput = Input.GetAxis("Horizontal");

        // --- Move the ship ---
        if (moveInput != 0f)
            rb.MovePosition(transform.position + transform.up * moveInput * moveSpeed * Time.deltaTime);

        if (turnInput != 0f)
            rb.MoveRotation(rb.rotation * Quaternion.Euler(0, 0, -turnInput * rotationSpeed * Time.deltaTime));

        // --- Freeze the ship completely if no input ---
        if (moveInput == 0f && turnInput == 0f)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }


    public void EnableControl(bool state, PlayerEquipmentManager pilot)
    {
        isControlled = state;

        if (state)
        {
            currentPilot = pilot;

            // Stop any running movement coroutines before starting a new one
            StopAllCoroutines();
            StartCoroutine(MovePlayerToHelm(pilot));

            // Freeze player Rigidbody to lock movement
            var playerRb = pilot.GetComponent<Rigidbody>();
            if (playerRb)
                playerRb.constraints = RigidbodyConstraints.FreezeAll;
        }
        else
        {
            // --- UNLOCK PLAYER POSITION ---
            pilot.transform.SetParent(null);

            Vector3 exitPos = helmSeat.position + transform.up * -1.5f;
            exitPos.z = pilot.transform.position.z; // keep Z same
            pilot.transform.position = exitPos;

            // Unfreeze player Rigidbody
            var playerRb = pilot.GetComponent<Rigidbody>();
            if (playerRb)
                playerRb.constraints = RigidbodyConstraints.FreezeRotation;

            currentPilot = null;
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
