using UnityEditor;
using UnityEngine;

namespace FlowSort.EditorTools
{
    /// <summary>
    /// Same pattern Frontline uses for its Kenney/Quaternius imports: anything under
    /// Assets/Art/Kenney gets configured automatically on import, so dropping in a new PNG from a
    /// pack never requires manually clicking through Texture Type/PPU/filter settings.
    /// </summary>
    public class ArtImporter : AssetPostprocessor
    {
        static string KenneyRoot => ProjectPaths.Art + "/";

        // Model colour atlases and particle sprites are sampled by materials, not drawn through
        // the sprite pipeline, so forcing them to Sprite would be wrong (and for the model atlas
        // would break the FBX's own material binding). They import as plain textures.
        static string[] TextureOnlyPaths => new[]
        {
            ProjectPaths.TowerDefense + "/Textures/",
            ProjectPaths.Particles + "/",
            ProjectPaths.GuiBundle + "/",
        };

        static string MenuRoot => ProjectPaths.MenuUI + "/";

        /// <summary>
        /// 9-slice borders, in source pixels, for the menu sprites that get stretched. Without
        /// these a rounded box scaled to a wide pill drags its corner radius out with it.
        /// Unity rescales the border itself when the texture is downsized on import.
        ///
        /// The capsule buttons are deliberately NOT here. Their corner radius is half their
        /// height, so the borders can never fit a button shorter than the source: Unity squashes
        /// them to fit and the pill collapses into a lens. Stretched whole they just look like a
        /// shorter pill, which is what they should look like.
        ///
        /// Every border here is under half the smallest rect it is used at, which is the rule
        /// that keeps this from happening again.
        /// </summary>
        static readonly (string Name, float Border)[] SlicedMenuSprites =
        {
            ("Panel", 190f),
            ("PanelLight", 190f),
            ("TabOn", 60f),
            ("TabOff", 60f),
        };

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(KenneyRoot)) return;

            var importer = (TextureImporter)assetImporter;

            if (assetPath.StartsWith(MenuRoot))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Bilinear;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                // The bundle ships 1500px art for what draws at a couple of hundred; capping this
                // keeps the menu from being most of the APK.
                importer.maxTextureSize = 512;

                string file = System.IO.Path.GetFileNameWithoutExtension(assetPath);
                foreach (var (name, border) in SlicedMenuSprites)
                {
                    if (name != file) continue;
                    importer.spriteBorder = new Vector4(border, border, border, border);
                    break;
                }

                return;
            }

            // The block atlas is minified hard — a 112px tile draws at roughly 40px on a phone —
            // so it is the one texture here that needs mips, or every block outline shimmers.
            if (assetPath == BlockAtlasBuilder.AtlasPath)
            {
                importer.textureType = TextureImporterType.Default;
                importer.mipmapEnabled = true;
                importer.mipMapBias = -0.4f;
                importer.alphaIsTransparency = true;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.npotScale = TextureImporterNPOTScale.None;
                return;
            }

            foreach (var prefix in TextureOnlyPaths)
            {
                if (!assetPath.StartsWith(prefix)) continue;
                importer.textureType = TextureImporterType.Default;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
        }

        /// <summary>
        /// RacingKit pieces are welded into one vertex-coloured mesh by ConveyorTrack, so the only
        /// thing the import has to preserve is the mesh and its submesh/material NAMES — the kit's
        /// own grey materials are never rendered.
        /// </summary>
        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(KenneyRoot + "RacingKit/")) return;

            var importer = (ModelImporter)assetImporter;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.isReadable = true;
        }
    }
}
