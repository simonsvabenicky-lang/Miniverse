using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Miniverse.EditorTools
{
    /// <summary>
    /// Command-line Android build entry point, same shape as Frontline's BuildScript.
    ///
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod Miniverse.EditorTools.HubBuildScript.BuildAndroid
    /// </summary>
    public static class HubBuildScript
    {
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
            Debug.Log($"[Miniverse] version 0.1.{n} (build {n})");
            return n;
        }

        public static void BuildAndroid()
        {
            BuildSceneSync.Sync();
            StampVersion();

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            Directory.CreateDirectory("Builds/Android");

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = "Builds/Android/Miniverse.apk",
                target = BuildTarget.Android,
                options = BuildOptions.None,
            });

            if (report.summary.result != BuildResult.Succeeded)
                throw new System.Exception($"[Miniverse] Build failed: {report.summary.result}, {report.summary.totalErrors} error(s)");

            Debug.Log($"[Miniverse] Build succeeded: {report.summary.outputPath} ({report.summary.totalSize} bytes)");
        }
    }
}
