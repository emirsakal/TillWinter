using System;

namespace TillWinter.Core
{
    /// <summary>
    /// Hands-free play (GDD §10.6 v2.5): the player taps to place the ring's home, and the ring tends the plots around it
    /// on its own, most urgent first (a Ripe crop, then the Wet plot nearest to ripening, then a Dry one). Input, not a
    /// rule: it only produces the <see cref="RingInput"/> a finger would have, so everything stays deterministic.
    /// </summary>
    public sealed class AutoRing
    {
        /// <summary>Plots per second the ring glides; how far from home it will go (plot units).</summary>
        public float Speed = 3f, Reach = 1.6f;

        public bool Placed { get; private set; }
        public float HomeX { get; private set; }
        public float HomeY { get; private set; }
        public float X { get; private set; }
        public float Y { get; private set; }

        /// <summary>Sets the ring's home (a tap on the field); the ring starts there.</summary>
        public void Place(float x, float y)
        {
            if (!Placed)
            {
                X = x;
                Y = y;
            }
            HomeX = x;
            HomeY = y;
            Placed = true;
        }

        public void Clear() => Placed = false;

        /// <summary>The ring for this frame, or null before a home is placed or outside the year.</summary>
        public RingInput? Step(FarmState state, float dt)
        {
            if (!Placed || state == null || state.Phase != Phase.Year || state.PlotArray == null) return null;
            Plot best = null;
            double bestScore = double.NegativeInfinity;
            float reach2 = Reach * Reach;
            foreach (var p in state.PlotArray)
            {
                float hx = p.Pos.X - HomeX, hy = p.Pos.Y - HomeY;
                if (hx * hx + hy * hy > reach2) continue;
                // Stones are cleared by the ring too, but only when nothing else needs it.
                double score = p.IsRipe ? 3 : p.State == PlotState.Wet ? 1 + p.Progress : p.IsStony ? 0.2 : 0.5;
                float dx = p.Pos.X - X, dy = p.Pos.Y - Y;
                score -= 0.05 * Math.Sqrt(dx * dx + dy * dy); // near beats far on a tie
                if (score > bestScore)
                {
                    bestScore = score;
                    best = p;
                }
            }
            float tx = best != null ? best.Pos.X : HomeX, ty = best != null ? best.Pos.Y : HomeY;
            float mx = tx - X, my = ty - Y;
            float dist = (float)Math.Sqrt(mx * mx + my * my);
            float step = Speed * Math.Max(0f, dt);
            if (dist <= step || dist < 1e-4f)
            {
                X = tx;
                Y = ty;
            }
            else
            {
                X += mx / dist * step;
                Y += my / dist * step;
            }
            return new RingInput(X, Y);
        }
    }
}
