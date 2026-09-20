using System;
using System.Collections.Generic;
using TillWinter.Core.Feel;
using UnityEngine;
using UnityEngine.Audio;

namespace TillWinter.Unity
{
    public enum SfxId
    {
        HarvestPop, CoinArrive, CoinArriveRich, GoldenHarvest,
        WaterSplash, Sprout,
        CrowCaw, CrowScared,
        CloudTap, TractorStart, Step,
        FrostTick, WinterChime,
        Purchase, Denied,
        RetireSwell, NewGeneration, Expansion,
        UiClick,
        // The moments the v2.x systems added, which used to pass in silence or borrow another moment's sound.
        GoalMet, Achievement, EndingSting, ComboMilestone,
        MarketSale, TraderArrive,
        PestArrive, PestStruck, ScarecrowPlace,
        DogBark, HensFlutter,
    }

    /// <summary>Which Kenney clips back each <see cref="SfxId"/> (Resources/Kenney). Tests assert every id resolves; no generated fallback exists.</summary>
    public static class SfxTable
    {
        public sealed class Row
        {
            public string[] Clips;
            public float Volume;
            public float PitchJitter;
            public int MaxPerSecond;
            /// <summary>Resources folder the clips live in; the Kenney packs are the default, other CC0 sets have their own.</summary>
            public string Folder;
            public Row(float volume, float jitter, int maxPerSecond, params string[] clips)
                : this("Kenney", volume, jitter, maxPerSecond, clips) { }
            public Row(string folder, float volume, float jitter, int maxPerSecond, params string[] clips)
            { Folder = folder; Volume = volume; PitchJitter = jitter; MaxPerSecond = maxPerSecond; Clips = clips; }
        }

        public static readonly Dictionary<SfxId, Row> Rows = new Dictionary<SfxId, Row>
        {
            { SfxId.HarvestPop, new Row(0.7f, 0.06f, 12, "impactSoft_medium_000", "impactSoft_medium_001", "impactSoft_medium_002") },
            { SfxId.CoinArrive, new Row(0.4f, 0.08f, 20, "impactGlass_light_000", "impactGlass_light_001") },
            { SfxId.CoinArriveRich, new Row(0.45f, 0.06f, 20, "impactGlass_medium_000", "impactGlass_medium_001") },
            { SfxId.GoldenHarvest, new Row(0.8f, 0.0f, 4, "impactBell_heavy_003") },
            { SfxId.WaterSplash, new Row(0.4f, 0.12f, 10, "drop_001", "drop_002") },
            { SfxId.Sprout, new Row(0.35f, 0.1f, 10, "pluck_001", "pluck_002") },
            { SfxId.CrowCaw, new Row(0.5f, 0.15f, 4, "creak2") },
            { SfxId.CrowScared, new Row(0.5f, 0.1f, 4, "cloth1", "cloth2") },
            { SfxId.CloudTap, new Row(0.5f, 0.05f, 4, "drop_003", "drop_004") },
            { SfxId.TractorStart, new Row(0.55f, 0.05f, 2, "metalLatch") },
            { SfxId.Step, new Row(0.12f, 0.15f, 6, "footstep_grass_000", "footstep_grass_001", "footstep_grass_002", "footstep_grass_003", "footstep_grass_004") },
            { SfxId.FrostTick, new Row(0.5f, 0.02f, 4, "tick_001", "tick_002") },
            { SfxId.WinterChime, new Row(0.8f, 0.0f, 1, "impactBell_heavy_000") },
            { SfxId.Purchase, new Row(0.7f, 0.03f, 6, "confirmation_001", "impactBell_heavy_001") },
            { SfxId.Denied, new Row(0.5f, 0.0f, 6, "error_004") },
            { SfxId.RetireSwell, new Row(0.9f, 0.0f, 1, "maximize_008") },
            { SfxId.NewGeneration, new Row(0.8f, 0.0f, 1, "open_001", "confirmation_003") },
            { SfxId.Expansion, new Row(0.6f, 0.05f, 4, "impactWood_medium_000", "impactPlank_medium_001") },
            { SfxId.UiClick, new Row(0.5f, 0.05f, 12, "click1", "click2", "click3") },
            { SfxId.GoalMet, new Row(0.8f, 0.0f, 2, "jingle_goal") },
            { SfxId.Achievement, new Row(0.8f, 0.0f, 2, "jingle_achievement") },
            { SfxId.EndingSting, new Row(0.9f, 0.0f, 1, "jingle_ending") },
            { SfxId.ComboMilestone, new Row(0.6f, 0.02f, 3, "jingle_combo") },
            { SfxId.MarketSale, new Row(0.7f, 0.04f, 4, "handleCoins") },
            { SfxId.TraderArrive, new Row(0.6f, 0.04f, 2, "handleSmallLeather") },
            { SfxId.PestStruck, new Row(0.55f, 0.08f, 4, "chop") },
            { SfxId.ScarecrowPlace, new Row(0.5f, 0.08f, 4, "cloth3") },
            { SfxId.HensFlutter, new Row(0.45f, 0.12f, 4, "cloth4") },
            { SfxId.PestArrive, new Row("Creatures", 0.5f, 0.08f, 3, "bug_01") },
            { SfxId.DogBark, new Row("Creatures", 0.45f, 0.1f, 3, "barking_01", "barking_02") },
        };

