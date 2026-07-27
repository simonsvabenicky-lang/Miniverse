using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Frontline
{
    /// <summary>
    /// Pointer state, normalised across mouse and touch, and across both of Unity's
    /// input backends. The URP template can be created with either the legacy Input
    /// Manager or the new Input System, so we compile against whichever is active
    /// rather than betting on one.
    /// </summary>
    public static class InputReader
    {
        public static bool IsPressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
                    return true;
                return Mouse.current != null && Mouse.current.leftButton.isPressed;
#else
                if (Input.touchCount > 0)
                    return Input.GetTouch(0).phase != TouchPhase.Ended
                        && Input.GetTouch(0).phase != TouchPhase.Canceled;
                return Input.GetMouseButton(0);
#endif
            }
        }

        public static Vector2 Position
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
                    return Touchscreen.current.primaryTouch.position.ReadValue();
                return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
                if (Input.touchCount > 0) return Input.GetTouch(0).position;
                return Input.mousePosition;
#endif
            }
        }
    }
}
