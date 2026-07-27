using System.Collections.Generic;
using UnityEngine;

namespace Frontline
{
    /// <summary>
    /// Flies its aim direction and resolves hits with a plain distance test against the live
    /// enemy list. No colliders, no rigidbodies, no physics scene. At our counts (tens of
    /// bullets vs tens of enemies) the brute-force check is a rounding error on the frame
    /// budget, and it buys us fully deterministic hits with zero physics setup to get wrong.
    /// </summary>
    public class Projectile : MonoBehaviour, IPoolable
    {
        float _rangeLeft;
        Vector3 _direction = Vector3.forward;
        float _damage = 10f;
        int _pierceLeft;

        // Enemies this bullet has already damaged. A piercing round overlaps the same target for
        // several frames on its way through, so without this it would hit him once per frame and
        // never reach the man behind.
        readonly List<Enemy> _alreadyHit = new List<Enemy>(6);

        // Counts down distance travelled rather than seconds alive, so Tuning.WeaponRange
        // means what it says and stays honest if ProjectileSpeed is ever retuned.
        public void OnSpawned() => _rangeLeft = Tuning.WeaponRange;
        public void OnDespawned() { }

        /// <summary>Set by the firer after the pool hands it back, before the first Update.</summary>
        public void Configure(Vector3 direction, float damage, int pierce, float range)
        {
            _direction = direction.normalized;
            _damage = damage;
            _pierceLeft = pierce;
            _alreadyHit.Clear();
            _rangeLeft = range;   // per-weapon, set after OnSpawned seeded the default
        }

        void Update()
        {
            float step = Tuning.ProjectileSpeed * Time.deltaTime;
            _rangeLeft -= step;
            if (_rangeLeft <= 0f)
            {
                GameManager.Instance.ReleaseProjectile(this);
                return;
            }

            Vector3 p = transform.position + _direction * step;
            transform.position = p;

            // A gate or hurdle in this bullet's lane stops it outright (no pierce through a
            // panel or a barrel). That's deliberate for occupied lanes -- but the row always
            // leaves lanes open, so a bullet aimed down an open lane never meets either and
            // reaches whatever enemy is actually there.
            WeaponGate gate = GameManager.Instance.FindGateOverlapping(p);
            if (gate != null)
            {
                bool claimed = gate.TakeDamage(_damage);
                GameManager.Instance.SpawnFlash(p, Tuning.HitFlashSize, Tuning.HitFlashDuration,
                                                claimed ? Color.white : new Color(0.5f, 0.9f, 1f),
                                                Tuning.HitFlashEmission);
                GameManager.Instance.ReleaseProjectile(this);
                return;
            }

            Hurdle hurdle = GameManager.Instance.FindHurdleOverlapping(p);
            if (hurdle != null)
            {
                hurdle.TakeDamage(_damage);
                GameManager.Instance.SpawnFlash(p, Tuning.HitFlashSize, Tuning.HitFlashDuration,
                                                new Color(0.75f, 0.7f, 0.6f), Tuning.HitFlashEmission);
                GameManager.Instance.ReleaseProjectile(this);
                return;
            }

            Enemy hit = GameManager.Instance.FindEnemyOverlapping(p, Tuning.ProjectileRadius);
            if (hit != null && !_alreadyHit.Contains(hit))
            {
                _alreadyHit.Add(hit);
                bool killed = hit.TakeDamage(_damage);

                // White on the kill, warm on a wound: the difference between "still shooting
                // this one" and "that one's done" is worth being able to read at a glance.
                Color c = killed ? Color.white : new Color(1f, 0.85f, 0.45f);
                GameManager.Instance.SpawnFlash(p, Tuning.HitFlashSize, Tuning.HitFlashDuration,
                                                c, Tuning.HitFlashEmission);

                // Both pitched up: the impact clips are almost pure bass (bright 0.016-0.024)
                // and a phone speaker cannot reproduce them at any volume. Shifting them up
                // moves the energy into the band the hardware can actually move.
                if (Audio.Instance != null)
                    Audio.Instance.Play(killed ? Sfx.Kill : Sfx.Hit,
                                        killed ? Tuning.KillVolume : Tuning.HitVolume,
                                        killed ? Tuning.KillPitch : Tuning.HitPitch);

                // Keep going if this round still has punch left in it.
                if (_pierceLeft <= 0)
                {
                    GameManager.Instance.ReleaseProjectile(this);
                    return;
                }
                _pierceLeft--;
            }
        }
    }
}
