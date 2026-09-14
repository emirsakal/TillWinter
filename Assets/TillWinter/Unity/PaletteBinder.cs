using System.Collections.Generic;
using UnityEngine;

namespace TillWinter.Unity
{
    /// <summary>
    /// Records which palette slot each renderer of a prefab uses and applies per-object overrides
    /// (plot soil state, ring lift, golden glow) through one MaterialPropertyBlock per renderer.
    /// Untouched renderers keep no block, so identical meshes GPU-instance; the slot colour itself lives on the
    /// shared per-slot material (see <see cref="Palette.ApplyToMaterials"/>). Season snow and leaf tint are shader globals.
    /// </summary>
    public sealed class PaletteBinder : MonoBehaviour
    {
        [System.Serializable]
        public sealed class Binding
        {
            public Renderer Renderer;
            public int MaterialIndex;
            public PaletteSlot Slot;
            public Color Tint = Color.white;
            public bool Weathered = true;
        }

        public List<Binding> Bindings = new List<Binding>();

        private Color _tintMul = Color.white;
        private Color _emission = Color.black;
        private readonly Dictionary<PaletteSlot, Color> _overrides = new Dictionary<PaletteSlot, Color>();
        private bool _dirty = true;
        private bool _hadBlocks;

        private static MaterialPropertyBlock _mpb;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        private static readonly int SnowId = Shader.PropertyToID("_TW_Snow");
        private static readonly int SeasonTintId = Shader.PropertyToID("_TW_SeasonTint");

        /// <summary>Season state shared by every material (set by SeasonPresenter).</summary>
        public static float SnowAmount { get; private set; }
        public static Color LeafTint { get; private set; } = Color.white;

        public static void SetSeason(float snow, Color leafTint, Color grassTint)
        {
            SnowAmount = snow;
            LeafTint = leafTint;
            Shader.SetGlobalFloat(SnowId, snow);
            Shader.SetGlobalColor(SeasonTintId, leafTint);
        }

        private void LateUpdate()
        {
            if (_dirty) Apply();
        }

        public void Add(Renderer r, PaletteSlot slot, Color? tint = null, bool weathered = true, int materialIndex = 0)
        {
            Bindings.Add(new Binding { Renderer = r, MaterialIndex = materialIndex, Slot = slot, Tint = tint ?? Color.white, Weathered = weathered });
            _dirty = true;
        }

        /// <summary>Multiplies every bound colour (ring lift = brighter, vanish = darker).</summary>
        public void SetTintMultiplier(Color mul)
        {
            if (mul == _tintMul) return;
            _tintMul = mul;
            _dirty = true;
        }

        public void SetEmission(Color emission)
        {
            if (emission == _emission) return;
            _emission = emission;
            _dirty = true;
        }

        /// <summary>Replaces one slot's colour on this object only (plot Dry/Wet/ring soil, golden crop).</summary>
        public void Override(PaletteSlot slot, Color color)
        {
            if (_overrides.TryGetValue(slot, out var c) && c == color) return;
            _overrides[slot] = color;
            _dirty = true;
        }

        public void ClearOverride(PaletteSlot slot)
        {
            if (_overrides.Remove(slot)) _dirty = true;
        }

        public void Apply()
        {
            _dirty = false;
            bool custom = _overrides.Count > 0 || _tintMul != Color.white || _emission != Color.black;
            if (!custom)
            {
                if (_hadBlocks)
                    for (int i = 0; i < Bindings.Count; i++)
                        if (Bindings[i].Renderer != null) Bindings[i].Renderer.SetPropertyBlock(null);
                _hadBlocks = false;
                return;
            }
            var palette = Palette.Load();
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            Renderer last = null;
            for (int i = 0; i < Bindings.Count; i++)
            {
                var b = Bindings[i];
                if (b.Renderer == null || b.Renderer == last) continue; // one block per renderer (first binding wins)
                last = b.Renderer;
                Color c = _overrides.TryGetValue(b.Slot, out var o) ? o : b.Slot == PaletteSlot.White ? Color.white : palette.Get(b.Slot);
                c *= b.Tint;
                c *= _tintMul;
                c.a = 1f;
                _mpb.Clear();
                _mpb.SetColor(BaseColorId, c);
                _mpb.SetColor(EmissionId, _emission);
                b.Renderer.SetPropertyBlock(_mpb);
            }
            _hadBlocks = true;
        }
    }
}