        /// <summary>Ids whose clips did not all load (empty when the audio folder is complete).</summary>
        public static List<string> Missing()
        {
            var missing = new List<string>();
            foreach (var kv in Rows)
                foreach (var clip in kv.Value.Clips)
                    if (Resources.Load<AudioClip>(kv.Value.Folder + "/" + clip) == null) missing.Add(kv.Key + ":" + clip);
            return missing;
        }
    }

    /// <summary>
    /// Pitch-randomised one-shots on a capped voice pool (8) through the Master/SFX/Ambience mixer, every id
    /// rate-limited (merged plays come out slightly louder), combo pitch, ducking for the big moments, volumes
    /// from settings.json. No generated fallback: a missing clip logs once and stays silent.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        public const int Voices = 8;
        public const string MixerName = "TillWinterMixer";

        private sealed class Entry
        {
            public AudioClip[] Clips;
            public SfxTable.Row Row;
            public RateLimiter Limiter;
            public float PendingVolume, PendingPitch;
            public bool Pending;
        }

        private readonly Dictionary<SfxId, Entry> _table = new Dictionary<SfxId, Entry>();
        private readonly List<Entry> _entries = new List<Entry>();
        private AudioSource[] _sources;
        private float[] _busyUntil;
        private AudioMixer _mixer;
        private AudioMixerGroup _sfxGroup, _ambienceGroup, _musicGroup;

        /// <summary>Buses the music and ambience players attach their own looping sources to.</summary>
        public AudioMixerGroup MusicGroup => _musicGroup;
        public AudioMixerGroup AmbienceGroup => _ambienceGroup;
        private float _duckUntil;
        private float _sfxDb;
        private System.Random _rng = new System.Random(7);

        public static AudioManager Instance { get; private set; }
        public bool UsingKenneyClips { get; private set; }
        public bool HasMixer => _mixer != null;
        /// <summary>Pitch multiplier applied to harvest pops (combo); set by the field view.</summary>
        public float HarvestPitch = 1f;

        /// <summary>Voices playing right now (smoke budget check).</summary>
        public int ActiveVoices
        {
            get
            {
                int n = 0;
                float now = Time.unscaledTime;
                for (int i = 0; i < _sources.Length; i++) if (_busyUntil[i] > now) n++;
                return n;
            }
        }

