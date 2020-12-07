using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

#if UNITY_EDITOR
using UnityEditor;
#endif

#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif

/*
 	In this demo, we demonstrate:
	1.	Automatic asset bundle dependency resolving & loading.
		It shows how to use the manifest assetbundle like how to get the dependencies etc.
	2.	Automatic unloading of asset bundles (When an asset bundle or a dependency thereof is no longer needed, the asset bundle is unloaded)
	3.	Editor simulation. A bool defines if we load asset bundles from the project or are actually using asset bundles(doesn't work with assetbundle variants for now.)
		With this, you can player in editor mode without actually building the assetBundles.
	4.	Optional setup where to download all asset bundles
	5.	Build pipeline build postprocessor, integration so that building a player builds the asset bundles and puts them into the player data (Default implmenetation for loading assetbundles from disk on any platform)
	6.	Use WWW.LoadFromCacheOrDownload and feed 128 bit hash to it when downloading via web
		You can get the hash from the manifest assetbundle.
	7.	AssetBundle variants. A prioritized list of variants that should be used if the asset bundle with that variant exists, first variant in the list is the most preferred etc.
*/

namespace AssetBundles
{
    // Loaded assetBundle contains the references count which can be used to unload dependent assetBundles automatically.
    [System.Serializable]
    public class LoadedAssetBundle
    {
        public string name;
        public AssetBundle m_AssetBundle;
        public int m_ReferencedCount;

        public LoadedAssetBundle(AssetBundle assetBundle)
        {
            name = assetBundle.name;
            m_AssetBundle = assetBundle;
            m_ReferencedCount = 1;

            if (string.IsNullOrEmpty(name))
                name = Utility.GetPlatformName();
        }

        public LoadedAssetBundle(AssetBundle assetBundle, int referencedCount)
        {
            name = assetBundle.name;
            m_AssetBundle = assetBundle;
            m_ReferencedCount = referencedCount;
        }
    }

    // Class takes care of loading assetBundle and its dependencies automatically, loading variants automatically.
    public class AssetBundleManager : MonoBehaviour
    {
        public enum LogMode { All, JustErrors };
        public enum LogType { Info, Warning, Error };
        public enum LoadMode { Internal, Local, Remote, LocalFirst, RemoteFirst };

        static AssetBundleManifest m_AssetBundleManifest = null;

#if UNITY_EDITOR
        static int m_SimulateAssetBundleInEditor = -1;
        const string kSimulateAssetBundles = "SimulateAssetBundles";
        static List<string> m_SimulateAssetBundleList = new List<string>();
#endif

#if ODIN_INSPECTOR
        [SerializeField]
        private bool showOdinInfo;
#endif

#if ODIN_INSPECTOR
        [ShowInInspector, ShowIf("showOdinInfo"), DictionaryDrawerSettings(IsReadOnly = true)]
#endif
        static Dictionary<string, LoadedAssetBundle> m_LoadedAssetBundles = new Dictionary<string, LoadedAssetBundle>();
        static Dictionary<string, UnityWebRequestAsyncOperation> m_UnityWebRequests = new Dictionary<string, UnityWebRequestAsyncOperation>();
        static Dictionary<string, AssetBundleCreateRequest> m_CreatingAssetBundles = new Dictionary<string, AssetBundleCreateRequest>();


        static Dictionary<string, string> m_DownloadingErrors = new Dictionary<string, string>();
        static List<AssetBundleLoadOperation> m_InProgressOperations = new List<AssetBundleLoadOperation>();
#if ODIN_INSPECTOR
        [ShowInInspector, ShowIf("showOdinInfo"), DictionaryDrawerSettings(IsReadOnly = true)]
#endif
        static Dictionary<string, string[]> m_Dependencies = new Dictionary<string, string[]>();

