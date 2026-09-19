using TillWinter.Core;
using UnityEngine;

namespace TillWinter.Unity
{
    /// <summary>
    /// Two butterflies looping lazily over the field and two bees hopping from bed to bed (ripe ones first) in Spring
    /// and Summer; they shrink away in Autumn and Winter. Small life so the farm is not still when the player is not
    /// touching it.
    /// </summary>
    public sealed class CrittersView : MonoBehaviour
    {
        private const int Count = 2;

        private GameController _game;
        private readonly Transform[] _butterflies = new Transform[Count];
        private readonly Transform[] _wingL = new Transform[Count];
        private readonly Transform[] _wingR = new Transform[Count];
        private const int BeeCount = 2;
        private readonly Transform[] _bees = new Transform[BeeCount];
        private readonly Transform[] _beeWingL = new Transform[BeeCount];
        private readonly Transform[] _beeWingR = new Transform[BeeCount];
        private readonly Vector3[] _beeTarget = new Vector3[BeeCount];
        private readonly float[] _beeTimer = new float[BeeCount];
        private float _t;
        private float _visible;

        public void Init(GameController game, VisualCatalog catalog)
        {
            _game = game;
            for (int i = 0; i < Count; i++)
            {
                var go = catalog.Spawn(catalog.Butterfly, transform, "Butterfly");
                if (go == null) return;
                _butterflies[i] = go.transform;
                _wingL[i] = go.transform.Find("WingL") ?? go.transform;
                _wingR[i] = go.transform.Find("WingR") ?? go.transform;
                go.transform.localScale = Vector3.zero;
            }
            if (catalog.Bee == null) return;
            for (int i = 0; i < BeeCount; i++)
            {
                var go = catalog.Spawn(catalog.Bee, transform, "Bee");
                _bees[i] = go.transform;
                _beeWingL[i] = go.transform.Find("WingL") ?? go.transform;
                _beeWingR[i] = go.transform.Find("WingR") ?? go.transform;
                go.transform.localScale = Vector3.zero;
            }
        }

        /// <summary>A bed to visit: a ripe one if the bee finds one within a few random picks.</summary>
        private Vector3 PickBed(int bee)
        {
            var plots = _game.State.Plots;
            int n = plots.Count;
            if (n == 0) return Vector3.up;
            int pick = Random.Range(0, n);
            for (int tries = 0; tries < 6; tries++)
            {
                int k = Random.Range(0, n);
                if (plots[k].State == PlotState.Ripe) { pick = k; break; }
            }
            var p = plots[pick].Pos;
            return transform.InverseTransformPoint(_game.PlotToWorld(p, 0.55f + bee * 0.08f));
        }

        private void TickBees(float dt)
        {
            for (int i = 0; i < BeeCount; i++)
            {
                var b = _bees[i];
                if (b == null) continue;
                _beeTimer[i] -= dt;
                if (_beeTimer[i] <= 0f)
                {
                    _beeTimer[i] = 2.5f + Random.value * 2.5f;
                    _beeTarget[i] = PickBed(i);
                }
                // Hover in a small wobbling loop over the bed, dart to the next one.
                var goal = _beeTarget[i] + new Vector3(Mathf.Sin(_t * 3.1f + i) * 0.12f, Mathf.Sin(_t * 7f + i) * 0.03f, Mathf.Cos(_t * 2.7f + i) * 0.1f);
                var pos = b.localPosition;
                var next = Vector3.Lerp(pos, goal, 1f - Mathf.Exp(-dt * 3f));
                var d = next - pos;
                if (d.sqrMagnitude > 1e-6f) b.localRotation = Quaternion.Euler(0f, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, 0f);
                b.localPosition = next;
                b.localScale = Vector3.one * Mathf.Max(0.0001f, _visible);
                float flap = Mathf.Sin(_t * 60f + i) * 45f;
                _beeWingL[i].localRotation = Quaternion.Euler(0f, 0f, flap);
                _beeWingR[i].localRotation = Quaternion.Euler(0f, 0f, -flap);
            }
        }

