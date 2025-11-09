using UnityEngine;

public class Bullet : MonoBehaviour
{
    public float speed = 25f;
    public float lifetime = 3f;
    public float damage = 10f;

    private void Start()
    {
        Destroy(gameObject, lifetime); // Bullet auto-destroys after some seconds
    }

    void Update()
    {
        transform.Translate(Vector3.right * speed * Time.deltaTime);
        // Use Vector3.forward instead if your gun points forward (Z+)
    }

    private void OnTriggerEnter(Collider other)
    {
        // Example: Check for enemy hit
        if (other.CompareTag("Enemy"))
        {
            Debug.Log($"Hit enemy for {damage} damage!");
            Destroy(gameObject);
        }
    }
}
