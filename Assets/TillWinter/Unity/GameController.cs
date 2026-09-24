using System.Collections.Generic;
using TillWinter.Core;
using UnityEngine;

namespace TillWinter.Unity
{
    /// <summary>
    /// Owns the <see cref="FarmSim"/>, ticks it once per frame, turns the finger into the core loop's verbs (GDD §2v3:
    /// a press strikes hard ground or takes a crow, a hold waters a growing crop, a swipe reaps the ripe crops it
    /// crosses, a tap reaps one) and maps between plot space and world space. No game rules live here.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class GameController : MonoBehaviour
    {
        public FarmSim Sim { get; private set; }
        public FarmState State => Sim.State;

        [Range(0.5f, 8f)] public float TimeScale = 1f;
        /// <summary>Seconds a still finger must rest on a growing crop before it waters it.</summary>
        public float HoldSeconds = 0.25f;
        /// <summary>Pixels (at 2340 tall) a finger must travel before the press is a swipe.</summary>
        public float SwipePixels = 30f;
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
        /// <summary>Debug/smoke-test hook: a one-shot tap (press and release) at this screen position; cleared after use.</summary>
        public Vector2? DebugTapScreen;
        /// <summary>Set by CloudView: is this screen point on the rain cloud?</summary>
        public System.Func<Vector2, bool> CloudHitTest;
        /// <summary>Set by DogView: is this screen point on the dog? Returns true when it took the tap.</summary>
        public System.Func<Vector2, bool> DogHitTest;
        /// <summary>Set by the HUD while a seed is picked from the bag or the magnifier is armed: takes the tapped plot. A crow still takes the tap first.</summary>
        public System.Func<GridPos, bool> PlotTapOverride;
        /// <summary>Set by the HUD while a scarecrow is being placed: gets the tap in plot space (corners between beds count).</summary>
        public System.Func<Vector2, bool> FieldTapOverride;
        /// <summary>Set by ApprenticesView: which apprentice is under this screen point, or -1.</summary>
        public System.Func<Vector2, int> ApprenticeHitTest;
        /// <summary>A tapped apprentice switched role (GDD §2v3.9): its index.</summary>
        public event System.Action<int> ApprenticeRoleToggled;

        /// <summary>Sim seconds elapsed (respects TimeScale). Use for animation that should follow the sim.</summary>
        public float SimTime { get; private set; }
        /// <summary>The plot the finger came down on, while it is down (the halo and the hints read it).</summary>
        public GridPos? PressedPlot { get; private set; }
        /// <summary>The plots a swipe in progress has crossed (a trail can be drawn along it).</summary>
        public IReadOnlyList<GridPos> SwipePath => _path;
        /// <summary>True while a hold is watering the pressed plot.</summary>
        public bool Watering => _watering;

        private readonly Plane _ground = new Plane(Vector3.up, Vector3.zero);
        private readonly List<GridPos> _path = new List<GridPos>(64);
        private bool _pressActive, _pressHandled, _dragging, _watering, _debugWasDown;
        private GridPos? _pressPlot;

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

        private bool TryScreenToCell(Vector2 screen, out GridPos cell)
        {
            cell = default;
            if (!TryScreenToPlot(screen, out var p)) return false;
            cell = new GridPos(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y));
            return State.InBounds(cell);
        }

        private void Update()
        {
            if (Sim == null) return;
            if (Paused)
            {
                if (_pressActive) EndPress();
                return;
            }
            Sim.AddPlayTime(Time.unscaledDeltaTime); // stats: time played (not while paused or closed)
            float dt = Mathf.Min(Time.deltaTime, 0.1f) * TimeScale;

            if (!InputBlocked && Pointer != null)
            {
                var s = Pointer.Current;
                // Debug hooks stand in for a finger: a held point, or a one-shot press-and-release.
                if (DebugPointerScreen.HasValue)
                {
                    s.IsDown = true;
                    s.Position = DebugPointerScreen.Value;
                    if (!_debugWasDown) { s.Pressed = true; s.DownPosition = s.Position; s.MaxMove = 0f; }
                    s.HeldSeconds += Time.unscaledDeltaTime;
                    _debugWasDown = true;
                }
                else if (_debugWasDown)
                {
                    s.Released = true;
                    _debugWasDown = false;
                }
                if (DebugTapScreen.HasValue)
                {
                    s.Pressed = s.Released = s.Tapped = true;
                    s.Position = s.DownPosition = s.TapPosition = DebugTapScreen.Value;
                    s.MaxMove = 0f;
                    s.IsDown = false;
                    DebugTapScreen = null;
                }

                if (s.Pressed) OnPress(s.Position);
                if (_pressActive && s.IsDown && !s.Released) OnHold(s.Position, s.MaxMove, s.HeldSeconds);
                if (_pressActive && (s.Released || !s.IsDown)) OnRelease(s.Position, s.MaxMove);
            }
            else if (_pressActive) EndPress();

            Sim.Tick(dt);
            SimTime += dt;
        }