        static Dictionary<string, int> m_LocalLoadingReferenceCount = new Dictionary<string, int>();
        static Dictionary<string, int> m_RemoteLoadingReferencedCount = new Dictionary<string, int>();
#if ODIN_INSPECTOR
        [ShowInInspector, ShowIf("showOdinInfo"), DictionaryDrawerSettings(IsReadOnly = true)]
#endif
        static Dictionary<string, List<string>> m_AllAssetBundlesWithVariant = new Dictionary<string, List<string>>();

#if UNITY_EDITOR
#if ODIN_INSPECTOR
        [ShowInInspector, HideIf("showOdinInfo"), ListDrawerSettings(IsReadOnly = true)]
#endif
        public List<LoadedAssetBundle> allLoaded = new List<LoadedAssetBundle>();
        private static bool isDirty;
#endif

#if ODIN_INSPECTOR
        [ShowInInspector, ShowIf("showOdinInfo")]
#endif
        public static LoadMode loadMode { get; set; } = LoadMode.Local;

#if ODIN_INSPECTOR
        [ShowInInspector, ShowIf("showOdinInfo")]
#endif
        public static LogMode logMode { get; set; } = LogMode.All;

#if ODIN_INSPECTOR
        [ShowInInspector, ShowIf("showOdinInfo"), PropertyTooltip("The base downloading url which is used to generate the full downloading url with the assetBundle names.")]
#endif
        public static string BaseDownloadingURL { get; set; } = "";

#if ODIN_INSPECTOR
        [ShowInInspector, ShowIf("showOdinInfo")]
#endif
        public static string BaseLocalPath { get; set; } = "";

        // Variants which is used to define the active variants.
#if ODIN_INSPECTOR
        [ShowInInspector, ShowIf("showOdinInfo")]
#endif
        public static string[] ActiveVariants { get; set; } = { };

        // AssetBundleManifest object which can be used to load the dependecies and check suitable assetBundle variants.
        public static AssetBundleManifest AssetBundleManifestObject
        {
            get { return m_AssetBundleManifest; }
            set
            {
                if (m_AssetBundleManifest != null)
                    UnloadAssetBundle(Utility.GetPlatformName());

                m_AssetBundleManifest = value;

                string[] bundlesWithVariant = m_AssetBundleManifest.GetAllAssetBundlesWithVariant();

                InitAllAssetBundleWithVariants(bundlesWithVariant);
            }
        }

        private static void Log(LogType logType, string text)
        {
            if (logType == LogType.Error)
                Debug.LogError("[AssetBundleManager] " + text);
            else if (logMode == LogMode.All)
                Debug.Log("[AssetBundleManager] " + text);
        }

#if UNITY_EDITOR
        // Flag to indicate if we want to simulate assetBundles in Editor without building them actually.
        public static bool SimulateAssetBundleInEditor
        {
            get
            {
                if (m_SimulateAssetBundleInEditor == -1)
                    m_SimulateAssetBundleInEditor = EditorPrefs.GetBool(kSimulateAssetBundles, true) ? 1 : 0;

                return m_SimulateAssetBundleInEditor != 0;
            }
            set
            {
                int newValue = value ? 1 : 0;
                if (newValue != m_SimulateAssetBundleInEditor)
                {
                    m_SimulateAssetBundleInEditor = newValue;
                    EditorPrefs.SetBool(kSimulateAssetBundles, value);
                }
            }
        }
#endif

