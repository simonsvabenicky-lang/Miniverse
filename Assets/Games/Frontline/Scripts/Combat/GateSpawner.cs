using System.Collections;
using UnityEngine;

namespace Frontline
{
    /// <summary>
    /// Periodically sends a lane row down the road -- 1-3 of the 5 lanes occupied by a gate or a
    /// hurdle, the rest left open. See GameManager.SpawnGateRow for the actual layout logic.
    /// </summary>
    public class GateSpawner : MonoBehaviour
    {
        void OnEnable() => StartCoroutine(RunGates());

        IEnumerator RunGates()
        {
            // Offset from the wave clock so gates don't arrive buried inside a wave every time.
            yield return new WaitForSeconds(Tuning.GateFirstDelay);

            while (true)
            {
                if (!GameManager.Instance.RunActive) yield break;

                GameManager.Instance.SpawnGateRow();
                yield return new WaitForSeconds(Tuning.GateInterval);
            }
        }
    }
}
