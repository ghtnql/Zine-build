using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Build;
using UnityEngine;

public static class ZineWebGLBuild
{
    private const string DefaultBuildPath = "Builds/WebGL";

    [MenuItem("Zine/Build WebGL PWA")]
    public static void BuildFromMenu()
    {
        Build(false);
    }

    public static void BuildFromCommandLine()
    {
        Build(true);
    }

    private static void Build(bool exitEditor)
    {
        string buildPath = GetBuildPath();
        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            throw new InvalidOperationException("No enabled scenes were found in Build Settings.");
        }

        ConfigurePlayerSettings();

        if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL))
        {
            throw new InvalidOperationException("Unable to switch the active build target to WebGL.");
        }

        PrepareBuildDirectory(buildPath);
        Directory.CreateDirectory(buildPath);

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = buildPath,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        if (summary.result != BuildResult.Succeeded)
        {
            Debug.LogError($"Zine WebGL build failed: {summary.result} ({summary.totalErrors} errors)");
            if (exitEditor)
            {
                EditorApplication.Exit(1);
            }

            return;
        }

        File.WriteAllText(Path.Combine(buildPath, ".nojekyll"), string.Empty);
        Debug.Log($"Zine WebGL build completed: {Path.GetFullPath(buildPath)} ({summary.totalSize} bytes)");

        if (exitEditor)
        {
            EditorApplication.Exit(0);
        }
    }

    private static void ConfigurePlayerSettings()
    {
        PlayerSettings.productName = "Zine 3D";
        PlayerSettings.bundleVersion = "2.4.0";
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.runInBackground = true;

        PlayerSettings.WebGL.template = "PROJECT:Zine3D";
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.WebGL.nameFilesAsHashes = true;
        PlayerSettings.WebGL.memorySize = 256;
        PlayerSettings.WebGL.initialMemorySize = 256;
        PlayerSettings.WebGL.maximumMemorySize = 1024;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
        PlayerSettings.WebGL.threadsSupport = false;

        PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Medium);
    }

    private static string GetBuildPath()
    {
        string[] arguments = Environment.GetCommandLineArgs();
        for (int i = 0; i < arguments.Length - 1; i++)
        {
            if (arguments[i] == "-zineBuildPath")
            {
                return Path.GetFullPath(arguments[i + 1]);
            }
        }

        return Path.GetFullPath(DefaultBuildPath);
    }

    private static void PrepareBuildDirectory(string buildPath)
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string fullBuildPath = Path.GetFullPath(buildPath);
        string projectPrefix = projectRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        bool isInsideProject = fullBuildPath.StartsWith(projectPrefix, StringComparison.OrdinalIgnoreCase);
        bool isExpectedLeaf = string.Equals(Path.GetFileName(fullBuildPath), "WebGL", StringComparison.OrdinalIgnoreCase);
        if (!isInsideProject || !isExpectedLeaf)
        {
            throw new InvalidOperationException($"Refusing to clean an unexpected build path: {fullBuildPath}");
        }

        if (Directory.Exists(fullBuildPath))
        {
            Directory.Delete(fullBuildPath, true);
        }
    }
}
