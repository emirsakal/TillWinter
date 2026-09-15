using UnityEngine;
using UnityEngine.EventSystems;

namespace TillWinter.Unity
{
    /// <summary>Press-and-hold input for destructive actions (reset save): Held while a pointer is down on it.</summary>
    public sealed class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public bool Held { get; private set; }

        public void OnPointerDown(PointerEventData e) => Held = true;
        public void OnPointerUp(PointerEventData e) => Held = false;
        public void OnPointerExit(PointerEventData e) => Held = false;
        private void OnDisable() => Held = false;
    }
}
