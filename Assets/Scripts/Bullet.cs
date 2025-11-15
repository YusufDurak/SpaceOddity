using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Bullet with network synchronization.
/// NetworkTransform component is required to sync bullet position across the network.
/// Movement is server-authoritative - only server moves the bullet.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Unity.Netcode.Components.NetworkTransform))]
public class Bullet : NetworkBehaviour
{
    public float speed = 25f;
    public float lifetime = 3f;
    public float damage = 10f;

    private float spawnTime;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        spawnTime = Time.time;
        
        // Only server handles lifetime and collisions
        if (IsServer)
        {
            // Schedule despawn after lifetime
            Invoke(nameof(DespawnBullet), lifetime);
        }
    }

    void Update()
    {
        // Only server moves the bullet (authoritative server)
        if (IsServer)
        {
            transform.Translate(Vector3.right * speed * Time.deltaTime);
            // Use Vector3.forward instead if your gun points forward (Z+)
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Only server handles collisions
        if (!IsServer) return;

        // Example: Check for enemy hit
        if (other.CompareTag("Enemy"))
        {
            Debug.Log($"Hit enemy for {damage} damage!");
            
            // Try to damage the enemy
            IDamageable damageable = other.GetComponent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(damage);
            }
            
            DespawnBullet();
        }
    }

    private void DespawnBullet()
    {
        if (IsServer && IsSpawned)
        {
            GetComponent<NetworkObject>().Despawn(true);
        }
    }
}
