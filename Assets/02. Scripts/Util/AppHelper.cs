using System;
using System.Threading;
using DG.Tweening;
using SystemEnums;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Singleton<T> where T : class, new()
{
    private static T _instance;
    public static T Instance
    {
        get
        {
            return _instance ?? (_instance = new T());
        }
    }
}

public class AppHelper : MonoBehaviour
{
    const int SceneTargetFrameRate = 120;

    private static bool _shouldQuit = false;
    private static int _canQuit = 0;

    public static bool CanQuit
    {
        get => _canQuit <= 0;
        set
        {
            if (value)
            {
                Interlocked.Increment(ref _canQuit);
            }
            else
            {
                Interlocked.Decrement(ref _canQuit);
            }
        }
    }

    void Awake()
    {
        if (transform.parent != null)
        {
            DontDestroyOnLoad(transform.parent.gameObject);
        }
        else
        {
            DontDestroyOnLoad(gameObject);
        }

        SetFrameLimit();
    }


    void Start()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;

        App.OnSceneLoad -= OnSceneLoad;
        App.OnSceneLoad += OnSceneLoad;

        Application.wantsToQuit -= OnQuit;
        Application.wantsToQuit += OnQuit;

    }

    private void Update()
    {
        if (_shouldQuit && CanQuit)
        {
            Application.Quit();
            return;
        }
    }

    public static void LoadScene(EScene sceneName, LoadSceneMode mode = LoadSceneMode.Single)
    {
        SceneManager.LoadScene((int)sceneName, mode);
    }

    public static void OnSceneLoad(EScene sceneName)
    {
        DOTween.KillAll();
    }

    public static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SetFrameLimit();

        if (scene.buildIndex >= 0 && scene.buildIndex < Enum.GetNames(typeof(EScene)).Length)
        {
            var loadedScene = (EScene)scene.buildIndex;
            App.BootstrapScene(loadedScene);
            App.OnSceneLoaded?.Invoke(loadedScene);
        }
    }

    public static bool OnQuit()
    {
        if (_shouldQuit)
        {
            return CanQuit;
        }

        try { App.OnQuit?.Invoke(); }
        catch (Exception e)
        {
            Debug.LogException(e);
            return true;
        }

        _shouldQuit = true;
        return CanQuit;
    }

    public static void SetFrameLimit()
    {
        //TODO : if wants to change frame data or platform specific frame rate, change here
        // now only set frame rate to 120
        var frameLimit = SystemInfo.deviceType switch
        {
            DeviceType.Handheld => SceneTargetFrameRate,
            DeviceType.Desktop => SceneTargetFrameRate,
            _ => SceneTargetFrameRate,
        };

        Application.targetFrameRate = frameLimit;
    }
}

