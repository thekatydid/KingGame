using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildWebGL
{
    private const string OutputPath = "Build/WebGL";

    [MenuItem("Build/Build WebGL")]
    public static void BuildFromMenu()
    {
        Build();
    }

    public static void BuildFromCommandLine()
    {
        Build();
    }

    private static void Build()
    {
        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            throw new BuildFailedException("No enabled scenes found in Build Settings.");
        }

        Directory.CreateDirectory(OutputPath);

        WebGLCompressionFormat originalCompression = PlayerSettings.WebGL.compressionFormat;

        try
        {
            // Disable compression so local static servers work without special headers.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputPath,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException(
                    $"WebGL build failed. Result={summary.result}, Errors={summary.totalErrors}, Warnings={summary.totalWarnings}");
            }

            Debug.Log($"WebGL build succeeded: {summary.outputPath}");
        }
        finally
        {
            PlayerSettings.WebGL.compressionFormat = originalCompression;
        }
    }
}
