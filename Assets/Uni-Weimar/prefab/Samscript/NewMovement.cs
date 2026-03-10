using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using VRSYS.Core.Avatar;
using VRSYS.Core.Utility;

namespace VRSYS.Core.Navigation
{
    public class NewMovement : MonoBehaviour
    {
        #region Enums

        public enum NavigationType
        {
            Steering,
            Teleport
        }

        public enum SteeringDirection
        {
            Head,
            Hand
        }

        public enum RotationMode
        {
            Continuous,
            Snap
        }

        private enum TeleportState
        {
            Idle,
            Aiming,
            Locked
        }

        #endregion

        #region Input Actions

        [Header("Input Actions")]
        public InputActionProperty leftThumbstick;
        public InputActionProperty rightThumbstick;

        [Tooltip("Button to switch between Teleport and Steering")]
        public InputActionProperty switchModeAction;

        #endregion

        #region General Movement

        [Header("General Movement Configuration")]
        public HandType navigationHand = HandType.Left;
        public NavigationType navigationType = NavigationType.Steering;

        #endregion

        #region Steering

        [Header("Steering Configuration")]
        public Transform steeringTarget;
        public SteeringDirection steeringDirection = SteeringDirection.Hand;

        [Range(0f, 10f)]
        public float steeringSpeed = 3f;

        public bool verticalSteering = false;

        #endregion

        #region Teleport

        [Header("Teleport Configuration")]
        public Transform teleportationTarget;
        public LineRenderer ray;
        public TeleportPreviewAvatar previewAvatar;
        public float maxRayLength = 30f;
        public LayerMask teleportLayerMask = ~0;

        private float activationThreshold = 0.1f;
        private float lockThreshold = 0.9f;
        private TeleportState teleportState = TeleportState.Idle;

        #endregion

        #region Rotation

        [Header("Rotation Configuration")]
        public Transform rotationTarget;
        public Transform rotationReference;
        public RotationMode rotationMode = RotationMode.Continuous;

        [Range(0f, 720f)]
        public float continuousRotationSpeed = 240f;

        [Range(0f, 180f)]
        public float snapRotationAmount = 30f;

        public bool enableDirectionFlip = false;

        private float snapThreshold = 0.9f;
        private float lastFlipInput = 0f;
        private float lastRotInput = 0f;

        #endregion

        #region Internal

        private bool initialized = false;

        private Transform head;
        private Transform leftHand;
        private Transform rightHand;

        #endregion

        #region Unity Lifecycle

        private void OnEnable()
        {
            if (switchModeAction.action != null)
            {
                switchModeAction.action.Enable();
                switchModeAction.action.performed += OnSwitchMode;
            }

            leftThumbstick.action?.Enable();
            rightThumbstick.action?.Enable();
        }

        private void OnDisable()
        {
            if (switchModeAction.action != null)
                switchModeAction.action.performed -= OnSwitchMode;

            leftThumbstick.action?.Disable();
            rightThumbstick.action?.Disable();
        }

        private void Start()
        {
            NetworkObject netObj = GetComponentInParent<NetworkObject>();
            if (netObj != null && !netObj.IsOwner)
            {
                Destroy(this);
                return;
            }

            Initialize();
        }

        private void Update()
        {
            if (!initialized)
            {
                Initialize();
                if (!initialized)
                    return;
            }

            if (navigationType == NavigationType.Steering)
                ApplySteering();
            else
                ApplyTeleport();

            ApplyRotation();
        }

        #endregion

        #region Initialization

        private void Initialize()
        {
            AvatarHMDAnatomy anatomy = GetComponent<AvatarHMDAnatomy>();
            if (anatomy == null)
                anatomy = GetComponentInParent<AvatarHMDAnatomy>();

            if (anatomy == null)
            {
                Debug.LogError("NewMovement: AvatarHMDAnatomy not found.");
                return;
            }

            head = anatomy.head;
            leftHand = anatomy.leftHand;
            rightHand = anatomy.rightHand;

            if (head == null || leftHand == null || rightHand == null)
            {
                Debug.LogError("NewMovement: head / leftHand / rightHand reference missing in AvatarHMDAnatomy.");
                return;
            }

            if (steeringTarget == null)
                steeringTarget = transform;

            if (teleportationTarget == null)
                teleportationTarget = transform;

            if (rotationTarget == null)
                rotationTarget = transform;

            if (rotationReference == null)
                rotationReference = head;

            initialized = true;
        }

        #endregion

        #region Mode Switching

        private void OnSwitchMode(InputAction.CallbackContext ctx)
        {
            ToggleMode();
        }

        public void ToggleMode()
        {
            if (navigationType == NavigationType.Teleport)
            {
                navigationType = NavigationType.Steering;

                if (ray != null)
                    ray.enabled = false;

                if (previewAvatar != null)
                    previewAvatar.Deactivate();

                teleportState = TeleportState.Idle;

                Debug.Log("Switched to STEERING");
            }
            else
            {
                navigationType = NavigationType.Teleport;
                Debug.Log("Switched to TELEPORT");
            }
        }

        #endregion

        #region Steering

