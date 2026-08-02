using System;
using TMPro;
using UnityEngine;

namespace FlowSort.Blocks
{
    /// <summary>
    /// A colour-matched tower. Sits in a slot until deployed, then rides the conveyor firing
    /// STRAIGHT inward — the belt turning it 90 degrees at each corner is the only aiming there
    /// is, so there is no sweep and no aim logic here.
    ///
    /// Its shots only destroy blocks of its own colour; a shot that meets any other colour dies
    /// on impact. Ammo spent on mismatches is simply lost, which is where the challenge lives.
    ///
    /// If it completes a full lap with ammo left it goes back to its slot to be sent again. If it
    /// runs dry it is consumed, and losing every tower with blocks still standing is the fail
    /// condition.
    /// </summary>
    public class ConveyorTower : MonoBehaviour
    {
        /// <summary>
        /// Launching and Returning are the two short tweens either side of a lap. They exist so
        /// a tower visibly travels between its slot and the belt rather than teleporting, which
        /// is most of the difference between the board feeling built and feeling assembled.
        /// Neither is tappable, so they cannot be used to dodge the belt cap.
        /// </summary>
        public enum State { InSlot, Launching, Riding, Returning, Spent }

        const float LaunchTime = 0.22f;
        const float ReturnTime = 0.3f;

        public event Action<ConveyorTower> OnReturnedToSlot;
        public event Action<ConveyorTower> OnSpent;

        public State Current { get; private set; } = State.InSlot;
        public byte ColorIndex { get; private set; }
        public int Ammo { get; private set; }
        public int SlotIndex { get; set; }

        public BlockWall Wall { set => wall = value; }
        public ImpactFX Fx;

        BallSystem balls;
        BlockWall wall;
        Transform model;
        TMP_Text ammoLabel;
        public TMP_Text AmmoLabel => ammoLabel;

        float distance;
        float travelled;
        float fireTimer;
        float sinceShot;
        float recoilOffset;
        float recoilVelocity;
        float popTimer;

        Vector3 slotPosition;

        public void Init(byte colorIndex, int ammo, BallSystem ballSystem,
                         Transform modelRoot, TMP_Text label, Vector3 restPosition)
        {
            ColorIndex = colorIndex;
            Ammo = ammo;
            balls = ballSystem;
            model = modelRoot;
            ammoLabel = label;
            slotPosition = restPosition;

            transform.position = restPosition;
            if (model != null) model.localRotation = Quaternion.identity;

            // Ink flips on light towers. White text on a white tower was unreadable however heavy
            // the outline, and the white tower is the one whose count you most need to plan with.
            Color tint = BlockPalette.Get(colorIndex);
            bool light = 0.299f * tint.r + 0.587f * tint.g + 0.114f * tint.b > 0.6f;
            labelInk = light ? new Color(0.13f, 0.04f, 0.25f) : Color.white;

            if (ammoLabel != null)
            {
                ammoLabel.color = labelInk;
                ammoLabel.outlineColor = light ? (Color32)Color.white : new Color32(0x20, 0x0A, 0x40, 0xFF);
            }

            RefreshLabel();
        }

        Color labelInk = Color.white;

        public void SetRestPosition(Vector3 p)
        {
            slotPosition = p;
            // Only snap when parked. During the return tween this is the destination being set by
            // the slot assignment, and snapping to it would skip the whole animation.
            if (Current == State.InSlot) transform.position = p;
        }

        /// <summary>
        /// Queued towers behind the front of a line are shown but not sendable, so they are
        /// darkened and shrunk — you can read their colour and count to plan, without them
        /// looking tappable.
        /// </summary>
        public void SetDimmed(bool dim)
        {
            if (model == null) return;

            restScale = dim ? 0.78f : 1f;
            model.localScale = Vector3.one * restScale;

            var block = new MaterialPropertyBlock();
            Color tint = BlockPalette.Get(ColorIndex);
            // Only enough to read as "not yours yet". At 0.45 queued towers went black-brown,
            // which both hid their colour — the whole point of showing the queue — and dragged
            // the scene back toward the dark palette the art direction rules out.
            if (dim) tint *= 0.72f;
            tint.a = 1f;

            foreach (var r in model.GetComponentsInChildren<MeshRenderer>())
            {
                r.GetPropertyBlock(block);
                block.SetColor(BaseColorId, tint);
                r.SetPropertyBlock(block);
            }

            if (ammoLabel != null)
            {
                ammoLabel.transform.localScale = Vector3.one * (dim ? 0.82f : 1f);
                ammoLabel.color = dim ? labelInk * 0.85f : labelInk;
            }
        }

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public void Deploy()
        {
            if (Current != State.InSlot || Ammo <= 0) return;

            distance = ConveyorPath.EntryDistanceForX(slotPosition.x);
            travelled = 0f;
            fireTimer = 0f;
            sinceShot = float.MaxValue; // free to fire the instant it meets a match
            rejectTimer = 0f;

            tweenFrom = transform.position;
            tweenTo = ConveyorPath.PositionAt(distance);
            tweenTime = 0f;
            Current = State.Launching;
        }

