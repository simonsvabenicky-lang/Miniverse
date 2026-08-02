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

        [Header("Turret model (Modern-Military ToonShooterKit)")]
        public GameObject TurretCommonModel;

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
        /// The tank's mesh is authored tiny — raw bounds 0.0211 x 0.0223 x 0.0191 (X/Y/Z, before
        /// the stand-up pitch below) — and setting Transform.localScale, unlike parenting under an
        /// unscaled prefab instance, REPLACES the value rather than multiplying it. So this can't
        /// lean on Unity's usual "FBX unit conversion just works" default the way every other
        /// import in this project does: it has to target the true mesh size directly, or the tank
        /// renders at roughly 1/100th its intended size — present and tappable (everything else
        /// about a tower is built off its transform, not its mesh) but visually gone.
        /// Pitched, its footprint in the slot is 0.0211 wide by 0.0223 tall, and this scales that
        /// to fill roughly the same fraction of the slot pitch the old Kenney turret did (~75%),
        /// so the swap didn't also change how crowded a full belt reads.
        /// </summary>
        public const float ModelScale = 174f;

        /// <summary>
        /// The tank is a normal upright vehicle model — Y up, footprint on the XZ ground plane —
        /// but the game's play field is the XY plane (the camera looks straight down +Z). Pitching
        /// back 90 degrees around X lays the model's up-axis into the field's Y axis, the same
        /// flattening the previous top-down Kenney turret needed for the opposite reason. The
        /// extra 180 yaw is because the model's front faces -Z in its own space, which after the
        /// pitch put its BACK toward the camera — this turns the front to face the player instead.
        /// </summary>
        static readonly Vector3 VisualEuler = new Vector3(-90f, 180f, 0f);

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

            // Well clear of the model's front face (the tank is ~3.1 units deep once scaled), so
            // the number can't end up buried inside the turret geometry. Lifted slightly because
            // the model's visual mass sits above its bounds centre.
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
