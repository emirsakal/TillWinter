using System;
using System.Collections.Generic;
using TillWinter.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TillWinter.Unity
{
    /// <summary>
    /// Generic skill-tree canvas (v3.4): the tree is a field seen from above. One <see cref="SkillTree"/>, its
    /// <see cref="SkillTreeLayout"/> and a <see cref="TreeTheme"/>. The whole field is fitted to the canvas, so
    /// there is no panning or zooming; every node is a bed (grown, tilled or hard), every prerequisite a furrow.
    /// Nodes are plain uGUI objects driven by a small state machine; furrows are one <see cref="UILines"/> mesh.
    /// No per-frame allocations while idle.
    /// </summary>
    public sealed class SkillTreeView : MonoBehaviour
    {
        public enum NodeState { Locked, Unaffordable, Affordable, Maxed }

        private sealed class NodeView
        {
            public SkillNode Node;
            public RectTransform Rt;
            public Image Bed, Crop, CropShine, Seed, Crack, Outline;
            /// <summary>The Almanac's suggestion (GDD §6.3 v2.2): a small star badge and the word that explains it.</summary>
            public Image Badge, SuggestPill;
            public NodeState State;
            public float Punch;
            public float Size;
            public int[] Edges;
        }

        private struct Flow
        {
            public int Edge;
            public float T;
        }

        public event Action<string> Selected;

        private GameController _game;
        private SkillTree _tree;
        private TreeTheme _theme;
        private Dictionary<string, LayoutPos> _layout;
        private RectTransform _viewport, _content;
        private Image _snow, _soil;
        private UILines _lines;
        private readonly Dictionary<string, NodeView> _nodes = new Dictionary<string, NodeView>();
        private readonly List<NodeView> _nodeList = new List<NodeView>();
        private readonly List<(string from, string to)> _edgeIds = new List<(string, string)>();
        private readonly List<Flow> _flows = new List<Flow>();
        private readonly List<UILines.Line> _flowLines = new List<UILines.Line>();
        private readonly HashSet<string> _wasAvailable = new HashSet<string>();
        private readonly HashSet<string> _highlight = new HashSet<string>();
        private readonly List<TMP_Text> _labels = new List<TMP_Text>();
        private string _selectedId;
        private float _unit = 1f;
        /// <summary>The unit everything was built at; a later fit (another screen shape) scales the content instead of rebuilding it.</summary>
        private float _builtUnit = 1f;
        private Vector2 _fieldSize;
        private bool _built;
        private string _suggested;

        public SkillTree Tree => _tree;
        public string SelectedId => _selectedId;
        /// <summary>Pixels per layout unit, as a fraction of the theme's reference scale: the field's fit, since there is no zoom.</summary>
        public float Zoom => _unit / Mathf.Max(1f, _theme.UnitPixels);
        public Vector2 Pan => Vector2.zero;

        public void Init(GameController game, SkillTree tree, TreeTheme theme, Dictionary<string, LayoutPos> layout)
        {
            _game = game;
            _tree = tree;
            _theme = theme;
            _layout = layout;
            _viewport = (RectTransform)transform;

            // The field itself: a snow rim around the soil, both fitted to the canvas with the layout's aspect.
            _snow = UiKit.Panel(_viewport, "Snow", theme.FieldSnow, true, false);
            _soil = UiKit.Panel(_snow.transform, "Soil", theme.FieldSoil, true, true);
            UiKit.Stretch(_soil.rectTransform, Vector2.zero, Vector2.one, Vector2.one * theme.SnowRim, -Vector2.one * theme.SnowRim);
            _content = UiKit.Rect("Content", _soil.transform);
            _content.anchorMin = _content.anchorMax = new Vector2(0.5f, 0.5f);
            _content.pivot = new Vector2(0.5f, 0.5f);
            _content.sizeDelta = Vector2.zero;

            var linesRt = UiKit.Rect("Furrows", _content);
            linesRt.anchorMin = linesRt.anchorMax = new Vector2(0.5f, 0.5f);
            linesRt.sizeDelta = Vector2.zero;
            _lines = linesRt.gameObject.AddComponent<UILines>();
            _lines.raycastTarget = false;

            Fit();
            _builtUnit = _unit;
            BuildLabels();
            BuildNodes();
            BuildEdges();
            _built = true;
            Refresh(true);
        }

        // ------------------------------------------------------------------ build

        /// <summary>Layout units to content pixels: the field's bottom-left corner is (-w/2, -h/2).</summary>
        private Vector2 ToPixels(LayoutPos p) => new Vector2((p.X - SkillTreeLayout.Width * 0.5f) * _unit, (p.Y - SkillTreeLayout.Height * 0.5f) * _unit);

        /// <summary>Sizes the field to the canvas: the largest rectangle with the layout's aspect that fits, less the padding.</summary>
        private void Fit()
        {
            var size = _viewport.rect.size;
            if (size.x < 2f || size.y < 2f) size = new Vector2(1012f, 1385f); // before the first layout pass: the reference portrait canvas
            float pad = _theme.FieldPadding + _theme.SnowRim;
            _unit = Mathf.Min((size.x - 2f * pad) / SkillTreeLayout.Width, (size.y - 2f * pad) / SkillTreeLayout.Height);
            _fieldSize = new Vector2(SkillTreeLayout.Width, SkillTreeLayout.Height) * _unit;
            var snowRt = _snow.rectTransform;
            // Hangs from the top of its canvas: the room a taller screen has goes under the field, where the text lines are.
            snowRt.anchorMin = snowRt.anchorMax = new Vector2(0.5f, 1f);
            snowRt.pivot = new Vector2(0.5f, 1f);
            snowRt.sizeDelta = _fieldSize + Vector2.one * (2f * _theme.SnowRim);
            snowRt.anchoredPosition = new Vector2(0f, -_theme.FieldPadding);
            if (_built) _content.localScale = Vector3.one * (_unit / Mathf.Max(0.001f, _builtUnit));
        }

        private void BuildLabels()
        {
            foreach (var branch in SkillTreeLayout.BranchOrder)
            {
                bool any = false;
                foreach (var n in _tree.Nodes) if (n.Branch == branch) { any = true; break; }
                if (!any) continue;
                var label = UiKit.Label(_content, "Branch " + branch, Capitals(Strings.Branch(branch)), UiType.Caption, _theme.BranchLabel, TextAnchor.MiddleCenter, FontStyle.Bold);
                label.characterSpacing = 6f;
                label.enableWordWrapping = false;
                UiKit.Box(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), ToPixels(SkillTreeLayout.LabelOf(branch)), new Vector2(_unit * 4f, _unit * 0.6f));
                _labels.Add(label);
            }
        }

        /// <summary>Upper case in the game's language: Turkish dots its capital I and keeps its dotless one.</summary>
        private static string Capitals(string text) =>
            GameLanguage.Current == GameLanguage.Turkish ? text.ToUpper(new System.Globalization.CultureInfo("tr-TR")) : text.ToUpperInvariant();

        private float SizeOf(SkillNode node)
        {
            bool root = true, parent = false;
            foreach (var p in node.Prerequisites) if (_layout.ContainsKey(p)) { root = false; break; }
            foreach (var n in _tree.Nodes) if (Array.IndexOf(n.Prerequisites, node.Id) >= 0) { parent = true; break; }
            return (root ? _theme.BedRoot : parent ? _theme.BedMid : _theme.BedLeaf) * _unit;
        }

        private void BuildNodes()
        {
            foreach (var node in _tree.Nodes)
            {
                if (!_layout.TryGetValue(node.Id, out var pos)) continue;
                var nv = new NodeView { Node = node, Size = SizeOf(node) };
                float size = nv.Size;
                nv.Rt = UiKit.Rect("Node " + node.Id, _content);
                nv.Rt.anchorMin = nv.Rt.anchorMax = new Vector2(0.5f, 0.5f);
                nv.Rt.sizeDelta = new Vector2(size, size);
                nv.Rt.anchoredPosition = ToPixels(pos);

                // The selection outline sits behind the bed, a little larger.
                nv.Outline = UiKit.Panel(nv.Rt, "Outline", _theme.Highlight, true, false);
                UiKit.Box(nv.Outline.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * (size + _unit * 0.16f));
                nv.Outline.gameObject.SetActive(false);

                nv.Bed = UiKit.Panel(nv.Rt, "Bed", _theme.BedHard, true, true);
                UiKit.Box(nv.Bed.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * size);
                var btn = nv.Bed.gameObject.AddComponent<Button>();
                btn.targetGraphic = nv.Bed;
                btn.transition = Selectable.Transition.None;
                string id = node.Id;
                btn.onClick.AddListener(() => Select(id));

                // Hard ground: a crack. Tilled: a seed. Grown: the crop, with a shine.
                nv.Crack = UiKit.Panel(nv.Rt, "Crack", _theme.Crack, false, false);
                UiKit.Box(nv.Crack.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-size * 0.04f, size * 0.02f), new Vector2(size * 0.42f, Mathf.Max(2f, size * 0.05f)));
                nv.Crack.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -28f);
                nv.Seed = UiKit.CircleImage(nv.Rt, "Seed", _theme.SeedDot, Vector2.zero, size * 0.2f);
                nv.Crop = UiKit.CircleImage(nv.Rt, "Crop", _theme.Crop, Vector2.zero, size * 0.48f);
                nv.CropShine = UiKit.CircleImage(nv.Rt, "Shine", _theme.CropHighlight, new Vector2(-size * 0.14f, size * 0.12f), size * 0.2f);

                nv.Badge = UiKit.CircleImage(nv.Rt, "Suggested", _theme.Accent, new Vector2(-size * 0.42f, size * 0.42f), size * 0.4f);
                nv.Badge.raycastTarget = false;
                var badgeStar = NodeIcons.Image(nv.Badge.transform, "star", _theme.Card);
                UiKit.Box(badgeStar.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * (size * 0.26f));
                nv.Badge.gameObject.SetActive(false);
                // A star on its own says nothing. Only one node wears it at a time, so it can afford a word.
                nv.SuggestPill = UiKit.Panel(nv.Rt, "SuggestedLabel", _theme.Accent, true, false);
                nv.SuggestPill.raycastTarget = false;
                UiKit.Box(nv.SuggestPill.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, size * 0.5f + _unit * 0.34f), new Vector2(_unit * 1.7f, _unit * 0.42f));
                var pillText = UiKit.Label(nv.SuggestPill.transform, "Text", Strings.Get("ui.suggested"), UiType.Caption, _theme.Card, TextAnchor.MiddleCenter, FontStyle.Bold);
                pillText.raycastTarget = false;
                pillText.enableWordWrapping = false;
                pillText.enableAutoSizing = true;
                pillText.fontSizeMax = pillText.fontSize;
                pillText.fontSizeMin = 12f;
                UiKit.Stretch(pillText.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 0f), new Vector2(-8f, 0f));
                nv.SuggestPill.gameObject.SetActive(false);

                _nodes[node.Id] = nv;
                _nodeList.Add(nv);
            }
            // Labels are written on the soil under the beds.
            foreach (var label in _labels) label.transform.SetSiblingIndex(1);
        }

        private void BuildEdges()
        {
            _lines.Clear();
            _edgeIds.Clear();
            float width = _theme.FurrowWidth * _unit;
            foreach (var node in _tree.Nodes)
            {
                if (!_nodes.TryGetValue(node.Id, out var nv)) continue;
                var list = new List<int>();
                foreach (var p in node.Prerequisites)
                {
                    if (!_layout.ContainsKey(p)) continue;
                    _edgeIds.Add((p, node.Id));
                    list.Add(_lines.Count);
                    _lines.Add(ToPixels(_layout[p]), ToPixels(_layout[node.Id]), _theme.Furrow, width);
                }
                nv.Edges = list.ToArray();
            }
        }

        // ------------------------------------------------------------------ state

        public NodeState StateOf(string id)
        {
            var sim = _game.Sim;
            if (!sim.IsAvailable(id)) return NodeState.Locked;
            if (sim.IsMaxed(id)) return NodeState.Maxed;
            return Affordability(id);
        }

        private NodeState Affordability(string id)
        {
            var sim = _game.Sim;
            double currency = _tree.Kind == TreeKind.Almanac ? _game.State.Coins : _game.State.Seeds;
            return currency >= sim.CostOf(id) ? NodeState.Affordable : NodeState.Unaffordable;
        }

        /// <summary>Re-reads levels and availability; animates newly available furrows unless <paramref name="silent"/>.</summary>
        public void Refresh(bool silent = false)
        {
            if (!_built) return;
            foreach (var nv in _nodeList)
            {
                var state = StateOf(nv.Node.Id);
                nv.State = state;
                int level = _tree.GetLevel(nv.Node.Id);
                bool available = state != NodeState.Locked;
                bool grown = level > 0;
                nv.Bed.color = grown ? _theme.BedGrown : available ? _theme.BedTilled : _theme.BedHard;
                // Hard ground is a smaller, rougher square; a worked bed fills its plot.
                nv.Bed.rectTransform.sizeDelta = Vector2.one * (available ? nv.Size : nv.Size * 0.8f);
                nv.Crack.gameObject.SetActive(!available);
                nv.Seed.gameObject.SetActive(available && !grown);
                var seed = _theme.SeedDot;
                if (state == NodeState.Unaffordable) seed.a *= 0.55f;
                nv.Seed.color = seed;
                nv.Crop.gameObject.SetActive(grown);
                nv.CropShine.gameObject.SetActive(grown);
                nv.Crop.color = state == NodeState.Maxed ? _theme.CropMaxed : _theme.Crop;
                nv.CropShine.color = state == NodeState.Maxed ? Color.Lerp(_theme.CropMaxed, Color.white, 0.5f) : _theme.CropHighlight;
                if (available && !_wasAvailable.Contains(nv.Node.Id))
                {
                    _wasAvailable.Add(nv.Node.Id);
                    if (!silent) foreach (int e in nv.Edges) _flows.Add(new Flow { Edge = e, T = 0f });
                }
                nv.Outline.gameObject.SetActive(nv.Node.Id == _selectedId);
            }
            float width = _theme.FurrowWidth * _unit;
            for (int i = 0; i < _edgeIds.Count; i++)
            {
                var (from, to) = _edgeIds[i];
                bool lit = _tree.GetLevel(from) > 0 && _tree.GetLevel(to) > 0;
                _lines.SetColor(i, lit ? _theme.FurrowLit : _theme.Furrow);
                _lines.SetWidth(i, lit ? width * 0.8f : width, lit ? width * 0.8f : width);
            }
        }

        /// <summary>Marks one node as the suggestion (null clears it).</summary>
        public void SetSuggested(string id)
        {
            if (id == _suggested) return;
            _suggested = id;
            for (int i = 0; i < _nodeList.Count; i++)
            {
                bool on = _nodeList[i].Node.Id == id;
                _nodeList[i].Badge.gameObject.SetActive(on);
                _nodeList[i].SuggestPill.gameObject.SetActive(on);
                // The pill reaches past the bed, so the suggested bed draws over its neighbours while it wears one.
                if (on) _nodeList[i].Rt.SetAsLastSibling();
            }
        }

        public void Select(string id)
        {
            if (!_nodes.ContainsKey(id)) return;
            _selectedId = id;
            foreach (var nv in _nodeList) nv.Outline.gameObject.SetActive(nv.Node.Id == id);
            Selected?.Invoke(id);
        }

        public void Deselect()
        {
            _selectedId = null;
            foreach (var nv in _nodeList) nv.Outline.gameObject.SetActive(false);
        }

        /// <summary>Purchase feedback: the bed punches, dirt flies, the furrows to its children flow.</summary>
        public void OnPurchased(string id)
        {
            if (_nodes.TryGetValue(id, out var nv)) { nv.Punch = 1f; Sparkle(nv); }
            Refresh(false);
        }

        // ------------------------------------------------------------------ purchase sparkle

        private const int SparkCount = 10;
        private const float SparkSeconds = 0.55f;
        private readonly RectTransform[] _sparks = new RectTransform[SparkCount];
        private readonly Image[] _sparkImages = new Image[SparkCount];
        private readonly Vector2[] _sparkDir = new Vector2[SparkCount];
        private readonly Vector2[] _sparkHome = new Vector2[SparkCount];
        private float _sparkT = 1f;
        private Color _sparkColor;

        /// <summary>A handful of clods thrown out of a bed that was just worked.</summary>
        private void Sparkle(NodeView nv)
        {
            _sparkColor = nv.State == NodeState.Maxed ? _theme.CropMaxed : _theme.Crop;
            for (int i = 0; i < SparkCount; i++)
            {
                if (_sparks[i] == null)
                {
                    var dot = UiKit.CircleImage(_content, "Spark", _sparkColor, Vector2.zero, _unit * 0.1f);
                    dot.raycastTarget = false;
                    _sparkImages[i] = dot;
                    _sparks[i] = dot.rectTransform;
                    _sparks[i].anchorMin = _sparks[i].anchorMax = new Vector2(0.5f, 0.5f);
                }
                float angle = (i / (float)SparkCount) * Mathf.PI * 2f + UnityEngine.Random.value * 0.3f;
                _sparkDir[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (_unit * (0.6f + UnityEngine.Random.value * 0.4f));
                _sparkHome[i] = nv.Rt.anchoredPosition;
                _sparks[i].anchoredPosition = _sparkHome[i];
                _sparkImages[i].color = _sparkColor;
                _sparks[i].gameObject.SetActive(true);
            }
            _sparkT = 0f;
        }

        private void TickSparks(float dt)
        {
            if (_sparkT >= 1f) return;
            _sparkT = Mathf.Min(1f, _sparkT + dt / SparkSeconds);
            float eased = Prims.EaseOutQuad(_sparkT);
            var colour = _sparkColor;
            colour.a = 1f - _sparkT;
            for (int i = 0; i < SparkCount; i++)
            {
                if (_sparks[i] == null) continue;
                _sparks[i].anchoredPosition = _sparkHome[i] + _sparkDir[i] * eased;
                _sparks[i].localScale = Vector3.one * (1f - 0.6f * _sparkT);
                _sparkImages[i].color = colour;
                if (_sparkT >= 1f) _sparks[i].gameObject.SetActive(false);
            }
        }

        // ------------------------------------------------------------------ opening, onboarding, compatibility

        /// <summary>The field fades in as the page opens; an instant cut from the farm to a page of beds jarred.</summary>
        public void OnOpened()
        {
            Fit();
            StartCoroutine(OpenFadeRoutine());
        }

        private System.Collections.IEnumerator OpenFadeRoutine()
        {
            var group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();
            if (SettingsStore.Current.ReduceMotion) { group.alpha = 1f; yield break; }
            for (float t = 0f; t < UiMotion.Normal; t += Time.unscaledDeltaTime)
            {
                group.alpha = UiMotion.EaseOut(t / UiMotion.Normal);
                yield return null;
            }
            group.alpha = 1f;
        }

        public void OnClosed() { }

        /// <summary>Onboarding: these nodes pulse until <see cref="ClearHighlight"/>.</summary>
        public void Highlight(params string[] ids)
        {
            _highlight.Clear();
            foreach (var id in ids) if (_nodes.ContainsKey(id)) _highlight.Add(id);
        }

        public void ClearHighlight() => _highlight.Clear();
        public bool HasHighlight => _highlight.Count > 0;

        /// <summary>The whole field is always on screen, so centring, panning and zooming are no-ops kept for callers.</summary>
        public void CenterOn(params string[] ids) { }
        public void CenterOnRoots() { }
        public void PanBy(Vector2 delta) { }
        public void ZoomBy(float factor, Vector2? screenPivot = null) { }

        private void Update()
        {
            if (!_built) return;
            float dt = Time.unscaledDeltaTime;
            TickSparks(dt);

            // Bed animation: affordable pulse, onboarding pulse, selection scale, purchase punch.
            float pulse = 1f + _theme.PulseAmplitude * Mathf.Sin(Time.unscaledTime * 4f);
            bool moving = SettingsStore.MotionAllowed;
            foreach (var nv in _nodeList)
            {
                float s = 1f;
                if (moving && nv.State == NodeState.Affordable) s *= pulse;
                if (_highlight.Contains(nv.Node.Id)) s *= 1f + 0.12f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3f));
                if (nv.Node.Id == _selectedId) s *= 1.08f;
                if (nv.Punch > 0f)
                {
                    nv.Punch = Mathf.Max(0f, nv.Punch - dt * 3f);
                    s *= 1f + 0.3f * Mathf.Sin(nv.Punch * Mathf.PI);
                    nv.Bed.color = Color.Lerp(_theme.BedGrown, _theme.Highlight, nv.Punch * 0.6f);
                }
                nv.Rt.localScale = Vector3.one * s;
            }

            // Furrow flow animation: a newly opened furrow runs from the parent to the child.
            if (_flows.Count > 0)
            {
                _flowLines.Clear();
                for (int i = _flows.Count - 1; i >= 0; i--)
                {
                    var f = _flows[i];
                    f.T += dt * 1.6f;
                    if (f.T >= 1f) { _flows.RemoveAt(i); continue; }
                    _flows[i] = f;
                    float t0 = Mathf.Clamp01(f.T - 0.15f), t1 = Mathf.Clamp01(f.T);
                    _flowLines.Add(new UILines.Line { A = _lines.PointAt(f.Edge, t0), B = _lines.PointAt(f.Edge, t1), Color = _theme.Highlight, Width = _theme.FurrowWidth * _unit * 1.2f });
                }
                _lines.SetFlows(_flowLines);
                if (_flows.Count == 0) { _flowLines.Clear(); _lines.SetFlows(_flowLines); }
            }
        }
    }
}
