using System.Collections.Generic;
using UnityEngine;

namespace TillWinter.Unity
{
    /// <summary>Renders every apprentice in <see cref="TillWinter.Core.FarmState.Apprentices"/> from the catalogue's six character prefabs.</summary>
    public sealed class ApprenticesView : MonoBehaviour
    {
        private GameController _game;
        private VisualCatalog _catalog;
        private readonly List<ApprenticeView> _views = new List<ApprenticeView>();

        public void Init(GameController game, VisualCatalog catalog)
        {
            _game = game;
            _catalog = catalog;
            game.ApprenticeHitTest = HitTest;
        }

        /// <summary>The apprentice drawn under a screen point, or -1 (a generous finger-sized radius).</summary>
        private int HitTest(Vector2 screen)
        {
            if (_game.Cam == null) return -1;
            int best = -1;
            float bestD = 90f * 90f * (Screen.height / 2340f) * (Screen.height / 2340f);
            for (int i = 0; i < _views.Count; i++)
            {
                var p = _game.Cam.WorldToScreenPoint(_views[i].transform.position + Vector3.up * 0.3f);
                if (p.z < 0f) continue;
                float d = ((Vector2)p - screen).sqrMagnitude;
                if (d < bestD)
                {
                    bestD = d;
                    best = i;
                }
            }
            return best;
        }

        private void LateUpdate()
        {
            var list = _game.State.Apprentices;
            while (_views.Count < list.Count)
            {
                int i = _views.Count;
                var prefab = _catalog.Apprentices != null && _catalog.Apprentices.Length > 0 ? _catalog.Apprentices[i % _catalog.Apprentices.Length] : null;
                var go = _catalog.Spawn(prefab, transform, "Apprentice");
                go.name = "Apprentice " + i;
                _views.Add(go.AddComponent<ApprenticeView>().Setup(_catalog));
            }
            while (_views.Count > list.Count)
            {
                Destroy(_views[_views.Count - 1].gameObject);
                _views.RemoveAt(_views.Count - 1);
            }
            for (int i = 0; i < list.Count; i++)
                _views[i].Tick(_game, list[i], Time.deltaTime);
        }
    }

    /// <summary>Character prefab with a hat. Bobs while walking, bends while working a plot.</summary>
    public sealed class ApprenticeView : MonoBehaviour
    {
        private Transform _body;
        private Vector3 _lastPos;
        private float _bob;
        private float _facing;
        private bool _placed;
        private float _idle;
        private float _spawn;
        private Transform _bubble;
        private PaletteBinder _bubbleBinder;
        private float _bubbleShow;
        private int _bubbleTier = int.MinValue; // -1 means "water", so "not painted yet" must be something else

        public ApprenticeView Setup(VisualCatalog catalog = null)
        {
            // Everything spawned from the prefab moves under one body pivot so bob/squash apply to model + hat.
            _body = new GameObject("Body").transform;
            _body.SetParent(transform, false);
            var children = new List<Transform>();
            foreach (Transform c in transform) if (c != _body && c.name != "Bubble") children.Add(c);
            _bubble = transform.Find("Bubble");
            _bubbleBinder = _bubble != null ? _bubble.GetComponent<PaletteBinder>() : null;
            foreach (var c in children) c.SetParent(_body, false);
            // The kit's rig, if the prefab has one: idle and walk live in a blend tree ArtSetup built.
            _animator = GetComponentInChildren<Animator>(true);
            if (_animator != null && !HasSpeedParameter(_animator)) _animator = null;
            // The contact shadow stays on the root, so it does not bob with the body.
            var shadow = catalog != null ? catalog.Spawn(catalog.BlobShadow, transform, "Shadow") : null;
            if (shadow != null)
            {
                shadow.transform.localPosition = new Vector3(0f, 0.02f, 0f);
                shadow.transform.localScale = Vector3.one * 0.5f;
            }
            return this;
        }

        /// <summary>Plots per second a walking apprentice covers at full stride; the blend tree reads 1 at this speed.</summary>
        private const float WalkSpeedReference = 1.6f;
        private const string SpeedParameter = "Speed";
        /// <summary>Hashed once: Animator.SetFloat(string) allocates every call, and this runs per apprentice per frame.</summary>
        private static readonly int SpeedHash = Animator.StringToHash(SpeedParameter);
        private Animator _animator;
        private float _animSpeed;

        private static bool HasSpeedParameter(Animator animator)
        {
            if (animator.runtimeAnimatorController == null) return false;
            foreach (var p in animator.parameters) if (p.name == SpeedParameter) return true;
            return false;
        }

