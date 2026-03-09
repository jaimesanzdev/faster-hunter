using Unity.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Server-authoritative manager for all game round logic.
/// Handles round generation, shape spawning, player selection validation, scoring,
/// and round transitions. All game state is replicated to clients via NetworkVariables.
/// </summary>
public class GameRoundManager : NetworkBehaviour
{
    /// <summary>Global singleton reference accessible from any script.</summary>
    public static GameRoundManager Instance { get; private set; }

    [Header("Prefabs")]
    /// <summary>The networked shape prefab used to spawn shape entities in the scene.</summary>
    public NetworkObject ShapePrefab;

    [Header("Spawn Area")]
    /// <summary>Center point of the rectangular area where shapes are spawned.</summary>
    public Vector2 SpawnAreaCenter = Vector2.zero;

    /// <summary>Width and height of the spawn area.</summary>
    public Vector2 SpawnAreaSize = new Vector2(10f, 6f);

    [Header("Difficulty")]
    /// <summary>Number of shapes spawned in the first round.</summary>
    public int InitialTotalObjects = 12;

    /// <summary>Maximum number of shapes that can be spawned regardless of round index.</summary>
    public int MaxTotalObjects = 40;

    [Header("Players")]
    /// <summary>Maximum number of players supported. Used to initialize the scores list.</summary>
    public int MaxPlayers = 4;

    [Header("Selection")]
    /// <summary>Radius of the overlap circle used to detect shape clicks on the server. Recommended range: 0.08–0.14.</summary>
    public float SelectionRadius = 0.12f;

    /// <summary>Optional offset applied to the selection point. Leave at zero for standard mouse input.</summary>
    public Vector2 SelectionOffset = Vector2.zero;

    /// <summary>LayerMask used to filter Physics2D overlap queries to only hit shape colliders.</summary>
    public LayerMask ShapesMask;

    // --- Networked state (replicated to all clients for UI) ---

    /// <summary>Per-player scores, indexed by client ID. Replicated to all clients.</summary>
    public NetworkList<int> Scores;

    /// <summary>Current round number, incremented at the start of each round.</summary>
    public NetworkVariable<int> RoundIndex = new NetworkVariable<int>(0);

    /// <summary>Number of target shapes still remaining in the current round.</summary>
    public NetworkVariable<int> TargetsRemaining = new NetworkVariable<int>(0);

    /// <summary>Human-readable rule text describing what players must find this round.</summary>
    public NetworkVariable<FixedString128Bytes> RuleText = new NetworkVariable<FixedString128Bytes>("");

    // --- Server-only round state ---

    /// <summary>The rule type active for the current round.</summary>
    RuleKind _ruleKind;

    /// <summary>Total number of target shapes to find this round.</summary>
    int _targetCount;

    /// <summary>Target shape type index, or -1 if shape is not part of the rule.</summary>
    int _targetShape;

    /// <summary>Target color index, or -1 if color is not part of the rule.</summary>
    int _targetColor;

    /// <summary>Number of correct captures made so far this round. Used for diminishing score rewards.</summary>
    int _capturesThisRound;

    /// <summary>List of all currently spawned shape NetworkObjects. Used for cleanup between rounds.</summary>
    readonly List<NetworkObject> _activeShapes = new();

    /// <summary>
    /// Initializes the singleton reference and creates the networked scores list.
    /// NetworkList must be instantiated before OnNetworkSpawn.
    /// </summary>
    void Awake()
    {
        Instance = this;
        Scores = new NetworkList<int>();
    }

    /// <summary>
    /// Called when the object is spawned on the network.
    /// Server-only: initializes sprite/collider caches, sets up player scores, and starts the first round.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        // Initialize sprite and collider caches once at game start
        ShapeSpriteCache.Initialize();
        ShapeColliderCache.Initialize();

        // Initialize score slots for all potential players
        Scores.Clear();
        for (int i = 0; i < MaxPlayers; i++) Scores.Add(0);

