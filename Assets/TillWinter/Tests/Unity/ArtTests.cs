using System;
using System.Collections.Generic;
using NUnit.Framework;
using TillWinter.Core;
using TillWinter.Unity;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D;

namespace TillWinter.Tests.Unity
{
    /// <summary>Art pass guards: the catalogue is complete, every art material uses TW_Toon, every node icon exists in the atlas, palettes are populated.</summary>
    public class ArtTests
    {
        [Test]
        public void VisualCatalog_IsFullyPopulated()
        {
            var catalog = Resources.Load<VisualCatalog>("VisualCatalog");
            Assert.IsNotNull(catalog, "Resources/VisualCatalog (run art-setup.bat)");
            var missing = new List<string>();
            foreach (var (name, value) in catalog.RequiredEntries())
                if (value == null) missing.Add(name);
            Assert.IsEmpty(missing, "missing catalogue entries: " + string.Join(", ", missing));
            Assert.IsTrue(catalog.Apprentices.Length >= 6, "six apprentice variants");
            Assert.IsTrue(catalog.Trees.Length >= 2, "tree variants");
        }

        [Test]
        public void EveryCatalogPrefab_HasAPaletteBinder_AndOnlyToonMaterials()
        {
            var catalog = Resources.Load<VisualCatalog>("VisualCatalog");
            Assert.IsNotNull(catalog);
            foreach (var (name, value) in catalog.RequiredEntries())
            {
                var go = value as GameObject;
                if (go == null) continue;
                var renderers = go.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) continue;
                Assert.IsNotNull(go.GetComponentInChildren<PaletteBinder>(true), name + " needs a PaletteBinder");
                foreach (var r in renderers)
                    foreach (var m in r.sharedMaterials)
                        Assert.IsTrue(m != null && m.shader != null && m.shader.name == "TillWinter/TW_Toon", name + " uses " + (m != null && m.shader != null ? m.shader.name : "null"));
            }
        }

        [Test]
        public void NoMaterialInArt_UsesAnotherShader()
        {
            var allowed = new HashSet<string> { "TillWinter/TW_Toon", "TillWinter/TW_Sky", "TillWinter/TW_Shadow", "Shader Graphs/Decal" };
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/Art" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".fbx")) continue; // Kenney's embedded materials: read once by ArtSetup for palette slots, referenced by nothing
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                Assert.IsTrue(m.shader != null && (allowed.Contains(m.shader.name) || m.shader.name.StartsWith("UI/")), path + " uses " + (m.shader != null ? m.shader.name : "null"));
            }
        }

        [Test]
        public void EveryNodeIcon_ExistsInTheAtlas()
        {
            var atlas = Resources.Load<SpriteAtlas>("NodeIcons");
            Assert.IsNotNull(atlas, "Resources/NodeIcons atlas (run art-setup.bat)");
            var missing = new List<string>();
            foreach (var n in AlmanacData.Nodes) if (atlas.GetSprite(n.IconKey) == null) missing.Add(n.Id + ":" + n.IconKey);
            foreach (var n in HeritageData.Nodes) if (atlas.GetSprite(n.IconKey) == null) missing.Add(n.Id + ":" + n.IconKey);
            Assert.IsEmpty(missing, "icons missing from the atlas: " + string.Join(", ", missing));
        }

        [Test]
        public void Palette_AndSeasonPalette_ArePopulated()
        {
            var palette = Resources.Load<Palette>("Palette");
            Assert.IsNotNull(palette, "Resources/Palette");
            foreach (PaletteSlot slot in Enum.GetValues(typeof(PaletteSlot)))
                Assert.That(palette.Get(slot).a, Is.GreaterThan(0f), slot.ToString());
            var seasons = Resources.Load<SeasonPalette>("SeasonPalette");
            Assert.IsNotNull(seasons, "Resources/SeasonPalette");
            foreach (Season s in Enum.GetValues(typeof(Season)))
            {
                var look = seasons.For(s);
                Assert.That(look.Intensity, Is.GreaterThan(0f), s + " light");
                Assert.That(look.FogEnd, Is.GreaterThan(look.FogStart), s + " fog");
                Assert.That(look.SkyTop.a, Is.GreaterThan(0f), s + " sky");
            }
            Assert.That(seasons.Winter.SnowAmount, Is.GreaterThan(0.9f), "winter snow");
            Assert.That(seasons.Spring.SnowAmount, Is.LessThan(0.1f), "spring snow");
        }

        [Test]
        public void KenneyKits_HaveLicensesNextToThem()
        {
            foreach (var kit in new[] { "nature-kit", "food-kit", "mini-characters", "game-icons" })
                Assert.IsTrue(System.IO.File.Exists("Assets/Art/Kenney/" + kit + "/License.txt"), kit + " license");
            Assert.IsTrue(System.IO.File.Exists("Assets/Art/LICENSES.md"));
        }
    }
}
