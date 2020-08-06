using UnityEngine;
using UnityEngine.Assertions;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AssetBundles
{
    public class Utility
    {
        public const string AssetBundlesOutputPath = "AssetBundles";

        public static string GetStreamingAssetsUrl()
        {
            if (Application.isEditor)
                return "file://" + Path.Combine(System.Environment.CurrentDirectory.Replace("\\", "/"), AssetBundlesOutputPath, GetPlatformName()); // Use the build output folder directly.
            else if (Application.platform == RuntimePlatform.Android || Application.isConsolePlatform)
                return Application.streamingAssetsPath;
            else // standalone player and iphone player.
                return "file://" + Application.streamingAssetsPath;
        }

        public static string GetStreamingAssetsPath()
        {
            if (Application.isEditor)
                return Path.Combine(System.Environment.CurrentDirectory.Replace("\\", "/"), AssetBundlesOutputPath, GetPlatformName()); // Use the build output folder directly.
            else if (Application.platform == RuntimePlatform.Android)
                return Application.dataPath + "!assets";
            else // todo console platform maybe can not run well
                return Application.streamingAssetsPath;
        }

        public static void ParseAssetPath(string assetPath, bool isSeparatedAsset, out string assetName, out string assetBundle)
        {
            Assert.IsTrue(!string.IsNullOrEmpty(assetPath), "asset path is null or empty");
            
            assetName = Path.GetFileNameWithoutExtension(assetPath);
            int length = assetPath.LastIndexOf(isSeparatedAsset ? '.' : '/');
            if (length != -1)
                assetBundle = assetPath.Substring(0, length).ToLower();
            else
                assetBundle = assetPath.ToLower();
        }

        public static string GetPlatformName()
        {
#if UNITY_EDITOR
            return GetPlatformForAssetBundles(EditorUserBuildSettings.activeBuildTarget);
#else
			return GetPlatformForAssetBundles(Application.platform);
#endif
        }

#if UNITY_EDITOR
        private static string GetPlatformForAssetBundles(BuildTarget target)
        {
            switch (target)
            {
                case BuildTarget.Android:
                    return "Android";
                case BuildTarget.iOS:
                    return "iOS";
                case BuildTarget.WebGL:
                    return "WebGL";
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    return "StandaloneWindows";
                case BuildTarget.StandaloneOSX:
                    return "StandaloneOSXUniversal";
                case BuildTarget.PS4:
                    return "PS4";
                case BuildTarget.XboxOne:
                    return "XboxOne";
                // Add more build targets for your own.
                // If you add more targets, don't forget to add the same platforms to GetPlatformForAssetBundles(RuntimePlatform) function.
                default:
                    return string.Empty;
            }
        }
#endif

        private static string GetPlatformForAssetBundles(RuntimePlatform platform)
        {
            switch (platform)
            {
                case RuntimePlatform.Android:
                    return "Android";
                case RuntimePlatform.IPhonePlayer:
                    return "iOS";
                case RuntimePlatform.WebGLPlayer:
                    return "WebGL";
                case RuntimePlatform.WindowsPlayer:
                    return "StandaloneWindows";
                case RuntimePlatform.OSXPlayer:
                    return "StandaloneOSXUniversal";
                case RuntimePlatform.PS4:
                    return "PS4";
                case RuntimePlatform.XboxOne:
                    return "XboxOne";
                // Add more build targets for your own.
                // If you add more targets, don't forget to add the same platforms to GetPlatformForAssetBundles(RuntimePlatform) function.
                default:
                    return string.Empty;
            }
        }
    }
}