using UnityEngine;

public class CatWalk : MonoBehaviour
{
    [Header("Camera References")]
    public Transform outsideCamera;     // assign in Inspector
    public Transform lobbyCamera;       // assign in Inspector

    [Header("References")]
    public Transform tree;

    [Header("Orbit Settings")]
    public float orbitSpeed = 1f;
    public float orbitRadius = 3f;
    public float heightOffset = 0f;

    [Header("Follow Settings")]
    public float followRadius = 5f;
    public float followSpeed = 2f;

    [Tooltip("How close the cat is allowed to get to the player (petting distance).")]
    public float stopDistance = 1.2f;

    private float angle = 0f;
    private Animator anim;

    void Start()
    {
        anim = GetComponent<Animator>();
    }

    Transform GetActiveCamera()
    {
        // Priority 1: Lobby camera (if player is in lobby)
        if (lobbyCamera != null && lobbyCamera.gameObject.activeInHierarchy)
            return lobbyCamera;

        // Priority 2: Outside camera (if player is outside lobby)
        if (outsideCamera != null && outsideCamera.gameObject.activeInHierarchy)
            return outsideCamera;

        // Priority 3: Fallback
        if (Camera.main != null)
            return Camera.main.transform;

        return null;
    }

    void Update()
    {
        Transform playerHead = GetActiveCamera();
        if (playerHead == null || tree == null) return;

        Vector3 catPos = transform.position;
        Vector3 playerPosFlat = new Vector3(playerHead.position.x, catPos.y, playerHead.position.z);
        float distanceToPlayer = Vector3.Distance(catPos, playerPosFlat);

        // FOLLOW PLAYER (but stop at stopDistance)
        if (distanceToPlayer <= followRadius)
        {
            Vector3 toPlayer = (playerPosFlat - catPos);
            float distance = toPlayer.magnitude;

            Vector3 dir = (distance > 0.0001f) ? (toPlayer / distance) : transform.forward;

            // Only move if we are farther than stopDistance
            if (distance > stopDistance)
            {
                transform.position += dir * followSpeed * Time.deltaTime;

                if (anim != null && !anim.GetCurrentAnimatorStateInfo(0).IsName("walk"))
                    anim.Play("walk");
            }
            else
            {
                // Close enough -> stop walking (optional sit)
                if (anim != null && !anim.GetCurrentAnimatorStateInfo(0).IsName("Sit"))
                    anim.Play("Sit");
            }

            // Always face the player
            Quaternion lookRot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, Time.deltaTime * 5f);

            return;
        }

        // Otherwise ORBIT TREE
        if (anim == null || anim.GetCurrentAnimatorStateInfo(0).IsName("walk"))
        {
            angle += orbitSpeed * Time.deltaTime;

            float x = tree.position.x + Mathf.Cos(angle) * orbitRadius;
            float z = tree.position.z + Mathf.Sin(angle) * orbitRadius;
            float y = tree.position.y + heightOffset;

            transform.position = new Vector3(x, y, z);

            Vector3 tangentDir = new Vector3(-Mathf.Sin(angle), 0, Mathf.Cos(angle));
            Quaternion orbitRot = Quaternion.LookRotation(tangentDir);

            transform.rotation = Quaternion.Slerp(transform.rotation, orbitRot, Time.deltaTime * 5f);
        }
    }
}
