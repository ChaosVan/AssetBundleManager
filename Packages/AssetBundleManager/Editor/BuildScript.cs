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

        static void ValidateAssetBundle(string bundleName, ref List<string> errList)
        {
            string path = Utility.GetStreamingAssetsDirectory();
            string bundlePath = Path.Combine(path, bundleName);
            AssetBundle bundle = AssetBundle.LoadFromFile(bundlePath);
            if (bundle != null)
                bundle.Unload(true);
            else
                errList.Add(bundleName);
        }

        public static bool ValidateAssetBundles()
        {
            string path = Utility.GetStreamingAssetsDirectory();
            Debug.LogFormat("AssetBundle Path = {0}", path);

            List<string> errBundles = new List<string>();
            ValidateAssetBundle(Utility.GetPlatformName(), ref errBundles);

            string[] bundles = AssetDatabase.GetAllAssetBundleNames();
            float p = 0;
            foreach (var bundleName in bundles)
            {
                if (!Application.isBatchMode)
                    EditorUtility.DisplayProgressBar("ValidateAssetBundles", bundleName, p++ * 1f / bundles.Length);
                ValidateAssetBundle(bundleName, ref errBundles);
            }

            if (!Application.isBatchMode)
                EditorUtility.ClearProgressBar();

            if (errBundles.Count > 0)
            {
                foreach (var str in errBundles)
                {
                    Debug.LogErrorFormat("{0} has something err", str);
                }
                return false;
            }

            return true;
        }
    }
}