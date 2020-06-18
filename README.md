# AssetBundleManager

AssetBundleManager is improved from Unity AssetBundleManager.

## Support

* Set assetbundle names by created `AssetBundleNamesSetting.asset` file;
* Clean assetbundle names;
* Build or Rebuild assetbundles;
* Load assetbundles;
* Load assetbundle variants;
* Load in Local or Remote or LocalFirst or RemoteFirst;
* Unload assetbundles by reference count.
* Compatible Asset Bundle Browser

## How to use this
### Download
#### Setup
Download or clone this repository into your project in the folder `Packages/com.chaosvan.assetbundle-manager`.

### Package Manager Manifest
#### Requirements
[Git](https://git-scm.com/) must be installed and added to your path.
#### Setup
The following line needs to be added to your `Packages/manifest.json` file in your Unity Project under the `dependencies` section:

```json
"com.chaosvan.assetbundle-manager": "https://github.com/ChaosVan/AssetBundleManager.git#master",
```
