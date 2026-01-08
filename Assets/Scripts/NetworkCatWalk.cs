using UnityEngine;
using Unity.Netcode;

public class NetworkCatWalk : NetworkBehaviour
{
    [Header("References")]
    public Transform tree;

    [Header("Orbit Settings")]
    public float orbitSpeed = 1f;
    public float orbitRadius = 3f;
    public float heightOffset = 0f;

    [Header("Follow Settings")]
    public float followRadius = 5f;
    public float followSpeed = 2f;
    public float stopDistance = 1.2f;

    [Header("Animation")]
    public string walkStateName = "walk";
    public string sitStateName = "sit";

    float angle = 0f;
    Animator anim;

    void Awake()
    {
        anim = GetComponent<Animator>();
    }

    void Update()
    {
        // Server authority: only server decides and moves the cat
        if (!IsServer) return;

        // If Netcode isn't running yet, do nothing (prevents null refs)
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            return;

        Transform closestPlayer = GetClosestPlayerWithinRadius(followRadius);

        if (closestPlayer != null)
        {
            FollowPlayer(closestPlayer);
        }
        else if (tree != null)
        {
            OrbitTree();
        }
        // else: no tree + no players -> cat stays where it is
    }

    // ---------------------------------
    // PLAYER SELECTION (closest within radius)
    // ---------------------------------
    Transform GetClosestPlayerWithinRadius(float radius)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return null;

        var clients = nm.ConnectedClients;
        if (clients == null || clients.Count == 0) return null;

        Transform closest = null;
        float bestSqr = radius * radius;

        Vector3 catPos = transform.position;
        catPos.y = 0f;

        foreach (var kvp in clients)
        {
            var client = kvp.Value;
            var playerObj = client?.PlayerObject;
            if (playerObj == null) continue;

            Vector3 p = playerObj.transform.position;
            p.y = 0f;

            float sqr = (p - catPos).sqrMagnitude;
            if (sqr <= bestSqr)
            {
                bestSqr = sqr;
                closest = playerObj.transform;
            }
        }

        return closest;
    }

    // ---------------------------------
    // FOLLOW LOGIC
    // ---------------------------------
    void FollowPlayer(Transform player)
    {
        Vector3 catPos = transform.position;
        Vector3 targetPos = new Vector3(player.position.x, catPos.y, player.position.z);

        Vector3 toPlayer = targetPos - catPos;
        float distance = toPlayer.magnitude;

        Vector3 dir = (distance > 0.001f) ? toPlayer.normalized : transform.forward;

        if (distance > stopDistance)
        {
            // Move
            transform.position += dir * followSpeed * Time.deltaTime;
            PlayIfNotAlready(walkStateName);
        }
        else
        {
            PlayIfNotAlready(sitStateName);
        }

        // Rotate
        if (dir.sqrMagnitude > 0.0001f)
        {
            Quaternion lookRot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, Time.deltaTime * 5f);
        }
    }

    // ---------------------------------
    // ORBIT LOGIC
    // ---------------------------------
    void OrbitTree()
    {
        angle += orbitSpeed * Time.deltaTime;

        float x = tree.position.x + Mathf.Cos(angle) * orbitRadius;
        float z = tree.position.z + Mathf.Sin(angle) * orbitRadius;
        float y = tree.position.y + heightOffset;

        transform.position = new Vector3(x, y, z);

        Vector3 tangentDir = new Vector3(-Mathf.Sin(angle), 0, Mathf.Cos(angle));
        if (tangentDir.sqrMagnitude > 0.0001f)
        {
            Quaternion orbitRot = Quaternion.LookRotation(tangentDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, orbitRot, Time.deltaTime * 5f);
        }

        PlayIfNotAlready(walkStateName);
    }

    void PlayIfNotAlready(string stateName)
    {
        if (anim == null || string.IsNullOrEmpty(stateName)) return;

        var st = anim.GetCurrentAnimatorStateInfo(0);
        if (!st.IsName(stateName))
            anim.Play(stateName);
    }
}
