using UnityEditor;
using UnityEngine;
using Miniverse.Hub;

namespace Frontline.EditorTools
{
    /// <summary>
    /// One-time generator for Frontline's MiniGameDef catalog entry -- see Miniverse's
    /// HANDOFF.md ("The plug-in mechanism: MiniGameDef + GameCatalog"). Written as a script
    /// rather than hand-placed in the Editor so graduation stays "run a command," not "click
    /// through Create > Miniverse > MiniGame Definition and fill in fields by hand" -- same
    /// "generate, don't hand-author" contract every other asset in this project follows.
    /// Run once via `Frontline/Create Miniverse Catalog Entry` (or -executeMethod headlessly);
    /// safe to re-run, overwrites the same asset rather than duplicating it.
    /// </summary>
    public static class FrontlineCatalogEntry
    {
        const string Dir = "Assets/Games/Frontline/Resources/GameCatalog";
        const string Path = Dir + "/Frontline.asset";

        [MenuItem("Frontline/Create Miniverse Catalog Entry")]
        public static void Create()
        {
            AssetDatabase.Refresh();

            if (!AssetDatabase.IsValidFolder(Dir))
            {
                AssetDatabase.CreateFolder("Assets/Games/Frontline", "Resources");
                AssetDatabase.CreateFolder("Assets/Games/Frontline/Resources", "GameCatalog");
            }

            var existing = AssetDatabase.LoadAssetAtPath<MiniGameDef>(Path);
            var def = existing != null ? existing : ScriptableObject.CreateInstance<MiniGameDef>();

            def.gameId = "frontline";
            def.displayName = "Frontline";
            def.sceneName = "Main";
            def.category = "Action";
            def.enabled = true;

            const string IconPath = Dir + "/frontline_icon.png";
            var iconImporter = (TextureImporter)AssetImporter.GetAtPath(IconPath);
            if (iconImporter != null && iconImporter.textureType != TextureImporterType.Sprite)
            {
                iconImporter.textureType = TextureImporterType.Sprite;
                iconImporter.spriteImportMode = SpriteImportMode.Single;
                iconImporter.SaveAndReimport();
            }
            def.icon = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);

            if (existing == null) AssetDatabase.CreateAsset(def, Path);
            else EditorUtility.SetDirty(def);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Frontline] Catalog entry written -> {Path}");
        }
    }
}
