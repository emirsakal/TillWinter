#if UNITY_EDITOR || TW_DEBUG
using System;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;

namespace TillWinter.Unity
{
    /// <summary>
    /// Managed allocations made by gameplay scripts each frame: bytes allocated on the main thread between the first
    /// Update (order -32000) and the last LateUpdate (order 32000). Engine-side work (rendering, UI mesh rebuilds,
    /// input) is outside that window; Unity's frame-wide "GC Allocated In Frame" profiler counter is recorded next to it.
    /// Frames that save the game are excluded (JsonUtility allocates by design). Compiled out of release builds.
    /// The counter is calibrated at runtime: GC.GetAllocatedBytesForCurrentThread reads 0 in Unity's Mono, so the
    /// most precise counter that actually moves is chosen (and named in the smoke log).
    /// </summary>
    public static class FrameAlloc
    {
        public static int Frames { get; private set; }
        public static long TotalBytes { get; private set; }
        public static long MaxBytes { get; private set; }
        public static float AverageBytes => Frames == 0 ? 0f : (float)TotalBytes / Frames;
        public static string CounterName { get; private set; } = "uncalibrated";

        private static int _frameWideFrames;
        private static long _frameWideTotal, _frameWideMax;
        private static ProfilerRecorder _frameRecorder;
        private static Func<long> _counter;

        internal static long Start;
        private static int _ignoredFrame = -1;

        public static long Now()
        {
            if (_counter == null) Calibrate();
            return _counter();
        }

        /// <summary>Picks the most precise live counter (a 1 KB allocation must register; 64 KB for a coarse heap counter). False when none moves.</summary>
        public static bool Calibrate()
        {
            // GC.GetTotalAllocatedBytes is .NET Core 3.0+, not in Unity's editor profile: bind it by reflection when present.
            var total = typeof(GC).GetMethod("GetTotalAllocatedBytes", new[] { typeof(bool) });
            // A typed delegate, bound once: MethodInfo.Invoke would allocate (argument array, boxed result) on every read.
            Func<long> totalRead = null;
            if (total != null)
            {
                var fn = (Func<bool, long>)Delegate.CreateDelegate(typeof(Func<bool, long>), total);
                totalRead = () => fn(true);
            }
            var list = new System.Collections.Generic.List<(string name, Func<long> read)>
            {
                ("GC.GetAllocatedBytesForCurrentThread", () => GC.GetAllocatedBytesForCurrentThread()),
            };
            if (totalRead != null) list.Add(("GC.GetTotalAllocatedBytes(precise)", totalRead));
            list.Add(("Profiler.GetMonoUsedSizeLong", () => Profiler.GetMonoUsedSizeLong()));
            var candidates = list.ToArray();
            foreach (int probe in new[] { 1024, 64 * 1024 })
            {
                foreach (var c in candidates)
                {
                    long a;
                    try { a = c.read(); }
                    catch (Exception) { continue; }
                    var junk = new byte[probe];
                    long b = c.read();
                    if (b - a >= probe / 2 && junk.Length == probe)
                    {
                        _counter = c.read;
                        CounterName = c.name + (probe > 1024 ? " (coarse)" : "");
                        return true;
                    }
                }
            }
            _counter = () => 0;
            CounterName = "none";
            return false;
        }

        public static void StartFrameWide()
        {
            if (!_frameRecorder.Valid) _frameRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
        }

        public static void Reset()
        {
            Frames = 0;
            TotalBytes = 0;
            MaxBytes = 0;
            _frameWideFrames = 0;
            _frameWideTotal = 0;
            _frameWideMax = 0;
        }

        public static void IgnoreThisFrame() => _ignoredFrame = Time.frameCount;

        internal static void End()
        {
            long bytes = Math.Max(0, Now() - Start);
            if (Time.frameCount == _ignoredFrame) return;
            Frames++;
            TotalBytes += bytes;
            if (bytes > MaxBytes) MaxBytes = bytes;
            if (_frameRecorder.Valid)
            {
                long fw = _frameRecorder.LastValue; // the previous complete frame, engine + scripts
                _frameWideFrames++;
                _frameWideTotal += fw;
                if (fw > _frameWideMax) _frameWideMax = fw;
            }
        }

        public static string Describe() => Frames + " frames, avg " + AverageBytes.ToString("0.0") + " B, max " + MaxBytes + " B [" + CounterName + "]";

        public static string DescribeFrameWide() => _frameWideFrames == 0
            ? "n/a (profiler counter unavailable)"
            : _frameWideFrames + " frames, avg " + (_frameWideTotal / (float)_frameWideFrames).ToString("0") + " B, max " + _frameWideMax + " B";
    }

    [DefaultExecutionOrder(-32000)]
    public sealed class FrameAllocStart : MonoBehaviour
    {
        private void Awake()
        {
            FrameAlloc.Calibrate();
            FrameAlloc.StartFrameWide();
        }

        private void Update() => FrameAlloc.Start = FrameAlloc.Now();
    }

    [DefaultExecutionOrder(32000)]
    public sealed class FrameAllocEnd : MonoBehaviour
    {
        private void LateUpdate() => FrameAlloc.End();
    }
}
#endif