        public static void SetLocalAssetBundlePath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                if (Application.isMobilePlatform || Application.isEditor)
                    BaseLocalPath = Application.persistentDataPath + "/AssetBundles";
                else
                    BaseLocalPath = Application.dataPath + "/AssetBundles";
            }
            else
                BaseLocalPath = path;
        }

        public static void SetRemoteAssetBundleURL(string url)
        {
            if (string.IsNullOrEmpty(url))
                SetDevelopmentAssetBundleServer();
            else
                BaseDownloadingURL = url;
        }

        public static void SetDevelopmentAssetBundleServer()
        {
#if UNITY_EDITOR
            // If we're in Editor simulation mode, we don't have to setup a download URL
            if (SimulateAssetBundleInEditor)
                return;
#endif

            TextAsset urlFile = Resources.Load("AssetBundleServerURL") as TextAsset;
            string url = urlFile?.text.Trim();
            if (string.IsNullOrEmpty(url))
            {
                if (loadMode != LoadMode.Local && loadMode != LoadMode.Internal)
                {
                    Log(LogType.Error, "Development Server URL could not be found.");
                    if (string.IsNullOrEmpty(BaseLocalPath))
                        loadMode = LoadMode.Internal;
                    else
                        loadMode = LoadMode.Local;
                }
            }
            else
            {
                SetSourceAssetBundleURL(url);
            }
        }

        public static void SetSourceAssetBundleURL(string absolutePath)
        {
            BaseDownloadingURL = absolutePath;
        }

        /// <summary>
        /// Is assetbundle in manifest. the asset bundle should not cotains extension
        /// </summary>
        /// <returns><c>true</c>, if asset bundle asset was hased, <c>false</c> otherwise.</returns>
        /// <param name="assetBundle">Asset bundle name with variant.</param>
        public static bool HasAssetBundleInternal(string assetBundle)
        {
            if (string.IsNullOrEmpty(assetBundle))
                return false;

            return m_AllAssetBundlesWithVariant.ContainsKey(assetBundle);
        }

        /// <summary>
        /// Is assetbundle file in local. the asset bundle should not cotains extension
        /// </summary>
        /// <returns><c>true</c>, if asset in local was hased, <c>false</c> otherwise.</returns>
        /// <param name="assetBundle">Asset bundle.</param>
        public static bool HasAssetBundleInLocal(string assetBundle, bool useSimulatePath = false, bool isLoadingAssetBundleManifest = false)
        {
            if (string.IsNullOrEmpty(assetBundle))
                return false;

#if UNITY_EDITOR
            if (useSimulatePath && SimulateAssetBundleInEditor)
            {
                return HasAssetBundleInternal(assetBundle);
            }
#endif

            if (!isLoadingAssetBundleManifest)
                assetBundle = RemapVariantName(assetBundle);

            string fullPath = Path.Combine(BaseLocalPath, assetBundle);
            if (File.Exists(fullPath))
                return true;

            return false;
        }

        public static List<string> GetAllAssetBundleNames()
        {
            List<string> list = new List<string>();
            list.AddRange(m_AllAssetBundlesWithVariant.Keys);
            return list;
        }

#if UNITY_EDITOR
        static public bool GetLoadedSimulateAssetBundle(string assetBundleName)
        {
            if (m_SimulateAssetBundleList.Contains(assetBundleName))
                return true;
            else
                return false;
        }
