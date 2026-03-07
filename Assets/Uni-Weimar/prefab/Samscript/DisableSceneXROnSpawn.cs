using UnityEngine;
using Unity.Netcode;

public class DisableSceneXROnSpawn : NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;

        GameObject sceneXR = GameObject.Find("XR Origin (VR)");

        if (sceneXR != null)
        {
            sceneXR.SetActive(false);
        }
    }
}