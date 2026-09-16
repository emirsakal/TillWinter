using TillWinter.Core;
using UnityEngine;

namespace TillWinter.Unity
{
    /// <summary>
    /// The farm dog. It does nothing for the economy: it naps by the kennel, trots along the grass behind the fence,
    /// runs to the fence and bounces at any crow on the field, and when the player pats it a heart pops over its head
    /// for a couple of seconds. The tap is swallowed (see GameController.DogHitTest) so petting the dog never waters
    /// the plot behind it.
    /// </summary>
    public sealed class DogView : MonoBehaviour
    {
        private const float HeartSeconds = 2f;
        private const float WalkSpeed = 0.8f;
        private const float RunSpeed = 2.4f;

        private GameController _game;
        private AudioManager _audio;
        private Transform _tail;
        private readonly Transform[] _legs = new Transform[4];
        private GameObject _bubble;
        private float _heartLeft;
        private float _phase;

        private bool _hasArea;
        private Vector3 _home;
        private float _minX, _maxX, _minZ, _maxZ;
        private Vector3 _target;
        private float _wait;
        private float _facing;
        private float _stride;
        private bool _alert;

        public void Init(GameController game, AudioManager audio)
        {
            _game = game;
            _audio = audio;
            _tail = transform.Find("Tail");
            for (int i = 0; i < 4; i++) _legs[i] = transform.Find("Leg" + i);
            _bubble = transform.Find("Bubble")?.gameObject;
            if (_bubble != null) _bubble.SetActive(false);
            _phase = Random.value * 10f;
            _game.DogHitTest = HitTest;
        }

        /// <summary>Where the dog lives (local to the island) and the strip of grass it may trot along.</summary>
        public void SetArea(Vector3 home, float minX, float maxX, float minZ, float maxZ)
        {
            _hasArea = true;
            _home = home;
            _minX = minX;
            _maxX = maxX;
            _minZ = minZ;
            _maxZ = maxZ;
            transform.localPosition = home;
            _target = home;
            _facing = transform.localEulerAngles.y;
            _wait = 2f + Random.value * 3f;
        }

        private void OnDestroy()
        {
            // Only give up the hook if it is still ours: a rebuilt dog will have claimed it.
            if (_game != null && (System.Func<Vector2, bool>)HitTest == _game.DogHitTest) _game.DogHitTest = null;
        }

        /// <summary>Same screen-space test the rain cloud uses: a generous radius around where the dog is drawn.</summary>
        private bool HitTest(Vector2 screen)
        {
            if (_game.Cam == null || !gameObject.activeInHierarchy) return false;
            var sp = _game.Cam.WorldToScreenPoint(transform.position + Vector3.up * 0.3f);
            if (sp.z < 0f) return false;
            float radius = Screen.height * 0.05f;
            if ((new Vector2(sp.x, sp.y) - screen).sqrMagnitude > radius * radius) return false;
            Pat();
            return true;
        }

        private void Pat()
        {
            _heartLeft = HeartSeconds;
            if (_bubble != null) _bubble.SetActive(true);
            _audio?.Play(SfxId.UiClick);
            Haptics.Play(HapticKind.Selection);
        }

        /// <summary>Picks where to go next; a crow on the field always wins.</summary>
        private void Decide(float dt)
        {
            var state = _game.State;
            _alert = false;
            if (state.Phase == Phase.Year && state.Crows.Count > 0 && transform.parent != null)
            {
                var crow = transform.parent.InverseTransformPoint(_game.PlotToWorld(state.Crows[0].Pos));
                _target = new Vector3(Mathf.Clamp(crow.x, _minX, _maxX), 0f, _minZ);
                _alert = true;
                return;
            }
            if (state.Phase != Phase.Year)
            {
                _target = _home; // Winter: back to the kennel
                return;
            }
            _wait -= dt;
            if (_wait > 0f) return;
            _wait = 3f + Random.value * 4f;
            _target = Random.value < 0.35f
                ? _home
                : new Vector3(Random.Range(_minX, _maxX), 0f, Random.Range(_minZ, _maxZ));
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            float t = Time.time + _phase;
            bool patted = _heartLeft > 0f;

            float moving = 0f;
            float bark = 0f;
            if (_hasArea && _game.State != null)
            {
                Decide(dt);
                var pos = transform.localPosition;
                var to = _target - pos;
                to.y = 0f;
                float dist = to.magnitude;
                if (!patted && dist > 0.04f)
                {
                    float speed = _alert ? RunSpeed : WalkSpeed;
                    float step = Mathf.Min(dist, speed * dt);
                    pos += to / dist * step;
                    transform.localPosition = pos;
                    _facing = Mathf.LerpAngle(_facing, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, 1f - Mathf.Exp(-dt * 10f));
                    moving = _alert ? 1f : 0.6f;
                    _stride += dt * (_alert ? 22f : 12f);
                }
                else if (_alert)
                {
                    // At the fence: face the field and bounce.
                    _facing = Mathf.LerpAngle(_facing, 180f, 1f - Mathf.Exp(-dt * 10f));
                    bark = Mathf.Abs(Mathf.Sin(t * 9f));
                }
                float hop = moving > 0f ? Mathf.Abs(Mathf.Sin(_stride)) * 0.04f * moving : bark * 0.09f;
                transform.localRotation = Quaternion.Euler(moving > 0f ? Mathf.Sin(_stride) * 3f : -bark * 12f, _facing, 0f);
                var lp = transform.localPosition;
                transform.localPosition = new Vector3(lp.x, hop, lp.z);
            }
            for (int i = 0; i < 4; i++)
            {
                if (_legs[i] == null) continue;
                float swing = moving > 0f ? Mathf.Sin(_stride + (i == 0 || i == 3 ? 0f : Mathf.PI)) * 35f * moving : 0f;
                _legs[i].localRotation = Quaternion.Euler(swing, 0f, 0f);
            }

            // Wags faster while it is being made a fuss of, or when it is on to a crow.
            if (_tail != null)
            {
                bool excited = patted || _alert;
                float speed = excited ? 16f : 5f;
                float sweep = excited ? 34f : 14f;
                _tail.localRotation = Quaternion.Euler(0f, Mathf.Sin(t * speed) * sweep, 0f);
            }
            if (_heartLeft > 0f)
            {
                _heartLeft -= dt;
                if (_bubble != null)
                {
                    float k = Mathf.Clamp01(_heartLeft / HeartSeconds);
                    float pop = Prims.EaseOutQuad(Mathf.Clamp01((HeartSeconds - _heartLeft) * 6f));
                    _bubble.transform.localScale = Vector3.one * pop * Mathf.Clamp01(k * 3f);
                    _bubble.transform.localPosition = new Vector3(0f, 0.72f + (1f - k) * 0.18f, 0.1f);
                    if (_heartLeft <= 0f) _bubble.SetActive(false);
                }
            }
        }
    }
}
