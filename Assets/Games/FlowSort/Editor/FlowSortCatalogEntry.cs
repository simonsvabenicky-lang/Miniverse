using UnityEditor;
using UnityEngine;
using Miniverse.Hub;

namespace FlowSort.EditorTools
{
    /// <summary>
    /// One-time generator for FlowSort's MiniGameDef catalog entry -- same pattern as
    /// Frontline's FrontlineCatalogEntry. Run once via `FlowSort/Create Miniverse Catalog Entry`
    /// (or -executeMethod headlessly); safe to re-run, overwrites the same asset rather than
    /// duplicating it.
    /// </summary>
    public static class FlowSortCatalogEntry
    {
        const string Dir = "Assets/Games/FlowSort/Resources/GameCatalog";
        const string Path = Dir + "/FlowSort.asset";

        [MenuItem("FlowSort/Create Miniverse Catalog Entry")]
        public static void Create()
        {
            if (!AssetDatabase.IsValidFolder(Dir))
            {
                AssetDatabase.CreateFolder("Assets/Games/FlowSort", "Resources");
                AssetDatabase.CreateFolder("Assets/Games/FlowSort/Resources", "GameCatalog");
            }

            var existing = AssetDatabase.LoadAssetAtPath<MiniGameDef>(Path);
            var def = existing != null ? existing : ScriptableObject.CreateInstance<MiniGameDef>();

            def.gameId = "flowsort";
            def.displayName = "FlowSort";
            def.sceneName = "FlowSortMain";
            def.category = "Puzzle";
            def.enabled = true;
            // Public name still unresolved per FlowSort's own HANDOFF -- displayName is a
            // placeholder, trivial to rename later without touching gameId (the save-data key).

            if (existing == null) AssetDatabase.CreateAsset(def, Path);
            else EditorUtility.SetDirty(def);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[FlowSort] Catalog entry written -> {Path}");
        }
    }
}
