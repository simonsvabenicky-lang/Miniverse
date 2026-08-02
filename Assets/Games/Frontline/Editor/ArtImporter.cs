using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Frontline.EditorTools
{
    /// <summary>
    /// Import-side handling for the Quaternius Toon Shooter Game Kit (CC0).
    ///
    /// Build() generates the character prefabs; like the scene and the volume profile they
    /// are build artifacts, regenerated from code so nothing has to be wired by hand.
    /// Report() exists because the FBX are binary and their clip names aren't greppable --
    /// asking Unity what it actually imported beats guessing from the file format.
    ///
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod Frontline.EditorTools.ArtImporter.Build
    /// </summary>
    public static class ArtImporter
    {
        const string ArtRoot = "Assets/Games/Frontline/Art";
        const string PrefabDir = "Assets/Games/Frontline/Art/Prefabs";
        const string AnimatorDir = "Assets/Games/Frontline/Art/Animators";

        /// <summary>Multiplier on the gate's displayed gun, so it reads from up the lane.</summary>
        const float GateGunScale = 0.9f;

        /// <summary>
        /// Every character FBX ships the *entire* weapon rack already parented to the hand
        /// bone -- all 14 guns, inside each character. So a weapon swap is SetActive on one
        /// mesh, not socket/attach-point wiring. This is most of the gate-pickup system for
        /// free. Names must match the FBX child object names exactly.
        /// </summary>
        public static readonly string[] GunMeshes = {
            "AK", "Pistol", "Revolver", "Revolver_Small", "RocketLauncher", "GrenadeLauncher",
            "Shotgun", "SMG", "Sniper", "Sniper_2", "ShortCannon", "Knife_1", "Knife_2", "Shovel"
        };

        [MenuItem("Frontline/Build Art Prefabs")]
        public static void Build()
        {
            Directory.CreateDirectory(PrefabDir);
            Directory.CreateDirectory(AnimatorDir);

            // Idle_Shoot, not Run_Gun: the soldier strafes but never advances, so a run cycle
            // would foot-skid across the lane.
            System.Action<GameObject> playerComponents = go =>
            {
                go.AddComponent<PlayerController>();
                go.AddComponent<PlayerWeapon>();
                go.AddComponent<AutoFirer>();
            };
            BuildCharacter("Character_Soldier", "Player", gun: "AK", clip: "Idle_Shoot", yaw: 0f,
                           extra: playerComponents);

            // Second playable hero (Shop unlock, see Heroes.Hazmat). Character_Hazmat.fbx sat
            // unused in the source pack -- same rig/clip set as the Soldier (both ship in the
            // Quaternius Toon Shooter Game Kit), so it's a straight reuse of BuildCharacter with
            // no format-specific handling needed.
            BuildCharacter("Character_Hazmat", "Player_Hazmat", gun: "AK", clip: "Idle_Shoot", yaw: 0f,
                           extra: playerComponents);

            // Knife: the enemy is melee, which is *why* he has to close the distance.
            // yaw 180 because he marches down -Z; without it he moonwalks into the player.
            BuildCharacter("Character_Enemy", "Enemy", gun: "Knife_1", clip: "Run", yaw: 180f,
                           extra: go => go.AddComponent<Enemy>(),
                           deathClip: "Death");

            BuildGate();
            BuildHurdle();
            BuildProjectile();
            BuildFlash();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Frontline] Art prefabs built.");
        }

        static void BuildCharacter(string fbxName, string prefabName, string gun, string clip,
                                   float yaw, System.Action<GameObject> extra, string deathClip = null)
        {
            string fbxPath = $"{ArtRoot}/Characters/{fbxName}.fbx";
            ConfigureClipLooping(fbxPath);

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (source == null) { Debug.LogError($"[Frontline] missing {fbxPath}"); return; }

            // Plain Instantiate rather than InstantiatePrefab: we want a self-contained prefab
            // we regenerate from code, not a variant chained to the FBX.
            var instance = (GameObject)Object.Instantiate(source);
            instance.name = prefabName;
            instance.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            foreach (Transform t in instance.GetComponentsInChildren<Transform>(true))
                if (GunMeshes.Contains(t.name))
                    t.gameObject.SetActive(t.name == gun);

            Animator animator = instance.GetComponent<Animator>();
            if (animator == null) animator = instance.AddComponent<Animator>();

            // A Generic rig plays nothing at all without an Avatar, and fails *silently* --
            // the model just sits in bind pose. Take the one the ModelImporter generated
            // rather than trusting whatever survived Instantiate.
            var avatar = AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<Avatar>().FirstOrDefault();
            if (avatar != null) animator.avatar = avatar;

            animator.runtimeAnimatorController = BuildController(fbxPath, prefabName, clip, deathClip);
            // Position is driven in code; root motion would fight it and drift the lane.
            animator.applyRootMotion = false;

            Debug.Log($"[Frontline] {prefabName}: avatar={(avatar != null ? avatar.name : "NULL")} " +
                      $"valid={(avatar != null && avatar.isValid)} " +
                      $"controller={(animator.runtimeAnimatorController != null)} gun={gun}");

            extra?.Invoke(instance);

            PrefabUtility.SaveAsPrefabAsset(instance, $"{PrefabDir}/{prefabName}.prefab");
            Object.DestroyImmediate(instance);
        }

        /// <summary>
        /// The bullet, as a prefab rather than a runtime GameObject.CreatePrimitive.
        ///
        /// On device this logged "Can't add component because class 'SphereCollider' doesn't
        /// exist!" every startup: managed stripping drops the physics module (nothing in our
        /// code references it -- hits are distance tests), but CreatePrimitive tries to add a
        /// collider internally regardless. Building the prefab in the editor sidesteps it
        /// entirely: the mesh becomes a plain asset reference, and no physics type is ever
        /// named at runtime.
        /// </summary>
        static void BuildProjectile()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);   // editor-side: physics exists here
            go.name = "Projectile";
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.localScale = Vector3.one * (Tuning.ProjectileRadius * 2f);

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "ProjectileMat" };
            mat.SetColor("_BaseColor", new Color(1f, 0.92f, 0.4f));
            // Emissive so the bloom actually catches it -- a lit-only bullet never crosses the
            // threshold and the tracers stop reading as tracers.
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(1f, 0.85f, 0.25f) * 2.2f);

            const string matPath = "Assets/Games/Frontline/Materials/ProjectileMat.mat";
            AssetDatabase.DeleteAsset(matPath);
            Directory.CreateDirectory("Assets/Games/Frontline/Materials");
            AssetDatabase.CreateAsset(mat, matPath);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;

            go.AddComponent<Projectile>();
            PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabDir}/Projectile.prefab");
            Object.DestroyImmediate(go);
            Debug.Log("[Frontline] Projectile prefab built.");
        }

        /// <summary>
        /// The muzzle/impact pop. A sphere whose material has emission *enabled* — that matters:
        /// keywords like _EMISSION are per-material and cannot be set from a MaterialPropertyBlock,
        /// so if it isn't switched on here, every per-instance _EmissionColor the Flash sets at
        /// runtime is silently ignored and the pops render flat.
        /// </summary>
        static void BuildFlash()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Flash";
            Object.DestroyImmediate(go.GetComponent<Collider>());

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "FlashMat" };
            mat.SetColor("_BaseColor", Color.white);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", Color.white * 4f);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            // Per-instance colour comes from the property block, so the renderer must not be
            // batched into a shared draw that would flatten them all to one colour.
            mat.enableInstancing = true;

            const string matPath = "Assets/Games/Frontline/Materials/FlashMat.mat";
            AssetDatabase.DeleteAsset(matPath);
            Directory.CreateDirectory("Assets/Games/Frontline/Materials");
            AssetDatabase.CreateAsset(mat, matPath);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            go.GetComponent<MeshRenderer>().shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;   // a spark casting a shadow looks wrong

            go.AddComponent<Flash>();
            PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabDir}/Flash.prefab");
            Object.DestroyImmediate(go);
            Debug.Log("[Frontline] Flash prefab built.");
        }

        /// <summary>
        /// A gate: a translucent panel you drive through, with the offered gun spinning in it.
        ///
        /// Carries the *whole* gun rack as inactive children and toggles one on Configure --
        /// same trick the characters use. That's what lets gates be pooled: a pooled object
        /// can't swap in a model it doesn't already own without instantiating mid-run.
        /// </summary>
        static void BuildGate()
        {
            var root = new GameObject("Gate");
            root.AddComponent<WeaponGate>();

            var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = "Panel";
            Object.DestroyImmediate(panel.GetComponent<Collider>());
            panel.transform.SetParent(root.transform, false);
            // RowItemHalfWidth, not the old GateHalfWidth (which spanned half the whole road --
            // gates now occupy one lane out of RowLaneCount, alongside hurdles and open lanes).
            panel.transform.localScale = new Vector3(Tuning.RowItemHalfWidth * 2f, Tuning.GateHeight, 0.08f);
            panel.transform.localPosition = new Vector3(0f, Tuning.GateHeight * 0.5f, 0f);
            panel.GetComponent<MeshRenderer>().sharedMaterial = BuildGatePanelMaterial();

            var mount = new GameObject("Mount");
            mount.transform.SetParent(root.transform, false);
            // In front of the panel (-Z is toward the player/camera), not inside it. Coincident
            // with the panel the gun was ambiguous against a non-ZWriting transparent surface.
            mount.transform.localPosition = new Vector3(0f, Tuning.GateHeight * 0.55f, -0.35f);

            foreach (string gun in GunMeshes)
            {
                var fbx = AssetDatabase.LoadAssetAtPath<GameObject>($"{ArtRoot}/Guns/{gun}.fbx");
                if (fbx == null) continue;
                var inst = (GameObject)Object.Instantiate(fbx, mount.transform);
                inst.name = gun;   // Configure matches on this; Instantiate would name it "AK(Clone)"
                inst.transform.localPosition = Vector3.zero;
                // MULTIPLY, never assign. These FBX are authored in centimetres, so the importer
                // bakes a ~100x scale into the model root. Assigning localScale = 1.5f wiped
                // that out and collapsed the gun to its raw authored size -- a 2cm AK that
                // rendered perfectly and was invisible at any sane camera distance.
                inst.transform.localScale *= GateGunScale;

                // Re-centre on the mount. These meshes are authored well off their pivot (the
                // AK's centre sits +0.32 along x), and the mount spins -- so left alone the gun
                // would swing round the panel like a hammer throw instead of turning on itself.
                var rend = inst.GetComponentInChildren<Renderer>();
                if (rend != null)
                    inst.transform.localPosition += inst.transform.position - rend.bounds.center;

                inst.SetActive(false);
            }

            PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/Gate.prefab");
            Object.DestroyImmediate(root);
            Debug.Log("[Frontline] Gate prefab built.");
        }

        /// <summary>
        /// A hurdle: a plain shoot-through obstacle occupying one lane. No panel, no gun --
        /// the environment prop itself IS the visual, sitting solid in the lane. Grants nothing
        /// on clearing; it exists purely to make an occupied lane worth avoiding or punching
        /// through, per Simon's "a bunch of old tires or scrap barrels... you dont get a new
        /// gun but you can shoot through it to get to soldiers behind it".
        ///
        /// Same toggle-one-of-many-children trick as the gate's gun rack, reusing the
        /// already-imported Environment props rather than pulling in new art. Picked four that
        /// measured under a lane's width (~1.2) at their native import scale, so unlike the
        /// gate's guns these are NOT rescaled -- BuildProps placed identical instances around
        /// the lane unscaled and they read fine there.
        /// </summary>
        static readonly string[] HurdleProps = { "Crate", "GasTank", "CardboardBoxes_2", "ExplodingBarrel" };

        static void BuildHurdle()
        {
            var root = new GameObject("Hurdle");
            root.AddComponent<Hurdle>();

            var mount = new GameObject("Mount");
            mount.transform.SetParent(root.transform, false);

            foreach (string prop in HurdleProps)
            {
                var fbx = AssetDatabase.LoadAssetAtPath<GameObject>($"{ArtRoot}/Environment/{prop}.fbx");
                if (fbx == null) { Debug.LogWarning($"[Frontline] missing hurdle prop {prop}.fbx"); continue; }
                var inst = (GameObject)Object.Instantiate(fbx, mount.transform);
                inst.name = prop;   // Configure matches on this, same contract as the gate's guns
                inst.transform.localPosition = Vector3.zero;
                inst.SetActive(false);
            }

            PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/Hurdle.prefab");
            Object.DestroyImmediate(root);
            Debug.Log("[Frontline] Hurdle prefab built.");
        }

        /// <summary>
        /// URP Lit doesn't go transparent by setting an alpha -- Surface/Blend/ZWrite and the
        /// keyword all have to be set by hand or you get an opaque wall you can't see the
        /// battlefield through.
        /// </summary>
        static Material BuildGatePanelMaterial()
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "GatePanelMat" };
            mat.SetFloat("_Surface", 1f);   // 0 = opaque, 1 = transparent
            mat.SetFloat("_Blend", 0f);     // alpha blend
            mat.SetFloat("_ZWrite", 0f);
            mat.SetColor("_BaseColor", new Color(0.25f, 0.85f, 0.35f, 0.42f));

            // Setting _Surface and blend floats by hand is NOT enough -- that produced a fully
            // opaque slab that hid the gun behind it. URP derives blend state, render queue and
            // keywords from _Surface, and nothing re-derives them for a material built in code,
            // so the properties were set and ignored. These are the functions URP's own material
            // inspector calls when you flip Surface Type to Transparent.
            //
            // NB: BaseShaderGUI lives in the bare UnityEditor namespace, not
            // UnityEditor.Rendering.Universal, despite shipping in the URP package.
            BaseShaderGUI.SetupMaterialBlendMode(mat);
            BaseShaderGUI.SetMaterialKeywords(mat);

            const string path = "Assets/Games/Frontline/Materials/GatePanelMat.mat";
            AssetDatabase.DeleteAsset(path);   // regenerate rather than stack up on rebuilds
            Directory.CreateDirectory("Assets/Games/Frontline/Materials");
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static AnimatorController BuildController(string fbxPath, string name, string clipName,
                                                  string deathClipName = null)
        {
            string path = $"{AnimatorDir}/{name}.controller";
            AssetDatabase.DeleteAsset(path);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);

            AnimationClip clip = FindClip(fbxPath, clipName);
            if (clip == null)
            {
                Debug.LogError($"[Frontline] no clip ending '{clipName}' in {fbxPath}");
                return controller;
            }

            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            AnimatorState state = sm.AddState(clipName);
            state.motion = clip;
            sm.defaultState = state;
            Debug.Log($"[Frontline]   clip '{clip.name}' len={clip.length:F2}s loop={clip.isLooping}");

            if (deathClipName != null)
            {
                AnimationClip death = FindClip(fbxPath, deathClipName);
                if (death == null)
                {
                    Debug.LogError($"[Frontline] no death clip '{deathClipName}' in {fbxPath}");
                    return controller;
                }

                controller.AddParameter(Enemy.DieTrigger, AnimatorControllerParameterType.Trigger);
                AnimatorState deathState = sm.AddState(deathClipName);
                deathState.motion = death;

                AnimatorStateTransition t = state.AddTransition(deathState);
                t.AddCondition(AnimatorConditionMode.If, 0f, Enemy.DieTrigger);
                // No exit time: dying has to happen on the bullet, not at the end of the run
                // cycle. A short blend keeps it from snapping.
                t.hasExitTime = false;
                t.duration = 0.04f;

                Debug.Log($"[Frontline]   death '{death.name}' len={death.length:F2}s loop={death.isLooping}");
            }

            return controller;
        }

        /// <summary>
        /// Clips are named "CharacterArmature|Idle_Shoot"; match on the suffix. EndsWith and not
        /// Contains, so "Run" doesn't also catch "Run_Shoot"/"Run_Gun".
        /// </summary>
        static AnimationClip FindClip(string fbxPath, string clipName) =>
            AssetDatabase.LoadAllAssetsAtPath(fbxPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview") && c.name.EndsWith(clipName));

        static void ConfigureClipLooping(string fbxPath)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(fbxPath);

            // Spell the rig out. Relying on the import default left the FBX with no Avatar
            // sub-asset at all, and a Generic Animator with a null avatar plays nothing while
            // reporting no error -- the character just stands in bind pose looking "imported".
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;

            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            for (int i = 0; i < clips.Length; i++)
                clips[i].loopTime = !clips[i].name.Contains("Death");  // death holds its last pose
            importer.clipAnimations = clips;
            importer.SaveAndReimport();

            string kinds = string.Join(", ", AssetDatabase.LoadAllAssetsAtPath(fbxPath)
                .Where(a => a != null)
                .Select(a => a.GetType().Name).Distinct());
            Debug.Log($"[Frontline] {Path.GetFileName(fbxPath)} sub-assets: {kinds}");
        }

        /// <summary>
        /// Renders each built prefab from the front, posed at its animation clip, straight to
        /// a PNG. The in-game camera looks down the lane from behind at 42 degrees, which
        /// foreshortens everything and makes "forward" project as "up the screen" -- useless
        /// for judging whether a character actually holds his gun properly. This is the art
        /// equivalent of AutoPilot: turn a thing-that-must-be-looked-at into a file.
        ///
        ///   Unity.exe -batchmode -quit -projectPath . -executeMethod Frontline.EditorTools.ArtImporter.Preview
        /// </summary>
        [MenuItem("Frontline/Preview Art Prefabs")]
        public static void Preview()
        {
            foreach (string name in new[] { "Player", "Player_Hazmat", "Enemy" })
                RenderPreview(name);
            RenderPreview("Gate", gateGun: "AK");
        }

        static void RenderPreview(string prefabName, string gateGun = null)
        {
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                UnityEditor.SceneManagement.NewSceneMode.Single);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/{prefabName}.prefab");
            if (prefab == null) { Debug.LogError($"[Frontline] no {prefabName}.prefab"); return; }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            // The gate's Awake never runs in edit mode, so do by hand what Configure would do
            // at runtime: switch on the offered gun.
            if (gateGun != null)
            {
                Transform mount = go.transform.Find("Mount");
                if (mount == null) { Debug.LogError("[Frontline] gate preview: no Mount"); }
                else
                {
                    foreach (Transform t in mount)
                        t.gameObject.SetActive(t.name == gateGun);

                    var names = new System.Text.StringBuilder();
                    foreach (Transform t in mount)
                        names.Append($"{t.name}:{(t.gameObject.activeSelf ? "ON" : "off")} ");
                    Debug.Log($"[Frontline] gate mount children={mount.childCount} {names}");

                    Transform g = mount.Find(gateGun);
                    if (g != null)
                    {
                        var rs = g.GetComponentsInChildren<Renderer>(true);
                        Debug.Log($"[Frontline] gun '{gateGun}' worldPos={g.position} scale={g.lossyScale} " +
                                  $"renderers={rs.Length} enabled={(rs.Length > 0 && rs[0].enabled)} " +
                                  $"activeInHierarchy={g.gameObject.activeInHierarchy}");
                        if (rs.Length > 0)
                            Debug.Log($"[Frontline] gun bounds center={rs[0].bounds.center} size={rs[0].bounds.size} " +
                                      $"mesh={(rs[0] as MeshRenderer)?.GetComponent<MeshFilter>()?.sharedMesh?.name}");
                    }
                }
            }

            // Pose it. Animators don't tick in an editor batch, so sample the clip by hand --
            // otherwise this would render the bind pose and prove nothing.
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                $"{AnimatorDir}/{prefabName}.controller");
            AnimationClip clip = controller != null ? controller.animationClips.FirstOrDefault() : null;
            if (clip != null) clip.SampleAnimation(go, clip.length * 0.5f);

            var lightGo = new GameObject("Sun");
            Light sun = lightGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            lightGo.transform.rotation = Quaternion.Euler(35f, 200f, 0f);

            var camGo = new GameObject("PreviewCam");
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.17f, 0.21f);
            cam.fieldOfView = 35f;
            // Three-quarter front view: shows the weapon in the hand and the facing direction.
            // The gate is a 3-unit-wide panel, so it needs backing off and centring higher.
            if (gateGun != null)
            {
                // From -Z, the side the player actually approaches from. The first version shot
                // this from +Z and photographed the *back* of the gate: the gun is mounted on
                // the player-facing face, so it was hidden behind the panel and the preview
                // looked like a bug that wasn't there.
                camGo.transform.position = new Vector3(2.2f, 2.1f, -5.5f);
                camGo.transform.LookAt(new Vector3(0f, 1.2f, 0f));
            }
            else
            {
                camGo.transform.position = new Vector3(2.6f, 1.4f, 3.2f);
                camGo.transform.LookAt(new Vector3(0f, 0.85f, 0f));
            }

            const int W = 600, H = 800;
            var rt = new RenderTexture(W, H, 24);
            cam.targetTexture = rt;
            // Twice on purpose. The first render in a batchmode session comes back untextured
            // grey -- URP isn't warm yet -- which made the first previewed character look like
            // its materials had failed to import when they were fine.
            cam.Render();
            cam.Render();

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = null;

            Directory.CreateDirectory("Shots");
            string outPath = $"Shots/preview_{prefabName}.png";
            File.WriteAllBytes(outPath, tex.EncodeToPNG());
            Debug.Log($"[Frontline] preview -> {outPath} (clip '{(clip != null ? clip.name : "none")}')");
        }

        /// <summary>
        /// Marketing/store key art: both heroes posed with a heavier weapon than they ever
        /// actually start with, a couple of dropped enemies at their feet, and a few
        /// Environment props for a battlefield instead of void. Same "instantiate, pose by
        /// hand, render to a RenderTexture, write PNG" recipe as RenderPreview -- just a wider
        /// scene instead of one character on a solid backdrop. First-attempt framing: expect to
        /// re-run this with different numbers once Simon has seen a render.
        ///
        ///   Unity.exe -batchmode -quit -projectPath . -executeMethod Frontline.EditorTools.ArtImporter.RenderKeyArt
        /// </summary>
        [MenuItem("Frontline/Render Key Art")]
        public static void RenderKeyArt()
        {
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                UnityEditor.SceneManagement.NewSceneMode.Single);

            // ---- ground ----
            var dirt = GameObject.CreatePrimitive(PrimitiveType.Plane);
            dirt.name = "Ground";
            Object.DestroyImmediate(dirt.GetComponent<Collider>());
            dirt.transform.localScale = new Vector3(1.4f, 1f, 1.4f);
            dirt.GetComponent<MeshRenderer>().sharedMaterial =
                AssetDatabase.LoadAssetAtPath<Material>("Assets/Games/Frontline/Materials/GroundMat.mat");

            // ---- heroes -- closer together than attempt 5 but backed off from attempt 6's 0.8,
            // which overlapped both of them ----
            GameObject soldier = SpawnPosedCharacter("Player", new Vector3(-0.95f, 0f, 0f), "Sniper");
            GameObject hazmat = SpawnPosedCharacter("Player_Hazmat", new Vector3(0.95f, 0f, 0f), "RocketLauncher");

            // ---- exactly two dropped enemies (Simon: "only two... as close as possible
            // without touching"), pulled in tighter still -- yawed away from each other so
            // limbs don't reach across the gap ----
            GameObject enemyL = SpawnDeadEnemy(new Vector3(-1.05f, 0f, 1.6f), 35f);
            GameObject enemyR = SpawnDeadEnemy(new Vector3(1.05f, 0f, 1.6f), -145f);

            // ---- background. All three were lying on their sides (RenderTiltTest, see
            // store-assets/keyart/tilt_test.png caught it from a raised angle -- the flat
            // top-down/orthographic yaw tests couldn't tell "facing the camera" from "facing
            // the camera while tipped over on its back", which is exactly what was wrong with
            // the tank's barrel). All three needed the same fix: +90 pitch to stand them
            // upright, applied in local space before yaw (Quaternion.Euler applies Z, X, Y in
            // that order -- pitch happens first, then yaw spins the now-upright model to face
            // wherever it needs to on the ground). Re-ran the yaw test with the pitch fix in
            // place to find yaw ~0 faces the camera for all three, once they're standing up.
            //
            // Screen left/right is the OPPOSITE of world +x/-x from this camera (verified by
            // checking which hero -- spawned at known x -- actually rendered on which side), so
            // "wall on screen-left" needs positive x, not negative.
            GameObject wall = SpawnProp("BrickWall_2", new Vector3(2.4f, 0.7f, -2.0f), 0f, 90f);
            // Yaw 15, not a dead 0 -- "slightly at an angle" per Simon, only for the tank.
            GameObject tank = SpawnProp("Tank", new Vector3(0f, 0.7f, -3.0f), 15f, 90f);
            GameObject car = SpawnProp("Debris_BrokenCar", new Vector3(-2.6f, 0.7f, -3.4f), 0f, 90f);

            // Tank green, per Simon -- multiplying _BaseColor rather than replacing it outright
            // (see ReportEnemyMaterials' doc comment: flat colour replacements have broken this
            // art pack's shading twice before), so the model's own baked shading/AO survives.
            var tankGreen = new Color(0.30f, 0.42f, 0.24f);
            foreach (Renderer r in tank.GetComponentsInChildren<Renderer>(true))
                r.material.SetColor("_BaseColor", tankGreen);

            CheckOverlaps(
                ("Soldier", soldier), ("Hazmat", hazmat),
                ("EnemyL", enemyL), ("EnemyR", enemyR),
                ("BrickWall", wall), ("Tank", tank), ("Car", car));

            // ---- lighting: warm key light + a dim cool fill so the shadow side isn't pure black ----
            var sunGo = new GameObject("Sun");
            Light sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.92f, 0.78f);
            sun.intensity = 1.5f;
            sunGo.transform.rotation = Quaternion.Euler(38f, 145f, 0f);

            var fillGo = new GameObject("Fill");
            Light fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.55f, 0.62f, 0.75f);
            fill.intensity = 0.4f;
            fillGo.transform.rotation = Quaternion.Euler(30f, -60f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.35f, 0.3f, 0.28f);

            // ---- camera: pulled back and slightly high, low-angle "hero shot" ----
            var camGo = new GameObject("KeyArtCam");
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            // Same sunset family CanvasBuilder's menu backdrop uses, so this reads as one brand
            // even before a real gradient/sky is composited behind it.
            cam.backgroundColor = new Color(0.62f, 0.30f, 0.17f);
            cam.fieldOfView = 36f;
            // Closer than attempt 5, to match the tighter cluster -- everyone's nearer the
            // centre now, so this can zoom in without cropping anyone.
            camGo.transform.position = new Vector3(0f, 1.9f, 7.0f);
            camGo.transform.LookAt(new Vector3(0f, 0.95f, -0.1f));

            const int W = 1400, H = 1400;
            var rt = new RenderTexture(W, H, 24);
            cam.targetTexture = rt;
            cam.Render();
            cam.Render();   // first render in a fresh batchmode session comes back untextured

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = null;

            Directory.CreateDirectory("store-assets/keyart");
            string outPath = "store-assets/keyart/attempt9.png";
            File.WriteAllBytes(outPath, tex.EncodeToPNG());
            Debug.Log($"[Frontline] key art -> {outPath}");
        }

        /// <summary>
        /// "Check nothing is going through anything else" -- pairwise world-space renderer
        /// bounds against every other spawned piece, logged as a warning per overlapping pair.
        /// Pixel-eyeballing a 1400px render missed the enemy-through-soldier clip that prompted
        /// this in the first place; a bounds check catches it even where the render doesn't make
        /// it obvious (limbs behind another mesh, etc).
        /// </summary>
        static void CheckOverlaps(params (string name, GameObject go)[] objects)
        {
            var bounds = new List<(string name, Bounds b)>();
            foreach (var (name, go) in objects)
            {
                // Excludes the held weapon: a rifle barrel's bounding box reaching over a nearby
                // corpse isn't what "going through" means, and every character's gun is long
                // enough that including it made this warn on nearly every pair regardless of
                // whether the actual bodies were anywhere near each other.
                Bounds? b = null;
                foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
                {
                    bool underGun = false;
                    for (Transform t = r.transform; t != null && t != go.transform; t = t.parent)
                        if (GunMeshes.Contains(t.name)) { underGun = true; break; }
                    if (underGun) continue;
                    if (b == null) b = r.bounds;
                    else { Bounds bb = b.Value; bb.Encapsulate(r.bounds); b = bb; }
                }
                if (b != null) bounds.Add((name, b.Value));
            }

            bool any = false;
            for (int i = 0; i < bounds.Count; i++)
            for (int j = i + 1; j < bounds.Count; j++)
                if (bounds[i].b.Intersects(bounds[j].b))
                {
                    any = true;
                    Debug.LogWarning($"[Frontline] KEY ART OVERLAP: {bounds[i].name} {bounds[i].b} <-> {bounds[j].name} {bounds[j].b}");
                }
            if (!any) Debug.Log("[Frontline] key art: no bounding-box overlaps between any spawned piece.");
        }

        /// <summary>
        /// One-off: 4 yaws x 3 props laid out in a grid, all facing the same camera, so the
        /// right "faces the camera" rotation for each prop can be read off a single render
        /// instead of guessed one full-scene render at a time.
        ///
        ///   Unity.exe -batchmode -quit -projectPath . -executeMethod Frontline.EditorTools.ArtImporter.RenderRotationTest
        /// </summary>
        /// <summary>
        /// Each of the 3 background props, unrotated, from a raised three-quarter angle -- the
        /// flat orthographic front-on test could tell facing (left/right) apart but couldn't
        /// tell "pointing at the camera" from "pointing at the floor", which is exactly the bug
        /// Simon caught (the tank's barrel). This looks from above and to the side specifically
        /// so a downward-pointing barrel is obviously downward instead of foreshortened away.
        ///
        ///   Unity.exe -batchmode -quit -projectPath . -executeMethod Frontline.EditorTools.ArtImporter.RenderTiltTest
        /// </summary>
        [MenuItem("Frontline/Render Key Art Tilt Test")]
        public static void RenderTiltTest()
        {
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                UnityEditor.SceneManagement.NewSceneMode.Single);

            var dirt = GameObject.CreatePrimitive(PrimitiveType.Plane);
            dirt.transform.localScale = new Vector3(2f, 1f, 2f);
            dirt.GetComponent<MeshRenderer>().sharedMaterial =
                AssetDatabase.LoadAssetAtPath<Material>("Assets/Games/Frontline/Materials/GroundMat.mat");

            string[] props = { "Debris_BrokenCar", "Tank", "BrickWall_2" };
            for (int i = 0; i < props.Length; i++)
                SpawnProp(props[i], new Vector3(i * 6f - 6f, 0.7f, 0f), 0f, 90f);

            var sunGo = new GameObject("Sun");
            Light sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.4f;
            sunGo.transform.rotation = Quaternion.Euler(45f, 150f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.4f, 0.4f, 0.4f);

            var camGo = new GameObject("Cam");
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.fieldOfView = 45f;
            // Raised and pulled back, aimed level-ish at the row -- high enough that a
            // downward-pointing barrel reads as clearly downward, not head-on-foreshortened.
            camGo.transform.position = new Vector3(0f, 4.5f, 9f);
            camGo.transform.LookAt(new Vector3(0f, 0.5f, 0f));

            const int W = 1800, H = 900;
            var rt = new RenderTexture(W, H, 24);
            cam.targetTexture = rt;
            cam.Render();
            cam.Render();
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = null;

            Directory.CreateDirectory("store-assets/keyart");
            File.WriteAllBytes("store-assets/keyart/tilt_test.png", tex.EncodeToPNG());
            Debug.Log("[Frontline] tilt test -> store-assets/keyart/tilt_test.png " +
                      "(left to right: Debris_BrokenCar, Tank, BrickWall_2, all yaw=0)");
        }

        [MenuItem("Frontline/Render Key Art Rotation Test")]
        public static void RenderRotationTest()
        {
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                UnityEditor.SceneManagement.NewSceneMode.Single);

            var dirt = GameObject.CreatePrimitive(PrimitiveType.Plane);
            dirt.transform.localScale = new Vector3(6f, 1f, 2f);
            dirt.GetComponent<MeshRenderer>().sharedMaterial =
                AssetDatabase.LoadAssetAtPath<Material>("Assets/Games/Frontline/Materials/GroundMat.mat");

            // All 12 (3 props x 4 yaws) in a single line along x, same y/z -- an orthographic
            // camera looking dead along -z projects x/y linearly regardless of depth, so unlike
            // the first attempt (a wide perspective grid, where the outer columns picked up
            // apparent skew just from being off to the side of the camera) nothing here can be
            // mistaken for a rotation that isn't really there.
            string[] props = { "Debris_BrokenCar", "Tank", "BrickWall_2" };
            float[] yaws = { 0f, 90f, 180f, 270f };
            int slot = 0;
            foreach (string prop in props)
                foreach (float yaw in yaws)
                    SpawnProp(prop, new Vector3(slot++ * 4f - 22f, 0.7f, 0f), yaw, 90f);

            var sunGo = new GameObject("Sun");
            Light sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.4f;
            sunGo.transform.rotation = Quaternion.Euler(45f, 150f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.4f, 0.4f, 0.4f);

            var camGo = new GameObject("Cam");
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.orthographic = true;
            cam.orthographicSize = 3.8f;
            camGo.transform.position = new Vector3(0f, 1.2f, 12f);
            camGo.transform.rotation = Quaternion.LookRotation(Vector3.back);   // dead level, no tilt

            const int W = 3200, H = 500;
            var rt = new RenderTexture(W, H, 24);
            cam.targetTexture = rt;
            cam.Render();
            cam.Render();
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = null;

            Directory.CreateDirectory("store-assets/keyart");
            File.WriteAllBytes("store-assets/keyart/rotation_test.png", tex.EncodeToPNG());
            Debug.Log("[Frontline] rotation test -> store-assets/keyart/rotation_test.png " +
                      "(rows: BrickWall_2, Tank, Debris_BrokenCar; columns: yaw 0/90/180/270)");
        }

        static GameObject SpawnPosedCharacter(string prefabName, Vector3 pos, string gun)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/{prefabName}.prefab");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetPositionAndRotation(pos, Quaternion.identity);

            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                if (GunMeshes.Contains(t.name))
                    t.gameObject.SetActive(t.name == gun);

            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>($"{AnimatorDir}/{prefabName}.controller");
            AnimationClip clip = controller != null ? controller.animationClips.FirstOrDefault() : null;
            if (clip != null) clip.SampleAnimation(go, clip.length * 0.5f);

            return go;
        }

        static GameObject SpawnDeadEnemy(Vector3 pos, float yaw)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/Enemy.prefab");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));

            // A corpse gripping an upright knife reads like a tombstone, not a body -- the
            // Enemy prefab ships with Knife_1 active (its melee weapon), so switch it off
            // explicitly rather than leaving whatever BuildCharacter last enabled.
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                if (GunMeshes.Contains(t.name))
                    t.gameObject.SetActive(false);

            AnimationClip death = FindClip($"{ArtRoot}/Characters/Character_Enemy.fbx", "Death");
            if (death != null) death.SampleAnimation(go, death.length);
            return go;
        }

        static GameObject SpawnProp(string propName, Vector3 pos, float yaw, float pitch = 0f)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>($"{ArtRoot}/Environment/{propName}.fbx");
            if (fbx == null) { Debug.LogWarning($"[Frontline] key art: missing prop {propName}"); return null; }
            var go = (GameObject)Object.Instantiate(fbx);
            go.name = propName;
            // Euler applies Z, then X (pitch), then Y (yaw) -- the pitch correction happens in
            // the model's own local space before yaw swings the (now upright) object to face
            // wherever it needs to on the ground.
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(pitch, yaw, 0f));
            return go;
        }

        /// <summary>
        /// One-off diagnostic: what shader/colour does each of the Enemy prefab's renderers
        /// actually carry? Written after two guesses at material tinting both broke the
        /// character's appearance (first solid white, then solid black) -- reading the real
        /// values beats guessing a third time.
        /// </summary>
        [MenuItem("Frontline/Report Enemy Materials")]
        public static void ReportEnemyMaterials()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/Enemy.prefab");
            if (prefab == null) { Debug.LogError("[Frontline] no Enemy.prefab"); return; }

            foreach (Renderer r in prefab.GetComponentsInChildren<Renderer>(true))
            {
                Material mat = r.sharedMaterial;
                if (mat == null) { Debug.Log($"[Frontline] {r.name}: NULL material"); continue; }

                string baseColor = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor").ToString() : "n/a";
                string legacyColor = mat.HasProperty("_Color") ? mat.GetColor("_Color").ToString() : "n/a";
                bool hasVertexColor = r is SkinnedMeshRenderer smr && smr.sharedMesh != null && smr.sharedMesh.colors != null && smr.sharedMesh.colors.Length > 0;

                Debug.Log($"[Frontline] {r.name,-20} shader={mat.shader.name,-40} " +
                          $"_BaseColor={baseColor} _Color={legacyColor} vertexColors={hasVertexColor}");
            }
        }

        [MenuItem("Frontline/Report Imported Art")]
        public static void Report()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[Frontline] ---- ART REPORT ----");

            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { ArtRoot });
            foreach (string guid in guids.OrderBy(g => AssetDatabase.GUIDToAssetPath(g)))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                Object[] all = AssetDatabase.LoadAllAssetsAtPath(path);

                // Unity generates hidden __preview__ clips; they aren't real content.
                string[] clips = all.OfType<AnimationClip>()
                    .Select(c => c.name)
                    .Where(n => !n.StartsWith("__preview"))
                    .ToArray();
                string[] meshes = all.OfType<Mesh>().Select(m => m.name).ToArray();
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                bool skinned = go != null && go.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length > 0;

                sb.AppendLine($"[Frontline] {path}");
                sb.AppendLine($"[Frontline]    rig={importer?.animationType} skinned={skinned} meshes={meshes.Length} scaleFactor={importer?.globalScale}");
                sb.AppendLine($"[Frontline]    clips({clips.Length}): {string.Join(" | ", clips)}");

                if (go != null)
                {
                    // World-space size of the imported model. Needed before anything can be
                    // placed: the grey-box is built around a 0.8-unit capsule and these have
                    // to sit in the same lane.
                    Bounds b = new Bounds();
                    bool first = true;
                    foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
                    {
                        Bounds rb = r is SkinnedMeshRenderer smr && smr.sharedMesh != null
                            ? new Bounds(smr.sharedMesh.bounds.center, smr.sharedMesh.bounds.size)
                            : r.bounds;
                        if (first) { b = rb; first = false; } else b.Encapsulate(rb);
                    }
                    sb.AppendLine($"[Frontline]    size={b.size} center={b.center}");
                    sb.AppendLine($"[Frontline]    meshNames: {string.Join(", ", meshes)}");

                    // Which bone each weapon hangs off, and where it sits. If the AK rides a
                    // different bone than the knife, that explains a gun floating behind the head.
                    foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                    {
                        if (!GunMeshes.Contains(t.name)) continue;
                        string chain = "";
                        for (Transform p = t.parent; p != null && p != go.transform; p = p.parent)
                            chain = p.name + "/" + chain;
                        sb.AppendLine($"[Frontline]      gun {t.name,-16} under {chain} localPos={t.localPosition}");
                    }
                }
            }

            sb.AppendLine($"[Frontline] ---- {guids.Length} models ----");
            Debug.Log(sb.ToString());
        }
    }
}
