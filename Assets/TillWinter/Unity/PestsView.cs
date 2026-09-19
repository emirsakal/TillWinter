using TillWinter.Core;
using UnityEngine;

namespace TillWinter.Unity
{
    /// <summary>
    /// The pest on the field and the lucky clover (GDD §5.5/§5.6 v2.1): a mole rising from its mound, a rabbit hopping
    /// on the spot, a swarm of locusts circling its patch, a clover spinning over its plot. Each is spawned once and
    /// shown or hidden; puffs mark arrivals, departures and damage.
    /// </summary>
    public sealed class PestsView : MonoBehaviour
    {
        private const int Swarm = 14;

        private GameController _game;
        private VfxPlayer _fx;
        private Transform _mole, _moleBody, _rabbit, _rabbitBody, _swarm, _clover, _cloverBody;
        private readonly Transform[] _locusts = new Transform[Swarm];
        private float _show, _cloverShow;

        public void Init(GameController game, VfxPlayer fx, VisualCatalog catalog)
        {
            _game = game;
            _fx = fx;
            _mole = Spawn(catalog, catalog.Mole, "Mole", out _moleBody);
            _rabbit = Spawn(catalog, catalog.Rabbit, "Rabbit", out _rabbitBody);
            _clover = Spawn(catalog, catalog.Clover, "Clover", out _cloverBody);
            _swarm = new GameObject("Swarm").transform;
            _swarm.SetParent(transform, false);
            for (int i = 0; i < Swarm; i++) _locusts[i] = catalog.Spawn(catalog.Locust, _swarm, "Locust" + i).transform;
            _swarm.gameObject.SetActive(false);
            game.Sim.PestArrived += OnArrived;
            game.Sim.PestScared += OnScared;
            game.Sim.PestStruck += OnStruck;
            game.Sim.LuckyFound += OnLucky;
        }

        private void OnDestroy()
        {
            if (_game == null || _game.Sim == null) return;
            _game.Sim.PestArrived -= OnArrived;
            _game.Sim.PestScared -= OnScared;
            _game.Sim.PestStruck -= OnStruck;
            _game.Sim.LuckyFound -= OnLucky;
        }

        private Transform Spawn(VisualCatalog catalog, GameObject prefab, string name, out Transform body)
        {
            var go = catalog.Spawn(prefab, transform, name);
            body = go.transform.Find("Body");
            go.SetActive(false);
            return go.transform;
        }

        private void OnArrived(PestKind kind, GridPos pos)
        {
            _show = 0f;
            if (_game.Sim.IsSimulatingOffline) return;
            _fx.Play(VfxId.SoilPuff, _game.PlotToWorld(pos, 0.1f), kind == PestKind.Locusts ? 1.6f : 1f);
        }

        private void OnScared(PestKind kind, GridPos pos, double coins)
        {
            if (_game.Sim.IsSimulatingOffline) return;
            _fx.Play(VfxId.PlotPop, _game.PlotToWorld(pos, 0.2f));
        }

        private void OnStruck(PestKind kind, GridPos pos)
        {
            if (_game.Sim.IsSimulatingOffline) return;
            _fx.Play(VfxId.SoilPuff, _game.PlotToWorld(pos, 0.15f), kind == PestKind.Locusts ? 2.4f : 1.6f);
        }

        private void OnLucky(LuckyKind kind, double coins)
        {
            if (kind != LuckyKind.Clover || _game.Sim.IsSimulatingOffline) return;
            _fx.Play(VfxId.RipeSparkle, _clover.position + Vector3.up * 0.2f, 1.6f);
        }

        private void LateUpdate()
        {
            var state = _game.State;
            float dt = Time.deltaTime, t = Time.time;
            bool motion = SettingsStore.MotionAllowed;
            var pest = state.Pest;
            bool year = state.Phase == Phase.Year;
            var kind = year ? pest.Kind : PestKind.None;
            _show = Mathf.Min(1f, _show + dt / 0.3f);

            Show(_mole, kind == PestKind.Mole);
            Show(_rabbit, kind == PestKind.Rabbit);
            Show(_swarm, kind == PestKind.Locusts);
            if (kind != PestKind.None)
            {
                var at = _game.PlotToWorld(pest.Pos, 0f);
                switch (kind)
                {
                    case PestKind.Mole:
                    {
                        _mole.position = at;
                        _mole.localScale = Vector3.one * 1.8f; // read at phone size from the field camera
                        // Rises out of the mound, then digs harder as its time runs out.
                        float urgency = Mathf.Clamp01(pest.Timer / Mathf.Max(0.01f, _game.Sim.Config.MoleDigSeconds));
                        float bob = motion ? Mathf.Abs(Mathf.Sin(t * (6f + 10f * urgency))) * 0.03f : 0f;
                        if (_moleBody != null) _moleBody.localPosition = new Vector3(0f, Mathf.Lerp(-0.12f, 0f, Prims.EaseOutBack(_show)) + bob, 0f);
                        break;
                    }
                    case PestKind.Rabbit:
                    {
                        _rabbit.position = at + new Vector3(0.25f, 0f, -0.2f);
                        _rabbit.rotation = Quaternion.LookRotation(at - _rabbit.position, Vector3.up);
                        float hop = motion ? Mathf.Max(0f, Mathf.Sin(t * 7f)) * 0.08f : 0f;
                        if (_rabbitBody != null) _rabbitBody.localPosition = new Vector3(0f, hop, 0f);
                        _rabbit.localScale = Vector3.one * 1.6f * Mathf.Max(0.001f, Prims.EaseOutBack(_show));
                        break;
                    }
                    case PestKind.Locusts:
                    {
                        _swarm.position = at + Vector3.up * 0.35f;
                        float radius = (_game.Sim.Config.LocustRadius + 0.4f) * (1f - 0.5f * pest.Shoo);
                        for (int i = 0; i < Swarm; i++)
                        {
                            float a = (motion ? t * (1.6f + (i % 3) * 0.4f) : 0f) + i * 2.4f;
                            float r = radius * (0.35f + 0.65f * ((i * 37 % 10) / 10f));
                            float h = (motion ? Mathf.Sin(t * 3f + i) * 0.12f : 0f) + (i % 4) * 0.05f;
                            var l = _locusts[i];
                            l.localPosition = new Vector3(Mathf.Cos(a) * r, h, Mathf.Sin(a) * r);
                            l.localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f);
                            l.localScale = Vector3.one * 2.2f;
                        }
                        break;
                    }
                }
            }

            // The clover: pops in, spins, bobs; gone once found or faded.
            var luck = state.Luck;
            bool clover = year && luck.CloverLeft > 0f;
            _cloverShow = Prims.Damp(_cloverShow, clover ? 1f : 0f, 10f, dt);
            Show(_clover, _cloverShow > 0.02f);
            if (_cloverShow > 0.02f)
            {
                _clover.position = _game.PlotToWorld(luck.CloverPos, 0.25f);
                _clover.localScale = Vector3.one * 2f * _cloverShow;
                if (_cloverBody != null)
                {
                    _cloverBody.localRotation = Quaternion.Euler(0f, motion ? t * 90f : 0f, 0f);
                    _cloverBody.localPosition = new Vector3(0f, motion ? Mathf.Sin(t * 2.5f) * 0.04f : 0f, 0f);
                }
            }
        }

        private static void Show(Transform t, bool on)
        {
            if (t != null && t.gameObject.activeSelf != on) t.gameObject.SetActive(on);
        }
    }
}
