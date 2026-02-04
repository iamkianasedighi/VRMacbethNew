using Unity.Netcode;
using UnityEngine;

public class RandomSpawner : NetworkBehaviour
{
    [Header("Prefabs (Network Prefab ASSETS only)")]
    public GameObject[] objectsToSpawn;

    [Header("Spawn Count")]
    public int spawnCount = 10;

    [Header("Spawn Area (XZ)")]
    public Vector2 areaSize = new Vector2(20f, 20f);

    [Header("Ground Placement")]
    public LayerMask groundMask;

    [Tooltip("Raycast starts this high above the spawn point")]
    public float rayStartHeight = 30f;     // ⬆ HIGHER

    [Tooltip("How far down the ray searches")]
    public float rayDistance = 100f;        // ⬆ LONGER

    [Tooltip("Lift above ground after hit")]
    public float groundOffset = 2.0f;       // ⬆ SAFER

    [Tooltip("Fallback height if no ground is found")]
    public float fallbackHeight = 2.0f;     // ⬆ SAFER

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        SpawnObjects();
    }

    private void SpawnObjects()
    {
        for (int i = 0; i < spawnCount; i++)
        {
            var prefab = objectsToSpawn[Random.Range(0, objectsToSpawn.Length)];
            if (prefab == null) continue;

            // Random XZ position
            Vector3 spawnPos = transform.position +
                               new Vector3(
                                   Random.Range(-areaSize.x * 0.5f, areaSize.x * 0.5f),
                                   0f,
                                   Random.Range(-areaSize.y * 0.5f, areaSize.y * 0.5f)
                               );

            // Raycast downward to find ground
            Vector3 rayStart = spawnPos + Vector3.up * rayStartHeight;

            if (Physics.Raycast(
                rayStart,
                Vector3.down,
                out RaycastHit hit,
                rayDistance,
                groundMask,
                QueryTriggerInteraction.Ignore))
            {
                spawnPos.y = hit.point.y + groundOffset;
            }
            else
            {
                // Absolute fallback
                spawnPos.y = transform.position.y + fallbackHeight;
            }

            GameObject go = Instantiate(prefab, spawnPos, Quaternion.identity);

            NetworkObject netObj = go.GetComponent<NetworkObject>();
            if (netObj != null)
                netObj.Spawn();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(
            transform.position,
            new Vector3(areaSize.x, 0.1f, areaSize.y)
        );
    }
}
