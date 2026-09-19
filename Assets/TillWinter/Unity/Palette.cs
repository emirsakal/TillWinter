using UnityEngine;

namespace TillWinter.Unity
{
    /// <summary>Every colour a material can be bound to. Recolouring the game means editing the Palette asset.</summary>
    public enum PaletteSlot
    {
        SoilDry, SoilWet, SoilRing, SoilBlock, Grass, Path,
        Crop0, Crop1, Crop2, Crop3, Crop4, Crop5, Golden,
        Leaf, LeafDark, Sprout, Wood, WoodLight, Stone, Roof, Wall, Metal, Glass, Flower,
        Skin, Cloth0, Cloth1, Cloth2, Cloth3, Cloth4, Cloth5, Hat,
        Water, Snow, Cloud, Crow, Beak, Eye,
        /// <summary>Base map already carries the colour (Kenney colormaps): tint white.</summary>
        White,
    }

    /// <summary>The one colour table (GDD §11). Loaded from Resources/Palette; defaults are the S1–S5 programmer-art colours.</summary>
    [CreateAssetMenu(menuName = "Till Winter/Palette", fileName = "Palette")]
    public sealed class Palette : ScriptableObject
    {
        [Header("Ground")]
        public Color SoilDry = new Color(0.66f, 0.52f, 0.34f);
        public Color SoilWet = new Color(0.36f, 0.24f, 0.14f);
        public Color SoilRing = new Color(0.78f, 0.58f, 0.3f);
        public Color SoilBlock = new Color(0.45f, 0.3f, 0.18f);
        public Color Grass = new Color(0.46f, 0.68f, 0.36f);
        public Color Path = new Color(0.72f, 0.62f, 0.46f);

        [Header("Crops (tier 0..5) and golden")]
        public Color Crop0 = new Color(0.95f, 0.5f, 0.12f);
        public Color Crop1 = new Color(0.88f, 0.16f, 0.14f);
        public Color Crop2 = new Color(0.98f, 0.82f, 0.2f);
        public Color Crop3 = new Color(0.95f, 0.55f, 0.1f);
        public Color Crop4 = new Color(0.45f, 0.2f, 0.55f);
        public Color Crop5 = new Color(1f, 0.85f, 0.35f);
        public Color Golden = new Color(1f, 0.82f, 0.25f);
        /// <summary>Emission for the golden variant (HDR-ish, applied through the binder).</summary>
        public Color GoldenGlow = new Color(0.9f, 0.7f, 0.15f);

        [Header("Foliage and materials")]
        public Color Leaf = new Color(0.32f, 0.62f, 0.27f);
        public Color LeafDark = new Color(0.22f, 0.46f, 0.2f);
        public Color Sprout = new Color(0.45f, 0.75f, 0.3f);
        public Color Wood = new Color(0.5f, 0.34f, 0.2f);
        public Color WoodLight = new Color(0.72f, 0.54f, 0.34f);
        public Color Stone = new Color(0.6f, 0.6f, 0.62f);
        public Color Roof = new Color(0.7f, 0.25f, 0.2f);
        public Color Wall = new Color(0.95f, 0.9f, 0.78f);
        public Color Metal = new Color(0.75f, 0.75f, 0.78f);
        public Color Glass = new Color(0.75f, 0.9f, 1f);
        public Color Flower = new Color(0.95f, 0.4f, 0.6f);

        [Header("Characters")]
        public Color Skin = new Color(0.95f, 0.8f, 0.65f);
        public Color Cloth0 = new Color(0.9f, 0.78f, 0.4f);
        public Color Cloth1 = new Color(0.85f, 0.35f, 0.3f);
        public Color Cloth2 = new Color(0.35f, 0.6f, 0.85f);
        public Color Cloth3 = new Color(0.5f, 0.75f, 0.35f);
        public Color Cloth4 = new Color(0.75f, 0.45f, 0.8f);
        public Color Cloth5 = new Color(0.95f, 0.6f, 0.2f);
        public Color Hat = new Color(0.9f, 0.78f, 0.4f);

        [Header("Weather and animals")]
        public Color Water = new Color(0.4f, 0.65f, 0.95f);
        public Color Snow = new Color(0.93f, 0.95f, 1f);
        public Color Cloud = new Color(0.62f, 0.66f, 0.72f);
        public Color Crow = new Color(0.08f, 0.08f, 0.1f);
        public Color Beak = new Color(0.95f, 0.65f, 0.15f);
        public Color Eye = Color.white;

