using System.IO;
using UnityEditor;
using UnityEngine;

namespace Frontline.EditorTools
{
    /// <summary>
    /// Imports the Kenney UI Pack (CC0) sprites as 9-sliced UI sprites, and ensures TextMeshPro
    /// is actually usable (this project has been 100% IMGUI until now -- neither TMP's Essential
    /// Resources nor a single Canvas has ever existed here).
    ///
    /// Call WITHOUT -quit -- this method owns its own exit once the async TMP import genuinely
    /// finishes. See EnsureTmpEssentials for why that matters.
    ///
    ///   Unity.exe -batchmode -projectPath . -executeMethod Frontline.EditorTools.UIImporter.Build
    /// </summary>
    public static class UIImporter
    {
        const string UiRoot = "Assets/Games/Frontline/Art/UI"; // repointed at graduation (2026-07-27)

        static System.DateTime s_ImportStarted;
        static bool s_TmpDone;

        [MenuItem("Frontline/Build UI")]
        public static void Build()
        {
            ImportSprites();
            EnsureTmpEssentials();
        }

        /// <summary>
        /// Sets every PNG under Assets/Art/UI to a Sprite, 9-sliced where the shape is actually
        /// a stretchable container (buttons, panels) and left Simple where it's a small fixed
        /// glyph (icons, checkboxes, stars, the divider) -- 9-slicing a 64px checkbox by corner
        /// regions sized for a 384px button would smear it instead of framing it.
        ///
        /// Borders read off the actual images (Read tool, not a guess): rectangle buttons/panels
        /// are 384x128 with a ~24px corner radius, and "depth_gradient" variants carry an extra
        /// ~8px drop-shadow band along the bottom that a uniform border would stretch and smear.
        /// The square icon-button is a smaller 128x128 composition -- the corner radius scales
        /// down with it, so it gets its own (smaller) border rather than the rectangle's.
        /// </summary>
        static void ImportSprites()
        {
            string[] pngs = Directory.GetFiles(UiRoot, "*.png", SearchOption.AllDirectories);
            foreach (string path in pngs)
            {
                string unityPath = path.Replace('\\', '/');
                var importer = (TextureImporter)AssetImporter.GetAtPath(unityPath);
                if (importer == null) continue;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;          // flat UI, never seen at a distance
                importer.filterMode = FilterMode.Bilinear;
                importer.spritePixelsPerUnit = 100f;
                importer.alphaIsTransparency = true;

                string fileName = Path.GetFileName(unityPath);
                bool isStretchable = fileName.StartsWith("button_") || fileName.StartsWith("input_");
                bool hasDepth = fileName.Contains("depth");
                bool isSquare = fileName.Contains("button_square");

                if (!isStretchable)
                {
                    // Icons, checkboxes, stars, the divider: shown near-native size, never
                    // stretched into a container, so a border would only distort them.
                    importer.spriteBorder = Vector4.zero;
                }
                else if (isSquare)
                {
                    importer.spriteBorder = hasDepth
                        ? new Vector4(18, 22, 18, 18)
                        : new Vector4(18, 18, 18, 18);
                }
                else
                {
                    importer.spriteBorder = hasDepth
                        ? new Vector4(24, 32, 24, 24)   // left, bottom, right, top -- extra bottom for the shadow band
                        : new Vector4(24, 24, 24, 24);
                }

                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
            }
            Debug.Log($"[Frontline] {pngs.Length} UI sprites imported.");
        }

        /// <summary>
        /// TMP Essential Resources (default font/material/shader) are a UPM sample package, not
        /// code -- nothing in this project has ever used a TMP component, so they've never been
        /// pulled in. Without them, TextMeshProUGUI silently renders no visible text at all.
        ///
        /// Two things had to be fixed to make this actually land on disk rather than just log
        /// success:
        /// (1) TMP_PackageUtilities.ImportProjectResourcesMenu() hardcodes
        /// AssetDatabase.ImportPackage(path, interactive: true), and that genuinely blocks on a
        /// modal in batchmode instead of being auto-suppressed the way every other headless
        /// Editor call in this project is. Call ImportPackage directly with interactive:false.
        /// (2) ImportPackage only finishes via the importPackageCompleted callback, which fires
        /// through Unity's own EditorApplication.update loop -- and blocking that same thread in
        /// a Thread.Sleep spin-wait (tried first) starves the very loop that would deliver the
        /// callback, a straightforward self-deadlock that silently "succeeded" while doing
        /// nothing. The fix is to never block: hook EditorApplication.update and let control
        /// return to Unity's normal loop, which is also why this can't run with -quit -- nothing
        /// would be left alive to tick that loop. Build() exits the batch process itself once
        /// done (or after a timeout).
        /// </summary>
        static void EnsureTmpEssentials()
        {
            if (AssetDatabase.IsValidFolder("Assets/TextMesh Pro"))
            {
                Debug.Log("[Frontline] TMP Essential Resources already present.");
                Finish();
                return;
            }

            s_ImportStarted = System.DateTime.Now;
            s_TmpDone = false;
            AssetDatabase.importPackageCompleted += OnImportComplete;

            string path = TMPro.EditorUtilities.TMP_EditorUtility.packageFullPath
                          + "/Package Resources/TMP Essential Resources.unitypackage";
            AssetDatabase.ImportPackage(path, interactive: false);

            EditorApplication.update += PollForCompletion;
        }

        static void OnImportComplete(string packageName) => s_TmpDone = true;

        static void PollForCompletion()
        {
            bool timedOut = (System.DateTime.Now - s_ImportStarted).TotalSeconds > 60;
            if (!s_TmpDone && !timedOut) return;

            EditorApplication.update -= PollForCompletion;
            AssetDatabase.importPackageCompleted -= OnImportComplete;

            Debug.Log(s_TmpDone
                ? "[Frontline] TMP Essential Resources import completed."
                : "[Frontline] TMP Essential Resources import TIMED OUT waiting for callback.");
            Finish();
        }

        static void Finish()
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Frontline] UI import done. TMP folder present={AssetDatabase.IsValidFolder("Assets/TextMesh Pro")}");
            EditorApplication.Exit(0);
        }
    }
}
