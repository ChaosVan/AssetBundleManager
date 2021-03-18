using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AssetBundles
{
    [System.Serializable]
    public struct AssetBundleInfo
    {
        public string path;
        public string bundle;
        public string asset;
        public string variant;
    }

    [System.Serializable]
    public class AssetBundleConfig
    {
        public List<AssetBundleInfo> content;

        Dictionary<string, AssetBundleInfo> dic;

        public void Initialize()
        {
            if (content != null)
            {
                dic = new Dictionary<string, AssetBundleInfo>();
                foreach (var info in content)
                    dic[info.path] = info;
            }
        }

        public void Clear()
        {
            if (content != null)
                content.Clear();
            else
                content = new List<AssetBundleInfo>();
        }

        public void Add(AssetBundleInfo info)
        {
            if (!content.Contains(info))
                content.Add(info);
        }

        public void Remove(AssetBundleInfo info)
        {
            if (content.Contains(info))
                content.Remove(info);
        }

        public bool TryGet(string path, out AssetBundleInfo info)
        {
            return dic.TryGetValue(path, out info);
        }
    }
}
