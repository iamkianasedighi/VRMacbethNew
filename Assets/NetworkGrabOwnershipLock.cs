using Unity.Netcode;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable))]
public class NetworkGrabOwnershipLock : NetworkBehaviour
{
    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable _grab;

    // Server-owned lock state
    private readonly NetworkVariable<bool> _isHeld =
        new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<ulong> _holderClientId =
        new NetworkVariable<ulong>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("DEBUG (Play Mode)")]
    [SerializeField] private bool debug_isSpawned;
    [SerializeField] private bool debug_isServer;
    [SerializeField] private bool debug_isHost;
    [SerializeField] private bool debug_isOwner;
    [SerializeField] private ulong debug_localClientId;

    [Space(6)]
    [SerializeField] private bool debug_isHeld;
    [SerializeField] private ulong debug_holderClientId;
    [SerializeField] private bool debug_isHeldBySomeoneElse;
    [SerializeField] private bool debug_canLocalPlayerGrab;

    [Space(6)]
    [SerializeField] private bool debug_xrIsSelected;
    [SerializeField] private string debug_firstInteractorName;

    private void Awake()
    {
        _grab = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        _grab.selectEntered.AddListener(OnSelectEntered);
        _grab.selectExited.AddListener(OnSelectExited);
    }

    public override void OnNetworkSpawn()
    {
        _isHeld.OnValueChanged += (_, __) => Refresh();
        _holderClientId.OnValueChanged += (_, __) => Refresh();
        Refresh();
    }

    private void Update()
    {
        // Update Inspector-visible debug state in Play Mode
        debug_isSpawned = IsSpawned;
        debug_isServer = IsServer;
        debug_isHost = IsHost;
        debug_isOwner = IsOwner;

        debug_localClientId = NetworkManager.Singleton != null ? NetworkManager.LocalClientId : 0;

        if (!IsSpawned)
            return;

        debug_isHeld = _isHeld.Value;
        debug_holderClientId = _holderClientId.Value;

        debug_isHeldBySomeoneElse = _isHeld.Value && _holderClientId.Value != debug_localClientId;
        debug_canLocalPlayerGrab = !_isHeld.Value || _holderClientId.Value == debug_localClientId;

        debug_xrIsSelected = _grab != null && _grab.isSelected;
        debug_firstInteractorName = (_grab != null && _grab.firstInteractorSelecting != null)
            ? _grab.firstInteractorSelecting.transform.name
            : "";
    }

    private void Refresh()
    {
        // Nothing required here for functionality; debug values update in Update().
    }

    private bool IsHeldBySomeoneElse()
    {
        return _isHeld.Value && _holderClientId.Value != NetworkManager.LocalClientId;
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        // If it's held by someone else, instantly cancel local grab
        if (IsHeldBySomeoneElse())
        {
            _grab.interactionManager.SelectExit(args.interactorObject, _grab);
            return;
        }

        // Ask server to lock + give ownership
        RequestGrabServerRpc(NetworkManager.LocalClientId);
    }

    private void OnSelectExited(SelectExitEventArgs args)
    {
        // Only the holder releases
        RequestReleaseServerRpc(NetworkManager.LocalClientId);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestGrabServerRpc(ulong requestingClientId)
    {
        // Deny if held by someone else
        if (_isHeld.Value && _holderClientId.Value != requestingClientId)
        {
            DenyGrabClientRpc(requestingClientId);
            return;
        }

        _isHeld.Value = true;
        _holderClientId.Value = requestingClientId;

        var netObj = GetComponent<NetworkObject>();
        if (netObj.OwnerClientId != requestingClientId)
            netObj.ChangeOwnership(requestingClientId);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestReleaseServerRpc(ulong requestingClientId)
    {
        if (!_isHeld.Value) return;
        if (_holderClientId.Value != requestingClientId) return;

        _isHeld.Value = false;
        _holderClientId.Value = 0;

        var netObj = GetComponent<NetworkObject>();
        if (netObj.OwnerClientId != NetworkManager.ServerClientId)
            netObj.ChangeOwnership(NetworkManager.ServerClientId);
    }

    [ClientRpc] //this is the old version of ClientRpc, now use Rpc(SendTo.xxx)
    private void DenyGrabClientRpc(ulong deniedClientId)
    {
        if (NetworkManager.LocalClientId != deniedClientId) return;

        if (_grab.isSelected && _grab.firstInteractorSelecting != null)
            _grab.interactionManager.SelectExit(_grab.firstInteractorSelecting, _grab);
    }
}
