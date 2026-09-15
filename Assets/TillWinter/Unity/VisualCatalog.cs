using System.Collections.Generic;
using UnityEngine;

namespace TillWinter.Unity
{
    /// <summary>Three stage meshes per crop tier, chosen by Wet progress (0–0.33 / 0.33–0.8 / 0.8–1 and Ripe).</summary>
    [System.Serializable]
    public sealed class CropTierVisual
    {
        public GameObject Sprout;
        public GameObject Growing;
        public GameObject Ripe;

        public GameObject Stage(int stage) => stage <= 0 ? Sprout : stage == 1 ? Growing : Ripe;
    }

    /// <summary>
    /// Everything Bootstrap is allowed to spawn (GDD §11, Session 6). Views ask the catalogue for a prefab
    /// and never build primitives themselves. One asset in Resources/VisualCatalog, filled by ArtSetup.
    /// </summary>
    [CreateAssetMenu(menuName = "Till Winter/Visual Catalog", fileName = "VisualCatalog")]
    public sealed class VisualCatalog : ScriptableObject
    {
        [Header("Field")]
        public GameObject Plot;
        public CropTierVisual[] Crops = new CropTierVisual[6];
        /// <summary>Golden variant: same meshes, this material's palette slot + emission through the binder.</summary>
        public Material Golden;
        /// <summary>One TW_Toon material per <see cref="PaletteSlot"/> (index = slot); colours pushed from the Palette at boot.</summary>
        public Material[] SlotMaterials = new Material[0];
        public Material SoilBlock;
        public Material Sky;
        public Material RingDecal;
        public Texture2D RingTexture;
        /// <summary>Set by ArtSetup when the URP decal feature is installed; otherwise the ring is a textured disc.</summary>
        public bool UseDecalRing;

        [Header("Helpers and events")]
        public GameObject[] Apprentices = new GameObject[6];
        public GameObject Tractor;
        public GameObject Crow;
        public GameObject Cloud;
        public GameObject Greenhouse;

        [Header("Diorama and decor")]
        public GameObject[] Houses = new GameObject[3];
        public GameObject Well;
        public GameObject Windmill;
        public GameObject Fence;
        public GameObject FenceGate;
        public GameObject[] Trees = new GameObject[0];
        public GameObject Bush;
        public GameObject Rock;
        public GameObject PathTile;
        public GameObject Signpost;
        public GameObject Flowerbed;
        public GameObject Barrel;
        public GameObject LogStack;
        public GameObject Mushroom;
        public GameObject Stump;

        public Material SlotMaterial(PaletteSlot slot)
        {
            int i = (int)slot;
            return SlotMaterials != null && i < SlotMaterials.Length ? SlotMaterials[i] : null;
        }

        public GameObject Decor(DecorKind kind, int variant = 0)
        {
            switch (kind)
            {
                case DecorKind.Tree: return Trees.Length == 0 ? null : Trees[Mathf.Abs(variant) % Trees.Length];
                case DecorKind.FenceSegment: return Fence;
                case DecorKind.Well: return Well;
                case DecorKind.HouseFloor: return Houses.Length == 0 ? null : Houses[Mathf.Clamp(variant, 0, Houses.Length - 1)];
                case DecorKind.Windmill: return Windmill;
                case DecorKind.Signpost: return Signpost;
                case DecorKind.Flowerbed: return Flowerbed;
                case DecorKind.Barrel: return Barrel;
                case DecorKind.Bush: return Bush;
                case DecorKind.Rock: return Rock;
                case DecorKind.LogStack: return LogStack;
                case DecorKind.Mushroom: return Mushroom;
                case DecorKind.Stump: return Stump;
                case DecorKind.PathTile: return PathTile;
                case DecorKind.FenceGate: return FenceGate;
                default: return null;
            }
        }

        /// <summary>House prefab for a generation: 1–3 small, 4–6 medium, 7+ large.</summary>
        public GameObject HouseFor(int generation) => Decor(DecorKind.HouseFloor, generation <= 3 ? 0 : generation <= 6 ? 1 : 2);

        /// <summary>Instantiates under <paramref name="parent"/>; a missing entry yields an empty object and a warning (never null).</summary>
        public GameObject Spawn(GameObject prefab, Transform parent, string fallbackName)
        {
            if (prefab == null)
            {
                Debug.LogWarning("[VisualCatalog] missing prefab: " + fallbackName);
                var empty = new GameObject(fallbackName);
                empty.transform.SetParent(parent, false);
                return empty;
            }
            var go = Instantiate(prefab, parent);
            go.name = prefab.name;
            return go;
        }

        /// <summary>Every field that must be populated for the game to look right (ThemeTests checks them).</summary>
        public IEnumerable<(string name, Object value)> RequiredEntries()
        {
            yield return ("Plot", Plot);
            for (int t = 0; t < 6; t++)
            {
                var c = t < Crops.Length ? Crops[t] : null;
                yield return ("Crop" + t + ".Sprout", c?.Sprout);
                yield return ("Crop" + t + ".Growing", c?.Growing);
                yield return ("Crop" + t + ".Ripe", c?.Ripe);
            }
            yield return ("Golden", Golden);
            foreach (PaletteSlot slot in System.Enum.GetValues(typeof(PaletteSlot)))
                yield return ("Material." + slot, SlotMaterial(slot));
            yield return ("SoilBlock", SoilBlock);
            yield return ("Sky", Sky);
            yield return ("RingDecal", RingDecal);
            for (int i = 0; i < 6; i++) yield return ("Apprentice" + i, i < Apprentices.Length ? Apprentices[i] : null);
            yield return ("Tractor", Tractor);
            yield return ("Crow", Crow);
            yield return ("Cloud", Cloud);
            yield return ("Greenhouse", Greenhouse);
            for (int i = 0; i < 3; i++) yield return ("House" + i, i < Houses.Length ? Houses[i] : null);
            foreach (DecorKind k in System.Enum.GetValues(typeof(DecorKind)))
                yield return ("Decor." + k, Decor(k));
        }

        private static VisualCatalog _loaded;

        public static VisualCatalog Load()
        {
            if (_loaded != null) return _loaded;
            _loaded = Resources.Load<VisualCatalog>("VisualCatalog");
            if (_loaded == null)
            {
                Debug.LogWarning("[VisualCatalog] Resources/VisualCatalog missing; run art-setup.bat");
                _loaded = CreateInstance<VisualCatalog>();
            }
            return _loaded;
        }
    }
}
