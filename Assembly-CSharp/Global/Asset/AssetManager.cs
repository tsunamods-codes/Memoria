using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using Memoria;
using Memoria.Prime;
using Memoria.Assets;
using Object = System.Object;

public static class AssetManager
{
	/*
	New system for Memoria:
	 1) Assets can be read from mod folders (defined in Memoria.Configuration.Mod)
	 - They can be either read from bundle archives or as plain non-archived files
	 - Assets in non-bundle archives (typically "resources.assets", but also all the "levelX" and "sharedassetsX.assets") can currently only be read as plain files when they are in a mod's folder
	 - They are read in priority from the first mod's folder up to the last mod's folder, then from the default folder
	 - Bundle archives should be placed directly in the mod's folder
	 - Plain files should be placed in a subfolder of the mod's folder respecting the asset's access path (eg. "EmbeddedAsset/Manifest/Text/Localization.txt")
	 - Only assets of certain types can be read as plain files currently: binary (TextAsset), text (TextAsset), textures (Texture / Texture2D / Sprite / UIAtlas) and animations (AnimationClip)
	 - Texture files that are not in archives should be placed as PNG or JPG files, not DXT-compressed files or using Unity's format
	 - Assets that are not present in the mod folders must be archived as normally in the default folder
	 2) Any asset that is not archived can use a "Memoria information" file which is a text file placed in the same subfolder as the asset, with the same name and the extension "AssetManager.MemoriaInfoExtension" (".memnfo"); the purpose of these files may vary from type to type
	 - For textures, they can define different properties of the texture that are usually in the Unity-formatted assets, such as the anisotropic level (see "AssetManager.ApplyTextureGenericMemoriaInfo")
	 - For sounds and musics, they can define or change different properties, such as the LoopStart/LoopEnd frame points (see "SoundLoaderResources.Load")
	 - For battle scene binary files (".raw16"), they can give battle properties as textual informations, which can be useful for adding custom properties to Memoria or go beyond binary max limits (see "BTL_SCENE.ReadBattleScene")
	 - Etc... [To be completed]
	 - The idea behind these files is that they should be optional (non-modded assets don't need them) and allow to use asset-specific special features without forcing other mods to take these features into account (for explicitely disabling them for instance)
	*/

	static AssetManager()
	{
		Array values = Enum.GetValues(typeof(AssetManagerUtil.ModuleBundle));
		String[] foldname = new String[Configuration.Mod.FolderNames.Length + 1];
		String url;
		for (Int32 i = 0; i < (Int32)Configuration.Mod.FolderNames.Length; ++i)
			foldname[i] = Configuration.Mod.FolderNames[i] + "/";
		foldname[Configuration.Mod.FolderNames.Length] = "";
		Folder = new AssetFolder[foldname.Length];
		for (Int32 i = 0; i < (Int32)foldname.Length; ++i)
		{
			Folder[i] = new AssetFolder(foldname[i]);
			foreach (Object obj in values)
			{
				AssetManagerUtil.ModuleBundle moduleBundle = (AssetManagerUtil.ModuleBundle)((Int32)obj);
				if (moduleBundle == AssetManagerUtil.ModuleBundle.FieldMaps)
				{
					Array values2 = Enum.GetValues(typeof(AssetManagerUtil.FieldMapBundleId));
					foreach (Object obj2 in values2)
					{
						AssetManagerUtil.FieldMapBundleId bundleId = (AssetManagerUtil.FieldMapBundleId)((Int32)obj2);
						url = foldname[i] + AssetManagerUtil.GetStreamingAssetsPath() + "/" + AssetManagerUtil.CreateFieldMapBundleFilename(Application.platform, false, bundleId);
						if (File.Exists(url))
							Folder[i].DictAssetBundleRefs.Add(AssetManagerUtil.GetFieldMapBundleName(bundleId), new AssetBundleRef(url, 0));
					}
				}
				else if (moduleBundle == AssetManagerUtil.ModuleBundle.Sounds)
				{
					Array values3 = Enum.GetValues(typeof(AssetManagerUtil.SoundBundleId));
					foreach (Object obj3 in values3)
					{
						AssetManagerUtil.SoundBundleId bundleId2 = (AssetManagerUtil.SoundBundleId)((Int32)obj3);
						url = foldname[i] + AssetManagerUtil.GetStreamingAssetsPath() + "/" + AssetManagerUtil.CreateSoundBundleFilename(Application.platform, false, bundleId2);
						if (File.Exists(url))
							Folder[i].DictAssetBundleRefs.Add(AssetManagerUtil.GetSoundBundleName(bundleId2), new AssetBundleRef(url, 0));
					}
				}
				else
				{
					url = foldname[i] + AssetManagerUtil.GetStreamingAssetsPath() + "/" + AssetManagerUtil.CreateModuleBundleFilename(Application.platform, false, moduleBundle);
					if (File.Exists(url))
						Folder[i].DictAssetBundleRefs.Add(AssetManagerUtil.GetModuleBundleName(moduleBundle), new AssetBundleRef(url, 0));
				}
			}
		}
		_LoadAnimationFolderMapping();
	}

