using UnityEngine;

namespace TillWinter.Unity
{
    /// <summary>
    /// The farm dog beside its kennel. It does nothing for the economy: it wags, it breathes, and when the player
    /// pats it a heart pops over its head for a couple of seconds. The tap is swallowed (see GameController.DogHitTest)
    /// so petting the dog never waters the plot behind it.
    /// </summary>
    public sealed class DogView : MonoBehaviour
    {
        private const float HeartSeconds = 2f;

        private GameController _game;
        private AudioManager _audio;
        private Transform _tail;
        private GameObject _bubble;
        private float _heartLeft;
        private float _phase;

        public void Init(GameController game, AudioManager audio)
        {
            _game = game;
            _audio = audio;
            _tail = transform.Find("Tail");
            _bubble = transform.Find("Bubble")?.gameObject;
            if (_bubble != null) _bubble.SetActive(false);
            _phase = Random.value * 10f;
            _game.DogHitTest = HitTest;
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

        private void LateUpdate()
        {
            float t = Time.time + _phase;
            // Wags faster while it is being made a fuss of.
            if (_tail != null)
            {
                float speed = _heartLeft > 0f ? 16f : 5f;
                float sweep = _heartLeft > 0f ? 34f : 14f;
                _tail.localRotation = Quaternion.Euler(0f, Mathf.Sin(t * speed) * sweep, 0f);
            }
            if (_heartLeft > 0f)
            {
                _heartLeft -= Time.deltaTime;
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
