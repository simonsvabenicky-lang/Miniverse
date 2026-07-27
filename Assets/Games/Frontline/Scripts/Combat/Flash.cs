using UnityEngine;

namespace Frontline
{
    /// <summary>
    /// A short-lived emissive pop. One prefab serves both the muzzle flash and the bullet
    /// impact — they're the same thing at different sizes, colours and lifetimes, and a single
    /// pooled object is cheaper than two.
    ///
    /// Impact sparks rather than flashing the enemy's body on purpose: the character is 16
    /// sub-meshes with their own materials, so a body flash means caching and lerping every
    /// base colour. A spark also marks *where* the bullet landed, which is what hit feedback is
    /// actually for.
    /// </summary>
    public class Flash : MonoBehaviour, IPoolable
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        Renderer _renderer;
        MaterialPropertyBlock _mpb;
        float _life;
        float _duration = 0.1f;
        float _size = 0.3f;

        void Awake()
        {
            _renderer = GetComponentInChildren<Renderer>();
            _mpb = new MaterialPropertyBlock();
        }

        public void OnSpawned() { }
        public void OnDespawned() { }

        /// <summary>Set by the spawner right after the pool hands it back.</summary>
        public void Configure(float size, float duration, Color color, float emission)
        {
            _size = size;
            _duration = duration;
            _life = duration;
            transform.localScale = Vector3.one * size;

            // A property block, not a material instance: these are pooled and fire dozens of
            // times a second, so touching .material would leak a new material per flash.
            if (_renderer != null)
            {
                _renderer.GetPropertyBlock(_mpb);
                _mpb.SetColor(BaseColorId, color);
                _mpb.SetColor(EmissionColorId, color * emission);
                _renderer.SetPropertyBlock(_mpb);
            }
        }

        void Update()
        {
            _life -= Time.deltaTime;
            if (_life <= 0f)
            {
                GameManager.Instance.ReleaseFlash(this);
                return;
            }

            // Shrink out. Cheaper than fading (no alpha blending) and reads harder at this
            // camera distance, where a flash is only a few pixels across.
            float t = _life / _duration;
            transform.localScale = Vector3.one * (_size * t);
        }
    }
}
