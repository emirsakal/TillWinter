using UnityEngine;
using UnityEngine.UI;

namespace TillWinter.Unity
{
    /// <summary>
    /// Invisible raycast target (no mesh, no overdraw). Put on HUD areas so a finger resting on them never becomes
    /// ring input: PointerInput ignores presses that start over UI.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RaycastBlocker : Graphic
    {
        protected override void OnPopulateMesh(VertexHelper vh) => vh.Clear();
    }
}
