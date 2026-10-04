using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS.Xcode;
using UnityEngine;

public static class ZineIOSBuild
{
    public const string BundleId = "com.ghtnql.zine3d";

    public static void Export()
    {
        string output = Environment.GetEnvironmentVariable("ZINE_IOS_EXPORT") ?? "Builds/iOS";
        output = Path.GetFullPath(output);
        if (!output.StartsWith(Path.GetFullPath("Builds") + Path.DirectorySeparatorChar))
            throw new InvalidOperationException("iOS export must be inside this project's Builds directory.");

        Configure();

        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0) throw new InvalidOperationException("No enabled game scene.");
        Directory.CreateDirectory(output);
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes, locationPathName = output,
            target = BuildTarget.iOS, options = BuildOptions.None
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException("Zine iOS export failed: " + report.summary.result);
        string pbxPath = PBXProject.GetPBXProjectPath(output);
        PBXProject project = new PBXProject();
        project.ReadFromFile(pbxPath);
        string app = project.GetUnityMainTargetGuid();
        string framework = project.GetUnityFrameworkTargetGuid();
        project.SetBuildProperty(app, "CODE_SIGN_STYLE", "Manual");
        project.SetBuildProperty(app, "PROVISIONING_PROFILE_SPECIFIER", "Zine3D-AppStore");
        project.SetBuildProperty(app, "CODE_SIGN_IDENTITY", "Apple Distribution");
        project.SetBuildProperty(framework, "CODE_SIGN_STYLE", "Manual");
        project.SetBuildProperty(framework, "PROVISIONING_PROFILE_SPECIFIER", "");
        project.SetBuildProperty(framework, "CODE_SIGN_IDENTITY", "Apple Distribution");
        // Keep Unity's required-reason declarations and disclose optional ranking data.
        string privacyPath = Path.Combine(output, "PrivacyInfo.xcprivacy");
        PlistDocument privacy = new PlistDocument();
        if (File.Exists(privacyPath)) privacy.ReadFromFile(privacyPath);
        else privacy.Create();
        privacy.root.SetBoolean("NSPrivacyTracking", false);
        privacy.root.CreateArray("NSPrivacyTrackingDomains");
        PlistElementArray collected = privacy.root.CreateArray("NSPrivacyCollectedDataTypes");
        foreach (string type in new[] { "NSPrivacyCollectedDataTypeUserID", "NSPrivacyCollectedDataTypeGameplayContent", "NSPrivacyCollectedDataTypeOtherDataTypes" })
        {
            PlistElementDict entry = collected.AddDict();
            entry.SetString("NSPrivacyCollectedDataType", type);
            entry.SetBoolean("NSPrivacyCollectedDataTypeLinked", type != "NSPrivacyCollectedDataTypeOtherDataTypes");
            entry.SetBoolean("NSPrivacyCollectedDataTypeTracking", false);
            entry.CreateArray("NSPrivacyCollectedDataTypePurposes").AddString("NSPrivacyCollectedDataTypePurposeAppFunctionality");
        }
        privacy.WriteToFile(privacyPath);
        if (project.FindFileGuidByProjectPath("PrivacyInfo.xcprivacy") == null)
        {
            string resource = project.AddFile("PrivacyInfo.xcprivacy", "PrivacyInfo.xcprivacy", PBXSourceTree.Source);
            project.AddFileToBuild(app, resource);
        }
        project.WriteToFile(pbxPath);
        PlistDocument plist = new PlistDocument();
        string plistPath = Path.Combine(output, "Info.plist");
        plist.ReadFromFile(plistPath);
        plist.root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
        plist.WriteToFile(plistPath);
        File.WriteAllText(Path.Combine(output, "ZINE_EXPORT.json"), JsonUtility.ToJson(new ExportInfo
        {
            bundleId = BundleId, version = PlayerSettings.bundleVersion,
            build = PlayerSettings.iOS.buildNumber,
            sourceCommit = Environment.GetEnvironmentVariable("ZINE_SOURCE_COMMIT") ?? "uncommitted",
            unityVersion = Application.unityVersion
        }, true));
        Debug.Log("ZINE_IOS_EXPORT_OK " + output);
    }

    public static void Configure()
    {
        PlayerSettings.productName = "지네";
        PlayerSettings.bundleVersion = "2.4.0";
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId);
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.iOS, ManagedStrippingLevel.Low);
        PlayerSettings.iOS.buildNumber = Environment.GetEnvironmentVariable("ZINE_IOS_BUILD_NUMBER") ?? "1";
        PlayerSettings.iOS.targetOSVersionString = "15.0";
        PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneOnly;
        PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
        PlayerSettings.iOS.appleEnableAutomaticSigning = false;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        PlayerSettings.statusBarHidden = true;

        // Reuse the established game's icon; Unity produces the required iOS sizes.
        Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/03. Images/main.png");
        if (icon == null) throw new InvalidOperationException("Existing game icon is missing.");
        string iconPath = "Assets/Editor/ZineAppStoreIcon.png";
        if (!File.Exists(iconPath))
        {
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(icon));
            bool wasReadable = importer.isReadable;
            if (!wasReadable) { importer.isReadable = true; importer.SaveAndReimport(); }
            icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/03. Images/main.png");
            Texture2D opaqueIcon = new Texture2D(1024, 1024, TextureFormat.RGB24, false);
            Color background = new Color(0.02f, 0.08f, 0.035f, 1f);
            for (int y = 0; y < 1024; y++)
                for (int x = 0; x < 1024; x++)
                {
                    Color pixel = icon.GetPixelBilinear((x + 0.5f) / 1024f, (y + 0.5f) / 1024f);
                    opaqueIcon.SetPixel(x, y, Color.Lerp(background, new Color(pixel.r, pixel.g, pixel.b, 1f), pixel.a));
                }
            opaqueIcon.Apply();
            File.WriteAllBytes(iconPath, opaqueIcon.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(opaqueIcon);
            if (!wasReadable) { importer.isReadable = false; importer.SaveAndReimport(); }
            AssetDatabase.ImportAsset(iconPath);
        }
        icon = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
        foreach (PlatformIconKind kind in PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.iOS))
        {
            PlatformIcon[] icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.iOS, kind);
            foreach (PlatformIcon slot in icons) slot.SetTexture(icon);
            PlayerSettings.SetPlatformIcons(NamedBuildTarget.iOS, kind, icons);
        }

        AssetDatabase.SaveAssets();
    }

    [Serializable]
    private class ExportInfo
    {
        public string bundleId, version, build, sourceCommit, unityVersion;
    }
}
