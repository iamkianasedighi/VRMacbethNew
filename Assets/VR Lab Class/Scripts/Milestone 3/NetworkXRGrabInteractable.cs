using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRLabClass.Milestone3
{
    public class NetworkXRGrabInteractable : XRGrabInteractable
    {
        #region Properties

        private GrabPolicy _grabPolicy;
        private bool _isLocallyGrabbed = false;

        #endregion

        #region MonoBehaviour Methods

        protected override void Awake()
        {
            _grabPolicy = GetComponent<GrabPolicy>();
            
            base.Awake();
        }

        #endregion

        #region XRInteractable Callbacks

        protected override void OnSelectEntered(SelectEnterEventArgs args)
        {
            if (_grabPolicy.RequestAccess())
            {
                _isLocallyGrabbed = true;
                base.OnSelectEntered(args);
            }
        }

        protected override void OnSelectExited(SelectExitEventArgs args)
        {
            if (_isLocallyGrabbed)
            {
                _isLocallyGrabbed = false;
                _grabPolicy.Release();
                base.OnSelectExited(args);
            }
        }

        #endregion
    }
}
