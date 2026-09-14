using System;
using System.Collections.Generic;
using UnityEngine;

namespace TillWinter.Unity
{
    public enum SfxId
    {
        HarvestPop,
        CoinArrive,
        Purchase,
        FrostTick,
        CrowCaw,
        CrowScared,
        WinterChime,
        UiClick,
        Denied,
        WaterSplash,
    }

    /// <summary>
    /// Pitch-randomised one-shots. Loads Kenney CC0 clips from Resources/Kenney when present and
    /// falls back to clips generated with AudioClip.Create so every hook is exercised offline.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        private const int Voices = 8;
        private const int SampleRate = 22050;

        private sealed class Entry
        {
            public AudioClip[] Clips;
            public float Volume;
            public float PitchJitter;
        }

        private readonly Dictionary<SfxId, Entry> _table = new Dictionary<SfxId, Entry>();
        private AudioSource[] _sources;
        private int _next;
        private float _lastCoin;
        private System.Random _rng = new System.Random(7);
        public bool UsingKenneyClips { get; private set; }

        public void Init()
        {
            _sources = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                _sources[i] = s;
            }

            int loaded = 0;
            Register(SfxId.HarvestPop, 0.7f, 0.12f, ref loaded, () => Gen("pop", 0.09f, t => Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(620f, 260f, t / 0.09f) * t) * Mathf.Exp(-t * 18f)),
                "impactSoft_medium_000", "impactSoft_medium_001", "impactSoft_medium_002", "impactSoft_medium_003");
            Register(SfxId.CoinArrive, 0.45f, 0.1f, ref loaded, () => Gen("coin", 0.12f, t => Mathf.Sin(2f * Mathf.PI * 1320f * t) * Mathf.Exp(-t * 22f)),
                "impactGlass_light_000", "impactGlass_light_001", "impactGlass_light_002", "impactGlass_light_003");
            Register(SfxId.Purchase, 0.7f, 0.04f, ref loaded, () => Gen("buy", 0.3f, t => (Mathf.Sin(2f * Mathf.PI * 520f * t) + Mathf.Sin(2f * Mathf.PI * 780f * Mathf.Max(0f, t - 0.1f))) * 0.5f * Mathf.Exp(-t * 7f)),
                "impactBell_heavy_001");
            Register(SfxId.FrostTick, 0.5f, 0.02f, ref loaded, () => Gen("tick", 0.05f, t => Mathf.Sin(2f * Mathf.PI * 220f * t) * Mathf.Exp(-t * 60f)),
                "switch8", "switch9");
            Register(SfxId.CrowCaw, 0.5f, 0.15f, ref loaded, () => Gen("caw", 0.22f, t => (float)(_rng.NextDouble() * 2 - 1) * Mathf.Sin(2f * Mathf.PI * 90f * t) * Mathf.Sin(t / 0.22f * Mathf.PI)));
            Register(SfxId.CrowScared, 0.5f, 0.1f, ref loaded, () => Gen("flap", 0.3f, t => (float)(_rng.NextDouble() * 2 - 1) * Mathf.Abs(Mathf.Sin(2f * Mathf.PI * 14f * t)) * Mathf.Exp(-t * 6f)));
            Register(SfxId.WinterChime, 0.8f, 0.0f, ref loaded, () => Gen("chime", 0.9f, t => (Mathf.Sin(2f * Mathf.PI * 880f * t) + 0.6f * Mathf.Sin(2f * Mathf.PI * 1320f * t)) * 0.6f * Mathf.Exp(-t * 3.5f)),
                "impactBell_heavy_000");
            Register(SfxId.UiClick, 0.5f, 0.05f, ref loaded, () => Gen("click", 0.04f, t => Mathf.Sin(2f * Mathf.PI * 900f * t) * Mathf.Exp(-t * 90f)),
                "click1", "click2", "click3");
            Register(SfxId.Denied, 0.5f, 0.0f, ref loaded, () => Gen("denied", 0.15f, t => Mathf.Sin(2f * Mathf.PI * 160f * t) * Mathf.Exp(-t * 12f)),
                "impactMetal_light_000");
            Register(SfxId.WaterSplash, 0.35f, 0.15f, ref loaded, () => Gen("splash", 0.12f, t => (float)(_rng.NextDouble() * 2 - 1) * Mathf.Exp(-t * 30f) * 0.6f),
                "impactSoft_medium_001", "impactSoft_medium_003");
            UsingKenneyClips = loaded > 0;
            Debug.Log("[TillWinter] Audio: " + (UsingKenneyClips ? loaded + " Kenney clips loaded from Resources/Kenney" : "no Kenney clips found, using generated placeholders"));
        }

        private void Register(SfxId id, float volume, float pitchJitter, ref int loadedCount, Func<AudioClip> fallback, params string[] resourceNames)
        {
            var clips = new List<AudioClip>();
            foreach (var n in resourceNames)
            {
                var c = Resources.Load<AudioClip>("Kenney/" + n);
                if (c != null) clips.Add(c);
            }
            loadedCount += clips.Count;
            if (clips.Count == 0) clips.Add(fallback());
            _table[id] = new Entry { Clips = clips.ToArray(), Volume = volume, PitchJitter = pitchJitter };
        }

        private static AudioClip Gen(string name, float seconds, Func<float, float> wave)
        {
            int n = Mathf.CeilToInt(seconds * SampleRate);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float fadeIn = Mathf.Min(1f, i / 40f);
                data[i] = Mathf.Clamp(wave(t), -1f, 1f) * fadeIn;
            }
            var clip = AudioClip.Create(name, n, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public void Play(SfxId id, float volumeScale = 1f)
        {
            if (!_table.TryGetValue(id, out var e)) return;
            if (id == SfxId.CoinArrive)
            {
                if (Time.unscaledTime - _lastCoin < 0.035f) return;
                _lastCoin = Time.unscaledTime;
            }
            var src = _sources[_next];
            _next = (_next + 1) % _sources.Length;
            src.pitch = 1f + UnityEngine.Random.Range(-e.PitchJitter, e.PitchJitter);
            var clip = e.Clips[UnityEngine.Random.Range(0, e.Clips.Length)];
            src.PlayOneShot(clip, e.Volume * volumeScale);
        }
    }
}
