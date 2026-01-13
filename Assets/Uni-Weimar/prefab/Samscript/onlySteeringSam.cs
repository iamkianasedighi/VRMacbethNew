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
        [Tooltip("LEFT stick Vector2 action (bind ONLY LeftHand primary2DAxis).")]
        public InputActionProperty moveAction;

        [Tooltip("RIGHT stick Vector2 action (bind ONLY RightHand primary2DAxis).")]
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

        private NetworkObject netObj;
        private bool initialized = false;

        private Transform rigRoot;
        private Transform head;
        private Transform leftHand;
        private Transform rightHand;

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

            initialized = true;
        }

        private Transform ForwardIndicator =>
            steeringDirection == SteeringDirection.Head ? head : leftHand; // steering uses LEFT hand reference

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
            if (input.sqrMagnitude < moveDeadzone * moveDeadzone) return;

            Vector3 moveDir = StickToWorldDirection(input);
            float scaleFactor = steeringTarget.localScale.x;

            steeringTarget.position += moveDir * (steeringSpeed * input.magnitude * Time.deltaTime) * scaleFactor;
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
