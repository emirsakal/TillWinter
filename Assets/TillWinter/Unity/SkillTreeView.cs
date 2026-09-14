using System;
using System.Collections.Generic;
using TillWinter.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TillWinter.Unity
{
    /// <summary>
    /// Generic pannable/zoomable skill-tree canvas: one <see cref="SkillTree"/>, its layout, a <see cref="TreeTheme"/>.
    /// Works for the Almanac (S4) and the Heritage tree (S5). Nodes are plain uGUI objects driven by a small
    /// state machine; edges are one <see cref="UILines"/> mesh. No per-frame allocations while idle.
    /// </summary>
    public sealed class SkillTreeView : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
    {
        public enum NodeState { Locked, Unaffordable, Affordable, Maxed }

        private sealed class NodeView
        {
            public SkillNode Node;
            public RectTransform Rt;
            public Image Ring, Inner, Lock;
            public TMP_Text Glyph, LevelText;
            public Image[] Pips;
            public NodeState State;
            public Color BranchColor;
            public float Punch;
            public int ShownPips;
            public float PipTimer;
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
        private UILines _lines;
        private readonly Dictionary<string, NodeView> _nodes = new Dictionary<string, NodeView>();
        private readonly List<NodeView> _nodeList = new List<NodeView>();
        private readonly List<(string from, string to)> _edgeIds = new List<(string, string)>();
        private readonly List<Flow> _flows = new List<Flow>();
        private readonly List<UILines.Line> _flowLines = new List<UILines.Line>();
        private readonly HashSet<string> _wasAvailable = new HashSet<string>();
        private string _selectedId;
        private float _zoom = 1f;
        private Vector2 _velocity;
        private bool _dragging;
        private int _pointers;
        private float _pinchDistance;
        private Vector2 _contentMin, _contentMax;
        private bool _viewInitialised;
        private Vector2 _savedPan;
        private float _savedZoom = 1f;
        private bool _hasSavedView;
        private readonly HashSet<string> _highlight = new HashSet<string>();

        public SkillTree Tree => _tree;
        public string SelectedId => _selectedId;
        public float Zoom => _zoom;
        public Vector2 Pan => _content != null ? _content.anchoredPosition : Vector2.zero;

        public void Init(GameController game, SkillTree tree, TreeTheme theme, Dictionary<string, LayoutPos> layout)
        {
            _game = game;
            _tree = tree;
            _theme = theme;
            _layout = layout;
            _viewport = (RectTransform)transform;
            if (GetComponent<Image>() == null)
            {
                var img = gameObject.AddComponent<Image>();
                img.color = new Color(0f, 0f, 0f, 0.001f);
            }
            if (GetComponent<RectMask2D>() == null) gameObject.AddComponent<RectMask2D>();

            _content = UiKit.Rect("Content", _viewport);
            _content.anchorMin = _content.anchorMax = new Vector2(0.5f, 0.5f);
            _content.pivot = new Vector2(0.5f, 0.5f);
            _content.sizeDelta = Vector2.zero;

            var linesRt = UiKit.Rect("Edges", _content);
            linesRt.anchorMin = linesRt.anchorMax = new Vector2(0.5f, 0.5f);
            linesRt.sizeDelta = Vector2.zero;
            _lines = linesRt.gameObject.AddComponent<UILines>();
            _lines.raycastTarget = false;

            BuildNodes();
            BuildEdges();
            ComputeBounds();
            Refresh(true);
            CenterOnRoots();
        }

        // ------------------------------------------------------------------ build

        private Vector2 ToPixels(LayoutPos p) => new Vector2(p.X, p.Y) * _theme.UnitPixels;

        private void BuildNodes()
        {
            foreach (var node in _tree.Nodes)
            {
                var nv = new NodeView { Node = node, BranchColor = _theme.BranchColor(node.Branch) };
                float size = _theme.NodeSize;
                nv.Rt = UiKit.Rect("Node " + node.Id, _content);
                nv.Rt.anchorMin = nv.Rt.anchorMax = new Vector2(0.5f, 0.5f);
                nv.Rt.sizeDelta = new Vector2(size, size);
                nv.Rt.anchoredPosition = ToPixels(_layout[node.Id]);

                nv.Ring = UiKit.CircleImage(nv.Rt, "Ring", nv.BranchColor, Vector2.zero, size, true);
                var btn = nv.Ring.gameObject.AddComponent<Button>();
                btn.targetGraphic = nv.Ring;
                btn.transition = Selectable.Transition.None;
                string id = node.Id;
                btn.onClick.AddListener(() => Select(id));
                nv.Inner = UiKit.CircleImage(nv.Rt, "Inner", _theme.Paper, Vector2.zero, size * 0.74f);
                nv.Glyph = UiKit.Label(nv.Rt, "Glyph", Strings.Glyph(node), Mathf.RoundToInt(size * 0.26f), _theme.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
                UiKit.Stretch(nv.Glyph.rectTransform, Vector2.zero, Vector2.one, new Vector2(4f, 10f), new Vector2(-4f, -8f));
                nv.LevelText = UiKit.Label(nv.Rt, "Level", "", Mathf.RoundToInt(size * 0.16f), _theme.InkMuted, TextAnchor.LowerCenter);
                UiKit.Stretch(nv.LevelText.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 18f), new Vector2(0f, 0f));

                int max = node.MaxLevel < 0 ? 0 : Mathf.Min(node.MaxLevel, 8);
                nv.Pips = new Image[max];
                for (int i = 0; i < max; i++)
                {
                    float a = Mathf.PI * (0.5f + 0.5f) + (i + 0.5f) / max * Mathf.PI * 2f; // start at the bottom, clockwise
                    a = -Mathf.PI * 0.5f + (i + 0.5f) / max * Mathf.PI * 2f;
                    var pos = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (size * 0.5f + 9f);
                    nv.Pips[i] = UiKit.CircleImage(nv.Rt, "Pip" + i, _theme.EdgeDim, pos, 12f);
                }
                nv.Lock = UiKit.CircleImage(nv.Rt, "Lock", _theme.InkMuted, new Vector2(size * 0.32f, -size * 0.32f), size * 0.3f);
                var lockGlyph = UiKit.Label(nv.Lock.transform, "L", "×", Mathf.RoundToInt(size * 0.2f), _theme.Paper, TextAnchor.MiddleCenter, FontStyle.Bold);
                lockGlyph.rectTransform.offsetMin = Vector2.zero;
                lockGlyph.rectTransform.offsetMax = Vector2.zero;

                _nodes[node.Id] = nv;
                _nodeList.Add(nv);
            }
        }

        private void BuildEdges()
        {
            _lines.Clear();
            _edgeIds.Clear();
            foreach (var node in _tree.Nodes)
            {
                var list = new List<int>();
                foreach (var p in node.Prerequisites)
                {
                    if (!_layout.ContainsKey(p)) continue;
                    _edgeIds.Add((p, node.Id));
                    list.Add(_lines.Count);
                    _lines.Add(ToPixels(_layout[p]), ToPixels(_layout[node.Id]), _theme.EdgeDim, _theme.EdgeWidth);
                }
                _nodes[node.Id].Edges = list.ToArray();
            }
        }

        private void ComputeBounds()
        {
            var b = SkillTreeLayout.Bounds(_layout);
            float pad = _theme.NodeSize * 1.2f;
            _contentMin = new Vector2(b.minX, b.minY) * _theme.UnitPixels - Vector2.one * pad;
            _contentMax = new Vector2(b.maxX, b.maxY) * _theme.UnitPixels + Vector2.one * pad;
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

        /// <summary>Re-reads levels and availability; animates newly available edges unless <paramref name="silent"/>.</summary>
        public void Refresh(bool silent = false)
        {
            foreach (var nv in _nodeList)
            {
                var state = StateOf(nv.Node.Id);
                nv.State = state;
                int level = _tree.GetLevel(nv.Node.Id);
                bool available = state != NodeState.Locked;
                var col = available ? nv.BranchColor : TreeTheme.Desaturate(nv.BranchColor, _theme.LockedSaturation);
                if (!available) col.a = _theme.LockedAlpha;
                if (state == NodeState.Maxed) col = _theme.Gold;
                nv.Ring.color = col;
                nv.Inner.color = available ? _theme.Paper : TreeTheme.Desaturate(_theme.Paper, 0.6f);
                nv.Glyph.color = available ? _theme.Ink : _theme.InkMuted;
                nv.Lock.gameObject.SetActive(!available);
                int max = _game.Sim.GetMaxLevel(nv.Node.Id);
                nv.LevelText.text = nv.Pips.Length == 0 || state == NodeState.Maxed ? (max > 0 ? level + "/" + max : "") : "";
                if (silent) nv.ShownPips = level;
                if (nv.Pips.Length > 0)
                {
                    for (int i = 0; i < nv.Pips.Length; i++)
                        nv.Pips[i].gameObject.SetActive(state != NodeState.Maxed);
                    UpdatePips(nv);
                }
                if (available && !_wasAvailable.Contains(nv.Node.Id))
                {
                    _wasAvailable.Add(nv.Node.Id);
                    if (!silent) foreach (int e in nv.Edges) _flows.Add(new Flow { Edge = e, T = 0f });
                }
            }
            for (int i = 0; i < _edgeIds.Count; i++)
            {
                var (from, to) = _edgeIds[i];
                bool lit = _tree.GetLevel(from) > 0;
                _lines.SetColor(i, lit ? _nodes[to].BranchColor : _theme.EdgeDim);
            }
        }

        private void UpdatePips(NodeView nv)
        {
            for (int i = 0; i < nv.Pips.Length; i++)
                nv.Pips[i].color = i < nv.ShownPips ? nv.BranchColor : _theme.EdgeDim;
        }

        public void Select(string id)
        {
            if (!_nodes.ContainsKey(id)) return;
            _selectedId = id;
            Selected?.Invoke(id);
        }

        public void Deselect() => _selectedId = null;

        /// <summary>Purchase feedback: burst on the node, pips fill one by one, children edges flow.</summary>
        public void OnPurchased(string id)
        {
            if (_nodes.TryGetValue(id, out var nv)) nv.Punch = 1f;
            Refresh(false);
        }

        // ------------------------------------------------------------------ camera

        public void CenterOnRoots()
        {
            float sumX = 0f, minY = float.MaxValue;
            int n = 0;
            foreach (var node in _tree.Nodes)
            {
                if (node.Prerequisites.Length > 0) continue;
                var p = ToPixels(_layout[node.Id]);
                sumX += p.x;
                minY = Mathf.Min(minY, p.y);
                n++;
            }
            if (n == 0) return;
            _zoom = Mathf.Clamp(_theme.InitialZoom, _theme.ZoomMin, _theme.ZoomMax);
            _content.localScale = Vector3.one * _zoom;
            var focus = new Vector2(sumX / n, minY + _theme.NodeSize * 1.6f) * _zoom;
            _content.anchoredPosition = -focus + new Vector2(0f, -_viewport.rect.height * 0.28f);
            ClampPan(true);
            _viewInitialised = true;
        }

        /// <summary>First open ever: roots; later opens (saved in Core): last pan/zoom.</summary>
        public void OnOpened()
        {
            var mem = _tree.Kind == TreeKind.Almanac ? _game.State.AlmanacView : _game.State.HeritageView;
            if (mem.HasView)
            {
                _zoom = Mathf.Clamp(mem.Zoom, _theme.ZoomMin, _theme.ZoomMax);
                _content.localScale = Vector3.one * _zoom;
                _content.anchoredPosition = new Vector2(mem.PanX, mem.PanY);
                _hasSavedView = true;
            }
            else CenterOnRoots();
            _velocity = Vector2.zero;
        }

        public void OnClosed()
        {
            _savedPan = _content.anchoredPosition;
            _savedZoom = _zoom;
            _hasSavedView = true;
            _game.Sim.RememberTreeView(_tree.Kind, _savedPan.x, _savedPan.y, _zoom);
        }

        /// <summary>Onboarding: these nodes pulse until <see cref="ClearHighlight"/>.</summary>
        public void Highlight(params string[] ids)
        {
            _highlight.Clear();
            foreach (var id in ids) if (_nodes.ContainsKey(id)) _highlight.Add(id);
        }

        public void ClearHighlight() => _highlight.Clear();
        public bool HasHighlight => _highlight.Count > 0;

        /// <summary>Centres the canvas on the average position of the given nodes.</summary>
        public void CenterOn(params string[] ids)
        {
            Vector2 sum = Vector2.zero;
            int n = 0;
            foreach (var id in ids) if (_layout.ContainsKey(id)) { sum += ToPixels(_layout[id]); n++; }
            if (n == 0) return;
            _content.anchoredPosition = -(sum / n) * _zoom;
            ClampPan(true);
        }

        public void PanBy(Vector2 delta)
        {
            _content.anchoredPosition += delta;
            ClampPan(false);
        }

        public void ZoomBy(float factor, Vector2? screenPivot = null)
        {
            float target = Mathf.Clamp(_zoom * factor, _theme.ZoomMin, _theme.ZoomMax);
            factor = target / _zoom;
            if (Mathf.Approximately(factor, 1f)) return;
            Vector2 pivot = Vector2.zero;
            if (screenPivot.HasValue)
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_viewport, screenPivot.Value, null, out pivot);
            var pos = _content.anchoredPosition;
            _content.anchoredPosition = pivot + (pos - pivot) * factor;
            _zoom = target;
            _content.localScale = Vector3.one * _zoom;
            ClampPan(false);
        }

        private void ClampPan(bool hard)
        {
            var half = _viewport.rect.size * 0.5f;
            var min = -_contentMax * _zoom + half * 0.4f;
            var max = -_contentMin * _zoom - half * 0.4f;
            var p = _content.anchoredPosition;
            var target = new Vector2(Mathf.Clamp(p.x, Mathf.Min(min.x, max.x), Mathf.Max(min.x, max.x)), Mathf.Clamp(p.y, Mathf.Min(min.y, max.y), Mathf.Max(min.y, max.y)));
            _content.anchoredPosition = hard ? target : Vector2.Lerp(p, target, 0.5f); // soft edge: half-way pull back per frame
        }

        // ------------------------------------------------------------------ input

        public void OnPointerDown(PointerEventData e)
        {
            _pointers++;
            _velocity = Vector2.zero;
        }

        public void OnPointerUp(PointerEventData e)
        {
            _pointers = Mathf.Max(0, _pointers - 1);
        }

        public void OnBeginDrag(PointerEventData e) => _dragging = true;

        public void OnDrag(PointerEventData e)
        {
            if (_pointers > 1) return;
            var delta = e.delta / CanvasScale();
            _content.anchoredPosition += delta;
            _velocity = delta / Mathf.Max(0.001f, Time.unscaledDeltaTime);
            ClampPan(false);
        }

        public void OnEndDrag(PointerEventData e) => _dragging = false;

        public void OnScroll(PointerEventData e)
        {
            ZoomBy(1f + e.scrollDelta.y * 0.1f, e.position);
        }

        private float CanvasScale()
        {
            var canvas = GetComponentInParent<Canvas>();
            return canvas != null ? canvas.scaleFactor : 1f;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            // Pinch (touch) zoom.
            var ts = Touchscreen.current;
            if (ts != null)
            {
                int active = 0;
                Vector2 a = default, b = default;
                foreach (var t in ts.touches)
                {
                    if (!t.press.isPressed) continue;
                    if (active == 0) a = t.position.ReadValue();
                    else if (active == 1) b = t.position.ReadValue();
                    active++;
                }
                if (active >= 2)
                {
                    float d = Vector2.Distance(a, b);
                    if (_pinchDistance > 0f) ZoomBy(d / _pinchDistance, (a + b) * 0.5f);
                    _pinchDistance = d;
                }
                else _pinchDistance = 0f;
            }

            // Inertia.
            if (!_dragging && _velocity.sqrMagnitude > 1f)
            {
                _content.anchoredPosition += _velocity * dt;
                _velocity *= Mathf.Exp(-6f * dt);
                ClampPan(false);
            }

            // Node animation: affordable pulse, selection scale, purchase punch, pip fill.
            float pulse = 1f + _theme.PulseAmplitude * Mathf.Sin(Time.unscaledTime * 4f);
            foreach (var nv in _nodeList)
            {
                float s = 1f;
                if (nv.State == NodeState.Affordable) s *= pulse;
                if (_highlight.Contains(nv.Node.Id)) s *= 1f + 0.12f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3f));
                if (nv.Node.Id == _selectedId) s *= 1.12f;
                if (nv.Punch > 0f)
                {
                    nv.Punch = Mathf.Max(0f, nv.Punch - dt * 3f);
                    s *= 1f + 0.35f * Mathf.Sin(nv.Punch * Mathf.PI);
                    nv.Ring.color = Color.Lerp(nv.State == NodeState.Maxed ? _theme.Gold : nv.BranchColor, Color.white, nv.Punch * 0.6f);
                }
                nv.Rt.localScale = Vector3.one * s;
                int level = _tree.GetLevel(nv.Node.Id);
                if (nv.Pips.Length > 0 && nv.ShownPips != level)
                {
                    nv.PipTimer += dt;
                    if (nv.PipTimer > 0.12f)
                    {
                        nv.PipTimer = 0f;
                        nv.ShownPips += nv.ShownPips < level ? 1 : -1;
                        UpdatePips(nv);
                    }
                }
            }

            // Edge flow animation.
            if (_flows.Count > 0)
            {
                _flowLines.Clear();
                for (int i = _flows.Count - 1; i >= 0; i--)
                {
                    var f = _flows[i];
                    f.T += dt * 1.6f;
                    if (f.T >= 1f) { _flows.RemoveAt(i); continue; }
                    _flows[i] = f;
                    var line = _lines.Get(f.Edge);
                    float t0 = Mathf.Clamp01(f.T - 0.15f), t1 = Mathf.Clamp01(f.T);
                    _flowLines.Add(new UILines.Line { A = Vector2.Lerp(line.A, line.B, t0), B = Vector2.Lerp(line.A, line.B, t1), Color = Color.white, Width = _theme.EdgeWidth * 1.2f });
                }
                _lines.SetFlows(_flowLines);
                if (_flows.Count == 0) { _flowLines.Clear(); _lines.SetFlows(_flowLines); }
            }
        }
    }
}
