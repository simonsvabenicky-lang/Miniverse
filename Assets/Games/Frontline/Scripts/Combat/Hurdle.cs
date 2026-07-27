using UnityEngine;

namespace Frontline
{
    /// <summary>
    /// A plain shoot-through obstacle in one lane. Same lane-row mechanics as WeaponGate --
    /// HP you drain, marches in with the horde, despawns if it reaches the player -- but grants
    /// no weapon on clearing. It exists purely to make an occupied lane something you either
    /// avoid or spend a few bullets to clear, without it being a resource decision.
    /// </summary>
    public class Hurdle : MonoBehaviour, IPoolable
    {
        public float Hp { get; private set; }
        public float MaxHp { get; private set; }
        public float HalfWidth { get; private set; } = Tuning.RowItemHalfWidth;

        Transform _mount;
        bool _cleared;
        float _popTimer;

        void Awake() => _mount = transform.Find("Mount");

        public void Configure(float laneX, float halfWidth)
        {
            HalfWidth = halfWidth;
            // Scales with Level like a gate's cost, so a hurdle stays a real (if minor) speed
            // bump instead of falling trivially behind your growing damage.
            MaxHp = Tuning.HurdleBaseHp * GameManager.Instance.DamageMult;
            Hp = MaxHp;
            _cleared = false;

            Vector3 p = transform.position;
            p.x = laneX;
            transform.position = p;

            if (_mount != null)
            {
                string pick = ArtImporter_HurdleProps[Random.Range(0, ArtImporter_HurdleProps.Length)];
                foreach (Transform t in _mount)
                    t.gameObject.SetActive(t.name == pick);
            }
        }

        /// <summary>Mirrors ArtImporter.HurdleProps -- the prefab's Mount children are named
        /// for these, and this runtime code has no access to the Editor-only class.</summary>
        static readonly string[] ArtImporter_HurdleProps = { "Crate", "GasTank", "CardboardBoxes_2", "ExplodingBarrel" };

        /// <returns>true if this hit cleared the hurdle.</returns>
        public bool TakeDamage(float amount)
        {
            if (_cleared) return false;
            Hp -= amount;
            if (Hp <= 0f)
            {
                _cleared = true;
                _popTimer = Tuning.GatePopDuration;
                if (Audio.Instance != null) Audio.Instance.Play(Sfx.Kill);   // "something destroyed"
                return true;
            }
            return false;
        }

        public void OnSpawned()
        {
            _cleared = false;
            _popTimer = 0f;
            transform.localScale = Vector3.one;
            GameManager.Instance.RegisterHurdle(this);
        }

        public void OnDespawned() => GameManager.Instance.UnregisterHurdle(this);

        void Update()
        {
            if (!GameManager.Instance.RunActive) return;

            if (_popTimer > 0f)
            {
                _popTimer -= Time.deltaTime;
                float t = 1f - Mathf.Clamp01(_popTimer / Tuning.GatePopDuration);
                transform.localScale = Vector3.one * Mathf.Lerp(1f, Tuning.GatePopScale, t);
                if (_popTimer <= 0f) GameManager.Instance.ReleaseHurdle(this);
                return;
            }

            Vector3 p = transform.position;
            p.z -= Tuning.GateSpeed * Time.deltaTime;
            transform.position = p;

            if (p.z <= Tuning.GateDespawnZ) GameManager.Instance.ReleaseHurdle(this);
        }
    }
}
