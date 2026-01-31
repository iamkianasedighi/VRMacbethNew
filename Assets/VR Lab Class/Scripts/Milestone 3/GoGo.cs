using Unity.Netcode;
using UnityEngine;

namespace VRLabClass.Milestone3
{
    public class GoGo : MonoBehaviour
    {
        #region Properties

        [Header("Calculation Origin Configuration")]
        [SerializeField] private Transform _head;                 // Transform of user head --> used for origin calculation
        [SerializeField] private float _bodyCenterHeadOffset = .2f; // Vertical offset used to determine body center below users head

        private Vector3 _bodyCenter // returns position of body center used for calculation
        {
            get
            {
                Vector3 v = _head.position;
                v.y -= _bodyCenterHeadOffset;
                return v;
            }
        }

        [Header("GoGo Configuration")]
        [SerializeField] private Transform _hand;         // Transform of users real hand
        [SerializeField] private Transform _gogoHand;     // Hand transform to apply GoGo movement to
        [SerializeField] private GameObject _gogoVisual;  // Visual enabled when GoGo exceeds 1:1 threshold

        [SerializeField, Range(0f, 1f)] private float _k = .3f;                // value k in gogo equation (cm-based)
        [SerializeField, Range(0f, 1f)] private float _distanceThreshold = .30f; // value D in meters (lowered default to trigger earlier)

        #endregion

        #region MonoBehaviour Methods

        private void Start()
        {
            // Delete component if attached to remote users avatar
            var netObj = GetComponentInParent<NetworkObject>();
            if (netObj != null && !netObj.IsOwner)
            {
                Destroy(this);
                return;
            }

            if (_hand == null || _gogoHand == null || _head == null)
            {
                Debug.LogWarning($"{nameof(GoGo)} missing references on {gameObject.name}. Disabling.");
                enabled = false;
                return;
            }

            // set gogo hand to initial position and rotation, aligned with real hand
            _gogoHand.position = _hand.position;
            _gogoHand.rotation = _hand.rotation;

            // initially deactivate visuals
            if (_gogoVisual != null)
                _gogoVisual.SetActive(false);
        }

        private void Update()
        {
            ApplyGoGo();
        }

        #endregion

        #region GoGo Methods

        private void ApplyGoGo()
        {
            Vector3 bodyCenter = _bodyCenter;

            // Vector from body center to the real hand
            Vector3 bodyToHand = _hand.position - bodyCenter;
            float d_m = bodyToHand.magnitude; // real distance in meters

            // Decide whether GoGo mapping is active
            bool gogoActive = d_m > _distanceThreshold;

            // Activate/deactivate GoGo visual
            if (_gogoVisual != null)
                _gogoVisual.SetActive(gogoActive);

            // Keep rotation isomorphic (same as real hand)
            _gogoHand.rotation = _hand.rotation;

            // If within threshold (or too close), keep 1:1 mapping
            if (!gogoActive || d_m < 1e-6f)
            {
                _gogoHand.position = _hand.position;
                return;
            }

            // --- GoGo mapping (equation assumes cm; Unity uses meters) ---
            float d_cm = d_m * 100f;
            float D_cm = _distanceThreshold * 100f;

            float delta_cm = d_cm - D_cm;

            // dv = d + k * (d - D)^2  (all in cm)
            float dv_cm = d_cm + (_k * delta_cm * delta_cm);

            // Convert back to meters for Unity world coordinates
            float dv_m = dv_cm / 100f;

            // Apply along the direction from body center to hand
            Vector3 dir = bodyToHand / d_m; // normalized
            _gogoHand.position = bodyCenter + dir * dv_m;
        }

        #endregion
    }
}
