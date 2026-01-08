using UnityEngine;

public class RandomSpawner : MonoBehaviour
{
    [Header("List of prefabs you want to spawn")]
    public GameObject[] objectsToSpawn;

    [Header("How many objects to spawn")]
    public int spawnCount = 5;

    [Header("Random area size")]
    public Vector3 areaSize = new Vector3(10, 1, 10);

    void Start()
    {
        SpawnRandomObjects();
    }

    void SpawnRandomObjects()
    {
        for (int i = 0; i < spawnCount; i++)
        {
            // Choose a random prefab
            GameObject prefab = objectsToSpawn[Random.Range(0, objectsToSpawn.Length)];

            // Create random position
            Vector3 randomPosition = transform.position +
                                     new Vector3(
                                         Random.Range(-areaSize.x / 2, areaSize.x / 2),
                                         0,
                                         Random.Range(-areaSize.z / 2, areaSize.z / 2)
                                     );

            // Spawn
            Instantiate(prefab, randomPosition, Quaternion.identity);
        }
    }

    // draw spawn area in editor
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, areaSize);
    }
}
