using System.Collections.Generic;
using System.Linq;
using FlowSort.Blocks;
using FlowSort.Gameplay;
using FlowSort.Hub;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace FlowSort.EditorTools
{
    /// <summary>
    /// Builds Assets/Scenes/Main.unity from scratch, in code. The scene is a build artifact and
    /// is never hand-edited — same rule as Frontline/PocketVerse.
    ///
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod FlowSort.EditorTools.SceneBuilder.Build
    /// </summary>
    public static class SceneBuilder
    {
        public static string ScenePath => ProjectPaths.MainScene;

        /// <summary>Every glyph the HUD and turret labels can display, baked into a static atlas.</summary>
        const string Glyphs =
            "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz .,:;!?-+*/%()x";

        [MenuItem("FlowSort/Rebuild Main Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var art = BuildArt();
            var camera = BuildCamera();
            BuildLight();
            BuildVolume(camera);

            var frame = new GameObject("Frame", typeof(FrameRenderer));
            var frameRenderer = frame.GetComponent<FrameRenderer>();
            frameRenderer.Material = art.BackgroundMaterial;
            frameRenderer.SlotMaterial = art.SlotMaterial;

            var trackGO = new GameObject("ConveyorTrack", typeof(ConveyorTrack));
            var track = trackGO.GetComponent<ConveyorTrack>();
            track.TrackMaterial = art.TrackMaterial;
            track.StraightModel = art.RoadStraightModel;
            track.CornerModel = art.RoadCornerModel;
            track.KerbModel = art.RoadKerbModel;
            track.GateModel = art.GateModel;
            track.BarrierModelA = art.BarrierRedModel;
            track.BarrierModelB = art.BarrierWhiteModel;

            var wallGO = new GameObject("BlockWall", typeof(WallMesh), typeof(BlockWall));
            wallGO.GetComponent<WallMesh>().BlockMaterial = art.BlockMaterial;
            var wall = wallGO.GetComponent<BlockWall>();

            var ballsGO = new GameObject("BallSystem", typeof(BallSystem));
            var balls = ballsGO.GetComponent<BallSystem>();
            balls.Wall = wall;
            balls.BallMaterial = art.BallMaterial;

            // Slot positions come from the runtime Layout — they depend on aspect.
            var fxGO = new GameObject("ImpactFX", typeof(ImpactFX));
            var fx = fxGO.GetComponent<ImpactFX>();
            fx.ParticleMaterial = art.ParticleMaterial;

            AudioBuilder.Build();

            var slotGO = new GameObject("TowerSlots", typeof(TowerSlots));
            var slots = slotGO.GetComponent<TowerSlots>();
            slots.Art = art;
            slots.Balls = balls;
            slots.Wall = wall;
            slots.Fx = fx;
            slots.TapCamera = camera;

            var walletGO = new GameObject("CurrencyWallet", typeof(CurrencyWallet));
            var wallet = walletGO.GetComponent<CurrencyWallet>();

            var gameGO = new GameObject("Game", typeof(BlockBreakGame));
            // Graduation-only (2026-07-31): PocketVerse's hub wrapper, attached to the same
            // GameObject as BlockBreakGame per FlowSortMiniGame's own doc comment. Standalone
            // FlowSort has no equivalent -- Miniverse.Hub/IMiniGame don't exist there.
            gameGO.AddComponent<FlowSortMiniGame>();
            var game = gameGO.GetComponent<BlockBreakGame>();
            game.Wall = wall;
            game.Balls = balls;
            game.Slots = slots;
            game.Wallet = wallet;
            game.Fx = fx;
            game.Pictures = LoadPictures();

            var hud = BuildCanvas(art, game);
            game.Hud = hud;
            hud.Game = game;

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            System.IO.Directory.CreateDirectory(ProjectPaths.Scenes);
            EditorSceneManager.SaveScene(scene, ScenePath);

            // MenuBuilder.RegisterScenes() removed at graduation: PocketVerse doesn't ship
            // Menu.unity at all (the hub is FlowSort's front end here, see FlowSortMiniGame's doc
            // comment), and this project has already been bitten once by a scene builder that sets
            // EditorBuildSettings.scenes itself -- it silently drops every other graduated game's
            // scene the moment it runs standalone. BuildSceneSync is the sole source of truth for
            // Build Settings inside PocketVerse.

            Debug.Log($"[FlowSort] Main scene rebuilt at {ScenePath} " +
                      $"({game.Pictures.Length} pictures, {BlockTuning.WallWidth}x{BlockTuning.WallHeight} wall)");
        }

        // --- Assets ---

        static PixelPicture[] LoadPictures()
        {
            var pics = AssetDatabase.FindAssets("t:PixelPicture")
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(p => p)
                .Select(AssetDatabase.LoadAssetAtPath<PixelPicture>)
                .Where(p => p != null && p.Cells != null && p.Cells.Length > 0)
                .ToArray();

            if (pics.Length == 0)
                Debug.LogError("[FlowSort] No PixelPicture assets found — run FlowSort/Generate Pictures first.");

            return pics;
        }

        static BlockArt BuildArt()
        {
            System.IO.Directory.CreateDirectory(ProjectPaths.Materials);

            var go = new GameObject("BlockArt", typeof(BlockArt));
            var art = go.GetComponent<BlockArt>();

            // Baked first, so a scene rebuild can never ship a sheet that disagrees with the
            // palette the rest of the scene was built against.
            art.BlockAtlasTexture = BlockAtlasBuilder.Build();
            art.SlotFrameSprite = Load<Texture2D>($"{ProjectPaths.GuiBundle}/SlotFrame.png");
            art.PanelSprite = Load<Texture2D>($"{ProjectPaths.GuiBundle}/Panel.png");

            art.BlockMaterial = Material("M_Block", "FlowSort/BlockLit", m =>
            {
                if (art.BlockAtlasTexture != null) m.SetTexture("_MainTex", art.BlockAtlasTexture);
                m.SetFloat("_Cutoff", 0.35f);
                m.SetFloat("_AmbientBoost", 0.5f);
                m.SetFloat("_Saturate", 0.15f);
            });

            art.TrackMaterial = Material("M_Track", "FlowSort/VertexLit", m =>
            {
                m.SetFloat("_AmbientBoost", 0.6f);
            });

            // Deliberately untextured. The kit's turret ships with a tan/grey colormap, and
            // multiplying a tower's colour through it turned every tower muddy brown or black —
            // exactly the palette the art direction rules out. A white base lets _BaseColor,
            // set per tower through a property block, be the colour you actually see.
            art.TowerMaterial = Material("M_Tower", "Universal Render Pipeline/Lit", m =>
            {
                m.SetColor("_BaseColor", Color.white);
                m.SetFloat("_Smoothness", 0.25f);
                m.SetFloat("_Metallic", 0f);
            });

            // SrcAlpha / One = additive, so the ball's vertex-alpha gradient reads as a glow trail.
            art.BallMaterial = Material("M_BallGlow", "FlowSort/UnlitQuad", m =>
            {
                m.SetFloat("_BlendSrc", (float)BlendMode.SrcAlpha);
                m.SetFloat("_BlendDst", (float)BlendMode.One);
                m.SetColor("_Color", new Color(1f, 0.98f, 0.9f, 1f));
            });

            // SrcAlpha / OneMinusSrcAlpha = standard alpha blend for the opaque backdrop gradient.
            art.BackgroundMaterial = Material("M_Background", "FlowSort/UnlitQuad", m =>
            {
                m.SetFloat("_BlendSrc", (float)BlendMode.SrcAlpha);
                m.SetFloat("_BlendDst", (float)BlendMode.OneMinusSrcAlpha);
            });

            // The landing squares, straight from the GUI bundle: same authored bevel and outline
            // the blocks use, so an empty slot reads as part of the same set of furniture.
            art.SlotMaterial = Material("M_Slot", "FlowSort/UnlitQuad", m =>
            {
                m.SetFloat("_BlendSrc", (float)BlendMode.SrcAlpha);
                m.SetFloat("_BlendDst", (float)BlendMode.OneMinusSrcAlpha);
                if (art.SlotFrameSprite != null) m.SetTexture("_MainTex", art.SlotFrameSprite);
            });

            // Alpha-blended so debris reads as solid chips rather than glowing embers.
            art.ParticleMaterial = Material("M_Particle", "FlowSort/UnlitQuad", m =>
            {
                m.SetFloat("_BlendSrc", (float)BlendMode.SrcAlpha);
                m.SetFloat("_BlendDst", (float)BlendMode.OneMinusSrcAlpha);
                var dirt = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ProjectPaths.Particles}/dirt_02.png");
                if (dirt != null) m.SetTexture("_MainTex", dirt);
            });

            art.TurretCommonModel = Load<GameObject>($"{ProjectPaths.TowerDefense}/weapon-turret.fbx");
            art.TurretGoldModel = Load<GameObject>($"{ProjectPaths.TowerDefense}/weapon-cannon.fbx");

            art.RoadStraightModel = Load<GameObject>($"{ProjectPaths.RacingKit}/roadStraight.fbx");
            art.RoadCornerModel = Load<GameObject>($"{ProjectPaths.RacingKit}/roadCornerSmall.fbx");
            art.RoadKerbModel = Load<GameObject>($"{ProjectPaths.RacingKit}/roadCornerSmallBorder.fbx");
            art.GateModel = Load<GameObject>($"{ProjectPaths.RacingKit}/overheadRoundColored.fbx");
            art.BarrierRedModel = Load<GameObject>($"{ProjectPaths.RacingKit}/barrierRed.fbx");
            art.BarrierWhiteModel = Load<GameObject>($"{ProjectPaths.RacingKit}/barrierWhite.fbx");

            art.ParticleDirt = Load<Texture2D>($"{ProjectPaths.Particles}/dirt_02.png");
            art.ParticleSpark = Load<Texture2D>($"{ProjectPaths.Particles}/spark_04.png");
            art.ParticleSmoke = Load<Texture2D>($"{ProjectPaths.Particles}/smoke_04.png");
            art.ParticleStar = Load<Texture2D>($"{ProjectPaths.Particles}/star_08.png");

            art.Font = BuildFont();

            return art;
        }

        static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) Debug.LogError($"[FlowSort] Missing asset at {path}");
            return asset;
        }

        static Material Material(string name, string shaderName, System.Action<Material> configure)
        {
            string path = $"{ProjectPaths.Materials}/{name}.mat";
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError($"[FlowSort] Shader not found: {shaderName}");
                return null;
            }

            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = shader;
            }

            configure?.Invoke(mat);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>
        /// Builds a TMP font asset from the CC0 Kenney Future TTF. Falls back to TMP's default
        /// font rather than failing the whole scene build if generation isn't available.
        /// </summary>
        static TMP_FontAsset BuildFont()
        {
            string assetPath = ProjectPaths.FontAsset;

            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing != null) return existing;

            var ttf = AssetDatabase.LoadAssetAtPath<Font>(ProjectPaths.FontSource);
            if (ttf == null)
            {
                Debug.LogWarning("[FlowSort] Kenney Future.ttf not found; using TMP default font.");
                return TMP_Settings.defaultFontAsset;
            }

            try
            {
                var created = TMP_FontAsset.CreateFontAsset(
                    ttf, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
                    AtlasPopulationMode.Dynamic, enableMultiAtlasSupport: true);

                if (created == null) throw new System.Exception("CreateFontAsset returned null");

                created.name = "KenneyFuture SDF";
                AssetDatabase.CreateAsset(created, assetPath);

                // Bake the glyphs we actually use, then freeze the atlas. A Dynamic-mode asset
                // would try to rasterise missing glyphs at runtime in the player, which is the
                // fragile path; Static with a pre-populated atlas is what ships reliably.
                created.TryAddCharacters(Glyphs);

                // The atlas texture and material are separate objects. Without adding them as
                // sub-assets they are never serialised, and the build then throws
                // "m_AtlasTextures of TMP_FontAsset has not been assigned" and ships with
                // invisible text — caught only because the build log was read, not the Editor.
                if (created.atlasTextures != null)
                {
                    for (int i = 0; i < created.atlasTextures.Length; i++)
                    {
                        var tex = created.atlasTextures[i];
                        if (tex == null) continue;
                        tex.name = $"KenneyFuture Atlas {i}";
                        if (!AssetDatabase.IsSubAsset(tex)) AssetDatabase.AddObjectToAsset(tex, created);
                    }
                }

                if (created.material != null)
                {
                    created.material.name = "KenneyFuture Material";
                    if (!AssetDatabase.IsSubAsset(created.material))
                        AssetDatabase.AddObjectToAsset(created.material, created);
                }

                created.atlasPopulationMode = AtlasPopulationMode.Static;

                EditorUtility.SetDirty(created);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

                int atlasCount = created.atlasTextures?.Length ?? 0;
                Debug.Log($"[FlowSort] Generated TMP font asset from Kenney Future (CC0): " +
                          $"{atlasCount} atlas texture(s), {created.characterTable.Count} glyphs.");
                return created;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[FlowSort] TMP font generation failed ({e.Message}); using default.");
                return TMP_Settings.defaultFontAsset;
            }
        }

        // --- Scene pieces ---

        static Camera BuildCamera()
        {
            // AudioListener is explicit here. Unity only adds one to the camera of a DEFAULT new
            // scene, and these scenes are built from an empty one — so without this line the game
            // shipped with a full sound bank, a music bed, and nothing audible at all.
            var go = new GameObject("Main Camera", typeof(Camera), typeof(UniversalAdditionalCameraData),
                                    typeof(AudioListener), typeof(GameCamera));
            go.tag = "MainCamera";

            var cam = go.GetComponent<Camera>();
            cam.orthographic = false;
            cam.fieldOfView = BlockTuning.CameraFov;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 200f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = BlockPalette.BackgroundTop;

            go.transform.position = new Vector3(0f, 0f, -BlockTuning.CameraDistance);
            go.transform.rotation = Quaternion.identity;

            return cam;
        }

        static void BuildLight()
        {
            var go = new GameObject("Directional Light", typeof(Light));
            var light = go.GetComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color32(0xFF, 0xF3, 0xDE, 0xFF);
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;
            go.transform.rotation = Quaternion.Euler(48f, -28f, 0f);
        }

        static void BuildVolume(Camera camera)
        {
            System.IO.Directory.CreateDirectory(ProjectPaths.Settings);
            string path = ProjectPaths.Settings + "/BlockVolumeProfile.asset";

            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }

            // Fetch-or-add, never add-if-absent: the profile is an asset that survives rebuilds,
            // so guarding on Has<T>() silently kept the previous run's values forever.
            var bloom = Override<Bloom>(profile);
            bloom.threshold.overrideState = true;
            bloom.threshold.value = 1.15f;
            bloom.intensity.overrideState = true;
            bloom.intensity.value = 0.45f;
            bloom.scatter.overrideState = true;
            bloom.scatter.value = 0.6f;

            var vignette = Override<Vignette>(profile);
            vignette.intensity.overrideState = true;
            // Barely there. A heavier vignette is what made the board read as dim at the edges.
            vignette.intensity.value = 0.12f;
            vignette.smoothness.overrideState = true;
            vignette.smoothness.value = 0.4f;

            var tone = Override<Tonemapping>(profile);
            tone.mode.overrideState = true;
            tone.mode.value = TonemappingMode.Neutral;

            EditorUtility.SetDirty(profile);

            var go = new GameObject("Global Volume", typeof(Volume));
            var volume = go.GetComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.sharedProfile = profile;

            camera.GetComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
        }

        static T Override<T>(VolumeProfile profile) where T : VolumeComponent
            => profile.TryGet<T>(out var existing) ? existing : profile.Add<T>(true);

        // --- UI ---

        static BlockHud BuildCanvas(BlockArt art, BlockBreakGame game)
        {
            var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            var hud = canvasGO.AddComponent<BlockHud>();

            // Pivot is set to match the anchor for corner-anchored elements, so the position is a
            // plain inset from that corner. With a centred pivot, "LEVEL 1" at x=150 with a
            // 420-wide box started at x=-60 and got clipped off the left edge on device.
            // "BACK", not "EXIT": Kenney Future's X glyph renders as something that reads as an
            // H, so "EXIT" showed up on device as "EHIT". Avoid the letter entirely.
            var exit = Button(canvasGO.transform, "ExitButton", "BACK",
                new Vector2(0f, 1f), new Vector2(34f, -34f), new Vector2(230f, 116f), art.Font, 40f,
                "BtnGrey");
            hud.ExitButton = exit;

            hud.LevelText = Label(canvasGO.transform, "LevelText", "LEVEL 1", art.Font,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(290f, -50f), new Vector2(420f, 90f),
                TextAlignmentOptions.Left, 46f, BlockPalette.TextInk);

            hud.ScoreText = Label(canvasGO.transform, "ScoreText", "0", art.Font,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -40f), new Vector2(460f, 110f),
                TextAlignmentOptions.Right, 76f, BlockPalette.TextInk);

            hud.BeltText = Label(canvasGO.transform, "BeltText", "0/5", art.Font,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -160f), new Vector2(300f, 70f),
                TextAlignmentOptions.Right, 48f, BlockPalette.TextInk);

            BuildBanner(canvasGO.transform, art, hud);
            BuildPausePanel(canvasGO.transform, art, hud);
            BuildGameOverPanel(canvasGO.transform, art, hud);

            return hud;
        }

        /// <summary>
        /// The level card. Built before the game-over panel so it sits behind it in the hierarchy
        /// and cannot cover the retry buttons if a level ends while it is still on screen.
        /// </summary>
        static void BuildBanner(Transform parent, BlockArt art, BlockHud hud)
        {
            var root = new GameObject("Banner", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            root.transform.SetParent(parent, false);

            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 340f);
            rect.sizeDelta = new Vector2(720f, 200f);

            var image = root.GetComponent<Image>();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{ProjectPaths.MenuUI}/Panel.png");
            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
            }
            else
            {
                image.color = BlockPalette.GridWell;
            }

            image.raycastTarget = false;

            var group = root.GetComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            hud.BannerRoot = root;
            hud.BannerGroup = group;
            hud.BannerText = Label(root.transform, "Text", "LEVEL 1", art.Font,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 4f),
                new Vector2(680f, 140f), TextAlignmentOptions.Center, 72f, BlockPalette.TextInk);
        }

        /// <summary>
        /// Pause, reached from BACK. Built before the game-over panel so it can never cover it.
        /// </summary>
        static void BuildPausePanel(Transform parent, BlockArt art, BlockHud hud)
        {
            var panel = Overlay(parent, "PausePanel");
            var card = Card(panel.transform, new Vector2(0f, 40f), new Vector2(800f, 640f));

            Label(card.transform, "Title", "PAUSED", art.Font,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 200f), new Vector2(700f, 130f),
                TextAlignmentOptions.Center, 76f, BlockPalette.TextInk);

            hud.ResumeButton = Button(card.transform, "ResumeButton", "RESUME",
                new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(520f, 150f), art.Font, 50f,
                "BtnGreen");

            hud.QuitButton = Button(card.transform, "QuitButton", "QUIT",
                new Vector2(0.5f, 0.5f), new Vector2(0f, -160f), new Vector2(520f, 140f), art.Font, 46f,
                "BtnGrey");

            hud.PausePanel = panel;
        }

        static GameObject Overlay(Transform parent, string name)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);

            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            panel.GetComponent<Image>().color = new Color(0.05f, 0.06f, 0.14f, 0.88f);
            return panel;
        }

        /// <summary>The GUI-bundle panel the overlays hang their contents on.</summary>
        static GameObject Card(Transform parent, Vector2 position, Vector2 size)
        {
            var card = new GameObject("Card", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(parent, false);

            var rect = card.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var image = card.GetComponent<Image>();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{ProjectPaths.MenuUI}/Panel.png");
            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
            }
            else
            {
                image.color = BlockPalette.GridWell;
            }

            return card;
        }

        static void BuildGameOverPanel(Transform parent, BlockArt art, BlockHud hud)
        {
            var panel = new GameObject("GameOverPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);

            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            panel.GetComponent<Image>().color = new Color(0.05f, 0.06f, 0.14f, 0.88f);

            // Card behind the text, on the same panel sprite the menu's records board uses.
            var card = new GameObject("Card", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(panel.transform, false);

            var cardRect = card.GetComponent<RectTransform>();
            cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.anchoredPosition = new Vector2(0f, 60f);
            cardRect.sizeDelta = new Vector2(820f, 900f);

            var cardImage = card.GetComponent<Image>();
            var cardSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{ProjectPaths.MenuUI}/Panel.png");
            if (cardSprite != null)
            {
                cardImage.sprite = cardSprite;
                cardImage.type = cardSprite.border.sqrMagnitude > 0f
                    ? Image.Type.Sliced : Image.Type.Simple;
            }
            else
            {
                cardImage.color = BlockPalette.GridWell;
            }

            Label(card.transform, "Title", "GAME OVER", art.Font,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 300f), new Vector2(760f, 140f),
                TextAlignmentOptions.Center, 84f, BlockPalette.Get(1));

            Label(card.transform, "ScoreCaption", "SCORE", art.Font,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 160f), new Vector2(600f, 70f),
                TextAlignmentOptions.Center, 40f, BlockPalette.TextInk);

            hud.GameOverScoreText = Label(card.transform, "FinalScore", "0", art.Font,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(700f, 130f),
                TextAlignmentOptions.Center, 96f, BlockPalette.TextAccent);

            hud.RetryButton = Button(card.transform, "RetryButton", "RETRY",
                new Vector2(0.5f, 0.5f), new Vector2(0f, -130f), new Vector2(520f, 150f), art.Font, 50f,
                "BtnGreen");

            hud.MenuButton = Button(card.transform, "MenuButton", "MENU",
                new Vector2(0.5f, 0.5f), new Vector2(0f, -310f), new Vector2(520f, 140f), art.Font, 46f,
                "BtnBlue");

            hud.GameOverPanel = panel;
        }

        /// <summary>
        /// A HUD button on the same GUI-bundle sprite the menu uses. Flat coloured rectangles
        /// here next to a fully dressed menu made the two screens look like different games.
        /// </summary>
        static Button Button(Transform parent, string name, string text, Vector2 anchor,
                            Vector2 position, Vector2 size, TMP_FontAsset font, float fontSize,
                            string sprite = "BtnBlue")
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor == new Vector2(0.5f, 0.5f) ? new Vector2(0.5f, 0.5f) : anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var image = go.GetComponent<Image>();
            var face = AssetDatabase.LoadAssetAtPath<Sprite>($"{ProjectPaths.MenuUI}/{sprite}.png");
            if (face != null)
            {
                image.sprite = face;
                image.type = face.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
            }
            else
            {
                image.color = BlockPalette.SlotFrame;
            }

            var button = go.AddComponent<Button>();

            var label = Label(go.transform, "Label", text, font,
                new Vector2(0.5f, 0.5f), Vector2.zero, size,
                TextAlignmentOptions.Center, fontSize, BlockPalette.TextInk);
            label.outlineWidth = 0.22f;
            label.outlineColor = new Color32(0x20, 0x20, 0x38, 0xFF);

            // No onClick wiring here — BlockHud does it at runtime in Start(). Editor-time
            // AddListener does not survive scene serialisation.
            return button;
        }

        static TMP_Text Label(Transform parent, string name, string text, TMP_FontAsset font,
                              Vector2 anchor, Vector2 position, Vector2 size,
                              TextAlignmentOptions alignment, float fontSize, Color color)
            => Label(parent, name, text, font, anchor, new Vector2(0.5f, 0.5f), position, size,
                     alignment, fontSize, color);

        static TMP_Text Label(Transform parent, string name, string text, TMP_FontAsset font,
                              Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size,
                              TextAlignmentOptions alignment, float fontSize, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var tmp = go.GetComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = alignment;
            tmp.color = color;
            tmp.enableWordWrapping = false;

            return tmp;
        }
    }
}