#endif

        // Get loaded AssetBundle, only return vaild object when all the dependencies are downloaded successfully.
        static internal LoadedAssetBundle GetLoadedAssetBundle(string assetBundleName, out string error)
        {
            if (m_DownloadingErrors.TryGetValue(assetBundleName, out error))
                return null;

            m_LoadedAssetBundles.TryGetValue(assetBundleName, out LoadedAssetBundle bundle);
            if (bundle == null)
                return null;

            // No dependencies are recorded, only the bundle itself is required.
            if (!m_Dependencies.TryGetValue(assetBundleName, out string[] dependencies))
                return bundle;

            // Make sure all dependencies are loaded
            foreach (var dependency in dependencies)
            {
                if (m_DownloadingErrors.TryGetValue(dependency, out error))
                    return bundle;

                // Wait all the dependent assetBundles being loaded.
                m_LoadedAssetBundles.TryGetValue(dependency, out LoadedAssetBundle dependentBundle);
                if (dependentBundle == null)
                    return null;
            }

            return bundle;
        }

        static public AssetBundleLoadManifestOperation ReloadManifest()
        {
            UnloadAssetBundle(Utility.GetPlatformName());
            return Initialize();
        }

        static public AssetBundleLoadManifestOperation Initialize()
        {
            return Initialize(Utility.GetPlatformName());
        }

        public static bool IsInited = false;
        // Load AssetBundleManifest.
        static public AssetBundleLoadManifestOperation Initialize(string manifestAssetBundleName)
        {
#if UNITY_EDITOR
            Log(LogType.Info, "Simulation Mode: " + (SimulateAssetBundleInEditor ? "Enabled" : "Disabled"));
#endif

            AssetBundleManager mgr = FindObjectOfType<AssetBundleManager>();
            if (mgr == null)
                mgr = new GameObject("AssetBundleManager").AddComponent<AssetBundleManager>();
            DontDestroyOnLoad(mgr.gameObject);
            IsInited = true;
#if UNITY_EDITOR
            // If we're in Editor simulation mode, we don't need the manifest assetBundle.
            if (SimulateAssetBundleInEditor)
            {
                InitAllAssetBundleWithVariants(AssetDatabase.GetAllAssetBundleNames());
                return null;
            }
#endif

            LoadAssetBundle(manifestAssetBundleName, true);
            var operation = new AssetBundleLoadManifestOperation(manifestAssetBundleName, "AssetBundleManifest", typeof(AssetBundleManifest));
            m_InProgressOperations.Add(operation);
            return operation;
        }

        static void InitAllAssetBundleWithVariants(string[] bundlesWithVariant)
        {
            m_AllAssetBundlesWithVariant.Clear();
            // Loop all the assetBundles with variant to find the best fit variant assetBundle.
            for (int i = 0; i < bundlesWithVariant.Length; i++)
            {
                string[] curSplit = bundlesWithVariant[i].Split('.');

                if (curSplit.Length > 1)
                {
                    if (m_AllAssetBundlesWithVariant.ContainsKey(curSplit[0]))
                    {
                        m_AllAssetBundlesWithVariant[curSplit[0]].Add(curSplit[1]);
                    }
                    else
                    {
                        m_AllAssetBundlesWithVariant[curSplit[0]] = new List<string> { curSplit[1] };
                    }
                }
                else
                {
                    Log(LogType.Error, bundlesWithVariant[i] + " has no variant name");
                }
            }
        }

        // Load AssetBundle and its dependencies.
        static protected void LoadAssetBundle(string assetBundleName, bool isLoadingAssetBundleManifest = false)
        {
#if UNITY_EDITOR
            Log(LogType.Info, "Loading Asset Bundle " + (isLoadingAssetBundleManifest ? "Manifest: " : ": ") + assetBundleName);
            // If we're in Editor simulation mode, we don't have to really load the assetBundle and its dependencies.
            if (SimulateAssetBundleInEditor)
                return;
#endif

            if (!isLoadingAssetBundleManifest)
            {
                if (m_AssetBundleManifest == null)
                {
                    Log(LogType.Error, "Please load assetbundle " + assetBundleName + " after AssetBundleManager.Initialize()");
                    Log(LogType.Error, "Please initialize AssetBundleManifest by calling AssetBundleManager.Initialize()");
                    return;
                }
            }

            // Check if the assetBundle has already been processed.
            bool isAlreadyProcessed = LoadAssetBundleInternal(assetBundleName, isLoadingAssetBundleManifest, loadMode);

            // Load dependencies.
            if (!isAlreadyProcessed && !isLoadingAssetBundleManifest)
                LoadDependencies(assetBundleName);
        }

        // Remaps the asset bundle name to the best fitting asset bundle variant.
        static protected string RemapVariantName(string assetBundleName)
        {
            string[] split = assetBundleName.Split('.');
            assetBundleName = split[0];

#if UNITY_EDITOR
            if (SimulateAssetBundleInEditor)
            {
                return assetBundleName + "." + ActiveVariants[ActiveVariants.Length - 1];
            }
#endif

            if (m_AllAssetBundlesWithVariant.TryGetValue(assetBundleName, out List<string> variants))
            {
                int bestFit = int.MaxValue;
                int bestFitIndex = -1;
                for (int i = 0; i < variants.Count; ++i)
                {
                    int found = System.Array.IndexOf(ActiveVariants, variants[i]);

                    // If there is no active variant found. We still want to use the first 
                    if (found == -1)
                        found = int.MaxValue - 1;

                    if (found < bestFit)
                    {
                        bestFit = found;
                        bestFitIndex = i;
                    }
                }

                if (bestFit == int.MaxValue - 1)
                {
                    Log(LogType.Warning, "Ambigious asset bundle variant chosen because there was no matching active variant: " + variants[bestFitIndex]);
                }

                return assetBundleName + "." + variants[bestFitIndex];
            }
            else
            {
                return assetBundleName + "." + ActiveVariants[ActiveVariants.Length - 1];
            }

        }

        /// <summary>
        /// Loads the asset bundle internal. Where we actuall call WWW to download the assetBundle.
        /// </summary>
        /// <returns><c>true</c>, if asset bundle internal was already processed, <c>false</c> otherwise.</returns>
        /// <param name="assetBundleName">Asset bundle name.</param>
        /// <param name="isLoadingAssetBundleManifest">If set to <c>true</c> is loading asset bundle manifest.</param>
        static protected bool LoadAssetBundleInternal(string assetBundleName, bool isLoadingAssetBundleManifest, LoadMode mode, int refCount = 1)
        {
            // Has error
            if (m_DownloadingErrors.ContainsKey(assetBundleName))
            {
                return true;
            }
            // Already loaded.
            if (m_LoadedAssetBundles.TryGetValue(assetBundleName, out LoadedAssetBundle bundle))
            {
                bundle.m_ReferencedCount += refCount;
                return true;
            }

            // @TODO: Do we need to consider the referenced count of WWWs?
            // In the demo, we never have duplicate WWWs as we wait LoadAssetAsync()/LoadLevelAsync() to be finished before calling another LoadAssetAsync()/LoadLevelAsync().
            // But in the real case, users can call LoadAssetAsync()/LoadLevelAsync() several times then wait them to be finished which might have duplicate WWWs.

            if (m_LocalLoadingReferenceCount.ContainsKey(assetBundleName))
            {
                m_LocalLoadingReferenceCount[assetBundleName] += refCount;
                return true;
            }

            if (m_RemoteLoadingReferencedCount.ContainsKey(assetBundleName))
            {
                m_RemoteLoadingReferencedCount[assetBundleName] += refCount;
                return true;
            }

            if (mode == LoadMode.Local || mode == LoadMode.LocalFirst || mode == LoadMode.Internal)
            {
                m_LocalLoadingReferenceCount.Add(assetBundleName, refCount);

                string url = Path.Combine(BaseLocalPath, assetBundleName);

                if (mode == LoadMode.Internal || !HasAssetBundleInLocal(assetBundleName, false, isLoadingAssetBundleManifest))
                    url = Path.Combine(Utility.GetStreamingAssetsPath(), assetBundleName);

                AssetBundleCreateRequest request = null;

                if (isLoadingAssetBundleManifest)
                    request = AssetBundle.LoadFromFileAsync(url);
                else
                    request = AssetBundle.LoadFromFileAsync(url, 0); // TODO CRC校验

                request.priority = (int)Application.backgroundLoadingPriority;

                m_CreatingAssetBundles.Add(assetBundleName, request);
            }
            else if (mode == LoadMode.Remote || mode == LoadMode.RemoteFirst)
            {
                m_RemoteLoadingReferencedCount.Add(assetBundleName, refCount);

                string url = Path.Combine(BaseDownloadingURL, assetBundleName);

                //if (isLoadingAssetBundleManifest)
                //    WebRequestManager.Instance.LoadAssetBundle(url, OnRemoteCallback, (int)ThreadPriority.BelowNormal, assetBundleName);
                //else
                //    WebRequestManager.Instance.LoadAssetBundle(url, m_AssetBundleManifest.GetAssetBundleHash(assetBundleName), OnRemoteCallback, (int)ThreadPriority.BelowNormal, assetBundleName);

                UnityWebRequest download = null;

                //For manifest assetbundle, always download it as we don't have hash for it.
                if (isLoadingAssetBundleManifest)
                    download = UnityWebRequestAssetBundle.GetAssetBundle(url);
                else
                    download = UnityWebRequestAssetBundle.GetAssetBundle(url, m_AssetBundleManifest.GetAssetBundleHash(assetBundleName), 0); // TODO CRC校验

                UnityWebRequestAsyncOperation request = download.SendWebRequest();
                request.priority = (int)Application.backgroundLoadingPriority;

                m_UnityWebRequests.Add(assetBundleName, request);
            }

            return false;
        }

        // Where we get all the dependencies and load them all.
        static protected void LoadDependencies(string assetBundleName)
        {
            if (m_AssetBundleManifest == null)
            {
                Log(LogType.Error, "Please initialize AssetBundleManifest by calling AssetBundleManager.Initialize()");
                return;
            }

            // Get dependecies from the AssetBundleManifest object..
            string[] dependencies = m_AssetBundleManifest.GetAllDependencies(assetBundleName);
            if (dependencies.Length == 0)
                return;

            for (int i = 0; i < dependencies.Length; i++)
                dependencies[i] = RemapVariantName(dependencies[i]);

            // Record and load all dependencies.
            m_Dependencies.Add(assetBundleName, dependencies);
            for (int i = 0; i < dependencies.Length; i++)
                LoadAssetBundleInternal(dependencies[i], false, loadMode);
        }

        // Unload assetbundle and its dependencies.
        static public void UnloadAssetBundle(string assetBundleName)
        {
#if UNITY_EDITOR
            // If we're in Editor simulation mode, we don't have to load the manifest assetBundle.
            if (SimulateAssetBundleInEditor)
            {
                if (m_SimulateAssetBundleList.Contains(assetBundleName))
                    m_SimulateAssetBundleList.Remove(assetBundleName);

                return;
            }
#endif

            //Log(LogType.Info, m_LoadedAssetBundles.Count + " assetbundle(s) in memory before unloading " + assetBundleName);

            if (UnloadAssetBundleInternal(assetBundleName))
                UnloadDependencies(assetBundleName);

            if (m_DownloadingErrors.ContainsKey(assetBundleName))
                m_DownloadingErrors.Remove(assetBundleName);

            //Log(LogType.Info, m_LoadedAssetBundles.Count + " assetbundle(s) in memory after unloading " + assetBundleName);
        }

        static protected void UnloadDependencies(string assetBundleName)
        {
            if (!m_Dependencies.TryGetValue(assetBundleName, out string[] dependencies))
                return;

            // Loop dependencies.
            foreach (var dependency in dependencies)
            {
                UnloadAssetBundleInternal(dependency);
            }

            m_Dependencies.Remove(assetBundleName);
        }

        static protected bool UnloadAssetBundleInternal(string assetBundleName)
        {
            string error;
            LoadedAssetBundle bundle = GetLoadedAssetBundle(assetBundleName, out error);
            if (bundle == null)
                return false;

            if (--bundle.m_ReferencedCount == 0)
            {
                bundle.m_AssetBundle.Unload(true);
                m_LoadedAssetBundles.Remove(assetBundleName);

#if UNITY_EDITOR
                isDirty = true;
#endif
            }

#if UNITY_EDITOR
            Log(LogType.Info, string.Format(UNLOADSTR, assetBundleName, bundle.m_ReferencedCount));
#endif

            return bundle.m_ReferencedCount == 0;
        }

