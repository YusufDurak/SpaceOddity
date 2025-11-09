using UnityEngine;

public class ShipGunController : MonoBehaviour
{
    [Header("References")]
    public PlayerEquipmentManager player;
    public Transform gunSeatPoint;
    public Transform gunPivot;
    public GameObject bulletPrefab;
    public Transform firePoint;

    [Header("Settings")]
    public float fireRate = 0.25f;
    public float rotationSpeed = 200f;

    [Header("Rotation Limits")]
    public float minAngle = -90f;
    public float maxAngle = 90f;

    [Header("Turret Range")]
    public float fireRange = 5f;
    public float bulletSpeed = 20f;

    private float nextFireTime = 0f;
    private bool isControllingGun = false;
    private bool playerInRange = false; // NEW: Track if player is in collider
    private Vector3 targetWorldPosition;

    void Update()
    {
        // Only allow taking control if player is in range
        if (Input.GetKeyDown(KeyCode.Space) && playerInRange)
        {
            if (!isControllingGun)
                TakeControl();
            else
                ReleaseControl();
        }

        if (isControllingGun)
        {
            UpdateTargetPosition();
            AimAtTarget();

            if (Input.GetMouseButton(0))
                TryFireGun();
        }
    }

    void TakeControl()
    {
        if (player != null && player.currentRole.ToString() == "Gunner")
        {
            isControllingGun = true;
            Vector3 newPosition = gunSeatPoint.position;
            newPosition.z = player.transform.position.z;
            player.transform.position = newPosition;

            player.enabled = false;
            Debug.Log("Player is now controlling the gun.");
        }
        else
        {
            Debug.Log("Player must be Gunner to control this gun!");
        }
    }

    void ReleaseControl()
    {
        isControllingGun = false;
        if (player != null)
            player.enabled = true;

        Debug.Log("Player left the gun.");
    }

    void UpdateTargetPosition()
    {
        if (Camera.main == null) return;
        Plane targetPlane = new Plane(Vector3.forward, gunPivot.position);
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        float distance;
        if (targetPlane.Raycast(ray, out distance))
            targetWorldPosition = ray.GetPoint(distance);
    }

    void AimAtTarget()
    {
        if (gunPivot == null) return;
        Vector3 dir = targetWorldPosition - gunPivot.position;
        dir.z = 0f;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        float targetZRotation = angle - 90f;

        float currentAngle = gunPivot.eulerAngles.z;
        if (currentAngle > 180f) currentAngle -= 360f;

        float clampedTarget = Mathf.Clamp(targetZRotation, minAngle, maxAngle);
        float newAngle = Mathf.MoveTowardsAngle(currentAngle, clampedTarget, rotationSpeed * Time.deltaTime);
        gunPivot.rotation = Quaternion.Euler(0f, 0f, newAngle);
    }

    void TryFireGun()
    {
        if (Time.time >= nextFireTime)
        {
            nextFireTime = Time.time + fireRate;
            FireGun();
        }
    }

    void FireGun()
    {
        if (bulletPrefab != null && firePoint != null)
        {
            Vector3 fireDir = (targetWorldPosition - firePoint.position).normalized;
            GameObject bullet = Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);
            bullet.transform.right = fireDir;

            Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
            if (rb != null)
                rb.linearVelocity = fireDir * bulletSpeed;
            else
            {
                Rigidbody rb3D = bullet.GetComponent<Rigidbody>();
                if (rb3D != null)
                    rb3D.linearVelocity = fireDir * bulletSpeed;
                else
                    Debug.LogWarning("Bullet has no Rigidbody!");
            }
        }
    }

    // --- NEW: Trigger detection ---
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) // Make sure your player has the "Player" tag
        {
            playerInRange = true;
            Debug.Log("Player entered gun range!");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;

            if (isControllingGun)
                ReleaseControl(); // Automatically release gun if player leaves trigger
            Debug.Log("Player left gun range!");
        }
    }
}
