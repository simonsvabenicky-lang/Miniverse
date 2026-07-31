using TMPro;
using UnityEngine;

namespace FlowSort.Blocks
{
    /// <summary>
    /// Every asset reference the runtime needs, assigned once by SceneBuilder. These are plain
    /// public UnityEngine.Object reference fields, which is the one category of edit-time state
    /// that DOES survive scene serialisation into a player — see the serialisation lesson in
    /// HANDOFF.md. Everything built FROM these references is spawned at runtime.
    /// </summary>
    public class BlockArt : MonoBehaviour
    {
        [Header("Materials")]
        public Material BlockMaterial;
        public Material BallMaterial;
        public Material SlotMaterial;
        public Material BackgroundMaterial;
        public Material ParticleMaterial;
        public Material TrackMaterial;
        public Material TowerMaterial;

        [Header("Turret models (Kenney Tower Defense Kit, CC0)")]
        public GameObject TurretCommonModel;
        public GameObject TurretGoldModel;

        [Header("Track models (Kenney Racing Kit, CC0)")]
        public GameObject RoadStraightModel;
        public GameObject RoadCornerModel;
        public GameObject RoadKerbModel;
        public GameObject GateModel;
        public GameObject BarrierRedModel;
        public GameObject BarrierWhiteModel;

        [Header("Sprites (Basic GUI Bundle, CC0)")]
        public Texture2D BlockAtlasTexture;
        public Texture2D SlotFrameSprite;
        public Texture2D PanelSprite;

        [Header("Particle textures (Kenney Particle Pack, CC0)")]
        public Texture2D ParticleDirt;
        public Texture2D ParticleSpark;
        public Texture2D ParticleSmoke;
        public Texture2D ParticleStar;

        [Header("Type")]
        public TMP_FontAsset Font;

    }

    /// <summary>
    /// Builds a turret hierarchy at runtime. Kept separate from Turret so the behaviour has no
    /// knowledge of how it was assembled, and so the same rig serves both the slot row and the
    /// queue (queue items are non-firing turret rigs).
    /// </summary>
    public static class TowerFactory
    {
        /// <summary>
        /// Kenney's turret mesh is 0.62 along its barrel axis, so this puts it at roughly
        /// 4.3 world units — a little under the 5.6-unit slot pitch. At the original 2.6 it
        /// rendered as a 1.6-unit speck lost in its slot.
        /// </summary>
        public const float ModelScale = 5f;

        /// <summary>
        /// Kenney's tower-defense pieces are authored for a top-down view: barrel along +Y-up
        /// with the base on the ground plane. Pitching the visual back by 90 degrees stands it up
        /// to face our front-on camera, so the aim rotation about Z then reads correctly.
        /// </summary>
        static readonly Vector3 VisualEuler = new Vector3(-90f, 0f, 0f);

        public static ConveyorTower Create(BlockArt art, byte colorIndex, int ammo, BallSystem balls,
                                           BlockWall wall, ImpactFX fx,
                                           Transform parent, Vector3 restPosition)
        {
            var root = new GameObject($"Tower_{colorIndex}");
            root.transform.SetParent(parent, false);
            root.transform.position = restPosition;

            // "Model" carries aim rotation + recoil; the FBX instance underneath carries only
            // the fixed stand-up rotation, so the two never fight.
            var model = new GameObject("Model").transform;
            model.SetParent(root.transform, false);

            var source = art.TurretCommonModel;
            if (source != null)
            {
                var visual = Object.Instantiate(source, model, false);
                visual.name = "Visual";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localEulerAngles = VisualEuler;
                visual.transform.localScale = Vector3.one * ModelScale;

                // Tinted to the colour this tower can actually destroy. A property block keeps
                // the shared material intact, so all towers still batch.
                var block = new MaterialPropertyBlock();
                Color tint = BlockPalette.Get(colorIndex);

                foreach (var r in visual.GetComponentsInChildren<MeshRenderer>())
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    r.receiveShadows = false;

                    if (art.TowerMaterial != null) r.sharedMaterial = art.TowerMaterial;

                    r.GetPropertyBlock(block);
                    block.SetColor(BaseColorId, tint);
                    r.SetPropertyBlock(block);
                }
            }

            var label = BuildLabel(art, root.transform);

            var tower = root.AddComponent<ConveyorTower>();
            tower.Wall = wall;
            tower.Fx = fx;
            tower.Init(colorIndex, ammo, balls, model, label, restPosition);

            // The only physics collider in the scene — used purely for tap selection.
            var box = root.AddComponent<BoxCollider>();
            box.size = new Vector3(BlockTuning.SlotSpacing * 0.85f, BlockTuning.SlotSpacing * 0.85f, 2f);

            return tower;
        }

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        static TMP_Text BuildLabel(BlockArt art, Transform parent)
        {
            var go = new GameObject("Ammo", typeof(TextMeshPro));
            go.transform.SetParent(parent, false);

            // Well clear of the model's front face (the mesh is ~2.1 units deep once scaled), so
            // the number can't end up buried inside the turret geometry. Lifted slightly because
            // the turret's legs pull its visual mass above its bounds centre.
            go.transform.localPosition = new Vector3(0f, 0.5f, -4f);

            var tmp = go.GetComponent<TextMeshPro>();
            tmp.font = art.Font != null ? art.Font : TMP_Settings.defaultFontAsset;
            tmp.fontSize = 11f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.outlineWidth = 0.3f;
            tmp.enableWordWrapping = false;
            tmp.enableAutoSizing = false;
            tmp.overflowMode = TextOverflowModes.Overflow;

            // Generous box: at fontSize 13 a three-digit number needs far more than the 6 units
            // it had before, and an undersized rect is what hides the text entirely.
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(26f, 12f);

            return tmp;
        }
    }
}
