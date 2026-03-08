using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class EnvironmentScoreAudio : MonoBehaviour
{
    [Header("References")]
    public TrashGameManagerNet gameManager;

    [Header("Volume Settings")]
    public float baseVolume = 0.08f;     // volume at score 0
    public float maxVolume = 0.22f;      // never goes above this
    public float volumePerPoint = 0.01f; // how much louder per score point

    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    private void Update()
    {
        if (audioSource == null) return;
        if (gameManager == null) return;

        int score = gameManager.TeamScore.Value;

        float targetVolume = baseVolume + (score * volumePerPoint);
        targetVolume = Mathf.Clamp(targetVolume, baseVolume, maxVolume);

        audioSource.volume = targetVolume;
    }
}