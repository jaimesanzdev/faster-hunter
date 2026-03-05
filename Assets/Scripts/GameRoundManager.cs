using Unity.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class GameRoundManager : NetworkBehaviour
{
    public static GameRoundManager Instance { get; private set; }

    [Header("Prefabs")]
    public NetworkObject ShapePrefab;

    [Header("Spawn Area")]
    public Vector2 SpawnAreaCenter = Vector2.zero;
    public Vector2 SpawnAreaSize = new Vector2(10f, 6f);

    [Header("Difficulty")]
    public int InitialTotalObjects = 12;
    public int MaxTotalObjects = 40;

    [Header("Players")]
    public int MaxPlayers = 4;

    [Header("Selection")]
    public float SelectionRadius = 0.12f;     // mouse “preciso”: 0.08–0.14
    public Vector2 SelectionOffset = Vector2.zero; // déjalo en 0 para mouse
    public LayerMask ShapesMask; // así lo eliges en Inspector (mejor que GetMask hardcode)
    
    // Networked state for UI
    public NetworkList<int> Scores;
    public NetworkVariable<int> RoundIndex = new NetworkVariable<int>(0);
    public NetworkVariable<int> TargetsRemaining = new NetworkVariable<int>(0);
    public NetworkVariable<FixedString128Bytes> RuleText = new NetworkVariable<FixedString128Bytes>("");

    // Current round (server authoritative)
    RuleKind _ruleKind;
    int _targetCount;
    int _targetShape; // -1 if not used
    int _targetColor; // -1 if not used
    int _capturesThisRound;

    // Track spawned shapes (server)
    readonly List<NetworkObject> _activeShapes = new();

    void Awake()
    {
        Instance = this;
        Scores = new NetworkList<int>();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        // Init caches (one time)
        ShapeSpriteCache.Initialize();
        ShapeColliderCache.Initialize();

        // Init scores
        Scores.Clear();
        for (int i = 0; i < MaxPlayers; i++) Scores.Add(0);

        StartNewRound();
    }

    void StartNewRound()
    {
        RoundIndex.Value += 1;
        _capturesThisRound = 0;
        CleanupShapesServer();

        int totalObjects = Mathf.Clamp(InitialTotalObjects + RoundIndex.Value * 2, InitialTotalObjects, MaxTotalObjects);
        _targetCount = Random.Range(2, Mathf.Min(5, totalObjects / 2) + 1);

        _ruleKind = (RuleKind)Random.Range(0, 3);

        _targetShape = -1;
        _targetColor = -1;

        int shapeCount = System.Enum.GetValues(typeof(ShapeId)).Length;

        if (_ruleKind == RuleKind.FindByShape || _ruleKind == RuleKind.FindByShapeAndColor)
            _targetShape = Random.Range(0, shapeCount);

        if (_ruleKind == RuleKind.FindByColor || _ruleKind == RuleKind.FindByShapeAndColor)
            _targetColor = Random.Range(0, GameDefs.Palette.Length);

        RuleText.Value = BuildRuleText(_ruleKind, _targetCount, _targetShape, _targetColor);
        TargetsRemaining.Value = _targetCount;

        // Spawn targets
        for (int i = 0; i < _targetCount; i++)
        {
            int s = (_targetShape >= 0) ? _targetShape : Random.Range(0, shapeCount);
            int c = (_targetColor >= 0) ? _targetColor : Random.Range(0, GameDefs.Palette.Length);
            SpawnShapeServer(s, c);
        }

        // Spawn non-targets (no falsos positivos)
        int nonTargets = totalObjects - _targetCount;
        int safety = 0;

        while (nonTargets > 0 && safety < 5000)
        {
            safety++;

            int s = Random.Range(0, shapeCount);
            int c = Random.Range(0, GameDefs.Palette.Length);

            if (WouldSatisfyRule(_ruleKind, _targetShape, _targetColor, s, c))
                continue; // evita falsos positivos

            SpawnShapeServer(s, c);
            nonTargets--;
        }
        Debug.Log($"[Round {RoundIndex.Value}] {RuleText.Value} (Targets={TargetsRemaining.Value})");
    }

    FixedString128Bytes BuildRuleText(RuleKind kind, int count, int shape, int color)
    {
        if (kind == RuleKind.FindByShape)
            return $"Find {count} {((ShapeId)shape)}s";

        if (kind == RuleKind.FindByColor)
            return $"Find {count} {GameDefs.ColorNames[color]} objects";

        return $"Find {count} {GameDefs.ColorNames[color]} {((ShapeId)shape)}s";
    }

    bool WouldSatisfyRule(RuleKind kind, int targetShape, int targetColor, int s, int c)
    {
        return kind switch
        {
            RuleKind.FindByShape => s == targetShape,
            RuleKind.FindByColor => c == targetColor,
            RuleKind.FindByShapeAndColor => (s == targetShape && c == targetColor),
            _ => false
        };
    }

void SpawnShapeServer(int shapeId, int colorId)
{
    Vector3 pos = GetRandomSpawnPos();
    Quaternion rot = Quaternion.identity;

    NetworkObject no;
    if (NetworkObjectPool.Singleton != null)
        no = NetworkObjectPool.Singleton.Get(ShapePrefab, pos, rot);
    else
        no = Instantiate(ShapePrefab, pos, rot);

    var sr = no.GetComponent<SpriteRenderer>();
    if (sr != null) sr.sortingOrder = Random.Range(0, 6);

    // 1) Spawn primero (si hace falta)
    if (!no.IsSpawned)
        no.Spawn(true);

    // 2) Ahora ya es seguro escribir NetworkVariables
    var se = no.GetComponent<ShapeEntity>();
    se.Shape.Value = shapeId;
    se.ColorIndex.Value = colorId;

    // 3) Forzar visuals en server (en clientes se aplicará por OnValueChanged/OnNetworkSpawn)
    se.ForceRefreshServerOnly();

    _activeShapes.Add(no);
}

    Vector3 GetRandomSpawnPos()
    {
        Vector2 half = SpawnAreaSize * 0.5f;
        float x = Random.Range(-half.x, half.x);
        float y = Random.Range(-half.y, half.y);
        return new Vector3(SpawnAreaCenter.x + x, SpawnAreaCenter.y + y, 0f);
    }

    void CleanupShapesServer()
    {
        for (int i = 0; i < _activeShapes.Count; i++)
        {
            var no = _activeShapes[i];
            if (no == null) continue;

            if (no.IsSpawned)
                no.Despawn(true); // will return to pool if handler is registered
            else
                Destroy(no.gameObject);
        }
        _activeShapes.Clear();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireCube((Vector3)SpawnAreaCenter, SpawnAreaSize);
    }

    // Client sends worldPoint; server decides topmost + validates + scores
[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SelectAtServerRpc(Vector2 worldPoint, RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        int playerIndex = Mathf.Clamp((int)senderId, 0, MaxPlayers - 1);

        Vector2 p = worldPoint + SelectionOffset;
        Collider2D[] hits = Physics2D.OverlapCircleAll(p, SelectionRadius, ShapesMask);
        if (hits == null || hits.Length == 0) return;

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

        var se = top.GetComponent<ShapeEntity>();
        var no = top.GetComponent<NetworkObject>();
        if (se == null || no == null || !no.IsSpawned) return;

        int s = se.Shape.Value;
        int c = se.ColorIndex.Value;

        bool correct = WouldSatisfyRule(_ruleKind, _targetShape, _targetColor, s, c);
        if (!correct) return;

        int points = (_capturesThisRound == 0) ? 10 : (_capturesThisRound == 1 ? 5 : 1);
        _capturesThisRound++;

        Scores[playerIndex] = Scores[playerIndex] + points;
        TargetsRemaining.Value = Mathf.Max(0, TargetsRemaining.Value - 1);

        no.Despawn(true);
        _activeShapes.Remove(no);

        if (TargetsRemaining.Value <= 0)
            StartNewRound();
    }
}