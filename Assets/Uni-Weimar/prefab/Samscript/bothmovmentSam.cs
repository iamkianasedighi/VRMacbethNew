using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using VRSYS.Core.Avatar;
using VRSYS.Core.Logging;
using VRSYS.Core.Utility;

namespace VRSYS.Core.Navigation
{
    /// <summary>
    /// ONE locomotion script:
    /// - Steering movement OR Teleport movement (toggle in runtime via a button)
    /// - Rotation via the other thumbstick (continuous or snap)
    /// - Direction reference: Head or Hand
    /// - Network owner-only
    /// </summary>
    public class ThumbstickNavigationToggle : MonoBehaviour
    {
        #region Enums

        public enum NavigationType { Steering, Teleport }
        public enum SteeringDirection { Head, Hand }
        public enum RotationMode { Continuous, Snap }

        private enum TeleportState { Idle, Aiming, Locked }

        #endregion

        #region Inspector

        [Header("Network")]
        [Tooltip("If true, only the owner can run locomotion (recommended for Netcode).")]
        public bool ownerOnly = true;

        [Header("Input Actions")]
        [Tooltip("Vector2 from left thumbstick.")]
        public InputActionProperty leftThumbstick;

        [Tooltip("Vector2 from right thumbstick.")]
        public InputActionProperty rightThumbstick;

        [Tooltip("Button used to toggle between Steering and Teleport (Press).")]
        public InputActionProperty toggleNavigationAction;

        [Header("General")]
        [Tooltip("Which thumbstick is used for navigation (the other becomes rotation).")]
        public HandType navigationHand = HandType.Left;

        [Tooltip("Start mode.")]
        public NavigationType navigationType = NavigationType.Steering;

        [Header("Steering")]
        [Tooltip("Target moved during steering. If null, rig root is used.")]
        public Transform steeringTarget;

        [Tooltip("Forward reference for steering.")]
        public SteeringDirection steeringDirection = SteeringDirection.Hand;

        [Range(0, 10)] public float steeringSpeed = 3f;

        [Tooltip("If false, steering stays on XZ plane.")]
        public bool verticalSteering = false;

        [Header("Teleport")]
        [Tooltip("Target moved during teleportation. If null, rig root is used.")]
        public Transform teleportationTarget;

        public LineRenderer ray;
        public TeleportPreviewAvatar previewAvatar;

        public float maxRayLength = 30f;
        public LayerMask teleportLayerMask;

        [Header("Rotation")]
        [Tooltip("Target rotated. If null, rig root is used.")]
        public Transform rotationTarget;

        [Tooltip("Pivot reference for rotation. If null, head is used.")]
        public Transform rotationReference;

        public RotationMode rotationMode = RotationMode.Continuous;

        [Range(0, 360)] public float continuousRotationSpeed = 180f;
        [Range(0, 180)] public float snapRotationAmount = 30f;

        [Tooltip("Optional: flip 180° when pushing stick down past threshold.")]
        public bool enableDirectionFlip = false;

        #endregion

        #region Runtime State

        private NetworkObject netObj;
        private bool initialized = false;

        // Anatomy references
        private Transform head;
        private Transform leftHand;
        private Transform rightHand;

        // Rig root target (we move/rotate this)
        private Transform rigRoot;

        // Toggle state
        private bool lastTogglePressed = false;

        // Teleport state
        private readonly float activationThreshold = 0.1f;
        private readonly float lockThreshold = 0.9f;
        private TeleportState teleportState = TeleportState.Idle;

        // Deadzone for sticks
        private const float deadzone = 0.1f;

        // Snap rotation edge detection
        private readonly float snapThreshold = 0.9f;
        private float lastFlipInput = 0.0f;
        private float lastRotInput = 0.0f;

        #endregion

        #region Unity

        private void Awake()
        {
            netObj = GetComponentInParent<NetworkObject>();
        }

        private void Start()
        {
            // Owner-only: disable this script on remote players
            if (ownerOnly && netObj != null && !netObj.IsOwner)
            {
                Destroy(this);
                return;
            }

            Initialize();
            EnableTeleportVisuals(false);
        }

        private void OnEnable()
        {
            // Enabling actions helps if they are not auto-enabled
            leftThumbstick.action?.Enable();
            rightThumbstick.action?.Enable();
            toggleNavigationAction.action?.Enable();
        }

        private void OnDisable()
        {
            leftThumbstick.action?.Disable();
            rightThumbstick.action?.Disable();
            toggleNavigationAction.action?.Disable();
        }

