using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using VRSYS.Core.Utility;

namespace VRSYS.Core.Navigation
{
    public class HMDSteeringNavigation : MonoBehaviour
    {
        #region Member Variables

        [Header("Controls")]
        public Transform head;

        public HandType steeringHand;

        public InputActionProperty leftSteeringAction;
        public Transform leftController;

        public InputActionProperty rightSteeringAction;
        public Transform rightController;

        public InputActionProperty leftTurnAction;
        public InputActionProperty rightTurnAction;

        public Transform forwardIndicator;
        public NavigationBounds navigationBounds;

        [Header("Steering Properties")]
        public bool verticalSteering;
        [Range(0, 10)] public float steeringSpeed = 3f;
        [Range(0, 100)] public float rotationSpeed = 3f;

        [Header("Ground Following (Raycast)")]
        [Tooltip("Only surfaces on these layers are considered walkable ground.")]
        public LayerMask groundMask;

        [Tooltip("Raycast starts this far above the head to avoid starting inside colliders.")]
        [Min(0.1f)] public float raycastStartAboveHead = 0.5f;

        [Tooltip("How far downward the raycast checks for ground.")]
        [Min(0.5f)] public float raycastDownDistance = 10f;

        [Tooltip("Extra height offset added above the hit ground point (useful if your rig origin is not exactly at the feet).")]
        public float rigHeightOffset = 0f;

        [Tooltip("Maximum vertical change per second (prevents snapping up/down too hard).")]
        [Min(0f)] public float maxVerticalSpeed = 5f;

        [Tooltip("Optional: if true, draws the ray in Scene view.")]
        public bool debugDrawRay = false;

        #endregion

        #region MonoBehaviour Callbacks

        private void Start()
        {
            var netObj = GetComponent<NetworkObject>();
            if (netObj != null && !netObj.IsOwner)
            {
                Destroy(this);
                return;
            }
        }

        private void Update()
        {
            ApplyDisplacement();
            ApplyRotation();

            // Keep the rig on the walkable surface (raycast ground following)
            ApplyGroundFollowing();

            if (navigationBounds != null)
                EnsureIsInBounds();
        }

        private void EnsureIsInBounds()
        {
            if (navigationBounds.bounds.Contains(head.position))
                return;

            var closestPos = navigationBounds.collider.ClosestPointOnBounds(head.position);
            var displacement = closestPos - head.position;
            transform.position += displacement;
        }

        #endregion

        #region Custom Methods

        private void ApplyDisplacement()
        {
            Vector3 direction = Vector3.zero;
            float speedFactor = 0;

            if (steeringHand == HandType.Left)
            {
                speedFactor = leftSteeringAction.action.ReadValue<float>();
                direction = leftController != null ? leftController.forward : Vector3.zero;
            }
            else if (steeringHand == HandType.Right)
            {
                speedFactor = rightSteeringAction.action.ReadValue<float>();
                direction = rightController != null ? rightController.forward : Vector3.zero;
            }

            if (forwardIndicator != null)
                direction = forwardIndicator.forward;

            if (!verticalSteering)
                direction.y = 0;

            Vector3 moveVec = direction.normalized * (speedFactor * steeringSpeed * Time.deltaTime);

            // Including scale to keep perceived velocity constant with scale
            transform.position += moveVec * transform.localScale.x;
        }

        private void ApplyRotation()
        {
            float turnFactor = 0;

            if (steeringHand == HandType.Left)
                turnFactor = leftTurnAction.action.ReadValue<Vector2>().x;
            else if (steeringHand == HandType.Right)
                turnFactor = rightTurnAction.action.ReadValue<Vector2>().x;

            transform.RotateAround(head.position, Vector3.up, turnFactor * rotationSpeed * Time.deltaTime);
        }

        private void ApplyGroundFollowing()
        {
            if (head == null)
                return;

            // Ray starts above the head and goes down
            Vector3 rayOrigin = head.position + Vector3.up * raycastStartAboveHead;
            Vector3 rayDir = Vector3.down;

            if (debugDrawRay)
                Debug.DrawRay(rayOrigin, rayDir * (raycastStartAboveHead + raycastDownDistance), Color.yellow);

            float maxDist = raycastStartAboveHead + raycastDownDistance;

            if (Physics.Raycast(rayOrigin, rayDir, out RaycastHit hit, maxDist, groundMask, QueryTriggerInteraction.Ignore))
            {
                float targetY = hit.point.y + rigHeightOffset;

                Vector3 pos = transform.position;

                // Smooth / limit vertical movement
                float maxDelta = maxVerticalSpeed * Time.deltaTime;
                pos.y = Mathf.MoveTowards(pos.y, targetY, maxDelta);

                transform.position = pos;
            }
        }

        #endregion
    }
}
