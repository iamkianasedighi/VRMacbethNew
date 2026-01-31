using Unity.Netcode;
using UnityEngine;

namespace VRLabClass.Milestone2
{
    public class TeleportPreviewSerializer : NetworkBehaviour
    {
        #region Properties

        [Header("Serialized Visuals")] 
        [SerializeField] private GameObject _anchorVisuals;
        [SerializeField] private GameObject _previewAvatarVisuals;
        [SerializeField] private GameObject _distanceIndicatorVisuals;
        
        // Network Variables
        
        // used to serialize active state of anchor
        private NetworkVariable<bool> _aimingVisualsActive = new NetworkVariable<bool>(false,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner); 

        // used to serialize active state of preview avatar and distance indicator (both are only active at the same time)
        private NetworkVariable<bool> _lockedVisualsActive = new NetworkVariable<bool>(false,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        #endregion

        #region Mono- & NetworkBehavior Methods

        public override void OnNetworkSpawn()
        {
            if (!IsOwner) // if we are not the owner --> set current active state and subscribe to change events
            {
                _anchorVisuals.SetActive(_aimingVisualsActive.Value);
                _aimingVisualsActive.OnValueChanged += OnAimingVisualsActiveChanged;
                
                _previewAvatarVisuals.SetActive(_lockedVisualsActive.Value);
                _distanceIndicatorVisuals.SetActive(_lockedVisualsActive.Value);
                _lockedVisualsActive.OnValueChanged += OnLockedVisualsActiveChanged;
            }
        }

        private void Update()
        {
            if (IsOwner) // if we are the owner --> update network variables, if active states have changed
            {
                if (_anchorVisuals.activeSelf != _aimingVisualsActive.Value)
                    _aimingVisualsActive.Value = _anchorVisuals.activeSelf;

                if (_previewAvatarVisuals.activeSelf != _lockedVisualsActive.Value)
                    _lockedVisualsActive.Value = _previewAvatarVisuals.activeSelf;
            }
        }

        #endregion

        #region OnValueChanged Callbacks

        // Method to update active state on change for anchor
        private void OnAimingVisualsActiveChanged(bool previousValue, bool newValue) =>
            _anchorVisuals.SetActive(newValue);
        
        // Method to update active state on change for preview avatar and distance indicator
        private void OnLockedVisualsActiveChanged(bool previousValue, bool newValue)
        {
            _previewAvatarVisuals.SetActive(newValue);
            _distanceIndicatorVisuals.SetActive(newValue);
        }

        #endregion
    }
}
