using UnityEngine;

namespace FlowSort.Gameplay
{
    /// <summary>
    /// The visible row of upcoming critters. Any of them can be tapped (not just the front one)
    /// — the strategic decision is which color to prioritize sending, not queue order — and a
    /// tap only succeeds if a firing lane is currently free.
    /// </summary>
    public class CritterQueue : MonoBehaviour
    {
        public Transform QueueRoot;
        public RevealGameManager GameManager;

        readonly Critter[] slots = new Critter[GameTuning.QueueVisibleSlots];

        public void Refill()
        {
            for (int i = 0; i < slots.Length; i++)
                if (slots[i] == null) SpawnSlot(i);
        }

        public void ShuffleAll()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null) Destroy(slots[i].gameObject);
                slots[i] = null;
                SpawnSlot(i);
            }
        }

        void SpawnSlot(int index)
        {
            var color = GameManager.PickWeightedActiveColor();
            int ammo = Random.Range(GameTuning.CritterAmmoMin, GameTuning.CritterAmmoMax + 1);
            var critter = Critter.Create(QueueRoot, color, ammo);
            critter.transform.localPosition = new Vector3(SlotX(index), GameTuning.QueueY, 0f);

            var box = critter.gameObject.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.75f, 0.75f);

            var tap = critter.gameObject.AddComponent<QueueSlotTap>();
            tap.Bind(this, index);

            slots[index] = critter;
        }

        public void OnSlotTapped(int index)
        {
            var critter = slots[index];
            if (critter == null) return;

            if (GameManager.TryAssignToLane(critter))
            {
                slots[index] = null;
                SpawnSlot(index);
            }
        }

        static float SlotX(int index) =>
            (index - (GameTuning.QueueVisibleSlots - 1) / 2f) * GameTuning.QueueSlotSpacing;
    }

    class QueueSlotTap : MonoBehaviour, ITappable
    {
        CritterQueue queue;
        int index;

        public void Bind(CritterQueue owner, int slotIndex)
        {
            queue = owner;
            index = slotIndex;
        }

        public void OnTapped() => queue.OnSlotTapped(index);
    }
}
