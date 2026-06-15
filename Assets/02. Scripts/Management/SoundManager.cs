using System;
using System.Collections;
using System.Collections.Generic;
using SystemEnums;
using UnityEngine;

[DefaultExecutionOrder((int)EExecutionOrder.BaseManagement)]
public class SoundManager : CommonManagerBase
{
    private const int InitialSFXPoolSize = 10;

    [Header("Options")]
    [SerializeField] private bool _forceMute;

    [Header("BGM")]
    [SerializeField] private AudioSource _bgmSource;

    [Header("SFX Pool")]
    [SerializeField] private GameObject _sfxSourcePrefab;

    private readonly List<AudioSource> _sfxPool = new();
    private AudioSource _loopingSfxSource;

    const float BgmFadeDuration = 0.5f;
    Coroutine _bgmFadeCoroutine;
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
        StopBgmFadeCoroutine();
    }

    void StopBgmFadeCoroutine()
    {
        if (_bgmFadeCoroutine != null)
        {
            StopCoroutine(_bgmFadeCoroutine);
            _bgmFadeCoroutine = null;
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
        _bgmFadeCoroutine = StartCoroutine(FadeBgmVolume(0f, 1f, BgmFadeDuration, null));
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

    // AssetManager.GetAudioClip already caches; no second cache here.
    private void LoadClip(EAudioClip clip, Action<AudioClip> onLoaded)
    {
        onLoaded?.Invoke(App.SystemManager.Asset.GetAudioClip(clip));
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

            StopBgmFadeCoroutine();

            if (_bgmSource.isPlaying)
            {
                _bgmFadeCoroutine = StartCoroutine(FadeBgmVolume(_bgmSource.volume, 0f, BgmFadeDuration, () =>
                {
                    _bgmSource.Stop();
                    _bgmSource.volume = 1f;
                    StartBgmFadeIn(audioClip, clip);
                }));
            }
            else
            {
                StartBgmFadeIn(audioClip, clip);
            }
        });
    }

    public void StopBGM()
    {
        StopBgmFadeCoroutine();

        if (!_bgmSource.isPlaying)
        {
            _activeBgmClip = EAudioClip.None;
            return;
        }

        _bgmFadeCoroutine = StartCoroutine(FadeBgmVolume(_bgmSource.volume, 0f, BgmFadeDuration, ResetBgmSource));
    }

    public void StopBGMImmediate()
    {
        StopBgmFadeCoroutine();
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

    IEnumerator FadeBgmVolume(float from, float to, float duration, Action onComplete)
    {
        if (duration <= 0f)
        {
            onComplete?.Invoke();
            _bgmFadeCoroutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            _bgmSource.volume = Mathf.Lerp(from, to, t);
            yield return null;
        }

        _bgmSource.volume = to;
        onComplete?.Invoke();
        _bgmFadeCoroutine = null;
    }
}
