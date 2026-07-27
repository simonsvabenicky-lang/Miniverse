using FlowSort.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace FlowSort.UI
{
    public class PowerupBar : MonoBehaviour
    {
        public RevealGameManager GameManager;
        public Button RefillButton;
        public Button ShuffleButton;
        public Button UndoButton;
        public Button HintButton;

        void Start()
        {
            RefillButton.onClick.AddListener(GameManager.UsePowerupRefill);
            ShuffleButton.onClick.AddListener(GameManager.UsePowerupShuffle);
            UndoButton.onClick.AddListener(GameManager.UsePowerupUndo);
            HintButton.onClick.AddListener(GameManager.UsePowerupHint);
        }
    }
}
