using UnityEngine;
using Unity.Netcode;

public class TrashBinTriggerLocal : MonoBehaviour
{
    public TrashType acceptedType = TrashType.Plastics;

    private void OnTriggerEnter(Collider other)
    {
        if (TrashGameManagerNet.Instance == null) return;

        // Find the networked trash object
        var netObj = other.GetComponentInParent<NetworkObject>();
        if (netObj == null) return;

        var trash = netObj.GetComponent<TrashItemNet>();
        if (trash == null) return;

        // IMPORTANT: only the OWNER of the trash sends the score request
        // prevents both players sending the same score
        if (!netObj.IsOwner) return;

        TrashGameManagerNet.Instance.TryScoreTrashServerRpc(netObj.NetworkObjectId, acceptedType);
    }
}
