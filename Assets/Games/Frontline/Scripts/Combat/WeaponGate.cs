using UnityEngine;

namespace Frontline
{
    /// <summary>
    /// One lane in a gate row: a panel with a NUMBER you shoot through to claim its gun.
    ///
    /// Shoot-through, not drive-through. You steer under the gate you want and your fire drains
    /// its HP; deplete it before it reaches you and you claim the gun. A row mixes cheap and
    /// premium gates across its occupied lanes -- see GameManager.SpawnGateRow -- and always
    /// leaves at least one lane open, so no gate is ever a forced shot. A gate that reaches you
    /// undepleted just despawns -- no gun, and you spent that firepower on nothing.
    /// </summary>
    public class WeaponGate : MonoBehaviour, IPoolable
    {
        public WeaponDef Offer { get; private set; }
        public float Hp { get; private set; }
        public float MaxHp { get; private set; }
        // Instance field, not a Tuning constant: gates now occupy one lane out of RowLaneCount,
        // and every gate in a row gets the same RowItemHalfWidth, but reading it off the
        // instance (set at Configure time) keeps this from silently drifting out of step with
        // whatever the spawner actually placed here.
        public float HalfWidth { get; private set; } = Tuning.RowItemHalfWidth;

        Transform _panel;
        Transform _mount;
        Renderer _panelRenderer;
        bool _claimed;
        float _popTimer;

        // Found by name rather than serialized: ArtImporter builds this prefab in code, so the
        // names are the contract and there's no Inspector to wire anything in.
        void Awake()
        {
            _panel = transform.Find("Panel");
            _mount = transform.Find("Mount");
            if (_panel != null) _panelRenderer = _panel.GetComponent<Renderer>();
        }

        /// <summary>Called by the spawner after the pool positions us.</summary>
        public void Configure(WeaponDef offer, float laneX, float halfWidth)
        {
            Offer = offer;
            HalfWidth = halfWidth;
            // Gate numbers grow with your Level -- "bigger gates, higher numbers" -- scaled by
            // the same factor as your damage, so the break-time stays sane while the number on
            // the panel visibly climbs.
            MaxHp = offer.GateCost * GameManager.Instance.DamageMult;
            Hp = MaxHp;
            _claimed = false;

            Vector3 p = transform.position;
            p.x = laneX;
            transform.position = p;

            // Show the offered gun, hide the rest of the rack.
            if (_mount != null)
                foreach (Transform t in _mount)
                    t.gameObject.SetActive(t.name == offer.Mesh);

            UpdatePanelColor();
        }

        /// <summary>
        /// Bullets drain the gate. Returns true when this shot empties it (the claim frame), so
        /// the projectile knows to stop rather than keep hammering a spent gate.
        /// </summary>
        public bool TakeDamage(float amount)
        {
            if (_claimed) return false;

            Hp -= amount;
            UpdatePanelColor();

            if (Hp <= 0f)
            {
                Claim();
                return true;
            }
            return false;
        }

        void Claim()
        {
            _claimed = true;
            if (PlayerWeapon.Instance != null) PlayerWeapon.Instance.Equip(Offer);
            GameManager.Instance.OnGateTaken(Offer);
            _popTimer = Tuning.GatePopDuration;
        }

        /// <summary>
        /// Fills from red (full HP, far off) toward green as it drains, so "nearly broken" is
        /// legible at a glance. This colour tracks progress, not better/worse -- unlike the old
        /// red/green gates it isn't telling you which to pick, only how close you are.
        /// </summary>
        void UpdatePanelColor()
        {
            if (_panelRenderer == null) return;
            float t = MaxHp > 0f ? Mathf.Clamp01(1f - Hp / MaxHp) : 1f;   // 0 full -> 1 empty
            Color c = Color.Lerp(new Color(0.85f, 0.3f, 0.25f), new Color(0.3f, 0.85f, 0.35f), t);
            c.a = 0.45f;
            _panelRenderer.material.SetColor("_BaseColor", c);
        }

        public void OnSpawned()
        {
            _claimed = false;
            _popTimer = 0f;
            transform.localScale = Vector3.one;   // pooled: undo the last pop
            GameManager.Instance.RegisterGate(this);
        }

        public void OnDespawned() => GameManager.Instance.UnregisterGate(this);

        void Update()
        {
            if (!GameManager.Instance.RunActive) return;

            // Claimed: punch outward and get out of the way.
            if (_popTimer > 0f)
            {
                _popTimer -= Time.deltaTime;
                float t = 1f - Mathf.Clamp01(_popTimer / Tuning.GatePopDuration);
                transform.localScale = Vector3.one * Mathf.Lerp(1f, Tuning.GatePopScale, t);
                if (_popTimer <= 0f) GameManager.Instance.ReleaseGate(this);
                return;
            }

            Vector3 p = transform.position;
            p.z -= Tuning.GateSpeed * Time.deltaTime;
            transform.position = p;

            if (_mount != null)
                _mount.Rotate(0f, Tuning.GateSpinSpeed * Time.deltaTime, 0f, Space.Self);

            // Reached the player undepleted: it's gone, and the gun with it. No claim -- that's
            // the cost of committing to a gate you couldn't break in time.
            if (p.z <= Tuning.GateDespawnZ) GameManager.Instance.ReleaseGate(this);
        }
    }
}
