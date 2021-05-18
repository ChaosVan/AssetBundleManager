using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using UnityEngine.U2D;
using UnityEditor.U2D;

#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif

namespace AssetBundles
{
    [System.Serializable]
    [CreateAssetMenu(fileName = "AssetBundleNamesConfig", menuName = "AssetBundleNamesConfig", order = 999 * 2 + 1)]
    public class AssetBundleNamesConfig : ScriptableObject
    {
        public bool useLegacyMode = false;

#if ODIN_INSPECTOR
        [ShowIf("useLegacyMode")]
#endif
        public List<AssetBundlePathSetting> list;

#if ODIN_INSPECTOR
        [HideIf("useLegacyMode")]
#endif
        public List<AssetBundleGroupSetting> groups;

        private static AssetBundleConfig config = new AssetBundleConfig();

#if ODIN_INSPECTOR
        [Button(ButtonSizes.Medium), ButtonGroup]
#endif
        public void ApplyAssetBundleNames()
        {
            bool success = false;
            string errMsg = "list is null";

            if (useLegacyMode)
            {
                foreach (AssetBundlePathSetting setting in list)
                {
                    success = SetBundleName(setting.path, setting.name, setting.variant, ParseIgnores(setting.ignore), setting.separated, true, out errMsg);
                }
            }
            else
            {
                config.Clear();

                foreach (AssetBundleGroupSetting group in groups)
                {
                    success = SetBundleName(group, out errMsg);
                }

                var content = JsonUtility.ToJson(config);
                if (!Directory.Exists(Application.streamingAssetsPath))
                    Directory.CreateDirectory(Application.streamingAssetsPath);

                File.WriteAllText(Path.Combine(Application.streamingAssetsPath, "AssetBundleInfo.json"), content);
            }

            AssetDatabase.RemoveUnusedAssetBundleNames();
            AssetDatabase.SaveAssets();
            if (!Application.isBatchMode)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("AssetBundleNamesSetting", success ? "Success" : errMsg, "Close");
            }
        }

