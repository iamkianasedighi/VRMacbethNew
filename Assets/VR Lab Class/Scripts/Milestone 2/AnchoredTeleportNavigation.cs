using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using VRSYS.Core.Logging;

namespace VRLabClass.Milestone2
{
    public class AnchoredTeleportNavigation : MonoBehaviour
    {
        #region MyRegion

        private enum TeleportHandedness
        {
            Left,
            Right
        }
        
        private enum TeleportState
        {
            Idle, // user is not giving input
            Aiming, // user is aiming for point of interest
            Locked // user is selecting position around point of interest
        }

        #endregion
        
        #region Properties

        // Teleport variables
        [Header("Teleport Configuration")] 
        [SerializeField] private TeleportHandedness _teleportHand = TeleportHandedness.Right; // Selection which hand should be used for teleportation
        [SerializeField] private InputActionProperty _leftHandTeleportAction; // Left trigger
        [SerializeField] private InputActionProperty _rightHandTeleportAction; // Right trigger
        [SerializeField] private Transform _leftHand; // Transform of Left hand
        [SerializeField] private Transform _rightHand; // Transform of right hand
        [SerializeField] private float _activationThreshold = .1f; // Minimum input that has to be given to start aiming process
        [SerializeField] private float _lockThreshold = .95f; // Input threshold above which the teleport is considered locked in
        [SerializeField] private LineRenderer _ray; // Line renderer for teleport ray visualisation
        [SerializeField] private float _idleRayLength = 1f; // Length of ray if no valid target is hit
        [SerializeField] private float _maxRaycastDistance = 100f; // Maximum distance for raycast
        [SerializeField] private LayerMask _groundLayers; // Layers which are considered as ground during teleport
        [SerializeField] private Transform _head; // User head transform
        [SerializeField] private TeleportPreview _preview; // Reference to teleport preview

        private TeleportState _state = TeleportState.Idle; // Current state of teleport interaction
        private Vector3 _hitPoint; // Position of raycast hit
        private Vector3 _anchorPosition; // Position of anchor (Where user will look at) --> set in aiming phase
        private Vector3 _previewAvatarPosition; // Position of preview avatar (Where user will be teleported to) --> set in locked phase

        #endregion

        #region MonoBehaviour Methods

        private void Start()
        {
            // Delete script if attached to remote component
            if(GetComponent<NetworkObject>() != null)
                if (!GetComponent<NetworkObject>().IsOwner)
                {
                    Destroy(this);
                    return;
                }
            
            // Enabling input actions
            _leftHandTeleportAction.action.Enable();
            _rightHandTeleportAction.action.Enable();

            // Making sure ray line renderer only expects 2 positions (start and end point)
            _ray.positionCount = 2;
        }

        private void Update()
        {
            EvaluateInput();
        }

        #endregion

        #region Teleport Methods

        // Reading user input and deciding on current interaction state
        private void EvaluateInput()
        {
            // Deciding which input action to use based on select hand
            InputAction teleportAction = _teleportHand == TeleportHandedness.Left
                ? _leftHandTeleportAction.action
                : _rightHandTeleportAction.action;

            if (teleportAction.WasReleasedThisFrame() && _state == TeleportState.Locked) // Button was released after locking anchor input
            {
                PerformTeleport(); // Executing teleport
                Reset(); // return to idle state
            }
            else
            {
                float input = teleportAction.ReadValue<float>(); // Read input value

                if (input < _activationThreshold && _state != TeleportState.Locked) // Idle state
                {
                    if (_state != TeleportState.Idle) // if wasn't in idle state before
                    {
                        Reset(); // Return to idle state
                    }
                }
                else if (input >= _activationThreshold && input < _lockThreshold && _state != TeleportState.Locked) // Aiming state
                {
                    if (_state != TeleportState.Aiming) // button just got slightly pressed
                    {
                        _ray.enabled = true; // Enable ray
                        _preview.ActivateAnchorVisuals(); // Enable anchor visual of preview
                        _state = TeleportState.Aiming; // Updating state
                    }

                    EvaluateAimingInput(); // Process input according to aiming state
                }
                else if (input >= _lockThreshold) // Locked state
                {
                    if (_state != TeleportState.Locked) // button just got fully pressed
                    {
                        _ray.enabled = true; // Enable ray
                        _preview.ActivatePreviewAvatarVisuals(); // Enable preview avatar and distance indicator preview
                        _state = TeleportState.Locked; // Updating state
                    }
                    
                    EvaluateLockedInput(); // Process input according to locked state
                }
            }
        }

