using Unity.Netcode;
using UnityEngine;

public class AutoStartNetwork : MonoBehaviour
{
    void Start()
    {
        // For quick testing: start as Host automatically
        if (!NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.StartHost();
    }
}
