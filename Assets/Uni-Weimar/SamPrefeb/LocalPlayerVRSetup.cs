using UnityEngine;
using Unity.Netcode;

public class LocalPlayerVRSetup : NetworkBehaviour
{
    public Camera mainCamera;
    public AudioListener audioListener;

    public override void OnNetworkSpawn()
    {
        bool isLocal = IsOwner;

        if (mainCamera) mainCamera.enabled = isLocal;
        if (audioListener) audioListener.enabled = isLocal;
    }
}