        private void Update()
        {
            if (!initialized)
            {
                Initialize();
                return;
            }

            HandleToggleInput();

            // Movement
            if (navigationType == NavigationType.Steering)
                ApplySteering();
            else
                ApplyTeleport();

            // Rotation always
            ApplyRotation();
        }

        #endregion

        #region Initialize

        private void Initialize()
        {
            // IMPORTANT: anatomy is often on the root, not necessarily the same object
            AvatarHMDAnatomy anatomy = GetComponentInParent<AvatarHMDAnatomy>();
            if (anatomy == null)
            {
                ExtendedLogger.LogError(GetType().Name,
                    "AvatarHMDAnatomy not found in parents. Attach this script to the rig root or ensure AvatarHMDAnatomy is in a parent.", this);
                return;
            }

            head = anatomy.head;
            leftHand = anatomy.leftHand;
            rightHand = anatomy.rightHand;

            // Determine rig root (prefer NetworkObject root, else Transform.root)
            if (netObj == null) netObj = GetComponentInParent<NetworkObject>();
            rigRoot = (netObj != null) ? netObj.transform : transform.root;

            // Defaults: move/rotate the rig root
            if (steeringTarget == null) steeringTarget = rigRoot;
            if (teleportationTarget == null) teleportationTarget = rigRoot;
            if (rotationTarget == null) rotationTarget = rigRoot;

            // Default rotation pivot: head (matches original RotateAround(head.position,...))
            if (rotationReference == null) rotationReference = head;

            initialized = true;
        }

        #endregion

        #region Toggle Logic

        private void HandleToggleInput()
        {
            if (toggleNavigationAction.action == null) return;

            bool pressed = toggleNavigationAction.action.ReadValue<float>() > 0.5f;

            // Rising edge => toggle once
            if (pressed && !lastTogglePressed)
            {
                navigationType = (navigationType == NavigationType.Steering)
                    ? NavigationType.Teleport
                    : NavigationType.Steering;

                // Clean visuals/state when switching modes
                teleportState = TeleportState.Idle;
                EnableTeleportVisuals(false);
                previewAvatar?.Deactivate();

                ExtendedLogger.LogInfo(GetType().Name, $"Navigation switched to: {navigationType}", this);
            }

            lastTogglePressed = pressed;
        }

        private void EnableTeleportVisuals(bool enabled)
        {
            if (ray != null) ray.enabled = enabled;
        }

        #endregion

        #region Steering

        private Transform ForwardIndicator
        {
            get
            {
                return steeringDirection == SteeringDirection.Head
                    ? head
                    : (navigationHand == HandType.Left ? leftHand : rightHand);
            }
        }

        private Vector3 ForwardDirection
        {
            get
            {
                Vector3 dir = steeringDirection == SteeringDirection.Head
                    ? head.forward
                    : (navigationHand == HandType.Left ? leftHand.forward : rightHand.forward);

                if (!verticalSteering) dir.y = 0f;
                return dir.sqrMagnitude < 0.0001f ? Vector3.forward : dir.normalized;
            }
        }

        private void ApplySteering()
        {
            InputAction navAction = (navigationHand == HandType.Left) ? leftThumbstick.action : rightThumbstick.action;
            if (navAction == null) return;

            Vector2 input = navAction.ReadValue<Vector2>();
            if (input.sqrMagnitude < deadzone * deadzone) return;

            Vector3 direction = GetSteeringDirection(input);

            // Include scale to keep perceived velocity constant with scale (like original)
            float scaleFactor = steeringTarget.localScale.x;

            steeringTarget.position += direction * (steeringSpeed * input.magnitude * Time.deltaTime) * scaleFactor;
        }

        private Vector3 GetSteeringDirection(Vector2 input)
        {
            // Correct order: angle from "up" to the input direction
            float angle = Vector2.SignedAngle(Vector2.up, input);

            Vector3 axis = verticalSteering ? ForwardIndicator.up : Vector3.up;
            Vector3 dir = Quaternion.AngleAxis(angle, axis) * ForwardDirection;

            if (!verticalSteering) dir.y = 0f;

            return dir.sqrMagnitude < 0.0001f ? Vector3.zero : dir.normalized;
        }

        #endregion

        #region Teleport

