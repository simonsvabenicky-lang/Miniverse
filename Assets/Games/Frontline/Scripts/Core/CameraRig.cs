using UnityEngine;

namespace Frontline
{
    /// <summary>
    /// Trails the player's X only partially (see CamFollowXScale). A camera that tracks
    /// 1:1 kills the sense of movement — you slide but the world doesn't. Holding back
    /// keeps the lane edges as a fixed reference so the dodging reads.
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        public Transform target;

        void LateUpdate()
        {
            if (target == null) return;

            float wantX = target.position.x * Tuning.CamFollowXScale;
            Vector3 pos = transform.position;
            pos.x = Mathf.Lerp(pos.x, wantX, Tuning.CamFollowLerp * Time.deltaTime);
            pos.y = Tuning.CamHeight;
            // CamAnchorZ, not the soldier's Z: the camera frames a fixed point so the soldier
            // can be pulled back within the frame without dragging the whole view with him.
            pos.z = Tuning.CamAnchorZ - Tuning.CamBack;
            transform.position = pos;
            transform.rotation = Quaternion.Euler(Tuning.CamPitch, 0f, 0f);
        }
    }
}
