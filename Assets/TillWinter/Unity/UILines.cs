using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TillWinter.Unity
{
    /// <summary>All tree edges as one mesh (one draw call, no per-edge objects). Coordinates are local to this rect.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class UILines : MaskableGraphic
    {
        public struct Line
        {
            public Vector2 A, B;
            public Color Color;
            public float Width;
        }

        private readonly List<Line> _lines = new List<Line>(128);
        private readonly List<Line> _flows = new List<Line>(16);

        public void Clear()
        {
            _lines.Clear();
            SetVerticesDirty();
        }

        public void Add(Vector2 a, Vector2 b, Color color, float width)
        {
            _lines.Add(new Line { A = a, B = b, Color = color, Width = width });
            SetVerticesDirty();
        }

        public void SetColor(int index, Color color)
        {
            if (index < 0 || index >= _lines.Count) return;
            var l = _lines[index];
            if (l.Color == color) return;
            l.Color = color;
            _lines[index] = l;
            SetVerticesDirty();
        }

        public int Count => _lines.Count;
        public Line Get(int index) => _lines[index];

        /// <summary>Short bright segments drawn on top (edge "flow" animation); replaced every frame while any flow runs.</summary>
        public void SetFlows(List<Line> flows)
        {
            _flows.Clear();
            _flows.AddRange(flows);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            for (int i = 0; i < _lines.Count; i++) Quad(vh, _lines[i]);
            for (int i = 0; i < _flows.Count; i++) Quad(vh, _flows[i]);
        }

        private static void Quad(VertexHelper vh, Line l)
        {
            var dir = (l.B - l.A).normalized;
            var n = new Vector2(-dir.y, dir.x) * (l.Width * 0.5f);
            int i = vh.currentVertCount;
            vh.AddVert(l.A - n, l.Color, Vector2.zero);
            vh.AddVert(l.A + n, l.Color, Vector2.zero);
            vh.AddVert(l.B + n, l.Color, Vector2.zero);
            vh.AddVert(l.B - n, l.Color, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i, i + 2, i + 3);
        }
    }
}