        public void Tick(GameController game, TillWinter.Core.ApprenticeState a, float dt)
        {
            var target = game.PlotToWorld(a.X, a.Y, 0.2f);
            if (!_placed)
            {
                _lastPos = target;
                _placed = true;
                _spawn = 0f;
            }
            // A new helper (hired in Winter) pops in with a puff once the field is in view.
            if (_spawn < 1f && game.State.Phase == TillWinter.Core.Phase.Year)
            {
                if (_spawn <= 0f) VfxPlayer.Fire(VfxId.PlotPop, target + Vector3.up * 0.1f);
                _spawn = Mathf.Min(1f, _spawn + dt / 0.4f);
            }
            var delta = target - _lastPos;
            transform.position = target;

            bool walking = a.IsWalking && delta.sqrMagnitude > 1e-6f;
            if (walking)
            {
                float before = Mathf.Sin(_bob);
                _bob += dt * 12f;
                float after = Mathf.Sin(_bob);
                if ((before <= 0f) != (after <= 0f)) // each footfall
                {
                    VfxPlayer.Fire(VfxId.StepDust, target + Vector3.up * 0.02f);
                    AudioManager.Instance?.Play(SfxId.Step, 0.6f);
                }
                _facing = Mathf.LerpAngle(_facing, Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg, 1f - Mathf.Exp(-dt * 14f));
            }
            // With the kit's walk clip bound (ArtSetup), the legs carry the walk and the bob only tops it off.
            if (_animator != null)
            {
                float speed = walking ? Mathf.Clamp01(delta.magnitude / Mathf.Max(0.0001f, dt) / WalkSpeedReference) : 0f;
                _animSpeed = Mathf.MoveTowards(_animSpeed, speed, dt * 4f);
                _animator.SetFloat(SpeedHash, _animSpeed);
            }
            float bobY = walking ? Mathf.Abs(Mathf.Sin(_bob)) * (_animator != null ? 0.025f : 0.07f) : 0f;
            // Picking: bend down to the bed and straighten with a little hop as the crop comes up.
            float pick = a.IsWorking ? Mathf.Sin(a.WorkProgress * Mathf.PI) : 0f;
            float lift = a.IsWorking && a.WorkProgress > 0.8f ? Mathf.Sin((a.WorkProgress - 0.8f) / 0.2f * Mathf.PI) * 0.05f : 0f;
            float squash = 1f - 0.1f * pick;
            // Standing still: breathe, and look about now and then.
            bool idle = !walking && !a.IsWorking;
            _idle = idle ? _idle + dt : 0f;
            float breathe = idle ? 1f + 0.02f * Mathf.Sin(_idle * 2.4f) : 1f;
            float look = idle ? Mathf.Sin(_idle * 0.6f) * Mathf.Clamp01(_idle - 0.5f) * 30f : 0f;
            _body.localPosition = new Vector3(0f, bobY + lift, 0f);
            // Thought bubble: which crop is being picked, in its colour.
            if (_bubble != null)
            {
                _bubbleShow = Prims.Damp(_bubbleShow, a.IsWorking ? 1f : 0f, 12f, dt);
                bool show = _bubbleShow > 0.02f;
                if (_bubble.gameObject.activeSelf != show) _bubble.gameObject.SetActive(show);
                if (show)
                {
                    var gp = new TillWinter.Core.GridPos(Mathf.RoundToInt(a.X), Mathf.RoundToInt(a.Y));
                    // A waterer thinks of water, a digger of the ground, a picker of the crop it is picking (GDD §2v3.9).
                    bool waterer = a.Role == TillWinter.Core.ApprenticeRole.Waterer;
                    bool digger = a.Role == TillWinter.Core.ApprenticeRole.Digger;
                    int tier = waterer ? -1 : digger ? -2 : game.State.InBounds(gp) ? game.State.GetPlot(gp).Tier : 0;
                    if (tier != _bubbleTier && _bubbleBinder != null)
                    {
                        _bubbleTier = tier;
                        _bubbleBinder.Override(PaletteSlot.Golden, waterer ? Palette.Load().Water : digger ? Palette.Load().SoilDry : Palette.Load().Crop(tier));
                    }
                    float bob = SettingsStore.MotionAllowed ? Mathf.Sin(Time.time * 4f) * 0.02f : 0f;
                    _bubble.localPosition = new Vector3(0.12f, 0.8f + bob, 0f);
                    _bubble.localScale = Vector3.one * Prims.EaseOutBack(_bubbleShow);
                    if (game.Cam != null) _bubble.rotation = Quaternion.LookRotation(game.Cam.transform.forward, Vector3.up);
                }
            }
            float grow = _spawn >= 1f ? 1f : Mathf.Max(0.001f, Prims.EaseOutBack(_spawn));
            _body.localScale = new Vector3(1f / Mathf.Sqrt(squash), squash * breathe, 1f / Mathf.Sqrt(squash)) * grow;
            _body.localRotation = Quaternion.Euler(walking ? Mathf.Sin(_bob) * 4f : pick * 28f, _facing + look, 0f);
            _lastPos = target;
        }
    }
}
