using System;
using UnityEngine;
using UnityEngine.Events;

using SystemEnums;

public class App : Singleton<App>
{
    #region Managers
    private readonly UIManagerBase _uiManager;
    private readonly SceneManagerBase _sceneManager;
    private readonly TitleManager _titleManager;
    private readonly SoundManager _soundManager;
    private readonly AssetManager _assetManager;
    private readonly InputManager _inputManager;
    private readonly LobbyManager _lobbyManager;
    private readonly PlayerManager _playerManager;
    private readonly InGameManager _inGameManager;
    private readonly NetworkManager _networkManager;
    #endregion

    #region Data
    private readonly DataManager _dataManager;
    #endregion

    #region Utilities
    private readonly FrameChecker _fps;
    #endregion

    #region Platform
    public static RuntimePlatform Platform => Application.platform;

    public static bool IsWindows => Platform switch
    {
        RuntimePlatform.WindowsPlayer or
        RuntimePlatform.WindowsEditor => true,
        _ => false
    };

    public static bool IsIOS => Platform switch
    {
        RuntimePlatform.IPhonePlayer => true,
        _ => false
    };

    public static bool IsAndroid => Platform switch
    {
        RuntimePlatform.Android => true,
        _ => false
    };

    public static bool IsStandalone => IsWindows;
    public static bool IsMobile => IsIOS || IsAndroid;
    #endregion

    #region Event
    public static UnityAction<EScene> OnSceneLoad;

    public static UnityAction<EScene> OnSceneLoaded;

    public static UnityAction OnQuit;
    #endregion

    #region Scene
    public static EScene PrevScene { get; private set; }
    public static EScene CurrentScene { get; private set; }
    public static bool IsSceneLoading { get; private set; }

    public static bool IsGameScene => CurrentScene == EScene.InGame;

    public static void BootstrapScene(EScene scene)
    {
        PrevScene = scene;
        CurrentScene = scene;
    }

    public static void LoadScene(EScene sceneName)
    {
        try { OnSceneLoad?.Invoke(sceneName); }
        catch (Exception e)
        {
            Debug.LogException(e);
        }

        IsSceneLoading = true;

        AppHelper.LoadScene(sceneName);

        PrevScene = CurrentScene;
        CurrentScene = sceneName;

        IsSceneLoading = false;
    }
    #endregion

    public static class SystemManager
    {
        public static SoundManager Sound => Instance._soundManager;
        public static AssetManager Asset => Instance._assetManager;
        public static InputManager Input => Instance._inputManager;
        public static NetworkManager Network => Instance._networkManager;
    }


    public static class SceneManager
    {
        public static SceneManagerBase Current => Instance._sceneManager;
        public static TitleManager Title => Instance._titleManager;
        public static LobbyManager Lobby => Instance._lobbyManager;
        public static InGameManager InGame => Instance._inGameManager;
    }

    public static class Game
    {
        public static PlayerManager Players => Instance._playerManager;
    }

    public static class UI
    {
        public static UIManagerBase Current => Instance._uiManager;

        public static InGameUIManager InGame => Instance._uiManager as InGameUIManager;
        public static LobbyUIManager Lobby => Instance._uiManager as LobbyUIManager;
        public static TitleUIManager Title => Instance._uiManager as TitleUIManager;
    }

    public static class Utility
    {
        public static FrameChecker FPS => Instance._fps;
    }

    public static class Data
    {
        public static DataManager BaseData => Instance._dataManager;
    }
}
