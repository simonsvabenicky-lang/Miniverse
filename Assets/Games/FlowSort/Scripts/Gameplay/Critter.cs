using UnityEngine;

namespace FlowSort.Gameplay
{
    /// <summary>
    /// A single ammo unit — always spawned at runtime (queue slots and lanes both just move an
    /// instance's transform around rather than recreating it), so no serialization concerns.
    /// </summary>
    public class Critter : MonoBehaviour
    {
        public PieceColor Color { get; private set; }
        public int Ammo { get; private set; }

        TMPro.TextMeshPro ammoLabel;

        public static Critter Create(Transform parent, PieceColor color, int ammo)
        {
            var go = new GameObject("Critter", typeof(SpriteRenderer));
            go.transform.SetParent(parent, false);

            var bodySr = go.GetComponent<SpriteRenderer>();
            bodySr.sprite = ArtRegistry.Instance.Block(color);
            bodySr.sortingOrder = 20;
            float srcSize = Mathf.Max(bodySr.sprite.bounds.size.x, bodySr.sprite.bounds.size.y);
            float scale = 0.6f / srcSize;
            go.transform.localScale = Vector3.one * scale;

            var faceGo = new GameObject("Face", typeof(SpriteRenderer));
            faceGo.transform.SetParent(go.transform, false);
            faceGo.transform.localPosition = new Vector3(0f, 0f, -0.01f);
            var faceSr = faceGo.GetComponent<SpriteRenderer>();
            faceSr.sprite = ArtRegistry.Instance.RandomFace();
            faceSr.sortingOrder = 21;
            float faceSrcSize = Mathf.Max(faceSr.sprite.bounds.size.x, faceSr.sprite.bounds.size.y);
            faceGo.transform.localScale = Vector3.one * ((srcSize * 0.55f) / faceSrcSize);

            var labelGo = new GameObject("AmmoLabel", typeof(TMPro.TextMeshPro));
            labelGo.transform.SetParent(go.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, -0.75f, -0.02f);
            var tmp = labelGo.GetComponent<TMPro.TextMeshPro>();
            tmp.fontSize = 6;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.color = UnityEngine.Color.white;
            tmp.sortingOrder = 22;

            var critter = go.AddComponent<Critter>();
            critter.ammoLabel = tmp;
            critter.SetState(color, ammo);
            return critter;
        }

        void SetState(PieceColor color, int ammo)
        {
            Color = color;
            Ammo = ammo;
            ammoLabel.text = ammo.ToString();
        }

        /// <returns>true if the critter still has ammo left after firing</returns>
        public bool ConsumeAmmo()
        {
            Ammo = Mathf.Max(0, Ammo - 1);
            ammoLabel.text = Ammo.ToString();
            return Ammo > 0;
        }

        public void AddAmmo(int amount)
        {
            Ammo += amount;
            ammoLabel.text = Ammo.ToString();
        }
    }
}
