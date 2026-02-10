using Unity.Netcode;
using UnityEngine;
using TMPro;

public class PlayerUIBinder : NetworkBehaviour
{
    public TMP_Text timeText;
    public TMP_Text scoreText;
    public TMP_Text endText;

    public TMP_Text bestText;          // optional
    public TMP_Text newHighScoreText;  // optional

    public GameObject newGameButtonGO; // drag the Button GameObject here

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;

        if (TrashGameManagerNet.Instance == null)
        {
            Debug.LogError("TrashGameManagerNet not found in scene!");
            return;
        }

        TrashGameManagerNet.Instance.RegisterUI(
            timeText,
            scoreText,
            endText,
            bestText,
            newHighScoreText,
            newGameButtonGO
        );
    }
}