        private void ApplySteering()
        {
            InputAction moveAction = navigationHand == HandType.Left
                ? leftThumbstick.action
                : rightThumbstick.action;

            if (moveAction == null || steeringTarget == null)
                return;

            Vector2 input = moveAction.ReadValue<Vector2>();

            if (input.magnitude < 0.01f)
                return;

            Transform directionSource = steeringDirection == SteeringDirection.Head
                ? head
                : (navigationHand == HandType.Left ? leftHand : rightHand);

            if (directionSource == null)
                return;

            Vector3 forward = directionSource.forward;
            if (!verticalSteering)
                forward.y = 0f;

            if (forward.sqrMagnitude < 0.0001f)
                return;

            forward.Normalize();

            float angle = Vector2.SignedAngle(input, Vector2.up);
            Vector3 axis = verticalSteering ? directionSource.up : Vector3.up;
            Vector3 moveDir = Quaternion.AngleAxis(angle, axis) * forward;

            if (!verticalSteering)
                moveDir.y = 0f;

            steeringTarget.position += moveDir.normalized * steeringSpeed * input.magnitude * Time.deltaTime;
        }

        #endregion

        #region Teleport

        private void ApplyTeleport()
        {
            InputAction action = navigationHand == HandType.Left ? leftThumbstick.action : rightThumbstick.action;
            
            if (action == null) return;
            
            float input = action.ReadValue<Vector2>().y;

            if (input < activationThreshold)
            {
                if (teleportState == TeleportState.Locked)
                {
                    PerformTeleport();
                    return;
                }
                
                if (teleportState != TeleportState.Idle)
                {
                    if (ray != null) ray.enabled = false;
                    if (previewAvatar != null) previewAvatar.Deactivate();
                    teleportState = TeleportState.Idle;
                }
            }
            else if (input >= activationThreshold && input < lockThreshold)
            {
                if (teleportState == TeleportState.Locked)
                {
                    PerformTeleport();
                    return;
                }
                
                if (teleportState != TeleportState.Aiming)
                {
                    if (ray != null) ray.enabled = true;
                    if (previewAvatar != null) previewAvatar.ActivateIndicator();
                    teleportState = TeleportState.Aiming;
                }

                UpdateTeleportRay(input);
            }
            else if (input >= lockThreshold)
            {
                if (teleportState != TeleportState.Locked)
                {
                    if (ray != null) ray.enabled = true;
                    if (previewAvatar != null) previewAvatar.ActivateAvatar();
                    teleportState = TeleportState.Locked;
                }
                
                UpdateTeleportRay(input);
            }
        }

        private void UpdateTeleportRay(float input)
        {
            Transform hand = navigationHand == HandType.Left ? leftHand : rightHand;
            
            if (hand == null || ray == null) return;
            
            ray.SetPosition(0, hand.position);
            
            if (Physics.Raycast(hand.position, hand.forward, out RaycastHit hit, maxRayLength, teleportLayerMask))
            { 
                ray.SetPosition(1, hit.point);
                
                if (previewAvatar != null)
                {
                    if (teleportState == TeleportState.Aiming)
                        previewAvatar.UpdateIndicator(hit.point, input);
                    else if (teleportState == TeleportState.Locked)
                    {
                        float headHeightAboveRig = head.position.y - teleportationTarget.position.y;
                        previewAvatar.UpdateAvatar(hit.point, headHeightAboveRig);
                    }
                }
            }
            else
            {
                ray.SetPosition(1, hand.position + hand.forward * maxRayLength);
            }
        }

        private void PerformTeleport()
        {
            if (previewAvatar == null || teleportationTarget == null || head == null) return;
            
            Transform target = previewAvatar.transform;

            // Calculate XZ movement from current head position to target, ignoring Y
            Vector3 headPosFlat = head.position;
            headPosFlat.y = teleportationTarget.position.y;
            Vector3 movement = target.position - headPosFlat;
            teleportationTarget.Translate(movement, Space.World);

            // Rotate rig to match preview avatar direction
            float angle = Vector3.SignedAngle(head.forward, target.forward, Vector3.up);
            teleportationTarget.RotateAround(head.position, Vector3.up, angle);

            // Ground check — prevent sinking into hills/terrain
            float groundCheckDistance = 5f;
            if (Physics.Raycast(teleportationTarget.position + Vector3.up * groundCheckDistance, 
                                Vector3.down, out RaycastHit groundHit, 
                                groundCheckDistance * 2f, teleportLayerMask))
            {
                // Snap rig Y to ground surface
                teleportationTarget.position = new Vector3(
                    teleportationTarget.position.x,
                    groundHit.point.y,
                    teleportationTarget.position.z
                );
            }

            // Clean up
            if (ray != null) ray.enabled = false;
            if (previewAvatar != null) previewAvatar.Deactivate();
            teleportState = TeleportState.Idle;
        }
        #endregion


        #region Rotation

        private void ApplyRotation()
        {
            InputAction turnAction = navigationHand == HandType.Left
                ? rightThumbstick.action
                : leftThumbstick.action;

            if (turnAction == null || rotationTarget == null || rotationReference == null)
                return;

            Vector2 input = turnAction.ReadValue<Vector2>();

            if (rotationMode == RotationMode.Continuous)
            {
                if (Mathf.Abs(input.x) < 0.1f)
                    return;

                float turnBoost = 2f;
                float angle = input.x * continuousRotationSpeed * turnBoost * Time.deltaTime;
                rotationTarget.RotateAround(rotationReference.position, Vector3.up, angle);
            }
            else
            {
                if (enableDirectionFlip && lastFlipInput > -snapThreshold && input.y <= -snapThreshold)
                    rotationTarget.RotateAround(rotationReference.position, Vector3.up, 180f);

                if (Mathf.Abs(lastRotInput) < snapThreshold && Mathf.Abs(input.x) >= snapThreshold)
                {
                    float angle = input.x < 0f ? -snapRotationAmount : snapRotationAmount;
                    rotationTarget.RotateAround(rotationReference.position, Vector3.up, angle);
                }

                lastFlipInput = input.y;
                lastRotInput = input.x;
            }
        }

        #endregion
    }
}