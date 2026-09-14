using UnityEngine;
using UnityEngine.Rendering;

namespace TillWinter.Unity
{
    /// <summary>World-space particle bursts built from mesh particles (no textures needed).</summary>
    public sealed class FxManager : MonoBehaviour
    {
        private ParticleSystem _puff;
        private ParticleSystem _splash;
        private ParticleSystem[] _harvest;

        public void Init()
        {
            _puff = MakeBurst("Puff", new Color(0.8f, 0.8f, 0.82f), 0.14f, 0.5f, 0.9f, -0.1f);
            _splash = MakeBurst("Splash", new Color(0.45f, 0.7f, 1f), 0.07f, 0.35f, 1.6f, 1.5f);
            _harvest = new[]
            {
                MakeBurst("HarvestCarrot", new Color(1f, 0.6f, 0.2f), 0.07f, 0.35f, 2.2f, 1.2f),
                MakeBurst("HarvestTomato", new Color(0.95f, 0.25f, 0.2f), 0.08f, 0.4f, 2.2f, 1.2f),
                MakeBurst("HarvestCorn", new Color(1f, 0.85f, 0.25f), 0.08f, 0.45f, 2.4f, 1.2f),
                MakeBurst("HarvestPumpkin", new Color(0.95f, 0.55f, 0.1f), 0.09f, 0.45f, 2.4f, 1.2f),
                MakeBurst("HarvestGrapes", new Color(0.55f, 0.25f, 0.65f), 0.08f, 0.45f, 2.4f, 1.2f),
                MakeBurst("HarvestWheat", new Color(1f, 0.9f, 0.4f), 0.08f, 0.5f, 2.6f, 1.2f),
            };
        }

        public void Puff(Vector3 at)
        {
            _puff.transform.position = at;
            _puff.Emit(14);
        }

        public void WaterSplash(Vector3 at)
        {
            _splash.transform.position = at;
            _splash.Emit(10);
        }

        public void HarvestBurst(Vector3 at, int tier)
        {
            var ps = _harvest[Mathf.Clamp(tier, 0, _harvest.Length - 1)];
            ps.transform.position = at;
            ps.Emit(6 + 2 * Mathf.Clamp(tier, 0, 3));
        }

        private ParticleSystem MakeBurst(string name, Color color, float size, float life, float speed, float gravity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = life;
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.5f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
            main.gravityModifier = gravity;
            main.maxParticles = 300;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.12f;
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Mesh;
            r.mesh = Prims.BuiltinMesh(PrimitiveType.Sphere);
            r.sharedMaterial = Prims.Lit(color, 0.3f);
            r.shadowCastingMode = ShadowCastingMode.Off;
            return ps;
        }
    }
}