        private void ApplyTeleport()
        {
            InputAction action = (navigationHand == HandType.Left) ? leftThumbstick.action : rightThumbstick.action;
            if (action == null) return;

            float inputY = action.ReadValue<Vector2>().y;

            // Release / below activation: if locked => teleport, else cleanup
            if (inputY < activationThreshold)
            {
                if (teleportState == TeleportState.Locked)
                {
                    PerformTeleport();
                    return;
                }

                if (teleportState != TeleportState.Idle)
                {
                    EnableTeleportVisuals(false);
                    previewAvatar?.Deactivate();
                    teleportState = TeleportState.Idle;
                }
                return;
            }

            // Aiming
            if (inputY >= activationThreshold && inputY < lockThreshold)
            {
                if (teleportState == TeleportState.Locked)
                {
                    PerformTeleport();
                    return;
                }

                if (teleportState != TeleportState.Aiming)
                {
                    EnableTeleportVisuals(true);
                    previewAvatar?.ActivateIndicator();
                    teleportState = TeleportState.Aiming;
                }

                UpdateTeleportRay(inputY);
                return;
            }

            // Locked
            if (inputY >= lockThreshold)
            {
                if (teleportState != TeleportState.Locked)
                {
                    EnableTeleportVisuals(true);
                    previewAvatar?.ActivateAvatar();
                    teleportState = TeleportState.Locked;
                }

                UpdateTeleportRay(inputY);
            }
        }

        private void UpdateTeleportRay(float inputY)
        {
            if (ray == null || previewAvatar == null) return;

            Transform hand = (navigationHand == HandType.Left) ? leftHand : rightHand;
            if (hand == null) return;

            ray.SetPosition(0, hand.position);

            if (Physics.Raycast(hand.position, hand.forward, out RaycastHit hit, maxRayLength, teleportLayerMask))
            {
                ray.SetPosition(1, hit.point);

                if (teleportState == TeleportState.Aiming)
                    previewAvatar.UpdateIndicator(hit.point, inputY);
                else if (teleportState == TeleportState.Locked)
                    previewAvatar.UpdateAvatar(hit.point, head != null ? head.localPosition.y : 1.6f);
            }
            else
            {
                ray.SetPosition(1, hand.position + hand.forward);
            }
        }

        private void PerformTeleport()
        {
            if (previewAvatar == null || head == null) return;

            Transform target = previewAvatar.transform;

            // Flatten head to rig y-level (same idea as your original)
            Vector3 headPos = head.position;
            headPos.y = teleportationTarget.position.y;

            Vector3 movement = target.position - headPos;
            teleportationTarget.Translate(movement, Space.World);

            float angle = Vector3.SignedAngle(head.forward, target.forward, Vector3.up);
            teleportationTarget.RotateAround(head.position, Vector3.up, angle);

            previewAvatar.Deactivate();
            EnableTeleportVisuals(false);
            teleportState = TeleportState.Idle;

            ExtendedLogger.LogInfo(GetType().Name, "Triggered Teleport.", this);
        }

        #endregion

        #region Rotation

        private void ApplyRotation()
        {
            // Rotation thumbstick is the OTHER hand
            InputAction rotAction = (navigationHand == HandType.Left) ? rightThumbstick.action : leftThumbstick.action;
            if (rotAction == null) return;

            Vector2 input = rotAction.ReadValue<Vector2>();

            if (rotationMode == RotationMode.Continuous)
                ApplyContinuousRotation(input);
            else
                ApplySnapRotation(input);

            lastFlipInput = input.y;
            lastRotInput = input.x;
        }

        private void ApplyContinuousRotation(Vector2 input)
        {
            if (rotationTarget == null || rotationReference == null) return;

            float angle = input.x * continuousRotationSpeed * Time.deltaTime;
            rotationTarget.RotateAround(rotationReference.position, Vector3.up, angle);
        }

        private void ApplySnapRotation(Vector2 input)
        {
            if (rotationTarget == null || rotationReference == null) return;

            // 180 flip on stick down (optional)
            if (enableDirectionFlip && lastFlipInput > -snapThreshold && input.y <= -snapThreshold)
                rotationTarget.RotateAround(rotationReference.position, Vector3.up, 180f);

            // Snap only on threshold crossing
            if (Mathf.Abs(lastRotInput) < snapThreshold && Mathf.Abs(input.x) >= snapThreshold)
            {
                float angle = input.x < 0 ? -snapRotationAmount : snapRotationAmount;
                rotationTarget.RotateAround(rotationReference.position, Vector3.up, angle);
            }
        }

        #endregion
    }
}
