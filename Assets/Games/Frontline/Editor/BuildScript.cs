using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace Frontline.EditorTools
{
    /// <summary>
    /// Command-line build entry points. Called via -executeMethod from build.ps1.
    /// </summary>
    public static class BuildScript
    {
        static string[] Scenes => new[] { "Assets/Scenes/Main.unity" };

        /// <summary>
        /// Logs the build target. Deliberately does NOT log the quality level or pipeline.
        ///
        /// An earlier version did, as an alarm for the blank-render bug, and it was worse than
        /// useless: QualitySettings.GetQualityLevel() is the *editor's* current level, not the
        /// one the built player picks (each platform has its own default -- Android takes
        /// Mobile). So it cheerfully reported "target=Android quality='PC'" on a perfectly good
        /// APK. The real check is whether URP actually kept any Lit variants, which lives in
        /// build.ps1 where the compile log can be read.
        /// </summary>
        static void LogTarget() =>
            Debug.Log($"[Frontline] building for {EditorUserBuildSettings.activeBuildTarget}");

        /// <summary>
        /// Bumps the build number and stamps it into the version, which the HUD shows on screen.
        ///
        /// Exists because we wasted a round trip on it: a stale APK was tested against a fix it
        /// didn't contain, and neither of us could tell by looking. Now Simon reads a number off
        /// the screen and we both know exactly which build is in his hands.
        ///
        /// Doubles as the Android versionCode, which Play requires to increase on every upload
        /// -- so this would have been needed anyway.
        /// </summary>
        static int StampVersion()
        {
            const string path = "ProjectSettings/BuildNumber.txt";
            int n = 0;
            if (File.Exists(path) && int.TryParse(File.ReadAllText(path).Trim(), out int parsed))
                n = parsed;
            n++;
            File.WriteAllText(path, n.ToString());

            PlayerSettings.bundleVersion = $"0.1.{n}";
            PlayerSettings.Android.bundleVersionCode = n;
            Debug.Log($"[Frontline] version 0.1.{n} (build {n})");
            return n;
        }

        public static void BuildWebGL()
        {
            // Decompressed: we serve this off a plain local static server for screenshots,
            // and gzip/brotli would need server headers we don't control.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            // APPLICATION: = Unity's built-in templates. PROJECT: would look for a custom
            // one under Assets/WebGLTemplates/, which we don't have.
            PlayerSettings.WebGL.template = "APPLICATION:Default";
            PlayerSettings.runInBackground = true;

            Run(new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = "Builds/WebGL",
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            });
        }

        /// <summary>
        /// Our workhorse. ~2 min instead of WebGL's ~16, and the player can screenshot
        /// itself to disk, which is the only way Claude gets to see the game.
        /// </summary>
        public static void BuildWindows()
        {
            PlayerSettings.companyName = "Frontline";
            PlayerSettings.productName = "Frontline";
            PlayerSettings.runInBackground = true;
            // Portrait-ish window: this is a phone game, so judge it at phone proportions.
            PlayerSettings.defaultScreenWidth = 540;
            PlayerSettings.defaultScreenHeight = 960;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);

            LogTarget();
            StampVersion();

            Run(new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = "Builds/Windows/Frontline.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
        }

        public static void BuildAndroid()
        {
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.companyName = "Frontline";
            PlayerSettings.productName = "Frontline";

            // Portrait, locked. The project shipped as auto-rotate with all four orientations
            // allowed, so the game would have flipped to landscape on the first tilt -- and the
            // whole design assumes 9:16. The camera bands, the lane width, where props are
            // visible: all of it was derived against a portrait frustum.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.useAnimatedAutorotation = false;

            // Was still the URP template's id (com.UnityTechnologies.com.unity.template.urpblank),
            // which Play would reject outright.
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.frontline.game");
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;

            LogTarget();
            StampVersion();

            Run(new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = "Builds/Frontline.apk",
                target = BuildTarget.Android,
                // Release, not Development: the first thing we want off a real device is an
                // honest frame rate, and a dev build doesn't give one.
                options = BuildOptions.None
            });
        }

        static void Run(BuildPlayerOptions opts)
        {
            BuildReport report = BuildPipeline.BuildPlayer(opts);
            BuildSummary s = report.summary;

            if (s.result == BuildResult.Succeeded)
            {
                Debug.Log($"[Frontline] BUILD OK -> {s.outputPath} ({s.totalSize / 1048576f:F1} MB, {s.totalTime.TotalSeconds:F0}s)");
                EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError($"[Frontline] BUILD FAILED: {s.result} ({s.totalErrors} errors)");
                EditorApplication.Exit(1);
            }
        }
    }
}