        private void LateUpdate()
        {
            if (_butterflies[0] == null) return;
            var s = _game.State;
            float dt = Time.deltaTime;
            _t += dt;
            bool warm = s.Phase == Phase.Year && (s.Season == Season.Spring || s.Season == Season.Summer);
            _visible = Prims.Damp(_visible, warm ? 1f : 0f, 2f, dt);
            float half = s.GridSize * 0.5f + 0.5f;
            for (int i = 0; i < Count; i++)
            {
                var t = _butterflies[i];
                if (t == null) continue;
                float phase = _t * (0.33f + i * 0.07f) + i * 2.1f;
                t.localPosition = new Vector3(Mathf.Sin(phase) * half, 0.8f + Mathf.Sin(phase * 2.3f) * 0.16f, Mathf.Cos(phase * 0.7f) * half);
                t.localRotation = Quaternion.Euler(0f, -phase * Mathf.Rad2Deg, 0f);
                t.localScale = Vector3.one * Mathf.Max(0.0001f, _visible);
                float flap = Mathf.Sin(_t * 18f + i) * 55f;
                _wingL[i].localRotation = Quaternion.Euler(0f, 0f, flap);
                _wingR[i].localRotation = Quaternion.Euler(0f, 0f, -flap);
            }
            TickBees(dt);
        }
    }

    /// <summary>
    /// The pond frog: sits, puffs its throat, now and then hops a short way and back. Gone (shrunk away) in
    /// Autumn and Winter, like the butterflies.
    /// </summary>
    public sealed class FrogView : MonoBehaviour
    {
        private GameController _game;
        private Transform _throat;
        private Vector3 _home;
        private Vector3 _from, _to;
        private float _hopT = -1f;
        private float _wait;
        private float _visible;
        private float _t;
        private bool _away;

        public void Init(GameController game)
        {
            _game = game;
            _throat = transform.Find("Throat");
            _wait = 2f + Random.value * 3f;
            transform.localScale = Vector3.one * 0.0001f;
        }

        public void SetHome(Vector3 home)
        {
            _home = home;
            _away = false;
            _hopT = -1f;
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            _t += dt;
            var s = _game.State;
            bool warm = s.Phase == Phase.Year && (s.Season == Season.Spring || s.Season == Season.Summer);
            _visible = Prims.Damp(_visible, warm ? 1f : 0f, 3f, dt);
            transform.localScale = Vector3.one * Mathf.Max(0.0001f, _visible * 1.5f);
            if (_visible < 0.01f) return;

            if (_throat != null)
            {
                float puff = Mathf.Max(0f, Mathf.Sin(_t * 5f)) * (Mathf.Sin(_t * 0.7f) > 0.3f ? 1f : 0f);
                _throat.localScale = new Vector3(0.09f, 0.06f, 0.06f) * (1f + puff * 0.6f);
            }

            if (_hopT < 0f)
            {
                _wait -= dt;
                if (_wait > 0f) return;
                _wait = 3f + Random.value * 5f;
                _from = transform.localPosition;
                _to = _away ? _home : _home + new Vector3(Random.Range(-0.25f, 0.25f), 0f, Random.Range(-0.25f, 0.05f));
                _away = !_away;
                _hopT = 0f;
                var d = _to - _from;
                if (d.sqrMagnitude > 1e-4f) transform.localRotation = Quaternion.Euler(0f, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, 0f);
                return;
            }
            _hopT = Mathf.Min(1f, _hopT + dt / 0.35f);
            var p = Vector3.Lerp(_from, _to, _hopT);
            p.y = Mathf.Sin(_hopT * Mathf.PI) * 0.14f;
            transform.localPosition = p;
            if (_hopT >= 1f) _hopT = -1f;
        }
    }

    /// <summary>The farm flag: the cloth swings and ripples on its pole. Still under Reduce motion.</summary>
    public sealed class FlagView : MonoBehaviour
    {
        private Transform _cloth;
        private float _phase;

        private void Awake()
        {
            _cloth = transform.Find("Cloth");
            // New Game+ flies a golden flag (GDD §8.3 v2.4): the family's mark of having been round once already.
            if (GameSession.NgPlus > 0) GetComponent<PaletteBinder>()?.Override(PaletteSlot.Roof, Palette.Load().Golden);
            _phase = Random.value * 10f;
        }

