using System.Collections.Generic;
using TillWinter.Core.Feel;
using UnityEngine;

namespace TillWinter.Unity
{
    /// <summary>
    /// The only way effects reach the scene: pools pre-warmed at boot from the <see cref="VfxCatalog"/>, no Instantiate
    /// during play, no per-frame allocations. Every id is rate-limited; over-budget plays merge into a stronger one.
    /// Continuous ids (rain under the cloud, season ambience) are driven by emission rate.
    /// </summary>
    public sealed class VfxPlayer : MonoBehaviour
    {
        private sealed class Pool
        {
            public VfxEntry Entry;
            public ParticleSystem[] Systems;
            public int Next;
            public RateLimiter Limiter;
            public Vector3 LastPosition;
            public float LastScale;
            public Color? Tint;
            public bool Pending;
        }

        private readonly Dictionary<VfxId, Pool> _pools = new Dictionary<VfxId, Pool>();
        private readonly List<Pool> _poolList = new List<Pool>();
        private Palette _palette;
        private ParticleSystem.EmitParams _emit;
        public static VfxPlayer Instance { get; private set; }

        /// <summary>Particle systems currently alive (smoke budget check).</summary>
        public int ActiveSystems
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _poolList.Count; i++)
                    foreach (var ps in _poolList[i].Systems)
                        if (ps.isPlaying && ps.particleCount > 0) n++;
                return n;
            }
        }

        public void Init(VfxCatalog catalog)
        {
            Instance = this;
            _palette = Palette.Load();
            _emit = new ParticleSystem.EmitParams();
            foreach (var entry in catalog.Entries)
            {
                if (entry.Prefab == null || _pools.ContainsKey(entry.Id)) continue;
                int size = Mathf.Max(1, entry.Continuous ? 1 : entry.PoolSize);
                var pool = new Pool { Entry = entry, Systems = new ParticleSystem[size], Limiter = new RateLimiter(Mathf.Max(1, entry.MaxPerSecond)) };
                for (int i = 0; i < size; i++)
                {
                    var go = Instantiate(entry.Prefab, transform);
                    go.name = entry.Id + " " + i;
                    var ps = go.GetComponent<ParticleSystem>();
                    var main = ps.main;
                    main.playOnAwake = false;
                    var col = _palette.Get(entry.Color);
                    main.startColor = entry.UseColor2 ? new ParticleSystem.MinMaxGradient(col, _palette.Get(entry.Color2)) : new ParticleSystem.MinMaxGradient(col);
                    if (entry.Continuous)
                    {
                        var em = ps.emission;
                        em.rateOverTime = 0f;
                        ps.Play();
                    }
                    else ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    pool.Systems[i] = ps;
                }
                _pools[entry.Id] = pool;
                _poolList.Add(pool);
            }
        }

        /// <summary>Plays a burst; over budget the call merges into the next allowed burst (stronger). Scale multiplies the emitted count.</summary>
        public void Play(VfxId id, Vector3 position, float scale = 1f)
        {
            if (!_pools.TryGetValue(id, out var pool) || pool.Entry.Continuous) return;
            pool.LastPosition = position;
            pool.LastScale = scale;
            pool.Tint = null;
            if (pool.Limiter.Request(Time.unscaledTimeAsDouble, out float intensity)) Emit(pool, position, scale * intensity);
            else pool.Pending = true;
        }

        /// <summary>Same, with a palette colour chosen at play time (crop tier bursts).</summary>
        public void Play(VfxId id, Vector3 position, float scale, Color tint)
        {
            if (!_pools.TryGetValue(id, out var pool) || pool.Entry.Continuous) return;
            pool.LastPosition = position;
            pool.LastScale = scale;
            pool.Tint = tint;
            if (pool.Limiter.Request(Time.unscaledTimeAsDouble, out float intensity)) Emit(pool, position, scale * intensity);
            else pool.Pending = true;
        }

        /// <summary>Continuous systems: emission rate (0 = off) and position.</summary>
        public void SetRate(VfxId id, float rate, Vector3? position = null)
        {
            if (!_pools.TryGetValue(id, out var pool) || !pool.Entry.Continuous) return;
            var ps = pool.Systems[0];
            var em = ps.emission;
            em.rateOverTime = rate;
            if (position.HasValue) ps.transform.position = position.Value;
        }

        /// <summary>Continuous systems: clear every live particle (snow melts at once).</summary>
        public void Clear(VfxId id)
        {
            if (_pools.TryGetValue(id, out var pool)) foreach (var ps in pool.Systems) ps.Clear();
        }

        private void Emit(Pool pool, Vector3 position, float amount)
        {
            var ps = pool.Systems[pool.Next];
            pool.Next = (pool.Next + 1) % pool.Systems.Length;
            ps.transform.position = position;
            int count = Mathf.Max(1, Mathf.RoundToInt(pool.Entry.BaseCount * amount));
            if (pool.Tint.HasValue)
            {
                _emit.startColor = pool.Tint.Value;
                _emit.applyShapeToPosition = true;
                for (int i = 0; i < count; i++) ps.Emit(_emit, 1);
            }
            else ps.Emit(count);
        }

        private void LateUpdate()
        {
            double now = Time.unscaledTimeAsDouble;
            for (int i = 0; i < _poolList.Count; i++)
            {
                var pool = _poolList[i];
                if (!pool.Pending) continue;
                if (pool.Limiter.Poll(now, out float intensity)) Emit(pool, pool.LastPosition, pool.LastScale * intensity);
                if (pool.Limiter.Pending == 0) pool.Pending = false;
            }
            Haptics.Poll();
        }

        /// <summary>Convenience for views that hold no reference.</summary>
        public static void Fire(VfxId id, Vector3 position, float scale = 1f)
        {
            if (Instance != null) Instance.Play(id, position, scale);
        }
    }
}
