using Unity.Netcode;
using UnityEngine;

public class TrashItemNet : NetworkBehaviour
{
    public TrashType type = TrashType.Plastics;
    public int points = 10;
}
