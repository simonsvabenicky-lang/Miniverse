using UnityEditor;
using UnityEngine;

namespace FlowSort.EditorTools
{
    /// <summary>
    /// Same pattern Frontline uses for its Kenney/Quaternius imports: anything under
    /// Assets/Art/Kenney gets configured as a Sprite automatically on import, so dropping in a
    /// new PNG from a pack never requires manually clicking through Texture Type/PPU/filter
    /// settings in the Editor.
    /// </summary>
    public class ArtImporter : AssetPostprocessor
    {
        const string KenneyRoot = "Assets/Games/FlowSort/Art/Kenney/";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(KenneyRoot)) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
        }
    }
}
