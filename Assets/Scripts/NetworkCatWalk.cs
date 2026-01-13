using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(NetworkObject))]
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

    [Tooltip("How close the cat is allowed to get to the player (petting distance).")]
    public float stopDistance = 1.2f;

    [Header("Animation")]
    public string walkStateName = "walk";
    public string sitStateName = "Sit"; // must match Animator state name exactly

    [Header("Selection")]
    [Tooltip("How often the server re-evaluates who is in range (seconds).")]
    public float scanInterval = 0.1f;

    [Header("Debug")]
    public bool verboseLogs = false;

    private float angle;
    private Animator anim;

    // clientId -> server time when they entered followRadius
    private readonly Dictionary<ulong, double> inRangeSince = new Dictionary<ulong, double>();

    // current target clientId (first-come among those in range)
    private ulong? currentTargetClientId = null;

    private float scanTimer = 0f;

    void Awake()
    {
        anim = GetComponent<Animator>();
    }

    void Update()
    {
        // Shared cat: server decides and moves. Clients receive via NetworkTransform.
        if (!IsServer) return;

        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            return;

        scanTimer -= Time.deltaTime;
        if (scanTimer <= 0f)
        {
            scanTimer = scanInterval;
            RefreshInRangePlayers();
            PickOrValidateTarget();

            if (verboseLogs && Time.frameCount % 60 == 0)
            {
                Debug.Log($"[CAT][SERVER] targetClientId={(currentTargetClientId.HasValue ? currentTargetClientId.Value.ToString() : "NONE")}, inRange={inRangeSince.Count}");
            }
        }

        Transform target = GetTargetTransform(currentTargetClientId);

        if (target != null)
        {
            FollowTarget(target);
        }
        else if (tree != null)
        {
            OrbitTree();
        }
        // else: no tree + no target => stays still
    }

    // -------------------------------------------------------
    // FOLLOW TARGET RESOLUTION (UPDATED LOGIC)
    // Uses CatFollowTargetRef.target if present, else fallback
    // -------------------------------------------------------
    Transform GetFollowTransformFromPlayer(NetworkObject playerObj)
    {
        if (playerObj == null) return null;

        // Prefer the configured follow target (VR-safe)
        var refComp = playerObj.GetComponent<CatFollowTargetRef>();
        if (refComp != null && refComp.target != null)
            return refComp.target;

        // Fallback: use root
        return playerObj.transform;
    }

    // -------------------------------------------------------
    // 1) Scan who is within followRadius and maintain enter times
    // -------------------------------------------------------
    void RefreshInRangePlayers()
    {
        var nm = NetworkManager.Singleton;
        var clients = nm.ConnectedClients;
        if (clients == null) return;

        // Clean up disconnected ids
        HashSet<ulong> connectedIds = new HashSet<ulong>(clients.Keys);

        List<ulong> toRemove = null;
        foreach (var kv in inRangeSince)
        {
            if (!connectedIds.Contains(kv.Key))
            {
                toRemove ??= new List<ulong>();
                toRemove.Add(kv.Key);
            }
        }

        if (toRemove != null)
        {
            foreach (var id in toRemove)
            {
                inRangeSince.Remove(id);
                if (currentTargetClientId.HasValue && currentTargetClientId.Value == id)
                    currentTargetClientId = null;
            }
        }

        Vector3 catPos = transform.position;
        catPos.y = 0f;
        float rSqr = followRadius * followRadius;

        foreach (var kvp in clients)
        {
            ulong clientId = kvp.Key;
            var playerObj = kvp.Value?.PlayerObject;
            if (playerObj == null) continue;

            Transform followT = GetFollowTransformFromPlayer(playerObj);
            if (followT == null) continue;

            Vector3 p = followT.position;
            p.y = 0f;

            float sqr = (p - catPos).sqrMagnitude;

            bool isInRange = sqr <= rSqr;
            bool alreadyTracked = inRangeSince.ContainsKey(clientId);

            if (isInRange && !alreadyTracked)
            {
                // Record first entry time
                inRangeSince[clientId] = nm.ServerTime.Time;

                if (verboseLogs)
                    Debug.Log($"[CAT][SERVER] Client {clientId} ENTERED range (count={inRangeSince.Count})");
            }
            else if (!isInRange && alreadyTracked)
            {
                // Remove when leaving range
                inRangeSince.Remove(clientId);

                if (verboseLogs)
                    Debug.Log($"[CAT][SERVER] Client {clientId} LEFT range (count={inRangeSince.Count})");

                // If current target left, clear so we can choose next
                if (currentTargetClientId.HasValue && currentTargetClientId.Value == clientId)
                    currentTargetClientId = null;
            }
        }
    }

    // -------------------------------------------------------
    // 2) Select target: keep current if still in range,
    // otherwise pick earliest-entered player in range
    // -------------------------------------------------------
    void PickOrValidateTarget()
    {
        if (currentTargetClientId.HasValue && inRangeSince.ContainsKey(currentTargetClientId.Value))
            return;

        currentTargetClientId = null;

        double bestTime = double.MaxValue;
        foreach (var kv in inRangeSince)
        {
            if (kv.Value < bestTime)
            {
                bestTime = kv.Value;
                currentTargetClientId = kv.Key;
            }
        }
    }

    // -------------------------------------------------------
    // Resolve target transform for a chosen clientId
    // -------------------------------------------------------
    Transform GetTargetTransform(ulong? clientId)
    {
        if (!clientId.HasValue) return null;

        var nm = NetworkManager.Singleton;
        if (nm == null) return null;

        if (!nm.ConnectedClients.TryGetValue(clientId.Value, out var client)) return null;

        var playerObj = client?.PlayerObject;
        if (playerObj == null) return null;

        return GetFollowTransformFromPlayer(playerObj);
    }

    // -------------------------------------------------------
    // Follow behavior
    // -------------------------------------------------------
    void FollowTarget(Transform target)
    {
        Vector3 catPos = transform.position;
        Vector3 targetPos = new Vector3(target.position.x, catPos.y, target.position.z);

        Vector3 to = targetPos - catPos;
        float dist = to.magnitude;

        Vector3 dir = (dist > 0.001f) ? (to / dist) : transform.forward;

        if (dist > stopDistance)
        {
            transform.position += dir * followSpeed * Time.deltaTime;
            PlayIfNotAlready(walkStateName);
        }
        else
        {
            PlayIfNotAlready(sitStateName);
        }

        if (dir.sqrMagnitude > 0.0001f)
        {
            Quaternion lookRot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, Time.deltaTime * 5f);
        }
    }

    // -------------------------------------------------------
    // Orbit behavior
    // -------------------------------------------------------
    void OrbitTree()
    {
        if (tree == null) return;

        angle += orbitSpeed * Time.deltaTime;

        float x = tree.position.x + Mathf.Cos(angle) * orbitRadius;
        float z = tree.position.z + Mathf.Sin(angle) * orbitRadius;
        float y = tree.position.y + heightOffset;

        transform.position = new Vector3(x, y, z);

        Vector3 tangent = new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle));
        if (tangent.sqrMagnitude > 0.0001f)
        {
            Quaternion rot = Quaternion.LookRotation(tangent);
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, Time.deltaTime * 5f);
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
