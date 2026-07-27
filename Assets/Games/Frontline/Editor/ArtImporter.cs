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
        const string ArtRoot = "Assets/Art";
        const string PrefabDir = "Assets/Art/Prefabs";
        const string AnimatorDir = "Assets/Art/Animators";

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

            const string matPath = "Assets/Materials/ProjectileMat.mat";
            AssetDatabase.DeleteAsset(matPath);
            Directory.CreateDirectory("Assets/Materials");
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

            const string matPath = "Assets/Materials/FlashMat.mat";
            AssetDatabase.DeleteAsset(matPath);
            Directory.CreateDirectory("Assets/Materials");
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

            const string path = "Assets/Materials/GatePanelMat.mat";
            AssetDatabase.DeleteAsset(path);   // regenerate rather than stack up on rebuilds
            Directory.CreateDirectory("Assets/Materials");
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
