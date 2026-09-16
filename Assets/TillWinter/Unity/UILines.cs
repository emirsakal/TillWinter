using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TillWinter.Unity
{
    /// <summary>
    /// All tree edges as one mesh (one draw call, no per-edge objects). Coordinates are local to this rect. An edge can
    /// bow sideways (<see cref="Line.Bend"/>, a quadratic curve) and taper from <see cref="Line.Width"/> to
    /// <see cref="Line.WidthB"/>, so branches read as grown rather than ruled.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class UILines : MaskableGraphic
    {
        public struct Line
        {
            public Vector2 A, B;
            public Color Color;
            public float Width;
            /// <summary>Width at B; 0 means the same as at A.</summary>
            public float WidthB;
            /// <summary>Sideways bow as a fraction of the length (0 = straight).</summary>
            public float Bend;
        }

        private const int CurveSegments = 12;

        private readonly List<Line> _lines = new List<Line>(128);
        private readonly List<Line> _flows = new List<Line>(16);

        public void Clear()
        {
            _lines.Clear();
            SetVerticesDirty();
        }

        public void AddCurve(Vector2 a, Vector2 b, Color color, float widthA, float widthB, float bend)
        {
            _lines.Add(new Line { A = a, B = b, Color = color, Width = widthA, WidthB = widthB, Bend = bend });
            SetVerticesDirty();
        }

        public void SetWidth(int index, float widthA, float widthB)
        {
            if (index < 0 || index >= _lines.Count) return;
            var l = _lines[index];
            if (Mathf.Approximately(l.Width, widthA) && Mathf.Approximately(l.WidthB, widthB)) return;
            l.Width = widthA;
            l.WidthB = widthB;
            _lines[index] = l;
            SetVerticesDirty();
        }

        /// <summary>A point along edge <paramref name="index"/> (0 = A, 1 = B), following its curve.</summary>
        public Vector2 PointAt(int index, float t) => Point(_lines[index], t);

        private static Vector2 Point(Line l, float t)
        {
            if (l.Bend == 0f) return Vector2.LerpUnclamped(l.A, l.B, t);
            var d = l.B - l.A;
            var ctrl = (l.A + l.B) * 0.5f + new Vector2(-d.y, d.x) * l.Bend;
            float u = 1f - t;
            return u * u * l.A + 2f * u * t * ctrl + t * t * l.B;
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
            float wb = l.WidthB > 0f ? l.WidthB : l.Width;
            if (l.Bend != 0f || !Mathf.Approximately(wb, l.Width))
            {
                Strip(vh, l, wb);
                return;
            }
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

        /// <summary>One continuous strip along the curve, so bends have no gaps at the joints.</summary>
        private static void Strip(VertexHelper vh, Line l, float widthB)
        {
            int start = vh.currentVertCount;
            for (int k = 0; k <= CurveSegments; k++)
            {
                float t = (float)k / CurveSegments;
                var p = Point(l, t);
                var tangent = Point(l, Mathf.Min(1f, t + 0.02f)) - Point(l, Mathf.Max(0f, t - 0.02f));
                var dir = tangent.sqrMagnitude > 1e-6f ? tangent.normalized : Vector2.right;
                var n = new Vector2(-dir.y, dir.x) * (Mathf.Lerp(l.Width, widthB, t) * 0.5f);
                vh.AddVert(p - n, l.Color, Vector2.zero);
                vh.AddVert(p + n, l.Color, Vector2.zero);
                if (k == 0) continue;
                int i = start + (k - 1) * 2;
                vh.AddTriangle(i, i + 1, i + 3);
                vh.AddTriangle(i, i + 3, i + 2);
            }
        }
    }
}
