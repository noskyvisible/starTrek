using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace StarTrek.EditorTools
{
    /// <summary>Builds the playable Windows player (Builds/ is git-ignored).</summary>
    public static class BuildTools
    {
        public const string WindowsPath = "Builds/Windows/CaptainsChair.exe";

        [MenuItem("StarTrek/Build Windows Player")]
        public static void BuildWindowsFromMenu() => Debug.Log(BuildWindows(development: false));

        /// <returns>A one-line summary of the build result.</returns>
        public static string BuildWindows(bool development)
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0 || !scenes[0].Contains("Academy"))
                return "FAILED: the Academy must be the first enabled scene (run StarTrek/Build Academy Scene).";
            Directory.CreateDirectory(Path.GetDirectoryName(WindowsPath));
            PlayerSettings.productName = "Captain's Chair";
            PlayerSettings.companyName = "StarTrek Fan Project";
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = WindowsPath,
                target = BuildTarget.StandaloneWindows64,
                options = development ? BuildOptions.Development : BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            return $"{s.result}: {s.totalErrors} errors, {s.totalWarnings} warnings, {s.totalSize / (1024f * 1024f):F0} MB, {s.totalTime.TotalMinutes:F1} min -> {Path.GetFullPath(WindowsPath)}";
        }
    }
}