//        static void OnRemoteCallback(UnityWebRequest request, string err, object userdata)
//        {
//            string key = userdata as string;
//            if (request.isDone)
//            {
//                int refCount = m_RemoteLoadingReferencedCount[key];
//                m_RemoteLoadingReferencedCount.Remove(key);

//                AssetBundle bundle = DownloadHandlerAssetBundle.GetContent(request);

//                if (bundle == null)
//                {
//                    if (loadMode == LoadMode.RemoteFirst)
//                    {
//                        Log(LogType.Info, string.Format("No asset in remote {0}, try local {1}: {2}", BaseDownloadingURL, BaseLocalURL, key));
//                        LoadAssetBundleInternal(key, m_AssetBundleManifest == null, LoadMode.Local, refCount);
//                    }
//                    else
//                    {
//                        if (!m_DownloadingErrors.ContainsKey(key))
//                        {
//                            if (request.isNetworkError || request.isHttpError)
//                                m_DownloadingErrors.Add(key, string.Format("Failed downloading bundle {0} from {1}: {2}", key, request.url, err));
//                            else
//                                m_DownloadingErrors.Add(key, string.Format("{0} is not a valid asset bundle.", key));
//                        }
//                    }
//                }
//                else
//                {
//                    if (refCount > 1)
//                        m_LoadedAssetBundles.Add(key, new LoadedAssetBundle(bundle, refCount));
//                    else
//                        m_LoadedAssetBundles.Add(key, new LoadedAssetBundle(bundle));

