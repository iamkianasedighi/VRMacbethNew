using UnityEngine;
using UnityEngine.InputSystem;

public class SimpleVRTeleport : MonoBehaviour
{
    [Header("Input")]
    public InputActionProperty leftThumbstick;
    public InputActionProperty rightThumbstick;

    [Header("References")]
    public Transform xrRigRoot;
    public Transform head;
    public Transform leftHand;
    public LineRenderer teleportRay;
    public LayerMask teleportLayerMask;

    [Header("Teleport")]
    public float maxRayLength = 30f;
    public float aimThreshold = 0.15f;
    public float teleportThreshold = 0.85f;

    [Header("Rotation")]
    public float rotationSpeed = 180f;

    private bool hasValidHit = false;
    private Vector3 targetPoint;
    private bool teleportedThisPush = false;

    private void Update()
    {
        HandleTeleport();
        HandleRotation();
    }

    private void HandleTeleport()
    {
        Vector2 leftInput = leftThumbstick.action.ReadValue<Vector2>();
        float y = leftInput.y;

        if (y < aimThreshold)
        {
            teleportedThisPush = false;
            hasValidHit = false;

            if (teleportRay != null)
                teleportRay.enabled = false;

            return;
        }

        if (teleportRay != null)
            teleportRay.enabled = true;

        UpdateTeleportRay();

        if (y >= teleportThreshold && hasValidHit && !teleportedThisPush)
        {
            PerformTeleport();
            teleportedThisPush = true;
        }
    }

    private void UpdateTeleportRay()
    {
        if (leftHand == null)
            return;

        Vector3 start = leftHand.position;
        Vector3 direction = leftHand.forward;

        if (teleportRay != null)
        {
            teleportRay.positionCount = 2;
            teleportRay.SetPosition(0, start);
        }

        if (Physics.Raycast(start, direction, out RaycastHit hit, maxRayLength, teleportLayerMask))
        {
            hasValidHit = true;
            targetPoint = hit.point;

            if (teleportRay != null)
                teleportRay.SetPosition(1, hit.point);
        }
        else
        {
            hasValidHit = false;

            if (teleportRay != null)
                teleportRay.SetPosition(1, start + direction * maxRayLength);
        }
    }

    private void PerformTeleport()
    {
        if (xrRigRoot == null || head == null)
            return;

        Vector3 headOffset = head.position - xrRigRoot.position;
        Vector3 flatOffset = new Vector3(headOffset.x, 0f, headOffset.z);

        xrRigRoot.position = targetPoint - flatOffset;
    }

    private void HandleRotation()
    {
        if (xrRigRoot == null || head == null)
            return;

        Vector2 rightInput = rightThumbstick.action.ReadValue<Vector2>();
        float turn = rightInput.x;

        if (Mathf.Abs(turn) < 0.1f)
            return;

        xrRigRoot.RotateAround(head.position, Vector3.up, turn * rotationSpeed * Time.deltaTime);
    }
}