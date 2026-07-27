using System.Collections.Generic;
using UnityEngine;

namespace Frontline
{
    /// <summary>
    /// What the soldier is holding. Swapping is just toggling one of the gun meshes that
    /// already hang off his hand bone in the FBX -- no attach points, no instantiation.
    /// </summary>
    public class PlayerWeapon : MonoBehaviour
    {
        public static PlayerWeapon Instance { get; private set; }

        public WeaponDef Current { get; private set; }

        readonly Dictionary<string, GameObject> _rack = new Dictionary<string, GameObject>();
        Renderer _activeGun;

        /// <summary>
        /// World position of the equipped gun's barrel tip, taken from the gun's own bounds.
        ///
        /// This used to be a guess -- player position + a forward offset + a height -- which put
        /// it in the middle of the soldier's chest. The gun is held out to his right, so bullets
        /// flew out of his sternum and the muzzle flash spawned *inside* his torso, invisible.
        /// Reported as "no flashes from gun barrel" and "bullets don't fly out of weapon barrel
        /// but like middle of the character": one cause, both symptoms.
        ///
        /// bounds is the world AABB and updates with the animation, so max.z is the forward-most
        /// point of the gun -- the barrel -- wherever the hand currently is.
        /// </summary>
        public Vector3 MuzzlePosition
        {
            get
            {
                if (_activeGun == null)
                    return transform.position
                           + Vector3.forward * Tuning.MuzzleForwardOffset
                           + Vector3.up * Tuning.MuzzleHeight;

                Bounds b = _activeGun.bounds;
                return new Vector3(b.center.x, b.center.y, b.max.z + Tuning.MuzzleTipGap);
            }
        }

        void Awake()
        {
            Instance = this;

            // Cache every gun mesh once. Looked up by name because that's the contract the FBX
            // gives us, and it's the same list ArtImporter uses to build the prefab.
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
                if (System.Array.IndexOf(GunMeshNames, t.name) >= 0)
                    _rack[t.name] = t.gameObject;

            Equip(Weapons.Starting);
        }

        public void Equip(WeaponDef def)
        {
            if (def == null || def == Current) return;
            Current = def;

            foreach (var kv in _rack)
            {
                bool equipped = kv.Key == def.Mesh;
                kv.Value.SetActive(equipped);
                if (equipped) _activeGun = kv.Value.GetComponentInChildren<Renderer>();
            }
        }

        /// <summary>
        /// Mirrors ArtImporter.GunMeshes. Duplicated rather than shared because that lives in
        /// an Editor assembly and this has to run in a build.
        /// </summary>
        public static readonly string[] GunMeshNames = {
            "AK", "Pistol", "Revolver", "Revolver_Small", "RocketLauncher", "GrenadeLauncher",
            "Shotgun", "SMG", "Sniper", "Sniper_2", "ShortCannon", "Knife_1", "Knife_2", "Shovel"
        };
    }
}