        public void Init()
        {
            Instance = this;
            _mixer = Resources.Load<AudioMixer>(MixerName);
            if (_mixer != null)
            {
                var sfx = _mixer.FindMatchingGroups("SFX");
                var amb = _mixer.FindMatchingGroups("Ambience");
                var mus = _mixer.FindMatchingGroups("Music");
                if (sfx.Length > 0) _sfxGroup = sfx[0];
                if (amb.Length > 0) _ambienceGroup = amb[0];
                if (mus.Length > 0) _musicGroup = mus[0];
            }
            _sources = new AudioSource[Voices];
            _busyUntil = new float[Voices];
            for (int i = 0; i < Voices; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                s.outputAudioMixerGroup = _sfxGroup;
                _sources[i] = s;
            }
            int loaded = 0, expected = 0;
            foreach (var kv in SfxTable.Rows)
            {
                var clips = new List<AudioClip>();
                foreach (var n in kv.Value.Clips)
                {
                    expected++;
                    var c = Resources.Load<AudioClip>(kv.Value.Folder + "/" + n);
                    if (c != null) { clips.Add(c); loaded++; }
                    else Debug.LogWarning("[TillWinter] Audio clip missing: " + kv.Value.Folder + "/" + n + " for " + kv.Key);
                }
                var e = new Entry { Clips = clips.ToArray(), Row = kv.Value, Limiter = new RateLimiter(kv.Value.MaxPerSecond, 0.15f, 1.6f) };
                _table[kv.Key] = e;
                _entries.Add(e);
            }
            UsingKenneyClips = loaded == expected && expected > 0;
            ApplyVolumes();
            Debug.Log("[TillWinter] Audio: " + loaded + "/" + expected + " Kenney clips" + (_mixer != null ? ", mixer " + MixerName : ", no mixer asset"));
        }

        /// <summary>Pushes settings.json volumes to the mixer (dB) or, without a mixer, to the sources.</summary>
        public void ApplyVolumes()
        {
            var s = SettingsStore.Current;
            if (_mixer != null)
            {
                _mixer.SetFloat("MasterVolume", ToDb(s.MasterVolume));
                _sfxDb = ToDb(s.SfxVolume);
                _mixer.SetFloat("SfxVolume", _sfxDb);
                _mixer.SetFloat("AmbienceVolume", ToDb(s.AmbienceVolume));
                _mixer.SetFloat("MusicVolume", ToDb(s.MusicVolume));
            }
            else
                foreach (var src in _sources) src.volume = s.MasterVolume * s.SfxVolume;
        }

        private static float ToDb(float linear) => linear <= 0.0001f ? -80f : 20f * Mathf.Log10(linear);

        /// <summary>SFX drop by <paramref name="db"/> for <paramref name="seconds"/> (winter chime, retire swell).</summary>
        public void Duck(float seconds = 1f, float db = -6f)
        {
            _duckUntil = Time.unscaledTime + seconds;
            if (_mixer != null) _mixer.SetFloat("SfxVolume", _sfxDb + db);
        }

        public void Play(SfxId id, float volumeScale = 1f, float pitch = 1f)
        {
            if (!_table.TryGetValue(id, out var e) || e.Clips.Length == 0) return;
            if (id == SfxId.HarvestPop) pitch *= HarvestPitch;
            if (e.Limiter.Request(Time.unscaledTimeAsDouble, out float intensity)) Fire(e, volumeScale * intensity, pitch);
            else
            {
                e.Pending = true;
                e.PendingVolume = volumeScale;
                e.PendingPitch = pitch;
            }
        }

        private void Fire(Entry e, float volumeScale, float pitch)
        {
            float now = Time.unscaledTime;
            // Eight voices max: take a free one, else steal the one that ends soonest (never more than 8 playing).
            int slot = 0;
            for (int i = 0; i < _sources.Length; i++)
            {
                if (_busyUntil[i] <= now) { slot = i; break; }
                if (_busyUntil[i] < _busyUntil[slot]) slot = i;
            }
            var src = _sources[slot];
            var clip = e.Clips[_rng.Next(e.Clips.Length)];
            src.pitch = pitch * (1f + ((float)_rng.NextDouble() * 2f - 1f) * e.Row.PitchJitter);
            src.PlayOneShot(clip, e.Row.Volume * Mathf.Min(1.5f, volumeScale));
            _busyUntil[slot] = now + clip.length / Mathf.Max(0.5f, src.pitch);
        }

        private void LateUpdate()
        {
            double now = Time.unscaledTimeAsDouble;
            for (int i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                if (!e.Pending) continue;
                if (e.Limiter.Poll(now, out float intensity)) Fire(e, e.PendingVolume * intensity, e.PendingPitch);
                if (e.Limiter.Pending == 0) e.Pending = false;
            }
            if (_duckUntil > 0f && Time.unscaledTime >= _duckUntil)
            {
                _duckUntil = 0f;
                if (_mixer != null) _mixer.SetFloat("SfxVolume", _sfxDb);
            }
        }
    }
}