        private void LateUpdate()
        {
            if (_cloth == null) return;
            if (!SettingsStore.MotionAllowed) { _cloth.localRotation = Quaternion.Euler(0f, 20f, 0f); _cloth.localScale = Vector3.one; return; }
            float t = Time.time + _phase;
            float gust = 0.6f + 0.4f * Mathf.Sin(t * 0.35f);
            _cloth.localRotation = Quaternion.Euler(0f, 20f + Mathf.Sin(t * 2.1f) * 18f * gust, Mathf.Sin(t * 3.3f) * 4f);
            _cloth.localScale = new Vector3(1f, 1f + Mathf.Sin(t * 5f) * 0.05f, 1f);
        }
    }

    /// <summary>A hen behind the fence: walks a few steps, stops, pecks; hidden outside the Year.</summary>
    public sealed class ChickenView : MonoBehaviour
    {
        private Transform _head, _body;
        private GameController _game;
        private Vector3 _target;
        private float _minX, _maxX, _minZ, _maxZ;
        private float _wait, _peck, _facing, _step, _visible = 1f;
        private int _seed;

        private void Awake()
        {
            _body = transform.Find("Body");
            _head = _body != null ? _body.Find("Head") : null;
            _game = FindFirstObjectByType<GameController>();
        }

        public void SetArea(Vector3 start, float minX, float maxX, float minZ, float maxZ, int seed)
        {
            _minX = minX; _maxX = maxX; _minZ = minZ; _maxZ = maxZ;
            _seed = seed;
            transform.localPosition = start;
            _target = start;
            _wait = 1f + seed;
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            bool show = _game == null || _game.State == null || _game.State.Phase == TillWinter.Core.Phase.Year;
            _visible = Prims.Damp(_visible, show ? 1f : 0f, 4f, dt);
            transform.localScale = Vector3.one * Mathf.Max(0.0001f, _visible);
            if (!SettingsStore.MotionAllowed) return;
            var pos = transform.localPosition;
            var to = _target - pos;
            to.y = 0f;
            if (to.magnitude > 0.03f)
            {
                pos += to.normalized * Mathf.Min(to.magnitude, 0.45f * dt);
                transform.localPosition = pos;
                _facing = Mathf.LerpAngle(_facing, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, 1f - Mathf.Exp(-dt * 8f));
                _step += dt * 14f;
                _peck = 0f;
            }
            else
            {
                _wait -= dt;
                _peck += dt;
                if (_wait <= 0f)
                {
                    _wait = 2f + Random.value * 3f;
                    _target = new Vector3(Random.Range(_minX, _maxX), 0f, Random.Range(_minZ, _maxZ));
                }
            }
            transform.localRotation = Quaternion.Euler(0f, _facing, 0f);
            if (_body != null) _body.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(_step)) * 0.02f, 0f);
            if (_head != null)
            {
                float dip = _peck > 0f ? Mathf.Max(0f, Mathf.Sin(_peck * 7f + _seed)) : 0f;
                _head.localRotation = Quaternion.Euler(dip * 55f, 0f, 0f);
            }
        }
    }

    /// <summary>The cat sleeps in the grass: it breathes and its tail swishes now and then.</summary>
    public sealed class CatView : MonoBehaviour
    {
        private Transform _tail;
        private Vector3 _baseScale;

        private void Awake()
        {
            _tail = transform.Find("Tail");
            _baseScale = transform.localScale;
        }

        private void LateUpdate()
        {
            float t = Time.time;
            bool moving = SettingsStore.MotionAllowed;
            float breathe = moving ? 1f + Mathf.Sin(t * 1.6f) * 0.03f : 1f;
            transform.localScale = new Vector3(_baseScale.x, _baseScale.y * breathe, _baseScale.z);
            if (_tail != null)
            {
                float swish = moving ? Mathf.Sin(t * 3f) * Mathf.Max(0f, Mathf.Sin(t * 0.4f)) * 35f : 0f;
                _tail.localRotation = Quaternion.Euler(0f, swish, 0f);
            }
        }
    }
}
