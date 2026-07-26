using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Miniverse.EditorTools
{
    /// <summary>
    /// Regenerates Build Settings' scene list from disk instead of it being hand-maintained.
    /// Same motivation as MiniGameDef/GameCatalog: a minigame's graduation drops a scene
    /// under Assets/Games/&lt;Name&gt;/, runs this, and never has to touch a shared
    /// EditorBuildSettings list that another game's graduation might also be mid-edit on.
    /// Home.unity is always index 0 so PlayMode/the built app boots there.
    /// </summary>
    public static class BuildSceneSync
    {
        const string HomeScenePath = "Assets/_Hub/Scenes/Home.unity";

        [MenuItem("Miniverse/Sync Game Scenes To Build Settings")]
        public static void Sync()
        {
            var gameScenes = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Games" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path)
                .ToArray();

            var scenes = new[] { HomeScenePath }.Concat(gameScenes)
                .Select(path => new EditorBuildSettingsScene(path, true))
                .ToArray();

            EditorBuildSettings.scenes = scenes;
            Debug.Log($"[Miniverse] Build Settings synced: {scenes.Length} scene(s) — {string.Join(", ", scenes.Select(s => s.path))}");
        }
    }
}
