using System.Collections.Generic;
using SystemEnums;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

[DefaultExecutionOrder((int)EExecutionOrder.SystemManagement)]
public class AssetManager : CommonManagerBase
{
    readonly Dictionary<EAudioClip, AudioClip> _audioCache = new();

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
}
