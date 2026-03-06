using ParrelSync;
using UnityEngine;
using VRSYS.Core.Networking;

public class RoleAutoSetter : MonoBehaviour
{
    public NetworkUserSpawnInfo userSpawnInfo;
    public UserRole hmdRole;
    public UserRole desktopRole;

    private void Awake()
    {
        if (userSpawnInfo == null)
        {
            Debug.LogError("RoleAutoSetter: userSpawnInfo is missing.");
            return;
        }

        if (ClonesManager.IsClone())
        {
            userSpawnInfo.SetUserRole(desktopRole);
            userSpawnInfo.SetUserName("DesktopUser");
            Debug.Log("RoleAutoSetter: Clone detected -> Desktop role");
        }
        else
        {
            userSpawnInfo.SetUserRole(hmdRole);
            userSpawnInfo.SetUserName("VRUser");
            Debug.Log("RoleAutoSetter: Main editor -> HMD role");
        }
    }
}