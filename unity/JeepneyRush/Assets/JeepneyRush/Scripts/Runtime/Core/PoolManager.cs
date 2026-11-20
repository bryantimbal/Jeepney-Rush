using System;
using System.Collections.Generic;
using UnityEngine;

namespace JeepneyRush
{
    /// <summary>
    /// Keyed GameObject pools. Pooled objects live under a persistent root so a
    /// restart never instantiates traffic, coins or obstacles again.
    /// </summary>
    public sealed class PoolManager : MonoBehaviour
    {
        private sealed class Pool
        {
            public Func<GameObject> Factory;
            public readonly Stack<GameObject> Free = new Stack<GameObject>();
            public readonly HashSet<GameObject> Active = new HashSet<GameObject>();
        }

        private readonly Dictionary<string, Pool> _pools = new Dictionary<string, Pool>();
        private readonly Dictionary<GameObject, string> _keyOf = new Dictionary<GameObject, string>();
        private Transform _storage;

        private void Awake()
        {
            _storage = new GameObject("PoolStorage").transform;
            _storage.SetParent(transform, false);
        }

        public void Register(string key, Func<GameObject> factory, int prewarm = 0)
        {
            if (!_pools.TryGetValue(key, out var p))
            {
                p = new Pool();
                _pools[key] = p;
            }
            p.Factory = factory;
            for (int i = p.Free.Count + p.Active.Count; i < prewarm; i++)
            {
                var go = Create(key, p);
                go.SetActive(false);
                p.Free.Push(go);
            }
        }

        public bool Has(string key) => _pools.ContainsKey(key);

        private GameObject Create(string key, Pool p)
        {
            var go = p.Factory();
            go.transform.SetParent(_storage, false);
            _keyOf[go] = key;
            return go;
        }

        public GameObject Get(string key, Transform parent = null)
        {
            if (!_pools.TryGetValue(key, out var p) || p.Factory == null)
                throw new InvalidOperationException($"Pool '{key}' is not registered");
            GameObject go = null;
            while (p.Free.Count > 0 && go == null) go = p.Free.Pop(); // skip destroyed entries
            if (go == null) go = Create(key, p);
            p.Active.Add(go);
            if (parent != null) go.transform.SetParent(parent, false);
            go.SetActive(true);
            return go;
        }

        public void Release(GameObject go)
        {
            if (go == null) return;
            if (!_keyOf.TryGetValue(go, out var key) || !_pools.TryGetValue(key, out var p))
            {
                Destroy(go);
                return;
            }
            if (!p.Active.Remove(go)) return; // already released
            go.SetActive(false);
            go.transform.SetParent(_storage, false);
            p.Free.Push(go);
        }

        /// <summary>Return every active object to its pool (scene change / restart).</summary>
        public void ReleaseAll()
        {
            var tmp = new List<GameObject>();
            foreach (var p in _pools.Values) tmp.AddRange(p.Active);
            foreach (var go in tmp) Release(go);
        }

        public int ActiveCount
        {
            get { int n = 0; foreach (var p in _pools.Values) n += p.Active.Count; return n; }
        }
    }
}
