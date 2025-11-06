using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PlayerStats))]
public class PlayerMovement : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerStats stats;

    [Header("Plane/Physics")]
    [Tooltip("Karakterin Z eksenini (derinlik) kilitler. 2D oyun için true olmalı.")]
    [SerializeField] private bool lockZToInitial = true; // (Gemini) Y'den Z'ye geri değiştirildi

    [Tooltip("Rigidbody'e yerçekimi uygulansın mı?")]
    [SerializeField] private bool useGravity = false; // (Gemini) GDD  için 'false' kalması önerilir

    private Rigidbody rb;
    private Vector2 input;
    private float zLock;     // (Gemini) yLock'tan zLock'a geri değiştirildi

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (stats == null) stats = GetComponent<PlayerStats>();

        zLock = transform.position.z; // (Gemini) Y'den Z'ye geri değiştirildi
        ConfigureRigidbody();
    }

    private void ConfigureRigidbody()
    {
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.useGravity = useGravity;

        // Rotasyonları dondur
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        // (Gemini) Top-Down (X-Y) için Y yerine Z pozisyonunu kilitle
        if (lockZToInitial)
            rb.constraints |= RigidbodyConstraints.FreezePositionZ; // (Gemini) Y'den Z'ye geri değiştirildi
    }

    private void Update()
    {
        // Read WASD
        Vector2 dir = Vector2.zero;
        if (Input.GetKey(KeyCode.A)) dir.x -= 1f;
        if (Input.GetKey(KeyCode.D)) dir.x += 1f;
        if (Input.GetKey(KeyCode.S)) dir.y -= 1f;
        if (Input.GetKey(KeyCode.W)) dir.y += 1f;

        // Clamp to unit circle
        input = (dir.sqrMagnitude > 1f) ? dir.normalized : dir;
    }

    private void FixedUpdate()
    {
        // Hedef hızı PlayerStats'tan al
        float targetSpeed = (stats != null) ? stats.CurrentMoveSpeed : 5f;

        // (Gemini) DÜZELTME: Hareketi X-Y düzlemine geri al
        // input.y (W/S) -> velocity.y (Yukarı/Aşağı)
        // input.x (A/D) -> velocity.x (Sağ/Sol)
        Vector3 targetVelocity = new Vector3(input.x, input.y, 0f) * targetSpeed;

        // (Gemini) Eğer yerçekimi kullanıyorsak (useGravity=true),
        // Y hızı input'tan değil, Rigidbody'nin mevcut Y hızından alınmalı
        // ve W/S'nin hareketi engellenmeli.
        // Ancak GDD'niz  ve referanslarınız  yerçekimsiz bir ortamı
        // (useGravity=false) işaret ediyor. Bu yüzden aşağıdaki kod
        // useGravity'nin false olduğu varsayımıyla çalışır.
        if (useGravity)
        {
            // W/S'nin Y eksenini etkilememesi için input'u eziyoruz
            targetVelocity.y = rb.linearVelocity.y; 
        }

        // Z hızını Rigidbody'nin mevcut Z hızıyla koru (kilitli değilse)
        targetVelocity.z = rb.linearVelocity.z;

        rb.linearVelocity = targetVelocity;


        // (Gemini) Z pozisyonunu kilitli tut
        if (lockZToInitial)
        {
            var p = rb.position;
            p.z = zLock;
            rb.position = p;
        }
    }

    private void OnValidate()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (stats == null) stats = GetComponent<PlayerStats>();

        if (rb != null)
        {
            rb.useGravity = useGravity;
            // (Gemini) Y'den Z'ye geri değiştirildi
            rb.constraints = RigidbodyConstraints.FreezeRotation |
                             (lockZToInitial ? RigidbodyConstraints.FreezePositionZ : RigidbodyConstraints.None);
        }
    }
}