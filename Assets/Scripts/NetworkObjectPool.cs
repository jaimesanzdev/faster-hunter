using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class NetworkObjectPool : MonoBehaviour
{
    [System.Serializable]
    public class PoolConfig
    {
        public NetworkObject Prefab;
        public int PrewarmCount = 32;
    }

    public static NetworkObjectPool Singleton { get; private set; }

    [Header("Pools")]
    public List<PoolConfig> Pools = new();

    readonly Dictionary<NetworkObject, Queue<NetworkObject>> _pool = new();
    readonly Dictionary<NetworkObject, PooledPrefabHandler> _handlers = new();

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

    public void RegisterAll(NetworkManager nm)
    {
        foreach (var cfg in Pools)
        {
            RegisterPrefab(nm, cfg.Prefab, cfg.PrewarmCount);
        }
    }

    public void RegisterPrefab(NetworkManager nm, NetworkObject prefab, int prewarmCount)
    {
        if (prefab == null) return;
        if (_handlers.ContainsKey(prefab)) return;

        if (!_pool.ContainsKey(prefab))
            _pool[prefab] = new Queue<NetworkObject>();

        // Prewarm (local only; these will be used for Spawn later)
        for (int i = 0; i < prewarmCount; i++)
        {
            var inst = Instantiate(prefab);
            inst.gameObject.SetActive(false);
            _pool[prefab].Enqueue(inst);
        }

        var handler = new PooledPrefabHandler(this, prefab);
        _handlers[prefab] = handler;

        nm.PrefabHandler.AddHandler(prefab.gameObject, handler);
    }

    internal NetworkObject Get(NetworkObject prefab, Vector3 pos, Quaternion rot)
    {
        if (!_pool.TryGetValue(prefab, out var q))
            q = _pool[prefab] = new Queue<NetworkObject>();

        NetworkObject inst = q.Count > 0 ? q.Dequeue() : Instantiate(prefab);

        var t = inst.transform;
        t.SetPositionAndRotation(pos, rot);
        inst.gameObject.SetActive(true);
        return inst;
    }

    internal void Return(NetworkObject prefab, NetworkObject instance)
    {
        instance.gameObject.SetActive(false);
        _pool[prefab].Enqueue(instance);
    }

    // NGO handler
    class PooledPrefabHandler : INetworkPrefabInstanceHandler
    {
        readonly NetworkObjectPool _pool;
        readonly NetworkObject _prefab;

        public PooledPrefabHandler(NetworkObjectPool pool, NetworkObject prefab)
        {
            _pool = pool;
            _prefab = prefab;
        }

        public NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation)
            => _pool.Get(_prefab, position, rotation);

        public void Destroy(NetworkObject networkObject)
            => _pool.Return(_prefab, networkObject);
    }
}