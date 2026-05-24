using System;

namespace SystemEnums
{
    public enum EScene
    {
        Title,
        Lobby,
        InGame,
    }

    public enum ELobbyState
    {
        Disconnected,
        InLobby,
        Starting,
    }

    public enum EExecutionOrder
    {
        None,
        BaseManagement,
        SystemManagement,
        UIManagement,
        GameManagement,
        SystemHandler,
        GameContent,
    }

    public enum EUIType
    {
        None,
        Title,
        LobbyEntry,
        LobbyRoom,
    }

    /// <summary>
    /// 오디오 클립 식별자. Addressables 주소는 열거형 이름과 동일합니다.
    /// </summary>
    public enum EAudioClip
    {
        None,
        BGM_MAIN,
        BGM_NEWS,
    }
}
