using UnityEngine;
using UnityEngine.InputSystem;

namespace FlowSort.Gameplay
{
    /// <summary>
    /// Routes a tap on any world-space collider carrying an ITappable to that component. Uses
    /// the new Input System's device-agnostic Pointer so the same code handles mouse (editor)
    /// and touch (device).
    /// </summary>
    public class TapInputRouter : MonoBehaviour
    {
        void Update()
        {
            var pointer = Pointer.current;
            if (pointer == null || !pointer.press.wasPressedThisFrame) return;

            Vector2 screenPos = pointer.position.ReadValue();
            Vector3 worldPos = Camera.main.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 0f));
            worldPos.z = 0f;

            var hit = Physics2D.OverlapPoint(worldPos);
            if (hit != null && hit.TryGetComponent<ITappable>(out var tappable))
                tappable.OnTapped();
        }
    }
}
