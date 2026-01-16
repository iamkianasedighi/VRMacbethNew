using UnityEngine;
using Unity.XR.CoreUtils;

public class GroundFollower : MonoBehaviour
{
    [Header("References")]
    public XROrigin xrOrigin;
    public Transform head;

    [Header("Ground Detection")]
    public LayerMask groundLayer;
    public float raycastDistance = 5f;

    void Update()
    {
        Vector3 rayStart = xrOrigin.transform.position + Vector3.up;
        Ray ray = new Ray(rayStart, Vector3.down);

        if (Physics.Raycast(ray, out RaycastHit hit, raycastDistance, groundLayer))
        {
            float headHeight = head.localPosition.y;

            Vector3 newPosition = xrOrigin.transform.position;
            newPosition.y = hit.point.y - headHeight;

            xrOrigin.transform.position = newPosition;
        }
    }
}