        Vector3 tweenFrom;
        Vector3 tweenTo;
        float tweenTime;
        float restScale = 1f;
        float punch;

        /// <summary>Decelerating ease — fast off the mark, settling into the belt.</summary>
        static float EaseOut(float t) => 1f - (1f - t) * (1f - t);

        /// <summary>Overshoots slightly before settling, so arriving in a slot lands with weight.</summary>
        static float EaseOutBack(float t)
        {
            float u = t - 1f;
            return 1f + u * u * (2.2f * u + 1.2f);
        }

        void UpdateTween(float dt)
        {
            bool launching = Current == State.Launching;
            float duration = launching ? LaunchTime : ReturnTime;

            tweenTime += dt;
            float t = Mathf.Clamp01(tweenTime / duration);

            // The return destination can move under us: the slot is only assigned once the lap
            // completes, and HandleReturned sets it while this tween is already running.
            if (!launching) tweenTo = slotPosition;

            transform.position = Vector3.LerpUnclamped(
                tweenFrom, tweenTo, launching ? EaseOut(t) : EaseOutBack(t));

            if (t < 1f) return;

            if (launching)
            {
                Current = State.Riding;
                return;
            }

            Current = State.InSlot;
            transform.position = slotPosition;
            punch = 0.28f;
        }

        /// <summary>Squash-and-settle applied on top of whatever rest scale dimming chose.</summary>
        void UpdatePunch(float dt)
        {
            if (punch <= 0f || model == null) return;

            punch = Mathf.Max(0f, punch - dt * 2.6f);
            float wobble = Mathf.Sin(punch * 26f) * punch * 0.5f;
            model.localScale = Vector3.one * (restScale * (1f + wobble));
        }

        void Update()
        {
            float dt = Time.deltaTime;

            if (Current == State.Spent)
            {
                popTimer -= dt;
                float t = Mathf.Clamp01(popTimer / BlockTuning.TurretPopTime);
                if (model != null) model.localScale = Vector3.one * (t * t);
                if (popTimer <= 0f)
                {
                    OnSpent?.Invoke(this);
                    Destroy(gameObject);
                }
                return;
            }

            UpdatePunch(dt);
            UpdateRetire(dt);

            if (Current == State.InSlot)
            {
                UpdateReject(dt);
                return;
            }

            if (Current == State.Launching || Current == State.Returning)
            {
                UpdateTween(dt);
                return;
            }

            if (Current != State.Riding) return;

            float step = BlockTuning.ConveyorSpeed * dt;
            distance += step;
            travelled += step;

            // No firing through a corner: the heading there is diagonal and the grid ray only
            // takes axis steps. The tower just turns and picks up again on the next straight.
            bool aimed = ConveyorPath.TryFireDirection(distance, out Vector2 fireDir);

            UpdateRecoil(dt, fireDir);

            transform.position = ConveyorPath.PositionAt(distance)
                                 - new Vector3(fireDir.x, fireDir.y, 0f) * recoilOffset;

            // RotationAt keeps the tower's rest-pose facing (gun toward the wall) pointed the
            // right way around every corner, but the rest pose itself is calibrated to the belt's
            // straight-ahead heading — so riding it as-is aims the gun down the track rather than
            // in at the board. The quarter turn corrects for that offset once, everywhere.
            if (model != null)
                model.localRotation = Quaternion.Euler(0f, 0f, ConveyorPath.RotationAt(distance) + 90f);

            // Fire only when the first block straight ahead is our colour AND nothing is already
            // in the air toward it. Anything else and we hold fire entirely — ammo is never spent
            // on a shot that cannot connect.
            //
            // The travel gate is what keeps a tower from emptying itself: one shot per tile means
            // it takes what the exposed face of the picture offers on this pass and no more, then
            // carries the rest back to a landing square to wait for another colour to open a way
            // through.
            fireTimer += dt;
            sinceShot += step;

            bool ready = fireTimer >= BlockTuning.FireInterval
                         && sinceShot >= BlockTuning.TilesBetweenShots * BlockTuning.TileSize;

            if (ready && aimed && Ammo > 0 && wall != null)
            {
                if (wall.FirstBlockAlong(transform.position, fireDir,
                                         out int tx, out int ty, out byte c, out bool claimed)
                    && !claimed && c == ColorIndex)
                {
                    fireTimer = 0f;
                    sinceShot = 0f;
                    Fire(fireDir, tx, ty);
                }
                else
                {
                    fireTimer = BlockTuning.FireInterval; // stay primed, re-check next frame
                }
            }

            if (Ammo <= 0)
            {
                BeginSpend();
                return;
            }

            // Survived a full lap with ammo to spare — go home and wait to be sent again.
            if (travelled >= ConveyorPath.Perimeter) ReturnToSlot();
        }

