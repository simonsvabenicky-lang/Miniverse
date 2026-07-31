namespace FlowSort.EditorTools
{
    /// <summary>
    /// Every asset path the builders use, derived from one root.
    ///
    /// This exists for graduation. Inside PocketVerse the whole game moves under
    /// Assets/Games/FlowSort/, and the scenes are generated rather than hand-edited — so the port
    /// has to repoint the BUILDERS, not the scene files. With the paths spread across six files a
    /// missed one does not fail loudly: it loads null, the scene still builds, and the game ships
    /// with a missing font or an invisible sprite. That has already happened twice here.
    ///
    /// Change Root and everything follows.
    /// </summary>
    public static class ProjectPaths
    {
        // Repointed at graduation (2026-07-31).
        public const string Root = "Assets/Games/FlowSort";

        public static string Art => Root + "/Art/Kenney";
        public static string Scenes => Root + "/Scenes";
        public static string Materials => Root + "/Materials";
        public static string Settings => Root + "/Settings";
        public static string Fonts => Root + "/Fonts";
        public static string Pictures => Root + "/Pictures";
        public static string Audio => Root + "/Audio";

        public static string MenuUI => Art + "/MenuUI";
        public static string GuiBundle => Art + "/GuiBundle";
        public static string Particles => Art + "/Particles";
        public static string TowerDefense => Art + "/TowerDefense";
        public static string RacingKit => Art + "/RacingKit";

        // Renamed from Main.unity at graduation: Frontline's own scene is already named "Main",
        // and Unity resolves SceneManager.LoadScene(string) by scene NAME, not path -- two scenes
        // sharing a name in one project's Build Settings is ambiguous. Matches FlowSortCatalogEntry's
        // existing sceneName = "FlowSortMain" from the previous graduation, so the MiniGameDef
        // asset didn't need touching.
        public static string MainScene => Scenes + "/FlowSortMain.unity";
        // Unused inside PocketVerse -- Menu.unity isn't ported (the hub is FlowSort's front end
        // here), left defined only because nothing references it strongly enough to be worth
        // deleting a one-line constant over.
        public static string MenuScene => Scenes + "/Menu.unity";

        public static string BlockAtlas => GuiBundle + "/BlockAtlas.png";
        public static string FontAsset => Fonts + "/KenneyFuture SDF.asset";
        public static string FontSource => Fonts + "/Kenney Future.ttf";
    }
}