        private float SwipeThreshold => SwipePixels * Screen.height / 2340f;

        private void OnPress(Vector2 screen)
        {
            if (_pressActive) EndPress();
            _pressActive = true;
            _pressHandled = false;
            _dragging = false;
            _watering = false;
            _path.Clear();
            _pressPlot = null;
            PressedPlot = null;
            // Things that take the whole press: the cloud, the dog, an apprentice, a placing or picking mode. They act on release.
            if ((CloudHitTest != null && CloudHitTest(screen)) || (DogHitTest != null && DogHitTest(screen))
                || (ApprenticeHitTest != null && ApprenticeHitTest(screen) >= 0) || FieldTapOverride != null)
            {
                _pressHandled = true;
                return;
            }
            if (!TryScreenToCell(screen, out var cell)) return;
            _pressPlot = cell;
            PressedPlot = cell;
            var plot = State.GetPlot(cell);
            bool crowHere = plot.HasCrow;
            bool pestHere = State.Pest.Kind != PestKind.None && State.Pest.Pos == cell;
            if (PlotTapOverride != null && !crowHere && !pestHere)
            {
                _pressHandled = true; // the seed bag or the magnifier takes the plot on release
                return;
            }
            // A crow, a pest, a clover or hard ground answer the moment the finger lands: the strike must land on the
            // beat the player heard, not a fifth of a second later. A ripe crop waits: a lift reaps it, a drag reaps more.
            if (crowHere || pestHere || plot.IsHard)
            {
                Sim.Press(cell);
                if (!plot.IsHard) _pressHandled = true;
            }
        }

        private void OnHold(Vector2 screen, float maxMove, float held)
        {
            if (_pressHandled) return;
            if (maxMove > SwipeThreshold)
            {
                if (!_dragging)
                {
                    _dragging = true;
                    if (_watering) { Sim.SetWatering(null); _watering = false; }
                    if (_pressPlot.HasValue) _path.Add(_pressPlot.Value);
                }
                if (TryScreenToCell(screen, out var cell) && (_path.Count == 0 || _path[_path.Count - 1] != cell)) _path.Add(cell);
                return;
            }
            if (!_watering && _pressPlot.HasValue && held >= HoldSeconds && State.GetPlot(_pressPlot.Value).IsGrowing)
            {
                Sim.SetWatering(_pressPlot);
                _watering = true;
            }
        }

        private void OnRelease(Vector2 screen, float maxMove)
        {
            bool tap = maxMove <= (Pointer != null ? Pointer.TapMaxPixels : 20f) * Screen.height / 2340f;
            if (_pressHandled)
            {
                if (tap) ReleaseTap(screen);
            }
            else if (_dragging)
            {
                if (TryScreenToCell(screen, out var last) && (_path.Count == 0 || _path[_path.Count - 1] != last)) _path.Add(last);
                if (_path.Count > 0) Sim.Reap(_path);
            }
            else if (_pressPlot.HasValue && !_watering && State.GetPlot(_pressPlot.Value).IsRipe)
            {
                Sim.ReapOne(_pressPlot.Value);
            }
            EndPress();
        }

        /// <summary>The release-time taps: the cloud, an apprentice's role, a scarecrow corner, the seed bag or the magnifier.</summary>
        private void ReleaseTap(Vector2 screen)
        {
            if (CloudHitTest != null && CloudHitTest(screen) && Sim.TapCloud()) return;
            if (DogHitTest != null && DogHitTest(screen)) return; // the dog swallows its own tap: petting it never strikes the plot behind it
            if (ApprenticeHitTest != null)
            {
                int index = ApprenticeHitTest(screen);
                if (index >= 0)
                {
                    // Picker → waterer → digger → picker.
                    var role = (ApprenticeRole)(((int)State.Apprentices[index].Role + 1) % 3);
                    if (Sim.SetApprenticeRole(index, role)) ApprenticeRoleToggled?.Invoke(index);
                    return;
                }
            }
            if (FieldTapOverride != null && TryScreenToPlot(screen, out var fp) && FieldTapOverride(new Vector2(fp.x, fp.y))) return;
            if (PlotTapOverride != null && TryScreenToCell(screen, out var cell)) PlotTapOverride(cell);
        }

        private void EndPress()
        {
            if (_watering) Sim.SetWatering(null);
            _watering = false;
            _pressActive = false;
            _pressHandled = false;
            _dragging = false;
            _pressPlot = null;
            PressedPlot = null;
            _path.Clear();
        }
    }
}
