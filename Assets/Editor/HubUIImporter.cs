using System.IO;
using UnityEditor;
using UnityEngine;

namespace Miniverse.EditorTools
{
    /// <summary>
    /// Same contract as Frontline's UIImporter (Assets/Games/Frontline/Editor/UIImporter.cs):
    /// sets every PNG under the hub's own Art/UI to a 9-sliced Sprite where it's a stretchable
    /// container (button_/input_), Simple where it's a small fixed icon. Border values copied
    /// from Frontline's own (read directly off the source images there, not guessed) since these
    /// are the same Kenney UI Pack family at the same standard sizes -- a second hand-measurement
    /// would just reproduce the same numbers.
    ///
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod Miniverse.EditorTools.HubUIImporter.Build
    /// </summary>
    public static class HubUIImporter
    {
        const string UiRoot = "Assets/_Hub/Art/UI";

        [MenuItem("Miniverse/Build Hub UI Sprites")]
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

                string fileName = Path.GetFileName(unityPath);
                bool isStretchable = fileName.StartsWith("button_") || fileName.StartsWith("input_");
                bool hasDepth = fileName.Contains("depth");
                bool isSquareOrRound = fileName.Contains("button_square") || fileName.Contains("button_round");

                if (!isStretchable)
                {
                    importer.spriteBorder = Vector4.zero;
                }
                else if (isSquareOrRound)
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
            Debug.Log($"[Miniverse] {pngs.Length} hub UI sprites imported.");
            AssetDatabase.SaveAssets();
        }
    }
}
