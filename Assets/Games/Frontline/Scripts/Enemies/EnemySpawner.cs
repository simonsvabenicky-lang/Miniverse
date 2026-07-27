using System.Collections;
using UnityEngine;

namespace Frontline
{
    /// <summary>
    /// Wave director. Deliberately dumb for the grey-box: waves grow linearly and spawn
    /// at random X. Once the loop proves fun this becomes data-driven (formations,
    /// enemy types, boss beats) — but that's wasted work until we know it's worth it.
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        int _waveIndex;

        void OnEnable() => StartCoroutine(RunWaves());

        IEnumerator RunWaves()
        {
            // Small grace period so the player sees the field before it fills.
            yield return new WaitForSeconds(1.0f);

            while (true)
            {
                if (!GameManager.Instance.RunActive) yield break;

                int stage = GameManager.Instance.Stage;
                int count = Mathf.Min(
                    Tuning.WaveSizeBase + _waveIndex * Tuning.WaveSizeGrowth
                        + (stage - 1) * Tuning.WaveSizePerStage,
                    Tuning.WaveSizeMax);

                for (int i = 0; i < count; i++)
                {
                    // The FPS guard: never let live bodies exceed the cap, whatever the wave
                    // math says. Difficulty past this is stats, not more rigs.
                    while (GameManager.Instance.EnemyCount >= Tuning.EnemyConcurrentCap)
                    {
                        if (!GameManager.Instance.RunActive) yield break;
                        yield return null;
                    }

                    GameManager.Instance.SpawnEnemy(new Vector3(PickSpawnX(), 0f, Tuning.EnemySpawnZ));
                    yield return new WaitForSeconds(Tuning.WaveSpawnSpread);
                }

                _waveIndex++;
                GameManager.Instance.OnWaveStarted(_waveIndex);
                yield return new WaitForSeconds(Tuning.WaveIntervalAt(GameManager.Instance.Stage));
            }
        }

        /// <summary>
        /// A random X that avoids any lane a live weapon gate currently occupies. See
        /// GameManager.IsGateLane for why: spawning an enemy into a gate's lane spawns it
        /// already shielded, for up to the gate's whole ~10s lifetime. Hurdle lanes are left
        /// completely alone -- enemies are meant to be able to spawn and march behind those.
        /// Bounded retries so a freak worst case (every lane blocked) can't hang the spawner;
        /// it just spawns anyway rather than skip the enemy.
        /// </summary>
        static float PickSpawnX()
        {
            float x = Random.Range(-Tuning.LaneHalfWidth, Tuning.LaneHalfWidth);
            for (int attempt = 0; attempt < 6 && GameManager.Instance.IsGateLane(x); attempt++)
                x = Random.Range(-Tuning.LaneHalfWidth, Tuning.LaneHalfWidth);
            return x;
        }
    }
}
