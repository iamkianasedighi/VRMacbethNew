using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using VRSYS.Core.Avatar;
using VRSYS.Core.Logging;
using VRSYS.Core.Utility;

namespace VRSYS.Core.Navigation
{
    public class ThumbstickSteeringOnly : MonoBehaviour
    {
        public enum SteeringDirection { Head, Hand }
        public enum RotationMode { Continuous, Snap }

        [Header("Network")]
        public bool ownerOnly = true;

        [Header("Input Actions (Vector2)")]
        public InputActionProperty moveAction;
        public InputActionProperty turnAction;

        [Header("Steering")]
        public Transform steeringTarget;
        public SteeringDirection steeringDirection = SteeringDirection.Hand;
        [Range(0, 10)] public float steeringSpeed = 3f;
        public bool verticalSteering = false;

        [Header("Rotation")]
        public Transform rotationTarget;
        public Transform rotationReference;
        public RotationMode rotationMode = RotationMode.Continuous;
        [Range(0, 360)] public float continuousRotationSpeed = 180f;
        [Range(0, 180)] public float snapRotationAmount = 30f;

        [Header("CharacterController")]
        public bool useGravity = true;
        public float gravity = -9.81f;

        private NetworkObject netObj;
        private bool initialized = false;

        private Transform rigRoot;
        private Transform head;
        private Transform leftHand;
        private Transform rightHand;

        private CharacterController cc;
        private Vector3 verticalVelocity;

        private const float moveDeadzone = 0.1f;
        private const float snapThreshold = 0.9f;
        private float lastRotInput = 0f;

        private void Awake()
        {
            netObj = GetComponentInParent<NetworkObject>();
        }

        private void Start()
        {
            if (ownerOnly && netObj != null && !netObj.IsOwner)
            {
                Destroy(this);
                return;
            }

            Initialize();
        }

        private void OnEnable()
        {
            moveAction.action?.Enable();
            turnAction.action?.Enable();
        }

        private void OnDisable()
        {
            moveAction.action?.Disable();
            turnAction.action?.Disable();
        }

        private void Update()
        {
            if (!initialized)
            {
                Initialize();
                return;
            }

            ApplySteering();
            ApplyRotation();
        }

        private void Initialize()
        {
            AvatarHMDAnatomy anatomy = GetComponentInParent<AvatarHMDAnatomy>();
            if (anatomy == null)
            {
                ExtendedLogger.LogError(GetType().Name,
                    "AvatarHMDAnatomy not found in parents.", this);
                return;
            }

            head = anatomy.head;
            leftHand = anatomy.leftHand;
            rightHand = anatomy.rightHand;

            if (netObj == null) netObj = GetComponentInParent<NetworkObject>();
            rigRoot = (netObj != null) ? netObj.transform : transform.root;

            if (steeringTarget == null) steeringTarget = rigRoot;
            if (rotationTarget == null) rotationTarget = rigRoot;
            if (rotationReference == null) rotationReference = head;

            // IMPORTANT: CharacterController must be on the same object as steeringTarget (rigRoot).
            cc = steeringTarget.GetComponent<CharacterController>();
            if (cc == null)
            {
                ExtendedLogger.LogError(GetType().Name,
                    "No CharacterController found on steeringTarget. Add a CharacterController to the rig root (the object being moved).", this);
                // We can still run, but collisions will be bypassed if we fall back to position +=
            }

            initialized = true;
        }

        private Transform ForwardIndicator =>
            steeringDirection == SteeringDirection.Head ? head : leftHand;

        private Vector3 ForwardDirection
        {
            get
            {
                Vector3 dir = steeringDirection == SteeringDirection.Head ? head.forward : leftHand.forward;
                if (!verticalSteering) dir.y = 0f;
                return dir.sqrMagnitude < 0.0001f ? Vector3.forward : dir.normalized;
            }
        }

        private void ApplySteering()
        {
            if (moveAction.action == null) return;

            Vector2 input = moveAction.action.ReadValue<Vector2>();
            if (input.sqrMagnitude < moveDeadzone * moveDeadzone)
            {
                // still apply gravity even when not moving
                ApplyGravityAndMove(Vector3.zero);
                return;
            }

            Vector3 moveDir = StickToWorldDirection(input);

            float scaleFactor = steeringTarget.localScale.x;
            Vector3 horizontal = moveDir * (steeringSpeed * input.magnitude * scaleFactor);

            ApplyGravityAndMove(horizontal);
        }

        private void ApplyGravityAndMove(Vector3 horizontalVelocity)
        {
            float dt = Time.deltaTime;

            if (cc != null)
            {
                if (useGravity)
                {
                    if (cc.isGrounded && verticalVelocity.y < 0f)
                        verticalVelocity.y = -1f; // small stick-to-ground

                    verticalVelocity.y += gravity * dt;
                }
                else
                {
                    verticalVelocity = Vector3.zero;
                }

                Vector3 motion = (horizontalVelocity + verticalVelocity) * dt;
                cc.Move(motion);
            }
            else
            {
                // Fallback (NOT recommended): will still clip through terrain
                steeringTarget.position += horizontalVelocity * dt;
            }
        }

        private Vector3 StickToWorldDirection(Vector2 input)
        {
            float angle = Vector2.SignedAngle(Vector2.up, input);
            Vector3 axis = verticalSteering ? ForwardIndicator.up : Vector3.up;

            Vector3 dir = Quaternion.AngleAxis(angle, axis) * ForwardDirection;
            if (!verticalSteering) dir.y = 0f;

            return dir.sqrMagnitude < 0.0001f ? Vector3.zero : dir.normalized;
        }

        private void ApplyRotation()
        {
            if (turnAction.action == null) return;

            Vector2 input = turnAction.action.ReadValue<Vector2>();

            if (rotationMode == RotationMode.Continuous)
            {
                float angle = input.x * continuousRotationSpeed * Time.deltaTime;
                rotationTarget.RotateAround(rotationReference.position, Vector3.up, angle);
            }
            else
            {
                if (Mathf.Abs(lastRotInput) < snapThreshold && Mathf.Abs(input.x) >= snapThreshold)
                {
                    float angle = input.x < 0 ? -snapRotationAmount : snapRotationAmount;
                    rotationTarget.RotateAround(rotationReference.position, Vector3.up, angle);
                }
                lastRotInput = input.x;
            }
        }
    }
}
