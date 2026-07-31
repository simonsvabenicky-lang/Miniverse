using UnityEngine;

namespace FlowSort.Blocks
{
    /// <summary>
    /// All destruction feedback: block shatter particles, muzzle flashes, camera shake and SFX.
    ///
    /// One ParticleSystem per effect driven by Emit() rather than a system instantiated per event
    /// — at several shots a second per tower, spawning systems would be the single biggest
    /// avoidable cost in the frame. Sound goes through the shared Sfx player, which owns the
    /// voice pool for both scenes.
    /// </summary>
    public class ImpactFX : MonoBehaviour
    {
        public Material ParticleMaterial;

        ParticleSystem shatter;
        ParticleSystem sparks;

        Transform shakeTarget;
        float shakeAmount;
        Vector3 shakeBase;

        void Awake()
        {
            shatter = BuildSystem("Shatter", 0.55f, 9f, 0.55f);
            sparks = BuildSystem("Sparks", 0.18f, 0f, 0.28f);

            var cam = Camera.main;
            if (cam != null)
            {
                shakeTarget = cam.transform;
                shakeBase = shakeTarget.localPosition;
            }
        }

        ParticleSystem BuildSystem(string name, float life, float gravity, float size)
        {
            var go = new GameObject(name, typeof(ParticleSystem));
            go.transform.SetParent(transform, false);

            var ps = go.GetComponent<ParticleSystem>();

            var main = ps.main;
            main.startLifetime = life;
            main.startSpeed = 0f;          // supplied per-emit
            main.startSize = size;
            main.gravityModifier = gravity * 0.05f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 600;
            main.playOnAwake = false;

            var emission = ps.emission;
            emission.enabled = false;      // Emit() only

            var shape = ps.shape;
            shape.enabled = false;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.material = ParticleMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            return ps;
        }

        /// <summary>A block was destroyed: colour-matched debris, a tick of shake, and a hit sound.</summary>
        public void BlockDestroyed(Vector3 position, Color color)
        {
            var p = new ParticleSystem.EmitParams
            {
                position = position,
                // Particle start colours land in vertex colours untouched, so they need the same
                // colour-space correction the meshes do or the debris comes out washed out.
                startColor = BlockPalette.ToVertex(color),
                applyShapeToPosition = false
            };

            for (int i = 0; i < 5; i++)
            {
                p.velocity = new Vector3(Random.Range(-4.5f, 4.5f), Random.Range(1.5f, 7f), Random.Range(-1f, -4f));
                p.startSize = Random.Range(0.35f, 0.75f);
                shatter.Emit(p, 1);
            }

            shakeAmount = Mathf.Min(shakeAmount + 0.06f, 0.5f);
            Meta.Sfx.Instance?.Break();
        }

        public void MuzzleFlash(Vector3 position, Color color)
        {
            var p = new ParticleSystem.EmitParams
            {
                position = position,
                startColor = BlockPalette.ToVertex(color),
                velocity = Vector3.zero,
                startSize = 1.1f
            };
            sparks.Emit(p, 1);

            var sfx = Meta.Sfx.Instance;
            sfx?.Play(sfx.Shot, 0.13f, Random.Range(0.94f, 1.08f));
        }

        public void LevelCleared(Vector3 center)
        {
            var p = new ParticleSystem.EmitParams { position = center, applyShapeToPosition = false };

            for (int i = 0; i < 48; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float s = Random.Range(6f, 20f);
                p.velocity = new Vector3(Mathf.Cos(a) * s, Mathf.Sin(a) * s, Random.Range(-1f, -5f));
                p.startColor = BlockPalette.GetVertex(BlockPalette.RandomBlockIndex());
                p.startSize = Random.Range(0.5f, 1.1f);
                shatter.Emit(p, 1);
            }

            shakeAmount = 0.9f;

            var sfx = Meta.Sfx.Instance;
            sfx?.ResetStreak();
            sfx?.Play(sfx.LevelComplete, 0.7f);
        }

        void LateUpdate()
        {
            if (shakeTarget == null) return;

            if (shakeAmount <= 0.001f)
            {
                shakeTarget.localPosition = shakeBase;
                return;
            }

            shakeTarget.localPosition = shakeBase + new Vector3(
                Random.Range(-shakeAmount, shakeAmount),
                Random.Range(-shakeAmount, shakeAmount),
                0f);

            // Fast decay: shake should punctuate a hit, not wobble continuously under sustained fire.
            shakeAmount = Mathf.Max(0f, shakeAmount - Time.deltaTime * 3.5f);
        }
    }
}