        [Header("Effects (were literals in the views)")]
        public Color RipeGlow = new Color(0.3f, 0.24f, 0.08f);
        public Color StaleTint = new Color(0.72f, 0.66f, 0.55f);
        public Color WetSheen = new Color(0.85f, 0.92f, 1.15f);
        public Color RingIdle = Color.white;
        public Color RingCombo = new Color(1f, 0.92f, 0.55f);
        public Color CameraClear = new Color(0.55f, 0.75f, 0.55f);

        public Color Get(PaletteSlot slot)
        {
            switch (slot)
            {
                case PaletteSlot.SoilDry: return SoilDry;
                case PaletteSlot.SoilWet: return SoilWet;
                case PaletteSlot.SoilRing: return SoilRing;
                case PaletteSlot.SoilBlock: return SoilBlock;
                case PaletteSlot.Grass: return Grass;
                case PaletteSlot.Path: return Path;
                case PaletteSlot.Crop0: return Crop0;
                case PaletteSlot.Crop1: return Crop1;
                case PaletteSlot.Crop2: return Crop2;
                case PaletteSlot.Crop3: return Crop3;
                case PaletteSlot.Crop4: return Crop4;
                case PaletteSlot.Crop5: return Crop5;
                case PaletteSlot.Golden: return Golden;
                case PaletteSlot.Leaf: return Leaf;
                case PaletteSlot.LeafDark: return LeafDark;
                case PaletteSlot.Sprout: return Sprout;
                case PaletteSlot.Wood: return Wood;
                case PaletteSlot.WoodLight: return WoodLight;
                case PaletteSlot.Stone: return Stone;
                case PaletteSlot.Roof: return Roof;
                case PaletteSlot.Wall: return Wall;
                case PaletteSlot.Metal: return Metal;
                case PaletteSlot.Glass: return Glass;
                case PaletteSlot.Flower: return Flower;
                case PaletteSlot.Skin: return Skin;
                case PaletteSlot.Cloth0: return Cloth0;
                case PaletteSlot.Cloth1: return Cloth1;
                case PaletteSlot.Cloth2: return Cloth2;
                case PaletteSlot.Cloth3: return Cloth3;
                case PaletteSlot.Cloth4: return Cloth4;
                case PaletteSlot.Cloth5: return Cloth5;
                case PaletteSlot.Hat: return Hat;
                case PaletteSlot.Water: return Water;
                case PaletteSlot.Snow: return Snow;
                case PaletteSlot.Cloud: return Cloud;
                case PaletteSlot.Crow: return Crow;
                case PaletteSlot.Beak: return Beak;
                case PaletteSlot.Eye: return Eye;
                default: return Color.white;
            }
        }

        /// <summary>Slots that snow settles on in Winter (exterior surfaces).</summary>
        public static bool IsWeathered(PaletteSlot slot)
        {
            switch (slot)
            {
                case PaletteSlot.Glass: case PaletteSlot.Eye: case PaletteSlot.Water: case PaletteSlot.Cloud: case PaletteSlot.Crow:
                case PaletteSlot.Beak: case PaletteSlot.Skin: case PaletteSlot.SoilBlock: case PaletteSlot.White:
                    return false;
                default: return true;
            }
        }

        /// <summary>Slots that follow the season's leaf/grass tint.</summary>
        public static bool IsSeasonTinted(PaletteSlot slot) => slot == PaletteSlot.Leaf || slot == PaletteSlot.LeafDark || slot == PaletteSlot.Grass;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int WeatheredId = Shader.PropertyToID("_Weathered");
        private static readonly int SeasonTintId = Shader.PropertyToID("_SeasonTint");

        /// <summary>Pushes this palette into the per-slot materials (one material per slot, shared by every prefab). Called at boot and after runtime edits.</summary>
        public void ApplyToMaterials(Material[] slotMaterials)
        {
            if (slotMaterials == null) return;
            for (int i = 0; i < slotMaterials.Length; i++)
            {
                var m = slotMaterials[i];
                if (m == null) continue;
                var slot = (PaletteSlot)i;
                m.SetColor(BaseColorId, slot == PaletteSlot.White ? Color.white : Get(slot));
                m.SetFloat(WeatheredId, IsWeathered(slot) ? 1f : 0f);
                m.SetFloat(SeasonTintId, IsSeasonTinted(slot) ? 1f : 0f);
            }
        }

        public Color Crop(int tier) => Get((PaletteSlot)((int)PaletteSlot.Crop0 + Mathf.Clamp(tier, 0, 5)));
        public Color Cloth(int index) => Get((PaletteSlot)((int)PaletteSlot.Cloth0 + Mathf.Abs(index) % 6));

        private static Palette _loaded;

        public static Palette Load()
        {
            if (_loaded != null) return _loaded;
            _loaded = Resources.Load<Palette>("Palette");
            if (_loaded == null) _loaded = CreateInstance<Palette>();
            return _loaded;
        }
    }
}
