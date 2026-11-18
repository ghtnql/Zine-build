using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Android build entry points for Zine (Unity 2022.3.62f3).
/// Mirrors ZineIOSBuild validation/config conventions without touching iOS settings.
/// </summary>
public static class ZineAndroidBuild
{
    public const string AppId = "com.HSV.Jine";

    public static void ExportApk()
    {
        Build(false);
    }

    public static void ExportBundle()
    {
        Build(true);
    }

    public static void Build(bool appBundle)
    {
        string output = (Environment.GetEnvironmentVariable("ZINE_ANDROID_OUTPUT") ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(output))
            throw new InvalidOperationException("ZINE_ANDROID_OUTPUT is required.");

        string version = (Environment.GetEnvironmentVariable("ZINE_ANDROID_VERSION") ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(version))
            throw new InvalidOperationException("ZINE_ANDROID_VERSION is required.");

        string versionCodeRaw = (Environment.GetEnvironmentVariable("ZINE_ANDROID_VERSION_CODE") ?? string.Empty).Trim();
        if (!int.TryParse(versionCodeRaw, out int versionCode) || versionCode <= 0)
            throw new InvalidOperationException("ZINE_ANDROID_VERSION_CODE must be a positive integer.");

        string keystore = Environment.GetEnvironmentVariable("ZINE_ANDROID_KEYSTORE") ?? string.Empty;
        string keyAlias = Environment.GetEnvironmentVariable("ZINE_ANDROID_KEY_ALIAS") ?? string.Empty;
        string storePass = Environment.GetEnvironmentVariable("ZINE_ANDROID_STORE_PASS") ?? string.Empty;
        string keyPass = Environment.GetEnvironmentVariable("ZINE_ANDROID_KEY_PASS") ?? string.Empty;
        if (string.IsNullOrEmpty(keystore))
            throw new InvalidOperationException("ZINE_ANDROID_KEYSTORE is required.");
        if (string.IsNullOrEmpty(keyAlias))
            throw new InvalidOperationException("ZINE_ANDROID_KEY_ALIAS is required.");
        if (string.IsNullOrEmpty(storePass))
            throw new InvalidOperationException("ZINE_ANDROID_STORE_PASS is required.");
        if (string.IsNullOrEmpty(keyPass))
            throw new InvalidOperationException("ZINE_ANDROID_KEY_PASS is required.");

        ConfigureProductionAds();

        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, AppId);
        PlayerSettings.bundleVersion = version;
        PlayerSettings.Android.bundleVersionCode = versionCode;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;
        PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)36;
        if ((int)PlayerSettings.Android.minSdkVersion < (int)AndroidSdkVersions.AndroidApiLevel23)
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel23;

        PlayerSettings.Android.useCustomKeystore = true;
        PlayerSettings.Android.keystoreName = keystore;
        PlayerSettings.Android.keyaliasName = keyAlias;
        PlayerSettings.Android.keystorePass = storePass;
        PlayerSettings.Android.keyaliasPass = keyPass;

        EditorUserBuildSettings.buildAppBundle = appBundle;
        EditorUserBuildSettings.development = false;

        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0)
            throw new InvalidOperationException("No enabled game scene.");

        string location = output;
        string wantExt = appBundle ? ".aab" : ".apk";
        if (!location.EndsWith(wantExt, StringComparison.OrdinalIgnoreCase))
            location += wantExt;
        string directory = Path.GetDirectoryName(Path.GetFullPath(location));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = location,
            target = BuildTarget.Android,
            options = BuildOptions.None
        });
        if (report == null || report.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException("Zine Android build failed: " + (report == null ? "no report" : report.summary.result.ToString()));

        AssetDatabase.SaveAssets();
        Debug.Log("ZINE_ANDROID_BUILD_OK " + report.summary.result + " " + (appBundle ? "AAB" : "APK") + " " + location
            + " version=" + version + " versionCode=" + versionCode
            + " scenes=" + scenes.Length + " size=" + report.summary.totalSize);
    }

    private static void ConfigureProductionAds()
    {
        string appId = (Environment.GetEnvironmentVariable("ZINE_ADMOB_ANDROID_APP_ID") ?? string.Empty).Trim();
        string interstitialId = (Environment.GetEnvironmentVariable("ZINE_ADMOB_ANDROID_INTERSTITIAL_ID") ?? string.Empty).Trim();

        if (!Regex.IsMatch(appId, @"^ca-app-pub-[0-9]+~[0-9]+$"))
            throw new InvalidOperationException("ZINE_ADMOB_ANDROID_APP_ID is missing or invalid.");
        if (!Regex.IsMatch(interstitialId, @"^ca-app-pub-[0-9]+/[0-9]+$"))
            throw new InvalidOperationException("ZINE_ADMOB_ANDROID_INTERSTITIAL_ID is missing or invalid.");

        string appPublisher = appId.Substring("ca-app-pub-".Length).Split('~')[0];
        string unitPublisher = interstitialId.Substring("ca-app-pub-".Length).Split('/')[0];
        if (appPublisher != unitPublisher)
            throw new InvalidOperationException("AdMob app and interstitial IDs must use the same publisher.");
        bool allowTestAds = Environment.GetEnvironmentVariable("ZINE_ALLOW_TEST_ADS") == "1";
        if (appPublisher == "3940256099942544" && !allowTestAds)
            throw new InvalidOperationException("Google sample AdMob IDs cannot be used for production export.");

        const string runtimePath = "Assets/Resources/ZineAdConfig.asset";
        UnityEngine.Object runtime = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(runtimePath);
        if (runtime == null)
            throw new InvalidOperationException("Existing Resources/ZineAdConfig.asset is missing.");

        SerializedObject runtimeSerialized = new SerializedObject(runtime);
        SerializedProperty androidUnit = runtimeSerialized.FindProperty("androidInterstitialId");
        if (androidUnit == null)
            throw new InvalidOperationException("ZineAdConfig schema changed: androidInterstitialId missing.");
        androidUnit.stringValue = interstitialId;
        runtimeSerialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(runtime);

        const string settingsPath = "Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset";
        UnityEngine.Object settings = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(settingsPath);
        if (settings == null)
            throw new InvalidOperationException("Google Mobile Ads settings asset is missing.");

        SerializedObject serialized = new SerializedObject(settings);
        SerializedProperty androidId = serialized.FindProperty("adMobAndroidAppId");
        if (androidId == null)
            throw new InvalidOperationException("Google Mobile Ads settings schema changed: adMobAndroidAppId missing.");
        androidId.stringValue = appId;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Configured production AdMob for Zine Android publisher " + appPublisher + ".");
    }
}
