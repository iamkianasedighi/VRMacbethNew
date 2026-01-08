using Unity.Netcode;
using UnityEngine;

public class XROwnerOnly : NetworkBehaviour
{
    [SerializeField] private Camera xrCamera;
    [SerializeField] private AudioListener audioListener;

    public override void OnNetworkSpawn()
    {
        bool local = IsOwner;
        if (xrCamera) xrCamera.enabled = local;
        if (audioListener) audioListener.enabled = local;
    }
}
