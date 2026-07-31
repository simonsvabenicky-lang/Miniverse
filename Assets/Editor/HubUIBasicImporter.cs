using System.IO;
using UnityEditor;
using UnityEngine;

namespace Miniverse.EditorTools
{
    /// <summary>
    /// Import-side handling for the "Basic GUI Bundle" pack Simon picked for Home's premium-casual
    /// look (thick black outlines, gloss highlight, drop-shadow gradient) -- same job as
    /// HubUIImporter did for the original Kenney set, but these source PNGs ship at a much higher,
    /// inconsistent native resolution (roughly 280px-1500px depending on file) instead of Kenney's
    /// uniform ~190-280px. A single hardcoded border in pixels would be wrong for half these files,
    /// so borders here are computed as a FRACTION of each file's own measured width/height via
    /// TextureImporter.GetSourceTextureWidthAndHeight -- correct regardless of which size variant
    /// (Large/Small) or how big the source PNG is.
    ///
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod Miniverse.EditorTools.HubUIBasicImporter.Build
    /// </summary>
    public static class HubUIBasicImporter
    {
        const string UiRoot = "Assets/_Hub/Art/UIBasic";

        [MenuItem("Miniverse/Build Hub UIBasic Sprites")]
        public static void Build()
        {
            string[] pngs = Directory.GetFiles(UiRoot, "*.png", SearchOption.AllDirectories);
            foreach (string path in pngs)
            {
                string unityPath = path.Replace('\\', '/');
                var importer = (TextureImporter)AssetImporter.GetAtPath(unityPath);
                if (importer == null) continue;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.spritePixelsPerUnit = 100f;
                importer.alphaIsTransparency = true;
                // These source PNGs are 5-10x the resolution Kenney's were (up to 1510px for a
                // panel meant to draw at a couple hundred units) -- capping this keeps texture
                // memory and APK size sane without visibly softening anything at the sizes these
                // actually render at.
                importer.maxTextureSize = 1024;

                string fileName = Path.GetFileNameWithoutExtension(unityPath);
                bool isIcon = unityPath.Contains("/Icons/");

                if (isIcon)
                {
                    // Small fixed glyphs, never stretched.
                    importer.spriteBorder = Vector4.zero;
                }
                else
                {
                    importer.GetSourceTextureWidthAndHeight(out int w, out int h);

                    // "_Round" buttons are a true stadium/pill shape -- semicircular end caps,
                    // straight top/bottom -- so the LEFT/RIGHT border has to cover at least the
                    // radius (exactly half the image height) or the caps visibly squash into an
                    // oval when the button is stretched wider/narrower than its source aspect.
                    // Everything else here (Box_*, IconButton_*_Rounded) is a modest rounded-rect,
                    // not a stadium, so a flat percentage is enough.
                    bool isPill = fileName.Contains("_Round") && !fileName.Contains("Square");

                    // Two rounds of on-device testing on this: the *card frame* (~230-260 canvas
                    // units, Box_WhiteOutline_Rounded via CreatePanel/HomeScreenController) looks
                    // right with a generous border. But CreatePillPanel/CreateIconButton render
                    // these same sprite families down at 44-130 canvas units, and 9-slicing broke
                    // down there at every border fraction tried -- too large warped the pill into
                    // a pointed almond shape and squashed icon buttons into near-circles, too
                    // small tore the corner artwork into long pointed streaks across the stretched
                    // centre. Rather than keep chasing a border value that works at that render
                    // size, CreatePillPanel/CreateIconButton switched to Image.Type.Simple (no
                    // slicing at all) -- these border values now only matter for the
                    // larger-rendered sliced sprites (cards, panels, CreateButton's BACK pills).
                    float leftRightFrac = isPill ? 0.20f : 0.16f;
                    float topFrac = isPill ? 0.24f : 0.16f;
                    // Bottom gets extra: every sprite in this pack has a visible gradient shadow
                    // band along the bottom edge (see the reference screenshots), same reasoning
                    // as Kenney's own "depth_gradient" bottom-heavy border in HubUIImporter.
                    float bottomFrac = isPill ? 0.32f : 0.22f;

                    importer.spriteBorder = new Vector4(
                        Mathf.Round(w * leftRightFrac),
                        Mathf.Round(h * bottomFrac),
                        Mathf.Round(w * leftRightFrac),
                        Mathf.Round(h * topFrac));
                }

                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
            }
            Debug.Log($"[Miniverse] {pngs.Length} hub UIBasic sprites imported.");
            AssetDatabase.SaveAssets();
        }
    }
}
