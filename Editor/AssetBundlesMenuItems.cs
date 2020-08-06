using UnityEngine;
using UnityEditor;
using System.Collections;

namespace AssetBundles
{
    public static class AssetBundlesMenuItems
    {
        const string kSimulationMode = "Assets/AssetBundles/Simulation Mode";

        [MenuItem(kSimulationMode)]
        public static void ToggleSimulationMode()
        {
            AssetBundleManager.SimulateAssetBundleInEditor = !AssetBundleManager.SimulateAssetBundleInEditor;
        }

        [MenuItem(kSimulationMode, true)]
        public static bool ToggleSimulationModeValidate()
        {
            Menu.SetChecked(kSimulationMode, AssetBundleManager.SimulateAssetBundleInEditor);
            return true;
        }

        [MenuItem("Assets/AssetBundles/Build AssetBundles")]
        static public void BuildAssetBundles()
        {
            if (!BuildScript.BuildAssetBundles(BuildAssetBundleOptions.StrictMode))
                throw new System.Exception("Build assetbundles failed!!!");
        }

        [MenuItem("Assets/AssetBundles/Rebuild AssetBundles")]
        static public void RebuildAssetBundles()
        {
            if (!BuildScript.BuildAssetBundles(BuildAssetBundleOptions.ForceRebuildAssetBundle | BuildAssetBundleOptions.StrictMode))
                throw new System.Exception("Rebuild assetbundles failed!!!");
        }

        [MenuItem("Assets/AssetBundles/Validate AssetBundles")]
        static public void ValidateAssetBundles()
        {
            if (!BuildScript.ValidateAssetBundles())
                throw new System.Exception("Validate assetbundles failed!!!");
        }
    }
}