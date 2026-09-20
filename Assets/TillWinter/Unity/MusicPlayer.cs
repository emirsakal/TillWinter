using System.Collections.Generic;
using TillWinter.Core;
using UnityEngine;
using UnityEngine.Audio;

namespace TillWinter.Unity
{
    /// <summary>One piece of music. The season pieces are the year; the rest belong to a screen or a moment.</summary>
    public enum MusicId
    {
        Title,
        Spring,
        Summer,
        Autumn,
        Winter,
        Golden,
        Ending,
    }

    /// <summary>
    /// Which clip backs each <see cref="MusicId"/> (Resources/Music). Like <see cref="SfxTable"/>: a missing file is a
    /// bug to fix by adding the clip, never a generated fallback — the piece simply stays silent and says so once.
    /// </summary>
    public static class MusicTable
    {
        public sealed class Row
        {
            public readonly string Clip;
            public readonly float Volume;
            /// <summary>A piece that plays once and hands the floor back (the ending), rather than looping.</summary>
            public readonly bool Loop;
            public Row(string clip, float volume = 1f, bool loop = true) { Clip = clip; Volume = volume; Loop = loop; }
        }

        public static readonly Dictionary<MusicId, Row> Rows = new Dictionary<MusicId, Row>
        {
            { MusicId.Title, new Row("music_title", 0.85f) },
            { MusicId.Spring, new Row("music_spring", 0.7f) },
            { MusicId.Summer, new Row("music_summer", 0.7f) },
            { MusicId.Autumn, new Row("music_autumn", 0.7f) },
            { MusicId.Winter, new Row("music_winter", 0.75f) },
            { MusicId.Golden, new Row("music_golden", 0.85f) },
            { MusicId.Ending, new Row("music_ending", 0.9f, false) },
        };

        /// <summary>Ids whose clip is not in Resources/Music (empty when the music folder is complete).</summary>
        public static List<string> Missing()
        {
            var missing = new List<string>();
            foreach (var kv in Rows)
                if (Resources.Load<AudioClip>("Music/" + kv.Value.Clip) == null) missing.Add(kv.Key + ":" + kv.Value.Clip);
            return missing;
        }
    }

    /// <summary>
    /// The music bed (GDD §12): two sources crossfading, so a season change or a screen change never cuts. Seasons pick
    /// their own piece, winter and the Golden Year have their own, and the ending plays once over everything.
    /// Volume comes from settings.json through the mixer's Music group; with no clip the game is simply quiet.
    /// </summary>
    public sealed class MusicPlayer : MonoBehaviour
    {
        public const float CrossfadeSeconds = 1.8f;

        private AudioSource _a, _b;
        private bool _onA;
        private float _fade = 1f;          // 0..1 through the crossfade
        private float _fromVolume, _toVolume;
        private MusicId? _current;
        private bool _muted;
        private bool _held;

        /// <summary>The farm whose season the bed follows; null on the title screen, which picks its own piece.</summary>
        public GameController Follow;

        public MusicId? Current => _current;

        public void Init(AudioMixerGroup group)
        {
            _a = NewSource(group);
            _b = NewSource(group);
            int missing = MusicTable.Missing().Count;
            if (missing > 0) Debug.LogWarning("[TillWinter] Music: " + missing + " of " + MusicTable.Rows.Count + " pieces have no clip yet");
        }

        private AudioSource NewSource(AudioMixerGroup group)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            s.loop = true;
            s.volume = 0f;
            s.outputAudioMixerGroup = group;
            return s;
        }

        /// <summary>Crossfades to a piece. The same piece twice in a row is left alone, so a season repeat never restarts it.</summary>
        public void Play(MusicId id, float fade = CrossfadeSeconds)
        {
            if (_current == id) return;
            if (!MusicTable.Rows.TryGetValue(id, out var row)) return;
            var clip = Resources.Load<AudioClip>("Music/" + row.Clip);
            _current = id;
            var next = _onA ? _b : _a;
            var prev = _onA ? _a : _b;
            _onA = !_onA;
            _fromVolume = prev.volume;
            _toVolume = clip != null ? row.Volume : 0f;
            _fade = fade <= 0f ? 1f : 0f;
            next.clip = clip;
            next.loop = row.Loop;
            next.volume = _fade >= 1f ? _toVolume : 0f;
            if (clip != null) next.Play();
        }

        /// <summary>Fades everything out (the ending's silence, or a screen that wants the room quiet).</summary>
        public void StopAll(float fade = CrossfadeSeconds)
        {
            _current = null;
            _fromVolume = (_onA ? _a : _b).volume;
            _toVolume = 0f;
            _fade = fade <= 0f ? 1f : 0f;
            if (_fade >= 1f) { _a.Stop(); _b.Stop(); }
        }

        /// <summary>Music off while a louder moment plays, without losing the piece.</summary>
        public void SetMuted(bool muted) => _muted = muted;

        /// <summary>Takes the floor for a moment that owns its own music (the ending); the farm's seasons wait.</summary>
        public void Hold(MusicId id)
        {
            _held = true;
            Play(id);
        }

        /// <summary>Hands the floor back to the season.</summary>
        public void Release()
        {
            _held = false;
            _current = null; // the season decides again on the next frame
        }

        private void Update()
        {
            if (_a == null) return;
            if (!_held && Follow != null && Follow.Sim != null) Play(ForState(Follow.State));
            float dt = Time.unscaledDeltaTime;
            var to = _onA ? _a : _b;
            var from = _onA ? _b : _a;
            if (_fade < 1f)
            {
                _fade = Mathf.Min(1f, _fade + dt / CrossfadeSeconds);
                from.volume = Mathf.Lerp(_fromVolume, 0f, _fade);
                if (from.volume <= 0.001f && from.isPlaying) from.Stop();
            }
            float target = _muted ? 0f : _toVolume;
            to.volume = _fade >= 1f ? Mathf.MoveTowards(to.volume, target, dt * 2f) : Mathf.Lerp(0f, target, _fade);
        }

        /// <summary>The piece a farm in this state should be playing.</summary>
        public static MusicId ForState(FarmState state)
        {
            if (state == null) return MusicId.Title;
            if (state.GoldenYearActive) return MusicId.Golden;
            if (state.Phase != Phase.Year) return MusicId.Winter;
            switch (state.Season)
            {
                case Season.Summer: return MusicId.Summer;
                case Season.Autumn: return MusicId.Autumn;
                case Season.Winter: return MusicId.Winter;
                default: return MusicId.Spring;
            }
        }
    }
}