        private void PerformTeleport()
        {
            Vector3 targetPos = _previewAvatarPosition; // determining target position (where preview avatar is placed) 
            Quaternion targetRotation = Quaternion.LookRotation(_anchorPosition - _previewAvatarPosition, Vector3.up); // determining target rotation, only considering rotation around worlds y-axis (user should look towards anchor)
            
            Matrix4x4 targetMatrix = Matrix4x4.TRS(targetPos, targetRotation, Vector3.one); // Creating target matrix with target position & rotation (assuming uniform target scale of 1)

            // Getting local head position and removing y-component
            Vector3 headPos = _head.localPosition;
            headPos.y = 0;

            // Getting local head rotation only considering rotation around y-axis 
            Vector3 headRotationAngles = _head.localRotation.eulerAngles;
            headRotationAngles = new Vector3(0, headRotationAngles.y, 0);
            Quaternion headRotation = Quaternion.Euler(headRotationAngles);
            
            Matrix4x4 headMatrix = Matrix4x4.TRS(headPos, headRotation, Vector3.one); // Creating head matrix with adjusted head position and rotation (assuming uniform target scale of 1)

            Matrix4x4 teleportResult = targetMatrix * headMatrix.inverse; // Calculating final teleport matrix considering head offset

            // Applying position and rotation to user
            transform.position = teleportResult.GetPosition();
            transform.rotation = teleportResult.rotation;
        }

        // Processing user input in aiming state
        private void EvaluateAimingInput()
        {
            PerformRaycast();

            // Updating anchor position & it's preview
            _anchorPosition = _hitPoint; 
            _preview.UpdateAnchorTransform(_anchorPosition);
        }

        // Processing user input in locked state
        private void EvaluateLockedInput()
        {
            PerformRaycast();

            // Updating preview avatar position & it's preview
            _previewAvatarPosition = _hitPoint;
            _preview.UpdatePreviewAvatarTransform(_previewAvatarPosition, _head.localPosition.y);
        }

        // Performing raycast to aim for anchor || preview position
        private void PerformRaycast()
        {
            Transform hand = _teleportHand == TeleportHandedness.Left ? _leftHand : _rightHand; // selecting hand transform based on selected teleportation hand

            if (Physics.Raycast(hand.position, hand.forward, out RaycastHit hit, _maxRaycastDistance, _groundLayers)) // Executing raycast
            {
                // if something valid got hit:
                // update hit point and ray
                _hitPoint = hit.point;
                UpdateRay(true, hand);
            }
            else
            {
                // if nothing valid got hit
                // update ray
                UpdateRay(false, hand);
            }
        }

        private void UpdateRay(bool useHitPoint, Transform hand)
        {
            _ray.SetPosition(0, hand.position); // set start position to hand position
            
            if (useHitPoint) 
            {
                // if raycast hit something valid
                _ray.SetPosition(1, _hitPoint); // ray ends at hit point
                
                // ray color to green
                _ray.startColor = Color.green; 
                _ray.endColor = Color.green;
            }
            else
            {
                // if raycast hit nothing valid
                _ray.SetPosition(1, hand.position + hand.forward * _idleRayLength); // ray ends along hand forward with idle ray length
                
                // ray color to red
                _ray.startColor = Color.red;
                _ray.endColor = Color.red;
            }
        }

        // Method to reset teleport to idle state
        private void Reset()
        {
            _ray.enabled = false; // Disable ray
            _preview.DeactivateVisuals(); // Disable all preview visuals
            _state = TeleportState.Idle; // Updating state
        }
        
        #endregion
    }
}
