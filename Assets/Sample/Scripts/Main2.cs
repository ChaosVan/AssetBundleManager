using System;
using System.Collections;
using System.Collections.Generic;
using AssetBundles;
using UnityEngine;
using UnityEngine.U2D;

public class Main2 : MonoBehaviour
{
    public Canvas canvas;
    public string perferVariant = "bundle";

    private readonly List<string> loadedAssetBundle = new List<string>();

    // Start is called before the first frame update
    IEnumerator Start()
    {
        SpriteAtlasManager.atlasRequested += OnAtlasRequested;
        SpriteAtlasManager.atlasRegistered += OnAtlasRegistered;

        AssetBundleManager.loadMode = AssetBundleManager.LoadMode.RemoteFirst;
        AssetBundleManager.logMode = AssetBundleManager.LogMode.All;

        AssetBundleManager.SetLocalAssetBundleDirectory(Application.persistentDataPath);
        AssetBundleManager.SetRemoteAssetBundleURL("file://" + Application.temporaryCachePath);
        AssetBundleManager.ActiveVariants = new string[] { perferVariant };

        yield return AssetBundleManager.Initialize();

        StartCoroutine(Load("assets/sample/prefabs/image", "Image", typeof(GameObject), (asset) =>
        {
            Instantiate(asset as GameObject, canvas.transform);
        }));
    }

    void OnAtlasRequested(string atlas, Action<SpriteAtlas> action)
    {
        StartCoroutine(Load("assets/sample/atlas/" + atlas.ToLower(), atlas, typeof(SpriteAtlas), (asset) =>
        {
            action(asset as SpriteAtlas);
        }));
    }

    void OnAtlasRegistered(SpriteAtlas spriteAtlas)
    {

    }

    IEnumerator Load(string assetBundle, string assetName, Type type, Action<object> callback)
    {
        AssetBundleLoadAssetOperation ao = AssetBundleManager.LoadAssetAsync(assetBundle, assetName, type);

        yield return ao;

        if (ao.IsDone())
        {
            loadedAssetBundle.Add(ao.assetBundleVariant);
            callback(ao.GetAsset());
        }
    }

    private void OnDestroy()
    {
        foreach (var v in loadedAssetBundle)
        {
            AssetBundleManager.UnloadAssetBundle(v);
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
