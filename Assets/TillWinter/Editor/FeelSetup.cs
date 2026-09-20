using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TillWinter.Unity;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace TillWinter.EditorTools
{
    /// <summary>
    /// Feel setup (feel-setup.bat / menu): builds one pooled particle prefab per <see cref="VfxId"/> into Assets/Art/Vfx,
    /// the VfxCatalog asset, and the Master/SFX/Ambience audio mixer (Resources/TillWinterMixer) with exposed volumes.
    /// Idempotent; prefabs are rebuilt every run.
    /// </summary>
    public static class FeelSetup
    {
        private const string VfxDir = "Assets/Art/Vfx/";
        private const string ResourcesDir = "Assets/TillWinter/Unity/Resources/";
        private const string MixerPath = ResourcesDir + AudioManager.MixerName + ".mixer";

        private static Material _particleMaterial, _softMaterial;

        public static void Run()
        {
            int code = 0;
            try { RunAll(); }
            catch (Exception e) { Debug.LogError("[FeelSetup] failed: " + e); code = 1; }
            EditorApplication.Exit(code);
        }

        [MenuItem("Till Winter/Feel setup (vfx, mixer)")]
        public static void RunAll()
        {
            Directory.CreateDirectory(VfxDir);
            Directory.CreateDirectory(ResourcesDir);
            CreateMixer();
            var visuals = AssetDatabase.LoadAssetAtPath<VisualCatalog>(ResourcesDir + "VisualCatalog.asset");
            _particleMaterial = visuals != null ? visuals.SlotMaterial(PaletteSlot.White) : null;
            if (_particleMaterial == null) throw new Exception("VisualCatalog / TW_White material missing: run art-setup.bat first");
            _softMaterial = visuals.ParticleSoft;
            if (_softMaterial == null) throw new Exception("VisualCatalog.ParticleSoft missing: run art-setup.bat first");
            var catalog = AssetDatabase.LoadAssetAtPath<VfxCatalog>(ResourcesDir + "VfxCatalog.asset");
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<VfxCatalog>();
                AssetDatabase.CreateAsset(catalog, ResourcesDir + "VfxCatalog.asset");
            }
            catalog.Entries.Clear();
            BuildEntries(catalog);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[FeelSetup] done: " + catalog.Entries.Count + " vfx entries");
        }

        // ------------------------------------------------------------------ vfx

        private sealed class Spec
        {
            public VfxId Id;
            public PrimitiveType Mesh = PrimitiveType.Sphere;
            public float Size = 0.08f, SizeMax = 0f, Life = 0.4f, Speed = 1f, Gravity = 1f;
            public int Count = 10, Pool = 2, PerSecond = 30;
            public PaletteSlot Color = PaletteSlot.White, Color2 = PaletteSlot.White;
            public bool UseColor2, Continuous, Flat, Grow, Noise;
            public Vector3 Box = Vector3.zero; // zero = sphere shape (radius 0.12)
            public float Height; // spawn height for boxes
        }

        private static void BuildEntries(VfxCatalog catalog)
        {
            var specs = new[]
            {
                new Spec { Id = VfxId.WaterSplash, Size = 0.07f, Life = 0.35f, Speed = 1.6f, Gravity = 1.5f, Count = 10, Color = PaletteSlot.Water, PerSecond = 10 },
                new Spec { Id = VfxId.SoilRipple, Mesh = PrimitiveType.Quad, Flat = true, Grow = true, Size = 0.5f, Life = 0.45f, Speed = 0f, Gravity = 0f, Count = 1, Color = PaletteSlot.SoilWet, PerSecond = 10, Pool = 3 },
                new Spec { Id = VfxId.Sprout, Size = 0.05f, Life = 0.3f, Speed = 1f, Gravity = 0.8f, Count = 6, Color = PaletteSlot.Sprout, PerSecond = 10 },
                new Spec { Id = VfxId.RipeSparkle, Size = 0.05f, Life = 0.5f, Speed = 0.6f, Gravity = -0.2f, Count = 5, Color = PaletteSlot.Snow, Color2 = PaletteSlot.Golden, UseColor2 = true, PerSecond = 12 },
                new Spec { Id = VfxId.Harvest, Size = 0.08f, Life = 0.4f, Speed = 2.2f, Gravity = 1.2f, Count = 8, Color = PaletteSlot.Crop0, PerSecond = 12, Pool = 3 },
                new Spec { Id = VfxId.HarvestGolden, Size = 0.09f, Life = 0.6f, Speed = 2.8f, Gravity = 1f, Count = 24, Color = PaletteSlot.Golden, Color2 = PaletteSlot.Snow, UseColor2 = true, PerSecond = 4 },
                new Spec { Id = VfxId.Feathers, Mesh = PrimitiveType.Cube, Size = 0.07f, Life = 0.9f, Speed = 1.2f, Gravity = 0.3f, Count = 10, Color = PaletteSlot.Crow, Color2 = PaletteSlot.Stone, UseColor2 = true, PerSecond = 4 },
                new Spec { Id = VfxId.SoilPuff, Size = 0.14f, Life = 0.5f, Speed = 0.9f, Gravity = -0.1f, Count = 12, Color = PaletteSlot.SoilDry, PerSecond = 6 },
                new Spec { Id = VfxId.RainDrops, Continuous = true, Size = 0.04f, Life = 0.6f, Speed = 0.2f, Gravity = 3f, Color = PaletteSlot.Water, Box = new Vector3(0.9f, 0.1f, 0.6f), Pool = 1 },
                new Spec { Id = VfxId.RainSweep, Size = 0.05f, Life = 0.9f, Speed = 0.4f, Gravity = 2.5f, Count = 60, Color = PaletteSlot.Water, Box = new Vector3(6.5f, 0.2f, 6.5f), Height = 1.6f, PerSecond = 2, Pool = 1 },
                new Spec { Id = VfxId.TractorDust, Size = 0.1f, Life = 0.7f, Speed = 0.5f, Gravity = -0.05f, Count = 4, Color = PaletteSlot.Path, PerSecond = 12 },
                new Spec { Id = VfxId.TractorExhaust, Size = 0.08f, Life = 0.8f, Speed = 0.8f, Gravity = -0.3f, Count = 6, Color = PaletteSlot.Stone, PerSecond = 4 },
                new Spec { Id = VfxId.StepDust, Size = 0.06f, Life = 0.4f, Speed = 0.4f, Gravity = -0.05f, Count = 2, Color = PaletteSlot.Path, PerSecond = 12, Pool = 3 },
                new Spec { Id = VfxId.PlotPop, Size = 0.08f, Life = 0.45f, Speed = 1.2f, Gravity = 0.4f, Count = 10, Color = PaletteSlot.SoilDry, PerSecond = 12, Pool = 3 },
                new Spec { Id = VfxId.Petals, Continuous = true, Mesh = PrimitiveType.Cube, Size = 0.06f, Life = 6f, Speed = 0.3f, Gravity = 0.02f, Color = PaletteSlot.Flower, Color2 = PaletteSlot.Snow, UseColor2 = true, Box = new Vector3(10f, 0.5f, 10f), Height = 6f, Noise = true, Pool = 1 },
                new Spec { Id = VfxId.Leaves, Continuous = true, Mesh = PrimitiveType.Cube, Size = 0.08f, Life = 6f, Speed = 0.3f, Gravity = 0.03f, Color = PaletteSlot.Crop3, Color2 = PaletteSlot.Crop2, UseColor2 = true, Box = new Vector3(10f, 0.5f, 10f), Height = 6f, Noise = true, Pool = 1 },
                new Spec { Id = VfxId.Snow, Continuous = true, Size = 0.05f, SizeMax = 0.11f, Life = 9f, Speed = 0.15f, Gravity = 0.045f, Color = PaletteSlot.Snow, Box = new Vector3(10f, 0.5f, 10f), Height = 6f, Noise = true, Pool = 1 },
                new Spec { Id = VfxId.RetireSnow, Size = 0.08f, Life = 1.5f, Speed = 1f, Gravity = 0.3f, Count = 80, Color = PaletteSlot.Snow, Box = new Vector3(6f, 0.5f, 6f), Height = 3f, PerSecond = 1, Pool = 1 },
                new Spec { Id = VfxId.MeltSparkle, Size = 0.06f, Life = 0.8f, Speed = 0.8f, Gravity = -0.3f, Count = 40, Color = PaletteSlot.Snow, Color2 = PaletteSlot.Golden, UseColor2 = true, Box = new Vector3(6f, 0.2f, 6f), Height = 0.2f, PerSecond = 1, Pool = 1 },
                new Spec { Id = VfxId.StormRain, Continuous = true, Size = 0.035f, SizeMax = 0.05f, Life = 1.0f, Speed = 0.2f, Gravity = 1.5f, Color = PaletteSlot.Water, Box = new Vector3(10f, 0.5f, 10f), Height = 6f, Pool = 1 },
                new Spec { Id = VfxId.GoldMotes, Continuous = true, Size = 0.04f, SizeMax = 0.08f, Life = 7f, Speed = 0.12f, Gravity = -0.02f, Color = PaletteSlot.Golden, Color2 = PaletteSlot.Snow, UseColor2 = true, Box = new Vector3(10f, 0.5f, 10f), Height = 0.3f, Noise = true, Pool = 1 },
            };
            foreach (var s in specs)
            {
                var prefab = BuildPrefab(s);
                catalog.Entries.Add(new VfxEntry
                {
                    Id = s.Id, Prefab = prefab, PoolSize = s.Pool, BaseCount = s.Count, Color = s.Color, Color2 = s.Color2,
                    UseColor2 = s.UseColor2, MaxPerSecond = s.PerSecond, Continuous = s.Continuous,
                });
            }
            foreach (VfxId id in Enum.GetValues(typeof(VfxId)))
                if (catalog.Get(id) == null) throw new Exception("no spec for " + id);
        }

        private static GameObject BuildPrefab(Spec s)
        {
            var go = new GameObject("Vfx" + s.Id);
            if (s.Flat) go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = s.Continuous;
            main.playOnAwake = false;
            main.startLifetime = s.Life;
            main.startSpeed = s.Continuous ? new ParticleSystem.MinMaxCurve(s.Speed) : new ParticleSystem.MinMaxCurve(s.Speed * 0.5f, s.Speed);
            main.startSize = s.SizeMax > 0f ? new ParticleSystem.MinMaxCurve(s.Size, s.SizeMax) : new ParticleSystem.MinMaxCurve(s.Size * 0.7f, s.Size);
            main.gravityModifier = s.Gravity;
            main.maxParticles = s.Continuous ? 1200 : 300;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startRotation3D = !s.Flat && s.Mesh == PrimitiveType.Cube;
            if (main.startRotation3D)
            {
                main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            }
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var shape = ps.shape;
            if (s.Box != Vector3.zero)
            {
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = s.Box;
                shape.position = new Vector3(0f, s.Height, 0f);
            }
            else
            {
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.12f;
            }
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = s.Grow
                ? new ParticleSystem.MinMaxCurve(2.4f, AnimationCurve.EaseInOut(0f, 0.2f, 1f, 1f))
                : new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));
            if (s.Noise)
            {
                var noise = ps.noise;
                noise.enabled = true;
                noise.strength = 0.25f;
                noise.frequency = 0.4f;
            }
            if (s.Continuous && s.Mesh == PrimitiveType.Cube)
            {
                var rot = ps.rotationOverLifetime;
                rot.enabled = true;
                rot.z = new ParticleSystem.MinMaxCurve(-2f, 2f);
            }
            var r = ps.GetComponent<ParticleSystemRenderer>();
            // Cubes stay solid: petals, leaves and feathers are flakes and want an edge. Everything round becomes a
            // soft billboard, which is what a sparkle, a splash or a puff of dust actually looks like.
            bool flake = s.Mesh == PrimitiveType.Cube;
            if (flake)
            {
                r.renderMode = ParticleSystemRenderMode.Mesh;
                r.mesh = BuiltinMesh(s.Mesh);
                r.sharedMaterial = _particleMaterial;
            }
            else
            {
                r.renderMode = ParticleSystemRenderMode.Billboard;
                r.mesh = null;
                r.sharedMaterial = _softMaterial;
            }
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.alignment = s.Flat && flake ? ParticleSystemRenderSpace.Local : ParticleSystemRenderSpace.View;
            string path = VfxDir + go.name + ".prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static readonly Dictionary<PrimitiveType, Mesh> Meshes = new Dictionary<PrimitiveType, Mesh>();

        private static Mesh BuiltinMesh(PrimitiveType type)
        {
            if (Meshes.TryGetValue(type, out var m) && m != null) return m;
            // Unity's sphere is ~760 triangles; a spark a few pixels wide needs 80. With a few hundred particles alive
            // the built-in one alone was over the smoke test's whole triangle budget.
            if (type == PrimitiveType.Sphere) return Meshes[type] = LowSphere();
            var go = GameObject.CreatePrimitive(type);
            m = go.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(go);
            Meshes[type] = m;
            return m;
        }

        /// <summary>A once-subdivided icosahedron (80 triangles, flat-ish shading suits the toon look), saved next to the prefabs.</summary>
        private static Mesh LowSphere()
        {
            const string path = VfxDir + "LowSphere.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var verts = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            int[] ico =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            var mid = new Dictionary<long, int>();
            int Mid(int a, int b)
            {
                long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (mid.TryGetValue(key, out int i)) return i;
                verts.Add((verts[a] + verts[b]) * 0.5f);
                return mid[key] = verts.Count - 1;
            }
            var tris = new List<int>();
            for (int i = 0; i < ico.Length; i += 3)
            {
                int a = ico[i], b = ico[i + 1], c = ico[i + 2];
                int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                tris.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }
            for (int i = 0; i < verts.Count; i++) verts[i] = verts[i].normalized * 0.5f; // unit diameter, like the built-in sphere
            // Every face outward in Unity's winding (the cross product points away from the centre), whatever the table's order.
            for (int i = 0; i < tris.Count; i += 3)
            {
                Vector3 a = verts[tris[i]], b = verts[tris[i + 1]], c = verts[tris[i + 2]];
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), a + b + c) < 0f) { int k = tris[i + 1]; tris[i + 1] = tris[i + 2]; tris[i + 2] = k; }
            }
            var mesh = existing != null ? existing : new Mesh();
            mesh.Clear();
            mesh.name = "LowSphere";
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            if (existing == null) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        // ------------------------------------------------------------------ mixer (editor-internal API through reflection)

        private static void CreateMixer()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (existing != null)
            {
                if (existing.FindMatchingGroups("Music").Length > 0) { Debug.Log("[FeelSetup] mixer already present"); return; }
                AssetDatabase.DeleteAsset(MixerPath); // older mixer from before the music bus
            }
            var asm = typeof(Editor).Assembly;
            var ctrlType = asm.GetType("UnityEditor.Audio.AudioMixerController");
            var groupType = asm.GetType("UnityEditor.Audio.AudioMixerGroupController");
            var expType = asm.GetType("UnityEditor.Audio.ExposedAudioParameter");
            if (ctrlType == null || groupType == null || expType == null) { Debug.LogWarning("[FeelSetup] mixer editor API not found; AudioManager runs without a mixer"); return; }
            const BindingFlags any = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var create = ctrlType.GetMethod("CreateMixerControllerAtPath", any);
            var controller = create.Invoke(null, new object[] { MixerPath });
            var master = ctrlType.GetProperty("masterGroup", any).GetValue(controller);
            var newGroup = ctrlType.GetMethod("CreateNewGroup", any);
            var addChild = ctrlType.GetMethod("AddChildToParent", any);
            var sfx = newGroup.Invoke(controller, new object[] { "SFX", false });
            var ambience = newGroup.Invoke(controller, new object[] { "Ambience", false });
            var music = newGroup.Invoke(controller, new object[] { "Music", false });
            addChild.Invoke(controller, new[] { sfx, master });
            addChild.Invoke(controller, new[] { ambience, master });
            addChild.Invoke(controller, new[] { music, master });

            var getVolumeGuid = groupType.GetMethod("GetGUIDForVolume", any);
            var exposed = Array.CreateInstance(expType, 4);
            void Expose(int i, object group, string name)
            {
                var p = Activator.CreateInstance(expType);
                expType.GetField("guid").SetValue(p, getVolumeGuid.Invoke(group, null));
                expType.GetField("name").SetValue(p, name);
                exposed.SetValue(p, i);
            }
            Expose(0, master, "MasterVolume");
            Expose(1, sfx, "SfxVolume");
            Expose(2, ambience, "AmbienceVolume");
            Expose(3, music, "MusicVolume");
            ctrlType.GetProperty("exposedParameters", any).SetValue(controller, exposed);
            EditorUtility.SetDirty((Object)controller);
            AssetDatabase.SaveAssets();
            Debug.Log("[FeelSetup] mixer created with Master/SFX/Ambience/Music and exposed volumes");
        }
    }
}