        StartNewRound();
    }

    /// <summary>
    /// Starts a new round: increments the round index, determines a random rule,
    /// spawns the correct target shapes, and fills the rest with non-matching distractors.
    /// Server-only.
    /// </summary>
    void StartNewRound()
    {
        RoundIndex.Value += 1;
        _capturesThisRound = 0;
        CleanupShapesServer();

        // Scale total object count with round index, capped at MaxTotalObjects
        int totalObjects = Mathf.Clamp(InitialTotalObjects + RoundIndex.Value * 2, InitialTotalObjects, MaxTotalObjects);

        // Pick a random number of targets (between 2 and 4, proportional to total objects)
        _targetCount = Random.Range(2, Mathf.Min(5, totalObjects / 2) + 1);

        // Pick a random rule type for this round
        _ruleKind = (RuleKind)Random.Range(0, 3);

        _targetShape = -1;
        _targetColor = -1;

        int shapeCount = System.Enum.GetValues(typeof(ShapeId)).Length;

        // Assign target shape if the rule involves shape matching
        if (_ruleKind == RuleKind.FindByShape || _ruleKind == RuleKind.FindByShapeAndColor)
            _targetShape = Random.Range(0, shapeCount);

        // Assign target color if the rule involves color matching
        if (_ruleKind == RuleKind.FindByColor || _ruleKind == RuleKind.FindByShapeAndColor)
            _targetColor = Random.Range(0, GameDefs.Palette.Length);

        // Build and replicate the rule description text
        RuleText.Value = BuildRuleText(_ruleKind, _targetCount, _targetShape, _targetColor);
        TargetsRemaining.Value = _targetCount;

        // Spawn the target shapes that match the current rule
        for (int i = 0; i < _targetCount; i++)
        {
            int s = (_targetShape >= 0) ? _targetShape : Random.Range(0, shapeCount);
            int c = (_targetColor >= 0) ? _targetColor : Random.Range(0, GameDefs.Palette.Length);
            SpawnShapeServer(s, c);
        }

        // Spawn distractor shapes that do NOT satisfy the rule (no false positives)
        int nonTargets = totalObjects - _targetCount;
        int safety = 0;

        while (nonTargets > 0 && safety < 5000)
        {
            safety++;

            int s = Random.Range(0, shapeCount);
            int c = Random.Range(0, GameDefs.Palette.Length);

            // Reject any combination that would accidentally satisfy the rule
            if (WouldSatisfyRule(_ruleKind, _targetShape, _targetColor, s, c))
                continue;

            SpawnShapeServer(s, c);
            nonTargets--;
        }

        Debug.Log($"[Round {RoundIndex.Value}] {RuleText.Value} (Targets={TargetsRemaining.Value})");
    }

    /// <summary>
    /// Builds the human-readable rule description shown to players.
    /// </summary>
    /// <param name="kind">The type of rule for the round.</param>
    /// <param name="count">How many matching shapes the players must find.</param>
    /// <param name="shape">Target shape index (used if rule involves shape).</param>
    /// <param name="color">Target color index (used if rule involves color).</param>
    FixedString128Bytes BuildRuleText(RuleKind kind, int count, int shape, int color)
    {
        if (kind == RuleKind.FindByShape)
            return $"Find {count} {((ShapeId)shape)}s";

        if (kind == RuleKind.FindByColor)
            return $"Find {count} {GameDefs.ColorNames[color]} objects";

        return $"Find {count} {GameDefs.ColorNames[color]} {((ShapeId)shape)}s";
    }

    /// <summary>
    /// Returns true if the given shape/color combination satisfies the current round rule.
    /// Used both when spawning distractors and when validating player selections.
    /// </summary>
    /// <param name="kind">The active rule type.</param>
    /// <param name="targetShape">The required shape index (-1 if not applicable).</param>
    /// <param name="targetColor">The required color index (-1 if not applicable).</param>
    /// <param name="s">The shape index of the candidate.</param>
    /// <param name="c">The color index of the candidate.</param>
    bool WouldSatisfyRule(RuleKind kind, int targetShape, int targetColor, int s, int c)
    {
        return kind switch
        {
            RuleKind.FindByShape         => s == targetShape,
            RuleKind.FindByColor         => c == targetColor,
            RuleKind.FindByShapeAndColor => (s == targetShape && c == targetColor),
            _                            => false
        };
    }

    /// <summary>
    /// Spawns a single shape on the server with the given shape and color.
    /// Retrieves from the object pool if available, otherwise instantiates a new one.
    /// Writes NetworkVariable values only after the object is spawned on the network.
    /// </summary>
    /// <param name="shapeId">The shape type index to assign.</param>
    /// <param name="colorId">The color index to assign.</param>
    void SpawnShapeServer(int shapeId, int colorId)
    {
        Vector3 pos = GetRandomSpawnPos();
        Quaternion rot = Quaternion.identity;

        // Prefer pooled instances to avoid runtime allocations
        NetworkObject no;
        if (NetworkObjectPool.Singleton != null)
            no = NetworkObjectPool.Singleton.Get(ShapePrefab, pos, rot);
        else
            no = Instantiate(ShapePrefab, pos, rot);

        // Randomize rendering order to create visual layering between shapes
        var sr = no.GetComponent<SpriteRenderer>();
        if (sr != null) sr.sortingOrder = Random.Range(0, 6);

        // 1) Spawn on the network first — required before writing NetworkVariables
        if (!no.IsSpawned)
            no.Spawn(true);

        // 2) Set networked shape data after spawning to ensure correct replication
        var se = no.GetComponent<ShapeEntity>();
        se.Shape.Value = shapeId;
        se.ColorIndex.Value = colorId;

        // 3) Force visual refresh on the server (clients will apply visuals via OnValueChanged)
        se.ForceRefreshServerOnly();

        _activeShapes.Add(no);
    }

    /// <summary>
    /// Returns a random position within the defined spawn area.
    /// </summary>
    Vector3 GetRandomSpawnPos()
    {
        Vector2 half = SpawnAreaSize * 0.5f;
        float x = Random.Range(-half.x, half.x);
        float y = Random.Range(-half.y, half.y);
        return new Vector3(SpawnAreaCenter.x + x, SpawnAreaCenter.y + y, 0f);
    }

    /// <summary>
    /// Despawns and clears all currently active shapes on the server.
    /// Spawned objects are returned to the pool; unspawned ones are destroyed directly.
    /// </summary>
    void CleanupShapesServer()
    {
        for (int i = 0; i < _activeShapes.Count; i++)
        {
            var no = _activeShapes[i];
            if (no == null) continue;

            if (no.IsSpawned)
                no.Despawn(true); // Returns to pool if a handler is registered
            else
                Destroy(no.gameObject);
        }
        _activeShapes.Clear();
    }

    /// <summary>
    /// Clears the singleton reference and allows NetworkBehaviour to perform its own cleanup.
    /// </summary>
    public override void OnDestroy()
    {
        if (Instance == this) Instance = null;
        base.OnDestroy();
    }

    /// <summary>
    /// Draws a wire cube in the Scene View to visualize the shape spawn area.
    /// </summary>
    void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireCube((Vector3)SpawnAreaCenter, SpawnAreaSize);
    }

    /// <summary>
    /// Server RPC called by any client (including the host) when the player clicks on the game world.
    /// The server determines which shape was clicked (topmost by sorting order),
    /// validates whether it satisfies the current rule, awards points, and triggers the next round if needed.
    /// </summary>
    /// <param name="worldPoint">The world-space position of the player's click.</param>
    /// <param name="rpcParams">RPC metadata, used to identify the sending client.</param>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SelectAtServerRpc(Vector2 worldPoint, RpcParams rpcParams = default)
    {
        Debug.Log($"[SERVER] SelectAtServerRpc from {rpcParams.Receive.SenderClientId} worldPoint={worldPoint}");

        ulong senderId = rpcParams.Receive.SenderClientId;
        int playerIndex = Mathf.Clamp((int)senderId, 0, MaxPlayers - 1);

        // Apply optional offset and perform an overlap circle query on the shapes layer
        Vector2 p = worldPoint + SelectionOffset;
        Collider2D[] hits = Physics2D.OverlapCircleAll(p, SelectionRadius, ShapesMask);
        if (hits == null || hits.Length == 0) return;

        // Find the topmost shape by sorting order, with Z position as a tiebreaker
        Collider2D top = null;
        int topOrder = int.MinValue;
        float topZ = float.MinValue;

        foreach (var h in hits)
        {
            var sr = h.GetComponent<SpriteRenderer>();
            if (sr == null) continue;

            int order = sr.sortingOrder;
            float z = h.transform.position.z;

            if (order > topOrder || (order == topOrder && z > topZ))
            {
                top = h;
                topOrder = order;
                topZ = z;
            }
        }

        if (top == null) return;

        // Validate that the hit object has the required components and is still active
        var se = top.GetComponent<ShapeEntity>();
        var no = top.GetComponent<NetworkObject>();
        if (se == null || no == null || !no.IsSpawned) return;

        int s = se.Shape.Value;
        int c = se.ColorIndex.Value;

        // Reject the selection if it does not satisfy the current round rule
        bool correct = WouldSatisfyRule(_ruleKind, _targetShape, _targetColor, s, c);
        if (!correct) return;

        // Award points with diminishing returns: 10 for first capture, 5 for second, 1 for all subsequent
        int points = (_capturesThisRound == 0) ? 10 : (_capturesThisRound == 1 ? 5 : 1);
        _capturesThisRound++;

        Scores[playerIndex] = Scores[playerIndex] + points;
        TargetsRemaining.Value = Mathf.Max(0, TargetsRemaining.Value - 1);

        // Show popup points
        var playerCursor = NetworkManager.Singleton.ConnectedClients[senderId].PlayerObject?.GetComponent<PlayerCursor>();
        playerCursor?.ShowScorePopupOwnerRpc(points, c, no.transform.position);

        // Remove the captured shape from the scene and tracking list
        no.Despawn(true);
        _activeShapes.Remove(no);

        // If all targets have been found, immediately start the next round
        if (TargetsRemaining.Value <= 0)
            StartNewRound();
    }
}