        private static bool CheckIgnored(string fileName, string[] ignores)
        {
            if (ignores != null)
            {
                foreach (string ignore in ignores)
                {
                    if (ignore.StartsWith("!"))
                    {
                        var word = ignore.Substring(1);
                        if (!fileName.Contains(word))
                        {
                            return true;
                        }
                    }
                    else if (fileName.Contains(ignore))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static string[] ParseIgnores(string ignore)
        {
            return string.IsNullOrEmpty(ignore) ? null : ignore.Split(',');
        }

        [MenuItem("Assets/AssetBundles/SetAllAssetBundleNames", false, 3002)]
        static public void SetAllAssetBundleNames()
        {
            string[] files = Directory.GetFiles("Assets", "*.asset", SearchOption.AllDirectories);
            foreach (string file in files)
            {
                System.Type t = AssetDatabase.GetMainAssetTypeAtPath(file);
                if (t == typeof(AssetBundleNamesConfig))
                {
                    AssetBundleNamesConfig config = AssetDatabase.LoadAssetAtPath(file, t) as AssetBundleNamesConfig;
                    config.ApplyAssetBundleNames();
                }
            }

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

        static public bool CleanAssetBundleNames(string path, out string err)
        {
            err = string.Empty;

            // handle self
            CleanFile(path);

            if (Directory.Exists(path)) // Folder
            {
                string[] files = Directory.GetFiles(path);
                string[] subFolders = Directory.GetDirectories(path);

                // handle files
                for (int i = 0; i < files.Length; ++i)
                {
                    if (!Application.isBatchMode && EditorUtility.DisplayCancelableProgressBar("Clear assetbundle names", files[i], 1f * i / files.Length))
                    {
                        err = "User Canceled!";
                        return false;
                    }

                    string ext = Path.GetExtension(files[i]);
                    if (".meta".Equals(ext) || ".cs".Equals(ext) || ".js".Equals(ext) || ".DS_Store".Equals(ext))
                        continue;

                    if (!CleanAssetBundleNames(files[i], out err)) return false;

                    // handle spriteatlas ref folder
                    if (".spriteatlas".Equals(ext) || ".spriteatlasv2".Equals(ext))
                    {
                        SpriteAtlas atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(files[i]);
                        Object[] objs = atlas.GetPackables();
                        foreach (Object obj in objs)
                        {
                            string objPath = AssetDatabase.GetAssetPath(obj);
                            if (!CleanAssetBundleNames(objPath, out err)) return false;
                        }
                    }
                }

                // handle sub folders
                for (int i = 0; i < subFolders.Length; ++i)
                {
                    if (subFolders[i].StartsWith("."))
                        continue;

                    if (subFolders[i].Contains("Editor"))
                        continue;

                    if (subFolders[i].Contains("Plugins"))
                        continue;

                    if (subFolders[i].Contains("Scripts"))
                        continue;

                    if (subFolders[i].Contains("StreamingAssets"))
                        continue;

                    if (!CleanAssetBundleNames(subFolders[i], out err)) return false;
                }
            }

            return true;
        }

        [System.Obsolete]
        static public bool SetBundleName(string directory, string name, string variant, string[] ignores, bool separateFile, bool separateSubFolder, out string err)
        {
            err = string.Empty;

            if (!Directory.Exists(directory))
            {
                err = string.Format("Setting Path is not exist: {0}", directory);
                return false;
            }

            string[] files = Directory.GetFiles(directory);
            string[] subFolders = Directory.GetDirectories(directory);
            bool hasSubFolders = subFolders.Length > 0 && separateSubFolder;

            // handle files
            for (int i = 0; i < files.Length; i++)
            {
                if (!Application.isBatchMode && EditorUtility.DisplayCancelableProgressBar("Set: " + name, files[i], 1f * i / files.Length))
                {
                    err = "User Canceled";
                    return false;
                }

                string ext = Path.GetExtension(files[i]);
                if (".meta".Equals(ext) || ".cs".Equals(ext) || ".js".Equals(ext) || ".DS_Store".Equals(ext))
                    continue;

                string fileName = Path.GetFileName(files[i]);

                if (fileName.StartsWith(".", System.StringComparison.Ordinal))
                    continue;

                if (CheckIgnored(fileName, ignores))
                    continue;

                string assetBundleName;
                if (separateFile || hasSubFolders)
                {
                    assetBundleName = name + "/" + Path.GetFileNameWithoutExtension(files[i]);
                }
                else
                {
                    assetBundleName = name;
                }

                SetFile(files[i], assetBundleName, variant);

                // handle spriteatlas ref folder
                if (".spriteatlas".Equals(ext) || ".spriteatlasv2".Equals(ext))
                {
                    SpriteAtlas atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(files[i]);
                    Object[] objs = atlas.GetPackables();
                    foreach (Object obj in objs)
                    {
                        string objPath = AssetDatabase.GetAssetPath(obj);
                        if (Directory.Exists(objPath))
                        {
                            if (!SetBundleName(objPath, assetBundleName, variant, ignores, false, false, out err)) return false;
                        }
                        else
                        {
                            SetFile(objPath, assetBundleName, variant);
                        }
                    }
                }
            }

            // handle sub folders
            for (int i = 0; i < subFolders.Length; i++)
            {
                if (subFolders[i].StartsWith("."))
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

        static public bool SetBundleName(AssetBundleGroupSetting group, out string errMsg)
        {
            errMsg = string.Empty;

            if (!Directory.Exists(group.directory))
            {
                errMsg = string.Format("Directory is not exist: {0}", group.directory);
                return false;
            }

            string[] files = Directory.GetFiles(group.directory, "*", SearchOption.AllDirectories);
            if (files == null || files.Length == 0)
            {
                errMsg = string.Format("Directory is empty: {0}", group.directory);
                return false;
            }

            var ignores = ParseIgnores(group.ignore);
            var separator = group.GetSeparator();

            float p = 0;
            foreach (var file in files)
            {
                if (!Application.isBatchMode && EditorUtility.DisplayCancelableProgressBar("Processing: " + group.directory, file, p++ / files.Length))
                {
                    errMsg = "User Canceled";
                    return false;
                }

                string ext = Path.GetExtension(file);
                if (".meta".Equals(ext) || ".cs".Equals(ext) || ".js".Equals(ext) || ".DS_Store".Equals(ext))
                    continue;

                string fileName = Path.GetFileName(file);

                if (fileName.StartsWith(".", System.StringComparison.Ordinal))
                    continue;

                if (CheckIgnored(fileName, ignores))
                    continue;

                string packName = "";
                switch (group.mode)
                {
                    case PackMode.PackTogether:
                        packName = group.directory.Replace("/", separator);
                        break;
                    case PackMode.PackTogetherByGroup:
                        packName = group.name.Replace("/", separator);
                        break;
                    case PackMode.PackSeparately:
                        packName = Path.Combine(Path.GetDirectoryName(file), Path.GetFileNameWithoutExtension(file)).Replace(Path.DirectorySeparatorChar.ToString(), separator);
                        break;
                    case PackMode.PackSeparatelyByFolder:
                        packName = Path.GetDirectoryName(file).Replace(Path.DirectorySeparatorChar.ToString(), separator);
                        break;
                }

                SetFile(file, packName, group.variant, group.important);

                // handle spriteatlas reference folder
                if (".spriteatlas".Equals(ext) || ".spriteatlasv2".Equals(ext))
                {
                    SpriteAtlas atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(file);
                    Object[] objs = atlas.GetPackables();
                    foreach (Object obj in objs)
                    {
                        string objPath = AssetDatabase.GetAssetPath(obj);
                        if (Directory.Exists(objPath))
                        {
                            var spriteGroup = new AssetBundleGroupSetting
                            {
                                name = packName,
                                variant = group.variant,
                                directory = objPath,
                                mode = PackMode.PackTogetherByGroup,
                                separator = group.separator,
                                ignore = "!.png",
                                important = false,
                            };

                            if (!SetBundleName(spriteGroup, out errMsg)) return false;
                        }
                        else
                        {
                            SetFile(objPath, packName, group.variant, true);
                        }
                    }
                }
            }

            return true;
        }

        static void SetFile(string file, string assetBundleName, string assetBundleVariant, bool important = false)
        {
            AssetImporter importer = AssetImporter.GetAtPath(file);
            if (importer != null)
            {
                assetBundleName = ValidateAssetBundleName(assetBundleName);
#if UNITY_PS4
                if (assetBundleName.Split('/').Length >= 7)
                    Debug.LogErrorFormat("{0}: folder depth exceeds the limit in PlayStation platform!!", assetBundleName);
#endif

                assetBundleName = assetBundleName.ToLower();
                assetBundleVariant = assetBundleVariant.ToLower();

                if (!importer.assetBundleName.Equals(assetBundleName))
                    importer.assetBundleName = assetBundleName;
                if (!importer.assetBundleVariant.Equals(assetBundleVariant))
                    importer.assetBundleVariant = assetBundleVariant;

                if (important) 
                {
                    config.Add(new AssetBundleInfo
                    {
                        path = file.Replace(Path.DirectorySeparatorChar.ToString(), "/"),
                        bundle = assetBundleName,
                        asset = Path.GetFileNameWithoutExtension(file),
                        variant = assetBundleVariant,
                    });
                }
            }
        }

        static void CleanFile(string file)
        {
            AssetImporter importer = AssetImporter.GetAtPath(file);
            if (importer != null && !string.IsNullOrEmpty(importer.assetBundleName))
                importer.assetBundleName = null;
        }

        static string ValidateAssetBundleName(string assetBundleName)
        {
            return assetBundleName.Replace(" ", "_").ToLower();
        }
    }
}

