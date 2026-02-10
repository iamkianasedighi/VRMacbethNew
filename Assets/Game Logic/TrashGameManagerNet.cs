using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using TMPro;

public class TrashGameManagerNet : NetworkBehaviour
{
    public static TrashGameManagerNet Instance { get; private set; }

    [Header("Round Settings")]
    public float roundSeconds = 120f;

    [Header("UI (assigned at runtime by PlayerUIBinder)")]
    public TMP_Text timeText;
    public TMP_Text scoreText;
    public TMP_Text endText;
    public TMP_Text bestText;          // show only after end (Highest Score)
    public TMP_Text newHighScoreText;  // show only after end, only if new best
    public GameObject newGameButtonGO; // show only after end

    public NetworkVariable<int> TeamScore = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<float> TimeLeft = new(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<int> BestScore = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<bool> IsRunning = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<bool> NewHighScore = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private const string BestScoreKey = "TRASHGAME_BEST_SCORE";

    // Server-side: prevents double scoring per trash object
    private readonly HashSet<ulong> _scoredTrashIds = new HashSet<ulong>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        TeamScore.OnValueChanged += (_, __) => UpdateUI();
        TimeLeft.OnValueChanged += (_, __) => UpdateUI();
        BestScore.OnValueChanged += (_, __) => UpdateUI();
        IsRunning.OnValueChanged += (_, __) => UpdateUI();
        NewHighScore.OnValueChanged += (_, __) => UpdateUI();

        if (IsServer)
        {
            BestScore.Value = PlayerPrefs.GetInt(BestScoreKey, 0);
            StartRoundServer();
        }

        UpdateUI();
    }

    private void Update()
    {
        if (!IsServer) return;
        if (!IsRunning.Value) return;

        TimeLeft.Value -= Time.deltaTime;

        if (TimeLeft.Value <= 0f)
        {
            TimeLeft.Value = 0f;
            EndRoundServer();
        }
    }

    /// <summary>
    /// Called by PlayerUIBinder on LOCAL player after spawn.
    /// </summary>
    public void RegisterUI(
        TMP_Text time,
        TMP_Text score,
        TMP_Text end,
        TMP_Text best = null,
        TMP_Text newHigh = null,
        GameObject newGameBtn = null)
    {
        timeText = time;
        scoreText = score;
        endText = end;
        bestText = best;
        newHighScoreText = newHigh;
        newGameButtonGO = newGameBtn;

        // Initialize states (hide end-of-round stuff)
        if (endText)
        {
            endText.gameObject.SetActive(false);
            endText.text = "";
        }

        if (bestText)
        {
            bestText.gameObject.SetActive(false);
            bestText.text = "";
        }

        if (newHighScoreText)
        {
            newHighScoreText.gameObject.SetActive(false);
            newHighScoreText.text = "";
        }

        if (newGameButtonGO)
            newGameButtonGO.SetActive(false);

        UpdateUI();
        Debug.Log("TrashGameManagerNet: UI registered for local player.");
    }

    // Client/Owner calls this to ask server to score
    [ServerRpc(RequireOwnership = false)]
    public void TryScoreTrashServerRpc(ulong trashNetworkObjectId, TrashType binType)
    {
        if (!IsRunning.Value) return;

        if (_scoredTrashIds.Contains(trashNetworkObjectId))
            return;

        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(trashNetworkObjectId, out var netObj))
            return;

        var trash = netObj.GetComponent<TrashItemNet>();
        if (trash == null) return;

        _scoredTrashIds.Add(trashNetworkObjectId);

        if (trash.type == binType)
            TeamScore.Value += trash.points;

        netObj.Despawn(true);
    }

    // Called by New Game button
    [ServerRpc(RequireOwnership = false)]
    public void StartNewGameServerRpc()
    {
        StartRoundServer();
    }

    // Server starts round
    public void StartRoundServer()
    {
        if (!IsServer) return;

        _scoredTrashIds.Clear();
        TeamScore.Value = 0;
        TimeLeft.Value = roundSeconds;

        // Reset end-of-round flags
        NewHighScore.Value = false;

        IsRunning.Value = true;
    }

    private void EndRoundServer()
    {
        if (!IsServer) return;

        IsRunning.Value = false;

        int previousBest = BestScore.Value;

        if (TeamScore.Value > previousBest)
        {
            NewHighScore.Value = true;
            BestScore.Value = TeamScore.Value;

            PlayerPrefs.SetInt(BestScoreKey, BestScore.Value);
            PlayerPrefs.Save();
        }
        else
        {
            NewHighScore.Value = false;
        }
    }

    private void UpdateUI()
    {
        // UI exists only on local player
        if (timeText)
        {
            int seconds = Mathf.CeilToInt(TimeLeft.Value);
            int min = seconds / 60;
            int sec = seconds % 60;
            timeText.text = $"{min:0}:{sec:00}";
        }

        if (scoreText)
            scoreText.text = $"Score: {TeamScore.Value}";

        bool roundEnded = (!IsRunning.Value && TimeLeft.Value <= 0f);

        // "Time's up" only after end
        if (endText)
        {
            endText.gameObject.SetActive(roundEnded);
            if (roundEnded)
                endText.text = $"Time's up!\nScore: {TeamScore.Value}";
        }

        // ✅ Highest Score ALWAYS shown after end
        if (bestText)
        {
            bestText.gameObject.SetActive(roundEnded);
            if (roundEnded)
                bestText.text = $"Highest Score: {BestScore.Value}";
        }

        // ✅ NEW HIGH SCORE only if achieved (after end)
        if (newHighScoreText)
        {
            bool showNewHigh = roundEnded && NewHighScore.Value;
            newHighScoreText.gameObject.SetActive(showNewHigh);
            if (showNewHigh)
                newHighScoreText.text = "NEW HIGH SCORE!";
        }

        // New Game button only after end
        if (newGameButtonGO)
            newGameButtonGO.SetActive(roundEnded);
    }
}
