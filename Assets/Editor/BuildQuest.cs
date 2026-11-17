using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.XR.Management;
using UnityEditor.XR.Management;

/// <summary>
/// One-click Android/Quest build for the simulated Winker.
/// Invoke in batchmode: -executeMethod BuildQuest.Build
/// </summary>
public static class BuildQuest
{
    const string PerTargetPath = "Assets/XR/XRGeneralSettingsPerBuildTarget.asset";
    const string LoaderPath = "Assets/XR/Loaders/OpenXRLoader.asset";
    const string FittsScenePath = "Assets/Scenes/experiments/Fitts.unity";
    const string PackageId = "com.elias.winker";

    public static void Configure()
    {
        // --- Player: IL2CPP + ARM64 (Quest 3 is 64-bit only) ---
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, PackageId);
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
        PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)0; // auto
        PlayerSettings.Android.preferredInstallLocation = AndroidPreferredInstallLocation.Auto;

        // --- XR: enable OpenXR for Android ---
        var perTarget = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(PerTargetPath);
        if (perTarget == null)
            throw new FileNotFoundException("XRGeneralSettingsPerBuildTarget not found", PerTargetPath);

        if (!perTarget.HasSettingsForBuildTarget(BuildTargetGroup.Android))
            perTarget.CreateDefaultSettingsForBuildTarget(BuildTargetGroup.Android);
        if (!perTarget.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android))
            perTarget.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);

        var manager = perTarget.ManagerSettingsForBuildTarget(BuildTargetGroup.Android);
        var loader = AssetDatabase.LoadAssetAtPath<XRLoader>(LoaderPath);
        if (loader == null)
            throw new FileNotFoundException("OpenXRLoader not found", LoaderPath);

#pragma warning disable 0618
        manager.loaders = new List<XRLoader> { loader };
#pragma warning restore 0618
        EditorUtility.SetDirty(manager);
        AssetDatabase.SaveAssets();

        // --- Build scenes ---
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(FittsScenePath, true)
        };
    }

    public static void Build()
    {
        Configure();

        bool switched = EditorUserBuildSettings.SwitchActiveBuildTarget(
            BuildTargetGroup.Android, BuildTarget.Android);
        if (!switched)
            throw new System.Exception("SwitchActiveBuildTarget to Android failed");

        string outDir = "/Users/hilith/Documents/in-progress/reproduce/Builds";
        Directory.CreateDirectory(outDir);
        string apkPath = Path.Combine(outDir, "WinkerQuest.apk");

        var options = new BuildPlayerOptions
        {
            scenes = new[] { FittsScenePath },
            locationPathName = apkPath,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.Development
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;
        Debug.Log($"Build result: {summary.result}, size={summary.totalSize}, errors={summary.totalErrors}");
        if (summary.result != BuildResult.Succeeded)
            throw new System.Exception("Build failed: " + summary.result);

        Debug.Log("APK written to " + apkPath);
    }
}
