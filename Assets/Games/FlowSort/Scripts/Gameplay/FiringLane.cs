using UnityEngine;

namespace FlowSort.Gameplay
{
    /// <summary>
    /// Holds at most one critter, which auto-fires at a random matching-color grid cell every
    /// GameTuning.FireInterval until its ammo runs out (or the grid has no cells of its color
    /// left, in which case it's released immediately rather than sitting there uselessly).
    /// </summary>
    public class FiringLane : MonoBehaviour
    {
        public PuzzleGrid Grid;
        public RevealGameManager GameManager;

        Critter current;
        float timer;

        public bool IsFree => current == null;

        public void Assign(Critter critter)
        {
            current = critter;
            critter.transform.SetParent(transform, false);
            critter.transform.localPosition = Vector3.zero;
            timer = 0f;
        }

        public void RefillCurrent(int amount) => current?.AddAmmo(amount);

        void Update()
        {
            if (current == null) return;

            if (Grid.RemainingOfColor(current.Color) <= 0)
            {
                Release();
                return;
            }

            timer += Time.deltaTime;
            if (timer < GameTuning.FireInterval) return;
            timer = 0f;
            Fire();
        }

        void Fire()
        {
            Grid.TryClearRandomCellOfColor(current.Color);
            if (!current.ConsumeAmmo()) Release();
        }

        void Release()
        {
            Destroy(current.gameObject);
            current = null;
        }
    }
}
