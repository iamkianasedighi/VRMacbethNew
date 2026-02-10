using UnityEngine;

public class ShowOnlyAfterGameEnds : MonoBehaviour
{
    [Tooltip("If true, panel only appears when timer hits 0 (game over).")]
    public bool onlyWhenEnded = true;

    private void Update()
    {
        if (TrashGameManagerNet.Instance == null) return;

        bool ended = !TrashGameManagerNet.Instance.IsRunning.Value
                     && TrashGameManagerNet.Instance.TimeLeft.Value <= 0f;

        bool shouldShow = onlyWhenEnded ? ended : !TrashGameManagerNet.Instance.IsRunning.Value;

        if (gameObject.activeSelf != shouldShow)
            gameObject.SetActive(shouldShow);
    }
}
