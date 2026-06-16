using System.Collections.Generic;
using SystemEnums;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

[DefaultExecutionOrder((int)EExecutionOrder.SystemManagement)]
public class AssetManager : CommonManagerBase
{
    readonly Dictionary<EAudioClip, AudioClip> _audioCache = new();
    readonly Dictionary<string, Object> _assetCache = new();

    public AudioClip GetAudioClip(EAudioClip clip)
    {
        if (clip == EAudioClip.None)
        {
            return null;
        }

        if (_audioCache.TryGetValue(clip, out AudioClip cached))
        {
            return cached;
        }

        string address = clip.ToString();
        AsyncOperationHandle<AudioClip> handle = Addressables.LoadAssetAsync<AudioClip>(address);
        AudioClip loaded = handle.WaitForCompletion();

        if (loaded != null)
        {
            _audioCache[clip] = loaded;
        }
        else
        {
            Debug.LogWarning($"[AssetManager] 오디오 클립을 찾지 못했습니다: {address}");
        }

        return loaded;
    }

    public bool TryGetAsset<T>(string address, out T asset) where T : Object
    {
        asset = null;

        if (string.IsNullOrWhiteSpace(address))
            return false;

        if (_assetCache.TryGetValue(address, out Object cached))
        {
            asset = cached as T;
            if (asset != null)
                return true;

            Debug.LogWarning($"[AssetManager] Cached asset type mismatch. address: {address}, requested: {typeof(T).Name}, cached: {cached.GetType().Name}");
            return false;
        }

        AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(address);
        T loaded = handle.WaitForCompletion();

        if (loaded == null)
        {
            Debug.LogWarning($"[AssetManager] Asset not found. address: {address}, type: {typeof(T).Name}");
            return false;
        }

        _assetCache[address] = loaded;
        asset = loaded;
        return true;
    }
}
