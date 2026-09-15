using System.Collections.Generic;
using UnityEngine;

namespace TillWinter.Unity
{
    /// <summary>Every effect the game can play. Adding one = a row in the catalogue (built by FeelSetup) + a Play call.</summary>
    public enum VfxId
    {
        WaterSplash, SoilRipple, Sprout, RipeSparkle,
        Harvest, HarvestGolden,
        Feathers, SoilPuff,
        RainDrops, RainSweep,
        TractorDust, TractorExhaust, StepDust,
        PlotPop,
        Petals, Leaves, Snow,
        RetireSnow, MeltSparkle,
        GoldMotes,
    }

    /// <summary>One pooled particle prefab per <see cref="VfxId"/>; colours come from the Palette at play time.</summary>
    [System.Serializable]
    public sealed class VfxEntry
    {
        public VfxId Id;
        public GameObject Prefab;
        public int PoolSize = 2;
        /// <summary>Particles emitted per Play at intensity 1.</summary>
        public int BaseCount = 10;
        public PaletteSlot Color = PaletteSlot.White;
        /// <summary>Second palette colour mixed in randomly (0 = none).</summary>
        public PaletteSlot Color2 = PaletteSlot.White;
        public bool UseColor2;
        /// <summary>Triggers per second before merging (RateLimiter).</summary>
        public int MaxPerSecond = 30;
        /// <summary>Looping/ambient systems are driven by emission rate instead of Emit.</summary>
        public bool Continuous;
    }

    [CreateAssetMenu(menuName = "Till Winter/Vfx Catalog", fileName = "VfxCatalog")]
    public sealed class VfxCatalog : ScriptableObject
    {
        public List<VfxEntry> Entries = new List<VfxEntry>();

        public VfxEntry Get(VfxId id)
        {
            for (int i = 0; i < Entries.Count; i++) if (Entries[i].Id == id) return Entries[i];
            return null;
        }

        private static VfxCatalog _loaded;

        public static VfxCatalog Load()
        {
            if (_loaded != null) return _loaded;
            _loaded = Resources.Load<VfxCatalog>("VfxCatalog");
            if (_loaded == null)
            {
                Debug.LogWarning("[VfxCatalog] Resources/VfxCatalog missing; run feel-setup.bat");
                _loaded = CreateInstance<VfxCatalog>();
            }
            return _loaded;
        }
    }
}
