using UnityEngine;

namespace FlowSort.Gameplay
{
    /// <summary>
    /// Every Kenney sprite the game uses, in one place. Populated once by SceneBuilder at edit
    /// time (public fields, so the references survive scene serialization the normal way) —
    /// everything else looks sprites up through ArtRegistry.Instance rather than loading assets
    /// itself.
    /// </summary>
    public class ArtRegistry : MonoBehaviour
    {
        public static ArtRegistry Instance { get; private set; }

        [Header("Grid blocks — indexed by PieceColor")]
        public Sprite[] BlockSprites = new Sprite[6];

        [Header("Critter faces — random per critter")]
        public Sprite[] FaceSprites = new Sprite[4];

        [Header("Icons")]
        public Sprite KeyIcon;
        public Sprite CoinIcon;
        public Sprite StarIcon;
        public Sprite LockedIcon;
        public Sprite UnlockedIcon;

        [Header("UI chrome")]
        public Sprite PanelSprite;
        public Sprite RoundButtonGrey;
        public Sprite[] RoundButtonColored = new Sprite[4]; // Blue, Green, Red, Yellow

        void Awake() => Instance = this;

        public Sprite Block(PieceColor c) => BlockSprites[(int)c];
        public Sprite RandomFace() => FaceSprites[Random.Range(0, FaceSprites.Length)];
    }
}