	private static void _LoadAnimationFolderMapping()
	{
		AnimationInFolder = new Dictionary<String, List<String>>();
		AnimationReverseFolder = new Dictionary<String, String>();
		String filestr = LoadString("EmbeddedAsset/Manifest/Animations/AnimationFolderMapping.txt", out _);
		if (filestr == null)
		{
			return;
		}
		String[] folderList = filestr.Split(new Char[]
		{
			'\n'
		});
		for (Int32 i = 0; i < (Int32)folderList.Length; i++)
		{
			String folderFull = folderList[i];
			String[] geoAndAnim = folderFull.Split(new Char[]
			{
				':'
			});
			String geoFolder = geoAndAnim[0];
			String modelName = Path.GetFileNameWithoutExtension(geoFolder);
			String[] animList = geoAndAnim[1].Split(new Char[]
			{
				','
			});
			List<String> animFolderList = new List<String>();
			for (Int32 j = 0; j < (Int32)animList.Length; j++)
			{
				String animName = animList[j].Trim();
				String animFolder = geoFolder + "/" + animName;
				animFolder = animFolder.Trim();
				animFolderList.Add(animFolder);
				AnimationReverseFolder[animName] = modelName;
			}
			AnimationInFolder.Add(geoFolder, animFolderList);
		}
	}

	public static void ClearCache()
	{
		Caching.CleanCache();
		foreach (AssetFolder modfold in Folder)
			foreach (KeyValuePair<String, AssetBundleRef> keyValuePair in modfold.DictAssetBundleRefs)
			{
				AssetBundleRef value = keyValuePair.Value;
				if (value.assetBundle != (UnityEngine.Object)null)
				{
					value.assetBundle.Unload(true);
					value.assetBundle = (AssetBundle)null;
				}
			}
	}

    /*
	public static Byte[] LoadBinary(String resourcePath)
    {
		// By default, this method is only used for Localization.txt for a subsequent ByteReader.ReadCSV call
		// We delete it and use LoadBytes instead
        TextAsset textAsset = Resources.Load<TextAsset>(resourcePath);
        if (textAsset == null)
            throw new FileNotFoundException(resourcePath);

        return textAsset.bytes;
    }
	*/
	
