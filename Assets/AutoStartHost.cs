using Unity.Netcode;
using UnityEngine;

public class AutoStartHost : MonoBehaviour
{
    void Start()
    {
        if (NetworkManager.Singleton != null &&
            !NetworkManager.Singleton.IsListening)
        {
            Debug.Log("Starting as HOST");
            NetworkManager.Singleton.StartHost();
        }
    }
}
