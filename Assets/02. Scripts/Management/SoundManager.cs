using System;
using System.Collections.Generic;
using DG.Tweening;
using SystemEnums;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

[DefaultExecutionOrder((int)EExecutionOrder.BaseManagement)]
public class SoundManager : CommonManagerBase
{
    private const int InitialSFXPoolSize = 10;

    public static readonly EAudioClip[] AttackSfx =
    {
        EAudioClip.SFX_Attack_01,
        EAudioClip.SFX_Attack_02,
        EAudioClip.SFX_Attack_03,
    };

    public static readonly EAudioClip[] KoSfx =
    {
        EAudioClip.SFX_KO_01,
        EAudioClip.SFX_KO_02,
        EAudioClip.SFX_KO_03,
        EAudioClip.SFX_KO_04,
    };

    [Header("Options")]
    [SerializeField] private bool _forceMute;

    [Header("BGM")]
    [SerializeField] private AudioSource _bgmSource;

    [Header("SFX Pool")]
    [SerializeField] private GameObject _sfxSourcePrefab;

    private readonly List<AudioSource> _sfxPool = new();
    private AudioSource _loopingSfxSource;
    private readonly Dictionary<EAudioClip, AudioClip> _clipCache = new();

    const float BgmFadeDuration = 0.5f;
    Tween _bgmFadeTween;
    EAudioClip _activeBgmClip = EAudioClip.None;

    public bool ForceMute
    {
        get => _forceMute;
        set
        {
            if (_forceMute == value)
                return;
            _forceMute = value;
            ApplyForceMute();
        }
    }

    protected override void Awake()
    {
        base.Awake();
        InitAudioSources();
        ApplyForceMute();
    }

    void OnDestroy()
    {
        KillBgmFadeTween();
        if (_bgmSource != null)
            _bgmSource.DOKill();
    }

    void KillBgmFadeTween()
    {
        if (_bgmFadeTween != null)
        {
            _bgmFadeTween.Kill();
            _bgmFadeTween = null;
        }
    }

    void StartBgmFadeIn(AudioClip audioClip, EAudioClip clipEnum)
    {
        if (audioClip == null)
        {
            return;
        }

        _bgmSource.clip = audioClip;
        _activeBgmClip = clipEnum;
        _bgmSource.volume = 0f;
        _bgmSource.Play();
        _bgmFadeTween = _bgmSource.DOFade(1f, BgmFadeDuration).SetUpdate(true);
    }

    private void ApplyForceMute()
    {
        if (_bgmSource != null)
        {
            _bgmSource.mute = _forceMute;
        }

        foreach (AudioSource source in _sfxPool)
        {
            source.mute = _forceMute;
        }

        if (_loopingSfxSource != null)
        {
            _loopingSfxSource.mute = _forceMute;
        }
    }

    public void PlayLoopingSfx(EAudioClip clip)
    {
        LoadClip(clip, audioClip =>
        {
            if (audioClip == null)
            {
                return;
            }

            if (_loopingSfxSource.isPlaying)
            {
                _loopingSfxSource.Stop();
            }

            _loopingSfxSource.clip = audioClip;
            _loopingSfxSource.Play();
        });
    }

    public void StopLoopingSfx()
    {
        if (_loopingSfxSource.isPlaying)
        {
            _loopingSfxSource.Stop();
        }

        _loopingSfxSource.clip = null;
    }

    private void InitAudioSources()
    {
        for (int i = 0; i < InitialSFXPoolSize; i++)
        {
            _sfxPool.Add(CreateSFXSource());
        }

        var loopingGo = new GameObject("LoopingSFX");
        loopingGo.transform.SetParent(transform);
        _loopingSfxSource = loopingGo.AddComponent<AudioSource>();
        _loopingSfxSource.playOnAwake = false;
        _loopingSfxSource.loop = true;
        _loopingSfxSource.mute = _forceMute;
    }

    private AudioSource CreateSFXSource()
    {
        GameObject go = Instantiate(_sfxSourcePrefab, transform);
        AudioSource source = go.GetComponent<AudioSource>();

        source.playOnAwake = false;
        source.loop = false;
        source.mute = _forceMute;

        return source;
    }

    private void LoadClip(EAudioClip clip, Action<AudioClip> onLoaded)
    {
        onLoaded?.Invoke(GetClip(clip));
    }

    private AudioClip GetClip(EAudioClip clip)
    {
        if (clip == EAudioClip.None)
            return null;

        if (_clipCache.TryGetValue(clip, out AudioClip cached))
            return cached;

        string address = clip.ToString();
        AsyncOperationHandle<AudioClip> handle = Addressables.LoadAssetAsync<AudioClip>(address);
        AudioClip loaded = handle.WaitForCompletion();

        if (loaded != null)
            _clipCache[clip] = loaded;
        else
            Debug.LogWarning($"[SoundManager] 오디오 클립을 찾지 못했습니다: {address}");

        return loaded;
    }

    public void PlayBGM(EAudioClip clip)
    {
        if (clip == EAudioClip.None)
        {
            return;
        }

        LoadClip(clip, audioClip =>
        {
            if (audioClip == null)
            {
                return;
            }

            if (clip == _activeBgmClip && _bgmSource.isPlaying && _bgmSource.volume >= 0.99f)
            {
                return;
            }

            KillBgmFadeTween();

            if (_bgmSource.isPlaying)
            {
                _bgmFadeTween = _bgmSource.DOFade(0f, BgmFadeDuration)
                    .SetUpdate(true)
                    .OnComplete(() =>
                    {
                        _bgmSource.Stop();
                        StartBgmFadeIn(audioClip, clip);
                    });
            }
            else
            {
                StartBgmFadeIn(audioClip, clip);
            }
        });
    }

    public void StopBGM()
    {
        KillBgmFadeTween();

        if (!_bgmSource.isPlaying)
        {
            _activeBgmClip = EAudioClip.None;
            return;
        }

        _bgmFadeTween = _bgmSource.DOFade(0f, BgmFadeDuration)
            .SetUpdate(true)
            .OnComplete(ResetBgmSource);
    }

    public void StopBGMImmediate()
    {
        KillBgmFadeTween();
        ResetBgmSource();
    }

    void ResetBgmSource()
    {
        _bgmSource.Stop();
        _activeBgmClip = EAudioClip.None;
        _bgmSource.volume = 1f;
    }

    private AudioSource GetFreeSFXSource()
    {
        foreach (var source in _sfxPool)
        {
            if (!source.isPlaying)
            {
                return source;
            }
        }

        var newSource = CreateSFXSource();
        _sfxPool.Add(newSource);
        return newSource;
    }

    public void PlaySFX(EAudioClip clip)
    {
        LoadClip(clip, audioClip =>
        {
            if (audioClip == null) return;

            GetFreeSFXSource().PlayOneShot(audioClip);
        });
    }

    public void PlayRandomSFX(params EAudioClip[] clips)
    {
        if (clips == null || clips.Length == 0) return;
        PlaySFX(clips[UnityEngine.Random.Range(0, clips.Length)]);
    }

    public void StopAllSFX()
    {
        foreach (var source in _sfxPool)
        {
            if (source.isPlaying)
            {
                source.Stop();
            }
        }

        StopLoopingSfx();
    }
}
