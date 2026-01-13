using UnityEngine;
using Unity.Netcode;

public class CatSpawner : NetworkBehaviour
{
    public NetworkObject catPrefab;
    public Transform spawnPoint;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        // Avoid spawning twice if scene reloads or multiple spawners exist
        if (GameObject.FindWithTag("SharedCat") != null) return;

        var cat = Instantiate(catPrefab, spawnPoint.position, spawnPoint.rotation);
        cat.Spawn();
    }
}