	public static void ApplyTextureGenericMemoriaInfo<T>(ref T texture, ref String[] memoriaInfo) where T : UnityEngine.Texture
	{
		// Maybe remove the successfully parsed lines from the "info" array?
		foreach (String s in memoriaInfo)
        {
			String[] textureCode = s.Split(' ');
			if (textureCode.Length >= 2 && String.Compare(textureCode[0], "AnisotropicLevel") == 0)
			{
				Int32 anisoLevel;
				if (Int32.TryParse(textureCode[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out anisoLevel) && anisoLevel >= 1 && anisoLevel <= 9)
					texture.anisoLevel = anisoLevel;
			} else if (textureCode.Length >= 2 && String.Compare(textureCode[0], "FilterMode") == 0)
			{
				foreach (FilterMode m in (FilterMode[]) Enum.GetValues(typeof(FilterMode)))
					if (String.Compare(textureCode[1], m.ToString()) == 0)
						texture.filterMode = m;
			}
			else if(textureCode.Length >= 2 && String.Compare(textureCode[0], "HideFlags") == 0)
			{
				foreach (HideFlags f in (HideFlags[]) Enum.GetValues(typeof(HideFlags)))
					if (String.Compare(textureCode[1], f.ToString()) == 0)
						texture.hideFlags = f;
			}
			else if(textureCode.Length >= 2 && String.Compare(textureCode[0], "MipMapBias") == 0)
			{
				Single mipMapBias;
				if (Single.TryParse(textureCode[1], out mipMapBias))
					texture.mipMapBias = mipMapBias;
			}
			else if(textureCode.Length >= 2 && String.Compare(textureCode[0], "WrapMode") == 0)
			{
				foreach (TextureWrapMode m in (TextureWrapMode[]) Enum.GetValues(typeof(TextureWrapMode)))
					if (String.Compare(textureCode[1], m.ToString()) == 0)
						texture.wrapMode = m;
			}
		}
	}

	public static Boolean IsTextureFormatReadableFromBytes(TextureFormat format)
	{
		// Todo: verify which TextureFormat are compatible with LoadRawTextureData
		return format == TextureFormat.Alpha8
			|| format == TextureFormat.RGB24
			|| format == TextureFormat.RGBA32
			|| format == TextureFormat.ARGB32
			|| format == TextureFormat.RGB565
			|| format == TextureFormat.DXT1
			|| format == TextureFormat.DXT5;
	}

	public static Texture2D LoadTextureGeneric(Byte[] raw)
	{
		const UInt32 DDS_HEADER_SIZE = 0x3C;
		if (raw.Length >= DDS_HEADER_SIZE)
		{
			UInt32 w = BitConverter.ToUInt32(raw, 0);
			UInt32 h = BitConverter.ToUInt32(raw, 4);
			UInt32 imgSize = BitConverter.ToUInt32(raw, 8);
			TextureFormat fmt = (TextureFormat)BitConverter.ToUInt32(raw, 12);
			UInt32 mipCount = BitConverter.ToUInt32(raw, 16);
			// UInt32 flags = BitConverter.ToUInt32(raw, 20); // 1 = isReadable (for use of GetPixels); 0x100 is usually on
			// UInt32 imageCount = BitConverter.ToUInt32(raw, 24);
			// UInt32 dimension = BitConverter.ToUInt32(raw, 28);
			// UInt32 filterMode = BitConverter.ToUInt32(raw, 32);
			// UInt32 anisotropic = BitConverter.ToUInt32(raw, 36);
			// UInt32 mipBias = BitConverter.ToUInt32(raw, 40);
			// UInt32 wrapMode = BitConverter.ToUInt32(raw, 44);
			// UInt32 lightmapFormat = BitConverter.ToUInt32(raw, 48);
			// UInt32 colorSpace = BitConverter.ToUInt32(raw, 52);
			// UInt32 imgSize = BitConverter.ToUInt32(raw, 56);
			if (raw.Length == DDS_HEADER_SIZE + imgSize && IsTextureFormatReadableFromBytes(fmt))
			{
				Byte[] imgBytes = new Byte[imgSize];
				Buffer.BlockCopy(raw, (Int32)DDS_HEADER_SIZE, imgBytes, 0, (Int32)imgSize);
				Texture2D ddsTexture = new Texture2D((Int32)w, (Int32)h, fmt, mipCount > 1);
				ddsTexture.LoadRawTextureData(imgBytes);
				ddsTexture.Apply();
				return ddsTexture;
			}
		}
		Texture2D pngTexture = new Texture2D(1, 1, DefaultTextureFormat, false);
		if (pngTexture.LoadImage(raw))
			return pngTexture;
		return null;
	}

	public static T LoadFromDisc<T>(String name, ref String[] memoriaInfo, String archiveName)
	{
		/*
		Types used by the game by default:
		 * TextAsset (many, both text and binaries, including sounds and musics) - Use LoadString / LoadBytes instead
		 * Texture2D (LoadAsync is used in MBG) - Can be read as PNG/JPG
		 * Texture (only used by ModelFactory for "GEO_MAIN_F3_ZDN", "GEO_MAIN_F4_ZDN" and "GEO_MAIN_F5_ZDN") - Can be read as PNG/JPG as Texture2D
		 * RenderTexture (Usually split into many pieces) - Can't be read from disc currently
		 * Material - Can't be read from disc currently
		 * AnimationClip (LoadAll is used in AnimationFactory) - Can be read as .anim (serialized format, as in the p0data5.bin) or JSON but with simple readers
		 * GameObject (Usually split into many pieces ; LoadAsync is used in WMWorld) - Can't be read from disc currently
		*/
		if (typeof(T) == typeof(String))
		{
			return (T)(Object)File.ReadAllText(name);
		}
		else if (typeof(T) == typeof(Byte[]))
		{
			return (T)(Object)File.ReadAllBytes(name);
		}
		else if (typeof(T) == typeof(Texture2D) || typeof(T) == typeof(Texture))
		{
			Byte[] raw = File.ReadAllBytes(name);
			Texture2D newTexture = LoadTextureGeneric(raw);
			if (newTexture == null)
				newTexture = new Texture2D(1, 1, DefaultTextureFormat, false);
			ApplyTextureGenericMemoriaInfo<Texture2D>(ref newTexture, ref memoriaInfo);
			return (T)(Object)newTexture;
		}
		else if (typeof(T) == typeof(Sprite))
		{
			Byte[] raw = File.ReadAllBytes(name);
			Texture2D newTexture = LoadTextureGeneric(raw);
			if (newTexture == null)
				newTexture = new Texture2D(1, 1, DefaultTextureFormat, false);
			ApplyTextureGenericMemoriaInfo<Texture2D>(ref newTexture, ref memoriaInfo);
			Sprite newSprite = Sprite.Create(newTexture, new Rect(0, 0, newTexture.width, newTexture.height), new Vector2(0.5f, 0.5f));
			return (T)(Object)newSprite;
		}
		else if (typeof(T) == typeof(UIAtlas))
		{
			// Todo: Maybe avoid the call of Resources.Load<UIAtlas> if a complete .tpsheet is available and it's not necessary to get the original's sprite list
			UIAtlas newAtlas = Resources.Load<UIAtlas>(archiveName);
			if (newAtlas != null)
				newAtlas.ReadFromDisc(name);
			else
				Log.Message("[AssetManager] Embeded asset not found: " + archiveName);
			return (T)(Object)newAtlas;
		}
		else if (typeof(T) == typeof(AnimationClip))
		{
			return (T)(Object)AnimationClipReader.ReadAnimationClipFromDisc(name);
		}
		Log.Message("[AssetManager] Trying to load from disc the asset " + name + " of type " + typeof(T).ToString() + ", which is not currently possible");
		return (T)(Object)null;
	}

	public static T Load<T>(String name, out String[] info, Boolean suppressMissingError = false) where T : UnityEngine.Object
	{
		String infoFileName = Path.ChangeExtension(name, MemoriaInfoExtension);
		info = new String[0];
		T result;
		if (AssetManagerForObb.IsUseOBB)
			return AssetManagerForObb.Load<T>(name, suppressMissingError);
		if (AssetManagerUtil.IsMemoriaAssets(name))
		{
			foreach (AssetFolder modfold in Folder)
				if (File.Exists(modfold.FolderPath + name))
				{
					if (File.Exists(modfold.FolderPath + infoFileName))
						info = File.ReadAllLines(modfold.FolderPath + infoFileName);
					return LoadFromDisc<T>(modfold.FolderPath + name, ref info, name);
				}
			if (File.Exists(name))
			{
				if (File.Exists(infoFileName))
					info = File.ReadAllLines(infoFileName);
				return LoadFromDisc<T>(name, ref info, name);
			}
			if (!suppressMissingError)
				Log.Message("[AssetManager] Memoria asset not found: " + name);
			return null;
		}
		if (!UseBundles || AssetManagerUtil.IsEmbededAssets(name))
		{
			foreach (AssetFolder modfold in Folder)
				if (File.Exists(modfold.FolderPath + AssetManagerUtil.GetResourcesAssetsPath(true) + "/" + name))
				{
					if (File.Exists(modfold.FolderPath + AssetManagerUtil.GetResourcesAssetsPath(true) + "/" + infoFileName))
						info = File.ReadAllLines(modfold.FolderPath + AssetManagerUtil.GetResourcesAssetsPath(true) + "/" + infoFileName);
					return LoadFromDisc<T>(modfold.FolderPath + AssetManagerUtil.GetResourcesAssetsPath(true) + "/" + name, ref info, name);
				}
			result = Resources.Load<T>(name);
			if (result == (UnityEngine.Object)null && !suppressMissingError)
				Log.Message("[AssetManager] Embeded asset not found: " + name);
			return result;
		}
		String belongingBundleFilename = AssetManagerUtil.GetBelongingBundleFilename(name);
		if (!String.IsNullOrEmpty(belongingBundleFilename))
		{
			String nameInBundle = AssetManagerUtil.GetResourcesBasePath() + name + AssetManagerUtil.GetAssetExtension<T>(name);
			foreach (AssetFolder modfold in Folder)
			{
				if (File.Exists(modfold.FolderPath + AssetManagerUtil.GetStreamingAssetsPath() + "/" + nameInBundle))
				{
					if (File.Exists(modfold.FolderPath + AssetManagerUtil.GetStreamingAssetsPath() + "/" + AssetManagerUtil.GetResourcesBasePath() + infoFileName))
						info = File.ReadAllLines(modfold.FolderPath + AssetManagerUtil.GetStreamingAssetsPath() + "/" + AssetManagerUtil.GetResourcesBasePath() + infoFileName);
					return LoadFromDisc<T>(modfold.FolderPath + AssetManagerUtil.GetStreamingAssetsPath() + "/" + nameInBundle, ref info, nameInBundle);
				}
				AssetBundleRef assetBundleRef = null;
				modfold.DictAssetBundleRefs.TryGetValue(belongingBundleFilename, out assetBundleRef);
				if (assetBundleRef != null && assetBundleRef.assetBundle != (UnityEngine.Object)null)
				{
					result = assetBundleRef.assetBundle.LoadAsset<T>(nameInBundle);
					if (result != (UnityEngine.Object)null)
					{
						return result;
					}
				}
			}
		}
		if (ForceUseBundles)
			return (T)((Object)null);
		foreach (AssetFolder modfold in Folder)
			if (File.Exists(modfold.FolderPath + AssetManagerUtil.GetResourcesAssetsPath(true) + "/" + name))
			{
				if (File.Exists(modfold.FolderPath + AssetManagerUtil.GetResourcesAssetsPath(true) + "/" + infoFileName))
					info = File.ReadAllLines(modfold.FolderPath + AssetManagerUtil.GetResourcesAssetsPath(true) + "/" + infoFileName);
				return LoadFromDisc<T>(modfold.FolderPath + AssetManagerUtil.GetResourcesAssetsPath(true) + "/" + name, ref info, name);
			}
		result = Resources.Load<T>(name);
		if (result == (UnityEngine.Object)null && !suppressMissingError)
			Log.Message("[AssetManager] Asset not found: " + name);
		return result;
	}
	
	public static String LoadString(String name, out String[] info, Boolean suppressMissingError = false)
	{
		String infoFileName = Path.ChangeExtension(name, MemoriaInfoExtension);
		TextAsset txt = null;
		info = new String[0];
		if (AssetManagerForObb.IsUseOBB)
		{
			txt = AssetManagerForObb.Load<TextAsset>(name, suppressMissingError);
			if (txt != (UnityEngine.Object)null)
				return txt.text;
			return null;
		}
		if (AssetManagerUtil.IsMemoriaAssets(name))
		{
			foreach (AssetFolder modfold in Folder)
				if (File.Exists(modfold.FolderPath + name))
				{
					if (File.Exists(modfold.FolderPath + infoFileName))
						info = File.ReadAllLines(modfold.FolderPath + infoFileName);
					return LoadFromDisc<String>(modfold.FolderPath + name, ref info, name);
				}
			if (File.Exists(name))
			{
				if (File.Exists(infoFileName))
					info = File.ReadAllLines(infoFileName);
				return LoadFromDisc<String>(name, ref info, name);
			}
			if (!suppressMissingError)
				Log.Message("[AssetManager] Memoria asset not found: " + name);
			return null;
		}
		if (!UseBundles || AssetManagerUtil.IsEmbededAssets(name))
		{
			foreach (AssetFolder modfold in Folder)
				if (File.Exists(modfold.FolderPath + AssetManagerUtil.GetResourcesAssetsPath(true) + "/" + name))
				{
					if (File.Exists(modfold.FolderPath + AssetManagerUtil.GetResourcesAssetsPath(true) + "/" + infoFileName))
						info = File.ReadAllLines(modfold.FolderPath + AssetManagerUtil.GetResourcesAssetsPath(true) + "/" + infoFileName);
					return LoadFromDisc<String>(modfold.FolderPath + AssetManagerUtil.GetResourcesAssetsPath(true) + "/" + name, ref info, name);
				}
			txt = Resources.Load<TextAsset>(name);
			if (txt != (UnityEngine.Object)null)
				return txt.text;
			if (!suppressMissingError)
				Log.Message("[AssetManager] Embeded asset not found: " + name);
			return null;
		}
		String belongingBundleFilename = AssetManagerUtil.GetBelongingBundleFilename(name);
		if (!String.IsNullOrEmpty(belongingBundleFilename))
		{
			String nameInBundle = AssetManagerUtil.GetResourcesBasePath() + name + AssetManagerUtil.GetAssetExtension<TextAsset>(name);
			foreach (AssetFolder modfold in Folder)
			{
				if (File.Exists(modfold.FolderPath + AssetManagerUtil.GetStreamingAssetsPath() + "/" + nameInBundle))
				{
					if (File.Exists(modfold.FolderPath + AssetManagerUtil.GetStreamingAssetsPath() + "/" + AssetManagerUtil.GetResourcesBasePath() + infoFileName))
						info = File.ReadAllLines(modfold.FolderPath + AssetManagerUtil.GetStreamingAssetsPath() + "/" + AssetManagerUtil.GetResourcesBasePath() + infoFileName);
					return LoadFromDisc<String>(modfold.FolderPath + AssetManagerUtil.GetStreamingAssetsPath() + "/" + nameInBundle, ref info, nameInBundle);
				}
				AssetBundleRef assetBundleRef = null;
				modfold.DictAssetBundleRefs.TryGetValue(belongingBundleFilename, out assetBundleRef);
				if (assetBundleRef != null && assetBundleRef.assetBundle != (UnityEngine.Object)null)
				{
					txt = assetBundleRef.assetBundle.LoadAsset<TextAsset>(nameInBundle);
					if (txt != (UnityEngine.Object)null)
						return txt.text;
				}
			}
		}
		if (ForceUseBundles)
			return null;
		foreach (AssetFolder modfold in Folder)
			if (File.Exists(modfold.FolderPath + AssetManagerUtil.GetResourcesAssetsPath(true) + "/" + name))
			{
				if (File.Exists(modfold.FolderPath + AssetManagerUtil.GetResourcesAssetsPath(true) + "/" + infoFileName))
					info = File.ReadAllLines(modfold.FolderPath + AssetManagerUtil.GetResourcesAssetsPath(true) + "/" + infoFileName);
				return LoadFromDisc<String>(modfold.FolderPath + AssetManagerUtil.GetResourcesAssetsPath(true) + "/" + name, ref info, name);
			}
		txt = Resources.Load<TextAsset>(name);
		if (txt != (UnityEngine.Object)null)
			return txt.text;
		if (!suppressMissingError)
			Log.Message("[AssetManager] Asset not found: " + name);
		return null;
	}
	
	public static Byte[] LoadBytes(String name, out String[] info, Boolean suppressMissingError = false)
	{
		String infoFileName = Path.ChangeExtension(name, MemoriaInfoExtension);
		TextAsset txt = null;
		info = new String[0];
		if (AssetManagerForObb.IsUseOBB)
		{
			txt = AssetManagerForObb.Load<TextAsset>(name, suppressMissingError);
			if (txt != (UnityEngine.Object)null)
				return txt.bytes;
			return null;
		}
		if (AssetManagerUtil.IsMemoriaAssets(name))
		{
			foreach (AssetFolder modfold in Folder)
				if (File.Exists(modfold.FolderPath + name))
				{
					if (File.Exists(modfold.FolderPath + infoFileName))
						info = File.ReadAllLines(modfold.FolderPath + infoFileName);
					return LoadFromDisc<Byte[]>(modfold.FolderPath + name, ref info, name);
				}
			if (File.Exists(name))
			{
				if (File.Exists(infoFileName))
					info = File.ReadAllLines(infoFileName);
				return LoadFromDisc<Byte[]>(name, ref info, name);
			}
			if (!suppressMissingError)
				Log.Message("[AssetManager] Memoria asset not found: " + name);
			return null;
		}
		if (!UseBundles || AssetManagerUtil.IsEmbededAssets(name))
		{
			foreach (AssetFolder modfold in Folder)
				if (File.Exists(modfold.FolderPath + AssetManagerUtil.GetResourcesAssetsPath(true) + "/" + name))
				{
					if (File.Exists(modfold.FolderPath + AssetManagerUtil.GetResourcesAssetsPath(true) + "/" + infoFileName))
						info = File.ReadAllLines(modfold.FolderPath + AssetManagerUtil.GetResourcesAssetsPath(true) + "/" + infoFileName);
					return LoadFromDisc<Byte[]>(modfold.FolderPath + AssetManagerUtil.GetResourcesAssetsPath(true) + "/" + name, ref info, name);
				}
			txt = Resources.Load<TextAsset>(name);
			if (txt != (UnityEngine.Object)null)
				return txt.bytes;
			if (!suppressMissingError)
				Log.Message("[AssetManager] Embeded asset not found: " + name);
			return null;
		}
		String belongingBundleFilename = AssetManagerUtil.GetBelongingBundleFilename(name);
		if (!String.IsNullOrEmpty(belongingBundleFilename))
		{
			String nameInBundle = AssetManagerUtil.GetResourcesBasePath() + name + AssetManagerUtil.GetAssetExtension<TextAsset>(name);
			foreach (AssetFolder modfold in Folder)
			{
				if (File.Exists(modfold.FolderPath + AssetManagerUtil.GetStreamingAssetsPath() + "/" + nameInBundle))
				{
					if (File.Exists(modfold.FolderPath + AssetManagerUtil.GetStreamingAssetsPath() + "/" + AssetManagerUtil.GetResourcesBasePath() + infoFileName))
						info = File.ReadAllLines(modfold.FolderPath + AssetManagerUtil.GetStreamingAssetsPath() + "/" + AssetManagerUtil.GetResourcesBasePath() + infoFileName);
					return LoadFromDisc<Byte[]>(modfold.FolderPath + AssetManagerUtil.GetStreamingAssetsPath() + "/" + nameInBundle, ref info, nameInBundle);
				}
				AssetBundleRef assetBundleRef = null;
				modfold.DictAssetBundleRefs.TryGetValue(belongingBundleFilename, out assetBundleRef);
				if (assetBundleRef != null && assetBundleRef.assetBundle != (UnityEngine.Object)null)
				{
					txt = assetBundleRef.assetBundle.LoadAsset<TextAsset>(nameInBundle);
					if (txt != (UnityEngine.Object)null)
						return txt.bytes;
				}
			}
		}
		if (ForceUseBundles)
			return null;
		foreach (AssetFolder modfold in Folder)
			if (File.Exists(modfold.FolderPath + AssetManagerUtil.GetResourcesAssetsPath(true) + "/" + name))
			{
				if (File.Exists(modfold.FolderPath + AssetManagerUtil.GetResourcesAssetsPath(true) + "/" + infoFileName))
					info = File.ReadAllLines(modfold.FolderPath + AssetManagerUtil.GetResourcesAssetsPath(true) + "/" + infoFileName);
				return LoadFromDisc<Byte[]>(modfold.FolderPath + AssetManagerUtil.GetResourcesAssetsPath(true) + "/" + name, ref info, name);
			}
		txt = Resources.Load<TextAsset>(name);
		if (txt != (UnityEngine.Object)null)
			return txt.bytes;
		if (!suppressMissingError)
			Log.Message("[AssetManager] Asset not found: " + name);
		return null;
	}

	public static AssetManagerRequest LoadAsync<T>(String name) where T : UnityEngine.Object
	{
		if (AssetManagerForObb.IsUseOBB)
			return AssetManagerForObb.LoadAsync<T>(name);
		if (!UseBundles || AssetManagerUtil.IsEmbededAssets(name))
		{
			ResourceRequest resourceRequest = Resources.LoadAsync<T>(name);
			if (resourceRequest != null)
				return new AssetManagerRequest(resourceRequest, (AssetBundleRequest)null);
		}
		String belongingBundleFilename = AssetManagerUtil.GetBelongingBundleFilename(name);
		if (!String.IsNullOrEmpty(belongingBundleFilename))
		{
			String nameInBundle = AssetManagerUtil.GetResourcesBasePath() + name + AssetManagerUtil.GetAssetExtension<T>(name);
			foreach (AssetFolder modfold in Folder)
			{
				AssetBundleRef assetBundleRef = null;
				modfold.DictAssetBundleRefs.TryGetValue(belongingBundleFilename, out assetBundleRef);
				if (assetBundleRef != null && assetBundleRef.assetBundle != (UnityEngine.Object)null)
				{
					AssetBundleRequest assetBundleRequest = assetBundleRef.assetBundle.LoadAssetAsync(nameInBundle);
					if (assetBundleRequest != null)
						return new AssetManagerRequest((ResourceRequest)null, assetBundleRequest);
				}
			}
		}
		if (ForceUseBundles)
			return (AssetManagerRequest)null;
		ResourceRequest resourceRequest2 = Resources.LoadAsync<T>(name);
		if (resourceRequest2 != null)
			return new AssetManagerRequest(resourceRequest2, (AssetBundleRequest)null);
		Log.Message("[AssetManager] Asset not found: " + name);
		return (AssetManagerRequest)null;
	}

	public static String SearchAssetOnDisc(String name, Boolean includeAssetPath, Boolean includeAssetExtension)
	{
		if (!UseBundles || AssetManagerUtil.IsEmbededAssets(name))
		{
			foreach (AssetFolder modfold in Folder)
				if (File.Exists(modfold.FolderPath + (includeAssetPath ? AssetManagerUtil.GetResourcesAssetsPath(true) + "/" : "") + name))
					return modfold.FolderPath + (includeAssetPath ? AssetManagerUtil.GetResourcesAssetsPath(true) + "/" : "") + name;
			return String.Empty;
		}
		String belongingBundleFilename = AssetManagerUtil.GetBelongingBundleFilename(name);
		if (!String.IsNullOrEmpty(belongingBundleFilename))
		{
			String nameInBundle = (includeAssetPath ? AssetManagerUtil.GetStreamingAssetsPath() + "/" + AssetManagerUtil.GetResourcesBasePath() : "") + name + (includeAssetExtension ? AssetManagerUtil.GetAssetExtension<TextAsset>(name) : "");
			foreach (AssetFolder modfold in Folder)
				if (File.Exists(modfold.FolderPath + nameInBundle))
					return modfold.FolderPath + nameInBundle;
		}
		if (ForceUseBundles)
			return String.Empty;
		foreach (AssetFolder modfold in Folder)
			if (File.Exists(modfold.FolderPath + (includeAssetPath ? AssetManagerUtil.GetResourcesAssetsPath(true) : "") + "/" + name))
				return modfold.FolderPath + (includeAssetPath ? AssetManagerUtil.GetResourcesAssetsPath(true) : "") + "/" + name;
		return String.Empty;
	}

	public static Boolean HasAssetOnDisc(String name, Boolean includeAssetPath, Boolean includeAssetExtension)
	{
		return !String.IsNullOrEmpty(SearchAssetOnDisc(name, includeAssetPath, includeAssetExtension));
	}

	public static T[] LoadAll<T>(String name) where T : UnityEngine.Object
	{
		if (AssetManagerForObb.IsUseOBB)
		{
			return AssetManagerForObb.LoadAll<T>(name);
		}
		if (typeof(T) != typeof(AnimationClip))
		{
			return null;
		}
		if (!UseBundles)
		{
			name = AnimationFactory.GetRenameAnimationDirectory(name);
			return Resources.LoadAll<T>(name);
		}
		if (AnimationInFolder.ContainsKey(name))
		{
			List<String> list = AnimationInFolder[name];
			T[] array = new T[list.Count];
			for (Int32 i = 0; i < list.Count; i++)
			{
				String renameAnimationPath = AnimationFactory.GetRenameAnimationPath(list[i]);
				array[i] = Load<T>(renameAnimationPath, out _, false);
				AnimationClip clip = array[i] as AnimationClip;
				if (clip != null)
				{
					if (String.Compare(clip.name, "CUSTOM_MUST_RENAME") == 0)
						clip.name = Path.GetFileNameWithoutExtension(list[i]);
				}
			}
			return array;
		}
		return null;
	}

	// TODO: Maybe remove completly that ".memnfo" feature:
	//  UIAtlas are better handled by .tpsheet
	//  BTL_SCENE are better handled by BattlePatch.txt
	//  The features it offers for textures, sounds and musics are not offered by any other system except modifying the binary files themselves, so it could be kept at least for that if it is useful
	public const String MemoriaInfoExtension = ".memnfo";

	public const TextureFormat DefaultTextureFormat = TextureFormat.ARGB32;

	public static Boolean UseBundles;

	public static Boolean ForceUseBundles;

	public static AssetFolder[] Folder;

	public static Dictionary<String, List<String>> AnimationInFolder;

	public static Dictionary<String, String> AnimationReverseFolder;

	public class AssetBundleRef
	{
		public AssetBundleRef(String strUrl, Int32 intVersionIn)
		{
			this.fullUrl = strUrl;
			this.version = intVersionIn;
		}

		public AssetBundle assetBundle;

		public Int32 version;

		public String fullUrl;
	}
	
	public class AssetFolder
	{
		public AssetFolder(String path)
		{
			this.FolderPath = path;
			this.DictAssetBundleRefs = new Dictionary<String, AssetBundleRef>();
		}

		public String FolderPath;

		public Dictionary<String, AssetBundleRef> DictAssetBundleRefs;
	}
}
