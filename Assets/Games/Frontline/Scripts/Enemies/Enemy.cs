using UnityEngine;

namespace Frontline
{
    public class Enemy : MonoBehaviour, IPoolable
    {
        /// <summary>Animator trigger. ArtImporter builds the controller against this name.</summary>
        public const string DieTrigger = "Die";

        float _health;
        float _speed;
        bool _dying;
        float _deathTimer;
        bool _isBoss;
        Animator _animator;

        public float Radius => Tuning.EnemyRadius;
        public bool IsBoss => _isBoss;

        void Awake() => _animator = GetComponent<Animator>();

        public void OnSpawned()
        {
            // Stats captured at spawn from the current stage, so an enemy that's already marching
            // doesn't suddenly get tankier when the stage ticks over mid-lane.
            int stage = GameManager.Instance.Stage;
            _health = Tuning.EnemyHealthAt(stage);
            _speed = Tuning.EnemySpeedAt(stage);
            _dying = false;
            _deathTimer = 0f;
            // Reset every spawn -- this is a pooled instance, and without resetting scale a
            // slot that was last used for a boss would stay oversized for the next ordinary
            // enemy pulled from it.
            _isBoss = false;
            transform.localScale = Vector3.one;

            // These are pooled, so the animator arrives still holding the last corpse's pose
            // and a spent Die trigger. Without the rebind, recycled enemies spawn dead.
            if (_animator != null)
            {
                _animator.ResetTrigger(DieTrigger);
                _animator.Rebind();
                _animator.Update(0f);   // apply the reset this frame, not next
            }

            GameManager.Instance.RegisterEnemy(this);
        }

        /// <summary>
        /// Called right after OnSpawned (see GameManager.SpawnBoss) to turn a freshly-spawned,
        /// stage-scaled ordinary enemy into a boss: a flat multiplier on top of its already-set
        /// stats, plus a visible scale bump. Deliberately NOT a colour/material tint -- this
        /// project already broke Character_Enemy's rendering twice trying that (per-submesh
        /// textured materials, not the flat _BaseColor a property block assumes), and a uniform
        /// transform scale can never misrender a character the way a tint did.
        /// </summary>
        public void ConfigureAsBoss()
        {
            _isBoss = true;
            _health *= Tuning.BossHealthMult;
            _speed *= Tuning.BossSpeedMult;
            transform.localScale = Vector3.one * Tuning.BossScale;
        }

        public void OnDespawned()
        {
            GameManager.Instance.UnregisterEnemy(this);
        }

        void Update()
        {
            // The run is over -- freeze where we stand. Without this the horde kept marching
            // over the corpse and draining lives well past OVERRUN (observed: LIVES -20).
            if (!GameManager.Instance.RunActive) return;

            // Falling over. Not moving, not shootable, still on screen.
            if (_dying)
            {
                _deathTimer -= Time.deltaTime;

                // No body-fall sound, on request. The attempt used explosionCrunch -- the only
                // impact-ish clip a phone speaker actually reproduces -- but that is also the
                // shotgun's clip, so every one of ~80 deaths a run fired a shotgun blast and
                // drowned the entire mix. Better nothing than that; a proper one can be dropped
                // in later.
                if (_deathTimer <= 0f) GameManager.Instance.ReleaseEnemy(this);
                return;
            }

            Vector3 p = transform.position;
            p.z -= _speed * Time.deltaTime;
            transform.position = p;

            if (p.z <= Tuning.EnemyBreachZ)
            {
                // Reached the player: costs a life, not a kill.
                GameManager.Instance.OnEnemyBreachedLine(this);
            }
        }

        /// <returns>true if this hit killed the enemy</returns>
        public bool TakeDamage(float amount)
        {
            if (_dying) return false;

            _health -= amount;
            if (_health > 0f) return false;

            Die();
            return true;
        }

        /// <summary>
        /// Play the death animation, then despawn -- rather than blinking out of existence the
        /// instant health hits zero, which is what it did before.
        ///
        /// The two halves are deliberately split. The enemy leaves the registry *now*, so it
        /// stops soaking bullets and the kill registers immediately; it just keeps rendering
        /// while it falls. Leaving corpses in the registry would eat shots meant for live
        /// targets and make the gun feel broken -- exactly the sort of thing "juice" usually
        /// breaks.
        /// </summary>
        void Die()
        {
            _dying = true;
            _deathTimer = Tuning.EnemyDeathDuration;
            GameManager.Instance.OnEnemyKilled(this);
            if (_animator != null) _animator.SetTrigger(DieTrigger);
        }
    }
}
