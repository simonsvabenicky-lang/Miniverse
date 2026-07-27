using UnityEngine;

namespace Frontline
{
    /// <summary>
    /// Fires forward on a fixed cadence, always. The player never taps to shoot in this
    /// genre — steering is the only verb, and taking the trigger away is what makes it
    /// playable one-handed.
    ///
    /// Cadence and shot pattern come from the equipped weapon rather than Tuning, so a gate
    /// pickup changes how the gun feels and not just a damage number.
    /// </summary>
    public class AutoFirer : MonoBehaviour
    {
        float _cooldown;
        PlayerWeapon _weapon;

        void Awake() => _weapon = GetComponent<PlayerWeapon>();

        void Update()
        {
            if (!GameManager.Instance.RunActive) return;

            WeaponDef def = _weapon != null && _weapon.Current != null ? _weapon.Current : Weapons.Starting;

            _cooldown -= Time.deltaTime;
            if (_cooldown > 0f) return;
            _cooldown = def.FireInterval;

            // From the barrel, not from a guessed offset off the player's pivot -- see
            // PlayerWeapon.MuzzlePosition for what that guess actually did.
            Vector3 muzzle = _weapon != null
                ? _weapon.MuzzlePosition
                : transform.position + Vector3.forward * Tuning.MuzzleForwardOffset
                                     + Vector3.up * Tuning.MuzzleHeight;

            // One flash and one bang per trigger pull, not per pellet -- a shotgun blast is one
            // shot, not five.
            GameManager.Instance.SpawnFlash(muzzle, Tuning.MuzzleFlashSize, Tuning.MuzzleFlashDuration,
                                            new Color(1f, 0.75f, 0.3f), Tuning.MuzzleFlashEmission);
            // Per-gun: a minigun and a shotgun sounding identical is most of why the weapons
            // don't read as different.
            if (Audio.Instance != null) Audio.Instance.PlayShot(def.Mesh, def.ShotVolume, def.ShotPitch);

            for (int i = 0; i < def.Pellets; i++)
            {
                // Fan the pellets evenly across the cone rather than randomising: a shotgun
                // whose spread is random reads as "my gun is broken" at this camera distance,
                // where you can see every individual pellet fly.
                float t = def.Pellets == 1 ? 0.5f : i / (float)(def.Pellets - 1);
                float yaw = (t - 0.5f) * def.SpreadDegrees;
                Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                // Damage scales with the stage -- this is the "gun level" keeping your power in
                // step with the enemy HP ramp. + PlayerPullback on range because the muzzle
                // moved back with the soldier, so the bullet travels further to the same line.
                // A second, independent multiplier from Shop upgrades (permanent, meta) stacks
                // on top -- see Tuning.WeaponUpgradeMultAt.
                float metaMult = Tuning.WeaponUpgradeMultAt(SaveData.I.WeaponLevel(def.Mesh));
                float damage = def.Damage * GameManager.Instance.DamageMult * metaMult;
                GameManager.Instance.SpawnProjectile(muzzle, dir, damage, def.Pierce,
                                                     def.Range + Tuning.PlayerPullback);
            }
        }
    }
}
