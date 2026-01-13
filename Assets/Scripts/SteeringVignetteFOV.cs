using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class SteeringVignetteFOV : MonoBehaviour
{
    [Header("Local Only")]
    public bool ownerOnly = true;

    [Header("UI Reference")]
    public Image vignetteImage;           // Drag VignetteImage here

    [Header("Measure Movement From")]
    public Transform rigRootToMeasure;    // Drag player root / XR Origin here

    [Header("Movement Detection")]
    public float startMoveThreshold = 0.08f; // increase if head jitter triggers it
    public float stopMoveThreshold = 0.04f;
    public float fadeSpeed = 8f;

    [Header("Speed → Vignette")]
    public float maxSpeed = 3f;
    [Range(0f, 1f)] public float maxAlpha = 0.6f;

    private NetworkObject netObj;
    private Vector3 lastPos;
    private bool moving;
    private float currentAlpha;

    private void Awake()
    {
        netObj = GetComponentInParent<NetworkObject>();
    }

    private void Start()
    {
        // Only show for local player
        if (ownerOnly && netObj != null && !netObj.IsOwner)
        {
            gameObject.SetActive(false);
            return;
        }

        if (vignetteImage == null)
        {
            Debug.LogError("[SteeringVignetteUI] vignetteImage not assigned.");
            enabled = false;
            return;
        }

        if (rigRootToMeasure == null)
            rigRootToMeasure = transform;

        lastPos = rigRootToMeasure.position;

        SetAlpha(0f);
    }

    private void Update()
    {
        // Speed (m/s) based on rig movement (steering)
        Vector3 pos = rigRootToMeasure.position;
        float speed = (pos - lastPos).magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
        lastPos = pos;

        // Movement on/off with hysteresis
        if (!moving && speed >= startMoveThreshold) moving = true;
        else if (moving && speed <= stopMoveThreshold) moving = false;

        float targetAlpha = 0f;

        // Only show vignette when moving (steering)
        if (moving)
        {
            float t = Mathf.Clamp01(speed / Mathf.Max(maxSpeed, 0.01f));
            targetAlpha = Mathf.Lerp(0f, maxAlpha, t);
        }

        // Smooth fade
        currentAlpha = Mathf.Lerp(currentAlpha, targetAlpha, Time.deltaTime * fadeSpeed);
        SetAlpha(currentAlpha);
    }

    private void SetAlpha(float a)
    {
        Color c = vignetteImage.color;
        c.a = a;
        vignetteImage.color = c;
    }
}
