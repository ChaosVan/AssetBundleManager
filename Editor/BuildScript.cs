using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace AssetBundles
{
    public static class BuildScript
    {

        public static bool BuildAssetBundles(BuildAssetBundleOptions assetbundleOptions = BuildAssetBundleOptions.None)
        {
            // Choose the output path according to the build target.
            string outputPath = Path.Combine(Utility.AssetBundlesOutputPath, Utility.GetPlatformName());
            if (!Directory.Exists(outputPath))
                Directory.CreateDirectory(outputPath);

            //@TODO: use append hash... (Make sure pipeline works correctly with it.)

            AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(outputPath, assetbundleOptions, EditorUserBuildSettings.activeBuildTarget);
            if (manifest != null)
            {
                if (!Application.isBatchMode)
                    EditorUtility.DisplayDialog("", "Success!", "OK");
                return true;
            }
            else
            {
                if (!Application.isBatchMode)
                    EditorUtility.DisplayDialog("", "Failure!", "OK");
                return false;
            }
        }

        static void ValidateAssetBundle(string bundleName, ref Dictionary<string, string> errors)
        {
            string path = Utility.GetStreamingAssetsDirectory();
            string bundlePath = Path.Combine(path, bundleName);

            try
            {
                AssetBundle bundle = AssetBundle.LoadFromFile(bundlePath);
                if (bundle != null)
                    bundle.Unload(true);
            } catch (System.Exception e)
            {
                errors.Add(bundleName, e.Message);
            }
        }

        public static bool ValidateAssetBundles()
        {
            string path = Utility.GetStreamingAssetsDirectory();
            Debug.LogFormat("AssetBundle Path = {0}", path);

            Dictionary<string, string> errors = new Dictionary<string, string>();
            ValidateAssetBundle(Utility.GetPlatformName(), ref errors);

            string[] bundles = AssetDatabase.GetAllAssetBundleNames();
            float p = 0;
            foreach (var bundleName in bundles)
            {
                if (!Application.isBatchMode)
                    EditorUtility.DisplayProgressBar("ValidateAssetBundles", bundleName, p++ * 1f / bundles.Length);
                ValidateAssetBundle(bundleName, ref errors);
            }

            if (!Application.isBatchMode)
                EditorUtility.ClearProgressBar();

            if (errors.Count > 0)
            {
                foreach (var error in errors)
                {
                    Debug.LogErrorFormat("[{0}]{1}", error.Key, error.Value);
                }
                return false;
            }

            return true;
        }
    }
}