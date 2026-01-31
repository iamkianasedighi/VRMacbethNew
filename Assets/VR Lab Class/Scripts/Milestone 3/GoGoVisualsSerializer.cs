using Unity.Netcode;
using UnityEngine;

namespace VRLabClass.Milestone3
{
    public class GoGoVisualsSerializer : NetworkBehaviour
    {
        #region Properties

        private NetworkVariable<bool> _isGogoHandActive = new NetworkVariable<bool>(false,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        [SerializeField] private GameObject _gogoVisual;

        #endregion

        #region MonoBehaviour Callbacks

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
                _isGogoHandActive.Value = _gogoVisual.activeSelf;
            else
            {
                _gogoVisual.SetActive(_isGogoHandActive.Value);
                _isGogoHandActive.OnValueChanged += OnGogoHandActiveChanged;
            }
        }

        private void Update()
        {
            if (IsOwner)
            {
                if (_isGogoHandActive.Value != _gogoVisual.activeSelf)
                    _isGogoHandActive.Value = _gogoVisual.activeSelf;
            }
        }

        #endregion

        #region On Value Changed Events

        private void OnGogoHandActiveChanged(bool previousValue, bool newValue) => _gogoVisual.SetActive(newValue);

        #endregion
    }
}
