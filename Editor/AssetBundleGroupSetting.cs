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
    public struct AssetBundleGroupSetting
    {
        public string name;
        public string variant;
#if ODIN_INSPECTOR
        [FolderPath]
#endif
        public string directory;
        public PackMode pack_mode;
        public SeparatorType separator;
        public string ignore;
        public string comment;

        public string GetSeparator()
        {
            switch (separator)
            {
                case SeparatorType.Slash:
                    return "/";
                case SeparatorType.BackSlash:
                    return "\\";
                case SeparatorType.Score:
                    return "-";
                case SeparatorType.UnderScore:
                    return "_";
            }

            return "/";
        }
    }

    public enum PackMode
    {
        /// <summary>
        /// 按Directory打包
        /// </summary>
        PackTogether,
        /// <summary>
        /// 按Name打包
        /// </summary>
        PackTogetherByGroup,
        /// <summary>
        /// 按单个文件打包
        /// </summary>
        PackSeparately,
        /// <summary>
        /// 按最小文件夹打包
        /// </summary>
        PackSeparatelyByFolder,
    }

    public enum SeparatorType
    {
        /// <summary>
        /// "_"
        /// </summary>
        UnderScore,
        /// <summary>
        /// "-"
        /// </summary>
        Score,
        /// <summary>
        /// "/"
        /// </summary>
        Slash, 
        /// <summary>
        /// "\"
        /// </summary>
        BackSlash,
    }
}
