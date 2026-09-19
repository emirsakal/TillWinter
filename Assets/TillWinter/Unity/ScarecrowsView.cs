using System.Collections.Generic;
using UnityEngine;

namespace TillWinter.Unity
{
    /// <summary>
    /// The placed scarecrows (GDD §5.1 v2.0), each standing on its plot corner in the grass between beds. A moved or new
    /// one pops in; all of them lean in the wind.
    /// </summary>
    public sealed class ScarecrowsView : MonoBehaviour
    {
        private GameController _game;
        private VisualCatalog _catalog;
        private readonly List<Transform> _views = new List<Transform>();
        private readonly List<Transform> _bodies = new List<Transform>();
        private readonly List<float> _pop = new List<float>();
        private readonly List<TillWinter.Core.GridPos> _at = new List<TillWinter.Core.GridPos>();

        public void Init(GameController game, VisualCatalog catalog)
        {
            _game = game;
            _catalog = catalog;
        }

        private void LateUpdate()
        {
            var list = _game.State.Scarecrows;
            while (_views.Count < list.Count)
            {
                var go = _catalog.Spawn(_catalog.Scarecrow, transform, "Scarecrow");
                go.name = "Scarecrow " + _views.Count;
                _views.Add(go.transform);
                _bodies.Add(go.transform.Find("Body"));
                _pop.Add(0f);
                _at.Add(new TillWinter.Core.GridPos(-99, -99));
            }
            while (_views.Count > list.Count)
            {
                int last = _views.Count - 1;
                Destroy(_views[last].gameObject);
                _views.RemoveAt(last);
                _bodies.RemoveAt(last);
                _pop.RemoveAt(last);
                _at.RemoveAt(last);
            }
            float dt = Time.deltaTime;
            bool motion = SettingsStore.MotionAllowed;
            for (int i = 0; i < list.Count; i++)
            {
                var c = list[i];
                var world = _game.PlotToWorld(c.X - 0.5f, c.Y - 0.5f, 0f);
                if (c != _at[i])
                {
                    _at[i] = c;
                    _pop[i] = 0f;
                    VfxPlayer.Fire(VfxId.PlotPop, world + Vector3.up * 0.1f);
                }
                _pop[i] = Mathf.Min(1f, _pop[i] + dt / 0.35f);
                var t = _views[i];
                t.position = world;
                t.localScale = Vector3.one * Mathf.Max(0.001f, Prims.EaseOutBack(_pop[i]));
                if (_bodies[i] != null)
                {
                    float sway = motion ? Mathf.Sin(Time.time * 1.3f + i * 1.7f) * 4f : 0f;
                    _bodies[i].localRotation = Quaternion.Euler(0f, 0f, sway);
                }
            }
        }
    }
}