        void Fire(Vector2 dir, int targetX, int targetY)
        {
            wall.Reserve(targetX, targetY);

            Vector3 origin = transform.position + new Vector3(dir.x, dir.y, 0f) * 1.6f;
            balls.Spawn(origin, dir * BlockTuning.BallSpeed, ColorIndex, targetX, targetY);

            Ammo--;
            RefreshLabel();

            recoilOffset = BlockTuning.RecoilKick;
            recoilVelocity = 0f;

            Fx?.MuzzleFlash(origin, BlockPalette.Get(ColorIndex));
        }

        /// <summary>
        /// The belt was full when this tower was tapped. It shakes in place instead of launching,
        /// so a refused tap still reads as "heard you, not now" rather than as a dead control.
        /// </summary>
        public void RejectSend()
        {
            if (Current == State.InSlot) rejectTimer = RejectTime;
        }

        const float RejectTime = 0.28f;
        float rejectTimer;

        void UpdateReject(float dt)
        {
            if (rejectTimer <= 0f) return;

            rejectTimer -= dt;
            float t = Mathf.Max(0f, rejectTimer) / RejectTime;
            float offset = Mathf.Sin(rejectTimer * 70f) * 0.45f * t;

            transform.position = slotPosition + new Vector3(offset, 0f, 0f);
        }

        void UpdateRecoil(float dt, Vector2 dir)
        {
            recoilVelocity += -(BlockTuning.RecoilSpring * recoilOffset
                                + BlockTuning.RecoilDamping * recoilVelocity) * dt;
            recoilOffset += recoilVelocity * dt;
        }

        /// <summary>
        /// Lap finished with ammo to spare. The slot is claimed FIRST — the handler assigns one
        /// and calls SetRestPosition — and only then does the tower fly to it, so the tween has a
        /// destination to aim at instead of arriving somewhere and being teleported afterwards.
        /// </summary>
        void ReturnToSlot()
        {
            recoilOffset = 0f;
            recoilVelocity = 0f;
            if (model != null) model.localRotation = Quaternion.identity;

            tweenFrom = transform.position;
            tweenTo = slotPosition;
            tweenTime = 0f;
            Current = State.Returning;

            OnReturnedToSlot?.Invoke(this);
        }

        /// <summary>
        /// Retire a tower with ammo left because there is nothing left of its colour to shoot.
        /// Otherwise it would sit in a landing square forever, and enough of those make a level
        /// structurally unwinnable — see the balance simulation.
        /// </summary>
        public void Retire() => BeginSpend();

        /// <summary>
        /// Retire after a delay, so a whole board's worth of towers leaves as a ripple instead of
        /// all at once. Detaches its events first: it is on its way out and must not claim a
        /// landing square or count against the belt on the way.
        /// </summary>
        public void RetireAfter(float delay)
        {
            OnReturnedToSlot = null;
            OnSpent = null;
            retireIn = delay;
        }

        float retireIn = -1f;

        void UpdateRetire(float dt)
        {
            if (retireIn < 0f) return;

            retireIn -= dt;
            if (retireIn > 0f) return;

            retireIn = -1f;
            if (Current != State.Spent) BeginSpend();
        }

        void BeginSpend()
        {
            Current = State.Spent;
            popTimer = BlockTuning.TurretPopTime;
            if (ammoLabel != null) ammoLabel.gameObject.SetActive(false);
        }

        void RefreshLabel()
        {
            if (ammoLabel != null) ammoLabel.text = Ammo.ToString();
        }
    }
}