//                    if (m_DownloadingErrors.ContainsKey(key))
//                        m_DownloadingErrors.Remove(key);
//#if UNITY_EDITOR
//                    isDirty = true;
//#endif
        //        }
        //    }
        //}

        void UpdateLocal()
        {
            var keysToRemove = new List<string>();

            Dictionary<string, AssetBundleCreateRequest>.Enumerator e = m_CreatingAssetBundles.GetEnumerator();
            while (e.MoveNext())
            {
                string key = e.Current.Key;
                AssetBundleCreateRequest request = e.Current.Value;

                if (request.isDone)
                {
                    int refCount = m_LocalLoadingReferenceCount[key];
                    m_LocalLoadingReferenceCount.Remove(key);
                    keysToRemove.Add(key);

                    AssetBundle bundle = request.assetBundle;
                    if (bundle == null)
                    {
                        if (loadMode == LoadMode.LocalFirst)
                        {
#if UNITY_EDITOR
                            Log(LogType.Info, string.Format("No asset[{2}] in local {0}, try remote {1}", BaseLocalPath, BaseDownloadingURL, key));
#endif
                            LoadAssetBundleInternal(key, m_AssetBundleManifest == null, LoadMode.Remote, refCount);
                        }
                        else
                        {
                            if (!m_DownloadingErrors.ContainsKey(key))
                                m_DownloadingErrors.Add(key, string.Format("{0} is not exist.", key));
                        }
                    }
                    else
                    {
                        if (refCount > 1)
                            m_LoadedAssetBundles.Add(key, new LoadedAssetBundle(bundle, refCount));
                        else
                            m_LoadedAssetBundles.Add(key, new LoadedAssetBundle(bundle));
#if UNITY_EDITOR
                        isDirty = true;
#endif
                    }
                }
            }

            for (int i = 0; i < keysToRemove.Count; ++i)
            {
                string key = keysToRemove[i];
                AssetBundleCreateRequest download = m_CreatingAssetBundles[key];
                m_CreatingAssetBundles.Remove(key);
            }
        }

        void UpdateRemote()
        {
            // Collect all the finished WWWs.
            var keysToRemove = new List<string>();

            Dictionary<string, UnityWebRequestAsyncOperation>.Enumerator e = m_UnityWebRequests.GetEnumerator();

            while (e.MoveNext())
            {
                string key = e.Current.Key;
                UnityWebRequestAsyncOperation download = e.Current.Value;

                if (download.isDone)
                {
                    int refCount = m_RemoteLoadingReferencedCount[key];
                    m_RemoteLoadingReferencedCount.Remove(key);
                    keysToRemove.Add(key);

                    AssetBundle bundle = DownloadHandlerAssetBundle.GetContent(download.webRequest);

                    if (bundle == null)
                    {
                        if (loadMode == LoadMode.RemoteFirst)
                        {
#if UNITY_EDITOR
                            Log(LogType.Info, string.Format("No asset[{2}] in remote {0}, try local {1}", BaseDownloadingURL, BaseLocalPath, key));
#endif
                            LoadAssetBundleInternal(key, m_AssetBundleManifest == null, LoadMode.Local, refCount);
                        }
                        else
                        {
                            if (!m_DownloadingErrors.ContainsKey(key))
                            {
                                if (download.webRequest.isNetworkError || download.webRequest.isHttpError)
                                    m_DownloadingErrors.Add(key, string.Format("Failed downloading bundle {0} from {1}: {2}", key, download.webRequest.url, download.webRequest.error));
                                else
                                    m_DownloadingErrors.Add(key, string.Format("{0} is not a valid asset bundle.", key));
                            }
                        }
                    }
                    else
                    {
                        if (refCount > 1)
                            m_LoadedAssetBundles.Add(key, new LoadedAssetBundle(bundle, refCount));
                        else
                            m_LoadedAssetBundles.Add(key, new LoadedAssetBundle(bundle));
#if UNITY_EDITOR
                        isDirty = true;
#endif
                    }
                }
            }

            for (int i = 0; i < keysToRemove.Count; ++i)
            {
                string key = keysToRemove[i];

                UnityWebRequestAsyncOperation download = m_UnityWebRequests[key];
                m_UnityWebRequests.Remove(key);
                download.webRequest.Dispose();
                System.GC.SuppressFinalize(download.webRequest);
            }
        }

        void Update()
        {
            if (loadMode != LoadMode.Remote)
                UpdateLocal();

            if (loadMode != LoadMode.Local)
                UpdateRemote();

            // Update all in progress operations, as smooth as possible
            if (m_InProgressOperations.Count > 0)
            {
                int count = m_InProgressOperations.Count;
                float time = Time.realtimeSinceStartup;
                int i = 0;
                while (i < m_InProgressOperations.Count)
                {
                    if (!m_InProgressOperations[i].Update())
                    {
                        m_InProgressOperations.RemoveAt(i);
                    }
                    else
                    {
                        i++;
                    }

                    if (Time.realtimeSinceStartup - time >= Time.fixedDeltaTime)
                        break;
                }
            }

#if UNITY_EDITOR
            if (isDirty)
            {
                isDirty = false;
                allLoaded.Clear();
                allLoaded.AddRange(m_LoadedAssetBundles.Values);
                //allLoaded.Sort((a, b) =>
                //{
                //    return string.Compare(a.name, b.name);
                //});
                allLoaded.Sort((a, b) =>
                {
                    if (b.m_ReferencedCount != a.m_ReferencedCount)
                        return b.m_ReferencedCount - a.m_ReferencedCount;
                    else
                        return string.Compare(a.name, b.name);
                });
            }
#endif
        }

        // Load asset from the given assetBundle.
        static public AssetBundleLoadAssetOperation LoadAssetAsync(string assetBundleName, string assetName, System.Type type)
        {
            AssetBundleLoadAssetOperation operation = null;
#if UNITY_EDITOR
            Log(LogType.Info, string.Format(LOADSTR, assetName, type, assetBundleName, Time.realtimeSinceStartup));
            if (SimulateAssetBundleInEditor)
            {
                m_SimulateAssetBundleList.Add(assetBundleName);
                string variant = RemapVariantName(assetBundleName);
                operation = new AssetBundleLoadAssetOperationSimulation(assetBundleName, variant, assetName, type);
                m_InProgressOperations.Add(operation);
            }
            else
#endif
            {
                string variant = RemapVariantName(assetBundleName);
                LoadAssetBundle(variant);
                operation = new AssetBundleLoadAssetOperationFull(assetBundleName, variant, assetName, type);

                m_InProgressOperations.Add(operation);
            }

            return operation;
        }

        // Load level from the given assetBundle.
        static public AssetBundleLoadLevelOperation LoadLevelAsync(string assetBundleName, string levelName, bool isAdditive, bool allowSceneActivation)
        {
            AssetBundleLoadLevelOperation operation = null;
#if UNITY_EDITOR
            Log(LogType.Info, string.Format(LOADSTR, levelName, typeof(UnityEngine.SceneManagement.Scene), assetBundleName, Time.realtimeSinceStartup));
            if (SimulateAssetBundleInEditor)
            {
                m_SimulateAssetBundleList.Add(assetBundleName);
                assetBundleName = RemapVariantName(assetBundleName);
                operation = new AssetBundleLoadLevelSimulationOperation(assetBundleName, levelName, isAdditive, allowSceneActivation);
            }
            else
#endif
            {
                assetBundleName = RemapVariantName(assetBundleName);
                LoadAssetBundle(assetBundleName);
                operation = new AssetBundleLoadLevelOperation(assetBundleName, levelName, isAdditive, allowSceneActivation);
            }

            m_InProgressOperations.Add(operation);
            return operation;
        }

        public const string LOADSTR = "Loading [{0}][{1}] from assetbundle [{2}] at realtime since startup [{3}]";
        public const string UNLOADSTR = "Unloading assetbundle reference [{0}], now ref count [{1}]";
    } // End of AssetBundleManager.
}