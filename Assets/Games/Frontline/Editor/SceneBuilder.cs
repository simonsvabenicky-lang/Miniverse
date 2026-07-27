using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Frontline.EditorTools
{
    /// <summary>
    /// Builds Main.unity from scratch, in code. This is the whole reason the project can
    /// be driven headlessly: the scene is a build artifact, not something hand-assembled
    /// in the editor, so it can always be regenerated from source and never drifts.
    ///
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod Frontline.EditorTools.SceneBuilder.Build
    /// </summary>
    public static class SceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string ProfilePath = "Assets/Settings/FrontlineVolume.asset";
        const string PrefabDir = "Assets/Art/Prefabs";
        const string EnvironmentDir = "Assets/Art/Environment";
        const string AudioDir = "Assets/Audio";
        const string GunAudioDir = "Assets/Audio/Guns";

        // The dirt strip's width, derived rather than hardcoded so props keep clearing the lane
        // if it's ever retuned. Unity's Plane primitive is 10 units across at scale 1.
        const float GroundScaleX = 0.28f;
        static float DirtHalfWidth => Tuning.LaneHalfWidth * GroundScaleX * 5f;

        [MenuItem("Frontline/Rebuild Main Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- Terrain: everything the camera can see ---
            // The lane strip alone is 8.4 units wide, but perspective means the camera sees far
            // wider than that near the horizon -- so the frame had black voids in the top
            // corners where the world simply stopped. Cheapest possible fix: a big plane under
            // everything, so "outside the lane" reads as ground rather than nothing.
            GameObject terrain = GameObject.CreatePrimitive(PrimitiveType.Plane);
            terrain.name = "Terrain";
            Object.DestroyImmediate(terrain.GetComponent<Collider>());
            terrain.transform.localScale = new Vector3(8f, 1f, 8f);   // plane is 10x10 at scale 1
            terrain.transform.position = new Vector3(0f, -0.02f, 14f); // just under the lane, no z-fight
            terrain.GetComponent<MeshRenderer>().sharedMaterial =
                MakeMaterial("TerrainMat", new Color(0.28f, 0.40f, 0.17f));  // grass

            // --- Ground: a long strip so the lane reads as a corridor ---
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            Object.DestroyImmediate(ground.GetComponent<Collider>());
            // Plane primitive is 10x10 units at scale 1.
            ground.transform.localScale = new Vector3(Tuning.LaneHalfWidth * GroundScaleX, 1f, 5f);
            ground.transform.position = new Vector3(0f, 0f, 14f);
            // Dirt, against the grass. Doubles as the lane boundary -- the player can now see
            // where he's allowed to move, which the old uniform olive never showed.
            ground.GetComponent<MeshRenderer>().sharedMaterial =
                MakeMaterial("GroundMat", new Color(0.44f, 0.36f, 0.26f));

            // --- Player ---
            // The real soldier if ArtImporter has built him, else the original grey-box
            // capsule. The fallback isn't ceremony: it keeps the project runnable from a
            // clean checkout even if the CC0 art is ever missing.
            var playerArt = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/Player.prefab");
            GameObject player;
            if (playerArt != null)
            {
                player = (GameObject)PrefabUtility.InstantiatePrefab(playerArt);
                player.transform.position = new Vector3(0f, 0f, Tuning.PlayerZ);  // model pivot is at the feet

                // Always spawns the Soldier (playerArt above); HeroSpawner swaps it for whichever
                // hero SaveData.EquippedHero actually names, at Awake. Every hero prefab is baked
                // in here rather than loaded by path at runtime -- see HeroSpawner's own remarks.
                var heroSpawner = player.AddComponent<HeroSpawner>();
                var heroSo = new SerializedObject(heroSpawner);
                var heroPrefabsProp = heroSo.FindProperty("_heroPrefabs");
                heroPrefabsProp.arraySize = Heroes.All.Length;
                for (int i = 0; i < Heroes.All.Length; i++)
                {
                    var heroPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/{Heroes.All[i].PrefabName}.prefab");
                    heroPrefabsProp.GetArrayElementAtIndex(i).objectReferenceValue = heroPrefab;
                    if (heroPrefab == null)
                        Debug.LogWarning($"[Frontline] No {Heroes.All[i].PrefabName}.prefab — {Heroes.All[i].DisplayName} can't be equipped yet.");
                }
                heroSo.ApplyModifiedProperties();
            }
            else
            {
                Debug.LogWarning("[Frontline] No Player.prefab — falling back to the grey-box capsule.");
                player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                player.name = "Player";
                Object.DestroyImmediate(player.GetComponent<Collider>());
                player.transform.position = new Vector3(0f, Tuning.PlayerRadius * 2f, Tuning.PlayerZ);
                player.transform.localScale = Vector3.one * (Tuning.PlayerRadius * 2f);
                player.GetComponent<MeshRenderer>().sharedMaterial =
                    MakeMaterial("PlayerMat", new Color(0.35f, 0.62f, 0.9f));
                player.AddComponent<PlayerController>();
                player.AddComponent<AutoFirer>();
            }

            // --- Camera ---
            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            // Sky, not void. Only shows above the terrain's far edge now, but near-black there
            // made the horizon look like a rendering failure.
            cam.backgroundColor = new Color(0.42f, 0.55f, 0.68f);
            cam.fieldOfView = 55f;
            camGo.transform.position = new Vector3(0f, Tuning.CamHeight, Tuning.CamAnchorZ - Tuning.CamBack);
            camGo.transform.rotation = Quaternion.Euler(Tuning.CamPitch, 0f, 0f);
            camGo.AddComponent<CameraRig>().target = player.transform;

            // Without this the game is completely silent and says nothing about it.
            //
            // Unity's default "Main Camera" prefab carries an AudioListener; a camera built in
            // code from new GameObject() + AddComponent<Camera>() does not. No listener means
            // no audio at all -- every AudioSource plays into the void, no error in a player
            // build. Same family as the missing ambient light: a generated scene silently lacks
            // what the template would have handed you for free.
            camGo.AddComponent<AudioListener>();

            // Post-processing is per-camera opt-in in URP and defaults to OFF, which is why
            // the game rendered flat: the effects weren't broken, they were never asked for.
            UniversalAdditionalCameraData camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;
            // FXAA over SMAA/TAA: cheapest of the three and we're aiming at mid-range Android.
            // Untextured primitives are all hard silhouette edges, so it earns its keep here.
            camData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;

            // --- Post-processing volume ---
            GameObject volGo = new GameObject("Global Volume");
            Volume volume = volGo.AddComponent<Volume>();
            volume.isGlobal = true;   // global = no collider needed, applies everywhere
            volume.sharedProfile = BuildVolumeProfile();

            // --- Light ---
            GameObject lightGo = new GameObject("Sun");
            Light sun = lightGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.5f;
            sun.color = new Color(1f, 0.95f, 0.85f);
            sun.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(48f, -28f, 0f);

            // Ambient, set explicitly -- this is the single biggest thing standing between the
            // game and the reference art, and it was invisible.
            //
            // A scene defaults to AmbientMode.Skybox, which needs a baked ambient probe. This
            // scene is generated empty in batchmode and never gets a lighting bake, so ambient
            // resolved to ~black: every surface was lit by the sun alone and every shadowed
            // face fell to nothing. It read as "the colour grade is too dark", but no exposure
            // value fixes absent light -- it only blows out the lit side to compensate.
            // Trilight is computed, not baked, so it survives headless generation.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.42f, 0.48f, 0.58f);      // cool light from above
            RenderSettings.ambientEquatorColor = new Color(0.34f, 0.35f, 0.34f);
            RenderSettings.ambientGroundColor = new Color(0.20f, 0.18f, 0.14f);   // warm bounce off the dirt
            RenderSettings.skybox = null;   // camera clears to a solid colour; nothing to render

            // --- Props ---
            BuildProps();

            // --- Systems ---
            GameObject systems = new GameObject("Systems");
            GameManager gm = systems.AddComponent<GameManager>();
            BuildAudio(systems.AddComponent<Audio>());
            systems.AddComponent<EnemySpawner>();
            systems.AddComponent<GateSpawner>();
            systems.AddComponent<GameUI>();   // menu / pause / settings shell
            systems.AddComponent<AutoPilot>();   // inert unless -autopilot is passed

            // Real uGUI/TextMeshPro, Kenney-styled -- the Main Menu specifically. Everything
            // else (Pause, Settings, HUD, Death Screen) is still GameUI's IMGUI.
            CanvasBuilder.Build();

            // Hand the enemy prefab to the pool. Done through SerializedObject because the
            // field is private -- the point is that nothing here is dragged into an Inspector,
            // not that the field should be public to the whole game.
            var gmSo = new SerializedObject(gm);

            var enemyArt = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/Enemy.prefab");
            if (enemyArt != null)
                gmSo.FindProperty("_enemyPrefab").objectReferenceValue = enemyArt.GetComponent<Enemy>();
            else
                Debug.LogWarning("[Frontline] No Enemy.prefab — pool falls back to grey-box capsules.");

            var gateArt = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/Gate.prefab");
            if (gateArt != null)
                gmSo.FindProperty("_gatePrefab").objectReferenceValue = gateArt.GetComponent<WeaponGate>();
            else
                Debug.LogWarning("[Frontline] No Gate.prefab — no weapon gates will spawn.");

            var hurdleArt = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/Hurdle.prefab");
            if (hurdleArt != null)
                gmSo.FindProperty("_hurdlePrefab").objectReferenceValue = hurdleArt.GetComponent<Hurdle>();
            else
                Debug.LogWarning("[Frontline] No Hurdle.prefab — occupied non-gate lanes stay empty.");

            var projArt = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/Projectile.prefab");
            if (projArt != null)
                gmSo.FindProperty("_projectilePrefab").objectReferenceValue = projArt.GetComponent<Projectile>();
            else
                Debug.LogWarning("[Frontline] No Projectile.prefab — falling back to a runtime primitive.");

            var flashArt = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/Flash.prefab");
            if (flashArt != null)
                gmSo.FindProperty("_flashPrefab").objectReferenceValue = flashArt.GetComponent<Flash>();
            else
                Debug.LogWarning("[Frontline] No Flash.prefab — no muzzle or impact flashes.");

            gmSo.ApplyModifiedProperties();

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();

            Debug.Log($"[Frontline] Scene rebuilt -> {ScenePath}");
        }

        /// <summary>
        /// The "decent graphics" recipe, generated like everything else so it stays in source
        /// rather than becoming a .asset only reachable through the Inspector.
        /// Bloom + ACES + a slight grade and vignette is most of what reads as "polished".
        /// </summary>
        static VolumeProfile BuildVolumeProfile()
        {
            // Regenerated from scratch each time, same contract as the scene: edit the C#,
            // not the asset.
            AssetDatabase.DeleteAsset(ProfilePath);
            Directory.CreateDirectory("Assets/Settings");

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);

            var tonemapping = AddOverride<Tonemapping>(profile);
            tonemapping.mode.Override(TonemappingMode.ACES);

            var bloom = AddOverride<Bloom>(profile);
            // Threshold up with the ambient: a brighter scene pushes far more of the frame past
            // the old 0.8 line, and bloom that catches everything is just haze.
            bloom.intensity.Override(0.8f);
            bloom.threshold.Override(1.0f);   // really only the bullets and hot highlights
            bloom.scatter.Override(0.6f);

            var grade = AddOverride<ColorAdjustments>(profile);
            // Down from +0.6 to +0.3, and that's a fix, not a reversal. The +0.6 was propping
            // up a scene with no ambient light; with Trilight ambient in SceneBuilder doing
            // that job properly, the same exposure now overexposes. Saturation likewise: 15
            // was pushing grey capsules, and the Quaternius art arrives saturated already.
            grade.postExposure.Override(0.3f);
            grade.contrast.Override(8f);
            grade.saturation.Override(8f);

            var vignette = AddOverride<Vignette>(profile);
            // 0.15, down from 0.28. Against an already-dark background a strong vignette just
            // reads as grime rather than focus.
            vignette.intensity.Override(0.15f);
            vignette.smoothness.Override(0.5f);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        /// <summary>
        /// Adds a volume component and, crucially, parents it into the profile asset --
        /// without AddObjectToAsset the component is a loose ScriptableObject that vanishes
        /// on reload and the effect silently does nothing.
        /// </summary>
        static T AddOverride<T>(VolumeProfile profile) where T : VolumeComponent
        {
            // Add(false): only the parameters explicitly Override()n below apply, so we don't
            // silently pin every default in the effect.
            T component = profile.Add<T>(false);
            component.active = true;
            component.hideFlags = HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        /// <summary>
        /// Fills the Audio component's clip banks from Assets/Audio. All Kenney CC0.
        ///
        /// Gun is laserRetro rather than a real firearm recording, and that's a bet, not a
        /// compromise: the soldiers are stylised blobs, so a genuine Mosin Nagant crack would
        /// read as wrong against them, the way arcade games use arcade sounds. It is also the
        /// only judgement here nobody has actually checked -- Claude cannot hear.
        ///
        /// The realistic alternative (OpenGameArt's firearm library) turned out to be CC-BY
        /// despite its page saying CC0, and shipped as ~17s field recordings that would need
        /// trimming. CC0 + game-ready won.
        /// </summary>
        static void BuildAudio(Audio audio)
        {
            var so = new SerializedObject(audio);

            // One clip each. See Audio's remarks: random "variants" turned the gun into a
            // mixtape because Kenney's numbered files are different sounds, not different takes.
            SetClip(so, "_defaultShot", "laserRetro_000");
            SetClip(so, "_hit", "impactPunch_medium_000");
            SetClip(so, "_kill", "impactPunch_heavy_000");
            SetClip(so, "_breach", "impactPunch_heavy_002");
            SetClip(so, "_gateTaken", "confirmation_001");
            SetClip(so, "_gateBad", "question_001");

            BuildGunSounds(so);
            so.ApplyModifiedProperties();
        }

        /// <summary>
        /// Maps Assets/Audio/Guns/&lt;GunName&gt;.* onto the weapon of that name, purely by
        /// filename. Drop in "AK.wav" and the AK uses it; drop nothing and it falls back to the
        /// default. No code, no Inspector, no rebuild of anything but the scene.
        ///
        /// This is deliberately the seam for someone with ears: Claude picked the current sounds
        /// by reading filenames, which is how the gun ended up sounding like a raygun mixtape.
        /// </summary>
        static void BuildGunSounds(SerializedObject so)
        {
            var names = new List<string>();
            var clips = new List<AudioClip>();

            if (Directory.Exists(GunAudioDir))
            {
                foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { GunAudioDir }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                    if (clip == null) continue;
                    names.Add(Path.GetFileNameWithoutExtension(path));   // must match WeaponDef.Mesh
                    clips.Add(clip);
                }
            }

            SerializedProperty nameProp = so.FindProperty("_gunNames");
            SerializedProperty clipProp = so.FindProperty("_gunClips");
            nameProp.arraySize = names.Count;
            clipProp.arraySize = clips.Count;
            for (int i = 0; i < names.Count; i++)
            {
                nameProp.GetArrayElementAtIndex(i).stringValue = names[i];
                clipProp.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
            }

            Debug.Log(names.Count == 0
                ? $"[Frontline] no per-gun sounds in {GunAudioDir} — every gun uses the default."
                : $"[Frontline] per-gun sounds: {string.Join(", ", names)}");
        }

        static void SetClip(SerializedObject so, string field, string name)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioDir}/{name}.ogg");
            if (clip == null) Debug.LogWarning($"[Frontline] missing audio clip {name}.ogg");
            so.FindProperty(field).objectReferenceValue = clip;
        }

        /// <summary>Low clutter, close to the lane. Nothing tall enough to hide a soldier.</summary>
        static readonly string[] NearProps = {
            "SackTrench", "SackTrench_Small", "Crate", "CardboardBoxes_1", "CardboardBoxes_2",
            "CardboardBoxes_3", "CardboardBoxes_4", "ExplodingBarrel", "ExplodingBarrel_Spilled",
            "Debris_Tires", "TrafficCone", "Pallet", "Pallet_Broken", "Barrier_Single",
            "Fence", "Fence_Long", "GasCan", "GasTank", "Debris_Pile", "WoodPlanks",
            "BrickWall_1", "BrickWall_2", "Barrier_Trash"
        };

        /// <summary>Tall silhouette further out, for depth against the horizon.</summary>
        static readonly string[] FarProps = {
            "Tree_1", "Tree_2", "Tree_3", "Tree_4", "StreetLight", "Container_Small",
            "Container_Long", "Debris_BrokenCar", "TrashContainer", "TrashContainer_Open",
            "WaterTank_Platform", "MetalFence", "Sofa", "Pipes"
        };

        /// <summary>
        /// Scatters CC0 props down both sides of the lane so it reads as a battlefield rather
        /// than a dirt strip.
        ///
        /// The bands come off the camera frustum, not taste. Vertical FOV 55 at aspect 9:16
        /// gives a ~33 degree horizontal FOV, so the visible half-width is only ~3 units at the
        /// player's Z and ~10 at the far end of the lane. That means props are only ever seen in
        /// the upper half of the frame, nothing past |x|~10 is ever rendered, and anything
        /// inside |x|~4.8 would sit on the dirt. Hence 4.8..9.5.
        ///
        /// Seeded so the scatter is identical every regeneration -- the scene is a build
        /// artifact and a scene that reshuffles itself on every build isn't reproducible.
        /// </summary>
        static void BuildProps()
        {
            Random.State prevState = Random.state;
            Random.InitState(20260716);

            var root = new GameObject("Props").transform;
            int placed = 0;

            for (float z = -4f; z < 40f; z += Random.Range(2.5f, 5.5f))
            {
                foreach (int side in new[] { -1, 1 })
                {
                    if (Random.value < 0.8f)
                        placed += Place(NearProps, root, side, 0.2f, 1.6f, z + Random.Range(-1f, 1f));
                    if (Random.value < 0.55f)
                        placed += Place(FarProps, root, side, 1.4f, 2.6f, z + Random.Range(-2f, 2f));
                }
            }

            Debug.Log($"[Frontline] {placed} props placed.");
            Random.state = prevState;
        }

        /// <summary>
        /// Places one prop, offset by its own measured footprint rather than a fixed band.
        ///
        /// Props have size, and a fixed inner edge ignores that: Barrier_Trash is 4.49 wide, so
        /// dropped at x=4.8 and rotated across the lane it reaches x=2.55 -- inside the +/-3
        /// playable lane, and enemies would walk straight through it. Measuring the rotated
        /// footprint lets every prop sit as close to the lane as it can without ever entering
        /// it, whatever its shape.
        /// </summary>
        static int Place(string[] pool, Transform parent, int side, float gap, float jitter, float z)
        {
            string name = pool[Random.Range(0, pool.Length)];
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{EnvironmentDir}/{name}.fbx");
            if (prefab == null) { Debug.LogWarning($"[Frontline] missing prop {name}"); return 0; }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));

            // World AABB after rotation, so extents.x is the real X half-span.
            float halfX = 0.5f;
            Renderer[] rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                halfX = b.extents.x;
            }

            float x = DirtHalfWidth + gap + halfX + Random.Range(0f, jitter);
            go.transform.position = new Vector3(side * x, 0f, z);
            // Batching only. Not ContributeGI: there's no lighting bake in this pipeline
            // (ambient is Trilight), so flagging for GI would just be a lie.
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return 1;
        }

        static Material MakeMaterial(string name, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("[Frontline] URP shader missing — was the project made with the 3D (URP) template?");
                shader = Shader.Find("Standard");
            }
            var mat = new Material(shader) { name = name, color = color };
            Directory.CreateDirectory("Assets/Materials");
            AssetDatabase.CreateAsset(mat, $"Assets/Materials/{name}.mat");
            return mat;
        }
    }
}
