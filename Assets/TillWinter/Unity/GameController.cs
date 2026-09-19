using TillWinter.Core;
using UnityEngine;

namespace TillWinter.Unity
{
    /// <summary>
    /// Owns the <see cref="FarmSim"/>, ticks it once per frame with the current ring input, and maps
    /// between plot space and world space. No game rules live here.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class GameController : MonoBehaviour
    {
        public FarmSim Sim { get; private set; }
        public FarmState State => Sim.State;

        [Range(0.5f, 8f)] public float TimeScale = 1f;
        /// <summary>Plots the ring is pushed toward the top of the screen so the finger does not cover it.</summary>
        public float RingOffsetPlots = 0.8f;
        /// <summary>Set by the winter shop while it is open.</summary>
        public bool InputBlocked;
        /// <summary>Pause menu open: no sim tick (year timer, crows, helpers all stop) and Time.timeScale 0 for views.</summary>
        public bool Paused { get; private set; }

        public void SetPaused(bool paused)
        {
            Paused = paused;
            Time.timeScale = paused ? 0f : 1f;
        }
        public Camera Cam;
        public PointerInput Pointer;
        /// <summary>Debug/smoke-test hook: a held pointer at this screen position (bypasses the Input System).</summary>
        public Vector2? DebugPointerScreen;
        /// <summary>Debug/smoke-test hook: a one-shot tap at this screen position; cleared after use.</summary>
        public Vector2? DebugTapScreen;
        /// <summary>Set by CloudView: is this screen point on the rain cloud?</summary>
        public System.Func<Vector2, bool> CloudHitTest;
        /// <summary>Set by DogView: is this screen point on the dog? Returns true when it took the tap.</summary>
        public System.Func<Vector2, bool> DogHitTest;
        /// <summary>Set by the HUD while a seed is picked from the bag: plants on the tapped plot. A crow still takes the tap first.</summary>
        public System.Func<GridPos, bool> PlotTapOverride;
        /// <summary>Set by the HUD while a scarecrow is being placed: gets the tap in plot space (corners between beds count).</summary>
        public System.Func<Vector2, bool> FieldTapOverride;
        /// <summary>Set by ApprenticesView: which apprentice is under this screen point, or -1.</summary>
        public System.Func<Vector2, int> ApprenticeHitTest;
        /// <summary>A tapped apprentice switched role (GDD §4.2 v2.0): its index.</summary>
        public event System.Action<int> ApprenticeRoleToggled;
        /// <summary>Hands-free play (GDD §10.6 v2.5): the ring a tap has placed, stepping on its own.</summary>
        public readonly AutoRing HandsFree = new AutoRing();

        /// <summary>Sim seconds elapsed (respects TimeScale). Use for animation that should follow the sim.</summary>
        public float SimTime { get; private set; }
        public RingInput? CurrentRing { get; private set; }
        private readonly Plane _ground = new Plane(Vector3.up, Vector3.zero);

        public void Init(FarmConfig config, int seed)
        {
            Sim = new FarmSim(config, seed);
        }

        public void InitFrom(FarmSim sim)
        {
            Sim = sim;
        }

        public Vector3 PlotToWorld(float px, float py, float y = 0f)
        {
            float c = (State.GridSize - 1) * 0.5f;
            return new Vector3(px - c, y, py - c);
        }

        public Vector3 PlotToWorld(GridPos pos, float y = 0f) => PlotToWorld(pos.X, pos.Y, y);

        public Vector2 WorldToPlot(Vector3 world)
        {
            float c = (State.GridSize - 1) * 0.5f;
            return new Vector2(world.x + c, world.z + c);
        }

        public bool TryScreenToPlot(Vector2 screen, out Vector2 plot)
        {
            plot = default;
            if (Cam == null) return false;
            var ray = Cam.ScreenPointToRay(screen);
            if (!_ground.Raycast(ray, out float dist)) return false;
            plot = WorldToPlot(ray.GetPoint(dist));
            return true;
        }

        private void Update()
        {
            if (Sim == null) return;
            if (Paused) { CurrentRing = null; return; }
            Sim.AddPlayTime(Time.unscaledDeltaTime); // stats: time played (not while paused or closed)
            float dt = Mathf.Min(Time.deltaTime, 0.1f) * TimeScale;

            RingInput? ring = null;
            if (!InputBlocked && Pointer != null)
            {
                var s = Pointer.Current;
                if (DebugPointerScreen.HasValue)
                {
                    s.IsDown = true;
                    s.Position = DebugPointerScreen.Value;
                }
                if (DebugTapScreen.HasValue)
                {
                    s.Tapped = true;
                    s.TapPosition = DebugTapScreen.Value;
                    DebugTapScreen = null;
                }
                bool fingerRing = false;
                if (s.IsDown && TryScreenToPlot(s.Position, out var p))
                {
                    ring = new RingInput(p.x, p.y + RingOffsetPlots);
                    fingerRing = true;
                }
                if (s.Tapped && CloudHitTest != null && CloudHitTest(s.TapPosition) && Sim.TapCloud())
                {
                    s.Tapped = false;
                }
                // The dog is pure decoration: it swallows its own tap so petting it never waters the plot behind it.
                if (s.Tapped && DogHitTest != null && DogHitTest(s.TapPosition))
                {
                    s.Tapped = false;
                }
                if (s.Tapped && FieldTapOverride != null && TryScreenToPlot(s.TapPosition, out var fp) && FieldTapOverride(new Vector2(fp.x, fp.y)))
                {
                    s.Tapped = false;
                }
                // Tapping an apprentice switches it between harvesting and watering.
                if (s.Tapped && ApprenticeHitTest != null)
                {
                    int index = ApprenticeHitTest(s.TapPosition);
                    if (index >= 0)
                    {
                        s.Tapped = false;
                        var role = State.Apprentices[index].Role == ApprenticeRole.Waterer ? ApprenticeRole.Harvester : ApprenticeRole.Waterer;
                        if (Sim.SetApprenticeRole(index, role)) ApprenticeRoleToggled?.Invoke(index);
                    }
                }
                bool homeTap = false;
                GridPos home = default;
                if (s.Tapped && TryScreenToPlot(s.TapPosition, out var tp))
                {
                    var gp = new GridPos(Mathf.RoundToInt(tp.x), Mathf.RoundToInt(tp.y));
                    if (State.InBounds(gp))
                    {
                        bool pestHere = State.Pest.Kind != PestKind.None && State.Pest.Pos == gp;
                        bool crowHere = State.GetPlot(gp).HasCrow;
                        if (PlotTapOverride != null && !crowHere && !pestHere) PlotTapOverride(gp);
                        else
                        {
                            Sim.TapAt(gp);
                            homeTap = PlotTapOverride == null && !crowHere && !pestHere; // a tap that did nothing else
                            home = gp;
                        }
                    }
                }
                if (!fingerRing && SettingsStore.Current.HandsFree)
                {
                    // Hands-free (GDD §10.6 v2.5): a plain tap on a plot moves the ring's home; taps that planted a seed,
                    // moved a scarecrow, chased a crow or pest or touched the cloud, dog or an apprentice leave it be.
                    if (homeTap) HandsFree.Place(home.X, home.Y);
                    HandsFree.KeepOnField(State.GridSize); // a smaller field after a retire or respec
                    ring = HandsFree.Step(State, dt);
                }
            }

            CurrentRing = ring;
            Sim.Tick(dt, ring);
            SimTime += dt;
        }
    }
}
