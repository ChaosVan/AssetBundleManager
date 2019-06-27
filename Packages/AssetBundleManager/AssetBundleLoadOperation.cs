using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AssetBundles
{
    public abstract class AssetBundleLoadOperation : IEnumerator
    {
        public object Current
        {
            get
            {
                return null;
            }
        }

        public bool MoveNext()
        {
            return !IsDone();
        }

        public void Reset()
        {
        }

        abstract public bool Update();

        abstract public bool IsDone();

        abstract public float Progress();

        protected string m_DownloadingError;
        public string Error => m_DownloadingError;
    }

    public class AssetBundleLoadLevelOperation : AssetBundleLoadOperation
    {
        protected string m_AssetBundleName;
        protected string m_LevelName;
        protected bool m_IsAdditive;
        protected bool m_allowSceneActivation;

        protected AsyncOperation m_Request;

        public string AssetBundle => m_AssetBundleName;
        public string LevelName => m_LevelName;

        public AssetBundleLoadLevelOperation(string assetbundleName, string levelName, bool isAdditive, bool allowSceneActivation)
        {
            m_AssetBundleName = assetbundleName;
            m_LevelName = levelName;
            m_IsAdditive = isAdditive;
            m_allowSceneActivation = allowSceneActivation;

        }

        public override bool Update()
        {
            if (m_Request != null)
                return false;

            LoadedAssetBundle bundle = AssetBundleManager.GetLoadedAssetBundle(m_AssetBundleName, out m_DownloadingError);
            if (bundle != null)
            {
                if (m_IsAdditive)
                    m_Request = SceneManager.LoadSceneAsync(m_LevelName, LoadSceneMode.Additive);
                else
                    m_Request = SceneManager.LoadSceneAsync(m_LevelName, LoadSceneMode.Single);

                m_Request.allowSceneActivation = m_allowSceneActivation;

                return false;
            }
            else
                return true;
        }

        public override bool IsDone()
        {
            // Return if meeting downloading error.
            // m_DownloadingError might come from the dependency downloading.
            if (m_Request == null && !string.IsNullOrEmpty(m_DownloadingError))
            {
                return true;
            }

            return m_Request != null && m_Request.isDone;
        }

        public override float Progress()
        {
            return m_Request == null ? 1 : m_Request.progress;
        }
    }

#if UNITY_EDITOR
    public class AssetBundleLoadLevelSimulationOperation : AssetBundleLoadLevelOperation
    {
        public AssetBundleLoadLevelSimulationOperation(string assetbundleName, string levelName, bool isAdditive, bool allowSceneActivation) 
            : base(assetbundleName, levelName, isAdditive, allowSceneActivation)
        {
        }

        public override bool Update()
        {

            string[] levelPaths = UnityEditor.AssetDatabase.GetAssetPathsFromAssetBundleAndAssetName(m_AssetBundleName, m_LevelName);
            if (levelPaths.Length == 0)
            {
                ///@TODO: The error needs to differentiate that an asset bundle name doesn't exist
                //        from that there right scene does not exist in the asset bundle...

                m_DownloadingError = string.Format("There is no scene with name {0} in {1}", m_LevelName, m_AssetBundleName);
                return false;
            }

#if UNITY_2018_3_OR_NEWER
            m_Request = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(levelPaths[0], new LoadSceneParameters(m_IsAdditive ? LoadSceneMode.Additive : LoadSceneMode.Single));
#else
            if (m_IsAdditive)
                m_Operation = UnityEditor.EditorApplication.LoadLevelAdditiveAsyncInPlayMode(levelPaths[0]);
            else
                m_Operation = UnityEditor.EditorApplication.LoadLevelAsyncInPlayMode(levelPaths[0]);
#endif

            m_Request.allowSceneActivation = m_allowSceneActivation;

            return false;
        }
    }

#endif

    public abstract class AssetBundleLoadAssetOperation : AssetBundleLoadOperation
    {
        public abstract Object GetAsset();
        public abstract T GetAsset<T>() where T : UnityEngine.Object;
        public abstract Object[] GetAllAssets();
        public abstract T[] GetAllAssets<T>() where T : UnityEngine.Object;

        public string assetBundleName;
        public string assetBundleVariant;
        public string assetName;
        public System.Type type;
    }

    public class AssetBundleLoadAssetOperationFull : AssetBundleLoadAssetOperation
    {
        protected AssetBundleRequest m_Request = null;

        public AssetBundleLoadAssetOperationFull(string assetBundleName, string assetBundleVariant, string assetName, System.Type type)
        {
            this.assetBundleName = assetBundleName;
            this.assetBundleVariant = assetBundleVariant;
            this.assetName = assetName;
            this.type = type;
        }

        public override Object GetAsset()
        {
            if (m_Request != null && m_Request.isDone)
                return m_Request.asset;
            else
                return null;
        }

        public override T GetAsset<T>()
        {
            if (m_Request != null && m_Request.isDone)
                return m_Request.asset as T;
            else
                return null;
        }

        public override Object[] GetAllAssets()
        {
            if (m_Request != null && m_Request.isDone)
                return m_Request.allAssets;
            else
                return null;
        }

        public override T[] GetAllAssets<T>()
        {
            if (m_Request != null && m_Request.isDone)
            {
                T[] array = new T[m_Request.allAssets.Length];
                for (int i = 0; i < array.Length; i++)
                {
                    array[i] = m_Request.allAssets[i] as T;
                }
                return array;
            }
            else
                return null;
        }

        // Returns true if more Update calls are required.
        public override bool Update()
        {
            if (m_Request != null)
                return false;

            LoadedAssetBundle bundle = AssetBundleManager.GetLoadedAssetBundle(assetBundleVariant, out m_DownloadingError);
            if (bundle != null)
            {
                ///@TODO: When asset bundle download fails this throws an exception...
                if (!string.IsNullOrEmpty(assetName))  //只下载bundle
                    m_Request = bundle.m_AssetBundle.LoadAssetAsync(assetName, type);
                else
                    m_Request = bundle.m_AssetBundle.LoadAllAssetsAsync(type);

                return false;
            }

            if (Error != null)
                return false;

            return true;
        }

        public override bool IsDone()
        {
            // Return if meeting downloading error.
            // m_DownloadingError might come from the dependency downloading.
            if (m_Request == null && Error != null)
            {
                return true;
            }

            if (m_Request != null && m_Request.isDone && m_Request.asset == null)
            {
                m_DownloadingError = string.Format("There is no asset with name \"{0}\" in \"{1}\" with type {2}", assetName, assetBundleVariant, type);
            }

            return m_Request != null && m_Request.isDone;
        }

        public override float Progress()
        {
            return m_Request == null ? 1 : m_Request.progress;
        }
    }

    /// <summary>
    /// 加载AssetBundleManifest的进程
    /// </summary>
    public class AssetBundleLoadManifestOperation : AssetBundleLoadAssetOperationFull
    {
        public AssetBundleLoadManifestOperation(string bundleName, string assetName, System.Type type)
            : base(bundleName, bundleName, assetName, type)
        {
        }

        public override bool Update()
        {
            base.Update();

            if (m_Request != null && m_Request.isDone)
            {
                AssetBundleManager.AssetBundleManifestObject = GetAsset<AssetBundleManifest>();
                return false;
            }
            else
                return true;
        }

        public override bool IsDone()
        {
            if (m_Request == null && Error != null)
            {
                return true;
            }

            return m_Request != null && m_Request.isDone && AssetBundleManager.AssetBundleManifestObject != null;
        }
    }

#if UNITY_EDITOR
    public class AssetBundleLoadAssetOperationSimulation : AssetBundleLoadAssetOperation
    {
        Object m_SimulatedObject;
        Object[] m_SimulatedObjects;

        public AssetBundleLoadAssetOperationSimulation(string assetBundleName, string assetBundleVariant, string assetName, System.Type type)
        {
            this.assetBundleName = assetBundleName;
            this.assetBundleVariant = assetBundleVariant;
            this.assetName = assetName;
            this.type = type;
        }

        public override Object GetAsset()
        {
            return m_SimulatedObject;
        }

        public override T GetAsset<T>()
        {
            return m_SimulatedObject as T;
        }

        public override Object[] GetAllAssets()
        {
            return m_SimulatedObjects;
        }

        public override T[] GetAllAssets<T>()
        {
            T[] array = new T[m_SimulatedObjects.Length];
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = m_SimulatedObjects[i] as T;
            }
            return array;
        }

        public override bool Update()
        {
            if (!string.IsNullOrEmpty(assetName))
            {
                string[] assetPaths = AssetDatabase.GetAssetPathsFromAssetBundleAndAssetName(assetBundleVariant, assetName);
                for (int i = 0; i < assetPaths.Length; ++i)
                {
                    Object target;
                    if (type != typeof(Object))
                    {
                        target = AssetDatabase.LoadAssetAtPath(assetPaths[i], type);
                    }
                    else
                    {
                        target = AssetDatabase.LoadMainAssetAtPath(assetPaths[i]);
                    }

                    if (target)
                    {
                        m_SimulatedObject = target;
                        break;
                    }
                }

                if (m_SimulatedObject == null)
                {
                    m_DownloadingError = string.Format("There is no asset with name \"{0}\" in \"{1}\" with type {2}", assetName, assetBundleVariant, type);
                }
            }
            else
            {
                string[] assetPaths = AssetDatabase.GetAssetPathsFromAssetBundle(assetBundleVariant);

                if (assetPaths.Length > 0)
                {
                    var targets = new List<Object>();
                    for (int i = 0; i < assetPaths.Length; i++)
                    {
                        Object target;
                        if (type != typeof(Object))
                        {
                            target = AssetDatabase.LoadAssetAtPath(assetPaths[i], type);
                        }
                        else
                        {
                            target = AssetDatabase.LoadMainAssetAtPath(assetPaths[i]);
                        }

                        if (target)
                            targets.Add(target);
                    }

                    m_SimulatedObjects = targets.ToArray();
                }
                else
                {
                    m_DownloadingError = "There is no assetbundle with name " + assetBundleVariant;
                }
            }


            return false;
        }

        public override bool IsDone()
        {
            return m_SimulatedObject != null || m_SimulatedObjects != null || !string.IsNullOrEmpty(m_DownloadingError);
        }

        public override float Progress()
        {
            return 1;
        }
    }
#endif
}
