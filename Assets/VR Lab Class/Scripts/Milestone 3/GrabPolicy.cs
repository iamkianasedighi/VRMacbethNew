using Unity.Netcode;
using UnityEngine;

namespace VRLabClass.Milestone3
{
    public class GrabPolicy : MonoBehaviour
    {
        #region Properties

        private NetworkVariable<bool> _isGrabbed = new NetworkVariable<bool>(false,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        #endregion

        #region Policy Methods

        public bool RequestAccess()
        {
            // HERE: Implementations for 3.4
            // if _isGrabbed --> return false
            // if !_isGrabbed --> give ownership to local user, set _isGrabbed to true, return true
            
            return true;
        }

        public void Release()
        {
            // HERE: Implementations for 3.4
            // update _isGrabbed to false
        }

        #endregion

        #region RPCs

        // HERE: Implementations for 3.4
        // implement a RPC to update _isGrabbed
        // implement a RPC to change ownership

        #endregion
    }
}
