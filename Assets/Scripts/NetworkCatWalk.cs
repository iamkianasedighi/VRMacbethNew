using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(NetworkObject))]
public class NetworkCatWalk : NetworkBehaviour
{
    [Header("References")]
    [Tooltip("Optional. If null at runtime, the server will auto-find by Tree Tag/Name below.")]
    public Transform tree;

    [Tooltip("If tree is null, server tries GameObject.FindWithTag() using this tag.")]
    public string treeTag = "Tree";

    [Tooltip("Fallback: if tag search fails, server tries GameObject.Find() using this name.")]
    public string treeNameFallback = "Tree";

    [Header("Orbit Settings")]
    public float orbitSpeed = 1f;
    public float orbitRadius = 3f;

    [Header("Follow Settings")]
    public float followRadius = 5f;
    public float followSpeed = 2f;

    [Tooltip("How close the cat is allowed to get to the player (petting distance).")]
    public float stopDistance = 1.2f;

    [Header("Grounding (Fixes floating)")]
    [Tooltip("Only these layers count as ground. Set this to your Ground/Terrain layer(s).")]
    public LayerMask groundMask = ~0;

    [Tooltip("Ray starts this far above cat position.")]
    public float groundRayHeight = 5f;

    [Tooltip("Extra offset above hit point (use if cat pivot is below feet).")]
    public float groundSnapOffset = 0f;

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

    public override void OnNetworkSpawn()
    {
        // IMPORTANT: on a shared NPC, the SERVER drives movement.
        if (!IsServer) return;

        // Fix #1: scene reference often missing on server for network-spawned prefab
        ResolveTreeReference();

        // Optional: start angle based on current position for nicer orbit
        if (tree != null)
        {
            Vector3 flat = transform.position - tree.position;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.0001f)
            {
                angle = Mathf.Atan2(flat.z, flat.x);
            }
        }

        if (verboseLogs)
            Debug.Log($"[CAT][SERVER] Spawned. tree={(tree ? tree.name : "NULL")}");
    }

    void Update()
    {
        // Shared cat: server decides and moves. Clients receive via NetworkTransform.
        if (!IsServer) return;

        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            return;

        // If tree reference was missing at spawn (scene load order), try occasionally
        if (tree == null && Time.frameCount % 60 == 0)
            ResolveTreeReference();

        scanTimer -= Time.deltaTime;
        if (scanTimer <= 0f)
        {
            scanTimer = scanInterval;
            RefreshInRangePlayers();
            PickOrValidateTarget();

            if (verboseLogs && Time.frameCount % 60 == 0)
            {
                Debug.Log($"[CAT][SERVER] target={(currentTargetClientId.HasValue ? currentTargetClientId.Value.ToString() : "NONE")}, " +
                          $"inRange={inRangeSince.Count}, tree={(tree ? tree.name : "NULL")}");
            }
        }

        Transform target = GetTargetTransform(currentTargetClientId);

        if (target != null)
            FollowTarget(target);
        else if (tree != null)
            OrbitTree();
        else
        {
            // no tree + no target => just stay grounded
            SnapToGround();
            PlayIfNotAlready(sitStateName);
        }
    }

    // -------------------------------------------------------
    // Tree resolving (server-side)
    // -------------------------------------------------------
    void ResolveTreeReference()
    {
        if (tree != null) return;

        if (!string.IsNullOrEmpty(treeTag))
        {
            var byTag = GameObject.FindWithTag(treeTag);
            if (byTag != null) tree = byTag.transform;
        }

        if (tree == null && !string.IsNullOrEmpty(treeNameFallback))
        {
            var byName = GameObject.Find(treeNameFallback);
            if (byName != null) tree = byName.transform;
        }
    }

    // -------------------------------------------------------
    // FOLLOW TARGET RESOLUTION
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

            // If this logs NULL for everyone, your players are not set as PlayerObject
            if (playerObj == null)
            {
                if (verboseLogs && Time.frameCount % 120 == 0)
                    Debug.Log($"[CAT][SERVER] Client {clientId} PlayerObject is NULL. Cat cannot track them via ConnectedClients.PlayerObject.");
                continue;
            }

            Transform followT = GetFollowTransformFromPlayer(playerObj);
            if (followT == null) continue;

            Vector3 p = followT.position;
            p.y = 0f;

            float sqr = (p - catPos).sqrMagnitude;

            bool isInRange = sqr <= rSqr;
            bool alreadyTracked = inRangeSince.ContainsKey(clientId);

            if (isInRange && !alreadyTracked)
            {
                inRangeSince[clientId] = nm.ServerTime.Time;
                if (verboseLogs) Debug.Log($"[CAT][SERVER] Client {clientId} ENTER range (count={inRangeSince.Count})");
            }
            else if (!isInRange && alreadyTracked)
            {
                inRangeSince.Remove(clientId);
                if (verboseLogs) Debug.Log($"[CAT][SERVER] Client {clientId} LEAVE range (count={inRangeSince.Count})");

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
    // Follow behavior (server authoritative)
    // -------------------------------------------------------
    void FollowTarget(Transform target)
    {
        Vector3 catPos = transform.position;

        // Move only in XZ
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

        // Rotate toward target
        if (dir.sqrMagnitude > 0.0001f)
        {
            Quaternion lookRot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, Time.deltaTime * 5f);
        }

        // Fix #2: always snap to ground
        SnapToGround();
    }

    // -------------------------------------------------------
    // Orbit behavior (server authoritative)
    // -------------------------------------------------------
    void OrbitTree()
    {
        if (tree == null) return;

        angle += orbitSpeed * Time.deltaTime;

        float x = tree.position.x + Mathf.Cos(angle) * orbitRadius;
        float z = tree.position.z + Mathf.Sin(angle) * orbitRadius;

        // Set XZ, then snap Y to ground
        Vector3 p = new Vector3(x, transform.position.y, z);
        transform.position = p;

        // Face tangent direction
        Vector3 tangent = new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle));
        if (tangent.sqrMagnitude > 0.0001f)
        {
            Quaternion rot = Quaternion.LookRotation(tangent);
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, Time.deltaTime * 5f);
        }

        PlayIfNotAlready(walkStateName);

        // Fix #2: always snap to ground
        SnapToGround();
    }

    // -------------------------------------------------------
    // Ground snapping utility
    // -------------------------------------------------------
    void SnapToGround()
    {
        Vector3 pos = transform.position;
        Vector3 rayStart = pos + Vector3.up * groundRayHeight;

        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, groundRayHeight * 10f, groundMask, QueryTriggerInteraction.Ignore))
        {
            pos.y = hit.point.y + groundSnapOffset;
            transform.position = pos;
        }
    }

    void PlayIfNotAlready(string stateName)
    {
        if (anim == null || string.IsNullOrEmpty(stateName)) return;

        var st = anim.GetCurrentAnimatorStateInfo(0);
        if (!st.IsName(stateName))
            anim.Play(stateName);
    }
}