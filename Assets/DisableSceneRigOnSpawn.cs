using System.Collections;
using UnityEngine;
using Unity.Netcode;

public class DisableSceneRigOnSpawn : MonoBehaviour
{
    public GameObject rigRoot;

    private void Awake()
    {
        if (rigRoot == null) rigRoot = gameObject;
    }

    private void Start()
    {
        StartCoroutine(DisableWhenLocalUserExists());
    }

    private IEnumerator DisableWhenLocalUserExists()
    {
        // Wait until NetworkManager exists
        while (NetworkManager.Singleton == null)
            yield return null;

        // Wait until networking is actually running (Host/Client started)
        while (!NetworkManager.Singleton.IsListening)
            yield return null;

        // Wait until the local spawned user exists (Our_Lobby_User)
        while (true)
        {
            // If VRSYS spawned the local user, it will be an owned NetworkObject.
            foreach (var no in FindObjectsOfType<NetworkObject>())
            {
                if (no != null && no.IsSpawned && no.IsOwner)
                {
                    // Found an owned spawned object -> disable scene rig
                    rigRoot.SetActive(false);
                    yield break;
                }
            }
            yield return null;
        }
    }
}
