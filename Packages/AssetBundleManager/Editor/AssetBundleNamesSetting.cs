using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.U2D;
using System.IO;
using UnityEngine.U2D;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif

namespace AssetBundles
{
    [System.Serializable]
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
        public void SetName()
        {
            bool success = true;
            string errMsg = string.Empty;
            if (Directory.Exists(path))
            {
                if (!AssetBundleNamesSetting.SetBundleName(path, name, variant, string.IsNullOrEmpty(ignore) ? null : ignore.Split(','), separated, true, out errMsg))
                {
                    success = false;
                }
            }
            else
            {
                success = false;
                errMsg = string.Format("Setting Path is not exist: {0}", path);
            }

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
        public void ClearName()
        {
            bool success = AssetBundleNamesSetting.CleanAssetBundleNames(path, out string errMsg);

            AssetDatabase.RemoveUnusedAssetBundleNames();
            AssetDatabase.SaveAssets();
            if (!Application.isBatchMode)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("AssetBundleNamesSetting", success ? "Done!" : errMsg, "Close");
            }
        }
    }

    [System.Serializable]
    [CreateAssetMenu(fileName = "AssetBundleNamesSetting", menuName = "AssetBundleNamesSetting", order = 999 * 2 + 1)]
    public class AssetBundleNamesSetting : ScriptableObject
    {
        public List<AssetBundlePathSetting> list;

        static bool CheckIgnored(string fileName, string[] ignores)
        {
            if (ignores != null)
            {
                foreach (string ignore in ignores)
                {
                    if (fileName.Contains(ignore))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

#if ODIN_INSPECTOR
        [Button(ButtonSizes.Medium), ButtonGroup]
#endif
        public void SetBundleNames()
        {
            bool success = true;
            string errMsg = string.Empty;
            foreach (AssetBundlePathSetting setting in list)
            {
                if (Directory.Exists(setting.path))
                {
                    if (!SetBundleName(setting.path, setting.name, setting.variant, string.IsNullOrEmpty(setting.ignore) ? null : setting.ignore.Split(','), setting.separated, true, out errMsg))
                    {
                        success = false;
                        break;
                    }
                }
                else
                {
                    success = false;
                    errMsg = string.Format("Setting Path is not exist: {0}", setting.path);
                    break;
                }
            }

            AssetDatabase.RemoveUnusedAssetBundleNames();
            AssetDatabase.SaveAssets();
            if (!Application.isBatchMode)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("AssetBundleNamesSetting", success ? "Success" : errMsg, "Close");
            }
            else
            {
                if (!success)
                    throw new System.Exception(errMsg);
            }
        }

        static public bool SetBundleName(string directory, string name, string variant, string[] ignores, bool separateFile, bool separateSubFolder, out string err)
        {
            err = string.Empty;

            string[] files = Directory.GetFiles(directory);
            string[] subFolders = Directory.GetDirectories(directory);
            bool hasSubFolders = subFolders.Length > 0 && separateSubFolder;

            // handle files
            for (int i = 0; i < files.Length; i++)
            {
                string ext = Path.GetExtension(files[i]);
                if (".meta".Equals(ext) || ".cs".Equals(ext) || ".js".Equals(ext) || ".DS_Store".Equals(ext))
                    continue;

                string fileName = Path.GetFileName(files[i]);

                if (fileName.StartsWith(".", System.StringComparison.Ordinal))
                    continue;

                if (CheckIgnored(fileName, ignores))
                    continue;

                AssetImporter importer = AssetImporter.GetAtPath(files[i]);

                if (importer == null)
                {
                    Debug.LogError(files[i]);
                    err = files[i];
                    return false;
                }

                string assetBundleName = string.Empty;
                if (separateFile || hasSubFolders)
                {
                    assetBundleName = name + "/" + Path.GetFileNameWithoutExtension(files[i]);
                }
                else
                {
                    assetBundleName = name;
                }

                assetBundleName = assetBundleName.ToLower();
                variant = variant.ToLower();

                if (!importer.assetBundleName.Equals(assetBundleName))
                    importer.assetBundleName = assetBundleName;
                if (!importer.assetBundleVariant.Equals(variant))
                    importer.assetBundleVariant = variant;

                if (assetBundleName.Split('/').Length >= 7)
                    Debug.LogWarningFormat("{0}: folder depth exceeds the limit", assetBundleName);

                if (!Application.isBatchMode && EditorUtility.DisplayCancelableProgressBar("Set: " + assetBundleName, files[i], 1f * i / files.Length))
                {
                    err = "User Canceled";
                    return false;
                }

                // handle spriteatlas ref folder
                if (".spriteatlas".Equals(ext))
                {
                    SpriteAtlas atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(files[i]);
                    Object[] objs = atlas.GetPackables();
                    foreach (Object obj in objs)
                    {
                        string objPath = AssetDatabase.GetAssetPath(obj);
                        if (Directory.Exists(objPath))
                        {
                            AssetImporter imp = AssetImporter.GetAtPath(objPath);
                            if (!imp.assetBundleName.Equals(assetBundleName))
                                imp.assetBundleName = assetBundleName;
                            if (!imp.assetBundleVariant.Equals(variant))
                                imp.assetBundleVariant = variant;

                            if (!SetBundleName(objPath, assetBundleName, variant, ignores, false, false, out err)) return false;
                        }
                        else
                        {
                            AssetImporter imp = AssetImporter.GetAtPath(objPath);
                            if (imp != null)
                            {
                                if (!imp.assetBundleName.Equals(assetBundleName))
                                    imp.assetBundleName = assetBundleName;
                                if (!imp.assetBundleVariant.Equals(variant))
                                    imp.assetBundleVariant = variant;
                            }
                        }
                    }
                }
            }

            // handle sub folders
            for (int i = 0; i < subFolders.Length; i++)
            {
                if (subFolders[i].Contains(".DS_Store"))
                    continue;

                if (CheckIgnored(subFolders[i], ignores))
                    continue;

                if (separateSubFolder)
                {
                    if (!SetBundleName(subFolders[i], name + subFolders[i].Replace(directory, "").ToLower(), variant, ignores, separateFile, separateSubFolder, out err)) return false;
                }
                else
                {
                    if (!SetBundleName(subFolders[i], name, variant, ignores, separateFile, separateSubFolder, out err)) return false;
                }
            }

            return true;
        }

        [MenuItem("Assets/AssetBundles/CleanAssetBundleNames", false, 2001)]
        static void CleanAssetBundleNames()
        {
            bool success = true;
            string errMsg = string.Empty;

            Object[] objects = Selection.GetFiltered(typeof(Object), SelectionMode.Assets);
            for (int i = 0; i < objects.Length; ++i)
            {
                if (!CleanAssetBundleNames(AssetDatabase.GetAssetPath(objects[i]), out errMsg))
                {
                    success = false;
                    break;
                }
            }

            AssetDatabase.RemoveUnusedAssetBundleNames();
            AssetDatabase.SaveAssets();
            if (!Application.isBatchMode)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("AssetBundleNamesSetting", success ? "Done!" : errMsg, "Close");
            }
        }

        [MenuItem("Assets/AssetBundles/CleanAssetBundleNames", true)]
        static bool CleanAssetBundleNamesValidate()
        {
            Object[] objects = Selection.GetFiltered(typeof(Object), SelectionMode.Assets);
            return objects.Length > 0;
        }

        static public bool CleanAssetBundleNames(string directory, out string err)
        {
            err = string.Empty;
            if (Directory.Exists(directory)) // Folder
            {
                err = string.Empty;

                // handle self
                AssetImporter importer = AssetImporter.GetAtPath(directory);
                if (!string.IsNullOrEmpty(importer.assetBundleName))
                    importer.assetBundleName = null;


                string[] files = Directory.GetFiles(directory);
                string[] subFolders = Directory.GetDirectories(directory);

                // handle files
                for (int i = 0; i < files.Length; ++i)
                {
                    string ext = Path.GetExtension(files[i]);
                    if (".meta".Equals(ext) || ".cs".Equals(ext) || ".js".Equals(ext) || ".DS_Store".Equals(ext))
                        continue;

                    if (!CleanAssetBundleNames(files[i], out err)) return false;

                    if (!Application.isBatchMode && EditorUtility.DisplayCancelableProgressBar("Clear assetbundle names", directory, 1f * i / files.Length))
                    {
                        err = "User Canceled!";
                        return false;
                    }
                }

                // handle sub folders
                for (int i = 0; i < subFolders.Length; ++i)
                {
                    if (subFolders[i].Contains(".DS_Store"))
                        continue;

                    if (subFolders[i].Contains("Plugins"))
                        continue;

                    if (subFolders[i].Contains("Scripts"))
                        continue;

                    if (subFolders[i].Contains("StreamingAssets"))
                        continue;

                    if (!CleanAssetBundleNames(subFolders[i], out err)) return false;
                }

                return true;
            }
            else
            {
                AssetImporter importer = AssetImporter.GetAtPath(directory);
                if (importer != null)
                {
                    if (!string.IsNullOrEmpty(importer.assetBundleName))
                        importer.assetBundleName = null;
                }

                return true;
            }
        }

#if ODIN_INSPECTOR
        [Button(ButtonSizes.Medium), ButtonGroup]
#endif
        [MenuItem("Assets/AssetBundles/CleanAllAssetBundleNames", false, 3001)]
        static public void CleanAllAssetBundleNames()
        {
            if (Application.isBatchMode || EditorUtility.DisplayDialog("Notice!", "It will clean all the assetbundle names of assets and take a very long time, make sure to do this?", "yes", "no"))
            {
                bool success = CleanAssetBundleNames("Assets", out string errMsg);

                AssetDatabase.RemoveUnusedAssetBundleNames();
                AssetDatabase.SaveAssets();
                if (!Application.isBatchMode)
                {
                    EditorUtility.ClearProgressBar();
                    EditorUtility.DisplayDialog("AssetBundleNamesSetting", success ? "Done!" : errMsg, "Close");
                }
            }
        }

        [MenuItem("Assets/AssetBundles/SetAllAssetBundleNames", false, 3002)]
        static public void SetAllAssetBundleNames()
        {
            string[] files = Directory.GetFiles("Assets", "*.asset", SearchOption.AllDirectories);
            foreach (string file in files)
            {
                System.Type t = AssetDatabase.GetMainAssetTypeAtPath(file);
                if (t == typeof(AssetBundleNamesSetting))
                {
                    AssetBundleNamesSetting setting = AssetDatabase.LoadAssetAtPath(file, t) as AssetBundleNamesSetting;
                    setting.SetBundleNames();
                }
            }

        }
    }
}

