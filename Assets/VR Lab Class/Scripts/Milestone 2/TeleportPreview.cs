using Unity.Netcode;
using UnityEngine;

namespace VRLabClass.Milestone2
{
    public class TeleportPreview : MonoBehaviour
    {
        #region Properties

        // Preview variables
        [Header("Preview Configuration")]
        [SerializeField] private Transform _anchorTransform; // anchor transform node that is used to position anchor preview
        [SerializeField] private GameObject _anchorVisuals; // anchor visuals node that is enabled/disabled to show/hide visuals
        [SerializeField] private Transform _previewAvatarTransform; // preview avatar transform node that is used to position preview avatar
        [SerializeField] private GameObject _previewAvatarVisuals; // preview avatar visuals node that is enabled/disabled to show/hide avatar visuals
        [SerializeField] private Transform _distanceIndicatorTransform; // distance indicator transform node that is used to position/scale distance indicator
        [SerializeField] private GameObject _distanceIndicatorVisuals; // distance indicator visuals node that is enabled/disabled to show/hide distance indicator

        #endregion

        #region MonoBehaviour Methods

        private void Start()
        {
            // Destroy component if attached to remote avatar
            if(GetComponentInParent<NetworkObject>() != null)
                if (!GetComponentInParent<NetworkObject>().IsOwner)
                {
                    Destroy(this);
                    return;
                }
            
            DeactivateVisuals();
        }

        #endregion

        #region Custom Methods

        // Activate only anchor visual (Aiming state)
        public void ActivateAnchorVisuals() => _anchorVisuals.SetActive(true);

        // Update anchor position (Aiming state)
        public void UpdateAnchorTransform(Vector3 hitPoint) => _anchorTransform.position = hitPoint;

        // Activating preview avatar and distance indicator (Locked state)
        public void ActivatePreviewAvatarVisuals()
        {
            _previewAvatarVisuals.SetActive(true);
            _distanceIndicatorVisuals.SetActive(true);
        }

        public void UpdatePreviewAvatarTransform(Vector3 hitPoint, float userHeight)
        {
            _previewAvatarTransform.position = hitPoint; // update avatar position in world coordinates
            
            // adjust avatar rotation
            Vector3 forwardLookDirection = _anchorTransform.position - _previewAvatarTransform.position;
            _previewAvatarTransform.rotation = Quaternion.LookRotation(forwardLookDirection, Vector3.up);
            
            // adjust avatar height in local coordinates
            Vector3 localPos = _previewAvatarTransform.localPosition;
            localPos.y = userHeight;
            _previewAvatarTransform.localPosition = localPos;
            
            UpdateDistanceIndicator();
        }

        private void UpdateDistanceIndicator()
        {
            // get distance between anchor and preview avatar in x-z-plane
            Vector3 anchorPos = _anchorTransform.position;
            anchorPos.y = 0;

            Vector3 avatarPos = _previewAvatarTransform.position;
            avatarPos.y = 0;

            // adjust distance indicator scale along x- and z-axis
            float scale = 2 * Vector3.Distance(anchorPos, avatarPos);
            _distanceIndicatorTransform.localScale =
                new Vector3(scale, _distanceIndicatorTransform.localScale.y, scale);
        }

        // Deactivating all visuals (Idle state)
        public void DeactivateVisuals()
        {
            _anchorVisuals.SetActive(false);
            _previewAvatarVisuals.SetActive(false);
            _distanceIndicatorVisuals.SetActive(false);
        }

        #endregion
    }
}
