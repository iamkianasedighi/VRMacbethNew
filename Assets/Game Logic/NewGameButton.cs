using UnityEngine;

public class NewGameButton : MonoBehaviour
{
    public void NewGame()
    {
        if (TrashGameManagerNet.Instance == null) return;
        TrashGameManagerNet.Instance.StartNewGameServerRpc();
    }
}
