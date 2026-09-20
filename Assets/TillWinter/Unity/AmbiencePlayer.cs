using TillWinter.Core;
using UnityEngine;
using UnityEngine.Audio;

namespace TillWinter.Unity
{
    /// <summary>The looping layers under the farm (GDD §12). Each one fades to where the year currently is.</summary>
    public enum AmbienceLayer
    {
        /// <summary>Always there, quiet: the field's own air.</summary>
        Wind = 0,
        /// <summary>Daytime birds: spring and summer.</summary>
        Birds = 1,
        /// <summary>Late-summer and autumn insects.</summary>
        Insects = 2,
        /// <summary>Only while it rains.</summary>
        Rain = 3,
        /// <summary>Winter: a colder, emptier wind.</summary>
        WinterWind = 4,
    }

    /// <summary>
    /// Ambience is a mix, not a track: five loops whose volumes follow the season, the weather and the phase, so the
    /// farm sounds different in a summer noon, an autumn storm and a winter evening without any of it ever cutting.
    /// Everything routes through the mixer's Ambience group, which is what the settings slider has always meant.
    /// </summary>
    public sealed class AmbiencePlayer : MonoBehaviour
    {
        /// <summary>Clip per layer in Resources/Ambience; a layer with no clip is simply silent.</summary>
        public static readonly string[] Clips =
        {
            "amb_wind", "amb_birds", "amb_insects", "amb_rain", "amb_winter_wind",
        };

        private const int Count = 5;
        private readonly AudioSource[] _sources = new AudioSource[Count];
        private readonly float[] _target = new float[Count];
        private GameController _game;
        private bool _any;

        public void Init(AudioMixerGroup group, GameController game)
        {
            _game = game;
            for (int i = 0; i < Count; i++)
            {
                var clip = Resources.Load<AudioClip>("Ambience/" + Clips[i]);
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                s.loop = true;
                s.volume = 0f;
                s.clip = clip;
                s.outputAudioMixerGroup = group;
                _sources[i] = s;
                if (clip == null) continue;
                _any = true;
                s.time = clip.length * (i * 0.17f % 1f); // the layers never start in lockstep
                s.Play();
            }
            if (!_any) Debug.LogWarning("[TillWinter] Ambience: no loops in Resources/Ambience yet");
        }

        /// <summary>The mix this state calls for, 0..1 per layer (pure, so it can be reasoned about and tested by eye).</summary>
        public static void Mix(FarmState state, float[] into)
        {
            for (int i = 0; i < into.Length; i++) into[i] = 0f;
            if (state == null) return;
            bool winter = state.Phase != Phase.Year || state.Season == Season.Winter;
            bool golden = state.GoldenYearActive;
            if (winter)
            {
                into[(int)AmbienceLayer.WinterWind] = 0.8f;
                into[(int)AmbienceLayer.Wind] = 0.15f;
                return;
            }
            into[(int)AmbienceLayer.Wind] = 0.5f;
            switch (state.Season)
            {
                case Season.Spring:
                    into[(int)AmbienceLayer.Birds] = 0.75f;
                    break;
                case Season.Summer:
                    into[(int)AmbienceLayer.Birds] = 0.4f;
                    into[(int)AmbienceLayer.Insects] = 0.6f;
                    break;
                case Season.Autumn:
                    into[(int)AmbienceLayer.Insects] = 0.35f;
                    into[(int)AmbienceLayer.Wind] = 0.7f; // the year's last wind is louder
                    break;
            }
            if (golden) into[(int)AmbienceLayer.Birds] = Mathf.Max(into[(int)AmbienceLayer.Birds], 0.6f);
            switch (state.Weather)
            {
                case Weather.Storm:
                    into[(int)AmbienceLayer.Rain] = 1f;
                    into[(int)AmbienceLayer.Birds] = 0f;     // birds sit a storm out
                    into[(int)AmbienceLayer.Insects] *= 0.3f;
                    break;
                case Weather.HeatWave:
                    into[(int)AmbienceLayer.Insects] = Mathf.Max(into[(int)AmbienceLayer.Insects], 0.8f);
                    into[(int)AmbienceLayer.Wind] *= 0.4f;   // a still, baking afternoon
                    break;
                case Weather.Fog:
                    into[(int)AmbienceLayer.Birds] *= 0.4f;
                    into[(int)AmbienceLayer.Wind] *= 0.5f;
                    break;
            }
        }

        private readonly float[] _wanted = new float[Count];

        private void Update()
        {
            if (!_any || _game == null || _game.Sim == null) return;
            Mix(_game.State, _wanted);
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < Count; i++)
            {
                if (_sources[i].clip == null) continue;
                // Layers cross over in about two seconds, so weather and seasons arrive as a change of air.
                _target[i] = Mathf.MoveTowards(_target[i], _wanted[i], dt * 0.5f);
                _sources[i].volume = _target[i];
            }
        }
    }
}
