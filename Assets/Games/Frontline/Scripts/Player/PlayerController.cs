using UnityEngine;

namespace Frontline
{
    /// <summary>
    /// Steers the soldier along X only. Uses relative drag (finger delta from where you
    /// pressed) rather than absolute position, so the soldier never teleports under your
    /// thumb when you first touch — that jump is the single worst feel bug in this genre.
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        float _targetX;
        bool _dragging;
        float _pointerStartX;
        float _playerStartX;
        float _moveSpeedMult = 1f;

        void Awake()
        {
            Vector3 p = transform.position;
            p.z = Tuning.PlayerZ;
            transform.position = p;
            _targetX = p.x;

            // The hero's trade-off cost, applied for real -- see HeroDef.MoveSpeedMult.
            _moveSpeedMult = Heroes.Equipped().MoveSpeedMult;
        }

        /// <summary>
        /// Steer from somewhere other than a finger. ReadDrag only writes _targetX while
        /// the pointer is down, so with no input this is uncontested — which is what lets
        /// AutoPilot drive without fighting the player.
        /// </summary>
        public void SetTargetX(float x)
        {
            _targetX = Mathf.Clamp(x, -Tuning.LaneHalfWidth, Tuning.LaneHalfWidth);
        }

        void Update()
        {
            ReadDrag();

            Vector3 pos = transform.position;
            pos.x = Mathf.MoveTowards(pos.x, _targetX, Tuning.PlayerMoveSpeed * _moveSpeedMult * Time.deltaTime);
            pos.z = Tuning.PlayerZ;
            transform.position = pos;
        }

        void ReadDrag()
        {
            if (!InputReader.IsPressed)
            {
                _dragging = false;
                return;
            }

            float pointerX = InputReader.Position.x;

            if (!_dragging)
            {
                _dragging = true;
                _pointerStartX = pointerX;
                _playerStartX = transform.position.x;
            }

            float delta = (pointerX - _pointerStartX) * DragScale;
            _targetX = Mathf.Clamp(_playerStartX + delta,
                                   -Tuning.LaneHalfWidth, Tuning.LaneHalfWidth);
        }

        /// <summary>
        /// Screen pixels -> world units, derived from the actual screen rather than baked in.
        ///
        /// Screen.width is a Unity property read every drag frame on purpose: it costs nothing
        /// and it means rotation, resolution and any device all resolve correctly without a
        /// cached value going stale. Deriving it is the whole point -- a hardcoded px->world
        /// constant is only ever right on the screen it was tuned against.
        /// </summary>
        static float DragScale =>
            (Tuning.LaneHalfWidth * 2f) / (Screen.width * Tuning.LaneCrossDragFraction);
    }
}
