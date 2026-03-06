using UnityEngine;
using UnityEngine.InputSystem;

public class DesktopNavigationLocal : MonoBehaviour
{
    public Transform target;

    public InputActionProperty moveAction;
    public InputActionProperty yawAction;
    public InputActionProperty pitchAction;

    public float translationVelocity = 3f;
    public float rotationVelocity = 30f;

    private Vector3 rotInput = Vector3.zero;

    private void Start()
    {
        if (target == null)
            target = transform;
    }

    private void Update()
    {
        Vector2 move = moveAction.action.ReadValue<Vector2>();
        Vector2 look = moveAction.action.activeControl != null ? Vector2.zero : Vector2.zero;

        float yaw = yawAction.action.ReadValue<float>();
        float pitch = pitchAction.action.ReadValue<float>();

        Vector3 translation = new Vector3(move.x, 0, move.y) * translationVelocity * Time.deltaTime;
        target.Translate(translation, Space.Self);

        rotInput.y += yaw * rotationVelocity * Time.deltaTime;
        rotInput.x -= pitch * rotationVelocity * Time.deltaTime;
        rotInput.x = Mathf.Clamp(rotInput.x, -80f, 80f);

        target.localRotation = Quaternion.Euler(rotInput.x, rotInput.y, 0f);
    }
}