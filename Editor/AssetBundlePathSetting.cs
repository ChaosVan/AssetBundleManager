using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif

namespace AssetBundles
{
    [System.Serializable, System.Obsolete]
    public struct AssetBundlePathSetting
    {
        public string name;
        public string variant;
#if ODIN_INSPECTOR
        [FolderPath]
#endif
        public string path;
        public bool separated;
        public string ignore;
        public string comment;


#if ODIN_INSPECTOR
        [Button(ButtonSizes.Small), ButtonGroup]
#endif
        private void SetName()
        {
            bool success = AssetBundleNamesConfig.SetBundleName(path, name, variant, string.IsNullOrEmpty(ignore) ? null : ignore.Split(','), separated, true, out string errMsg);

            AssetDatabase.RemoveUnusedAssetBundleNames();
            AssetDatabase.SaveAssets();
            if (!Application.isBatchMode)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("AssetBundleNamesSetting", success ? "Success" : errMsg, "Close");
            }

        }

#if ODIN_INSPECTOR
        [Button(ButtonSizes.Small), ButtonGroup]
#endif
        private void ClearName()
        {
            bool success = AssetBundleNamesConfig.CleanAssetBundleNames(path, out string errMsg);

            AssetDatabase.RemoveUnusedAssetBundleNames();
            AssetDatabase.SaveAssets();
            if (!Application.isBatchMode)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("AssetBundleNamesSetting", success ? "Done!" : errMsg, "Close");
            }
        }
    }
}
