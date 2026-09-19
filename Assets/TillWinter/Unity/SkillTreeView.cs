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
            /// <summary>The Almanac's suggestion (GDD §6.3 v2.2): a small star badge.</summary>
            public Image Badge;
            public TMP_Text LevelText;
            public Image Icon;
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
        /// <summary>Level ring and purchase wave per node (NodeView stays as it was).</summary>
        private readonly Dictionary<string, Image> _fills = new Dictionary<string, Image>();
        private readonly Dictionary<string, Image> _waves = new Dictionary<string, Image>();
        private readonly Dictionary<string, float> _waveT = new Dictionary<string, float>();
        private readonly Dictionary<string, float> _fillShown = new Dictionary<string, float>();
        private readonly Dictionary<string, float> _fillTarget = new Dictionary<string, float>();
        /// <summary>Root edges from the hub, by line index, with the root node they feed.</summary>
        private readonly List<(int line, string node)> _rootEdges = new List<(int, string)>();
        private bool _detail = true;
        private float _introT = 1f;
        private float _introTarget;

        public SkillTree Tree => _tree;

        private string _suggested;

        /// <summary>Marks one node as the suggestion (null clears it).</summary>
        public void SetSuggested(string id)
        {
            if (id == _suggested) return;
            _suggested = id;
            for (int i = 0; i < _nodeList.Count; i++)
            {
                var badge = _nodeList[i].Badge;
                if (badge != null) badge.gameObject.SetActive(_nodeList[i].Node.Id == id);
            }
        }
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

            BuildBranchRegions();
            BuildNodes();
            BuildEdges();
            ComputeBounds();
            Refresh(true);
            CenterOnRoots();
        }

        // ------------------------------------------------------------------ build

        private Vector2 ToPixels(LayoutPos p) => new Vector2(p.X, p.Y) * _theme.UnitPixels;

        /// <summary>
        /// The five branches leave one centre like a star, so each is marked by a name plate past its outermost node
        /// rather than a box behind it: boxes drawn around rays overlap in the middle, where every branch begins.
        /// </summary>
        private void BuildBranchRegions()
        {
            var farthest = new Dictionary<TillWinter.Core.Branch, Vector2>();
            foreach (var node in _tree.Nodes)
            {
                if (!_layout.TryGetValue(node.Id, out var p)) continue;
                var v = ToPixels(p);
                if (!farthest.TryGetValue(node.Branch, out var best) || v.sqrMagnitude > best.sqrMagnitude) farthest[node.Branch] = v;
            }
            // The hub is the farm itself: a trunk ring with the farmhouse at its heart, the roots of every branch leaving it.
            float hubSize = _theme.NodeSize * 1.7f;
            var halo = UiKit.CircleImage(_content, "HubHalo", new Color(_theme.Trunk.r, _theme.Trunk.g, _theme.Trunk.b, 0.18f), Vector2.zero, hubSize * 1.45f);
            halo.raycastTarget = false;
            var hub = UiKit.CircleImage(_content, "Hub", _theme.Trunk, Vector2.zero, hubSize);
            hub.raycastTarget = false;
            var growth = UiKit.CircleImage(hub.transform, "Ring", TreeTheme.Desaturate(_theme.Trunk, 0.6f) * 0.8f, Vector2.zero, hubSize * 0.84f);
            growth.raycastTarget = false;
            var heart = UiKit.CircleImage(hub.transform, "Heart", _theme.Paper, Vector2.zero, hubSize * 0.66f);
            heart.raycastTarget = false;
            var home = NodeIcons.Image(heart.transform, "home", _theme.Accent);
            UiKit.Box(home.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * (hubSize * 0.36f));
            foreach (var kv in farthest)
            {
                var branch = kv.Key;
                var colour = _theme.BranchColor(branch);
                var dir = kv.Value.sqrMagnitude > 0.01f ? kv.Value.normalized : Vector2.up;
                var plate = UiKit.Panel(_content, "Branch " + branch, new Color(colour.r, colour.g, colour.b, 0.85f), true, false);
                var plateRt = plate.rectTransform;
                plateRt.anchorMin = plateRt.anchorMax = new Vector2(0.5f, 0.5f);
                plateRt.pivot = new Vector2(0.5f, 0.5f);
                plateRt.sizeDelta = new Vector2(PlateWidth, PlateHeight);
                // Far enough out that the plate's own half-size clears the node, whichever way the branch points.
                plateRt.anchoredPosition = kv.Value + dir * (_theme.NodeSize * 0.6f + Mathf.Abs(dir.x) * PlateWidth * 0.5f + Mathf.Abs(dir.y) * PlateHeight * 0.55f);
                var icon = NodeIcons.Image(plate.transform, BranchIcon(branch), _theme.Paper);
                UiKit.Box(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(22f, 0f), Vector2.one * 48f);
                var name = UiKit.Label(plate.transform, "Name", Strings.Branch(branch), UiType.Heading, _theme.Paper, TextAnchor.MiddleCenter, FontStyle.Bold);
                UiKit.Stretch(name.rectTransform, Vector2.zero, Vector2.one, new Vector2(80f, 0f), new Vector2(-20f, 0f));
            }
        }

        private const float PlateWidth = 380f;
        private const float PlateHeight = 80f;

        private static string BranchIcon(TillWinter.Core.Branch b)
        {
            switch (b)
            {
                case TillWinter.Core.Branch.Hand: return "target";
                case TillWinter.Core.Branch.Soil: return "contrast";
                case TillWinter.Core.Branch.Field: return "menuGrid";
                case TillWinter.Core.Branch.Helpers: return "multiplayer";
                default: return "scrollHorizontal";
            }
        }

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
                nv.Icon = NodeIcons.Image(nv.Rt, node.IconKey, _theme.Ink);
                UiKit.Box(nv.Icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, size * 0.04f), Vector2.one * (size * 0.42f));
                nv.LevelText = UiKit.Label(nv.Rt, "Level", "", Mathf.RoundToInt(size * 0.16f), _theme.InkMuted, TextAnchor.LowerCenter);
                UiKit.Stretch(nv.LevelText.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 18f), new Vector2(0f, 0f));

                // Level reads as a ring filling clockwise around the node instead of a circle of pips.
                nv.Pips = new Image[0];
                var track = UiKit.CircleImage(nv.Rt, "LevelTrack", _theme.EdgeDim, Vector2.zero, size * 1.18f);
                track.transform.SetAsFirstSibling();
                var fill = UiKit.CircleImage(nv.Rt, "LevelFill", nv.BranchColor, Vector2.zero, size * 1.18f);
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Radial360;
                fill.fillOrigin = (int)Image.Origin360.Top;
                fill.fillClockwise = true;
                fill.fillAmount = 0f;
                fill.transform.SetSiblingIndex(1);
                _fills[node.Id] = fill;
                _fillShown[node.Id] = 0f;
                var wave = UiKit.CircleImage(nv.Rt, "Wave", nv.BranchColor, Vector2.zero, size * 1.3f);
                wave.raycastTarget = false;
                wave.gameObject.SetActive(false);
                _waves[node.Id] = wave;
                nv.Lock = UiKit.CircleImage(nv.Rt, "Lock", _theme.InkMuted, new Vector2(size * 0.32f, -size * 0.32f), size * 0.3f);
                var padlock = NodeIcons.Image(nv.Lock.transform, "locked", _theme.Paper); // a padlock, not a cross
                UiKit.Box(padlock.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * (size * 0.19f));

                nv.Badge = UiKit.CircleImage(nv.Rt, "Suggested", _theme.Accent, new Vector2(-size * 0.36f, size * 0.36f), size * 0.34f);
                nv.Badge.raycastTarget = false;
                var badgeStar = NodeIcons.Image(nv.Badge.transform, "star", _theme.Paper);
                UiKit.Box(badgeStar.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * (size * 0.22f));
                nv.Badge.gameObject.SetActive(false);

                _nodes[node.Id] = nv;
                _nodeList.Add(nv);
            }
        }

        private void BuildEdges()
        {
            _lines.Clear();
            _edgeIds.Clear();
            _rootEdges.Clear();
            // Roots first, so they sit under the branches: each root node grows out of the hub on a thick curve.
            foreach (var node in _tree.Nodes)
            {
                if (!_layout.ContainsKey(node.Id) || HasLaidOutParent(node)) continue;
                var to = ToPixels(_layout[node.Id]);
                _rootEdges.Add((_lines.Count, node.Id));
                _lines.AddCurve(Vector2.zero, to, _theme.Trunk, RootWidth, RootWidth * 0.55f, BendFor("hub", node.Id));
            }
            foreach (var node in _tree.Nodes)
            {
                var list = new List<int>();
                foreach (var p in node.Prerequisites)
                {
                    if (!_layout.ContainsKey(p)) continue;
                    _edgeIds.Add((p, node.Id));
                    list.Add(_lines.Count);
                    _lines.AddCurve(ToPixels(_layout[p]), ToPixels(_layout[node.Id]), _theme.EdgeDim, _theme.EdgeWidth, _theme.EdgeWidth * 0.8f, BendFor(p, node.Id));
                }
                _nodes[node.Id].Edges = list.ToArray();
            }
        }

        private float RootWidth => _theme.EdgeWidth * 2.6f;

        private bool HasLaidOutParent(SkillNode node)
        {
            foreach (var p in node.Prerequisites) if (_layout.ContainsKey(p)) return true;
            return false;
        }

        /// <summary>A small, stable sideways bow per edge, so siblings do not all curve the same way.</summary>
        private static float BendFor(string from, string to)
        {
            int h = 17;
            foreach (char c in from) h = h * 31 + c;
            foreach (char c in to) h = h * 31 + c;
            if (h < 0) h = -h;
            return ((h % 2) == 0 ? 1f : -1f) * (0.08f + (h / 2 % 5) * 0.012f);
        }

        private void ComputeBounds()
        {
            var b = SkillTreeLayout.Bounds(_layout);
            // Room for the branch name plates (240 x 52) past the outermost nodes. Measured to the plate's far edge,
            // not its centre: a plate on a level branch reaches about 0.6 node + 120 out plus its own half-width.
            var pad = new Vector2(_theme.NodeSize * 0.6f + PlateWidth + 16f, _theme.NodeSize * 0.6f + PlateHeight * 1.05f + 90f);
            _contentMin = new Vector2(b.minX, b.minY) * _theme.UnitPixels - pad;
            _contentMax = new Vector2(b.maxX, b.maxY) * _theme.UnitPixels + pad;
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
                nv.Icon.color = available ? _theme.Ink : _theme.InkMuted;
                nv.Lock.gameObject.SetActive(!available && _detail);
                int max = _game.Sim.GetMaxLevel(nv.Node.Id);
                if (_fills.TryGetValue(nv.Node.Id, out var fillImg))
                {
                    float ratio = max > 0 ? Mathf.Clamp01((float)level / max) : level > 0 ? 1f : 0f;
                    if (state == NodeState.Maxed) ratio = 1f;
                    fillImg.color = state == NodeState.Maxed ? _theme.Gold : available ? nv.BranchColor : TreeTheme.Desaturate(nv.BranchColor, _theme.LockedSaturation);
                    if (silent) { _fillShown[nv.Node.Id] = ratio; fillImg.fillAmount = ratio; }
                    _fillTarget[nv.Node.Id] = ratio;
                }
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
                int line = i + _rootEdges.Count;
                _lines.SetColor(line, lit ? _nodes[to].BranchColor : _theme.EdgeDim);
                // A bought path thickens, so the tree visibly grows with the farm.
                if (lit) _lines.SetWidth(line, _theme.EdgeWidth * 2f, _theme.EdgeWidth * 1.3f);
                else _lines.SetWidth(line, _theme.EdgeWidth, _theme.EdgeWidth * 0.8f);
            }
            foreach (var (line, node) in _rootEdges)
            {
                bool grown = _tree.GetLevel(node) > 0;
                _lines.SetColor(line, grown ? _nodes[node].BranchColor : _theme.Trunk);
                _lines.SetWidth(line, grown ? RootWidth * 1.25f : RootWidth, grown ? RootWidth * 0.8f : RootWidth * 0.55f);
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
            if (_waves.TryGetValue(id, out var wave)) { wave.gameObject.SetActive(true); _waveT[id] = 0f; }
            Refresh(false);
        }

        // ------------------------------------------------------------------ camera

        /// <summary>
        /// The opening view. The whole canopy, name plates included, is
        /// fitted and centred. (It used to aim at the roots of side-by-side lanes, which put the radial
        /// tree off-centre with branches cut at both edges.) Before the viewport has a size, the theme's zoom is used.
        /// </summary>
        public void CenterOnRoots()
        {
            // The canopy is lopsided (branches differ in depth), so it is the tree's bounds that get centred and
            // fitted, not the hub: centring the hub left one side short of room and cut the longest branch off.
            var size = _viewport.rect.size;
            var extent = _contentMax - _contentMin;
            float zoom = size.x > 1f && size.y > 1f
                ? Mathf.Min(size.x / Mathf.Max(1f, extent.x), size.y / Mathf.Max(1f, extent.y))
                : _theme.InitialZoom;
            _zoom = Mathf.Clamp(zoom, _theme.ZoomMin, _theme.ZoomMax);
            _content.localScale = Vector3.one * _zoom;
            _content.anchoredPosition = -(_contentMin + _contentMax) * 0.5f * _zoom;
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
            else
            {
                CenterOnRoots();
                // First open: settle in from a little further out.
                if (SettingsStore.MotionAllowed)
                {
                    _introTarget = _zoom;
                    ZoomBy(0.86f);
                    _introT = 0f;
                }
            }
            _velocity = Vector2.zero;
            StartCoroutine(OpenFadeRoutine());
        }

        /// <summary>A short fade as the tree appears: an instant cut from the farm to a full canvas of nodes jarred.</summary>
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
            var target = -(sum / n) * _zoom;
            // While the whole canopy fits, the pan stays inside the range that keeps all of it on screen: the
            // onboarding pulse already says which nodes matter, and centring them pushed whole branches off the edge.
            var half = _viewport.rect.size * 0.5f;
            var lo = -half - _contentMin * _zoom;
            var hi = half - _contentMax * _zoom;
            if (lo.x <= hi.x) target.x = Mathf.Clamp(target.x, lo.x, hi.x);
            if (lo.y <= hi.y) target.y = Mathf.Clamp(target.y, lo.y, hi.y);
            _content.anchoredPosition = target;
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

            if (_introT < 1f)
            {
                if (_dragging || _pointers > 0) _introT = 1f;
                else
                {
                    _introT = Mathf.Min(1f, _introT + dt / 0.7f);
                    float want = Mathf.Lerp(_introTarget * 0.86f, _introTarget, UiMotion.EaseInOut(_introT));
                    if (_zoom > 0f) ZoomBy(want / _zoom);
                }
            }

            // Far out a node is just its colour; levels and padlocks appear as the player zooms in.
            bool detail = _zoom >= _theme.DetailZoom;
            if (detail != _detail)
            {
                _detail = detail;
                foreach (var nv in _nodeList)
                {
                    nv.LevelText.gameObject.SetActive(detail);
                    nv.Lock.gameObject.SetActive(detail && nv.State == NodeState.Locked);
                }
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
                    nv.Ring.color = Color.Lerp(nv.State == NodeState.Maxed ? _theme.Gold : nv.BranchColor, _theme.Highlight, nv.Punch * 0.6f);
                }
                nv.Rt.localScale = Vector3.one * s;
                // The level ring eases toward its target; a bought node sends a wave outward.
                if (_fills.TryGetValue(nv.Node.Id, out var fillImg) && _fillTarget.TryGetValue(nv.Node.Id, out float target))
                {
                    float shown = _fillShown[nv.Node.Id];
                    if (!Mathf.Approximately(shown, target))
                    {
                        shown = Mathf.MoveTowards(shown, target, dt / UiMotion.Normal);
                        _fillShown[nv.Node.Id] = shown;
                        fillImg.fillAmount = shown;
                    }
                }
                if (_waveT.TryGetValue(nv.Node.Id, out float wt) && wt < 1f && _waves.TryGetValue(nv.Node.Id, out var waveImg))
                {
                    wt = Mathf.Min(1f, wt + dt / UiMotion.Slow);
                    _waveT[nv.Node.Id] = wt;
                    float e = UiMotion.EaseOut(wt);
                    waveImg.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.8f, 2.1f, e);
                    var wc = nv.BranchColor;
                    wc.a = 0.5f * (1f - e);
                    waveImg.color = wc;
                    if (wt >= 1f) waveImg.gameObject.SetActive(false);
                }
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
                    int edge = f.Edge + _rootEdges.Count;
                    float t0 = Mathf.Clamp01(f.T - 0.15f), t1 = Mathf.Clamp01(f.T);
                    _flowLines.Add(new UILines.Line { A = _lines.PointAt(edge, t0), B = _lines.PointAt(edge, t1), Color = _theme.Highlight, Width = _theme.EdgeWidth * 1.2f });
                }
                _lines.SetFlows(_flowLines);
                if (_flows.Count == 0) { _flowLines.Clear(); _lines.SetFlows(_flowLines); }
            }
        }
    }
}
