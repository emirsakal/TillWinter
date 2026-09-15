using System;

namespace TillWinter.Core.Feel
{
    /// <summary>
    /// Budget limiter for repeatable feedback (VFX, SFX, haptics). Token bucket: at most <see cref="MaxPerSecond"/>
    /// triggers per second; requests beyond the budget are not dropped but merged into the next allowed trigger,
    /// which fires stronger (intensity rises by <see cref="MergeStep"/> per merged request, capped at
    /// <see cref="MaxIntensity"/>). Pure C#, deterministic for a given clock.
    /// </summary>
    public sealed class RateLimiter
    {
        public int MaxPerSecond { get; }
        public float MergeStep { get; }
        public float MaxIntensity { get; }
        /// <summary>Requests merged but not yet fired.</summary>
        public int Pending { get; private set; }
        /// <summary>Triggers actually fired since construction or <see cref="Reset"/>.</summary>
        public int Fired { get; private set; }

        private double _tokens;
        private double _last;
        private bool _started;

        public RateLimiter(int maxPerSecond, float mergeStep = 0.25f, float maxIntensity = 3f)
        {
            if (maxPerSecond < 1) throw new ArgumentOutOfRangeException(nameof(maxPerSecond));
            MaxPerSecond = maxPerSecond;
            MergeStep = mergeStep;
            MaxIntensity = maxIntensity;
            _tokens = maxPerSecond;
        }

        /// <summary>
        /// Asks to trigger at time <paramref name="now"/> (seconds). Returns true when the trigger may fire; then
        /// <paramref name="intensity"/> is 1 plus the merged backlog. Returns false when the budget is spent; the
        /// request is remembered and folded into the next trigger (or <see cref="Poll"/>).
        /// </summary>
        public bool Request(double now, out float intensity)
        {
            Refill(now);
            if (_tokens >= 1.0)
            {
                _tokens -= 1.0;
                intensity = Intensity(Pending);
                Pending = 0;
                Fired++;
                return true;
            }
            Pending++;
            intensity = 0f;
            return false;
        }

        /// <summary>Call once per frame: fires the merged backlog as soon as the budget allows, so a burst never ends on a silent drop.</summary>
        public bool Poll(double now, out float intensity)
        {
            intensity = 0f;
            if (Pending == 0) return false;
            Refill(now);
            if (_tokens < 1.0) return false;
            _tokens -= 1.0;
            intensity = Intensity(Pending - 1);
            Pending = 0;
            Fired++;
            return true;
        }

        public void Reset()
        {
            _tokens = MaxPerSecond;
            Pending = 0;
            Fired = 0;
            _started = false;
        }

        private float Intensity(int merged) => Math.Min(MaxIntensity, 1f + merged * MergeStep);

        private void Refill(double now)
        {
            if (!_started)
            {
                _started = true;
                _last = now;
                return;
            }
            double dt = now - _last;
            if (dt < 0) dt = 0; // clock went backwards: no refill, no throw
            _last = now;
            _tokens = Math.Min(MaxPerSecond, _tokens + dt * MaxPerSecond);
        }
    }
}
