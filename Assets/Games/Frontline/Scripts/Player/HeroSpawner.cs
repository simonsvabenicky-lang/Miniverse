using UnityEngine;

namespace Frontline
{
    /// <summary>
    /// SceneBuilder always instantiates the Soldier by default -- simplest authoring, one fixed
    /// player transform to build the camera/props around, and the scene stays a build artifact
    /// that never needs regenerating just because a player bought a different hero on their
    /// phone. If SaveData says a different hero is equipped, this swaps the Soldier for the real
    /// thing at Awake, before the first frame renders.
    ///
    /// Hero prefabs are baked in via SerializedObject (see SceneBuilder.Build), not loaded by
    /// path at runtime -- AssetDatabase is Editor-only and would silently return null in an
    /// actual build, same reason CheckboxToggle/ShopActionButton bake their sprites instead of
    /// loading them by name.
    /// </summary>
    public class HeroSpawner : MonoBehaviour
    {
        [SerializeField] GameObject[] _heroPrefabs;   // indexed to match Heroes.All

        void Awake()
        {
            string equipped = SaveData.I.EquippedHero;
            if (equipped == Heroes.Starting.Id) return;   // already the right instance

            GameObject prefab = null;
            for (int i = 0; i < Heroes.All.Length && i < _heroPrefabs.Length; i++)
                if (Heroes.All[i].Id == equipped) { prefab = _heroPrefabs[i]; break; }
            if (prefab == null) return;

            var instance = Instantiate(prefab, transform.position, transform.rotation);

            CameraRig rig = Camera.main != null ? Camera.main.GetComponent<CameraRig>() : null;
            if (rig != null) rig.target = instance.transform;

            // SetActive(false) before Destroy: Destroy only takes effect at end of frame, and
            // without this the default Soldier would still render for one frame, overlapping
            // the real hero at the same spot.
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
