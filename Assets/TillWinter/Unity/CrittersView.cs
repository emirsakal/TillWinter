using TillWinter.Core;
using UnityEngine;

namespace TillWinter.Unity
{
    /// <summary>
    /// Two butterflies looping lazily over the field in Spring and Summer; they shrink away in Autumn and Winter.
    /// Small life so the farm is not still when the player is not touching it.
    /// </summary>
    public sealed class CrittersView : MonoBehaviour
    {
        private const int Count = 2;

        private GameController _game;
        private readonly Transform[] _butterflies = new Transform[Count];
        private readonly Transform[] _wingL = new Transform[Count];
        private readonly Transform[] _wingR = new Transform[Count];
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
        }
    }
}
