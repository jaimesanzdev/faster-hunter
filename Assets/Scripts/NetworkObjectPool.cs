using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Singleton object pool for NetworkObjects.
/// Integrates with Netcode for GameObjects via INetworkPrefabInstanceHandler,
/// allowing NGO to use pooled instances instead of instantiating and destroying objects at runtime.
/// Persists across scene loads via DontDestroyOnLoad.
/// </summary>
public class NetworkObjectPool : MonoBehaviour
{
    /// <summary>
    /// Configuration entry for a single prefab pool.
    /// Serializable so it can be set up directly in the Inspector.
    /// </summary>
    [System.Serializable]
    public class PoolConfig
    {
        /// <summary>The networked prefab to pool.</summary>
        public NetworkObject Prefab;

        /// <summary>Number of instances to pre-instantiate at registration time.</summary>
        public int PrewarmCount = 32;
    }

    /// <summary>Global singleton reference accessible from any script.</summary>
    public static NetworkObjectPool Singleton { get; private set; }

    [Header("Pools")]
    /// <summary>List of prefab pool configurations, set up in the Inspector.</summary>
    public List<PoolConfig> Pools = new();

    /// <summary>Internal pool storage: maps each prefab to its queue of inactive instances.</summary>
    readonly Dictionary<NetworkObject, Queue<NetworkObject>> _pool = new();

    /// <summary>Registered NGO prefab handlers, one per pooled prefab.</summary>
    readonly Dictionary<NetworkObject, PooledPrefabHandler> _handlers = new();

    /// <summary>
    /// Enforces the singleton pattern and marks this object to persist across scene loads.
    /// </summary>
    void Awake()
    {
        if (Singleton != null && Singleton != this)
        {
            Destroy(gameObject);
            return;
        }
        Singleton = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Registers all configured prefab pools with the given NetworkManager.
    /// Should be called once after the NetworkManager is initialized.
    /// </summary>
    /// <param name="nm">The NetworkManager instance to register prefab handlers with.</param>
    public void RegisterAll(NetworkManager nm)
    {
        foreach (var cfg in Pools)
        {
            RegisterPrefab(nm, cfg.Prefab, cfg.PrewarmCount);
        }
    }

    /// <summary>
    /// Registers a single prefab with the pool and the NGO prefab handler system.
    /// Pre-instantiates a number of inactive instances to avoid runtime allocations during gameplay.
    /// Skips registration if the prefab has already been registered.
    /// </summary>
    /// <param name="nm">The NetworkManager to register the handler with.</param>
    /// <param name="prefab">The NetworkObject prefab to pool.</param>
    /// <param name="prewarmCount">Number of instances to create upfront.</param>
    public void RegisterPrefab(NetworkManager nm, NetworkObject prefab, int prewarmCount)
    {
        if (prefab == null) return;
        if (_handlers.ContainsKey(prefab)) return; // Already registered — skip

        if (!_pool.ContainsKey(prefab))
            _pool[prefab] = new Queue<NetworkObject>();

        // Pre-populate the pool with inactive instances
        for (int i = 0; i < prewarmCount; i++)
        {
            var inst = Instantiate(prefab);
            inst.gameObject.SetActive(false);
            _pool[prefab].Enqueue(inst);
        }

        // Register the custom handler so NGO uses the pool instead of Instantiate/Destroy
        var handler = new PooledPrefabHandler(this, prefab);
        _handlers[prefab] = handler;
        nm.PrefabHandler.AddHandler(prefab.gameObject, handler);
    }

    /// <summary>
    /// Retrieves an instance of the given prefab from the pool.
    /// If the pool is empty, a new instance is instantiated.
    /// Positions and activates the instance before returning it.
    /// </summary>
    /// <param name="prefab">The prefab to retrieve an instance of.</param>
    /// <param name="pos">World position to place the instance at.</param>
    /// <param name="rot">World rotation to apply to the instance.</param>
    internal NetworkObject Get(NetworkObject prefab, Vector3 pos, Quaternion rot)
    {
        if (!_pool.TryGetValue(prefab, out var q))
            q = _pool[prefab] = new Queue<NetworkObject>();

        // Reuse a pooled instance if available, otherwise create a new one
        NetworkObject inst = q.Count > 0 ? q.Dequeue() : Instantiate(prefab);

        inst.transform.SetPositionAndRotation(pos, rot);
        inst.gameObject.SetActive(true);
        return inst;
    }

    /// <summary>
    /// Returns a used instance back to the pool by deactivating it and re-enqueueing it.
    /// </summary>
    /// <param name="prefab">The original prefab this instance belongs to.</param>
    /// <param name="instance">The instance to return.</param>
    internal void Return(NetworkObject prefab, NetworkObject instance)
    {
        instance.gameObject.SetActive(false);
        _pool[prefab].Enqueue(instance);
    }

    /// <summary>
    /// NGO prefab instance handler that redirects Instantiate and Destroy calls
    /// through the NetworkObjectPool instead of using Unity's default behavior.
    /// </summary>
    class PooledPrefabHandler : INetworkPrefabInstanceHandler
    {
        readonly NetworkObjectPool _pool;
        readonly NetworkObject _prefab;

        public PooledPrefabHandler(NetworkObjectPool pool, NetworkObject prefab)
        {
            _pool = pool;
            _prefab = prefab;
        }

        /// <summary>Called by NGO when it needs to spawn an instance of this prefab.</summary>
        public NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation)
            => _pool.Get(_prefab, position, rotation);

        /// <summary>Called by NGO when it needs to destroy an instance of this prefab.</summary>
        public void Destroy(NetworkObject networkObject)
            => _pool.Return(_prefab, networkObject);
    }
}